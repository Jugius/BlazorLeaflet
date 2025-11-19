using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
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
    
    public bool IsMapInitialized => _mapReadyTcs?.Task.IsCompleted == true;
    public List<LayerGroup> LayerGroups { get; } = [];
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
    public async Task<LayerGroup> GetOrCreateLayerGroupAsync(string layerId)
    {
        await EnsureMapReadyAsync();

        var existing = LayerGroups.FirstOrDefault(g => g.Id == layerId);
        if (existing != null)
            return existing;

        var layer = await LayerGroup.Create(this.leafletInterop!, this.Id, layerId);
        LayerGroups.Add(layer);
        return layer;
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