using System.ComponentModel.DataAnnotations;

namespace GroundControl.Api.Models;

public class ReserveRouteRequest
{
    [Required]
    public Guid ReservationId { get; set; }

    [Required]
    public string VehicleId { get; set; } = string.Empty;

    [Required]
    public VehicleType VehicleType { get; set; }

    [Required]
    public string FromNode { get; set; } = string.Empty;

    [Required]
    public string ToNode { get; set; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int TtlMinutes { get; set; }
}

public class EdgePathItem
{
    public string EdgeId { get; set; } = string.Empty;
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;
}

public class RouteDto
{
    public Guid RouteId { get; set; }
    public string VehicleId { get; set; } = string.Empty;
    public VehicleType VehicleType { get; set; }
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;
    public List<EdgePathItem> EdgesPath { get; set; } = new();
    public RouteStatus Status { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ReleaseResponse
{
    public bool Released { get; set; }
}

public class OccupancyItemDto
{
    public string EdgeId { get; set; } = string.Empty;
    public string OccupiedBy { get; set; } = string.Empty;
    public Guid? RouteId { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ErrorResponse
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class SimTimeTick
{
    public string? EventId { get; set; }
    public int TickMinutes { get; set; }
}
