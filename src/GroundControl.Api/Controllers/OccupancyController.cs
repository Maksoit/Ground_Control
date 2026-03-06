using GroundControl.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GroundControl.Api.Controllers;

[ApiController]
[Route("v1/occupancy")]
public class OccupancyController : ControllerBase
{
    private readonly GroundControlDbContext _context;

    public OccupancyController(GroundControlDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetOccupancy()
    {
        var occupancy = await _context.EdgeOccupancy
            .Select(o => new
            {
                edgeId = o.EdgeId,
                occupiedBy = o.OccupiedBy,
                routeId = o.RouteId,
                updatedAt = o.UpdatedAt
            })
            .ToListAsync();

        return Ok(occupancy);
    }
}