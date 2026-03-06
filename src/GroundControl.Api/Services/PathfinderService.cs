using GroundControl.Api.Models;

namespace GroundControl.Api.Services;

public class PathfinderService : IPathfinderService
{
    /// <inheritdoc/>
    public List<EdgePathItem>? FindPath(
        string fromNode,
        string toNode,
        IReadOnlyList<EdgeEntity> edges)
    {
        if (fromNode == toNode)
            return new List<EdgePathItem>();

        // Build adjacency list: nodeId -> list of (toNode, edge)
        var adjacency = new Dictionary<string, List<(string To, EdgeEntity Edge)>>(StringComparer.Ordinal);

        foreach (var edge in edges)
        {
            if (!adjacency.ContainsKey(edge.FromNode))
                adjacency[edge.FromNode] = new();
            adjacency[edge.FromNode].Add((edge.ToNode, edge));
        }

        // Dijkstra
        var dist = new Dictionary<string, double>(StringComparer.Ordinal);
        var prev = new Dictionary<string, (string? From, EdgeEntity? Edge)>(StringComparer.Ordinal);

        // Min-heap: (cost, nodeId)
        var pq = new PriorityQueue<string, double>();

        dist[fromNode] = 0;
        pq.Enqueue(fromNode, 0);

        while (pq.Count > 0)
        {
            var current = pq.Dequeue();
            var currentDist = dist.TryGetValue(current, out var d) ? d : double.PositiveInfinity;

            if (current == toNode)
                break;

            if (!adjacency.TryGetValue(current, out var neighbors))
                continue;

            foreach (var (next, edge) in neighbors)
            {
                var newDist = currentDist + edge.Length;
                var existingDist = dist.TryGetValue(next, out var ed) ? ed : double.PositiveInfinity;

                if (newDist < existingDist)
                {
                    dist[next] = newDist;
                    prev[next] = (current, edge);
                    pq.Enqueue(next, newDist);
                }
            }
        }

        // No path found
        if (!dist.ContainsKey(toNode))
            return null;

        // Reconstruct path
        var path = new List<EdgePathItem>();
        var node = toNode;

        while (prev.TryGetValue(node, out var step) && step.Edge != null)
        {
            path.Add(new EdgePathItem
            {
                EdgeId = step.Edge.EdgeId,
                FromNode = step.Edge.FromNode,
                ToNode = step.Edge.ToNode
            });
            node = step.From!;
        }

        path.Reverse();
        return path;
    }
}
