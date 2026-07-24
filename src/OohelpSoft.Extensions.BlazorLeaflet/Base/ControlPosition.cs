using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ControlPosition
{
    [JsonPropertyName("topleft")]
    TopLeft,

    [JsonPropertyName("topright")]
    TopRight,

    [JsonPropertyName("bottomleft")]
    BottomLeft,

    [JsonPropertyName("bottomright")]
    BottomRight
}
