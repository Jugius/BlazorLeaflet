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
        off(name) { this.handlers.delete(name); },
        fire(name, event = {}) {
            for (const handler of [...(this.handlers.get(name) ?? [])]) handler(event);
        }
    };
}

async function createHarness() {
    const markers = [];
    const L = {
        map(id) {
            return eventSource({
                id, markers: [], controls: [], container: { style: {} }, removeCount: 0,
                getContainer() { return this.container; },
                remove() { this.removeCount++; }
            });
        },
        tileLayer() { return { addTo() {} }; },
        marker(location) {
            const marker = eventSource({
                getLatLng() { return { lat: location[0], lng: location[1] }; },
                addTo(map) { map.markers.push(this); return this; }
            });
            markers.push(marker);
            return marker;
        },
        layerGroup() {
            return {
                markers: [],
                addLayer(marker) { this.markers.push(marker); },
                addTo() {}
            };
        },
        control: {
            layers() { return { addTo() {}, addOverlay() {} }; }
        },
        Control: {
            extend(definition) {
                return class {
                    options = definition.options;
                    addTo(map) {
                        this.container = definition.onAdd.call(this, map);
                        map.controls.push(this);
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
    const window = { L };
    const module = new SourceTextModule(source, { context: createContext({ window, L, console, setTimeout }) });
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

    return { api, window, createMap, addMarker };
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
