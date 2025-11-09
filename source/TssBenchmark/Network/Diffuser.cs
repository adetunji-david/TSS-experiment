using System.Diagnostics;

namespace TssBenchmark.Network;

public sealed class Diffuser
{
    private readonly int[] _thresholds;
    private readonly Graph _graph;
    private readonly bool[] _isActive;

    public int[] ActiveNeighborsCounts { get; }

    public Diffuser(Graph graph)
    {
        _graph = graph;
        _thresholds = graph.Thresholds;
        _isActive = new bool[_thresholds.Length];
        ActiveNeighborsCounts = new int[graph.NodeCount];
    }

    /// <summary>
    /// Activates a collection of nodes and returns a list of all nodes that were activated, 
    /// including any additional nodes that were activated as a result of the diffusion process.
    /// </summary>
    /// <param name="nodes">The collection of nodes to be activated.</param>
    /// <returns>
    /// A list of nodes that were activated, comprising the input nodes 
    /// and any additional nodes activated through diffusion.
    /// </returns>
    public List<int> ActivateNodes(IEnumerable<int> nodes)
    {
        var queue = new Queue<int>();
        var activatedNodes = new List<int>();
        var adjacencyList = _graph.AdjacencyList;
        foreach (var node in nodes)
        {
            Debug.Assert(node >= 0 && node < _graph.NodeCount);
            if (!_isActive[node])
            {
                _isActive[node] = true;
                queue.Enqueue(node);
                activatedNodes.Add(node);
            }
        }

        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var neighbor in adjacencyList[node])
            {
                ActiveNeighborsCounts[neighbor]++;
                if (_isActive[neighbor] || ActiveNeighborsCounts[neighbor] < _thresholds[neighbor])
                {
                    continue;
                }

                _isActive[neighbor] = true;
                queue.Enqueue(neighbor);
                activatedNodes.Add(neighbor);
            }
        }

        return activatedNodes;
    }
}