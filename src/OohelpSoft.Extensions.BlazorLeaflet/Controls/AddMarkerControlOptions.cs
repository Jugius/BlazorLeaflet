using OohelpSoft.BlazorLeaflet.Base;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Controls;

public sealed class AddMarkerControlOptions : ControlOptions
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = "Добавить маркер";

    /// <summary>
    /// Содержимое кнопки (HTML/SVG или просто эмодзи)
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = "📍";

    [JsonPropertyName("cursor")]
    public MapCursor Cursor { get; set; } = MapCursor.Crosshair;
}
