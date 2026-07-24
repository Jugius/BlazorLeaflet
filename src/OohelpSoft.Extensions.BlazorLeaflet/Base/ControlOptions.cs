using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

public abstract class ControlOptions
{
    [JsonPropertyName("position")]
    public ControlPosition? Position { get; set; }
}
