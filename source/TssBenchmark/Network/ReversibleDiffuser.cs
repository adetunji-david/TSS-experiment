using TssBenchmark.Util;

namespace TssBenchmark.Network;

public sealed class ReversibleDiffuser
{
    private sealed class Node
    {
        private readonly bool[] _neighborsVotedForFlags;
        public int Id { get; }
        public int Threshold { get; }
        public Node[] Neighbors { get; }
        public bool IsActive { get; set; }
        public int VotesReceived { get; private set; }
        public bool WasDirectlyActivated { get; set; }


        public Node(int id, int threshold, int neighborCount)
        {
            Id = id;
            Threshold = threshold;
            Neighbors = new Node[neighborCount];
            _neighborsVotedForFlags = new bool[neighborCount];
        }

        public bool VoteFor(Node other, int indexOfOtherInNeighborArray = -1)
        {
            if (indexOfOtherInNeighborArray < 0)
            {
                indexOfOtherInNeighborArray = FindIndexInNeighborArray(other);
                if (indexOfOtherInNeighborArray < 0)
                {
                    return false;
                }
            }

            if (!_neighborsVotedForFlags[indexOfOtherInNeighborArray])
            {
                _neighborsVotedForFlags[indexOfOtherInNeighborArray] = true;
                other.VotesReceived++;
                return true;
            }

            return false;
        }

        public bool WithdrawVoteFor(Node other, int indexOfOtherInNeighborArray = -1)
        {
            if (indexOfOtherInNeighborArray < 0)
            {
                indexOfOtherInNeighborArray = FindIndexInNeighborArray(other);
                if (indexOfOtherInNeighborArray < 0)
                {
                    return false;
                }
            }

            if (_neighborsVotedForFlags[indexOfOtherInNeighborArray])
            {
                _neighborsVotedForFlags[indexOfOtherInNeighborArray] = false;
                other.VotesReceived--;
                return true;
            }

            return false;
        }

        private int FindIndexInNeighborArray(Node other)
        {
            var low = 0;
            var high = Neighbors.Length - 1;
            while (low <= high)
            {
                var index = low + ((high - low) >> 1);
                var c = Neighbors[index].Id.CompareTo(other.Id);
                switch (c)
                {
                    case 0:
                        return index;
                    case < 0:
                        low = index + 1;
                        break;
                    default:
                        high = index - 1;
                        break;
                }
            }

            return -1;
        }
    }

    private readonly Node[] _nodes;

    public ReversibleDiffuser(Graph graph)
    {
        var thresholds = graph.Thresholds;
        var adjacencyList = graph.AdjacencyList;
        _nodes = new Node[graph.NodeCount];
        for (var i = 0; i < _nodes.Length; i++)
        {
            _nodes[i] = new Node(i, thresholds[i], adjacencyList[i].Count);
        }

        for (var i = 0; i < _nodes.Length; i++)
        {
            var j = 0;
            var neighbors = _nodes[i].Neighbors;
            foreach (var neighborId in adjacencyList[i])
            {
                neighbors[j++] = _nodes[neighborId];
            }

            Array.Sort(neighbors, (u, v) => u.Id.CompareTo(v.Id));
        }
    }

    /// <summary>
    /// Activates a node and returns a list of all nodes that were activated, 
    /// including any additional nodes that were activated as a result of the diffusion process.
    /// </summary>
    /// <param name="nodeId">The node to be activated.</param>
    /// <returns>
    /// A list of nodes that were activated, comprising the input node 
    /// and any additional nodes activated through diffusion.
    /// </returns>
    public List<int> ActivateNode(int nodeId)
    {
        var node = _nodes[nodeId];
        node.WasDirectlyActivated = true;
        return PropagateActivation(node);
    }

    /// <summary>
    /// Undoes the activation of a node and returns a set of all nodes that
    /// were deactivated as a result.
    /// </summary>
    /// <param name="nodeId">The node whose activation is to be undone.</param>
    /// <returns>
    /// A set of nodes that were deactivated.
    /// </returns>
    public HashSet<int> UndoActivation(int nodeId)
    {
        var node = _nodes[nodeId];
        if (!node.WasDirectlyActivated)
        {
            throw new InvalidOperationException();
        }

        node.WasDirectlyActivated = false;
        var deactivatedNodeIds = PropagateVoteWithdrawal(node);
        var reactivatedNodeIds = new HashSet<int>();
        foreach (var deactivatedNodeId in deactivatedNodeIds)
        {
            if (!reactivatedNodeIds.Contains(deactivatedNodeId))
            {
                var deactivatedNode = _nodes[deactivatedNodeId];
                if (SolicitVotes(deactivatedNode))
                {
                    reactivatedNodeIds.AddMany(PropagateActivation(deactivatedNode));
                }
            }
        }

        deactivatedNodeIds.RemoveMany(reactivatedNodeIds);
        return deactivatedNodeIds;
    }

    private static List<int> PropagateActivation(Node node)
    {
        var queue = new Queue<Node>();
        var activatedNodeIds = new List<int>();

        if (node.WasDirectlyActivated || node.VotesReceived >= node.Threshold)
        {
            node.IsActive = true;
            queue.Enqueue(node);
            activatedNodeIds.Add(node.Id);
        }

        while (queue.Count > 0)
        {
            var activatedNode = queue.Dequeue();
            var neighbors = activatedNode.Neighbors;
            for (var index = 0; index < neighbors.Length; index++)
            {
                var neighbor = neighbors[index];
                if (neighbor.IsActive)
                {
                    continue;
                }

                if (activatedNode.VoteFor(neighbor, index))
                {
                    if (neighbor.VotesReceived >= neighbor.Threshold)
                    {
                        neighbor.IsActive = true;
                        queue.Enqueue(neighbor);
                        activatedNodeIds.Add(neighbor.Id);
                    }
                }
            }
        }

        return activatedNodeIds;
    }

    private static HashSet<int> PropagateVoteWithdrawal(Node node)
    {
        var queue = new Queue<Node>();
        var deactivatedNodeIds = new HashSet<int>();

        if (node is { IsActive: true, WasDirectlyActivated: false } && node.VotesReceived < node.Threshold)
        {
            node.IsActive = false;
            queue.Enqueue(node);
            deactivatedNodeIds.Add(node.Id);
        }

        while (queue.Count > 0)
        {
            var deactivatedNode = queue.Dequeue();
            var neighbors = deactivatedNode.Neighbors;
            for (var index = 0; index < neighbors.Length; index++)
            {
                var neighbor = neighbors[index];
                if (deactivatedNode.WithdrawVoteFor(neighbor))
                {
                    if (neighbor is { IsActive: true, WasDirectlyActivated: false } &&
                        neighbor.VotesReceived < neighbor.Threshold)
                    {
                        neighbor.IsActive = false;
                        queue.Enqueue(neighbor);
                        deactivatedNodeIds.Add(neighbor.Id);
                    }
                }
            }
        }

        return deactivatedNodeIds;
    }

    private static bool SolicitVotes(Node node)
    {
        foreach (var neighbor in node.Neighbors)
        {
            if (neighbor.IsActive && neighbor.VoteFor(node))
            {
                if (node.VotesReceived >= node.Threshold)
                {
                    return true;
                }
            }
        }

        return false;
    }
}