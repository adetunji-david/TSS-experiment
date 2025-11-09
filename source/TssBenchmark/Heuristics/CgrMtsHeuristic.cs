using TssBenchmark.Network;
using TssBenchmark.Util;

namespace TssBenchmark.Heuristics;

/* Summary:
 * This class implements the MTS algorithm, which provides an approximate
 * solution to the problem of selecting a minimum subset of nodes in a
 * network that can activate all other nodes.
 *
 * Reference:
 * Cordasco, Gennaro, Luisa Gargano, and Adele A. Rescigno.
 * "On finding small sets that influence large networks."
 * Social Network Analysis and Mining 6 (2016): 1-20.
 */
public sealed class CgrMtsHeuristic : ITssHeuristic
{
    public HashSet<int> FindTargetSet(Graph graph)
    {
        var adjacencyList = graph.AdjacencyList;
        var targetSet = new HashSet<int>();
        var lSet = new HashSet<int>();
        var uSet = new HashSet<int>(Enumerable.Range(0, graph.NodeCount));
        var ks = graph.Thresholds.ToArray();
        var deltas = graph.Degrees.ToArray();
        var nodesWithZeroK = new HashSet<int>();
        var nodesWithDeltaLessK = new HashSet<int>();
        var priorityQueue = new UpdatableMaxPriorityQueue(graph.NodeCount);

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
                    var vNotInlSet = !lSet.Contains(v);
                    foreach (var neighbor in adjacencyList[v])
                    {
                        if (uSet.Contains(neighbor))
                        {
                            ks[neighbor] = int.Max(ks[neighbor] - 1, 0);
                            if (vNotInlSet)
                            {
                                deltas[neighbor]--;
                            }

                            MaintainDataStructuresInvariants(neighbor);
                        }
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
                if (uSet.Contains(v) && !lSet.Contains(v))
                {
                    targetSet.Add(v);
                    foreach (var neighbor in adjacencyList[v])
                    {
                        if (uSet.Contains(neighbor))
                        {
                            ks[neighbor]--;
                            deltas[neighbor]--;
                            MaintainDataStructuresInvariants(neighbor);
                        }
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
                // ReSharper disable once CanSimplifySetAddingWithSingleCall
                if (uSet.Contains(v) && !lSet.Contains(v))
                {
                    foreach (var neighbor in adjacencyList[v])
                    {
                        if (uSet.Contains(neighbor))
                        {
                            deltas[neighbor]--;
                            MaintainDataStructuresInvariants(neighbor);
                        }
                    }

                    lSet.Add(v);
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

            if (uSet.Contains(node) && !lSet.Contains(node))
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