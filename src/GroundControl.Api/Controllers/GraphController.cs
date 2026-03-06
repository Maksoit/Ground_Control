using GroundControl.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GroundControl.Api.Controllers;

[ApiController]
[Route("v1/graph")]
public class GraphController : ControllerBase
{
    private readonly GroundControlDbContext _context;

    public GraphController(GroundControlDbContext context)
    {
        _context = context;
    }

    [HttpGet("nodes")]
    public async Task<IActionResult> GetNodes()
    {
        var nodes = await _context.Nodes
            .Select(n => new
            {
                nodeId = n.NodeId,
                x = n.X,
                y = n.Y,
                nodeType = n.NodeType
            })
            .ToListAsync();

        return Ok(nodes);
    }

    [HttpGet("edges")]
    public async Task<IActionResult> GetEdges()
    {
        var edges = await _context.Edges
            .Select(e => new
            {
                edgeId = e.EdgeId,
                fromNode = e.FromNode,
                toNode = e.ToNode,
                length = e.Length
            })
            .ToListAsync();

        return Ok(edges);
    }
}