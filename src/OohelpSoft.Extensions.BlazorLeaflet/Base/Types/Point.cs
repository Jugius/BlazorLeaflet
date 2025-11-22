using OohelpSoft.BlazorLeaflet.JsonConverters;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base.Types;

[JsonConverter(typeof(PointJsonConverter))]
public readonly struct Point
{
    [JsonPropertyName("x")]
    public int X { get; }

    [JsonPropertyName("y")]
    public int Y { get; }

    [JsonConstructor]
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}
