using OohelpSoft.BlazorLeaflet.Base;

namespace OohelpSoft.BlazorLeaflet.Controls;

public sealed class LayersControlOptions : ControlOptions
{

    /// <summary>
    /// If true, the control will be collapsed into an icon and expanded on mouse hover, touch, or keyboard activation.
    /// </summary>
    /// <remarks>Default is <see langword="true"/></remarks>
    public bool? Collapsed { get; set; }

    /// <summary>
    /// If true, the control will assign zIndexes in increasing order to all of its layers so that the order is preserved when switching them on/off.
    /// </summary>
    /// <remarks>Default is <see langword="true"/></remarks>
    public bool? AutoZIndex { get; set; }


    /// <summary>
    /// If true, the base layers in the control will be hidden when there is only one.
    /// </summary>
    /// <remarks>Default is <see langword="false"/></remarks>
    public bool? HideSingleBase { get; set; }

    /// <summary>
    /// Whether to sort the layers. When false, layers will keep the order in which they were added to the control.
    /// </summary>
    /// <remarks>Default is <see langword="false"/></remarks>
    public bool? SortLayers { get; set; }

    }
