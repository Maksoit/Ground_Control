using GroundControl.Core.DTOs;
using GroundControl.Core.Models;

namespace GroundControl.Core.Interfaces;

public interface IRouteService
{
    Task<RouteResponse> ReserveRouteAsync(ReserveRouteRequest request);
    Task<ReleaseResponse> ReleaseRouteAsync(Guid routeId);
    Task<List<RouteResponse>> GetRoutesAsync(string? vehicleId = null, string? vehicleType = null);
    Task ProcessSimulationTickAsync(Guid eventId, int tickMinutes);
}