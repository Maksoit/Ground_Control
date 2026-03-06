using GroundControl.Api.Models;
using GroundControl.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Controllers;

[ApiController]
[Route("v1/routes")]
public class RoutesController : ControllerBase
{
    private readonly IRouteService _routeService;

    public RoutesController(IRouteService routeService) => _routeService = routeService;

    [HttpGet]
    public async Task<ActionResult<List<RouteDto>>> GetRoutes(
        [FromQuery] string? vehicleId,
        [FromQuery] VehicleType? vehicleType,
        CancellationToken ct)
    {
        var routes = await _routeService.GetRoutesAsync(vehicleId, vehicleType, ct);
        return Ok(routes);
    }

    [HttpPost("reserve")]
    public async Task<IActionResult> Reserve(
        [FromBody] ReserveRouteRequest request,
        CancellationToken ct)
    {
        try
        {
            var (route, created) = await _routeService.ReserveAsync(request, ct);
            return created ? StatusCode(201, route) : Ok(route);
        }
        catch (RouteConflictException ex)
        {
            return Conflict(new ErrorResponse
            {
                Code = "route_conflict",
                Message = ex.Message,
            });
        }
    }

    [HttpPost("{routeId:guid}/release")]
    public async Task<ActionResult<ReleaseResponse>> Release(Guid routeId, CancellationToken ct)
    {
        var response = await _routeService.ReleaseAsync(routeId, ct);
        return Ok(response);
    }
}
