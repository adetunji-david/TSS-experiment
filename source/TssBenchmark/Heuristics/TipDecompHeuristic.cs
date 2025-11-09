using TssBenchmark.Network;
using TssBenchmark.Util;

namespace TssBenchmark.Heuristics
{
    /* Summary:
     * This class implements the TIP_DECOMP algorithm, which provides an approximate
     * solution to the problem of selecting a minimum subset of nodes in a
     * network that can activate all other nodes.
     *
     * Reference:
     * Shakarian, Paulo, Sean Eyre, and Damon Paulo.
     * "A scalable heuristic for viral marketing under the tipping model."
     * Social Network Analysis and Mining 3 (2013): 1225-1248.
     */
    public sealed class TipDecompHeuristic : ITssHeuristic
    {
        public HashSet<int> FindTargetSet(Graph graph)
        {
            var targetSet = new HashSet<int>(Enumerable.Range(0, graph.NodeCount));
            var priorityQueue = new UpdatableMaxPriorityQueue(graph.NodeCount);

            // The algorithm typically selects a node where (degree - threshold) is minimal.  
            // To achieve the same effect, we use a max-priority queue to select a node where
            // (threshold - degree) is maximal, as these conditions are mathematically equivalent.
            for (var node = 0; node < graph.NodeCount; node++)
            {
                priorityQueue.EnqueueOrUpdate(node, graph.Thresholds[node] - graph.Degrees[node]);
            }

            while (priorityQueue.Count > 0)
            {
                var (node, _) = priorityQueue.Dequeue();
                targetSet.Remove(node);

                foreach (var neighbor in graph.AdjacencyList[node])
                {
                    if (priorityQueue.TryGetPriority(neighbor, out var dist))
                    {
                        if (dist < 0)
                        {
                            priorityQueue.EnqueueOrUpdate(neighbor, dist + 1);
                        }
                        else
                        {
                            priorityQueue.Remove(neighbor);
                        }
                    }
                }
            }

            return targetSet;
        }
    }
}
