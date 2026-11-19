using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Base;
using OohelpSoft.BlazorLeaflet.Base.Interfaces;
using OohelpSoft.BlazorLeaflet.Base.Types;
using OohelpSoft.BlazorLeaflet.Layers;
using OohelpSoft.BlazorLeaflet.Layers.UI;
using OohelpSoft.BlazorLeaflet.Utiles;
using System.Runtime.ExceptionServices;

namespace OohelpSoft.BlazorLeaflet;
public sealed partial class LeafletMap : IMap, IAsyncDisposable
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;
    private IJSObjectReference? leafletInterop;
    private readonly TaskCompletionSource<bool> _mapReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string _creationKey = Guid.NewGuid().ToString("N");
    private Task<Exception?>? _mapCreatedTask;
    private Task? _disposeTask;
    private Exception? _initializationFailure;
    private bool _initializationStarted;
    private bool _mapCreatedNotified;
    private readonly Dictionary<string, MarkerGroupLayer> _layers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PolylineGroupLayer> _polylineLayers = new(StringComparer.OrdinalIgnoreCase);
    private DotNetObjectReference<LeafletMap>? _dotNetRef;
    private bool _disposed;
    private readonly SemaphoreSlim _layerOperations = new(1, 1);

    internal string CreationKey => _creationKey;
    public IJSObjectReference Interop => this.leafletInterop!;    
    public IEnumerable<MarkerGroupLayer> LayerGroups => _layers.Values;
    public IEnumerable<PolylineGroupLayer> PolylineLayerGroups => _polylineLayers.Values;
    public bool TryGetLayer(string layerId, out MarkerGroupLayer? layer) => _layers!.TryGetValue(layerId, out layer);


    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed || _initializationStarted) return;
        _initializationStarted = true;
        try
        {
            var module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/OohelpSoft.Extensions.BlazorLeaflet/js/leafletInterop.js");
            if (_disposed)
            {
                await module.DisposeAsync();
                return;
            }
            leafletInterop = module;
            _dotNetRef = DotNetObjectReference.Create(this);
            Exception? createFailure = null;
            try
            {
                var created = await module.InvokeAsync<bool>("createMap", Id, JsInteropJson.Serialize(Options), _dotNetRef, _creationKey);
                if (!created && !_disposed) throw new InvalidOperationException("Map creation was superseded.");
            }
            catch (Exception error)
            {
                if (!_disposed || !IsExpectedDisposal(error))
                {
                    createFailure = _initializationFailure = error;
                    _mapReadyTcs.TrySetException(error);
                }
            }

            var observerFailure = _mapCreatedTask is null ? null : await _mapCreatedTask;
            if (_disposed && observerFailure is not null && IsExpectedDisposal(observerFailure)) observerFailure = null;
            if (createFailure is not null && observerFailure is not null)
                throw new AggregateException(createFailure, observerFailure);
            if (createFailure is not null) ExceptionDispatchInfo.Capture(createFailure).Throw();
            if (observerFailure is not null) ExceptionDispatchInfo.Capture(observerFailure).Throw();
        }
        catch (Exception error) when (_disposed && IsExpectedDisposal(error)) { }
        catch (Exception error)
        {
            _initializationFailure = error;
            _mapReadyTcs.TrySetException(error);
            try { await DisposeAsync(); }
            catch (Exception cleanupError) { throw new AggregateException(error, cleanupError); }
            throw;
        }
    }
    public Task EnsureMapReadyAsync(CancellationToken ct = default)
    {
        if (_initializationFailure is not null) return Task.FromException(_initializationFailure);
        if (_disposed) return Task.FromCanceled(new CancellationToken(true));
        return ct == default ? _mapReadyTcs.Task : _mapReadyTcs.Task.WaitAsync(ct);
    }

    [JSInvokable]
    public void OnJsMapCreated()
    {
        if (_disposed || _initializationFailure is not null || leafletInterop is null || _mapCreatedNotified) return;
        _mapCreatedNotified = true;
        // Readiness means the JS map exists; handlers may themselves await EnsureMapReadyAsync.
        _mapReadyTcs.TrySetResult(true);
        _mapCreatedTask = InvokeMapCreatedAsync();
    }

    private async Task<Exception?> InvokeMapCreatedAsync()
    {
        try { await MapCreated.InvokeAsync(null); return null; }
        // Capture immediately, including on disposal/early failure; initialization awaits and reports this result.
        catch (Exception error) { return error; }
    }

    [JSInvokable]
    public Task OnMapPointClicked(double latitude, double longitude) => _disposed ? Task.CompletedTask : OnAddPointRequest.InvokeAsync(new Location(latitude, longitude));

    [JSInvokable]
    public Task OnJSMarkerClick(string id) => _disposed ? Task.CompletedTask : OnMarkerClick.InvokeAsync(id);

    [JSInvokable]
    public Task OnJSMarkerDraged(string id, double latitude, double longitude) =>
        _disposed ? Task.CompletedTask : OnMarkerDragged.InvokeAsync(new Events.MarkerDragEvent(id, new Location(latitude, longitude)));


    public async Task RegisterMarkerClickCallback()
    {
        await leafletInterop!.InvokeVoidAsync("registerMarkerClickCallback", this.Id);
    }
    public async Task RegisterMarkerDragEndCallback()
    {
        await leafletInterop!.InvokeVoidAsync("registerMarkerDragEndCallback", this.Id);
    }
    public async Task AddMarkersAsync(IEnumerable<Marker> markers)
    {
        await leafletInterop!.InvokeVoidAsync("addMarkersAsync", this.Id, JsInteropJson.Serialize(markers));
    }
    public async Task AddMarkerGroupLayerAsync(MarkerGroupLayer layerGroup)
    {
        await EnsureMapReadyAsync();
        await _layerOperations.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_polylineLayers.ContainsKey(layerGroup.Id)) throw new ArgumentException("A polyline group already uses this ID.");
            await layerGroup.AddTo(this);
            if (_disposed) { layerGroup.Detach(); throw new ObjectDisposedException(nameof(LeafletMap)); }
            _layers[layerGroup.Id] = layerGroup;
        }
        finally { _layerOperations.Release(); }
    }

    public async Task FitBoundsToLayerGroupsAsync(params string[] layerGroupIds)
    {
        await EnsureMapReadyAsync();
        string[] ids;
        await _layerOperations.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ids = layerGroupIds.Select(CanonicalLayerId).ToArray();
        }
        finally { _layerOperations.Release(); }
        await leafletInterop!.InvokeVoidAsync("fitBoundsToLayerGroups", Id, ids, _creationKey);
    }

    private string CanonicalLayerId(string id) =>
        _layers.TryGetValue(id, out var marker) ? marker.Id :
        _polylineLayers.TryGetValue(id, out var polyline) ? polyline.Id : id;

    public async Task RemoveMarkerGroupLayerAsync(string layerId)
    {
        await EnsureMapReadyAsync();
        await _layerOperations.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _layers.TryGetValue(layerId, out var layer);
            await leafletInterop!.InvokeVoidAsync("removeMarkerGroupLayer", Id, layer?.Id ?? layerId, _creationKey);
            if (_layers.Remove(layerId)) layer!.Detach();
        }
        finally { _layerOperations.Release(); }
    }

    public async Task SetMarkerGroupLayerVisibilityAsync(string layerId, bool isVisible)
    {
        await EnsureMapReadyAsync();
        string id;
        await _layerOperations.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            id = _layers.TryGetValue(layerId, out var layer) ? layer.Id : layerId;
        }
        finally { _layerOperations.Release(); }
        await leafletInterop!.InvokeVoidAsync("setMarkerGroupLayerVisibility", Id, id, isVisible, _creationKey);
    }

    public async Task AddPolylineGroupLayerAsync(PolylineGroupLayer layer)
    {
        ArgumentNullException.ThrowIfNull(layer);
        await EnsureMapReadyAsync();
        await _layerOperations.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_layers.ContainsKey(layer.Id) || _polylineLayers.ContainsKey(layer.Id))
                throw new ArgumentException("A group already uses this ID.");
            await layer.AddTo(this);
            if (_disposed) { layer.Detach(); throw new ObjectDisposedException(nameof(LeafletMap)); }
            _polylineLayers.Add(layer.Id, layer);
        }
        finally { _layerOperations.Release(); }
    }

    public async Task RemovePolylineGroupLayerAsync(string layerId)
    {
        await EnsureMapReadyAsync();
        await _layerOperations.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_polylineLayers.TryGetValue(layerId, out var layer)) return;
            await leafletInterop!.InvokeVoidAsync("removePolylineGroupLayer", Id, layer.Id, _creationKey);
            _polylineLayers.Remove(layerId);
            layer.Detach();
        }
        finally { _layerOperations.Release(); }
    }

    public async Task SetPolylineGroupLayerVisibilityAsync(string layerId, bool isVisible)
    {
        await EnsureMapReadyAsync();
        string id;
        await _layerOperations.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_polylineLayers.TryGetValue(layerId, out var layer)) return;
            id = layer.Id;
        }
        finally { _layerOperations.Release(); }
        await leafletInterop!.InvokeVoidAsync("setPolylineGroupLayerVisibility", Id, id, isVisible, _creationKey);
    }

    public bool TryGetPolylineLayer(string layerId, out PolylineGroupLayer? layer) => _polylineLayers.TryGetValue(layerId, out layer);

    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());

    private static bool IsExpectedDisposal(Exception error) => error is JSDisconnectedException or ObjectDisposedException or OperationCanceledException;

    private async Task DisposeCoreAsync()
    {
        _disposed = true;
        foreach (var layer in _polylineLayers.Values) layer.Detach();
        _polylineLayers.Clear();
        _mapReadyTcs.TrySetCanceled();
        Exception? failure = null;
        try
        {
            if (leafletInterop is not null)
            {
                try
                {
                    await leafletInterop.InvokeVoidAsync("destroyMap", Id, _creationKey);
                }
                catch (Exception error) when (IsExpectedDisposal(error)) { }
                catch (Exception error) { failure = error; }

                try
                {
                    await leafletInterop.DisposeAsync();
                }
                catch (Exception error) when (IsExpectedDisposal(error)) { }
                catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
            }
        }
        finally
        {
            _dotNetRef?.Dispose();
            _dotNetRef = null;
            _layers.Clear();
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
