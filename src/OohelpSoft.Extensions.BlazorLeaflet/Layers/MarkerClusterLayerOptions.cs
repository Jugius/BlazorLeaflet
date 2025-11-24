namespace OohelpSoft.BlazorLeaflet.Layers;

public sealed class MarkerClusterLayerOptions
{

    /// <summary>When you mouse over a cluster it shows the bounds of its markers.</summary>  
    /// <remarks> Default is <see langword="true"/>.</remarks>
    public bool? ShowCoverageOnHover { get; set; } // true

    /// <summary>When you click a cluster we zoom to its bounds</summary>
    /// <remarks> Default is <see langword="true"/>.</remarks>
    public bool? ZoomToBoundsOnClick { get; set; } // true

    /// <summary>When you click a cluster at the bottom zoom level we spiderfy it so you can see all of its markers.</summary>
    /// <remarks>Note: the spiderfy occurs at the current zoom level if all items within the cluster are still clustered at the maximum zoom level or at zoom specified by disableClusteringAtZoom option</remarks>
    public bool? SpiderfyOnMaxZoom { get; set; } // true

    /// <summary>
    /// Clusters and markers too far from the viewport are removed from the map for performance.
    /// </summary>
    /// <remarks> Default is <see langword="true"/>.</remarks>
    public bool? RemoveOutsideVisibleBounds { get; set; }

    /// <summary>
    /// If set, at this zoom level and below, markers will not be clustered. This defaults to disabled.
    /// </summary>
    /// <remarks>Note: you may be interested in disabling <see langword="spiderfyOnMaxZoom"/>  option when using <see langword="disableClusteringAtZoom"/></remarks>
    public int? DisableClusteringAtZoom { get; set; }

    /// <summary>
    ///  The maximum radius that a cluster will cover from the central marker (in pixels)/>.
    /// </summary>
    /// <remarks>
    /// Default is <see langword="true"/>.
    /// Decreasing will make more, smaller clusters. You can also use a function that accepts the current map zoom and returns the maximum cluster radius in pixels.
    /// </remarks>
    public int? MaxClusterRadius { get; set; } // 80
}
