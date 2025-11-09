using System.Linq.Expressions;
using System.Reflection;

namespace HeuristicGen.Util;

public static class ExpressionTreesHelper
{
    public static BlockExpression ForEach(Expression collection, ParameterExpression loopVar,
        ParameterExpression resultVar, Expression loopContent)
    {
        var enumeratorVar = Expression.Variable(typeof(HashSet<int>.Enumerator), "enumerator");
        var moveNextCall = Expression.Call(enumeratorVar, MoveNextMethod);
        var breakLabel = Expression.Label(typeof(double), "LoopBreak");

        var loop = Expression.Block([enumeratorVar],
            Expression.Assign(enumeratorVar, Expression.Call(collection, GetEnumeratorMethod)),
            Expression.Loop(
                Expression.IfThenElse(
                    Expression.Equal(moveNextCall, Expression.Constant(true)),
                    Expression.Block([loopVar],
                        Expression.Assign(loopVar, Expression.Property(enumeratorVar, "Current")),
                        loopContent
                    ),
                    Expression.Break(breakLabel, resultVar)
                )
                , breakLabel)
        );

        return loop;
    }

    private static readonly MethodInfo MoveNextMethod = typeof(HashSet<int>.Enumerator).GetMethod("MoveNext")!;
    private static readonly MethodInfo GetEnumeratorMethod = typeof(HashSet<int>).GetMethod("GetEnumerator")!;
}