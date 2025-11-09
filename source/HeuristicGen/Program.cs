using System.Diagnostics;
using HeuristicGen.Evolution;
using HeuristicGen.Network;
using HeuristicGen.Network.Generators;
using HeuristicGen.Rng;
using HeuristicGen.TssAlgorithms;
using HeuristicGen.Util;
using CollectionExtensions = HeuristicGen.Util.CollectionExtensions;

namespace HeuristicGen;

internal class Program
{
    private static readonly Checkpoint DefaultCheckPoint;
    private static readonly double[] ErdosRenyiProbabilities = [0.01, 0.02, 0.03, 0.04, 0.05];
    private static readonly double[] DegreeSequnceExponents = [-1.1, -1.25, -1.5, -2.0, -2.5];
    private static readonly Solution[] SeedPopulation;
    private static string _checkpointDirectoryPath = string.Empty;
    private static string _checkpointFilePath = string.Empty;
    private static string _checkpointTempFilePath = string.Empty;
    private static Graph[] _graphs = [];
    private static Checkpoint _checkpoint;
    private static Solution[] _population = [];
    private static int[] _baselineCostVector = [];
    private static readonly Pcg64 Rng = Pcg64.Create();

    private static readonly string[] MainMenuOptions =
    [
        "Explore new solutions using genetic programming.",
        "Display the top solutions from the current population.",
        "Delete current population.",
        "Exit."
    ];

    static Program()
    {
        var root1 = new DerivationNode(Symbol.Expression, 2)
        {
            ChildNodes =
            [
                new DerivationNode(Symbol.NodeScalarProperty, 30)
                {
                    ChildNodes = [new DerivationNode(Symbol.DegreeInCurrentScope)]
                }
            ]
        }; // Degree
        Debug.Assert(Model.IsValidProgram(root1));

        var root2 = new DerivationNode(Symbol.Expression, 2)
        {
            ChildNodes =
            [
                new DerivationNode(Symbol.NodeScalarProperty, 31)
                {
                    ChildNodes = [new DerivationNode(Symbol.ThresholdInCurrentScope)]
                }
            ]
        }; // Threshold
        Debug.Assert(Model.IsValidProgram(root2));

        var root3 = new DerivationNode(Symbol.Expression, 2)
        {
            ChildNodes =
            [
                new DerivationNode(Symbol.NodeScalarProperty, 34)
                {
                    ChildNodes = [new DerivationNode(Symbol.InactiveNeighborsCountInCurrentScope)]
                }
            ]
        }; // InactiveNeighborsCount
        Debug.Assert(Model.IsValidProgram(root3));

        var root4 = new DerivationNode(Symbol.Expression, 2)
        {
            ChildNodes =
            [
                new DerivationNode(Symbol.NodeScalarProperty, 33)
                {
                    ChildNodes = [new DerivationNode(Symbol.DeficitInCurrentScope)]
                }
            ]
        }; // Deficit
        Debug.Assert(Model.IsValidProgram(root4));

        SeedPopulation =
        [
            new Solution(root1),
            new Solution(root2),
            new Solution(root3),
            new Solution(root4)
        ];
        _checkpoint = DefaultCheckPoint = new Checkpoint
        {
            Iteration = 0,
            Solutions = SeedPopulation
        };
    }

    private static void Main(string[] _)
    {
        Console.WriteLine("Welcome to the HeuristicGen Project!\n");
        PromptUserForCheckpointDirectory();
        _checkpointFilePath = Path.Combine(_checkpointDirectoryPath, "checkpoint.bin");
        _checkpointTempFilePath = Path.ChangeExtension(_checkpointFilePath, "temp.bin");

        if (!TryLoadCheckpoint())
        {
            Console.WriteLine("\nCould not find a valid checkpoint.");
            Console.WriteLine("\nDefaulting to handcrafted solutions.");
            _checkpoint = DefaultCheckPoint;
            _population = SeedPopulation;
        }

        while (true)
        {
            var choice = PresentMenu("What would you like to do next?", MainMenuOptions);
            switch (choice)
            {
                case 1:
                    RunEvolutionarySearch();
                    break;
                case 2:
                    ShowTopSolutions();
                    break;
                case 3:
                    DeleteCheckpoint();
                    break;
                default:
                    return;
            }
        }
    }

    private static void PromptUserForCheckpointDirectory()
    {
        string checkpointDirectoryPath;
        do
        {
            Console.WriteLine("Please provide the path to the checkpoint directory.");
            Console.WriteLine("If the given directory does not exist, it will be created, ");
            Console.Write("and program states will be stored in it.: ");
            try
            {
                checkpointDirectoryPath = Console.ReadLine()?.Trim() ?? string.Empty;
                if (!Directory.Exists(checkpointDirectoryPath))
                {
                    Console.Write("\nDirectory doesn't exist. Creating directory...");
                    Directory.CreateDirectory(checkpointDirectoryPath);
                    Console.WriteLine("\nDirectory created.");
                }

                break;
            }
            catch (Exception e)
            {
                LogException(e);
                var isYes = AskYesOrNo("Do you want to continue? (y/n): ");
                if (!isYes)
                {
                    Environment.Exit(0);
                }
            }
        } while (true);

        _checkpointDirectoryPath = checkpointDirectoryPath;
    }

    private static bool TryLoadCheckpoint()
    {
        try
        {
            if (File.Exists(_checkpointFilePath))
            {
                Console.Write("\nAttempting to load checkpoint...");
                _checkpoint = CheckpointIo.ReadFromFile(_checkpointFilePath);
                _population = _checkpoint.Solutions.ToArray();
                Console.WriteLine("\nCheckpoint loaded.");
                return true;
            }
        }
        catch (Exception e)
        {
            LogException(e);
        }

        return false;
    }

    private static void DeleteCheckpoint()
    {
        var isYes = AskYesOrNo("Are you sure you want to delete the population? (y/n): ");
        if (isYes)
        {
            if (File.Exists(_checkpointFilePath))
            {
                File.Delete(_checkpointFilePath);
            }

            Console.WriteLine("\nDefaulting to seed solutions.");
            _checkpoint = DefaultCheckPoint;
            _population = SeedPopulation;
        }
    }

    #region Search

    private static void RunEvolutionarySearch()
    {
        Console.WriteLine();
        var iterationCount = PromptForParameter("Please enter the number of iterations (at least 1): ",
            s => int.TryParse(s, out var result) ? (result > 0, result) : (false, default));
        var graphChangePeriod = PromptForParameter(
            "Please enter how frequently to change graphs (for example, after every 100 iterations): ",
            s => int.TryParse(s, out var result) ? (result > 0, result) : (false, default));
        var populationSize = PromptForParameter("Please enter the population size: ",
            s => int.TryParse(s, out var result) ? (result > 0, result) : (false, default));
        var maxDepth = PromptForParameter("Please enter the maximum expression depth during sampling (at least 2): ",
            s => int.TryParse(s, out var result) ? (result >= 2, result) : (false, default));
        var tournamentSize = PromptForParameter("Please enter the tournament size: ",
            s => int.TryParse(s, out var result) ? (result > 0, result) : (false, default));
        var mixtureWeightForRules = PromptForParameter("Please enter the mixture weight for rules (between 0 and 1): ",
            s => double.TryParse(s, out var result) ? (result is >= 0.0 and <= 1.0, result) : (false, default));
        var penaltyActivationProgramLength = PromptForParameter(
            "Please enter the program length above which a penalty will apply: ",
            s => int.TryParse(s, out var result) ? (result >= 0, result) : (false, default));
        var programLengthPenalty = PromptForParameter(
            "Please enter the penalty coefficient for the solution length (at least 0.0): ",
            s => double.TryParse(s, out var result) ? (result >= 0.0, result) : (false, default));
        var checkpointPeriod = PromptForParameter(
            "Please enter how frequently to save checkpoints (for example, after every 10 iterations): ",
            s => int.TryParse(s, out var result) ? (result > 0, result) : (false, default));

        if (!AskYesOrNo("Would you like to start the evolutionary search? (y/n): "))
        {
            return;
        }

        ReplaceSyntheticNetworks();
        var model = new Model(maxDepth, tournamentSize, mixtureWeightForRules);
        var nextIteration = _checkpoint.Iteration + 1;
        var iterLastGraphChange = _checkpoint.Iteration / graphChangePeriod * graphChangePeriod;
        for (var iter = iterLastGraphChange; iter <= iterationCount; iter += graphChangePeriod)
        {
            var config = new SearchConfiguration
            {
                Solutions = _checkpoint.Solutions.Span,
                StartingIteration = nextIteration,
                EndingIteration = int.Min(iterationCount, iter + graphChangePeriod - 1),
                CheckpointPeriod = checkpointPeriod
            };
            var searcher = new Searcher(model, _graphs, _baselineCostVector, populationSize,
                penaltyActivationProgramLength, programLengthPenalty);
            _checkpoint = searcher.Start(Rng, in config, OnProgressNotification);
            ReplaceSyntheticNetworks();
            nextIteration = iter + graphChangePeriod;
        }

        _population = _checkpoint.Solutions.ToArray();
    }

    private static void ReplaceSyntheticNetworks()
    {
        Console.Write("\nGenerating random graphs...");
        var generators = new List<IGraphGenerator>();
        generators.AddRange(ErdosRenyiProbabilities.Select(probability => new ErdosRenyi(1000, probability)));
        generators.AddRange(DegreeSequnceExponents.Select(exponent => new ChungLu(1000, 5, 100, exponent)));
        var graphs = generators.Select(generator => generator.Sample(Rng)).ToArray();
        _graphs = graphs;
        _baselineCostVector = new int[_graphs.Length];
        Console.Write("\nComputing the baseline cost vector...");
        Parallel.For(0, _graphs.Length, i =>
        {
            var graph = _graphs[i];
            _baselineCostVector[i] = MinDegreePruner.Prune(graph, new MaxDegreeHeuristic().FindTargetSet(graph)).Count;
        });
        Console.WriteLine("\nGraphs replaced.");
    }

    private static void OnProgressNotification(Searcher searcher, Checkpoint checkpoint)
    {
        CheckpointIo.WriteToFile(checkpoint, _checkpointTempFilePath);
        if (File.Exists(_checkpointFilePath))
        {
            File.Replace(_checkpointTempFilePath, _checkpointFilePath, null);
        }
        else
        {
            File.Move(_checkpointTempFilePath, _checkpointFilePath);
        }

        var populationCount = searcher.Population.Count;
        var costVectors = searcher.Population.CostVectors;
        var fitnesses = searcher.Population.Fitnesses;
        var solutions = searcher.Population.Solutions;
        var maxFitness = double.MinValue;
        var indexOfBestSolution = 0;

        for (var i = 0; i < populationCount; i++)
        {
            if (fitnesses[i] > maxFitness)
            {
                indexOfBestSolution = i;
                maxFitness = fitnesses[i];
            }
        }

        var (baselineMatchedCount, baselineSurpassedCount) = ComputeCostCountStats(costVectors[indexOfBestSolution]);
        var now = DateTime.Now.ToString("dd/MMM/yyyy, hh:mm:ss tt");
        Console.WriteLine($"\nTime Step {checkpoint.Iteration} @ {now}");
        Console.WriteLine($"\tThe average population fitness is {fitnesses.Average()}");
        Console.WriteLine($"\tThe best solution's fitness is {maxFitness}");
        Console.WriteLine(baselineMatchedCount > 0
            ? "\tThe best solution matched the baseline on " + PluralizeGraph(baselineMatchedCount)
            : "\tThe best solution did not match the baseline on any graph.");
        Console.WriteLine(baselineSurpassedCount > 0
            ? "\tThe best solution surpassed the baseline on " + PluralizeGraph(baselineSurpassedCount)
            : "\tThe best solution did not surpass the baseline on any graph.");
        Console.WriteLine("\tThe best solution is");
        Console.WriteLine(solutions[indexOfBestSolution].ToString("\t\t"));
        return;

        static string PluralizeGraph(int count) => count switch
        {
            1 => "1 graph.",
            > 1 => $"{count} graphs",
            _ => throw new ArgumentOutOfRangeException(nameof(count), count, null)
        };
    }

    private static (int Matched, int Surpassed) ComputeCostCountStats(int[] costVector)
    {
        var matched = 0;
        var surpassed = 0;
        var baselineCostVector = _baselineCostVector;
        for (var j = 0; j < costVector.Length; j++)
        {
            var b = baselineCostVector[j];
            var c = costVector[j];
            if (c <= b)
            {
                matched++;
            }

            if (c < b)
            {
                surpassed++;
            }
        }

        return (matched, surpassed);
    }

    #endregion

    #region Show Top Solutions

    private static void ShowTopSolutions()
    {
        Console.WriteLine();
        var choice = PresentMenu("Which networks do you want to use to rank solutions?", [
            "Synthetic networks", "Real-world networks"
        ]);
        var rankTotals = new double[_population.Length];
        var networks = new Dictionary<string, Func<Graph>>();
        switch (choice)
        {
            case 1:
            {
                var generators = new List<IGraphGenerator>();
                generators.AddRange(ErdosRenyiProbabilities.Select(probability => new ErdosRenyi(1000, probability)));
                generators.AddRange(DegreeSequnceExponents.Select(exponent => new ChungLu(1000, 5, 100, exponent)));
                for (var i = 0; i < generators.Count; i++)
                {
                    var generator = generators[i];
                    networks.Add($"Synth{i + 1:D2}", () => generator.Sample(Rng));
                }

                break;
            }
            case 2:
            {
                var realWorldNetworks = PromptUserForRealWorldNetworks();
                foreach (var (name, path) in realWorldNetworks)
                {
                    networks.Add(name, () => LoadGraph(path));
                }

                break;
            }
        }

        var networkNames = networks.Keys.ToList();
        networkNames.Sort(StringComparer.InvariantCultureIgnoreCase);
        var networkCostsMap = networkNames.ToDictionary(ks => ks, _ => new int[_population.Length]);
        foreach (var networkName in networkNames)
        {
            Console.Write($"\nEvaluating solutions on the {networkName} network...");
            var testNetwork = networks[networkName]();
            var costs = networkCostsMap[networkName];
            Parallel.For(0, _population.Length, j => { costs[j] = _population[j].EvaluateOn(testNetwork); });
            var rank = costs.RankData(CollectionExtensions.RankingMethod.Max);
            Parallel.For(0, _population.Length, j => { rankTotals[j] += rank[j]; });
        }


        Console.WriteLine("\nEvaluation done.");

        var indices = rankTotals.Argsort();
        var numSolutionsToDisplay = indices.Length;
        if (_population.Length > 1)
        {
            Console.WriteLine($"\nThere are {_population.Length} solutions in the population.");
            numSolutionsToDisplay = PromptForParameter("Please enter the number of solutions to display: ",
                s => int.TryParse(s, out var result)
                    ? (result >= 1 && result <= _population.Length, result)
                    : (false, default));
        }

        for (var i = 0; i < numSolutionsToDisplay; i++)
        {
            var index = indices[i];
            var solution = _population[index];
            var averageRank = rankTotals[index] / networkNames.Count;
            Console.WriteLine($"\nSolution {i + 1} out of {indices.Length}");
            Console.WriteLine($"\tProgram Length is {solution.ProgramLength}");
            Console.WriteLine($"\tAverage rank is {averageRank}");
            foreach (var networkName in networkNames)
            {
                var size = networkCostsMap[networkName][index];
                Console.WriteLine($"\tTarget set size on the {networkName} network is {size}");
            }

            Console.WriteLine("\tSolution is");
            Console.WriteLine(solution.ToString("\t\t"));
            if (i < numSolutionsToDisplay - 1)
            {
                if (!AskYesOrNo("\nDo you want to see the next solution? (y/n): "))
                {
                    break;
                }
            }
        }

        Console.Write("\nPress any key to continue...");
        Console.ReadKey(true);
        Console.WriteLine();
    }

    private static Dictionary<string, string> PromptUserForRealWorldNetworks()
    {
        do
        {
            Console.Write("Please provide the path to the networks' directory: ");
            try
            {
                var networkDirectoryPath = Console.ReadLine()?.Trim() ?? string.Empty;
                return Directory.GetFiles(networkDirectoryPath, "*.graph.txt").ToDictionary(
                    ks =>
                    {
                        var fileName = Path.GetFileName(ks);
                        return fileName[..^".graph.txt".Length];
                    }, vs => vs);
            }
            catch (Exception e)
            {
                LogException(e);
                var isYes = AskYesOrNo("Do you want to continue? (y/n): ");
                if (!isYes)
                {
                    Environment.Exit(0);
                }
            }
        } while (true);
    }

    private static Graph LoadGraph(string path)
    {
        var node = 0;
        var tempAdjacencyList = new List<HashSet<int>>();
        var stringToNodeMap = new Dictionary<string, int>();
        foreach (var line in File.ReadLines(path))
        {
            var edge = line.Split().Where(s => !string.IsNullOrEmpty(s)).ToArray();
            foreach (var nodeString in edge)
            {
                if (stringToNodeMap.ContainsKey(nodeString))
                    continue;
                stringToNodeMap[nodeString] = node++;
                tempAdjacencyList.Add([]);
            }

            var u = stringToNodeMap[edge[0]];
            var v = stringToNodeMap[edge[1]];
            if (u != v)
            {
                tempAdjacencyList[u].Add(v);
                tempAdjacencyList[v].Add(u);
            }
        }

        var adjacencyList = tempAdjacencyList.ToArray();
        return new Graph(adjacencyList);
    }

    #endregion

    #region Miscellaneous

    private static int PresentMenu(string message, ICollection<string> options)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(message);
            var optionKey = 0;
            foreach (var option in options)
            {
                Console.WriteLine($"{++optionKey}. {option}");
            }

            if (optionKey == 0)
            {
                return 0;
            }

            Console.Write($"Enter your choice (1-{optionKey}): ");
            var input = Console.ReadLine()?.Trim();
            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= optionKey)
            {
                return choice;
            }

            Console.WriteLine($"\nInvalid choice. Please enter a number between 1 and {optionKey}.");
        }
    }

    private static T PromptForParameter<T>(string message, Func<string, (bool, T)> parser)
    {
        do
        {
            Console.Write(message);
            var input = Console.ReadLine()?.Trim() ?? string.Empty;
            var (isValid, result) = parser(input);
            if (isValid)
            {
                return result;
            }

            Console.WriteLine($"'{input}' is not a valid response.");
            var isYes = AskYesOrNo("Do you want to continue? (y/n): ");
            if (!isYes)
            {
                Environment.Exit(0);
            }
        } while (true);
    }

    private static bool AskYesOrNo(string question)
    {
        do
        {
            Console.Write(question);
            var input = Console.ReadLine()?.Trim().ToLower() ?? string.Empty;

            switch (input)
            {
                case "":
                case "y":
                case "yes":
                    return true;
                case "n":
                case "no":
                    return false;
                default:
                    Console.WriteLine("Invalid input. Please enter 'y' for yes or 'n' for no.");
                    break;
            }
        } while (true);
    }

    private static void LogException(Exception e)
    {
        Console.WriteLine("The following error occured.");
        Console.WriteLine("\n" + e.Message);
    }

    #endregion
}