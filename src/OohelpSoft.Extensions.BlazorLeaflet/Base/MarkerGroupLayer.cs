using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Base.Interfaces;
using OohelpSoft.BlazorLeaflet.Utiles;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

public abstract class MarkerGroupLayer : Layer
{
    protected IMap? _map;
    protected readonly Dictionary<string, MarkerLayer> _markers;

    [JsonPropertyName("markers")]
    public IEnumerable<MarkerLayer> Markers => _markers.Values;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("layerType")]
    public abstract string LayerType { get; }
    public MarkerGroupLayer(string layerId)
    {
        this.Id = layerId;
        this._markers = new Dictionary<string, MarkerLayer>(StringComparer.OrdinalIgnoreCase);
    }
    public MarkerGroupLayer(string layerId, IEnumerable<MarkerLayer> markers)
    {
        this.Id = layerId;
        this._markers = markers.ToDictionary(marker => marker.Id!, StringComparer.OrdinalIgnoreCase);
    }
    internal virtual async Task AddTo(IMap map)
    {
        this._map = map;
        var layerJson = JsInteropJson.Serialize(this);
        await this._map.Interop.InvokeVoidAsync("addMarkerGroupLayer", this._map.Id, layerJson);
    }
    public bool HasMarker(string id) => _markers.ContainsKey(id);
    public async Task AddMarkersAsync(IEnumerable<MarkerLayer> markers)
    {        
        if (_map != null)
        {
            var markersJson = JsInteropJson.Serialize(markers.ToArray());
            await _map.Interop.InvokeVoidAsync("addMarkersToLayerAsync", _map.Id, this.Id, markersJson);
        }

        foreach (var marker in markers)
        {
            this._markers.Add(marker.Id!, marker);
        }
    }
    public async Task RemoveMarkersAsync(IEnumerable<MarkerLayer> markers)
    {
        var ids = markers.Select(marker => marker.Id);
        await RemoveMarkersAsync(ids);
    }
    public async Task RemoveMarkersAsync(IEnumerable<string> markerIds)
    {
        var ids = markerIds.ToArray();
        if (ids.Length == 0) return;

        if (_map != null)
        {
            await _map.Interop.InvokeVoidAsync("removeMarkersByIdsAsync", _map.Id, this.Id, JsInteropJson.Serialize(ids));
        }

        foreach (var id in ids)
        {
            this._markers.Remove(id!);
        }
    }
    public async Task ClearMarkersAsync()
    {
        if (_map != null)
        {
            await _map.Interop.InvokeVoidAsync("clearLayer", _map.Id, this.Id);
        }
        this._markers.Clear();
    }
}
