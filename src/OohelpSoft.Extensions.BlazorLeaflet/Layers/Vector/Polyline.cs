using OohelpSoft.BlazorLeaflet.Base;
using OohelpSoft.BlazorLeaflet.Base.Types;
using OohelpSoft.BlazorLeaflet.Layers.UI;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Layers.Vector;

public class Polyline : Layer
{
    [JsonPropertyName("locations")]
    public IReadOnlyList<Location> Locations { get; set; } = [];

    [JsonPropertyName("options")]
    public PolylineOptions? Options { get; set; }

    [JsonPropertyName("popup")]
    public Popup? Popup { get; set; }
}