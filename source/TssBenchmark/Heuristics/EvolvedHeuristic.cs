using TssBenchmark.Network;
using TssBenchmark.Util;

namespace TssBenchmark.Heuristics;

public sealed class EvolvedHeuristic : ITssHeuristic
{
    public sealed class EvaluationContext
    {
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

        public int Deficit(int node) => int.Max(0, Thresholds[node] - ActiveNeighborsCounts[node]);
        public int InactiveNeighborsCount(int node) => Degrees[node] - ActiveNeighborsCounts[node];
    }

    public HashSet<int> FindTargetSet(Graph graph)
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
            var priority = ComputePriority(node, evaluationContext);
            queue.EnqueueOrUpdate(node, priority);
        }

        while (inactiveNodes.Count > 0)
        {
            var (node, _) = queue.Dequeue();
            targetSet.Add(node);
            var activatedNodes = diffuser.ActivateNodes([node]);
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
                var priority = ComputePriority(inactiveNeighbor, evaluationContext);
                queue.EnqueueOrUpdate(inactiveNeighbor, priority);
            }
        }

        return targetSet;
    }

    private static double ComputePriority(int node, EvaluationContext evaluationContext)
    {
        double priority = evaluationContext.Deficit(node);
        foreach (var inactiveNeighbor in evaluationContext.InactiveNeighbors[node])
        {
            var a = evaluationContext.InactiveNeighborsCount(inactiveNeighbor);
            var b = evaluationContext.Deficit(inactiveNeighbor);
            priority += (double)a / b;
        }

        return priority;
    }
}