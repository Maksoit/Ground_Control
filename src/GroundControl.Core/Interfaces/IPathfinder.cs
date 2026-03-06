using GroundControl.Core.Models;

namespace GroundControl.Core.Interfaces;

public interface IPathfinder
{
    List<EdgePathItem>? FindPath(string fromNode, string toNode, List<Edge> edges);
}