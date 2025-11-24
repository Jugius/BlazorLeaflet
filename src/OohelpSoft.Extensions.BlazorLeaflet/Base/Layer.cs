using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

public abstract class Layer
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = default!;
    
}
