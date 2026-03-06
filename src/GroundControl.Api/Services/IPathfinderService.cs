using GroundControl.Api.Models;

namespace GroundControl.Api.Services;

public interface IPathfinderService
{
    /// <summary>
    /// Finds the shortest path from <paramref name="fromNode"/> to <paramref name="toNode"/>
    /// using Dijkstra's algorithm over the provided graph.
    /// Returns null if no path exists.
    /// </summary>
    List<EdgePathItem>? FindPath(
        string fromNode,
        string toNode,
        IReadOnlyList<EdgeEntity> edges);
}
