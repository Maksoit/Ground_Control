using GroundControl.Api.Models;
using GroundControl.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Controllers;

[ApiController]
[Route("v1/occupancy")]
public class OccupancyController : ControllerBase
{
    private readonly IRouteService _routeService;

    public OccupancyController(IRouteService routeService) => _routeService = routeService;

    [HttpGet]
    public async Task<ActionResult<List<OccupancyItemDto>>> GetOccupancy(CancellationToken ct)
    {
        var items = await _routeService.GetOccupancyAsync(ct);
        return Ok(items);
    }
}
