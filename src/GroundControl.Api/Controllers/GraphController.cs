using GroundControl.Api.Data;
using GroundControl.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GroundControl.Api.Controllers;

[ApiController]
[Route("v1/graph")]
public class GraphController : ControllerBase
{
    private readonly GroundDbContext _db;

    public GraphController(GroundDbContext db) => _db = db;

    [HttpGet("nodes")]
    public async Task<ActionResult<List<NodeEntity>>> GetNodes(CancellationToken ct)
    {
        var nodes = await _db.Nodes.ToListAsync(ct);
        return Ok(nodes.Select(n => new
        {
            nodeId = n.NodeId,
            x = n.X,
            y = n.Y,
            nodeType = n.NodeType,
        }));
    }

    [HttpGet("edges")]
    public async Task<ActionResult<List<EdgeEntity>>> GetEdges(CancellationToken ct)
    {
        var edges = await _db.Edges.ToListAsync(ct);
        return Ok(edges.Select(e => new
        {
            edgeId = e.EdgeId,
            fromNode = e.FromNode,
            toNode = e.ToNode,
            length = e.Length,
        }));
    }
}
