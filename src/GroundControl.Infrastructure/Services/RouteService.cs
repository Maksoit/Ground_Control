using GroundControl.Core.DTOs;
using GroundControl.Core.Interfaces;
using GroundControl.Core.Models;
using GroundControl.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GroundControl.Infrastructure.Services;

public class RouteService : IRouteService
{
    private readonly GroundControlDbContext _context;
    private readonly IPathfinder _pathfinder;
    private readonly ILogger<RouteService> _logger;

    public RouteService(
        GroundControlDbContext context,
        IPathfinder pathfinder,
        ILogger<RouteService> logger)
    {
        _context = context;
        _pathfinder = pathfinder;
        _logger = logger;
    }

    public async Task<RouteResponse> ReserveRouteAsync(ReserveRouteRequest request)
    {
        // Check if route already exists (idempotency)
        var existingRoute = await _context.Routes
            .FirstOrDefaultAsync(r => r.RouteId == request.ReservationId);

        if (existingRoute != null)
        {
            _logger.LogInformation("Route {RouteId} already exists, returning existing route", request.ReservationId);
            return MapToResponse(existingRoute);
        }

        // Get all edges
        var edges = await _context.Edges.ToListAsync();

        // Find path
        var path = _pathfinder.FindPath(request.FromNode, request.ToNode, edges);
        if (path == null || path.Count == 0)
        {
            _logger.LogWarning("No path found from {FromNode} to {ToNode}", request.FromNode, request.ToNode);
            throw new InvalidOperationException($"No path found from {request.FromNode} to {request.ToNode}");
        }

        // Check occupancy for all edges in path
        var edgeIds = path.Select(e => e.EdgeId).ToList();
        var occupiedEdges = await _context.EdgeOccupancy
            .Where(o => edgeIds.Contains(o.EdgeId))
            .ToListAsync();

        if (occupiedEdges.Any())
        {
            var occupiedEdgeIds = string.Join(", ", occupiedEdges.Select(o => o.EdgeId));
            _logger.LogWarning("Route conflict: edges {EdgeIds} are occupied", occupiedEdgeIds);
            throw new RouteConflictException($"Some edges are occupied: {occupiedEdgeIds}");
        }

        // Create route
        var vehicleType = Enum.Parse<VehicleType>(request.VehicleType, true);
        var route = new Route
        {
            RouteId = request.ReservationId,
            VehicleId = request.VehicleId,
            VehicleType = vehicleType,
            FromNode = request.FromNode,
            ToNode = request.ToNode,
            EdgesPath = path,
            Status = RouteStatus.Allocated,
            TtlRemainingMinutes = request.TtlMinutes,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Routes.Add(route);

        // Occupy all edges
        foreach (var edge in path)
        {
            var occupancy = new EdgeOccupancy
            {
                EdgeId = edge.EdgeId,
                OccupiedBy = request.VehicleId,
                RouteId = request.ReservationId,
                UpdatedAt = DateTime.UtcNow
            };
            _context.EdgeOccupancy.Add(occupancy);
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Route {RouteId} reserved for vehicle {VehicleId}", route.RouteId, route.VehicleId);

        return MapToResponse(route);
    }

    public async Task<ReleaseResponse> ReleaseRouteAsync(Guid routeId)
    {
        var route = await _context.Routes.FindAsync(routeId);

        if (route == null)
        {
            _logger.LogWarning("Route {RouteId} not found", routeId);
            return new ReleaseResponse { Released = false };
        }

        if (route.Status == RouteStatus.Finished)
        {
            _logger.LogInformation("Route {RouteId} already finished", routeId);
            return new ReleaseResponse { Released = true };
        }

        // Release all edges
        var edgeIds = route.EdgesPath.Select(e => e.EdgeId).ToList();
        var occupancies = await _context.EdgeOccupancy
            .Where(o => edgeIds.Contains(o.EdgeId) && o.RouteId == routeId)
            .ToListAsync();

        _context.EdgeOccupancy.RemoveRange(occupancies);

        // Update route status
        route.Status = RouteStatus.Finished;
        route.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Route {RouteId} released, freed {Count} edges", routeId, occupancies.Count);

        return new ReleaseResponse { Released = true };
    }

    public async Task<List<RouteResponse>> GetRoutesAsync(string? vehicleId = null, string? vehicleType = null)
    {
        var query = _context.Routes.AsQueryable();

        if (!string.IsNullOrEmpty(vehicleId))
        {
            query = query.Where(r => r.VehicleId == vehicleId);
        }

        if (!string.IsNullOrEmpty(vehicleType))
        {
            var type = Enum.Parse<VehicleType>(vehicleType, true);
            query = query.Where(r => r.VehicleType == type);
        }

        var routes = await query.ToListAsync();
        return routes.Select(MapToResponse).ToList();
    }

    public async Task ProcessSimulationTickAsync(Guid eventId, int tickMinutes)
    {
        // Check if event already processed (idempotency)
        var processed = await _context.ProcessedEvents
            .AnyAsync(e => e.EventId == eventId);

        if (processed)
        {
            _logger.LogDebug("Event {EventId} already processed", eventId);
            return;
        }

        // Get all allocated routes
        var allocatedRoutes = await _context.Routes
            .Where(r => r.Status == RouteStatus.Allocated)
            .ToListAsync();

        var expiredRoutes = new List<Route>();

        foreach (var route in allocatedRoutes)
        {
            route.TtlRemainingMinutes -= tickMinutes;
            route.UpdatedAt = DateTime.UtcNow;

            if (route.TtlRemainingMinutes <= 0)
            {
                _logger.LogWarning("Route {RouteId} expired (TTL exhausted)", route.RouteId);
                expiredRoutes.Add(route);
            }

            // Mark route as modified so EF Core tracks the changes
            _context.Routes.Update(route);
        }

        // Release expired routes
        foreach (var route in expiredRoutes)
        {
            var edgeIds = route.EdgesPath.Select(e => e.EdgeId).ToList();
            var occupancies = await _context.EdgeOccupancy
                .Where(o => edgeIds.Contains(o.EdgeId) && o.RouteId == route.RouteId)
                .ToListAsync();

            _context.EdgeOccupancy.RemoveRange(occupancies);
            route.Status = RouteStatus.Finished;
            route.UpdatedAt = DateTime.UtcNow;
        }

        // Mark event as processed
        _context.ProcessedEvents.Add(new ProcessedEvent
        {
            EventId = eventId,
            ProcessedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        if (expiredRoutes.Any())
        {
            _logger.LogInformation("Processed tick {EventId}: expired {Count} routes", eventId, expiredRoutes.Count);
        }
    }

    private RouteResponse MapToResponse(Route route)
    {
        return new RouteResponse
        {
            RouteId = route.RouteId,
            VehicleId = route.VehicleId,
            VehicleType = route.VehicleType.ToString().ToLower(),
            FromNode = route.FromNode,
            ToNode = route.ToNode,
            EdgesPath = route.EdgesPath.Select(e => new EdgePathItemDto
            {
                EdgeId = e.EdgeId,
                FromNode = e.FromNode,
                ToNode = e.ToNode
            }).ToList(),
            Status = route.Status.ToString().ToLower(),
            ExpiresAt = route.ExpiresAt,
            CreatedAt = route.CreatedAt,
            UpdatedAt = route.UpdatedAt
        };
    }
}

public class RouteConflictException : Exception
{
    public RouteConflictException(string message) : base(message) { }
}