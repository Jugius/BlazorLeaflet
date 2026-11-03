using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Runtime.ExceptionServices;
using OohelpSoft.BlazorLeaflet.Base;
using OohelpSoft.BlazorLeaflet.Base.Interfaces;
using OohelpSoft.BlazorLeaflet.Base.Types;
using OohelpSoft.BlazorLeaflet.Layers.UI;
using OohelpSoft.BlazorLeaflet.Utiles;

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
    private DotNetObjectReference<LeafletMap>? _dotNetRef;
    private bool _disposed;

    public IJSObjectReference Interop => this.leafletInterop!;    
    public IEnumerable<MarkerGroupLayer> LayerGroups => _layers.Values;    
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

        await layerGroup.AddTo(this);
        _layers[layerGroup.Id] = layerGroup;        
    }
    public async Task FitBoundsToLayerGroupsAsync(params string[] layerGroupIds)
    {
        await EnsureMapReadyAsync();

        await leafletInterop!.InvokeVoidAsync(
            "fitBoundsToLayerGroups",
            this.Id,
            layerGroupIds
        );
    }

    public async Task RemoveMarkerGroupLayerAsync(string layerId)
    {
        await EnsureMapReadyAsync();
        await leafletInterop!.InvokeVoidAsync("removeMarkerGroupLayer", Id, layerId);
        if (_layers.Remove(layerId, out var layer)) layer.Detach();
    }

    public ValueTask DisposeAsync() => new(_disposeTask ??= DisposeCoreAsync());

    private static bool IsExpectedDisposal(Exception error) => error is JSDisconnectedException or ObjectDisposedException or OperationCanceledException;

    private async Task DisposeCoreAsync()
    {
        _disposed = true;
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
