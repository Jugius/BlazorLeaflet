using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Layers.UI;
using OohelpSoft.BlazorLeaflet.Utiles;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Layers;
public class LayerGroup
{
    private readonly IJSObjectReference leafletInterop;
    private readonly string mapId;
    private readonly Dictionary<string, Marker> renderedMarkers = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("id")]
    public string Id { get; }

    [JsonPropertyName("markers")]
    public IEnumerable<Marker> Markers => renderedMarkers.Values;

    private LayerGroup(IJSObjectReference leafletInterop, string mapId, string layerId)
    {
        this.leafletInterop = leafletInterop;
        this.Id = layerId;
        this.mapId = mapId;
    }
    public bool HasMarker(string id) => renderedMarkers.ContainsKey(id);
    public async Task AddMarkersAsync(IEnumerable<Marker> markers)
    {
        await leafletInterop!.InvokeVoidAsync("addMarkersToLayerAsync", mapId, this.Id, JsInteropJson.Serialize(markers));
        foreach (var marker in markers)
        {
            this.renderedMarkers.Add(marker.Id!, marker);
        }
    }
    public async Task RemoveMarkersAsync(IEnumerable<Marker> markers)
    { 
        var ids = markers.Select(marker => marker.Id).ToArray();
        if (ids.Length == 0) return;
        await leafletInterop!.InvokeVoidAsync("removeMarkersByIdsAsync", mapId, this.Id, JsInteropJson.Serialize(ids));
        foreach (var id in ids)
        { 
            this.renderedMarkers.Remove(id!);
        }
    }
    public async Task RemoveMarkersAsync(IEnumerable<string> markerIds)
    {
        var ids = markerIds.ToArray();
        if (ids.Length == 0) return;
        await leafletInterop!.InvokeVoidAsync("removeMarkersByIdsAsync", mapId, this.Id, JsInteropJson.Serialize(ids));
        foreach (var id in ids)
        {
            this.renderedMarkers.Remove(id!);
        }
    }

    internal static async Task<LayerGroup> Create(IJSObjectReference leafletInterop, string mapId, string layerId)
    { 
        var layer = new LayerGroup(leafletInterop, mapId, layerId);
        await leafletInterop!.InvokeVoidAsync("createLayerGroupAsync", mapId, layerId);
        return layer;
    }
}
