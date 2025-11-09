using HeuristicGen.Util;

namespace HeuristicGen.Evolution;

public sealed class Population
{
    private readonly Dictionary<int[], int> _uniqueCostVectors;
    private readonly UpdatableMaxPriorityQueue _badSolutionsQueue;

    public int[][] CostVectors { get; }

    public double[] Fitnesses { get; }

    public Solution[] Solutions { get; }

    public int Capacity { get; }

    public int Count { get; private set; }

    public Population(int capacity)
    {
        capacity = int.Max(capacity, 8);
        _uniqueCostVectors = new Dictionary<int[], int>(capacity, new IntArrayComparer());
        _badSolutionsQueue = new UpdatableMaxPriorityQueue(capacity);
        CostVectors = new int[capacity][];
        Fitnesses = new double[capacity];
        Solutions = new Solution[capacity];
        Capacity = capacity;
    }

    public void Clear()
    {
        if (Count > 0)
        {
            Count = 0;
            _uniqueCostVectors.Clear();
            _badSolutionsQueue.Clear();
            Array.Fill(Solutions, null);
            Array.Fill(Fitnesses, 0.0);
            Array.Fill(CostVectors, null);
        }
    }

    public bool TryAdd(Solution solution, int[] costVector, double fitness,
        out Solution? ejectedSolution, out int[]? ejectedCostVector, out double? ejectedFitness)
    {
        ejectedSolution = null;
        ejectedCostVector = null;
        ejectedFitness = null;

        if (!_uniqueCostVectors.TryGetValue(costVector, out var competitorIndex))
        {
            // costVector is unique
            if (Count < Capacity)
            {
                var index = Count++;
                Fitnesses[index] = fitness;
                Solutions[index] = solution;
                CostVectors[index] = costVector;
                _uniqueCostVectors[costVector] = index;
                _badSolutionsQueue.EnqueueOrUpdate(index, -fitness);
                return true;
            }

            _badSolutionsQueue.TryPeek(out competitorIndex, out _);
        }

        var competitor = Solutions[competitorIndex];
        var competitorFitness = Fitnesses[competitorIndex];
        var comparisonResult = fitness.CompareTo(competitorFitness);
        var isBetterThanCompetitor = comparisonResult > 0 ||
                                     (comparisonResult == 0 &&
                                      solution.ProgramLength < competitor.ProgramLength);
        if (isBetterThanCompetitor)
        {
            ejectedSolution = competitor;
            ejectedCostVector = CostVectors[competitorIndex];
            ejectedFitness = competitorFitness;

            _uniqueCostVectors.Remove(ejectedCostVector);
            Fitnesses[competitorIndex] = fitness;
            Solutions[competitorIndex] = solution;
            CostVectors[competitorIndex] = costVector;
            _uniqueCostVectors[costVector] = competitorIndex;
            _badSolutionsQueue.EnqueueOrUpdate(competitorIndex, -fitness);
            return true;
        }

        return false;
    }
}