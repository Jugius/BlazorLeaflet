using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Utiles;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

public abstract class MarkerGroupLayer : Layer
{
    protected readonly IJSObjectReference leafletInterop;
    protected readonly string mapId;
    protected readonly Dictionary<string, MarkerLayer> renderedMarkers = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("markers")]
    public IEnumerable<MarkerLayer> Markers => renderedMarkers.Values;


    protected MarkerGroupLayer(IJSObjectReference leafletInterop, string mapId, string layerId)
    {
        this.leafletInterop = leafletInterop;
        this.mapId = mapId;
        this.Id = layerId;
    }


    public bool HasMarker(string id) => renderedMarkers.ContainsKey(id);
    public async Task AddMarkersAsync(IEnumerable<MarkerLayer> markers)
    {
        var markersJson = JsInteropJson.Serialize(markers);
        await leafletInterop!.InvokeVoidAsync("addMarkersToLayerAsync", mapId, this.Id, markersJson);
        foreach (var marker in markers)
        {
            this.renderedMarkers.Add(marker.Id!, marker);
        }
    }
    public async Task RemoveMarkersAsync(IEnumerable<MarkerLayer> markers)
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
}
