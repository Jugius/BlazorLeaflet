using OohelpSoft.BlazorLeaflet.Base.Types;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Maps;
public class MapOptions
{
    [JsonPropertyName("center")]
    public Location? Center { get; set; }

    [JsonPropertyName("zoom")]
    public int? Zoom { get; set; }

    [JsonPropertyName("minZoom")]
    public int? MinZoom { get; set; } 

    [JsonPropertyName("maxZoom")]
    public int? MaxZoom { get; set; } 

    [JsonPropertyName("zoomControl")]
    public bool? ZoomControl { get; set; } = true;

    [JsonPropertyName("attributionControl")]
    public bool? AttributionControl { get; set; } = true;

    [JsonPropertyName("scrollWheelZoom")]
    public bool? ScrollWheelZoom { get; set; } = true;

    [JsonPropertyName("doubleClickZoom")]
    public bool? DoubleClickZoom { get; set; } = true;

    [JsonPropertyName("dragging")]
    public bool? Dragging { get; set; } = true;

    [JsonPropertyName("preferCanvas")]
    public bool? PreferCanvas { get; set; }

    public static MapOptions Default()
        => new()
        {
            Center = new Location(50.440769, 30.522495),
            Zoom = 13
        };
}
