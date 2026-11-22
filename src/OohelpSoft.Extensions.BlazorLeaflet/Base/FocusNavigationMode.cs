using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

[JsonConverter(typeof(JsonStringEnumConverter<FocusNavigationMode>))]
public enum FocusNavigationMode
{
    /// <summary>
    /// Opens the bound popup with its existing options. Without a popup, pans only if
    /// the focus point is outside the current view.
    /// </summary>
    Popup,
    PanTo,
    /// <summary>
    /// Flies to the focus point and returns to the starting zoom within the map's zoom limits.
    /// </summary>
    FlyTo
}
