using Microsoft.JSInterop;

namespace OohelpSoft.BlazorLeaflet.Layers;
public class FeatureGroup : Base.MarkerGroupLayer
{
    private FeatureGroup(IJSObjectReference leafletInterop, string mapId, string layerId) : base(leafletInterop, mapId, layerId)
    {
    }

    internal static async Task<FeatureGroup> Create(IJSObjectReference leafletInterop, string mapId, string layerId)
    { 
        var layer = new FeatureGroup(leafletInterop, mapId, layerId);
        await leafletInterop!.InvokeVoidAsync("createFeatureGroupAsync", mapId, layerId);
        return layer;
    }
}
