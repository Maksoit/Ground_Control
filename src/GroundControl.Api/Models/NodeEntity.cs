namespace GroundControl.Api.Models;

public class NodeEntity
{
    public string NodeId { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public string? NodeType { get; set; }
}
