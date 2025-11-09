using TssBenchmark.Network;
using TssBenchmark.Util;

namespace TssBenchmark.Heuristics;

/*
 * This class implements the Maximum Degree Heuristic.
 * It constructs a valid target set S, where the diffusion process
 * starting from S eventually activates all nodes in the graph.
 * The algorithm starts with an empty set and greedily adds the node
 * with the largest degree that has not yet been activated. After each
 * addition, it checks if the set is valid, and once a valid set is found,
 * it returns the result.
 */
public sealed class MaxDegreeHeuristic : ITssHeuristic
{
    public HashSet<int> FindTargetSet(Graph graph)
    {
        var targetSet = new HashSet<int>();
        var diffuser = new Diffuser(graph);
        var inactiveNodes = new HashSet<int>(Enumerable.Range(0, graph.NodeCount));
        var priorityQueue = new UpdatableMaxPriorityQueue(graph.NodeCount);
        if (graph.ZeroThresholdNodes.Length > 0)
        {
            targetSet.AddMany(graph.ZeroThresholdNodes);
            var activatedNodes = diffuser.ActivateNodes(graph.ZeroThresholdNodes);
            inactiveNodes.RemoveMany(activatedNodes);
        }

        foreach (var node in inactiveNodes)
        {
            priorityQueue.EnqueueOrUpdate(node, graph.Degrees[node]);
        }

        while (inactiveNodes.Count > 0)
        {
            var (node, _) = priorityQueue.Dequeue();
            targetSet.Add(node);
            var activatedNodes = diffuser.ActivateNodes([node]);
            inactiveNodes.RemoveMany(activatedNodes);
            foreach (var activatedNode in activatedNodes)
            {
                priorityQueue.Remove(activatedNode);
            }
        }

        return targetSet;
    }
}