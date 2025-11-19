using OohelpSoft.BlazorLeaflet.Base.Types;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Maps;
public class MarkerIcon
{
    /// <summary>Путь к изображению или SVG-данные (если IsSvg = true)</summary>
    [JsonPropertyName("url")]
    public string Url { get; set; } = default!;

    /// <summary>Если true, то URL содержит SVG-код, который вставляется как data-URL</summary>
    [JsonPropertyName("isSvg")]
    public bool IsSvg { get; set; }

    [JsonPropertyName("iconSize")]
    public Point? IconSize { get; set; }

    [JsonPropertyName("iconAnchor")]
    public Point? IconAnchor { get; set; }

    [JsonPropertyName("popupAnchor")]
    public Point? PopupAnchor { get; set; }
}
