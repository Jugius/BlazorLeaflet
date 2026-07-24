using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MapCursor
{
    [JsonPropertyName("default")]
    Default,

    [JsonPropertyName("crosshair")]
    Crosshair,

    [JsonPropertyName("pointer")]
    Pointer,

    [JsonPropertyName("grab")]
    Grab,

    [JsonPropertyName("grabbing")]
    Grabbing,

    [JsonPropertyName("move")]
    Move,

    [JsonPropertyName("not-allowed")]
    NotAllowed,

    [JsonPropertyName("help")]
    Help,

    [JsonPropertyName("zoom-in")]
    ZoomIn,

    [JsonPropertyName("zoom-out")]
    ZoomOut
}
