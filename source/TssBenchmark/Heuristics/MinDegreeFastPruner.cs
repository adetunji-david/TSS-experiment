using TssBenchmark.Network;

namespace TssBenchmark.Heuristics;

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
public static class MinDegreeFastPruner
{
    public static HashSet<int> Prune(Graph graph, HashSet<int> targetSet)
    {
        var nodes = targetSet.ToArray();
        var diffuser = new Diffuser(graph);
        var activatedNodeCount = diffuser.ActivateNodes(nodes).Count;
        if (activatedNodeCount != graph.NodeCount)
        {
            throw new ArgumentException("Given set is not a valid target set", nameof(targetSet));
        }

        var prunnedSet = new HashSet<int>(targetSet);
        var indices = Enumerable.Range(0, nodes.Length).ToArray();
        var degrees = nodes.Select(node => graph.Degrees[node]).ToArray();
        Array.Sort(degrees, indices);

        var reversibleDiffuser = new ReversibleDiffuser(graph);
        foreach (var index in indices.Reverse())
        {
            reversibleDiffuser.ActivateNode(nodes[index]);
        }

        foreach (var index in indices)
        {
            var node = nodes[index];
            var deactivatedNodeCount = reversibleDiffuser.UndoActivation(node).Count;
            if (deactivatedNodeCount is 0)
            {
                prunnedSet.Remove(node);
            }
            else
            {
                reversibleDiffuser.ActivateNode(node);
            }
        }

        return prunnedSet;
    }
}