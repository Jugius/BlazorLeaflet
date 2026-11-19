using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Layers.Vector;

public class PolylineOptions
{
    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("weight")]
    public int? Weight { get; set; }

    [JsonPropertyName("opacity")]
    public double? Opacity { get; set; }

    [JsonPropertyName("dashArray")]
    public string? DashArray { get; set; }

    [JsonPropertyName("lineCap")]
    public string? LineCap { get; set; }

    [JsonPropertyName("lineJoin")]
    public string? LineJoin { get; set; }
}