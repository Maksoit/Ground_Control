using GroundControl.Api.Models;

namespace GroundControl.Api.Services;

public interface IRouteService
{
    Task<(RouteDto Route, bool Created)> ReserveAsync(ReserveRouteRequest request, CancellationToken ct = default);
    Task<ReleaseResponse> ReleaseAsync(Guid routeId, CancellationToken ct = default);
    Task<List<RouteDto>> GetRoutesAsync(string? vehicleId, VehicleType? vehicleType, CancellationToken ct = default);
    Task<List<OccupancyItemDto>> GetOccupancyAsync(CancellationToken ct = default);
    Task ExpireByTickAsync(int tickMinutes, string? eventId, CancellationToken ct = default);
}
