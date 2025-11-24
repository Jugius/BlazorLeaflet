using OohelpSoft.BlazorLeaflet.Base.Types;
using OohelpSoft.BlazorLeaflet.Layers.UI;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

public abstract class MarkerLayer : Layer
{
    [JsonPropertyName("location")]
    public Location Location { get; set; } = default!;

    [JsonPropertyName("popup")]
    public Popup? Popup { get; set; }
}
