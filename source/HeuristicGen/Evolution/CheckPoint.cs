namespace HeuristicGen.Evolution;

public sealed class Checkpoint
{
    public required int Iteration { get; init; }
    public required Memory<Solution> Solutions { get; init; }
}