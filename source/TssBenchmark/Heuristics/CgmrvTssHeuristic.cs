using TssBenchmark.Network;
using TssBenchmark.Util;

namespace TssBenchmark.Heuristics;

/* Summary:
 * This class implements the TSS algorithm, which provides an approximate
 * solution to the problem of selecting a minimum subset of nodes in a
 * network that can activate all other nodes.
 *
 * Reference:
 * Cordasco, Gennaro, et al. "Discovering small target sets in social networks:
 * a fast and effective algorithm." Algorithmica 80 (2018): 1804-1833.
 */
public sealed class CgmrvTssHeuristic : ITssHeuristic
{
    public HashSet<int> FindTargetSet(Graph graph)
    {
        var targetSet = new HashSet<int>();
        var uSet = new HashSet<int>(Enumerable.Range(0, graph.NodeCount));
        var ks = graph.Thresholds.ToArray();
        var deltas = graph.Degrees.ToArray();
        var nodesWithZeroK = new HashSet<int>();
        var nodesWithDeltaLessK = new HashSet<int>();
        var priorityQueue = new UpdatableMaxPriorityQueue(graph.NodeCount);
        var inactiveNeighbors = new HashSet<int>[graph.NodeCount];
        for (var i = 0; i < graph.NodeCount; i++)
        {
            var hashSet = graph.AdjacencyList[i];
            inactiveNeighbors[i] = new HashSet<int>(hashSet, hashSet.Comparer);
        }

        for (var node = 0; node < graph.NodeCount; node++)
        {
            var k = ks[node];
            var delta = deltas[node];
            if (k == 0)
            {
                nodesWithZeroK.Add(node);
            }

            if (delta < k)
            {
                nodesWithDeltaLessK.Add(node);
            }

            priorityQueue.EnqueueOrUpdate(node, (double)k / (delta * (delta + 1)));
        }

        while (uSet.Count > 0)
        {
            var isCase1Satisfied = false;
            var isCase2Satisfied = false;

            while (nodesWithZeroK.Count > 0)
            {
                var v = nodesWithZeroK.RemoveFirst();
                if (uSet.Contains(v))
                {
                    foreach (var neighbor in inactiveNeighbors[v])
                    {
                        ks[neighbor] = int.Max(ks[neighbor] - 1, 0);
                        deltas[neighbor]--;
                        inactiveNeighbors[neighbor].Remove(v);
                        MaintainDataStructuresInvariants(neighbor);
                    }

                    uSet.Remove(v);
                    isCase1Satisfied = true;
                    break;
                }
            }

            if (isCase1Satisfied)
            {
                continue;
            }

            while (nodesWithDeltaLessK.Count > 0)
            {
                var v = nodesWithDeltaLessK.RemoveFirst();
                if (uSet.Contains(v))
                {
                    targetSet.Add(v);
                    foreach (var neighbor in inactiveNeighbors[v])
                    {
                        ks[neighbor]--;
                        deltas[neighbor]--;
                        inactiveNeighbors[neighbor].Remove(v);
                        MaintainDataStructuresInvariants(neighbor);
                    }

                    uSet.Remove(v);
                    isCase2Satisfied = true;
                    break;
                }
            }

            if (isCase2Satisfied)
            {
                continue;
            }

            while (priorityQueue.Count > 0)
            {
                var (v, _) = priorityQueue.Dequeue();
                if (uSet.Contains(v))
                {
                    foreach (var neighbor in inactiveNeighbors[v])
                    {
                        deltas[neighbor]--;
                        inactiveNeighbors[neighbor].Remove(v);
                        MaintainDataStructuresInvariants(neighbor);
                    }

                    uSet.Remove(v);
                    break;
                }
            }
        }

        return targetSet;

        void MaintainDataStructuresInvariants(int node)
        {
            var k = ks[node];
            var delta = deltas[node];
            if (k == 0)
            {
                nodesWithZeroK.Add(node);
            }
            else
            {
                nodesWithZeroK.Remove(node);
            }

            if (delta < k)
            {
                nodesWithDeltaLessK.Add(node);
            }
            else
            {
                nodesWithDeltaLessK.Remove(node);
            }

            if (uSet.Contains(node))
            {
                priorityQueue.EnqueueOrUpdate(node, (double)k / (delta * (delta + 1)));
            }
            else
            {
                priorityQueue.Remove(node);
            }
        }
    }
}