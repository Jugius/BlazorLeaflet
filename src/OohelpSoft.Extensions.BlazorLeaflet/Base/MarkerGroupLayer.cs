using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Base.Interfaces;
using OohelpSoft.BlazorLeaflet.Layers.UI;
using OohelpSoft.BlazorLeaflet.Models;
using OohelpSoft.BlazorLeaflet.Utiles;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Base;

public abstract class MarkerGroupLayer : Layer
{
    protected IMap? _map;
    protected readonly Dictionary<string, MarkerLayer> _markers;
    private string? _focusAttachmentKey;

    [JsonPropertyName("markers")]
    public IEnumerable<MarkerLayer> Markers => _markers.Values;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("layerType")]
    public abstract string LayerType { get; }

    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; init; } = true;
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
        var focusAttachmentKey = Guid.NewGuid().ToString("N");
        await map.Interop.InvokeVoidAsync("addMarkerGroupLayer", map.Id, layerJson, focusAttachmentKey);
        _focusAttachmentKey = focusAttachmentKey;
    }
    public bool HasMarker(string id) => _markers.ContainsKey(id);
    internal void Detach()
    {
        _map = null;
        _markers.Clear();
    }
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

    public async Task UpdateMarkerIconAsync(string markerId, IconOptions icon)
    {
        if(_markers.TryGetValue(markerId, out var marker) && marker is Marker m)
        {
            m.Icon = icon;
            if (_map != null)
            {
                await _map.Interop.InvokeVoidAsync("updateMarkerIcon", _map.Id, this.Id, markerId, JsInteropJson.Serialize(icon));
            }
        }
    }
    public async Task UpdateMarkersIconsBulkAsync(List<MarkerIconUpdate> iconUpdates)
    {
        if (_map != null)        
        {
            await _map.Interop.InvokeVoidAsync("updateMarkersIconsBulk", _map.Id, this.Id, JsInteropJson.Serialize(iconUpdates));
        }
    }
    public Task FocusMarker(string markerId) => FocusMarker(markerId, null);

    /// <summary>
    /// With options, the final zoom is the starting zoom within the map's zoom limits.
    /// A flight may change zoom temporarily.
    /// An explicit mode does not expand clusters. A marker in a cluster group
    /// can open its popup or be highlighted only while the plugin renders it.
    /// </summary>
    public async Task FocusMarker(string markerId, FocusOptions? options = null)
    {
        if (_map is not { } map) return;

        if (options is null)
        {
            await map.Interop.InvokeVoidAsync("focusMarker", map.Id, Id, markerId);
            return;
        }

        if (_focusAttachmentKey is not { } focusAttachmentKey || !_markers.TryGetValue(markerId, out var marker)) return;
        var leafletMap = map as LeafletMap;
        if (leafletMap is not null && (!leafletMap.TryGetLayer(Id, out var current) || !ReferenceEquals(current, this)))
            return;

        await map.Interop.InvokeVoidAsync("focusMarker", map.Id, Id, marker.Id, JsInteropJson.Serialize(options),
            leafletMap?.CreationKey, focusAttachmentKey);
    }
}
