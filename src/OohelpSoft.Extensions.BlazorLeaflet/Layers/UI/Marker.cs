using OohelpSoft.BlazorLeaflet.Base;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Layers.UI;
public class Marker : MarkerLayer
{      

    [JsonPropertyName("icon")]
    public IconOptions? Icon { get; set; }    
}
