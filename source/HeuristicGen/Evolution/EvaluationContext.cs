using System.Reflection;

namespace HeuristicGen.Evolution;

public sealed class EvaluationContext
{
    private const double NaNReplacement = double.NegativeInfinity;
    public int[] ActiveNeighborsCounts { get; }
    public int[] Degrees { get; }
    public int[] Thresholds { get; }
    public HashSet<int>[] Neighbors { get; }
    public HashSet<int>[] InactiveNeighbors { get; }

    public EvaluationContext(int[] activeNeighborsCounts, int[] degrees, int[] thresholds,
        HashSet<int>[] neighbors, HashSet<int>[] inactiveNeighbors)
    {
        ActiveNeighborsCounts = activeNeighborsCounts;
        Degrees = degrees;
        Thresholds = thresholds;
        Neighbors = neighbors;
        InactiveNeighbors = inactiveNeighbors;
    }

    public int DeficitImpl(int node) => int.Max(0, Thresholds[node] - ActiveNeighborsCounts[node]);
    public int InactiveNeighborsCountImpl(int node) => Degrees[node] - ActiveNeighborsCounts[node];

    static EvaluationContext()
    {
        Deficit = typeof(EvaluationContext).GetMethod(nameof(DeficitImpl))!;
        InactiveNeighborsCount = typeof(EvaluationContext).GetMethod(nameof(InactiveNeighborsCountImpl))!;
        Exp = typeof(Math).GetMethod(nameof(Math.Exp), [typeof(double)])!;
        Maximum = typeof(Math).GetMethod(nameof(Math.Max), [typeof(double), typeof(double)])!;
        Minimum = typeof(Math).GetMethod(nameof(Math.Min), [typeof(double), typeof(double)])!;
        const BindingFlags bf = BindingFlags.NonPublic | BindingFlags.Static;
        Divide = typeof(EvaluationContext).GetMethod(nameof(DivideImpl), bf)!;
        Reciprocal = typeof(EvaluationContext).GetMethod(nameof(ReciprocalImpl), bf)!;
        Log = typeof(EvaluationContext).GetMethod(nameof(LogImpl), bf)!;
        Pow = typeof(EvaluationContext).GetMethod(nameof(PowImpl), bf)!;
        Square = typeof(EvaluationContext).GetMethod(nameof(SquareImpl), bf)!;
        SquareRoot = typeof(EvaluationContext).GetMethod(nameof(SquareRootImpl), bf)!;
        Cardinality = typeof(EvaluationContext).GetMethod(nameof(CardinalityImpl), bf)!;
        Union = typeof(EvaluationContext).GetMethod(nameof(UnionImpl), bf)!;
        Intersection = typeof(EvaluationContext).GetMethod(nameof(IntersectionImpl), bf)!;
        SetDifference = typeof(EvaluationContext).GetMethod(nameof(SetDifferenceImpl), bf)!;
        SymmetricSetDifference = typeof(EvaluationContext).GetMethod(nameof(SymmetricSetDifferenceImpl), bf)!;
    }

    public static MethodInfo Deficit { get; }
    public static MethodInfo InactiveNeighborsCount { get; }
    public static MethodInfo Divide { get; }
    public static MethodInfo Reciprocal { get; }
    public static MethodInfo Pow { get; }
    public static MethodInfo Log { get; }
    public static MethodInfo Exp { get; }
    public static MethodInfo SquareRoot { get; }
    public static MethodInfo Square { get; }
    public static MethodInfo Cardinality { get; }
    public static MethodInfo Union { get; }
    public static MethodInfo Intersection { get; }
    public static MethodInfo SetDifference { get; }
    public static MethodInfo SymmetricSetDifference { get; }
    public static MethodInfo Maximum { get; }
    public static MethodInfo Minimum { get; }

    private static double SquareImpl(double a) => a * a;

    private static double SquareRootImpl(double a)
    {
        var result = Math.Sqrt(a);
        return double.IsNaN(result) ? NaNReplacement : result;
    }

    private static double ReciprocalImpl(double a)
    {
        var result = 1.0 / a;
        return double.IsNaN(result) ? NaNReplacement : result;
    }

    private static double DivideImpl(double a, double b)
    {
        var result = a / b;
        return double.IsNaN(result) ? NaNReplacement : result;
    }

    private static double LogImpl(double a)
    {
        var result = Math.Log(a);
        return double.IsNaN(result) ? NaNReplacement : result;
    }

    private static double PowImpl(double a, double b)
    {
        var result = Math.Pow(a, b);
        return double.IsNaN(result) ? NaNReplacement : result;
    }

    private static double CardinalityImpl(HashSet<int> hashSet) => hashSet.Count;

    private static HashSet<int> UnionImpl(HashSet<int> setA, HashSet<int> setB)
    {
        if (setA.Count > setB.Count)
        {
            var setC = new HashSet<int>(setA, setA.Comparer);
            setC.UnionWith(setB);
            return setC;
        }
        else
        {
            var setC = new HashSet<int>(setB, setB.Comparer);
            setC.UnionWith(setA);
            return setC;
        }
    }

    private static HashSet<int> IntersectionImpl(HashSet<int> setA, HashSet<int> setB)
    {
        if (setA.Count < setB.Count)
        {
            var setC = new HashSet<int>(setA, setA.Comparer);
            setC.IntersectWith(setB);
            return setC;
        }
        else
        {
            var setC = new HashSet<int>(setB, setB.Comparer);
            setC.UnionWith(setA);
            return setC;
        }
    }

    private static HashSet<int> SetDifferenceImpl(HashSet<int> setA, HashSet<int> setB)
    {
        var setC = new HashSet<int>(setA, setA.Comparer);
        setC.ExceptWith(setB);
        return setC;
    }

    private static HashSet<int> SymmetricSetDifferenceImpl(HashSet<int> setA, HashSet<int> setB)
    {
        var setC = new HashSet<int>(setA, setA.Comparer);
        setC.SymmetricExceptWith(setB);
        return setC;
    }
}