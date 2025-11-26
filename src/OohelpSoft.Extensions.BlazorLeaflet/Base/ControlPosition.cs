using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

[JsonConverter(typeof(JsonConverters.ControlPositionConverter))]
public enum ControlPosition
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight
}
