namespace GroundControl.Api.Models;

public enum VehicleType
{
    plane,
    bus
}

public enum RouteStatus
{
    allocated,
    finished,
    rejected
}

public class RouteEntity
{
    public Guid RouteId { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;

    /// <summary>JSON-serialized EdgePathItem list</summary>
    public string EdgesPathJson { get; set; } = "[]";

    public RouteStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int TtlRemainingMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
