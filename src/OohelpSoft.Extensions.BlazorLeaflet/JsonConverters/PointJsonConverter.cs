using OohelpSoft.BlazorLeaflet.Base.Types;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.JsonConverters;
internal class PointJsonConverter : JsonConverter<Point>
{
    public override Point? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array for Point");

        reader.Read();
        if (reader.TokenType != JsonTokenType.Number)
            throw new JsonException("Expected number for X coordinate");
        int x = reader.GetInt32();

        reader.Read();
        if (reader.TokenType != JsonTokenType.Number)
            throw new JsonException("Expected number for Y coordinate");
        int y = reader.GetInt32();

        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("Expected end of array for Point");

        return new Point(x, y);
    }

    public override void Write(Utf8JsonWriter writer, Point value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}
