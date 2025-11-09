namespace HeuristicGen.Evolution;

public enum Symbol
{
    Expression = -1,
    SetExpression = -2,
    UnaryOperation = -3,
    BinaryOperation = -4,
    AggregateOperation = -5,
    SetOperation = -6,
    NodeScalarProperty = -8,
    NodeSetProperty = -9,
    Constant = -10,
    SamplePopulation = -11,

    AggregationScopeMarker = 1,

    Add = 2,
    Subtract = 3,
    Multiply = 4,
    Divide = 5,
    Pow = 6,
    Minimum = 7,
    Maximum = 8,

    Negate = 9,
    Exp = 10,
    SquareRoot = 11,
    Log = 12,
    Reciprocal = 13,
    Square = 14,

    Cardinality = 15,
    Union = 16,
    Intersection = 17,
    SetDifference = 18,
    SymmetricSetDifference = 19,

    SumOverInactiveNeighbors = 20,
    AverageOverInactiveNeighbors = 21,
    MaximumOverInactiveNeighbors = 22,
    MinimumOverInactiveNeighbors = 23,

    DegreeInCurrentScope = 24,
    ThresholdInCurrentScope = 25,
    ActiveNeighborsCountInCurrentScope = 26,
    DeficitInCurrentScope = 27,
    InactiveNeighborsCountInCurrentScope = 28,
    NeighborsInCurrentScope = 29,
    InactiveNeighborsInCurrentScope = 30,

    DegreeInOuterScope = 31,
    ThresholdInOuterScope = 32,
    ActiveNeighborsCountInOuterScope = 33,
    DeficitInOuterScope = 34,
    InactiveNeighborsCountInOuterScope = 35,
    NeighborsInOuterScope = 36,
    InactiveNeighborsInOuterScope = 37,

    OneSixteenth = 38,
    OneEighth = 39,
    OneQuarter = 40,
    OneHalf = 41,
    Zero = 42,
    One = 43,
    Two = 44,
    Four = 45,
    Eight = 46,
    Sixteen = 47
}

public static class SymbolExtensions
{
    public static bool IsTerminal(this Symbol symbol) => symbol >= Symbol.AggregationScopeMarker;

    public static bool IsNonTerminal(this Symbol symbol) => symbol < Symbol.AggregationScopeMarker;

    public static bool IsAggregateOperation(this Symbol symbol) => symbol is Symbol.SumOverInactiveNeighbors
        or Symbol.AverageOverInactiveNeighbors
        or Symbol.MaximumOverInactiveNeighbors or Symbol.MinimumOverInactiveNeighbors;
}