namespace GroundControl.Core.Models;

public class Node
{
    public string NodeId { get; set; } = string.Empty;
    public decimal X { get; set; }
    public decimal Y { get; set; }
    public string? NodeType { get; set; }
}