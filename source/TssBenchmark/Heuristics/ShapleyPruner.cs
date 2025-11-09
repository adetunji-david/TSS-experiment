using TssBenchmark.Network;
using TssBenchmark.Util;

namespace TssBenchmark.Heuristics;

public static class ShapleyPruner
{
    public record PruneRecord(int TargetSetSize, int Round);

    public static (List<PruneRecord>, HashSet<int>, double) Prune(Graph graph, HashSet<int> targetSet, int rounds,
        IEnumerable<double> deflationFactors)
    {
        var bestDeflationFactor = 0.0;
        var bestTargetSet = targetSet;
        var bestTrace = new List<PruneRecord>();
        foreach (var deflationFactor in deflationFactors)
        {
            var (trace, prunedTargetSet) = Prune(graph, targetSet, rounds, deflationFactor);
            if (prunedTargetSet.Count <= bestTargetSet.Count)
            {
                bestDeflationFactor = deflationFactor;
                bestTargetSet = prunedTargetSet;
                bestTrace = trace;
            }
        }

        return (bestTrace, bestTargetSet, bestDeflationFactor);
    }

    private static (List<PruneRecord>, HashSet<int>) Prune(Graph graph, HashSet<int> targetSet, int rounds,
        double deflationFactor)
    {
        var trace = new List<PruneRecord>();
        var crudeShapleyValues = new double[graph.NodeCount];
        var smallestPrunnedSet = targetSet;
        var nodes = targetSet.ToArray();
        var degrees = graph.Degrees;
        var tieBreakers = new int[graph.NodeCount]; // Bandaid for Array.Sort being Unstable
        var comparer = Comparer<int>.Create((u, v) =>
        {
            var result = crudeShapleyValues[v].CompareTo(crudeShapleyValues[u]);
            if (result == 0)
            {
                result = degrees[v].CompareTo(degrees[u]);
            }

            if (result == 0)
            {
                result = tieBreakers[v].CompareTo(tieBreakers[u]);
            }

            return result;
        });

        trace.Add(new PruneRecord(smallestPrunnedSet.Count, 0));
        for (var r = 1; r <= rounds; r++)
        {
            Array.Sort(nodes, comparer);
            var diffuser = new Diffuser(graph);
            var prunnedSet = new HashSet<int>(targetSet.Count);
            var activatedNodes = new HashSet<int>();
            foreach (var node in nodes)
            {
                if (!activatedNodes.Contains(node))
                {
                    var newlyActivatedNodes = diffuser.ActivateNodes([node]);
                    activatedNodes.AddMany(newlyActivatedNodes);
                    crudeShapleyValues[node] += newlyActivatedNodes.Count;
                    prunnedSet.Add(node);
                }

                if (activatedNodes.Count == graph.NodeCount)
                {
                    break;
                }
            }

            if (prunnedSet.Count < smallestPrunnedSet.Count)
            {
                smallestPrunnedSet = prunnedSet;
                var rank = nodes.Length;
                foreach (var node in nodes)
                {
                    tieBreakers[node] = rank--;
                    crudeShapleyValues[node] *= deflationFactor;
                }
            }

            trace.Add(new PruneRecord(smallestPrunnedSet.Count, r));
        }

        return (trace, smallestPrunnedSet);
    }
}