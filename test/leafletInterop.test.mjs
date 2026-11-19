// Run: node --experimental-vm-modules --test BlazorLeaflet/test/leafletInterop.test.mjs
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createContext, SourceTextModule } from 'node:vm';

const source = await readFile(new URL('../src/OohelpSoft.Extensions.BlazorLeaflet/wwwroot/js/leafletInterop.js', import.meta.url), 'utf8');

function eventSource(properties = {}) {
    return {
        ...properties,
        handlers: new Map(),
        on(name, handler) {
            const handlers = this.handlers.get(name) ?? [];
            handlers.push(handler);
            this.handlers.set(name, handlers);
            return this;
        },
        once(name, handler) {
            const once = event => {
                this.handlers.set(name, this.handlers.get(name).filter(item => item !== once));
                handler(event);
            };
            return this.on(name, once);
        },
        off(name) { if (name === undefined) this.handlers.clear(); else this.handlers.delete(name); },
        fire(name, event = {}) {
            for (const handler of [...(this.handlers.get(name) ?? [])]) handler(event);
        }
    };
}

async function createHarness({ deferredLoader = false, tileFailure = false, controlFailure = null } = {}) {
    const markers = [];
    const maps = [];
    const loader = [];
    const L = {
        map(id) {
            const map = eventSource({
                id, markers: [], controls: [], container: { style: {} }, removeCount: 0,
                getContainer() { return this.container; },
                remove() { this.removeCount++; },
                layers: new Set(),
                hasLayer(layer) { return this.layers.has(layer); },
                removeLayer(layer) { this.layers.delete(layer); this.removedLayer = layer; }
            });
            maps.push(map);
            return map;
        },
        tileLayer() { return { addTo() { if (tileFailure) throw new Error('tile failed'); } }; },
        marker(location) {
            const marker = eventSource({
                getLatLng() { return { lat: location[0], lng: location[1] }; },
                addTo(map) { map.markers.push(this); return this; },
                bindPopup(popup) { this.popup = popup; return this; },
                getPopup() { return this.popup; },
                unbindPopup() { this.popup = null; },
                closePopup() {},
                setIcon(icon) { this.icon = icon; }
            });
            markers.push(marker);
            return marker;
        },
        layerGroup() {
            return {
                markers: [],
                addLayer(marker) { this.markers.push(marker); },
                clearLayers() { this.markers.length = 0; },
                off() { this.detached = true; },
                addTo(map) { map.layers.add(this); }
            };
        },
        markerClusterGroup() { return this.layerGroup(); },
        control: {
            layers() { return { removed: [], addTo() { if (controlFailure === 'layers') throw new Error('control failed'); }, addOverlay() {}, removeLayer(layer) { this.removed.push(layer); } }; },
            scale() { return { addTo() { if (controlFailure === 'scale') throw new Error('control failed'); } }; }
        },
        popup() { return eventSource({ setContent() { return this; }, getElement() { return { querySelectorAll() { return []; } }; }, update() { this.updates = (this.updates ?? 0) + 1; } }); },
        icon(options) { return options; },
        Control: {
            extend(definition) {
                return class {
                    options = definition.options;
                    addTo(map) {
                        this.container = definition.onAdd.call(this, map);
                        map.controls.push(this);
                        if (controlFailure === 'add') throw new Error('control failed');
                    }
                };
            }
        },
        DomUtil: {
            create(tag, className, parent) {
                const element = eventSource({ tag, className, style: {}, children: [] });
                parent?.children.push(element);
                return element;
            }
        },
        DomEvent: {
            disableClickPropagation() {}, disableScrollPropagation() {}, preventDefault() {},
            on(element, name, handler) { element.on(name, handler); }
        }
    };
    const window = deferredLoader ? {} : { L };
    const module = new SourceTextModule(source, { context: createContext({ window, L, console,
        document: { querySelector: () => ({}) },
        setTimeout: deferredLoader ? callback => loader.push(callback) : setTimeout }) });
    await module.link(() => { throw new Error('Unexpected module dependency'); });
    await module.evaluate();
    const api = module.namespace;

    async function createMap(id, options = {}) {
        const reference = {
            disposed: false, calls: [],
            invokeMethodAsync(method, ...args) {
                assert.equal(this.disposed, false, 'A disposed .NET reference was invoked');
                this.calls.push([method, ...args]);
                return Promise.resolve();
            }
        };
        await api.createMap(id, JSON.stringify(options), reference);
        assert.deepEqual(reference.calls, [['OnJsMapCreated']]);
        reference.calls.length = 0;
        return reference;
    }

    async function addMarker(mapId, path) {
        const marker = { id: 'same-marker', location: [50, 30], draggable: true };
        if (path === 'direct') {
            await api.addMarkersAsync(mapId, JSON.stringify([marker]));
        } else {
            await api.addMarkerGroupLayer(mapId, JSON.stringify({
                id: 'same-layer', markers: path === 'initial-group' ? [marker] : []
            }));
            if (path === 'added-to-group') {
                await api.addMarkersToLayerAsync(mapId, 'same-layer', JSON.stringify([marker]));
            }
        }
        return markers.at(-1);
    }

    return { api, window, maps, createMap, addMarker,
        finishLoader() { window.L = L; for (const callback of loader.splice(0)) callback(); } };
}

function fireMarkerEvents(marker) {
    marker.fire('click');
    marker.fire('dragend');
}

const markerCalls = [['OnJSMarkerClick', 'same-marker'], ['OnJSMarkerDraged', 'same-marker', 50, 30]];

for (const path of ['initial-group', 'added-to-group', 'direct']) {
    test(`${path}: callbacks stay isolated through creation, destruction and mapId reuse`, async () => {
        const { api, window, createMap, addMarker } = await createHarness();
        const first = await createMap('first');
        api.registerMarkerClickCallback('first');
        api.registerMarkerDragEndCallback('first');
        const firstMarker = await addMarker('first', path);
        fireMarkerEvents(firstMarker);
        assert.deepEqual(first.calls, markerCalls);
        first.calls.length = 0;

        const second = await createMap('second', { layersControl: {} });
        api.registerMarkerClickCallback('second');
        api.registerMarkerDragEndCallback('second');
        const secondMarker = await addMarker('second', path);
        fireMarkerEvents(firstMarker);
        assert.deepEqual(first.calls, markerCalls);
        assert.deepEqual(second.calls, []);
        fireMarkerEvents(secondMarker);
        assert.deepEqual(second.calls, markerCalls);

        const secondMap = window._leafletMaps.second;
        api.destroyMap('second');
        second.disposed = true;
        api.destroyMap('second');
        assert.equal(secondMap.removeCount, 1);
        for (const storage of ['_leafletMaps', '_leafletLayers', '_leafletMarkers', '_leafletLayersControl']) {
            assert.equal(Object.hasOwn(window[storage], 'second'), false);
        }
        fireMarkerEvents(secondMarker);
        first.calls.length = 0;
        fireMarkerEvents(firstMarker);
        assert.deepEqual(first.calls, markerCalls);

        const replacement = await createMap('second');
        const replacementMarker = await addMarker('second', path);
        fireMarkerEvents(replacementMarker);
        assert.deepEqual(replacement.calls, [], 'Registration must reset for the replacement map');
        api.registerMarkerClickCallback('second');
        api.registerMarkerDragEndCallback('second');
        fireMarkerEvents(secondMarker);
        assert.deepEqual(replacement.calls, [], 'Old handlers must not reach the replacement map');
        fireMarkerEvents(replacementMarker);
        assert.deepEqual(replacement.calls, markerCalls);
        assert.deepEqual(second.calls, markerCalls);
        api.destroyMap('second');
        first.calls.length = 0;
        fireMarkerEvents(firstMarker);
        assert.deepEqual(first.calls, markerCalls);
        api.destroyMap('first');
    });

    test(`${path}: click and dragend registrations are independent and idempotent`, async () => {
        const { api, createMap, addMarker } = await createHarness();
        const clickOnly = await createMap('click');
        const clickMarker = await addMarker('click', path);
        const dragOnly = await createMap('drag');
        const dragMarker = await addMarker('drag', path);
        const unregistered = await createMap('none');
        const unregisteredMarker = await addMarker('none', path);
        api.registerMarkerClickCallback('click');
        api.registerMarkerClickCallback('click');
        api.registerMarkerDragEndCallback('drag');
        api.registerMarkerDragEndCallback('drag');
        api.registerMarkerClickCallback('missing');
        api.registerMarkerDragEndCallback('missing');
        fireMarkerEvents(clickMarker);
        fireMarkerEvents(dragMarker);
        fireMarkerEvents(unregisteredMarker);
        assert.deepEqual(clickOnly.calls, [markerCalls[0]]);
        assert.deepEqual(dragOnly.calls, [markerCalls[1]]);
        assert.deepEqual(unregistered.calls, []);
    });
}

test('add-marker controls keep their own reference and ignore stale callbacks', async () => {
    const { api, window, createMap } = await createHarness();
    const first = await createMap('first', { addMarkerControl: {} });
    const second = await createMap('second', { addMarkerControl: {} });
    const firstMap = window._leafletMaps.first;
    const secondMap = window._leafletMaps.second;
    const firstButton = firstMap.controls[0].container.children[0];
    const secondButton = secondMap.controls[0].container.children[0];
    firstButton.fire('click');
    assert.equal(firstMap.container.style.cursor, 'crosshair');
    firstMap.fire('click', { latlng: { lat: 50, lng: 30 } });
    assert.equal(firstMap.container.style.cursor, '');
    assert.deepEqual(first.calls, [['OnMapPointClicked', 50, 30]]);
    assert.deepEqual(second.calls, []);
    firstButton.fire('click');
    firstButton.fire('click');
    firstMap.fire('click', { latlng: { lat: 51, lng: 31 } });
    assert.equal(first.calls.length, 1, 'Cancelling add mode must preserve its existing behavior');

    secondButton.fire('click');
    secondMap.fire('click', { latlng: { lat: 52, lng: 32 } });
    assert.deepEqual(second.calls, [['OnMapPointClicked', 52, 32]]);
    secondButton.fire('click');
    const staleCallback = secondMap.handlers.get('click')[0];
    api.destroyMap('second');
    second.disposed = true;
    const replacement = await createMap('second', { addMarkerControl: {} });
    staleCallback({ latlng: { lat: 53, lng: 33 } });
    assert.deepEqual(replacement.calls, []);
    firstButton.fire('click');
    firstMap.fire('click', { latlng: { lat: 54, lng: 34 } });
    assert.deepEqual(first.calls.at(-1), ['OnMapPointClicked', 54, 34]);
});

test('destroy deactivates callbacks before remove and clears storage if remove throws', async () => {
    const { api, window, createMap, addMarker } = await createHarness();
    const reference = await createMap('map', { layersControl: {} });
    api.registerMarkerClickCallback('map');
    api.registerMarkerDragEndCallback('map');
    const marker = await addMarker('map', 'initial-group');
    window._leafletMaps.map.remove = () => {
        fireMarkerEvents(marker);
        throw new Error('remove failed');
    };
    assert.throws(() => api.destroyMap('map'), /remove failed/);
    reference.disposed = true;
    fireMarkerEvents(marker);
    assert.deepEqual(reference.calls, []);
    for (const storage of ['_leafletMaps', '_leafletLayers', '_leafletMarkers', '_leafletLayersControl']) {
        assert.equal(Object.hasOwn(window[storage], 'map'), false);
    }
    assert.doesNotThrow(() => api.destroyMap('map'));
});

function reference(callback = () => Promise.resolve()) {
    return { calls: 0, invokeMethodAsync() { this.calls++; return callback(); } };
}
function deferred() {
    let resolve, reject;
    const promise = new Promise((ok, fail) => { resolve = ok; reject = fail; });
    return { promise, resolve, reject };
}
function assertCleared(window, id) {
    for (const name of ['_leafletMaps', '_leafletLayers', '_leafletMarkers', '_leafletLayersControl'])
        assert.equal(Object.hasOwn(window[name] ?? {}, id), false, name + ' retains failed/disposed map');
}

test('destroy cancels create even before an already-loaded loader continuation', async () => {
    const { api, maps, window } = await createHarness();
    const ref = reference();
    const creating = api.createMap('map', '{}', ref);
    api.destroyMap('map');
    await creating;
    assert.equal(maps.length, 0, 'L.map must not run after destroy');
    assert.equal(ref.calls, 0);
    assertCleared(window, 'map');
});

test('deferred loader creation is cancelled independently for each map', async () => {
    const { api, maps, window, finishLoader } = await createHarness({ deferredLoader: true });
    const old = reference(), live = reference();
    const first = api.createMap('old', '{}', old);
    const second = api.createMap('live', '{}', live);
    api.destroyMap('old'); api.destroyMap('old');
    finishLoader();
    await Promise.all([first, second]);
    assert.equal(old.calls, 0); assert.equal(live.calls, 1);
    assert.deepEqual(maps.map(map => map.id), ['live']);
    assertCleared(window, 'old');
    api.destroyMap('live');
    assert.equal(maps[0].removeCount, 1);
});

test('same-id pending replacement and old owner destroy preserve the newest generation', async () => {
    const { api, maps, window, finishLoader } = await createHarness({ deferredLoader: true });
    const old = reference(), latest = reference();
    const first = api.createMap('map', '{}', old, 'old-owner');
    const second = api.createMap('map', '{}', latest, 'new-owner');
    finishLoader(); await Promise.all([first, second]);
    assert.equal(old.calls, 0); assert.equal(latest.calls, 1); assert.equal(maps.length, 1);
    api.destroyMap('map', 'old-owner');
    assert.equal(window._leafletMaps.map, maps[0]); assert.equal(maps[0].removeCount, 0);
    api.destroyMap('map', 'new-owner');
    assert.equal(maps[0].removeCount, 1); assertCleared(window, 'map');
});

test('tile failure rolls back L.map before destroy storage is otherwise available', async () => {
    const { api, maps, window } = await createHarness({ tileFailure: true });
    const ref = reference();
    await assert.rejects(api.createMap('map', '{}', ref), /tile failed/);
    assert.equal(maps.length, 1); assert.equal(maps[0].removeCount, 1); assert.equal(ref.calls, 0);
    assertCleared(window, 'map');
    api.destroyMap('map'); assert.equal(maps[0].removeCount, 1);
});

for (const [controlFailure, options] of [['layers', { layersControl: {} }], ['scale', { scaleControl: {} }], ['add', { addMarkerControl: {} }]])
    test(`${controlFailure} control failure rolls back map and callbacks`, async () => {
        const { api, maps, window } = await createHarness({ controlFailure });
        const ref = reference();
        await assert.rejects(api.createMap('map', JSON.stringify(options), ref), /control failed/);
        assert.equal(maps[0].removeCount, 1); assert.equal(ref.calls, 0); assertCleared(window, 'map');
        api.destroyMap('map'); assert.equal(maps[0].removeCount, 1);
    });

test('creation observes callback completion and rolls back a rejected callback', async () => {
    const { api, maps, window } = await createHarness();
    const entered = deferred(), completion = deferred();
    completion.promise.catch(() => {}); // The regression must not itself introduce an unhandled rejection.
    const ref = reference(() => { entered.resolve(); return completion.promise; });
    let settled = false;
    const creating = api.createMap('map', '{}', ref).finally(() => { settled = true; });
    await entered.promise;
    assert.equal(settled, false, 'create must await the callback promise');
    completion.reject(new Error('observer failed'));
    await assert.rejects(creating, /observer failed/);
    assert.equal(maps[0].removeCount, 1); assertCleared(window, 'map');
});

test('late old callback failure cannot delete replacement storage or remove replacement map', async () => {
    const { api, maps, window } = await createHarness();
    const entered = deferred(), completion = deferred();
    completion.promise.catch(() => {});
    const creating = api.createMap('map', '{}', reference(() => { entered.resolve(); return completion.promise; }), 'old');
    await entered.promise;
    api.destroyMap('map', 'old');
    await api.createMap('map', '{}', reference(), 'new');
    completion.reject(new Error('late observer failed'));
    await assert.rejects(creating, /late observer failed/);
    assert.equal(window._leafletMaps.map, maps[1]);
    assert.equal(maps[0].removeCount, 1); assert.equal(maps[1].removeCount, 0);
    api.destroyMap('map', 'new');
});

test('remove group cleans registries, controls, listeners and children; same-ID readd is independent', async () => {
    const { api, window, createMap, addMarker } = await createHarness();
    const first = await createMap('first', { layersControl: {} });
    const other = await createMap('other');
    api.registerMarkerClickCallback('first');
    api.registerMarkerDragEndCallback('first');
    const old = await addMarker('first', 'initial-group');
    await addMarker('other', 'initial-group');
    const group = window._leafletLayers.first['same-layer'];
    api.removeMarkerGroupLayer('first', 'same-layer');
    assert.equal(group.markers.length, 0);
    assert.equal(group.detached, true);
    assert.equal(window._leafletMaps.first.removedLayer, group);
    assert.equal(window._leafletLayersControl.first.removed[0], group);
    assert.equal(window._leafletLayers.first['same-layer'], undefined);
    assert.equal(window._leafletMarkers.first['same-layer'], undefined);
    api.removeMarkerGroupLayer('first', 'same-layer');
    api.removeMarkerGroupLayer('missing', 'same-layer');
    fireMarkerEvents(old);
    assert.deepEqual(first.calls, []);
    const replacement = await addMarker('first', 'initial-group');
    fireMarkerEvents(old); fireMarkerEvents(replacement);
    assert.deepEqual(first.calls, markerCalls);
    assert.ok(window._leafletMarkers.other['same-layer']['same-marker']);
    assert.deepEqual(other.calls, []);
});

test('deferred popup image callback is inert after removing and readding its group', async () => {
    const { api, window, createMap } = await createHarness();
    await createMap('first');
    await api.addMarkerGroupLayer('first', JSON.stringify({ id: 'group', markers: [
        { id: 'poi', location: [50, 30], popup: { html: '', options: {} } }
    ] }));
    const marker = window._leafletMarkers.first.group.poi;
    const popup = marker.getPopup();
    let loaded;
    window.updatePopupAfterImagesLoad({ querySelectorAll: () => [{ complete: false, addEventListener: (_, callback) => loaded = callback }] }, marker);
    api.removeMarkerGroupLayer('first', 'group');
    await api.addMarkerGroupLayer('first', JSON.stringify({ id: 'group', markers: [] }));
    loaded();
    assert.equal(popup.updates, undefined);
    assert.equal(marker.getPopup(), null);
    assert.equal(popup.handlers.size, 0);
});

test('old cluster focus callback cannot open a removed or replacement group popup', async () => {
    const { api, window, createMap, addMarker } = await createHarness();
    await createMap('first');
    const marker = await addMarker('first', 'initial-group');
    const group = window._leafletLayers.first['same-layer'];
    let deferred;
    group.zoomToShowLayer = (_, callback) => deferred = callback;
    api.focusMarker('first', 'same-layer', 'same-marker');
    api.removeMarkerGroupLayer('first', 'same-layer');
    await addMarker('first', 'initial-group');
    assert.doesNotThrow(() => deferred(), 'Old callback must not access map flyTo or old popup');
    assert.notEqual(window._leafletMarkers.first['same-layer']['same-marker'], marker);
});

test('visibility toggles reuse initially hidden group, markers and control registration', async () => {
    const { api, window, createMap } = await createHarness();
    await createMap('map', { layersControl: {} });
    await api.addMarkerGroupLayer('map', JSON.stringify({
        id: 'hidden', isVisible: false, markers: [{ id: 'poi', location: [50, 30] }]
    }));
    const map = window._leafletMaps.map;
    const group = window._leafletLayers.map.hidden;
    const marker = window._leafletMarkers.map.hidden.poi;
    assert.equal(map.hasLayer(group), false);
    api.setMarkerGroupLayerVisibility('map', 'hidden', true);
    assert.equal(map.hasLayer(group), true);
    api.setMarkerGroupLayerVisibility('map', 'hidden', false);
    api.setMarkerGroupLayerVisibility('map', 'hidden', false);
    assert.equal(map.hasLayer(group), false);
    assert.equal(window._leafletLayers.map.hidden, group);
    assert.equal(window._leafletMarkers.map.hidden.poi, marker);
    assert.equal(group.markers.length, 1);
    assert.equal(window._leafletLayersControl.map.removed.length, 0);
    api.setMarkerGroupLayerVisibility('map', 'hidden', true);
    assert.equal(map.hasLayer(group), true);
});

test('initially hidden cluster defers refresh and ignores focus while accepting icon updates', async () => {
    const { api, window, createMap } = await createHarness();
    await createMap('map');
    await api.addMarkerGroupLayer('map', JSON.stringify({
        id: 'cluster', layerType: 'MarkerClusterLayer', isVisible: false,
        markers: [{ id: 'poi', location: [50, 30] }]
    }));
    const group = window._leafletLayers.map.cluster;
    const marker = window._leafletMarkers.map.cluster.poi;
    let zooms = 0;
    let refreshes = 0;
    group.zoomToShowLayer = () => { zooms++; };
    group.refreshClusters = () => { refreshes++; };
    api.focusMarker('map', 'cluster', 'poi');
    api.updateMarkerIcon('map', 'cluster', 'poi', JSON.stringify({ iconUrl: 'single' }));
    assert.equal(marker.icon.iconUrl, 'single');
    api.updateMarkersIconsBulk('map', 'cluster', JSON.stringify([{ id: 'poi', icon: { iconUrl: 'bulk' } }]));
    assert.equal(marker.icon.iconUrl, 'bulk');
    assert.deepEqual([zooms, refreshes], [0, 0]);

    api.setMarkerGroupLayerVisibility('map', 'cluster', true);
    api.focusMarker('map', 'cluster', 'poi');
    api.updateMarkerIcon('map', 'cluster', 'poi', JSON.stringify({ iconUrl: 'visible' }));
    api.updateMarkersIconsBulk('map', 'cluster', JSON.stringify([{ id: 'poi', icon: { iconUrl: 'latest' } }]));
    assert.deepEqual([zooms, refreshes], [1, 2]);
    assert.equal(marker.icon.iconUrl, 'latest');
});
