namespace GroundControl.Core.Models;

public class Edge
{
    public string EdgeId { get; set; } = string.Empty;
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;
    public decimal Length { get; set; }
}