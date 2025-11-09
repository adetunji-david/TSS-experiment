using HeuristicGen.Evolution;

namespace HeuristicGen.Util;

public static class CheckpointIo
{
    public static Checkpoint ReadFromFile(string path)
    {
        using var reader = new BinaryReader(File.OpenRead(path));
        var iteration = reader.ReadInt32();
        var size = reader.ReadInt32();
        var solutions = new Solution[size];
        for (var i = 0; i < size; i++)
        {
            solutions[i] = ReadSolution(reader);
        }

        return new Checkpoint
        {
            Iteration = iteration,
            Solutions = solutions
        };
    }

    public static void WriteToFile(Checkpoint checkpoint, string path)
    {
        using var writer = new BinaryWriter(File.Open(path, FileMode.Create));
        writer.Write(checkpoint.Iteration);
        var solutions = checkpoint.Solutions.Span;
        writer.Write(solutions.Length);
        foreach (var solution in solutions)
        {
            WriteSolution(writer, solution);
        }
    }

    private static Solution ReadSolution(BinaryReader reader)
    {
        var nodeCount = reader.ReadInt32();
        var queue = new Queue<DerivationNode>(nodeCount);
        var symbol = (Symbol)reader.ReadInt16();
        var ruleIndex = reader.ReadInt16();
        var childNodesLength = reader.ReadInt16();
        var root = new DerivationNode(symbol, ruleIndex, new DerivationNode[childNodesLength]);
        queue.Enqueue(root);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            var childNodes = node.ChildNodes;
            for (var j = 0; j < childNodes.Length; j++)
            {
                symbol = (Symbol)reader.ReadInt16();
                ruleIndex = reader.ReadInt16();
                childNodesLength = reader.ReadInt16();
                childNodes[j] = new DerivationNode(symbol, ruleIndex, new DerivationNode[childNodesLength]);
                queue.Enqueue(childNodes[j]);
            }
        }

        return new Solution(root);
    }

    private static void WriteSolution(BinaryWriter writer, Solution solution)
    {
        writer.Write(solution.Nodes.Count);
        var queue = new Queue<DerivationNode>(solution.Nodes.Count);
        queue.Enqueue(solution.DerivationTreeRoot);
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            writer.Write((short)node.Symbol);
            writer.Write((short)node.RuleIndex);
            writer.Write((short)node.ChildNodes.Length);
            foreach (var childNode in node.ChildNodes)
            {
                queue.Enqueue(childNode);
            }
        }
    }
}