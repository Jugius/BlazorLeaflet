using OohelpSoft.BlazorLeaflet.Base.Types;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.JsonConverters;
internal class LocationJsonConverter : JsonConverter<Location>
{
    public override Location? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("Expected start of array for Location");

        reader.Read();
        if (reader.TokenType != JsonTokenType.Number)
            throw new JsonException("Expected latitude number");
        double latitude = reader.GetDouble();

        reader.Read();
        if (reader.TokenType != JsonTokenType.Number)
            throw new JsonException("Expected longitude number");
        double longitude = reader.GetDouble();

        // Закрываем массив
        reader.Read();
        if (reader.TokenType != JsonTokenType.EndArray)
            throw new JsonException("Expected end of array for Location");

        return new Location(latitude, longitude);
    }

    public override void Write(Utf8JsonWriter writer, Location value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Latitude);
        writer.WriteNumberValue(value.Longitude);
        writer.WriteEndArray();
    }
}
