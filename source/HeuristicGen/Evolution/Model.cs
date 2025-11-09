using HeuristicGen.Rng;

namespace HeuristicGen.Evolution;

public sealed class Model
{
    private static readonly Symbol[][] SubstitutionRules;
    private readonly int _maxDepth;
    private readonly int _tournamentSize;
    private readonly double _mixtureWeightForRules;
    private readonly double[][] _ruleWeightMatrix;

    static Model()
    {
        // Prefix-notation Expression Grammar
        var expressionSubstitutionRules = new Symbol[][]
        {
            [Symbol.SamplePopulation],
            [Symbol.Constant],
            [Symbol.NodeScalarProperty],
            [Symbol.UnaryOperation, Symbol.Expression],
            [Symbol.BinaryOperation, Symbol.Expression, Symbol.Expression],
            [Symbol.AggregateOperation, Symbol.Expression, Symbol.AggregationScopeMarker],
            [Symbol.Cardinality, Symbol.SetExpression]
        }; // 0 -- 2 -- 6

        var setExpressionSubstitutionRules = new Symbol[][]
        {
            [Symbol.NodeSetProperty],
            [Symbol.SetOperation, Symbol.SetExpression, Symbol.SetExpression]
        }; // 7 -- 7 -- 8

        var unaryOperationSubstitutionRules = new Symbol[][]
        {
            [Symbol.Negate],
            [Symbol.Exp],
            [Symbol.SquareRoot],
            [Symbol.Square],
            [Symbol.Log],
            [Symbol.Reciprocal]
        }; // 9 -- 14

        var binaryOperationSubstitutionRules = new Symbol[][]
        {
            [Symbol.Add],
            [Symbol.Subtract],
            [Symbol.Multiply],
            [Symbol.Divide],
            [Symbol.Pow],
            [Symbol.Maximum],
            [Symbol.Minimum]
        }; // 15 -- 21

        var setOperationSubstitutionRules = new Symbol[][]
        {
            [Symbol.Union],
            [Symbol.Intersection],
            [Symbol.SetDifference],
            [Symbol.SymmetricSetDifference]
        }; // 22 -- 25

        var aggregateOperationSubstitutionRules = new Symbol[][]
        {
            [Symbol.SumOverInactiveNeighbors],
            [Symbol.AverageOverInactiveNeighbors],
            [Symbol.MinimumOverInactiveNeighbors],
            [Symbol.MaximumOverInactiveNeighbors]
        }; // 26 -- 29

        var nodeScalarPropertySubstitutionRules = new Symbol[][]
        {
            [Symbol.DegreeInCurrentScope],
            [Symbol.ThresholdInCurrentScope],
            [Symbol.ActiveNeighborsCountInCurrentScope],
            [Symbol.DeficitInCurrentScope],
            [Symbol.InactiveNeighborsCountInCurrentScope],
            [Symbol.DegreeInOuterScope],
            [Symbol.ThresholdInOuterScope],
            [Symbol.ActiveNeighborsCountInOuterScope],
            [Symbol.DeficitInOuterScope],
            [Symbol.InactiveNeighborsCountInOuterScope]
        }; // 30 -- 39

        var nodeSetPropertySubstitutionRules = new Symbol[][]
        {
            [Symbol.NeighborsInCurrentScope],
            [Symbol.InactiveNeighborsInCurrentScope],
            [Symbol.NeighborsInOuterScope],
            [Symbol.InactiveNeighborsInOuterScope]
        }; // 40 -- 43

        var constantSubstitutionRules = new Symbol[][]
        {
            [Symbol.OneSixteenth],
            [Symbol.OneEighth],
            [Symbol.OneQuarter],
            [Symbol.OneHalf],
            [Symbol.Zero],
            [Symbol.One],
            [Symbol.Two],
            [Symbol.Four],
            [Symbol.Eight],
            [Symbol.Sixteen]
        }; // 44 -- 53

        var sampleArchiveubstitutionRules = new Symbol[][]
        {
            [Symbol.Expression]
        }; // 54 -- 54

        SubstitutionRules =
        [
            ..expressionSubstitutionRules,
            ..setExpressionSubstitutionRules,
            ..unaryOperationSubstitutionRules,
            ..binaryOperationSubstitutionRules,
            ..setOperationSubstitutionRules,
            ..aggregateOperationSubstitutionRules,
            ..nodeScalarPropertySubstitutionRules,
            ..nodeSetPropertySubstitutionRules,
            ..constantSubstitutionRules,
            ..sampleArchiveubstitutionRules
        ];
    }

    public Model(int maxDepth, int tournamentSize, double mixtureWeightForRules)
    {
        _maxDepth = maxDepth;
        _tournamentSize = tournamentSize;
        _mixtureWeightForRules = mixtureWeightForRules;
        _ruleWeightMatrix = new double[maxDepth + 1][];
        for (var i = 0; i < _ruleWeightMatrix.Length; i++)
        {
            _ruleWeightMatrix[i] = new double[SubstitutionRules.Length];
        }
    }

    private static (int StartIndex, int EndIndex) GetSubstitutionRuleRangeFor(Symbol symbol,
        bool forbidRecursion, bool forbidPopulationSampling, bool forbidOuterScopeVariables)
    {
        return symbol switch
        {
            Symbol.Expression when forbidRecursion && forbidPopulationSampling => (1, 2),
            Symbol.Expression when forbidRecursion => (0, 2),
            Symbol.Expression when forbidPopulationSampling => (1, 5),
            Symbol.Expression => (0, 5),
            Symbol.SetExpression when forbidRecursion => (7, 7),
            Symbol.SetExpression => (7, 8),
            Symbol.UnaryOperation => (9, 14),
            Symbol.BinaryOperation => (15, 21),
            Symbol.SetOperation => (22, 25),
            Symbol.AggregateOperation => (26, 29),
            Symbol.NodeScalarProperty when forbidOuterScopeVariables => (30, 34),
            Symbol.NodeScalarProperty => (30, 39),
            Symbol.NodeSetProperty when forbidOuterScopeVariables => (40, 41),
            Symbol.NodeSetProperty => (40, 43),
            Symbol.Constant => (44, 53),
            Symbol.SamplePopulation => (54, 54),
            _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, null)
        };
    }

    private int SelectRuleIndex(Pcg64 rng, int depth, int startIndex, int endIndex)
    {
        var useRules = rng.NextDouble() <= _mixtureWeightForRules;
        if (!useRules)
        {
            return rng.Next(startIndex, endIndex + 1);
        }

        var weights = _ruleWeightMatrix[depth];
        var maxZ = double.MinValue;
        var selectedRuleIndex = startIndex;
        for (var i = startIndex; i <= endIndex; i++)
        {
            var z = Math.Log(1.0 - rng.NextDouble()) / double.Max(1e-8, weights[i]);
            if (z > maxZ)
            {
                maxZ = z;
                selectedRuleIndex = i;
            }
        }

        return selectedRuleIndex;
    }

    public static bool IsValidProgram(DerivationNode root)
    {
        var aggregationScopeCounter = 0;
        var stack = new Stack<DerivationNode>(32);
        stack.Push(root);
        while (stack.Count > 0)
        {
            var (symbol, nodeRuleIndex, childNodes, _) = stack.Pop();
            aggregationScopeCounter += symbol switch
            {
                Symbol.AggregationScopeMarker => 1,
                Symbol.AggregateOperation => -1,
                _ => 0
            };
            if (symbol.IsTerminal())
            {
                if (nodeRuleIndex != -1)
                {
                    return false;
                }

                continue;
            }

            var (startIndex, endIndex) =
                GetSubstitutionRuleRangeFor(symbol, false, false, aggregationScopeCounter <= 0);
            if (nodeRuleIndex < startIndex || nodeRuleIndex > endIndex)
            {
                return false;
            }

            var expectedChildSymbols = SubstitutionRules[nodeRuleIndex];
            if (expectedChildSymbols.Length != childNodes.Length)
            {
                return false;
            }

            for (var i = 0; i < expectedChildSymbols.Length; i++)
            {
                var child = childNodes[i];
                if (child.Symbol != expectedChildSymbols[i])
                {
                    return false;
                }

                stack.Push(child);
            }
        }

        return true;
    }

    public DerivationNode Sample(Pcg64 rng, Population? population = null)
    {
        var forbidOuterScopeVariables = true;
        var forbidPopulationSampling = population is null || population.Count == 0;
        var stack = new Stack<DerivationNode>(4 * _maxDepth);
        var root = new DerivationNode(Symbol.Expression, depth: 0);
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            var symbol = node.Symbol;
            var depth = node.Depth;
            forbidOuterScopeVariables = symbol switch
            {
                Symbol.AggregateOperation => true,
                Symbol.AggregationScopeMarker => false,
                _ => forbidOuterScopeVariables
            };
            if (symbol.IsTerminal())
            {
                continue;
            }

            var forbidRecursion = depth >= _maxDepth - 2;
            var (startIndex, endIndex) = GetSubstitutionRuleRangeFor(symbol,
                forbidRecursion, forbidPopulationSampling, forbidOuterScopeVariables);
            var selectedRuleIndex = node.RuleIndex = SelectRuleIndex(rng, depth, startIndex, endIndex);
            if (symbol is Symbol.SamplePopulation)
            {
                var count = population!.Count;
                var fitnesses = population.Fitnesses;
                var chosenSolutionIndex = rng.Next(count);
                for (var i = 2; i <= _tournamentSize; i++)
                {
                    var index = rng.Next(count);
                    if (fitnesses[index] >= fitnesses[chosenSolutionIndex])
                    {
                        chosenSolutionIndex = index;
                    }
                }

                node.ChildNodes = [population.Solutions[chosenSolutionIndex].DerivationTreeRoot];
            }
            else
            {
                var childDepth = depth + 1;
                var childSymbols = SubstitutionRules[selectedRuleIndex];
                var childNodes = node.ChildNodes = new DerivationNode[childSymbols.Length];
                for (var i = 0; i < childNodes.Length; i++)
                {
                    var childNode = childNodes[i] = new DerivationNode(childSymbols[i], depth: childDepth);
                    stack.Push(childNode);
                }
            }
        }

        return root;
    }

    public void SolutionAccepted(Solution solution)
    {
        foreach (var node in solution.Nodes)
        {
            var (symbol, ruleIndex, _, depthFromRoot) = node;
            if (symbol.IsNonTerminal() && depthFromRoot < _ruleWeightMatrix.Length)
            {
                _ruleWeightMatrix[depthFromRoot][ruleIndex]++;
            }
        }
    }

    public void SolutionEjected(Solution solution)
    {
        foreach (var node in solution.Nodes)
        {
            var (symbol, ruleIndex, _, depthFromRoot) = node;
            if (symbol.IsNonTerminal() && depthFromRoot < _ruleWeightMatrix.Length)
            {
                _ruleWeightMatrix[depthFromRoot][ruleIndex]--;
            }
        }
    }

    public void Reset()
    {
        foreach (var row in _ruleWeightMatrix)
        {
            row.AsSpan().Clear();
        }
    }
}