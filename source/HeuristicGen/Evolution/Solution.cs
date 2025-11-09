using System.Linq.Expressions;
using HeuristicGen.Network;
using HeuristicGen.Util;

namespace HeuristicGen.Evolution;

public sealed class DerivationNode
{
    public Symbol Symbol { get; }
    public int RuleIndex { get; set; }
    public int Depth { get; set; }
    public DerivationNode[] ChildNodes { get; set; }

    public DerivationNode(Symbol symbol, int ruleIndex = -1, DerivationNode[]? childNodes = null, int depth = 0)
    {
        Symbol = symbol;
        RuleIndex = ruleIndex;
        Depth = depth;
        ChildNodes = childNodes ?? [];
    }

    public void Deconstruct(out Symbol symbol, out int ruleIndex, out DerivationNode[] childNodes,
        out int depth)
    {
        symbol = Symbol;
        ruleIndex = RuleIndex;
        childNodes = ChildNodes;
        depth = Depth;
    }
}

public sealed class Solution
{
    // To determine if expressions need parentheses when converting a solution into a readable string
    private enum OpType
    {
        ConstantOrVariable,
        FunctionCall,
        InfixAdditive,
        InfixMultiplicative
    }

    private const int MaxWidth = 90;
    private readonly Func<int, EvaluationContext, double> _compiledFunction;
    private readonly PrettyString _expressionString;

    public DerivationNode DerivationTreeRoot { get; }

    public List<DerivationNode> Nodes { get; }

    public int ExpressionDepth { get; }

    public int ProgramLength { get; }

    public Solution(DerivationNode derivationTreeRoot)
    {
        DerivationTreeRoot = derivationTreeRoot;
        (Nodes, ExpressionDepth, ProgramLength) = WalkTree(derivationTreeRoot);
        (_compiledFunction, _expressionString) = Compile(Nodes);
    }

    public override string ToString() => _expressionString.ToString();

    public string ToString(string prefix) => _expressionString.ToString(prefix);

    public int EvaluateOn(Graph graph)
    {
        var targetSet = new HashSet<int>();
        var nodeCount = graph.NodeCount;
        var diffuser = new Diffuser(graph);
        var adjacencyList = graph.AdjacencyList;
        var inactiveNodes = new HashSet<int>(Enumerable.Range(0, nodeCount));
        var inactiveNeighbors = new HashSet<int>[nodeCount];
        for (var i = 0; i < nodeCount; i++)
        {
            var hashSet = adjacencyList[i];
            inactiveNeighbors[i] = new HashSet<int>(hashSet, hashSet.Comparer);
        }

        var evaluationContext = new EvaluationContext(
            diffuser.ActiveNeighborsCounts, graph.Degrees, graph.Thresholds, adjacencyList, inactiveNeighbors);

        if (graph.ZeroThresholdNodes.Length > 0)
        {
            targetSet.AddMany(graph.ZeroThresholdNodes);
            var activatedNodes = diffuser.ActivateNodes(graph.ZeroThresholdNodes);
            inactiveNodes.RemoveMany(activatedNodes);
            foreach (var activatedNode in activatedNodes)
            {
                foreach (var neighbor in adjacencyList[activatedNode])
                {
                    inactiveNeighbors[neighbor].Remove(activatedNode);
                }
            }
        }

        var queue = new UpdatableMaxPriorityQueue(nodeCount);
        foreach (var node in inactiveNodes)
        {
            var priority = _compiledFunction(node, evaluationContext);
            queue.EnqueueOrUpdate(node, priority);
        }

        while (inactiveNodes.Count > 0)
        {
            var (node, _) = queue.Dequeue();
            targetSet.Add(node);
            var activatedNodes = diffuser.ActivateNode(node);
            inactiveNodes.RemoveMany(activatedNodes);
            var inactiveNeighborsOfActivatedNodes = new HashSet<int>();
            foreach (var activatedNode in activatedNodes)
            {
                queue.Remove(activatedNode);
                foreach (var neighbor in inactiveNeighbors[activatedNode])
                {
                    // The grammar does not give access to activated nodes,
                    // so we can get away with not updating their data. 
                    if (inactiveNodes.Contains(neighbor))
                    {
                        inactiveNeighborsOfActivatedNodes.Add(neighbor);
                        inactiveNeighbors[neighbor].Remove(activatedNode);
                    }
                }
            }

            foreach (var inactiveNeighbor in inactiveNeighborsOfActivatedNodes)
            {
                var priority = _compiledFunction(inactiveNeighbor, evaluationContext);
                queue.EnqueueOrUpdate(inactiveNeighbor, priority);
            }
        }

        return targetSet.Count;
        // return MinDegreePrunner.Prune(graph, targetSet).Count;
    }

    private static (List<DerivationNode> Nodes, int MaxDepthSeen, int ProgramLength) WalkTree(DerivationNode root)
    {
        var maxDepthSeen = 0;
        var programLength = 0;
        var scopeCounter = 0;
        var nodes = new List<DerivationNode>();
        var stack = new Stack<DerivationNode>(32);
        root.Depth = 0;
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            nodes.Add(node);
            var (symbol, _, childNodes, depthFromRoot) = node;
            maxDepthSeen = int.Max(maxDepthSeen, depthFromRoot);
            if (symbol.IsTerminal())
            {
                if (symbol is Symbol.AggregationScopeMarker)
                {
                    scopeCounter++;
                }
                else if (symbol.IsAggregateOperation())
                {
                    if (--scopeCounter == 0)
                    {
                        programLength++;
                    }
                }
                else
                {
                    programLength++;
                }
            }

            foreach (var childNode in childNodes)
            {
                childNode.Depth = depthFromRoot + 1;
                stack.Push(childNode);
            }
        }

        return (nodes, maxDepthSeen, programLength);
    }

    private static (Func<int, EvaluationContext, double>, PrettyString) Compile(IEnumerable<DerivationNode> nodes)
    {
        var stringBuilder = new Stack<(OpType, PrettyString)>();
        var stack = new Stack<Expression>();
        var scopeCounter = 0;
        var evalCtxParam = Expression.Parameter(typeof(EvaluationContext));
        var nodeParam = Expression.Parameter(typeof(int));
        var neighborVar = Expression.Variable(typeof(int));
        var nodeVariableInCurrentScope = nodeParam;
        var nodeNameInCurrentScope = "node";

        foreach (var node in nodes)
        {
            var symbol = node.Symbol;
            if (symbol.IsNonTerminal())
            {
                continue;
            }

            PrettyString leftString, rightString, topString, resultString;
            OpType leftOpType, rightOpType;
            Expression left, right, top, collection;
            ParameterExpression resultVar;
            string str;
            switch (symbol)
            {
                case Symbol.AggregationScopeMarker:
                    if (++scopeCounter == 1)
                    {
                        neighborVar = Expression.Variable(typeof(int));
                        nodeVariableInCurrentScope = neighborVar;
                        nodeNameInCurrentScope = "neighbor";
                    }

                    break;
                case Symbol.Add:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Add(left, right));
                    (_, leftString) = stringBuilder.Pop();
                    (_, rightString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "", " + ", "", leftString, rightString);
                    stringBuilder.Push((OpType.InfixAdditive, resultString));
                    break;
                case Symbol.Subtract:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Subtract(left, right));
                    (_, leftString) = stringBuilder.Pop();
                    (rightOpType, rightString) = stringBuilder.Pop();
                    if (rightOpType is OpType.InfixAdditive)
                    {
                        rightString = PrettyString.Combine(MaxWidth, "(", "", ")", rightString);
                    }

                    resultString = PrettyString.Combine(MaxWidth, "", " - ", "", leftString, rightString);
                    stringBuilder.Push((OpType.InfixAdditive, resultString));
                    break;
                case Symbol.Multiply:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Multiply(left, right));
                    (leftOpType, leftString) = stringBuilder.Pop();
                    (rightOpType, rightString) = stringBuilder.Pop();
                    if (leftOpType is OpType.InfixAdditive)
                    {
                        leftString = PrettyString.Combine(MaxWidth, "(", "", ")", leftString);
                    }

                    if (rightOpType is OpType.InfixAdditive)
                    {
                        rightString = PrettyString.Combine(MaxWidth, "(", "", ")", rightString);
                    }

                    resultString = PrettyString.Combine(MaxWidth, "", " * ", "", leftString, rightString);
                    stringBuilder.Push((OpType.InfixMultiplicative, resultString));
                    break;
                case Symbol.Divide:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Divide, left, right));
                    (leftOpType, leftString) = stringBuilder.Pop();
                    (rightOpType, rightString) = stringBuilder.Pop();
                    if (leftOpType is OpType.InfixAdditive)
                    {
                        leftString = PrettyString.Combine(MaxWidth, "(", "", ")", leftString);
                    }

                    if (rightOpType is OpType.InfixAdditive or OpType.InfixMultiplicative)
                    {
                        rightString = PrettyString.Combine(MaxWidth, "(", "", ")", rightString);
                    }

                    resultString = PrettyString.Combine(MaxWidth, "", " / ", "", leftString, rightString);
                    stringBuilder.Push((OpType.InfixMultiplicative, resultString));
                    break;
                case Symbol.Pow:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Pow, left, right));
                    (_, leftString) = stringBuilder.Pop();
                    (_, rightString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "pow(", ", ", ")", leftString, rightString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.Maximum:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Maximum, left, right));
                    (_, leftString) = stringBuilder.Pop();
                    (_, rightString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "max(", ", ", ")", leftString, rightString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.Minimum:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Minimum, left, right));
                    (_, leftString) = stringBuilder.Pop();
                    (_, rightString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "min(", ", ", ")", leftString, rightString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.Negate:
                    top = stack.Pop();
                    stack.Push(Expression.Negate(top));
                    (var topOpType, topString) = stringBuilder.Pop();
                    if (topOpType is OpType.InfixAdditive)
                    {
                        topString = PrettyString.Combine(MaxWidth, "(", "", ")", topString);
                    }

                    resultString = PrettyString.Combine(MaxWidth, "-", "", "", topString);
                    stringBuilder.Push((topOpType, resultString));
                    break;
                case Symbol.Exp:
                    top = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Exp, top));
                    (_, topString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "exp(", "", ")", topString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.SquareRoot:
                    top = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.SquareRoot, top));
                    (_, topString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "sqrt(", "", ")", topString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.Log:
                    top = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Log, top));
                    (_, topString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "ln(", "", ")", topString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.Reciprocal:
                    top = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Reciprocal, top));
                    (_, topString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "reciprocal(", "", ")", topString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.Square:
                    top = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Square, top));
                    (_, topString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "square(", "", ")", topString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.Cardinality:
                    top = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Cardinality, top));
                    (_, topString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "cardinality(", "", ")", topString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.Union:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Union, left, right));
                    (_, leftString) = stringBuilder.Pop();
                    (_, rightString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "union(", ", ", ")", leftString, rightString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.Intersection:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.Intersection, left, right));
                    (_, leftString) = stringBuilder.Pop();
                    (_, rightString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "intersection(", ", ", ")", leftString, rightString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.SetDifference:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.SetDifference, left, right));
                    (_, leftString) = stringBuilder.Pop();
                    (_, rightString) = stringBuilder.Pop();
                    resultString = PrettyString.Combine(MaxWidth, "difference(", ", ", ")", leftString, rightString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.SymmetricSetDifference:
                    left = stack.Pop();
                    right = stack.Pop();
                    stack.Push(Expression.Call(null, EvaluationContext.SymmetricSetDifference, left, right));
                    (_, leftString) = stringBuilder.Pop();
                    (_, rightString) = stringBuilder.Pop();
                    resultString =
                        PrettyString.Combine(MaxWidth, "sym_difference(", ", ", ")", leftString, rightString);
                    stringBuilder.Push((OpType.FunctionCall, resultString));
                    break;
                case Symbol.SumOverInactiveNeighbors:
                    if (--scopeCounter == 0)
                    {
                        top = stack.Pop();
                        collection = Expression.ArrayAccess(Expression.PropertyOrField(evalCtxParam,
                            nameof(EvaluationContext.InactiveNeighbors)), nodeParam);
                        resultVar = Expression.Variable(typeof(double));
                        stack.Push(Expression.Block([resultVar],
                            Expression.Assign(resultVar, Expression.Constant(0.0)),
                            ExpressionTreesHelper.ForEach(collection, neighborVar, resultVar,
                                Expression.AddAssign(resultVar, top))
                        ));
                        (_, topString) = stringBuilder.Pop();
                        resultString =
                            PrettyString.Combine(MaxWidth, "sum_over_inactive_neighbors[", "", "]", topString);
                        stringBuilder.Push((OpType.FunctionCall, resultString));
                        nodeVariableInCurrentScope = nodeParam;
                        nodeNameInCurrentScope = "node";
                    }

                    break;
                case Symbol.AverageOverInactiveNeighbors:
                    if (--scopeCounter == 0)
                    {
                        top = stack.Pop();
                        collection = Expression.ArrayAccess(Expression.PropertyOrField(evalCtxParam,
                            nameof(EvaluationContext.InactiveNeighbors)), nodeParam);
                        resultVar = Expression.Variable(typeof(double));
                        stack.Push(Expression.Block([resultVar],
                            Expression.Assign(resultVar, Expression.Constant(0.0)),
                            ExpressionTreesHelper.ForEach(collection, neighborVar, resultVar,
                                Expression.AddAssign(resultVar, top)),
                            Expression.Divide(resultVar, Expression.Convert(
                                Expression.PropertyOrField(collection, "Count"),
                                typeof(double)
                            ))
                        ));
                        (_, topString) = stringBuilder.Pop();
                        resultString = PrettyString.Combine(MaxWidth, "average_over_inactive_neighbors[", "", "]",
                            topString);
                        stringBuilder.Push((OpType.FunctionCall, resultString));
                        nodeVariableInCurrentScope = nodeParam;
                        nodeNameInCurrentScope = "node";
                    }

                    break;
                case Symbol.MaximumOverInactiveNeighbors:
                    if (--scopeCounter == 0)
                    {
                        top = stack.Pop();
                        collection = Expression.ArrayAccess(Expression.PropertyOrField(evalCtxParam,
                            nameof(EvaluationContext.InactiveNeighbors)), nodeParam);
                        resultVar = Expression.Variable(typeof(double));
                        stack.Push(Expression.Block([resultVar],
                            Expression.Assign(resultVar, Expression.Constant(double.NegativeInfinity)),
                            ExpressionTreesHelper.ForEach(collection, neighborVar, resultVar,
                                Expression.Assign(resultVar,
                                    Expression.Call(null, EvaluationContext.Maximum, resultVar, top))
                            )));
                        (_, topString) = stringBuilder.Pop();
                        resultString =
                            PrettyString.Combine(MaxWidth, "max_over_inactive_neighbors[", "", "]", topString);
                        stringBuilder.Push((OpType.FunctionCall, resultString));
                        nodeVariableInCurrentScope = nodeParam;
                        nodeNameInCurrentScope = "node";
                    }

                    break;
                case Symbol.MinimumOverInactiveNeighbors:
                    if (--scopeCounter == 0)
                    {
                        top = stack.Pop();
                        collection = Expression.ArrayAccess(Expression.PropertyOrField(evalCtxParam,
                            nameof(EvaluationContext.InactiveNeighbors)), nodeParam);
                        resultVar = Expression.Variable(typeof(double));
                        stack.Push(Expression.Block([resultVar],
                            Expression.Assign(resultVar, Expression.Constant(double.PositiveInfinity)),
                            ExpressionTreesHelper.ForEach(collection, neighborVar, resultVar,
                                Expression.Assign(resultVar,
                                    Expression.Call(null, EvaluationContext.Minimum, resultVar, top))
                            )));
                        (_, topString) = stringBuilder.Pop();
                        resultString =
                            PrettyString.Combine(MaxWidth, "min_over_inactive_neighbors[", "", "]", topString);
                        stringBuilder.Push((OpType.FunctionCall, resultString));
                        nodeVariableInCurrentScope = nodeParam;
                        nodeNameInCurrentScope = "node";
                    }

                    break;
                case Symbol.DegreeInCurrentScope:
                    stack.Push(
                        Expression.Convert(
                            Expression.ArrayAccess(
                                Expression.PropertyOrField(evalCtxParam, nameof(EvaluationContext.Degrees)),
                                nodeVariableInCurrentScope),
                            typeof(double))
                    );
                    str = $"degree({nodeNameInCurrentScope})";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.ThresholdInCurrentScope:
                    stack.Push(
                        Expression.Convert(
                            Expression.ArrayAccess(
                                Expression.PropertyOrField(evalCtxParam, nameof(EvaluationContext.Thresholds)),
                                nodeVariableInCurrentScope),
                            typeof(double))
                    );
                    str = $"threshold({nodeNameInCurrentScope})";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.ActiveNeighborsCountInCurrentScope:
                    stack.Push(
                        Expression.Convert(
                            Expression.ArrayAccess(
                                Expression.PropertyOrField(evalCtxParam,
                                    nameof(EvaluationContext.ActiveNeighborsCounts)),
                                nodeVariableInCurrentScope),
                            typeof(double))
                    );
                    str = $"num_active_neighbors({nodeNameInCurrentScope})";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.DeficitInCurrentScope:
                    stack.Push(
                        Expression.Convert(
                            Expression.Call(evalCtxParam, EvaluationContext.Deficit,
                                nodeVariableInCurrentScope),
                            typeof(double))
                    );
                    str = $"deficit({nodeNameInCurrentScope})";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.InactiveNeighborsCountInCurrentScope:
                    stack.Push(
                        Expression.Convert(
                            Expression.Call(evalCtxParam, EvaluationContext.InactiveNeighborsCount,
                                nodeVariableInCurrentScope),
                            typeof(double))
                    );
                    str = $"num_inactive_neighbors({nodeNameInCurrentScope})";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.NeighborsInCurrentScope:
                    stack.Push(Expression.ArrayAccess(Expression.PropertyOrField(evalCtxParam,
                        nameof(EvaluationContext.Neighbors)), nodeVariableInCurrentScope));
                    str = $"neighbors({nodeNameInCurrentScope})";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.InactiveNeighborsInCurrentScope:
                    stack.Push(Expression.ArrayAccess(Expression.PropertyOrField(evalCtxParam,
                        nameof(EvaluationContext.InactiveNeighbors)), nodeVariableInCurrentScope));
                    str = $"inactive_neighbors({nodeNameInCurrentScope})";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.DegreeInOuterScope:
                    stack.Push(
                        Expression.Convert(
                            Expression.ArrayAccess(
                                Expression.PropertyOrField(evalCtxParam, nameof(EvaluationContext.Degrees)),
                                nodeParam),
                            typeof(double))
                    );
                    str = "degree(node)";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.ThresholdInOuterScope:
                    stack.Push(
                        Expression.Convert(
                            Expression.ArrayAccess(
                                Expression.PropertyOrField(evalCtxParam, nameof(EvaluationContext.Thresholds)),
                                nodeParam),
                            typeof(double))
                    );
                    str = "threshold(node)";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.ActiveNeighborsCountInOuterScope:
                    stack.Push(
                        Expression.Convert(
                            Expression.ArrayAccess(
                                Expression.PropertyOrField(evalCtxParam,
                                    nameof(EvaluationContext.ActiveNeighborsCounts)),
                                nodeParam),
                            typeof(double))
                    );
                    str = "num_active_neighbors(node)";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.DeficitInOuterScope:
                    stack.Push(
                        Expression.Convert(
                            Expression.Call(evalCtxParam, EvaluationContext.Deficit, nodeParam),
                            typeof(double))
                    );
                    str = "deficit(node)";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.InactiveNeighborsCountInOuterScope:
                    stack.Push(
                        Expression.Convert(
                            Expression.Call(evalCtxParam, EvaluationContext.InactiveNeighborsCount, nodeParam),
                            typeof(double))
                    );
                    str = "num_inactive_neighbors(node)";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.NeighborsInOuterScope:
                    stack.Push(Expression.ArrayAccess(Expression.PropertyOrField(evalCtxParam,
                        nameof(EvaluationContext.Neighbors)), nodeParam));
                    str = "neighbors(node)";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.InactiveNeighborsInOuterScope:
                    stack.Push(Expression.ArrayAccess(Expression.PropertyOrField(evalCtxParam,
                        nameof(EvaluationContext.InactiveNeighbors)), nodeParam));
                    str = "inactive_neighbors(node)";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.OneSixteenth:
                    stack.Push(Expression.Constant(1 / 16.0));
                    str = "0.0625";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.OneEighth:
                    stack.Push(Expression.Constant(1 / 8.0));
                    str = "0.125";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.OneQuarter:
                    stack.Push(Expression.Constant(1 / 4.0));
                    str = "0.25";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.OneHalf:
                    stack.Push(Expression.Constant(1 / 2.0));
                    str = "0.5";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.Zero:
                    stack.Push(Expression.Constant(0.0));
                    str = "0";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.One:
                    stack.Push(Expression.Constant(1.0));
                    str = "1";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.Two:
                    stack.Push(Expression.Constant(2.0));
                    str = "2";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.Four:
                    stack.Push(Expression.Constant(4.0));
                    str = "4";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.Eight:
                    stack.Push(Expression.Constant(8.0));
                    str = "8";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                case Symbol.Sixteen:
                    stack.Push(Expression.Constant(16.0));
                    str = "16";
                    stringBuilder.Push((OpType.ConstantOrVariable, new PrettyString([str])));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        var lambda = Expression.Lambda<Func<int, EvaluationContext, double>>(stack.Pop(), nodeParam, evalCtxParam);
        var (_, prettyString) = stringBuilder.Pop();
        return (lambda.Compile(), prettyString);
    }
}