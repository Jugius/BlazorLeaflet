
using OohelpSoft.BlazorLeaflet.Base.Types;

namespace OohelpSoft.BlazorLeaflet.Layers.UI;

public class PopupOptions
{
    /// <summary>
    /// The offset of the popup position.
    /// </summary>
    public Point? Offset { get; set; }

    /// <summary>
    /// Max width of the popup, in pixels.
    /// </summary>
    public int? MaxWidth { get; set; } // 300 default

    /// <summary>
    /// Min width of the popup, in pixels.
    /// </summary>
    public int? MinWidth { get; set; } // 50 default

    /// <summary>
    /// If set, creates a scrollable container of the given height inside a popup if its content exceeds it. The scrollable container can be styled using the leaflet-popup-scrolled CSS class selector.
    /// </summary>
    public int? MaxHeight { get; set; }
    
    /// <summary>
    /// Controls the presence of a close button in the popup.
    /// </summary>
    public bool? CloseButton {  get; set; }

    /// <summary>
    /// Set it to false if you don't want the map to do panning animation to fit the opened popup.
    /// </summary>
    public bool? AutoPan { get; set; } // true default

    /// <summary>
    /// The margin between the popup and the top left corner of the map view after autopanning was performed.
    /// </summary>
    public Point? AutoPanPaddingTopLeft { get; set; } // null default

    /// <summary>
    /// The margin between the popup and the bottom right corner of the map view after autopanning was performed.
    /// </summary>
    public Point? AutoPanPaddingBottomRight { get; set; } // null default

    /// <summary>
    /// Equivalent of setting both top left and bottom right autopan padding to the same value.
    /// </summary>
    public Point? AutoPanPadding {  get; set; } // [5, 5] default

    /// <summary>
    /// Set it to true if you want to prevent users from panning the popup off of the screen while it is open.
    /// </summary>
    public bool? KeepInView { get; set; } // false default



    /// <summary>
    /// A custom CSS class name to assign to the popup.
    /// </summary>
    public string? ClassName { get; set; }
}
