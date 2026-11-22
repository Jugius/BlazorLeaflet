using System.Collections;
using System.Reflection;
using System.Text.Json;
using Microsoft.JSInterop;
using OohelpSoft.BlazorLeaflet;
using OohelpSoft.BlazorLeaflet.Base;
using OohelpSoft.BlazorLeaflet.Base.Interfaces;
using OohelpSoft.BlazorLeaflet.Base.Types;
using OohelpSoft.BlazorLeaflet.Layers;
using OohelpSoft.BlazorLeaflet.Layers.Vector;
using OohelpSoft.BlazorLeaflet.Layers.UI;
using OohelpSoft.BlazorLeaflet.Models;

var tests = new (string Name, Func<Task> Run)[]
{
    ("empty and prefilled groups", async () =>
    {
        Check(!new PolylineGroupLayer("empty").Polylines.Any());
        var group = new PolylineGroupLayer("group", [Line("x"), Line("X")]);
        Check(group.HasPolyline("x") && group.HasPolyline("X"));
        Check(group.Polylines.Count() == 2);
        await Throws<ArgumentException>(() => Task.FromResult(new PolylineGroupLayer("bad", [Line("x"), Line("x")])));
    }),
    ("unattached add remove and clear materialize inputs once", async () =>
    {
        var group = new PolylineGroupLayer("group", new Once<Polyline>([Line("a")]));
        await group.AddPolylinesAsync(new Once<Polyline>([Line("b")]));
        await group.RemovePolylinesAsync(new Once<string>(["a", "missing"]));
        Check(!group.HasPolyline("a") && group.HasPolyline("b"));
        await group.ClearPolylinesAsync();
        Check(!group.Polylines.Any());
    }),
    ("additions reject complete invalid batches", async () =>
    {
        var group = new PolylineGroupLayer("group");
        await Throws<ArgumentException>(() => group.AddPolylinesAsync([Line("a"), Line("a")]));
        await Throws<ArgumentException>(() => group.AddPolylinesAsync([Line("a"), new Polyline { Id = "bad", Locations = [new(1, 2)] }]));
        await Throws<ArgumentException>(() => group.AddPolylinesAsync([new Polyline { Id = "bad", Locations = [new(0, 0), new(double.NaN, 2)] }]));
        Check(!group.Polylines.Any());
    }),
    ("local partial styles preserve fields and empty dash resets", async () =>
    {
        var line = Line("a"); line.Options = new() { Color = "red", DashArray = "3 2", Weight = 2 };
        var group = new PolylineGroupLayer("group", [line]);
        await group.UpdatePolylineStyleAsync("a", new() { Opacity = 0.5 });
        Check(line.Options!.Color == "red" && line.Options.DashArray == "3 2" && line.Options.Opacity == 0.5);
        await group.UpdatePolylineStyleAsync("a", new() { DashArray = "" });
        Check(line.Options.DashArray == "" && line.Options.Weight == 2);
        await group.UpdatePolylineLocationsAsync("a", new Once<Location>([new(1, 2), new(3, 4)]));
        Check(line.Locations[0].Latitude == 1);
        await group.FocusPolyline("a");
    }),
    ("missing updates fail and validate the whole style packet", async () =>
    {
        var line = Line("a"); var group = new PolylineGroupLayer("group", [line]);
        await Throws<KeyNotFoundException>(() => group.UpdatePolylineStyleAsync("A", new()));
        await Throws<KeyNotFoundException>(() => group.UpdatePolylineLocationsAsync("missing", [new(0, 0), new(1, 1)]));
        await Throws<KeyNotFoundException>(() => group.FocusPolyline("missing"));
        await Throws<KeyNotFoundException>(() => group.UpdatePolylinesStylesBulkAsync([new() { Id = "a", Options = new() { Color = "blue" } }, new() { Id = "missing", Options = new() }]));
        Check(line.Options is null);
    }),
    ("marker focus preserves legacy calls and appends serialized options and identity keys", async () =>
    {
        var js = new FakeInterop();
        var map = ReadyMap(js);
        var markers = new TestMarkerGroup("markers");
        await markers.AddMarkersAsync([new Marker { Id = "Marker", Location = new(50, 30) }]);
        await map.AddMarkerGroupLayerAsync(markers);
        var attachmentKey = js.Calls[0].Args[2];
        Check(attachmentKey is string keyValue && keyValue.Length > 0);
        js.Calls.Clear();

        Func<string, Task> legacy = markers.FocusMarker;
        await legacy("Marker");
        await markers.FocusMarker("Marker", null);
        await markers.FocusMarker("marker", new());
        Check(js.Calls.Count == 3 && js.Calls.All(call => call.Method == "focusMarker"));
        Check(js.Calls[0].Args.SequenceEqual(new object?[] { map.Id, "markers", "Marker" }));
        Check(js.Calls[1].Args.SequenceEqual(js.Calls[0].Args));
        var explicitCall = js.Calls[2].Args;
        Check(explicitCall.Length == 6 && explicitCall.Take(3).SequenceEqual(js.Calls[0].Args));
        Check(explicitCall[4] is string key && key.Length > 0);
        Check(Equals(explicitCall[5], attachmentKey));
        using var options = JsonDocument.Parse((string)explicitCall[3]!);
        Check(options.RootElement.GetProperty("navigationMode").GetString() == "Popup");
        Check(options.RootElement.GetProperty("highlight").GetBoolean());
        Check(options.RootElement.GetProperty("animationDuration").GetDouble() == 0.5);
        await map.DisposeAsync();
    }),
    ("marker focus during replacement carries the old attachment key until C# publishes the new group", async () =>
    {
        var js = new FakeInterop();
        var map = ReadyMap(js);
        var old = new TestMarkerGroup("markers");
        await old.AddMarkersAsync([new Marker { Id = "marker", Location = new(50, 30) }]);
        await map.AddMarkerGroupLayerAsync(old);
        var oldKey = js.Calls[^1].Args[2];
        var replacement = new TestMarkerGroup("markers");
        await replacement.AddMarkersAsync([new Marker { Id = "marker", Location = new(51, 31) }]);

        js.BlockMethod = "addMarkerGroupLayer";
        js.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var adding = map.AddMarkerGroupLayerAsync(replacement);
        var newKey = js.Calls[^1].Args[2];
        Check(!adding.IsCompleted && !Equals(oldKey, newKey));
        Check(map.TryGetLayer("markers", out var registered) && ReferenceEquals(registered, old));
        await old.FocusMarker("marker", new());
        Check(js.Calls[^1].Method == "focusMarker" && Equals(js.Calls[^1].Args[5], oldKey));
        var count = js.Calls.Count;
        await replacement.FocusMarker("marker", new());
        Check(js.Calls.Count == count);

        js.Block.SetResult();
        await adding;
        await old.FocusMarker("marker", new());
        Check(js.Calls.Count == count);
        await replacement.FocusMarker("marker", new());
        Check(js.Calls[^1].Method == "focusMarker" && Equals(js.Calls[^1].Args[5], newKey));
        await map.DisposeAsync();
    }),
    ("reattaching a marker group publishes a fresh key only after add acknowledgement", async () =>
    {
        var js = new FakeInterop();
        var map = ReadyMap(js);
        var markers = new TestMarkerGroup("markers");
        await markers.AddMarkersAsync([new Marker { Id = "marker", Location = new(50, 30) }]);
        await map.AddMarkerGroupLayerAsync(markers);
        var oldKey = js.Calls[^1].Args[2];

        js.BlockMethod = "addMarkerGroupLayer";
        js.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var adding = map.AddMarkerGroupLayerAsync(markers);
        var newKey = js.Calls[^1].Args[2];
        Check(!adding.IsCompleted && !Equals(oldKey, newKey));
        await markers.FocusMarker("marker", new());
        Check(js.Calls[^1].Method == "focusMarker" && Equals(js.Calls[^1].Args[5], oldKey));

        js.Block.SetResult();
        await adding;
        await markers.FocusMarker("marker", new());
        Check(js.Calls[^1].Method == "focusMarker" && Equals(js.Calls[^1].Args[5], newKey));
        await map.DisposeAsync();
    }),
    ("a failed same-instance marker reattachment does not publish the unacknowledged key", async () =>
    {
        var js = new FakeInterop();
        var map = ReadyMap(js);
        var markers = new TestMarkerGroup("markers");
        await markers.AddMarkersAsync([new Marker { Id = "marker", Location = new(50, 30) }]);
        await map.AddMarkerGroupLayerAsync(markers);
        var acknowledgedKey = js.Calls[^1].Args[2];

        js.Fail = true;
        await Throws<JSException>(() => map.AddMarkerGroupLayerAsync(markers));
        Check(!Equals(js.Calls[^1].Args[2], acknowledgedKey));
        js.Fail = false;
        await markers.FocusMarker("marker", new());
        Check(js.Calls[^1].Method == "focusMarker" && Equals(js.Calls[^1].Args[5], acknowledgedKey));
        await map.DisposeAsync();
    }),
    ("explicit marker focus skips missing markers and replaced groups", async () =>
    {
        var js = new FakeInterop();
        var map = ReadyMap(js);
        var markers = new TestMarkerGroup("markers");
        await markers.AddMarkersAsync([new Marker { Id = "marker", Location = new(50, 30) }]);
        await map.AddMarkerGroupLayerAsync(markers);
        js.Calls.Clear();
        await markers.FocusMarker("missing", new());
        Check(js.Calls.Count == 0);

        await map.AddMarkerGroupLayerAsync(new TestMarkerGroup("MARKERS"));
        js.Calls.Clear();
        await markers.FocusMarker("marker", new());
        Check(js.Calls.Count == 0);
        await map.DisposeAsync();
    }),
    ("polyline focus preserves context key position when appending options and keeps legacy null behavior", async () =>
    {
        var js = new FakeInterop();
        var map = ReadyMap(js);
        var group = new PolylineGroupLayer("lines", [Line("line")]);
        await map.AddPolylineGroupLayerAsync(group);
        var key = js.Calls[0].Args[2];
        js.Calls.Clear();

        Func<string, Task> legacy = group.FocusPolyline;
        await legacy("line");
        await group.FocusPolyline("line", null);
        await group.FocusPolyline("line", new()
        {
            NavigationMode = FocusNavigationMode.FlyTo,
            Highlight = false,
            AnimationDuration = 0
        });
        Check(js.Calls.Count == 3 && js.Calls.All(call => call.Method == "focusPolyline"));
        Check(js.Calls[0].Args.SequenceEqual(new object?[] { map.Id, "lines", "line", key }));
        Check(js.Calls[1].Args.SequenceEqual(js.Calls[0].Args));
        var explicitCall = js.Calls[2].Args;
        Check(explicitCall.Length == 5 && explicitCall.Take(4).SequenceEqual(js.Calls[0].Args));
        using var options = JsonDocument.Parse((string)explicitCall[4]!);
        Check(options.RootElement.GetProperty("navigationMode").GetString() == "FlyTo");
        Check(!options.RootElement.GetProperty("highlight").GetBoolean());
        Check(options.RootElement.GetProperty("animationDuration").GetDouble() == 0);
        await Throws<KeyNotFoundException>(() => group.FocusPolyline("missing", new()));

        await map.RemovePolylineGroupLayerAsync("lines");
        js.Calls.Clear();
        await group.FocusPolyline("line", new());
        Check(js.Calls.Count == 0);
        await map.DisposeAsync();
    }),
    ("empty batches avoid interop", async () =>
    {
        var js = new FakeInterop(); var group = new PolylineGroupLayer("group"); await Attach(group, new FakeMap(js)); js.Calls.Clear();
        await group.AddPolylinesAsync([]); await group.RemovePolylinesAsync([]); await group.ClearPolylinesAsync(); await group.UpdatePolylinesStylesBulkAsync([]);
        Check(js.Calls.Count == 0);
    }),
    ("bulk styles use one call and existing JSON coordinate converter", async () =>
    {
        var js = new FakeInterop(); var group = new PolylineGroupLayer("group", [Line("a"), Line("b")]);
        await Attach(group, new FakeMap(js));
        using (var json = JsonDocument.Parse((string)js.Calls[0].Args[1]!))
            Check(json.RootElement.GetProperty("polylines")[0].GetProperty("locations")[0][0].GetDouble() == 50);
        js.Calls.Clear();
        await group.UpdatePolylinesStylesBulkAsync(new Once<PolylineStyleUpdate>([new() { Id = "a", Options = new() { DashArray = "" } }, new() { Id = "b", Options = new() { Weight = 5 } }]));
        Check(js.Calls.Count == 1 && js.Calls[0].Method == "updatePolylinesStylesBulk");
        using var update = JsonDocument.Parse((string)js.Calls[0].Args[2]!);
        Check(update.RootElement[0].GetProperty("options").GetProperty("dashArray").GetString() == "");
        Check(!update.RootElement[0].GetProperty("options").TryGetProperty("color", out _));
    }),
    ("interop failures preserve local add remove clear styles geometry", async () =>
    {
        var js = new FakeInterop(); var line = Line("a"); var group = new PolylineGroupLayer("group", [line]); await Attach(group, new FakeMap(js));
        js.Fail = true;
        await Throws<JSException>(() => group.AddPolylinesAsync([Line("b")]));
        await Throws<JSException>(() => group.RemovePolylinesAsync(["a"]));
        await Throws<JSException>(() => group.ClearPolylinesAsync());
        await Throws<JSException>(() => group.UpdatePolylineStyleAsync("a", new() { Color = "red" }));
        await Throws<JSException>(() => group.UpdatePolylineLocationsAsync("a", [new(1, 2), new(3, 4)]));
        Check(group.HasPolyline("a") && !group.HasPolyline("b") && line.Options is null && line.Locations[0].Latitude == 50);
    }),
    ("failed attachment remains unattached and retryable", async () =>
    {
        var js = new FakeInterop { Fail = true }; var group = new PolylineGroupLayer("group");
        await Throws<JSException>(() => Attach(group, new FakeMap(js)));
        await group.AddPolylinesAsync([Line("a")]); js.Fail = false;
        await Attach(group, new FakeMap(js)); Check(group.HasPolyline("a"));
    }),
    ("parallel group operations are serialized", async () =>
    {
        var js = new FakeInterop(); var group = new PolylineGroupLayer("group"); await Attach(group, new FakeMap(js)); js.Calls.Clear();
        js.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var adding = group.AddPolylinesAsync([Line("a")]);
        var styling = group.UpdatePolylineStyleAsync("a", new() { Color = "red" });
        Check(js.Calls.Count == 1 && !styling.IsCompleted);
        js.Block.SetResult(); await Task.WhenAll(adding, styling);
        Check(group.Polylines.Single().Options!.Color == "red");
    }),
    ("duplicate concurrent additions reject the second operation", async () =>
    {
        var js = new FakeInterop(); var group = new PolylineGroupLayer("group"); await Attach(group, new FakeMap(js));
        js.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = group.AddPolylinesAsync([Line("a")]); var second = group.AddPolylinesAsync([Line("a")]);
        js.Block.SetResult(); await first; await Throws<ArgumentException>(() => second);
        Check(group.Polylines.Count() == 1);
    }),
    ("detach preserves content and cancels stale completion", async () =>
    {
        var js = new FakeInterop(); var group = new PolylineGroupLayer("group", [Line("a")]); await Attach(group, new FakeMap(js));
        js.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = group.UpdatePolylineStyleAsync("a", new() { Color = "red" }); Detach(group);
        js.Block.SetResult(); await Throws<OperationCanceledException>(() => pending);
        Check(group.Polylines.Single().Options is null);
        js.Block = null; await Attach(group, new FakeMap(js)); Check(group.HasPolyline("a"));
    }),
    ("one group cannot attach to two maps", async () =>
    {
        var group = new PolylineGroupLayer("group"); var firstJs = new FakeInterop(); var secondJs = new FakeInterop();
        firstJs.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Attach(group, new FakeMap(firstJs)); var second = Attach(group, new FakeMap(secondJs));
        firstJs.Block.SetResult(); await first; await Throws<InvalidOperationException>(() => second); Check(secondJs.Calls.Count == 0);
    }),
    ("map canonicalizes group IDs and mixed bounds", async () =>
    {
        var js = new FakeInterop(); var map = ReadyMap(js); var group = new PolylineGroupLayer("Lines", [Line("a")]);
        await map.AddPolylineGroupLayerAsync(group); Check(map.TryGetPolylineLayer("LINES", out var actual) && ReferenceEquals(actual, group));
        await map.SetPolylineGroupLayerVisibilityAsync("lines", false); Check((string)js.Calls[^1].Args[1]! == "Lines");
        await map.AddMarkerGroupLayerAsync(new TestMarkerGroup("Markers"));
        await map.FitBoundsToLayerGroupsAsync("lines", "MARKERS");
        Check(((string[])js.Calls[^1].Args[1]!).SequenceEqual(["Lines", "Markers"]));
        await map.RemovePolylineGroupLayerAsync("lines"); Check(!map.PolylineLayerGroups.Any() && group.HasPolyline("a"));
        await map.AddPolylineGroupLayerAsync(group); await map.DisposeAsync();
    }),
    ("map rejects cross-type collisions in both directions", async () =>
    {
        var js = new FakeInterop(); var map = ReadyMap(js);
        await map.AddPolylineGroupLayerAsync(new("Lines"));
        await Throws<ArgumentException>(() => map.AddMarkerGroupLayerAsync(new TestMarkerGroup("lines")));
        await map.AddMarkerGroupLayerAsync(new TestMarkerGroup("Markers"));
        await Throws<ArgumentException>(() => map.AddPolylineGroupLayerAsync(new("MARKERS")));
        await Throws<ArgumentException>(() => map.AddPolylineGroupLayerAsync(new("LINES")));
        await map.DisposeAsync();
    }),
    ("marker same-type replacement retains existing API behavior", async () =>
    {
        var js = new FakeInterop(); var map = ReadyMap(js); var first = new TestMarkerGroup("Group"); var second = new TestMarkerGroup("GROUP");
        await first.AddMarkersAsync([new Marker { Id = "kept" }]);
        await map.AddMarkerGroupLayerAsync(first); await map.AddMarkerGroupLayerAsync(second);
        Check(map.LayerGroups.Count() == 1 && map.TryGetLayer("group", out var actual) && ReferenceEquals(actual, second));
        Check(!js.Calls.Any(c => c.Method == "removeMarkerGroupLayer") && first.HasMarker("kept"));
        await map.DisposeAsync();
    }),
    ("failed marker replacement preserves the existing registry and model", async () =>
    {
        var js = new FakeInterop(); var map = ReadyMap(js); var first = new TestMarkerGroup("Group");
        await first.AddMarkersAsync([new Marker { Id = "kept" }]);
        await map.AddMarkerGroupLayerAsync(first); var count = js.Calls.Count;
        await Throws<InvalidOperationException>(() => map.AddMarkerGroupLayerAsync(new BrokenMarkerGroup("GROUP")));
        Check(js.Calls.Count == count && map.TryGetLayer("group", out var actual) && ReferenceEquals(actual, first));
        js.Fail = true;
        await Throws<JSException>(() => map.AddMarkerGroupLayerAsync(new TestMarkerGroup("GROUP")));
        Check(map.TryGetLayer("group", out actual) && ReferenceEquals(actual, first) && first.HasMarker("kept"));
        Check(!js.Calls.Any(c => c.Method == "removeMarkerGroupLayer"));
        js.Fail = false; await map.DisposeAsync();
    }),
    ("marker methods forward JS-only IDs when the C# registry has no entry", async () =>
    {
        var js = new FakeInterop(); var map = ReadyMap(js);
        await map.SetMarkerGroupLayerVisibilityAsync("JS-only", false);
        Check(js.Calls[^1].Method == "setMarkerGroupLayerVisibility" && (string)js.Calls[^1].Args[1]! == "JS-only");
        await map.RemoveMarkerGroupLayerAsync("JS-only");
        Check(js.Calls[^1].Method == "removeMarkerGroupLayer" && (string)js.Calls[^1].Args[1]! == "JS-only");
        await map.DisposeAsync();
    }),
    ("visibility and bounds interop do not hold the mutation semaphore", async () =>
    {
        var operations = new (string Method, Func<LeafletMap, Task> Start)[]
        {
            ("setMarkerGroupLayerVisibility", map => map.SetMarkerGroupLayerVisibilityAsync("MARKERS", false)),
            ("setPolylineGroupLayerVisibility", map => map.SetPolylineGroupLayerVisibilityAsync("LINES", false)),
            ("fitBoundsToLayerGroups", map => map.FitBoundsToLayerGroupsAsync("LINES", "MARKERS"))
        };
        foreach (var operation in operations)
        {
            var js = new FakeInterop(); var map = ReadyMap(js);
            await map.AddMarkerGroupLayerAsync(new TestMarkerGroup("Markers"));
            await map.AddPolylineGroupLayerAsync(new("Lines"));
            js.BlockMethod = operation.Method; js.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending = operation.Start(map);
            var removing = map.RemovePolylineGroupLayerAsync("lines");
            var completedWithoutWaiting = removing.IsCompletedSuccessfully;
            js.Block.SetResult(); await Task.WhenAll(pending, removing);
            Check(completedWithoutWaiting && !map.PolylineLayerGroups.Any());
            await map.DisposeAsync();
        }
    }),
    ("add and remove mutations share one semaphore", async () =>
    {
        var js = new FakeInterop(); var map = ReadyMap(js);
        js.BlockMethod = "addPolylineGroupLayer"; js.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var adding = map.AddPolylineGroupLayerAsync(new("Lines"));
        var removing = map.RemovePolylineGroupLayerAsync("LINES");
        var waited = !removing.IsCompleted;
        js.Block.SetResult(); await Task.WhenAll(adding, removing);
        Check(waited && !map.PolylineLayerGroups.Any()); await map.DisposeAsync();
    }),
    ("dispose during pending attachment does not retain a stale map", async () =>
    {
        var js = new FakeInterop(); var map = ReadyMap(js); var group = new PolylineGroupLayer("Lines", [Line("a")]);
        js.BlockMethod = "addPolylineGroupLayer"; js.Block = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var adding = map.AddPolylineGroupLayerAsync(group); await map.DisposeAsync(); js.Block.SetResult();
        await Throws<ObjectDisposedException>(() => adding); Check(!map.PolylineLayerGroups.Any());
        var calls = js.Calls.Count; await group.AddPolylinesAsync([Line("b")]); Check(js.Calls.Count == calls);
    }),
    ("dispose detaches registered groups and preserves content", async () =>
    {
        var js = new FakeInterop(); var map = ReadyMap(js); var group = new PolylineGroupLayer("Lines", [Line("a")]);
        await map.AddPolylineGroupLayerAsync(group); await map.DisposeAsync(); var calls = js.Calls.Count;
        await group.UpdatePolylineStyleAsync("a", new() { Color = "red" }); Check(js.Calls.Count == calls && group.HasPolyline("a"));
        await Throws<OperationCanceledException>(() => map.AddPolylineGroupLayerAsync(new("other")));
    })
};
var failures = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception error) { failures++; Console.Error.WriteLine($"FAIL {test.Name}\n{error}"); }
}
Console.WriteLine($"{tests.Length - failures} passed, {failures} failed");
return failures == 0 ? 0 : 1;

static Polyline Line(string id) => new() { Id = id, Locations = [new(50, 30), new(51, 31)] };
static void Check(bool condition) { if (!condition) throw new InvalidOperationException("Assertion failed."); }
static async Task Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); } catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
static Task Attach(PolylineGroupLayer group, IMap map) => (Task)typeof(PolylineGroupLayer).GetMethod("AddTo", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(group, [map])!;
static void Detach(PolylineGroupLayer group) => typeof(PolylineGroupLayer).GetMethod("Detach", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(group, null);
static LeafletMap ReadyMap(FakeInterop js)
{
    var map = new LeafletMap();
    typeof(LeafletMap).GetField("leafletInterop", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(map, js);
    var ready = (TaskCompletionSource<bool>)typeof(LeafletMap).GetField("_mapReadyTcs", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(map)!;
    ready.SetResult(true);
    return map;
}
sealed class TestMarkerGroup(string id) : MarkerGroupLayer(id) { public override string LayerType => "FeatureGroup"; }
sealed class BrokenMarkerGroup(string id) : MarkerGroupLayer(id) { public override string LayerType => throw new InvalidOperationException("Injected serialization failure."); }
sealed class FakeMap(FakeInterop interop) : IMap { public string Id => "map"; public IJSObjectReference Interop => interop; }
sealed class FakeInterop : IJSObjectReference
{
    public List<(string Method, object?[] Args)> Calls { get; } = [];
    public bool Fail { get; set; }
    public TaskCompletionSource? Block { get; set; }
    public string? BlockMethod { get; set; }
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, default, args);
    public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        Calls.Add((identifier, args ?? []));
        if (Fail) throw new JSException("Injected failure.");
        if (Block is not null && (BlockMethod is null || BlockMethod == identifier)) await Block.Task;
        return default!;
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
sealed class Once<T>(IEnumerable<T> items) : IEnumerable<T>
{
    private bool _enumerated;
    public IEnumerator<T> GetEnumerator()
    {
        if (_enumerated) throw new InvalidOperationException("Enumerated twice.");
        _enumerated = true;
        return items.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
