using GroundControl.Core.DTOs;
using GroundControl.Core.Interfaces;
using GroundControl.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;

namespace GroundControl.Api.Controllers;

[ApiController]
[Route("v1/routes")]
public class RoutesController : ControllerBase
{
    private readonly IRouteService _routeService;
    private readonly ILogger<RoutesController> _logger;

    public RoutesController(IRouteService routeService, ILogger<RoutesController> logger)
    {
        _routeService = routeService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetRoutes([FromQuery] string? vehicleId = null, [FromQuery] string? vehicleType = null)
    {
        try
        {
            var routes = await _routeService.GetRoutesAsync(vehicleId, vehicleType);
            return Ok(routes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting routes");
            return StatusCode(500, new ErrorResponse
            {
                Code = "internal_error",
                Message = "An error occurred while retrieving routes"
            });
        }
    }

    [HttpPost("reserve")]
    public async Task<IActionResult> ReserveRoute([FromBody] ReserveRouteRequest request)
    {
        try
        {
            var route = await _routeService.ReserveRouteAsync(request);
            
            // Check if this was an idempotent repeat (route already existed)
            var isNewRoute = route.CreatedAt >= DateTime.UtcNow.AddSeconds(-2);
            
            return isNewRoute ? StatusCode(201, route) : Ok(route);
        }
        catch (RouteConflictException ex)
        {
            _logger.LogWarning(ex, "Route conflict for vehicle {VehicleId}", request.VehicleId);
            return Conflict(new ErrorResponse
            {
                Code = "route_conflict",
                Message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation for vehicle {VehicleId}", request.VehicleId);
            return BadRequest(new ErrorResponse
            {
                Code = "invalid_operation",
                Message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reserving route for vehicle {VehicleId}", request.VehicleId);
            return StatusCode(500, new ErrorResponse
            {
                Code = "internal_error",
                Message = "An error occurred while reserving the route"
            });
        }
    }

    [HttpPost("{routeId}/release")]
    public async Task<IActionResult> ReleaseRoute(Guid routeId)
    {
        try
        {
            var result = await _routeService.ReleaseRouteAsync(routeId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing route {RouteId}", routeId);
            return StatusCode(500, new ErrorResponse
            {
                Code = "internal_error",
                Message = "An error occurred while releasing the route"
            });
        }
    }
}