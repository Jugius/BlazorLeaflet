using System.Text.Json;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Utiles;
internal static class JsInteropJson
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, value!.GetType(), _options);
    }
}
