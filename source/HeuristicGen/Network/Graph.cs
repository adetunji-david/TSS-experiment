namespace HeuristicGen.Network;

public sealed class Graph
{
    public HashSet<int>[] AdjacencyList { get; }
    public int NodeCount { get; }
    public int EdgeCount { get; }
    public int[] Degrees { get; }
    public int[] Thresholds { get; }

    public int[] ZeroThresholdNodes { get; }

    public Graph(HashSet<int>[] adjacencyList)
    {
        AdjacencyList = adjacencyList;
        NodeCount = adjacencyList.Length;
        Degrees = adjacencyList.Select(neighbors => neighbors.Count).ToArray();
        Thresholds = Degrees.Select(d => (int)Math.Ceiling(d / 2.0)).ToArray();
        EdgeCount = Degrees.Sum();
        ZeroThresholdNodes = Enumerable.Range(0, NodeCount).Where(i => Thresholds[i] == 0).ToArray();
    }
}