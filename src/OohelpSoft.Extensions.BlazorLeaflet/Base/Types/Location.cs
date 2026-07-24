using OohelpSoft.BlazorLeaflet.JsonConverters;
using System.Globalization;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base.Types;


[JsonConverter(typeof(LocationJsonConverter))]
public readonly struct Location
{
    public double Latitude { get; }
    public double Longitude { get; }
    public Location() { }

    [JsonConstructor]
    public Location(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "{0:0.00000000},{1:0.00000000}", Latitude, Longitude);
}
