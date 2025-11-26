using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Utiles;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

public abstract class MarkerGroupLayer : Layer
{
    protected IMap? map;
    protected readonly Dictionary<string, MarkerLayer> renderedMarkers = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("markers")]
    public IEnumerable<MarkerLayer> Markers => renderedMarkers.Values;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("layerType")]
    public abstract string LayerType { get; }
    public MarkerGroupLayer(string layerId) => this.Id = layerId;
    public abstract Task AddTo(IMap map);  
    public bool HasMarker(string id) => renderedMarkers.ContainsKey(id);
    public async Task AddMarkersAsync(IEnumerable<MarkerLayer> markers)
    {
        var markersJson = JsInteropJson.Serialize(markers);
        if (map != null)
        {
            await map.Interop.InvokeVoidAsync("addMarkersToLayerAsync", map.Id, this.Id, markersJson);
        }

        foreach (var marker in markers)
        {
            this.renderedMarkers.Add(marker.Id!, marker);
        }
    }
    public async Task RemoveMarkersAsync(IEnumerable<MarkerLayer> markers)
    {
        var ids = markers.Select(marker => marker.Id).ToArray();
        await RemoveMarkersAsync(ids);
    }
    public async Task RemoveMarkersAsync(IEnumerable<string> markerIds)
    {
        var ids = markerIds.ToArray();
        if (ids.Length == 0) return;

        if (map != null)
        {
            await map.Interop.InvokeVoidAsync("removeMarkersByIdsAsync", map.Id, this.Id, JsInteropJson.Serialize(ids));
        }

        foreach (var id in ids)
        {
            this.renderedMarkers.Remove(id!);
        }
    }
}
