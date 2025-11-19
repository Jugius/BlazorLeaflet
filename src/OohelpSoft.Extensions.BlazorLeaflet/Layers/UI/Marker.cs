using OohelpSoft.BlazorLeaflet.Base;
using OohelpSoft.BlazorLeaflet.Base.Types;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Layers.UI;
public class Marker : Layer
{   
    [JsonPropertyName("location")]
    public Location Location { get; set; } = default!;

    [JsonPropertyName("icon")]
    public IconOptions? Icon { get; set; }    
}
