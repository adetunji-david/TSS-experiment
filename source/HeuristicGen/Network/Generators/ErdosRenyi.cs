using HeuristicGen.Rng;

namespace HeuristicGen.Network.Generators;

public sealed class ErdosRenyi : IGraphGenerator
{
    public int NodeCount { get; }
    public double EdgeProbability { get; }

    public ErdosRenyi(int nodeCount, double edgeProbability)
    {
        NodeCount = nodeCount;
        EdgeProbability = edgeProbability;
    }


    public Graph Sample(Pcg64 rng)
    {
        var adjacencyList = new HashSet<int>[NodeCount];
        for (var i = 0; i < NodeCount; i++)
        {
            adjacencyList[i] = [];
        }

        var log1Mp = Math.Log(1.0 - Math.Min(Math.Max(0.0, EdgeProbability), 1.0));
        if (Math.Abs(log1Mp) > 0.0)
        {
            for (var u = 0; u <= NodeCount - 2; u++)
            {
                var v = u + 1;
                while (v < NodeCount)
                {
                    var r = 1.0 - rng.NextDouble();
                    v += (int)Math.Floor(Math.Log(r) / log1Mp);
                    if (v >= NodeCount)
                    {
                        break;
                    }

                    adjacencyList[u].Add(v);
                    adjacencyList[v].Add(u);
                    v += 1;
                }
            }
        }

        return new Graph(adjacencyList);
    }
}