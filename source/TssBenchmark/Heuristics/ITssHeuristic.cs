using TssBenchmark.Network;

namespace TssBenchmark.Heuristics;

public interface ITssHeuristic
{
    HashSet<int> FindTargetSet(Graph graph);
}