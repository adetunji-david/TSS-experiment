using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TssBenchmark.Heuristics;
using TssBenchmark.Network;

namespace TssBenchmark;

internal class Program
{
    private static string _outputDirectoryPath = string.Empty;

    private static readonly (string Name, ITssHeuristic Heuristic)[] Heuristics =
    [
        ("MDG", new MaxDegreeHeuristic()),
        ("TIP_DECOMP", new TipDecompHeuristic()),
        ("MTS", new CgrMtsHeuristic()),
        ("C-TSS", new CgmrvTssHeuristic()),
        ("ELPH", new EvolvedHeuristic())
    ];

    private static readonly string[] MainMenuOptions =
    [
        "Benchmark the heuristics.",
        "Display the benchmark results.",
        "Trace Shapley prunner",
        "Exit."
    ];

    private static void Main_(string[] _)
    {
        var networkPath =
            @"C:\Users\Adetunji\Documents\Research-Projects\Target Set Selection\networks\socfb-nips-ego.graph.txt";
        var (graph, _) = LoadGraph(networkPath);
        var h = new TipDecompHeuristic();
        var ts = h.FindTargetSet(graph);
        var diffuser = new Diffuser(graph);
        var activatedNodeCount = diffuser.ActivateNodes(ts).Count;
        if (activatedNodeCount != graph.NodeCount)
        {
            Console.WriteLine("Given set is not a valid target set");
        }

        Console.WriteLine($"Target Set Size: {ts.Count}");
        Console.ReadLine();
    }

    private static void Main(string[] _)
    {
        Console.WriteLine("Welcome to the HeuristicGen Benchmark Project!\n");
        PromptUserForOutputDirectory();

        while (true)
        {
            var choice = PresentMenu("What would you like to do next?", MainMenuOptions);
            switch (choice)
            {
                case 1:
                    RunBenchmark();
                    break;
                case 2:
                    DisplayResults();
                    break;
                case 3:
                    RunShapleyTrace();
                    break;
                default:
                    return;
            }
        }
    }

    private static Dictionary<string, string> PromptUserForNetworks(string networkType)
    {
        do
        {
            Console.Write($"Please provide the path to the {networkType} networks' directory: ");
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

    private static void PromptUserForOutputDirectory()
    {
        string directoryPath;
        do
        {
            Console.WriteLine("Please provide the path to the output directory.");
            Console.Write("If the given directory does not exist, it will be created: ");
            try
            {
                directoryPath = Console.ReadLine()?.Trim() ?? string.Empty;
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
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

        _outputDirectoryPath = directoryPath;
    }

    private static void RunBenchmark()
    {
        Console.WriteLine();
        var networks = PromptUserForNetworks("benchamrk");
        Console.WriteLine();
        var rounds = PromptForParameter("Please enter the number of prunning rounds: ",
            s => int.TryParse(s, out var result) ? (result > 0, result) : (false, default));
        var deflationFactorsCount = PromptForParameter("Please enter the number of deflation factors to try: ",
            s => int.TryParse(s, out var result) ? (result > 0, result) : (false, default));
        var networkNames = networks.Keys.ToList();
        networkNames.Sort(StringComparer.InvariantCultureIgnoreCase);
        var deflationFactors = Enumerable.Range(0, deflationFactorsCount)
            .Select(i => i / (double)(deflationFactorsCount - 1))
            .ToArray();
        string? now;
        foreach (var networkName in networkNames)
        {
            Console.WriteLine($"\n\nLoading Network {networkName}.");
            var (graph, stringToNodeMap) = LoadGraph(networks[networkName]);
            var nodeToStringMap = stringToNodeMap.ToDictionary(p => p.Value, p => p.Key);
            foreach (var (heuristicName, tssHeuristic) in Heuristics)
            {
                var directoryPath = Path.Combine(_outputDirectoryPath, "Heuristics Benchmark", networkName,
                    heuristicName);
                Directory.CreateDirectory(directoryPath);
                now = DateTime.Now.ToString("dd/MMM/yyyy, hh:mm:ss tt");
                Console.WriteLine($"\n\tStarting {heuristicName} on {networkName} @ {now}");

                var variant = "Base";
                Console.WriteLine("\t\tfinding base target set...");
                var (targetSet, et) = ReadResult(directoryPath, variant, stringToNodeMap);
                if (targetSet is null)
                {
                    var watch = Stopwatch.StartNew();
                    targetSet = tssHeuristic.FindTargetSet(graph);
                    watch.Stop();
                    et = watch.ElapsedMilliseconds;
                    WriteResult(directoryPath, variant, nodeToStringMap, targetSet, et);
                }

                variant = "Base+Shapley";
                Console.WriteLine("\t\trunning Shapley prunner...");
                var (spTargetSet, spEt) = ReadResult(directoryPath, variant, stringToNodeMap);
                if (spTargetSet is null)
                {
                    var watch = Stopwatch.StartNew();
                    (_, spTargetSet, var bestDeflationFactor) =
                        ShapleyPruner.Prune(graph, targetSet, rounds, deflationFactors);
                    watch.Stop();
                    spEt = et + watch.ElapsedMilliseconds;
                    WriteResult(directoryPath, variant, nodeToStringMap, spTargetSet, spEt);
                    using var jsonFileStream = File.Create(Path.Combine(directoryPath, $"{variant}.extra.json"));
                    JsonSerializer.Serialize(jsonFileStream, new { BestDeflationFactor = bestDeflationFactor });
                }

                variant = "Base+Rev";
                Console.WriteLine("\t\trunning Min-degree prunner...");
                var (rpTargetSet, rpEt) = ReadResult(directoryPath, variant, stringToNodeMap);
                if (rpTargetSet is null)
                {
                    var watch = Stopwatch.StartNew();
                    rpTargetSet = MinDegreeFastPruner.Prune(graph, targetSet);
                    watch.Stop();
                    rpEt = et + watch.ElapsedMilliseconds;
                    WriteResult(directoryPath, variant, nodeToStringMap, rpTargetSet, rpEt);
                }

                variant = "Base+Shapley+Rev";
                Console.WriteLine("\t\trunning Shapley + Min-degree prunner...");
                var (srpTargetSet, _) = ReadResult(directoryPath, variant, stringToNodeMap);
                if (srpTargetSet is null)
                {
                    var watch = Stopwatch.StartNew();
                    srpTargetSet = MinDegreeFastPruner.Prune(graph, spTargetSet);
                    watch.Stop();
                    WriteResult(directoryPath, variant, nodeToStringMap, srpTargetSet,
                        spEt + watch.ElapsedMilliseconds);
                }

                variant = "Base+Rev+Shapley";
                Console.WriteLine("\t\trunning Min-degree + Shapley prunner...");
                var (rspTargetSet, _) = ReadResult(directoryPath, variant, stringToNodeMap);
                if (rspTargetSet is null)
                {
                    var watch = Stopwatch.StartNew();
                    (_, rspTargetSet, var bestDeflationFactor) =
                        ShapleyPruner.Prune(graph, rpTargetSet, rounds, deflationFactors);
                    watch.Stop();
                    WriteResult(directoryPath, variant, nodeToStringMap, rspTargetSet,
                        rpEt + watch.ElapsedMilliseconds);
                    using var jsonFileStream = File.Create(Path.Combine(directoryPath, $"{variant}.extra.json"));
                    JsonSerializer.Serialize(jsonFileStream, new { BestDeflationFactor = bestDeflationFactor });
                }
            }
        }

        now = DateTime.Now.ToString("dd/MMM/yyyy, hh:mm:ss tt");
        Console.WriteLine($"\nBenchmark completed @ {now}");
    }

    private static (HashSet<int>?, long) ReadResult(string directoryPath, string variant,
        Dictionary<string, int> stringToNodeMap)
    {
        try
        {
            using var jsonFileStream = File.OpenRead(Path.Combine(directoryPath, $"{variant}.summary.json"));
            var jsonObject =
                JsonSerializer.Deserialize<Dictionary<string, long>>(jsonFileStream)!;
            var targetSetSize = jsonObject["TargetSetSize"];
            var elapsedMilliseconds = jsonObject["ElapsedMilliseconds"];

            var targetSet = new HashSet<int>();
            using var reader = new StreamReader(Path.Combine(directoryPath, $"{variant}.targetSet.txt"));
            while (reader.ReadLine() is { } s)
            {
                var node = stringToNodeMap[s.Trim()];
                targetSet.Add(node);
            }

            return targetSetSize != targetSet.Count ? (null, 0) : (targetSet, elapsedMilliseconds);
        }
        catch
        {
            return (null, 0);
        }
    }

    private static void WriteResult(string directoryPath, string variant, Dictionary<int, string> nodeToStringMap,
        HashSet<int> targetSet, long elapsedMilliseconds)
    {
        using var writer =
            new StreamWriter(Path.Combine(directoryPath, $"{variant}.targetSet.txt"), false);
        foreach (var node in targetSet)
        {
            writer.WriteLine(nodeToStringMap[node]);
        }

        var jsonObject = new Dictionary<string, long>
        {
            ["TargetSetSize"] = targetSet.Count,
            ["ElapsedMilliseconds"] = elapsedMilliseconds
        };
        using var jsonFileStream = File.Create(Path.Combine(directoryPath, $"{variant}.summary.json"));
        JsonSerializer.Serialize(jsonFileStream, jsonObject);
    }

    private static void DisplayResults()
    {
        var baseDirectoryPath = Path.Combine(_outputDirectoryPath, "Heuristics Benchmark");
        var networkNames = Directory.EnumerateDirectories(baseDirectoryPath)
            .Select(subDirectoryPath => new DirectoryInfo(subDirectoryPath).Name).ToList();
        networkNames.Sort(StringComparer.InvariantCultureIgnoreCase);

        var displayedCount = 0;
        var networkCount = networkNames.Count;
        string[] variants = ["Base", "Base+Shapley", "Base+Rev", "Base+Shapley+Rev", "Base+Rev+Shapley"];
        foreach (var networkName in networkNames)
        {
            var best = new Dictionary<string, (long, List<string>)>();
            Console.WriteLine($"\n{networkName}");
            foreach (var (heuristicName, _) in Heuristics)
            {
                var directoryPath = Path.Combine(baseDirectoryPath, networkName, heuristicName);
                Directory.CreateDirectory(directoryPath);
                Console.WriteLine($"\t{heuristicName}");
                foreach (var variant in variants)
                {
                    try
                    {
                        using var jsonFileStream =
                            File.OpenRead(Path.Combine(directoryPath, $"{variant}.summary.json"));
                        var jsonObject =
                            JsonSerializer.Deserialize<Dictionary<string, long>>(jsonFileStream)!;
                        var size = jsonObject["TargetSetSize"];
                        var timeSpan = TimeSpan.FromMilliseconds(jsonObject["ElapsedMilliseconds"]);
                        Console.WriteLine($"\t\t {variant} TargetSet Size: {size}");
                        Console.WriteLine($"\t\t\t Time spent: {ToPrettyFormat(timeSpan)}");

                        if (!best.ContainsKey(variant))
                        {
                            best[variant] = (size, [heuristicName]);
                        }
                        else
                        {
                            var (bestSize, listOfBest) = best[variant];
                            if (size == bestSize)
                            {
                                listOfBest.Add(heuristicName);
                            }
                            else if (size < bestSize)
                            {
                                best[variant] = (size, [heuristicName]);
                            }
                        }
                    }
                    catch
                    {
                        Console.WriteLine($"\t\t {variant} TargetSet Size: _____");
                        Console.WriteLine("\t\t\t Time spent: ____");
                    }
                }
            }

            foreach (var (variant, (smallestSize, listOfBest)) in best)
            {
                Console.WriteLine($"\n\tBest {variant}: {string.Join(", ", listOfBest)} with size {smallestSize}.");
            }

            if (++displayedCount < networkCount)
            {
                if (!AskYesOrNo("\nDo you want to see the next set of results? (y/n): "))
                {
                    break;
                }
            }
        }
    }

    private static void RunShapleyTrace()
    {
        Console.WriteLine();
        var networksToTrace = PromptUserForNetworks("selection");
        Console.WriteLine();
        var rounds = PromptForParameter("Please enter the number of prunning rounds: ",
            s => int.TryParse(s, out var result) ? (result > 0, result) : (false, default));
        var deflationFactorsCount = PromptForParameter("Please enter the number of deflation factors to try: ",
            s => int.TryParse(s, out var result) ? (result > 0, result) : (false, default));
        var deflationFactors = Enumerable.Range(0, deflationFactorsCount)
            .Select(i => i / (double)(deflationFactorsCount - 1))
            .ToArray();
        var networkNames = networksToTrace.Keys.ToList();
        networkNames.Sort(StringComparer.InvariantCultureIgnoreCase);
        string? now;
        foreach (var networkName in networkNames)
        {
            Console.WriteLine($"\n\nLoading Network {networkName}.");
            var (graph, _) = LoadGraph(networksToTrace[networkName]);
            var directoryPath = Path.Combine(_outputDirectoryPath, "Shapley Trace", networkName);
            Directory.CreateDirectory(directoryPath);
            now = DateTime.Now.ToString("dd/MMM/yyyy, hh:mm:ss tt");
            Console.WriteLine($"\n\tStarting trace on {networkName} @ {now}");
            Console.WriteLine("\t\tfinding MDG target set...");
            var targetSet = new MaxDegreeHeuristic().FindTargetSet(graph);
            Console.WriteLine("\t\tpruning target set...");
            var (trace, _, bestFactor) = ShapleyPruner.Prune(graph, targetSet, rounds, deflationFactors);
            using var jsonFileStream = File.Create(Path.Combine(directoryPath, "trace.json"));
            JsonSerializer.Serialize(jsonFileStream, new { Trace = trace, BestDeflationFactor = bestFactor });
        }

        now = DateTime.Now.ToString("dd/MMM/yyyy, hh:mm:ss tt");
        Console.WriteLine($"\nTrace completed @ {now}");
    }

    private static (Graph, Dictionary<string, int>) LoadGraph(string path)
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
        return (new Graph(adjacencyList), stringToNodeMap);
    }

    private static string ToPrettyFormat(TimeSpan span)
    {
        var sb = new StringBuilder();
        if (span.Days > 0)
        {
            sb.Append($"{span.Days} day{(span.Days > 1 ? "s" : string.Empty)} ");
        }

        if (span.Hours > 0)
        {
            sb.Append($"{span.Hours} hour{(span.Hours > 1 ? "s" : string.Empty)} ");
        }

        if (span.Minutes > 0)
        {
            sb.Append($"{span.Minutes} minute{(span.Minutes > 1 ? "s" : string.Empty)} ");
        }

        if (span.Seconds > 0 || span.Milliseconds > 0)
        {
            var s = span.Seconds + span.Milliseconds / 1000;
            sb.Append($"{s} second{(s > 1 ? "s" : string.Empty)} ");
        }

        return sb.Length == 0 ? "0 seconds" : sb.ToString();
    }

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
        Console.WriteLine("\n" + e);
    }
}