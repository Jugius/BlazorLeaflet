using OohelpSoft.BlazorLeaflet.Layers.Vector;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Models;

public class PolylineStyleUpdate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;

    [JsonPropertyName("options")]
    public PolylineOptions Options { get; set; } = default!;
}