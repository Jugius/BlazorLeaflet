
namespace OohelpSoft.BlazorLeaflet.Layers;
public class FeatureGroup : Base.MarkerGroupLayer
{    
    public override string LayerType => "FeatureGroup";
    public FeatureGroup(string layerId) : base(layerId) { }
    public FeatureGroup(string layerId, IEnumerable<Base.MarkerLayer> markers) : base(layerId, markers) { }
}
