using OohelpSoft.BlazorLeaflet.JsonConverters;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base.Types;

[JsonConverter(typeof(PointJsonConverter))]
public class Point
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }

    public Point() { }
    public Point(int x, int y)
    {
        X = x;
        Y = y;
    }
}
