using OohelpSoft.BlazorLeaflet.Base;

namespace OohelpSoft.BlazorLeaflet.Controls;

public class ScaleControlOptions : ControlOptions
{
    /// <summary>
    /// Maximum width of the control in pixels. The width is set dynamically to show round values (e.g. 100, 200, 500).
    /// </summary>
    /// <remarks>Default is 100.</remarks>
    public int? MaxWidth { get; set; }

    /// <summary>
    /// Whether to show the imperial scale line (mi/ft).
    /// </summary>
    /// <remarks>Default is <see langword="true"/></remarks>
    public bool? Imperial { get; set; }

    /// <summary>
    /// Whether to show the metric scale line (m/km).
    /// </summary>
    /// <remarks>Default is <see langword="true"/></remarks>
    public bool? Metric { get; set; }

    /// <summary>
    /// If true, the control is updated on moveend, otherwise it's always up-to-date (updated on move).
    /// </summary>
    /// <remarks>Default is <see langword="false"/></remarks>
    public bool? UpdateWhenIdle { get; set; }
}
