using OohelpSoft.BlazorLeaflet.Base.Types;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Layers.UI;
public class IconOptions
{
    /// <summary>(required) The URL to the icon image (absolute or relative to your script path).</summary>
    [JsonPropertyName("iconUrl")]
    public string Url { get; set; } = default!;

    [JsonPropertyName("iconSize")]
    public Point? IconSize { get; set; }

    [JsonPropertyName("iconAnchor")]
    public Point? IconAnchor { get; set; }

    [JsonPropertyName("popupAnchor")]
    public Point? PopupAnchor { get; set; }
}
