namespace GroundControl.Api.Models;

public class EdgeOccupancy
{
    public string EdgeId { get; set; } = string.Empty;
    public string OccupiedBy { get; set; } = string.Empty;
    public Guid? RouteId { get; set; }
    public DateTime UpdatedAt { get; set; }
}
