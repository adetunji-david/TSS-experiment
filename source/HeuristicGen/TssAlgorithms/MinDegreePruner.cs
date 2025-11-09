using HeuristicGen.Network;

namespace HeuristicGen.TssAlgorithms;

/* Summary:
 * This class implements the Minimum-Degree Heuristic (Reverse-MDG) algorithm.
 * It takes a valid solution candidate for the Target Set Selection problem
 * and greedily eliminates vertices with the lowest degree.
 *
 * Reference:
 * Doerr, Benjamin, Martin S. Krejca, and Nguyen Vu. "Superior genetic algorithms
 * for the target set selection problem based on power-law parameter choices and
 * simple greedy heuristics."
 * Proceedings of the Genetic and Evolutionary Computation Conference. 2024
 */
public static class MinDegreePruner
{
    public static HashSet<int> Prune(Graph graph, HashSet<int> targetSet)
    {
        var prunnedSet = new HashSet<int>(targetSet, targetSet.Comparer);
        var nodes = targetSet.ToArray();
        var indices = Enumerable.Range(0, nodes.Length).ToArray();
        var degrees = nodes.Select(node => graph.Degrees[node]).ToArray();
        Array.Sort(degrees, indices);
        foreach (var index in indices)
        {
            var node = nodes[index];
            prunnedSet.Remove(node);
            var diffuser = new Diffuser(graph);
            var activatedNodeCount = diffuser.ActivateNodes(prunnedSet).Count;
            if (activatedNodeCount != graph.NodeCount)
            {
                prunnedSet.Add(node);
            }
        }

        return prunnedSet;
    }
}