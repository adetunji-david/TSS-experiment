using System.Diagnostics;
using HeuristicGen.Network;
using HeuristicGen.Rng;

namespace HeuristicGen.Evolution;

public readonly ref struct SearchConfiguration
{
    public required Span<Solution> Solutions { get; init; }
    public required int StartingIteration { get; init; }
    public required int EndingIteration { get; init; }
    public required int CheckpointPeriod { get; init; }

    public SearchConfiguration()
    {
    }
}

public sealed class Searcher
{
    private readonly Model _model;
    private readonly Graph[] _graphs;
    private readonly int _penaltyActivationProgramLength;
    private readonly double _programLengthPenalty;
    private readonly int[] _baselineCostVector;
    public Population Population { get; }


    public Searcher(Model model, IList<Graph> graphs, int[] baselineCostVector, int populationSize,
        int penaltyActivationProgramLength, double programLengthPenalty)
    {
        Debug.Assert(graphs.Count == baselineCostVector.Length);
        _model = model;
        _graphs = [.. graphs];
        _baselineCostVector = baselineCostVector;
        _penaltyActivationProgramLength = penaltyActivationProgramLength;
        _programLengthPenalty = programLengthPenalty;
        Population = new Population(populationSize);
    }

    public Checkpoint Start(Pcg64 rng, in SearchConfiguration config,
        Action<Searcher, Checkpoint>? checkpointingCallback)
    {
        _model.Reset();
        Population.Clear();
        var solutions = config.Solutions;
        var endingIteration = config.EndingIteration;
        var checkpointPeriod = config.CheckpointPeriod;
        for (var i = 0; i < solutions.Length; i++)
        {
            var solution = solutions[i];
            var costVector = new int[_graphs.Length];
            Parallel.For(0, _graphs.Length,
                j => { costVector[j] = solution.EvaluateOn(_graphs[j]); });
            var fitness = ComputeFitness(solution, costVector);
            if (Population.TryAdd(solution, costVector, fitness, out var ejectedSolution, out _, out _))
            {
                _model.SolutionAccepted(solution);
                if (ejectedSolution is not null)
                {
                    _model.SolutionEjected(ejectedSolution);
                }
            }
        }

        for (var iteration = config.StartingIteration; iteration <= endingIteration; iteration++)
        {
            var solution = new Solution(_model.Sample(rng, Population));
            var costVector = new int[_graphs.Length];
            Parallel.For(0, _graphs.Length,
                i => { costVector[i] = solution.EvaluateOn(_graphs[i]); });
            var fitness = ComputeFitness(solution, costVector);
            if (Population.TryAdd(solution, costVector, fitness, out var ejectedSolution, out _, out _))
            {
                _model.SolutionAccepted(solution);
                if (ejectedSolution is not null)
                {
                    _model.SolutionEjected(ejectedSolution);
                }
            }

            if (iteration % checkpointPeriod == 0)
            {
                checkpointingCallback?.Invoke(this, new Checkpoint
                    {
                        Iteration = iteration,
                        Solutions = Population.Solutions.AsMemory(..Population.Count)
                    }
                );
            }
        }

        var checkpoint = new Checkpoint
        {
            Iteration = endingIteration,
            Solutions = Population.Solutions.AsMemory(..Population.Count)
        };
        checkpointingCallback?.Invoke(this, checkpoint);
        return checkpoint;
    }

    private double ComputeFitness(Solution solution, int[] costVector)
    {
        var fitness = 0.0;
        var programLength = solution.ProgramLength;
        var baselineCostVector = _baselineCostVector;
        for (var j = 0; j < costVector.Length; j++)
        {
            fitness += baselineCostVector[j] / double.Max(1.0, costVector[j]);
        }

        if (programLength > _penaltyActivationProgramLength)
        {
            fitness -= _programLengthPenalty * programLength;
        }

        return fitness;
    }
}