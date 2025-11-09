using System.Diagnostics;

namespace HeuristicGen.Network;

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

    public HashSet<int> ActivateNode(int node) => ActivateNodes([node]);

    public HashSet<int> ActivateNodes(IEnumerable<int> nodes)
    {
        var stack = new Stack<int>();
        var activatedNodes = new HashSet<int>();
        var adjacencyList = _graph.AdjacencyList;
        foreach (var node in nodes)
        {
            Debug.Assert(node >= 0 && node < _graph.NodeCount);
            if (!_isActive[node])
            {
                _isActive[node] = true;
                stack.Push(node);
                activatedNodes.Add(node);
            }
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            foreach (var neighbor in adjacencyList[node])
            {
                ActiveNeighborsCounts[neighbor] += 1;
                if (_isActive[neighbor] || ActiveNeighborsCounts[neighbor] < _thresholds[neighbor])
                {
                    continue;
                }

                _isActive[neighbor] = true;
                stack.Push(neighbor);
                activatedNodes.Add(neighbor);
            }
        }

        return activatedNodes;
    }
}