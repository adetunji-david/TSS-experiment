using HeuristicGen.Rng;

namespace HeuristicGen.Network.Generators;

internal interface IGraphGenerator
{
    Graph Sample(Pcg64 rng);
}