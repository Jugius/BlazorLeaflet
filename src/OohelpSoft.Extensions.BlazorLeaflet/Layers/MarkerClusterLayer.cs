using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Utiles;

namespace OohelpSoft.BlazorLeaflet.Layers;

public class MarkerClusterLayer : Base.MarkerGroupLayer
{
    private MarkerClusterLayer(IJSObjectReference leafletInterop, string mapId, string layerId) : base(leafletInterop, mapId, layerId)
    {
    }

    internal static async Task<MarkerClusterLayer> Create(IJSObjectReference leafletInterop, string mapId, string layerId, MarkerClusterLayerOptions? options)
    {
        var o = options ?? new MarkerClusterLayerOptions();
        var layer = new MarkerClusterLayer(leafletInterop, mapId, layerId);
        await leafletInterop!.InvokeVoidAsync("createMarkerClusterLayerAsync", mapId, layerId, JsInteropJson.Serialize(o));
        return layer;
    }
}
