using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet.Base;
using OohelpSoft.BlazorLeaflet.Layers;
using OohelpSoft.BlazorLeaflet.Layers.UI;
using OohelpSoft.BlazorLeaflet.Utiles;

namespace OohelpSoft.BlazorLeaflet;
public sealed partial class LeafletMap
{
    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;
    private IJSObjectReference? leafletInterop;
    private TaskCompletionSource<bool>? _mapReadyTcs;
    private readonly Dictionary<string, MarkerGroupLayer> _layers = new(StringComparer.OrdinalIgnoreCase);
    
    public bool IsMapInitialized => _mapReadyTcs?.Task.IsCompleted == true;
    public IEnumerable<MarkerGroupLayer> LayerGroups => _layers.Values;
    [Parameter] public EventCallback MapCreated { get; set; }


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
        if (!firstRender)
        {
            Console.WriteLine("LeafletMap - Not first render");
            return;
        }

        if (leafletInterop == null)
        {
            Console.WriteLine("Loading leafletInterop..");
            leafletInterop = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/OohelpSoft.Extensions.BlazorLeaflet/js/leafletInterop.js");

            try
            {
                DotNetObjectReference<LeafletMap> dotNetObjectRef = DotNetObjectReference.Create(this);
                Console.WriteLine($"Creating map {this.Id}");
                await leafletInterop.InvokeVoidAsync("createMap", this.Id, JsInteropJson.Serialize(Options), dotNetObjectRef);
            }
            catch (JSException ex)
            {
                Console.Error.WriteLine($"JSException: {ex.Message}");
            }
        }
    }
    public Task EnsureMapReadyAsync(CancellationToken ct = default)
    {
        if (_mapReadyTcs == null) _mapReadyTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
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

    public async Task AddMarkersAsync(IEnumerable<Marker> markers)
    {
        await leafletInterop!.InvokeVoidAsync("addMarkersAsync", this.Id, JsInteropJson.Serialize(markers));
    }
    public async Task<FeatureGroup> GetOrCreateFeatureGroupAsync(string layerId)
    {
        await EnsureMapReadyAsync();

        if(_layers.TryGetValue(layerId, out var existing) && existing is FeatureGroup layerGroup)
            return layerGroup;

        var layer = await FeatureGroup.Create(this.leafletInterop!, this.Id, layerId);
        _layers[layerId] = layer;
        return layer;
    }
    public async Task<MarkerClusterLayer> GetOrCreateClusterLayerAsync(string layerId)
    {
        await EnsureMapReadyAsync();

        if (_layers.TryGetValue(layerId, out var existing) && existing is MarkerClusterLayer clasterLayer)
            return clasterLayer;

        var options = new MarkerClusterLayerOptions {
            DisableClusteringAtZoom = 12,
            ShowCoverageOnHover = false,
        };

        clasterLayer = await MarkerClusterLayer.Create(this.leafletInterop!, this.Id, layerId, options);
        _layers[layerId] = clasterLayer;
        return clasterLayer;
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