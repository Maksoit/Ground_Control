using GroundControl.Core.Interfaces;
using GroundControl.Core.Models;

namespace GroundControl.Core.Services;

public class DijkstraPathfinder : IPathfinder
{
    public List<EdgePathItem>? FindPath(string fromNode, string toNode, List<Edge> edges)
    {
        if (fromNode == toNode)
        {
            return new List<EdgePathItem>();
        }

        // Build adjacency list and collect all nodes
        var graph = new Dictionary<string, List<(string neighbor, Edge edge)>>();
        var allNodes = new HashSet<string>();

        foreach (var edge in edges)
        {
            if (!graph.ContainsKey(edge.FromNode))
                graph[edge.FromNode] = new List<(string, Edge)>();

            graph[edge.FromNode].Add((edge.ToNode, edge));

            // Collect all nodes (both from and to)
            allNodes.Add(edge.FromNode);
            allNodes.Add(edge.ToNode);
        }

        // Check if both nodes exist
        if (!allNodes.Contains(fromNode) || !allNodes.Contains(toNode))
            return null;

        // Dijkstra's algorithm
        var distances = new Dictionary<string, decimal>();
        var previous = new Dictionary<string, (string node, Edge edge)>();
        var unvisited = new HashSet<string>();

        // Initialize all nodes
        foreach (var node in allNodes)
        {
            distances[node] = decimal.MaxValue;
            unvisited.Add(node);
        }
        distances[fromNode] = 0;

        while (unvisited.Count > 0)
        {
            // Find node with minimum distance
            string? current = null;
            decimal minDistance = decimal.MaxValue;
            foreach (var node in unvisited)
            {
                if (distances.ContainsKey(node) && distances[node] < minDistance)
                {
                    minDistance = distances[node];
                    current = node;
                }
            }

            if (current == null || minDistance == decimal.MaxValue)
                break;

            unvisited.Remove(current);

            // Found target
            if (current == toNode)
                break;

            // Update neighbors
            if (graph.ContainsKey(current))
            {
                foreach (var (neighbor, edge) in graph[current])
                {
                    if (!unvisited.Contains(neighbor))
                        continue;

                    var altDistance = distances[current] + edge.Length;
                    if (altDistance < distances[neighbor])
                    {
                        distances[neighbor] = altDistance;
                        previous[neighbor] = (current, edge);
                    }
                }
            }
        }

        // Reconstruct path
        if (!previous.ContainsKey(toNode))
            return null;

        var path = new List<EdgePathItem>();
        var currentNode = toNode;

        while (previous.ContainsKey(currentNode))
        {
            var (prevNode, edge) = previous[currentNode];
            path.Insert(0, new EdgePathItem
            {
                EdgeId = edge.EdgeId,
                FromNode = edge.FromNode,
                ToNode = edge.ToNode
            });
            currentNode = prevNode;
        }

        return path;
    }
}