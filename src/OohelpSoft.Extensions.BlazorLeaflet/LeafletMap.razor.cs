using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Base;
using OohelpSoft.BlazorLeaflet.Base.Interfaces;
using OohelpSoft.BlazorLeaflet.Layers.UI;
using OohelpSoft.BlazorLeaflet.Utiles;

namespace OohelpSoft.BlazorLeaflet;
public sealed partial class LeafletMap : IMap
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;
    private IJSObjectReference? leafletInterop;
    private TaskCompletionSource<bool>? _mapReadyTcs;
    private readonly Dictionary<string, MarkerGroupLayer> _layers = new(StringComparer.OrdinalIgnoreCase);
    
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

            DotNetObjectReference<LeafletMap> dotNetObjectRef = DotNetObjectReference.Create(this);
            await leafletInterop.InvokeVoidAsync("createMap", this.Id, JsInteropJson.Serialize(Options), dotNetObjectRef);
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
}