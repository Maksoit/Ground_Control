namespace GroundControl.Core.DTOs;

public class ReserveRouteRequest
{
    public Guid ReservationId { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;
    public int TtlMinutes { get; set; }
}