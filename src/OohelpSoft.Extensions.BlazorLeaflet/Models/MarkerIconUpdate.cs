using OohelpSoft.BlazorLeaflet.Layers.UI;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Models;

public class MarkerIconUpdate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;
    
    [JsonPropertyName("icon")]
    public IconOptions Icon { get; set; } = default!;
}
