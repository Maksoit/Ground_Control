using System.Text.Json;
using GroundControl.Api.Data;
using GroundControl.Api.Kafka;
using GroundControl.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace GroundControl.Api.Services;

public class RouteConflictException : Exception
{
    public RouteConflictException(string message) : base(message) { }
}

public class RouteService : IRouteService
{
    private readonly GroundDbContext _db;
    private readonly IPathfinderService _pathfinder;
    private readonly IKafkaProducer _kafka;
    private readonly ILogger<RouteService> _logger;

    public RouteService(
        GroundDbContext db,
        IPathfinderService pathfinder,
        IKafkaProducer kafka,
        ILogger<RouteService> logger)
    {
        _db = db;
        _pathfinder = pathfinder;
        _kafka = kafka;
        _logger = logger;
    }

    public async Task<(RouteDto Route, bool Created)> ReserveAsync(
        ReserveRouteRequest request,
        CancellationToken ct = default)
    {
        // Idempotency: if a route for this reservationId already exists, return it
        var existing = await _db.Routes
            .FirstOrDefaultAsync(r => r.RouteId == request.ReservationId, ct);

        if (existing != null)
            return (MapToDto(existing), false);

        // Load graph edges
        var edges = await _db.Edges.ToListAsync(ct);

        // Find path
        var path = _pathfinder.FindPath(request.FromNode, request.ToNode, edges);
        if (path == null)
            throw new RouteConflictException($"No path found from {request.FromNode} to {request.ToNode}");

        // Check occupancy for each edge in path
        var edgeIds = path.Select(e => e.EdgeId).ToList();
        var occupied = await _db.EdgeOccupancies
            .Where(o => edgeIds.Contains(o.EdgeId))
            .ToListAsync(ct);

        if (occupied.Count > 0)
        {
            var conflictEdges = string.Join(", ", occupied.Select(o => o.EdgeId));
            throw new RouteConflictException($"Edges are occupied: {conflictEdges}");
        }

        var now = DateTime.UtcNow;
        var route = new RouteEntity
        {
            RouteId = request.ReservationId,
            VehicleId = request.VehicleId,
            VehicleType = request.VehicleType,
            FromNode = request.FromNode,
            ToNode = request.ToNode,
            EdgesPathJson = JsonSerializer.Serialize(path),
            Status = RouteStatus.allocated,
            ExpiresAt = now.AddMinutes(request.TtlMinutes),
            TtlRemainingMinutes = request.TtlMinutes,
            CreatedAt = now,
            UpdatedAt = now,
        };

        _db.Routes.Add(route);

        // Mark edges as occupied
        foreach (var edgeItem in path)
        {
            _db.EdgeOccupancies.Add(new EdgeOccupancy
            {
                EdgeId = edgeItem.EdgeId,
                OccupiedBy = request.VehicleId,
                RouteId = route.RouteId,
                UpdatedAt = now,
            });
        }

        await _db.SaveChangesAsync(ct);

        var dto = MapToDto(route);

        await _kafka.PublishAsync("ground.route.allocated", route.RouteId.ToString(), new
        {
            routeId = route.RouteId,
            vehicleId = route.VehicleId,
            vehicleType = route.VehicleType.ToString(),
            edgesPath = path,
        }, ct);

        _logger.LogInformation(
            "Route {RouteId} allocated for vehicle {VehicleId} ({FromNode} -> {ToNode})",
            route.RouteId, route.VehicleId, route.FromNode, route.ToNode);

        return (dto, true);
    }

    public async Task<ReleaseResponse> ReleaseAsync(Guid routeId, CancellationToken ct = default)
    {
        var route = await _db.Routes.FirstOrDefaultAsync(r => r.RouteId == routeId, ct);
        if (route == null)
            return new ReleaseResponse { Released = false };

        if (route.Status == RouteStatus.finished)
            return new ReleaseResponse { Released = true };

        await FreeRouteEdgesAsync(route, ct);
        route.Status = RouteStatus.finished;
        route.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _kafka.PublishAsync("ground.route.released", route.RouteId.ToString(), new
        {
            routeId = route.RouteId,
            vehicleId = route.VehicleId,
            expired = false,
        }, ct);

        _logger.LogInformation("Route {RouteId} released", routeId);

        return new ReleaseResponse { Released = true };
    }

    public async Task<List<RouteDto>> GetRoutesAsync(
        string? vehicleId,
        VehicleType? vehicleType,
        CancellationToken ct = default)
    {
        var query = _db.Routes.AsQueryable();

        if (!string.IsNullOrWhiteSpace(vehicleId))
            query = query.Where(r => r.VehicleId == vehicleId);

        if (vehicleType.HasValue)
            query = query.Where(r => r.VehicleType == vehicleType.Value);

        var routes = await query.ToListAsync(ct);
        return routes.Select(MapToDto).ToList();
    }

    public async Task<List<OccupancyItemDto>> GetOccupancyAsync(CancellationToken ct = default)
    {
        var items = await _db.EdgeOccupancies.ToListAsync(ct);
        return items.Select(o => new OccupancyItemDto
        {
            EdgeId = o.EdgeId,
            OccupiedBy = o.OccupiedBy,
            RouteId = o.RouteId,
            UpdatedAt = o.UpdatedAt,
        }).ToList();
    }

    public async Task ExpireByTickAsync(int tickMinutes, string? eventId, CancellationToken ct = default)
    {
        // Deduplication
        if (!string.IsNullOrWhiteSpace(eventId))
        {
            var alreadyProcessed = await _db.ProcessedEvents
                .AnyAsync(e => e.EventId == eventId, ct);
            if (alreadyProcessed)
                return;

            _db.ProcessedEvents.Add(new ProcessedEvent
            {
                EventId = eventId,
                ProcessedAt = DateTime.UtcNow,
            });
        }

        var activeRoutes = await _db.Routes
            .Where(r => r.Status == RouteStatus.allocated)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var route in activeRoutes)
        {
            route.TtlRemainingMinutes -= tickMinutes;
            route.UpdatedAt = now;

            if (route.TtlRemainingMinutes <= 0)
            {
                await FreeRouteEdgesAsync(route, ct);
                route.Status = RouteStatus.finished;

                await _kafka.PublishAsync("ground.route.released", route.RouteId.ToString(), new
                {
                    routeId = route.RouteId,
                    vehicleId = route.VehicleId,
                    expired = true,
                }, ct);

                _logger.LogInformation("Route {RouteId} expired via TTL tick", route.RouteId);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // ──────────────────────────────────────────────────────────────────────────

    private async Task FreeRouteEdgesAsync(RouteEntity route, CancellationToken ct)
    {
        var path = JsonSerializer.Deserialize<List<EdgePathItem>>(route.EdgesPathJson)
                   ?? new List<EdgePathItem>();

        var edgeIds = path.Select(e => e.EdgeId).ToList();
        var occupancies = await _db.EdgeOccupancies
            .Where(o => o.RouteId == route.RouteId && edgeIds.Contains(o.EdgeId))
            .ToListAsync(ct);

        _db.EdgeOccupancies.RemoveRange(occupancies);
    }

    private static RouteDto MapToDto(RouteEntity route)
    {
        var path = JsonSerializer.Deserialize<List<EdgePathItem>>(route.EdgesPathJson)
                   ?? new List<EdgePathItem>();

        return new RouteDto
        {
            RouteId = route.RouteId,
            VehicleId = route.VehicleId,
            VehicleType = route.VehicleType,
            FromNode = route.FromNode,
            ToNode = route.ToNode,
            EdgesPath = path,
            Status = route.Status,
            ExpiresAt = route.ExpiresAt,
            CreatedAt = route.CreatedAt,
            UpdatedAt = route.UpdatedAt,
        };
    }
}
