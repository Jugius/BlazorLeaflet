using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Utiles;

namespace OohelpSoft.BlazorLeaflet.Layers;
public class FeatureGroup(string layerId) : Base.MarkerGroupLayer(layerId)
{
    public override string LayerType => "FeatureGroup";

    public override async Task AddTo(IMap map)
    {
        this.map = map;
        var layerJson = JsInteropJson.Serialize(this);
        await this.map.Interop.InvokeVoidAsync("addMarkerGroupLayer", this.map.Id, layerJson);
    }
}
