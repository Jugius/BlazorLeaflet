using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Base;
using OohelpSoft.BlazorLeaflet.Base.Interfaces;
using OohelpSoft.BlazorLeaflet.Layers.UI;
using OohelpSoft.BlazorLeaflet.Utiles;

namespace OohelpSoft.BlazorLeaflet;
public sealed partial class LeafletMap : IMap, IAsyncDisposable
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;
    private IJSObjectReference? leafletInterop;
    private TaskCompletionSource<bool>? _mapReadyTcs;
    private readonly Dictionary<string, MarkerGroupLayer> _layers = new(StringComparer.OrdinalIgnoreCase);
    private DotNetObjectReference<LeafletMap>? _dotNetRef;
    private bool _disposed;

    public IJSObjectReference Interop => this.leafletInterop!;    
    public IEnumerable<MarkerGroupLayer> LayerGroups => _layers.Values;
    


    protected override void OnInitialized()
    {
        // инициализируем TCS
        _mapReadyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    // Метод, который вызывается после успешного создания карты (OnAfterRender или callback)
    private void NotifyMapCreated()
    {
        _mapReadyTcs?.TrySetResult(true);
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        if (leafletInterop == null)
        {
            leafletInterop = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/OohelpSoft.Extensions.BlazorLeaflet/js/leafletInterop.js");
            
            _dotNetRef = DotNetObjectReference.Create(this);
            await leafletInterop.InvokeVoidAsync("createMap", this.Id, JsInteropJson.Serialize(Options), _dotNetRef);
        }
    }
    public Task EnsureMapReadyAsync(CancellationToken ct = default)
    {
        _mapReadyTcs ??= new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (ct == default) return _mapReadyTcs.Task;
        // поддержка отмены
        return _mapReadyTcs.Task.WaitAsync(ct);
    }

    [JSInvokable]
    public void OnJsMapCreated()
    {
        NotifyMapCreated();
        _ = MapCreated.InvokeAsync(null);
    }


    [JSInvokable]
    public Task OnJSMarkerClick(string id) => OnMarkerClick.InvokeAsync(id);
    public async Task RegisterMarkerClickCallback()
    {
        await leafletInterop!.InvokeVoidAsync("registerMarkerClickCallback");
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

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            if (leafletInterop is not null)
            {
                try
                {
                    await leafletInterop.InvokeVoidAsync("destroyMap", this.Id);
                }
                catch (JSDisconnectedException)
                {
                    // normal during refresh/navigation
                }
                catch (ObjectDisposedException)
                {
                    // also safe to ignore
                }

                try
                {
                    await leafletInterop.DisposeAsync();
                }
                catch
                {
                    // ignore
                }
               
            }
        }
        finally
        {
            _dotNetRef?.Dispose();

            _layers.Clear();

            _mapReadyTcs = null;
        }
    }
}