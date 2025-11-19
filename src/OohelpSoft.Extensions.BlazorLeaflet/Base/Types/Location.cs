using OohelpSoft.BlazorLeaflet.JsonConverters;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base.Types;


[JsonConverter(typeof(LocationJsonConverter))]
public class Location
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Location() { }

    public Location(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public override string ToString() => $"{Latitude}, {Longitude}";
}
