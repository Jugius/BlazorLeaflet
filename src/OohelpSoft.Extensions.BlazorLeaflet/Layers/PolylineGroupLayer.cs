using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Base;
using OohelpSoft.BlazorLeaflet.Base.Interfaces;
using OohelpSoft.BlazorLeaflet.Base.Types;
using OohelpSoft.BlazorLeaflet.Layers.Vector;
using OohelpSoft.BlazorLeaflet.Models;
using OohelpSoft.BlazorLeaflet.Utiles;
using System.Text.Json.Serialization;

namespace OohelpSoft.BlazorLeaflet.Layers;

public class PolylineGroupLayer : Layer
{
    private IMap? _map;
    private int _generation;
    private readonly SemaphoreSlim _operations = new(1, 1);
    private readonly Dictionary<string, Polyline> _polylines = new(StringComparer.Ordinal);

    [JsonPropertyName("polylines")]
    public IEnumerable<Polyline> Polylines => _polylines.Values;
    [JsonPropertyName("name")]
    public string? Name { get; set; }
    [JsonPropertyName("layerType")]
    public string LayerType => "PolylineGroup";
    [JsonPropertyName("isVisible")]
    public bool IsVisible { get; init; } = true;

    public PolylineGroupLayer(string layerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        Id = layerId;
    }

    public PolylineGroupLayer(string layerId, IEnumerable<Polyline> polylines) : this(layerId)
    {
        var items = polylines.ToArray();
        ValidateAdditions(items);
        foreach (var item in items) _polylines.Add(item.Id, item);
    }

    public bool HasPolyline(string id) => _polylines.ContainsKey(id);

    internal async Task AddTo(IMap map)
    {
        await _operations.WaitAsync();
        try
        {
            if (_map is not null) throw new InvalidOperationException("The group is already attached to a map.");
            foreach (var item in _polylines.Values) ValidatePolyline(item);
            var generation = _generation;
            await map.Interop.InvokeVoidAsync("addPolylineGroupLayer", map.Id, JsInteropJson.Serialize(this), (map as LeafletMap)?.CreationKey);
            EnsureGeneration(generation);
            _map = map;
        }
        finally { _operations.Release(); }
    }

    internal void Detach()
    {
        _generation++;
        _map = null;
    }

    public async Task AddPolylinesAsync(IEnumerable<Polyline> polylines)
    {
        var items = polylines.ToArray();
        await _operations.WaitAsync();
        try
        {
            ValidateAdditions(items);
            if (items.Length == 0) return;
            await InvokeAsync("addPolylinesToLayerAsync", JsInteropJson.Serialize(items));
            foreach (var item in items) _polylines.Add(item.Id, item);
        }
        finally { _operations.Release(); }
    }

    public async Task RemovePolylinesAsync(IEnumerable<string> polylineIds)
    {
        var ids = polylineIds.ToArray();
        foreach (var id in ids) ArgumentException.ThrowIfNullOrWhiteSpace(id);
        await _operations.WaitAsync();
        try
        {
            var existing = ids.Where(_polylines.ContainsKey).Distinct(StringComparer.Ordinal).ToArray();
            if (existing.Length == 0) return;
            await InvokeAsync("removePolylinesByIdsAsync", JsInteropJson.Serialize(existing));
            foreach (var id in existing) _polylines.Remove(id);
        }
        finally { _operations.Release(); }
    }

    public async Task ClearPolylinesAsync()
    {
        await _operations.WaitAsync();
        try
        {
            if (_polylines.Count == 0) return;
            await InvokeAsync("clearPolylinesLayer");
            _polylines.Clear();
        }
        finally { _operations.Release(); }
    }

    public Task UpdatePolylineStyleAsync(string polylineId, PolylineOptions options) =>
        UpdatePolylinesStylesBulkAsync([new PolylineStyleUpdate { Id = polylineId, Options = options }]);

    public async Task UpdatePolylinesStylesBulkAsync(IEnumerable<PolylineStyleUpdate> updates)
    {
        var items = updates.ToArray();
        await _operations.WaitAsync();
        try
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                ArgumentNullException.ThrowIfNull(item);
                GetPolyline(item.Id);
                ValidateOptions(item.Options);
                if (!ids.Add(item.Id)) throw new ArgumentException("Duplicate polyline style update.");
            }
            if (items.Length == 0) return;
            await InvokeAsync("updatePolylinesStylesBulk", JsInteropJson.Serialize(items));
            foreach (var item in items)
            {
                var polyline = _polylines[item.Id];
                var current = polyline.Options;
                polyline.Options = new PolylineOptions
                {
                    Color = item.Options.Color ?? current?.Color,
                    Weight = item.Options.Weight ?? current?.Weight,
                    Opacity = item.Options.Opacity ?? current?.Opacity,
                    DashArray = item.Options.DashArray ?? current?.DashArray,
                    LineCap = item.Options.LineCap ?? current?.LineCap,
                    LineJoin = item.Options.LineJoin ?? current?.LineJoin
                };
            }
        }
        finally { _operations.Release(); }
    }

    public async Task UpdatePolylineLocationsAsync(string polylineId, IEnumerable<Location> locations)
    {
        var items = locations.ToArray();
        ValidateLocations(items);
        await _operations.WaitAsync();
        try
        {
            var polyline = GetPolyline(polylineId);
            await InvokeAsync("updatePolylineLocations", polylineId, JsInteropJson.Serialize(items));
            polyline.Locations = items;
        }
        finally { _operations.Release(); }
    }

    public Task FocusPolyline(string polylineId) => FocusPolyline(polylineId, null);

    /// <summary>
    /// Fits the whole line when options are omitted. With options, focuses the midpoint
    /// by length, with the final zoom at the starting zoom within the map's zoom limits.
    /// A flight may change zoom temporarily. Polyline highlighting is not supported.
    /// </summary>
    public async Task FocusPolyline(string polylineId, FocusOptions? options = null)
    {
        await _operations.WaitAsync();
        try
        {
            GetPolyline(polylineId);
            await InvokeAsync("focusPolyline", [polylineId], options is null ? null : JsInteropJson.Serialize(options));
        }
        finally { _operations.Release(); }
    }

    private Task InvokeAsync(string method, params object[] arguments) => InvokeAsync(method, arguments, null);

    private async Task InvokeAsync(string method, object[] arguments, string? focusOptionsJson)
    {
        var map = _map;
        if (map is null) return;
        var generation = _generation;
        var interopArguments = new object?[] { map.Id, Id }.Concat(arguments).Append((map as LeafletMap)?.CreationKey);
        if (focusOptionsJson is not null) interopArguments = interopArguments.Append(focusOptionsJson);
        await map.Interop.InvokeVoidAsync(method, interopArguments.ToArray());
        EnsureGeneration(generation);
    }

    private void EnsureGeneration(int generation)
    {
        if (_generation != generation) throw new OperationCanceledException("The group was detached during the operation.");
    }

    private Polyline GetPolyline(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        return _polylines.TryGetValue(id, out var item) ? item : throw new KeyNotFoundException($"Polyline '{id}' was not found.");
    }

    private void ValidateAdditions(Polyline[] items)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            ValidatePolyline(item);
            if (_polylines.ContainsKey(item.Id) || !ids.Add(item.Id)) throw new ArgumentException("Duplicate polyline ID.");
        }
    }

    private static void ValidatePolyline(Polyline item)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(item.Id);
        ValidateLocations(item.Locations);
        if (item.Options is not null) ValidateOptions(item.Options);
    }

    private static void ValidateLocations(IReadOnlyList<Location> locations)
    {
        ArgumentNullException.ThrowIfNull(locations);
        if (locations.Count < 2 || locations.Any(p => !double.IsFinite(p.Latitude) || !double.IsFinite(p.Longitude)))
            throw new ArgumentException("A polyline requires at least two finite coordinates.");
    }

    private static void ValidateOptions(PolylineOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Opacity is double opacity && !double.IsFinite(opacity)) throw new ArgumentException("Opacity must be finite.");
    }
}
