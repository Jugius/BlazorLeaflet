namespace OohelpSoft.BlazorLeaflet.Base;

public class FocusOptions
{
    public FocusNavigationMode NavigationMode { get; set; } = FocusNavigationMode.Popup;

    /// <summary>
    /// Highlights the focused marker if it is displayed, independently of navigation.
    /// Ignored for polylines.
    /// </summary>
    public bool Highlight { get; set; } = true;

    /// <summary>
    /// The duration in seconds of a programmatic pan or flight. Zero disables animation.
    /// Leaflet controls the duration of popup auto-panning.
    /// </summary>
    public double AnimationDuration { get; set; } = 0.5;
}
