using OohelpSoft.BlazorLeaflet.Base;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.JsonConverters;

public sealed class ControlPositionConverter : JsonConverter<ControlPosition>
{
    private const string TopLeft = "topleft";
    private const string TopRight = "topright";
    private const string BottomLeft = "bottomleft";
    private const string BottomRight = "bottomright";
    public override ControlPosition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()?.ToLowerInvariant();
        return value switch
        {
            TopLeft => ControlPosition.TopLeft,
            TopRight => ControlPosition.TopRight,
            BottomLeft => ControlPosition.BottomLeft,
            BottomRight => ControlPosition.BottomRight,
            _ => throw new JsonException($"Unknown control position: {value}")
        };
    }

    public override void Write(Utf8JsonWriter writer, ControlPosition value, JsonSerializerOptions options)
    {
        var str = value switch
        {
            ControlPosition.TopLeft => TopLeft,
            ControlPosition.TopRight => TopRight,
            ControlPosition.BottomLeft => BottomLeft,
            ControlPosition.BottomRight => BottomRight,
            _ => throw new JsonException($"Unknown control position: {value}")
        };

        writer.WriteStringValue(str);
    }
}
