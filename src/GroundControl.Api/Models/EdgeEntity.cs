namespace GroundControl.Api.Models;

public class EdgeEntity
{
    public string EdgeId { get; set; } = string.Empty;
    public string FromNode { get; set; } = string.Empty;
    public string ToNode { get; set; } = string.Empty;
    public double Length { get; set; }
}
