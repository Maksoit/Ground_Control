namespace GroundControl.Core.DTOs;

public class RouteResponse
{
    public Guid RouteId { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public string VehicleType { get; set; } = string.Empty;
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;
    public List<EdgePathItemDto> EdgesPath { get; set; } = new();
    public string Status { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EdgePathItemDto
{
    public string EdgeId { get; set; } = string.Empty;
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;
}

public class ReleaseResponse
{
    public bool Released { get; set; }
}

public class ErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class OccupancyItemDto
{
    public string EdgeId { get; set; } = string.Empty;
    public string OccupiedBy { get; set; } = string.Empty;
    public Guid? RouteId { get; set; }
    public DateTime UpdatedAt { get; set; }
}