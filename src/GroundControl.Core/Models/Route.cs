namespace GroundControl.Core.Models;

public class Route
{
    public Guid RouteId { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;
    public List<EdgePathItem> EdgesPath { get; set; } = new();
    public RouteStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int TtlRemainingMinutes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EdgePathItem
{
    public string EdgeId { get; set; } = string.Empty;
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;
}

public enum VehicleType
{
    Plane,
    Bus
}

public enum RouteStatus
{
    Allocated,
    Finished,
    Rejected
}