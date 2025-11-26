
namespace OohelpSoft.BlazorLeaflet.Layers;

public class MarkerClusterLayer : Base.MarkerGroupLayer
{
    public override string LayerType => "MarkerClusterLayer";
    public MarkerClusterLayerOptions Options { get; set; } = new MarkerClusterLayerOptions();

    public MarkerClusterLayer(string layerId) : base(layerId) { }
    public MarkerClusterLayer(string layerId, IEnumerable<Base.MarkerLayer> markers) : base(layerId, markers) { }
}
