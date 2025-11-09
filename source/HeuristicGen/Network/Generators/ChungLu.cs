using HeuristicGen.Rng;
using HeuristicGen.Rng.Distributions;

namespace HeuristicGen.Network.Generators;

/// <summary>
/// Create a random graph with a degree sequence drawn from a truncated power law distribution.
/// Implements the algorithm described in the paper "Efficient generation of networks with
/// given expected degrees." by  Miller, Joel C., and Aric Hagberg.
/// </summary>
public sealed class ChungLu : IGraphGenerator
{
    public int NodeCount { get; }
    public int ExpectedMinDegree { get; }
    public int ExpectedMaxDegree { get; }
    public double Exponent { get; }

    public ChungLu(int nodeCount, int expectedMinDegree, int expectedMaxDegree, double exponent)
    {
        NodeCount = nodeCount;
        ExpectedMinDegree = expectedMinDegree;
        ExpectedMaxDegree = expectedMaxDegree;
        Exponent = exponent;
    }

    public Graph Sample(Pcg64 rng)
    {
        var adjacencyList = new HashSet<int>[NodeCount];
        for (var i = 0; i < NodeCount; i++)
        {
            adjacencyList[i] = [];
        }

        var weights = new double[NodeCount];
        new TruncatedPowerLaw(Exponent, ExpectedMinDegree, ExpectedMaxDegree).Fill(rng, weights);
        var totalWeight = weights.Sum();
        Array.Sort(weights);
        Array.Reverse(weights);
        for (var u = 0; u <= NodeCount - 2; u++)
        {
            var v = u + 1;
            var p = Math.Min(1.0, weights[u] * weights[v] / totalWeight);
            var log1Mp = Math.Log(1.0 - p);
            while (v < NodeCount && p > 0.0)
            {
                var r = 1.0 - rng.NextDouble();
                v += (int)Math.Floor(Math.Log(r) / log1Mp);
                if (v >= NodeCount)
                {
                    continue;
                }

                var q = Math.Min(1.0, weights[u] * weights[v] / totalWeight);
                if (rng.NextDouble() < q / p)
                {
                    adjacencyList[u].Add(v);
                    adjacencyList[v].Add(u);
                }

                v += 1;
                p = q;
                log1Mp = Math.Log(1.0 - p);
            }
        }

        return new Graph(adjacencyList);
    }
}