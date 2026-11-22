import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';
import { createContext, SourceTextModule } from 'node:vm';

const source = await readFile(new URL('../src/OohelpSoft.Extensions.BlazorLeaflet/wwwroot/js/leafletInterop.js', import.meta.url), 'utf8');
const line = (id = 'line', options = {}) => ({ id, locations: [[50, 30], [51, 31]], options, popup: { html: id } });
const json = JSON.stringify;

function bounds(points = []) {
    return { points: [...points], isValid() { return this.points.length > 0; }, extend(other) { this.points.push(...other.points); return this; } };
}
function events(object) {
    return Object.assign(object, { handlers: new Map(), on(name, callback) { this.handlers.set(name, callback); return this; }, off() { this.handlers.clear(); this.cleaned = true; } });
}
async function harness() {
    const created = [];
    const failures = {};
    const L = {
        map(id) {
            return { id, layers: new Set(), hasLayer(layer) { return this.layers.has(layer); },
                options: { zoomSnap: 1 }, navigation: [], pointInView: true,
                getZoom() { return 13; }, getMinZoom() { return 0; }, getMaxZoom() { return 18; },
                getBounds() { return { contains: () => this.pointInView }; },
                panTo(point, options) { this.navigation.push({ point, options }); },
                flyTo(point, zoom, options) { this.navigation.push({ point, zoom, options }); },
                removeLayer(layer) { this.layers.delete(layer); }, fitBounds(value) { this.bounds = value; }, remove() { this.removed = true; } };
        },
        latLng(lat, lng) {
            return { lat, lng, distanceTo(other) {
                const radians = value => value * Math.PI / 180;
                const a = Math.sin(radians(other.lat - lat) / 2) ** 2 +
                    Math.cos(radians(lat)) * Math.cos(radians(other.lat)) * Math.sin(radians(other.lng - lng) / 2) ** 2;
                return 2 * 6371000 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
            } };
        },
        tileLayer() { return { addTo() {} }; },
        featureGroup() {
            if (failures.group) { delete failures.group; throw Error('group creation failed'); }
            return events({ children: new Set(), addLayer(item) {
                this.children.add(item);
                if (failures.add !== undefined && failures.add === item.id) { delete failures.add; throw Error('add failed'); }
            }, removeLayer(item) { this.children.delete(item); }, clearLayers() { this.children.clear(); },
            addTo(map) { map.layers.add(this); return this; }, getBounds() {
                const result = bounds();
                for (const item of this.children) result.extend(item.getBounds());
                return result;
            } });
        },
        layerGroup() { return this.featureGroup(); },
        marker(location) { return events({ getBounds() { return bounds([location]); } }); },
        polyline(locations, options) {
            const item = events({ id: null, locations, options: { color: '#3388ff', weight: 3, opacity: 1, dashArray: null, ...options },
                bindPopup(popup) { this.popup = popup; this.id = popup.html; return this; }, getPopup() { return this.popup; },
                openPopup(point) { this.openedPopupAt = point; },
                closePopup() { this.closed = true; }, unbindPopup() { this.popup = null; },
                setStyle(style) {
                    Object.assign(this.options, style);
                    if (failures.style === this.id || failures.restore === this.id) {
                        delete failures.style;
                        throw Error('style failed');
                    }
                },
                getLatLngs() { return this.locations.map(([lat, lng]) => L.latLng(lat, lng)); }, setLatLngs(value) {
                    this.locations = value.map(point => Array.isArray(point) ? point : [point.lat, point.lng]);
                    if (failures.geometry) { delete failures.geometry; throw Error('geometry failed'); }
                }, getBounds() { return bounds(this.locations); }
            });
            created.push(item);
            return item;
        },
        popup(options) { return events({ options, setContent(html) { this.html = html; return this; } }); },
        control: { layers() { return { overlays: new Set(), addTo() {}, addOverlay(layer) { this.overlays.add(layer); }, removeLayer(layer) { this.overlays.delete(layer); } }; } }
    };
    const window = { L };
    const module = new SourceTextModule(source, { context: createContext({ window, L, console, setTimeout }) });
    await module.link(() => { throw Error('Unexpected import'); });
    await module.evaluate();
    const api = module.namespace;
    const create = async (id = 'map', key = id) => api.createMap(id, json({ layersControl: {} }), { invokeMethodAsync() { return Promise.resolve(); } }, key);
    const add = (id = 'group', items = [], visible = true, map = 'map') => api.addPolylineGroupLayer(map, json({ id, polylines: items, isVisible: visible }));
    await create();
    return { api, window, created, failures, create, add };
}

test('empty and prefilled groups retain popup and support arbitrary ordinal polyline IDs', async () => {
    const h = await harness();
    h.add('empty');
    h.add('filled', [line('x'), line('X'), line('__proto__')]);
    assert.equal(Object.keys(h.window._leafletPolylines.map.empty).length, 0);
    const items = h.window._leafletPolylines.map.filled;
    assert.equal(Object.keys(items).length, 3);
    assert.equal(items.x.getPopup().html, 'x');
    assert.notEqual(items.x, items.X);
});

test('add remove clear clean popup and event handlers while absent removal is ignored', async () => {
    const h = await harness(); h.add();
    h.api.addPolylinesToLayerAsync('map', 'group', json([line('a'), line('b')]));
    const first = h.created[0], popup = first.popup;
    h.api.removePolylinesByIdsAsync('map', 'group', json(['missing', 'a']));
    assert.equal(first.cleaned, true); assert.equal(popup.cleaned, true); assert.equal(first.popup, null);
    h.api.clearPolylinesLayer('map', 'group');
    assert.equal(h.window._leafletLayers.map.group.children.size, 0);
    assert.equal(Object.keys(h.window._leafletPolylines.map.group).length, 0);
});

test('partial bulk styles preserve unspecified fields and empty dashArray resets dash', async () => {
    const h = await harness(); h.add('group', [line('a', { color: 'red', dashArray: '5 3' }), line('b')]);
    const first = h.created[0];
    h.api.updatePolylinesStylesBulk('map', 'group', json([{ id: 'a', options: { weight: 7, color: null, dashArray: '' } }, { id: 'b', options: { opacity: 0.4 } }]));
    assert.equal(first.options.color, 'red'); assert.equal(first.options.weight, 7); assert.equal(first.options.dashArray, null);
    assert.equal(h.created[1].options.opacity, 0.4);
    assert.equal(h.created.length, 2);
});

test('geometry and focus reuse the line and fit its entire bounds without opening popup', async () => {
    const h = await harness(); h.add('group', [line()]);
    const first = h.created[0], locations = [[1, 2], [3, 4], [5, 6]];
    h.api.updatePolylineLocations('map', 'group', 'line', json(locations));
    h.api.focusPolyline('map', 'group', 'line');
    assert.deepEqual(JSON.parse(json(h.window._leafletMaps.map.bounds.points)), locations);
    assert.equal(h.window._leafletMaps.map.layers.size, 1);
    assert.equal(h.window._leafletPolylines.map.group.line, first);
    assert.equal(h.created.length, 1);
    h.window._leafletMaps.map.bounds = null;
    h.api.focusPolyline('map', 'group', 'line', 'map', null);
    assert.deepEqual(JSON.parse(json(h.window._leafletMaps.map.bounds.points)), locations);
    assert.equal(first.openedPopupAt, undefined);
});

test('explicit polyline Popup opens at the halfway point by segment length and keeps native popup options', async () => {
    const h = await harness();
    const locations = [[0, 0], [0, 0], [0, 2], [0, 8], [0, 8]];
    h.add('group', [{ ...line(), locations, popup: { html: 'test', options: { autoPan: false } } }]);
    h.api.focusPolyline('map', 'group', 'line', 'map', json({ navigationMode: 'Popup' }));
    const map = h.window._leafletMaps.map, polyline = h.created[0];
    assert.equal(polyline.openedPopupAt.lat, 0);
    assert.ok(Math.abs(polyline.openedPopupAt.lng - 4) < 1e-12);
    assert.equal(polyline.getPopup().options.autoPan, false);
    assert.deepEqual(map.navigation, []);
    assert.equal(map.bounds, undefined);
});

test('explicit polyline Popup without a popup pans only for an offscreen midpoint and handles zero-length lines', async () => {
    const h = await harness();
    h.add('group', [{ ...line(), locations: [[50, 30], [50, 30]], popup: undefined }]);
    const map = h.window._leafletMaps.map;
    h.api.focusPolyline('map', 'group', 'line', 'map', json({}));
    assert.deepEqual(map.navigation, []);
    map.pointInView = false;
    h.api.focusPolyline('map', 'group', 'line', 'map', json({ animationDuration: 0 }));
    assert.deepEqual(JSON.parse(json(map.navigation)), [{ point: { lat: 50, lng: 30 }, options: { duration: 0, animate: false } }]);
    assert.equal(map.bounds, undefined);
});

test('explicit polyline PanTo and FlyTo target the midpoint with the configured duration and no highlight', async () => {
    const h = await harness();
    h.add('group', [{ ...line(), locations: [[0, 0], [0, 2]] }]);
    const map = h.window._leafletMaps.map, polyline = h.created[0];
    const initialStyle = { ...polyline.options };
    for (const options of [
        { navigationMode: 'PanTo', animationDuration: 0.75 },
        { navigationMode: 'FlyTo', animationDuration: 1 },
        { navigationMode: 'FlyTo', animationDuration: 0 }
    ]) h.api.focusPolyline('map', 'group', 'line', 'map', json(options));
    assert.deepEqual(JSON.parse(json(map.navigation)), [
        { point: { lat: 0, lng: 1 }, options: { duration: 0.75 } },
        { point: { lat: 0, lng: 1 }, zoom: 13, options: { duration: 1 } },
        { point: { lat: 0, lng: 1 }, zoom: 13, options: { duration: 0, animate: false } }
    ]);
    assert.equal(polyline.openedPopupAt, undefined);
    assert.equal(map.bounds, undefined);
    assert.deepEqual(polyline.options, initialStyle);
});

test('explicit polyline focus does not pan for a hidden group or a missing line and rejects an old context', async () => {
    const h = await harness();
    h.add('group', [line()], false);
    const map = h.window._leafletMaps.map;
    const options = json({ navigationMode: 'PanTo' });
    h.api.focusPolyline('map', 'group', 'line', 'map', options);
    h.api.setPolylineGroupLayerVisibility('map', 'group', true);
    h.api.removePolylinesByIdsAsync('map', 'group', json(['line']));
    assert.throws(() => h.api.focusPolyline('map', 'group', 'line', 'map', options), /not found/);
    await h.create('map', 'replacement');
    assert.throws(() => h.api.focusPolyline('map', 'group', 'line', 'map', options), /no longer active/);
    assert.deepEqual(map.navigation, []);
    assert.equal(map.bounds, undefined);
});

test('hidden group accepts updates and LayersControl visibility remains authoritative', async () => {
    const h = await harness(); h.add('group', [line()], false);
    const map = h.window._leafletMaps.map, group = h.window._leafletLayers.map.group;
    h.api.updatePolylinesStylesBulk('map', 'group', json([{ id: 'line', options: { color: 'blue' } }]));
    h.api.setPolylineGroupLayerVisibility('map', 'group', true);
    map.removeLayer(group); // Native LayersControl toggle.
    h.api.setPolylineGroupLayerVisibility('map', 'group', true);
    assert.equal(map.hasLayer(group), true);
    assert.equal(h.window._leafletPolylines.map.group.line.options.color, 'blue');
});

test('common fitBounds combines marker and polyline groups', async () => {
    const h = await harness(); h.add('lines', [line()]);
    await h.api.addMarkerGroupLayer('map', json({ id: 'markers', layerType: 'FeatureGroup', markers: [{ id: 'm', location: [10, 20] }] }));
    await h.api.fitBoundsToLayerGroups('map', ['markers', 'lines']);
    assert.deepEqual(JSON.parse(json(h.window._leafletMaps.map.bounds.points)), [[10, 20], [50, 30], [51, 31]]);
});

test('same object IDs stay isolated across groups and maps', async () => {
    const h = await harness(); h.add('one', [line()]); h.add('two', [line()]);
    await h.create('other'); h.add('one', [line()], true, 'other');
    h.api.updatePolylinesStylesBulk('map', 'one', json([{ id: 'line', options: { color: 'red' } }]));
    assert.equal(h.window._leafletPolylines.map.one.line.options.color, 'red');
    assert.equal(h.window._leafletPolylines.map.two.line.options.color, '#3388ff');
    assert.equal(h.window._leafletPolylines.other.one.line.options.color, '#3388ff');
});

test('cross-type group ID collisions reject both directions including case variants', async () => {
    const h = await harness(); h.add('Lines');
    await assert.rejects(h.api.addMarkerGroupLayer('map', json({ id: 'lines' })), /already uses/);
    await h.api.addMarkerGroupLayer('map', json({ id: 'Markers' }));
    assert.throws(() => h.add('markers'), /already uses/);
    assert.throws(() => h.add('LINES'), /already uses/);
});

test('invalid additions validate the entire batch before creating any objects', async () => {
    const h = await harness(); h.add('group', [line('existing')]);
    for (const bad of [line('existing'), { ...line('bad'), locations: [[0, 0]] }, { ...line('bad'), locations: [[0, 0], [null, 1]] }]) {
        assert.throws(() => h.api.addPolylinesToLayerAsync('map', 'group', json([line('new'), bad])));
        assert.equal(h.created.length, 1);
    }
    assert.throws(() => h.api.addPolylinesToLayerAsync('map', 'group', json([line('x'), line('x')])));
});

test('addition failure rolls back earlier objects and the failing object', async () => {
    const h = await harness(); h.add(); h.failures.add = 'b';
    assert.throws(() => h.api.addPolylinesToLayerAsync('map', 'group', json([line('a'), line('b')])), /add failed/);
    assert.equal(Object.keys(h.window._leafletPolylines.map.group).length, 0);
    assert.equal(h.window._leafletLayers.map.group.children.size, 0);
    assert.equal(h.created.every(item => item.cleaned && item.popup === null), true);
});

test('initial group addition failure leaves no registry or control entries', async () => {
    const h = await harness(); h.failures.add = 'b';
    assert.throws(() => h.add('group', [line('a'), line('b')]));
    assert.equal(Object.hasOwn(h.window._leafletLayers.map, 'group'), false);
    assert.equal(h.window._leafletLayersControl.map.overlays.size, 0);
});

test('invalid and missing style updates leave all lines unchanged', async () => {
    const h = await harness(); h.add('group', [line('a')]);
    assert.throws(() => h.api.updatePolylinesStylesBulk('map', 'group', json([{ id: 'a', options: { color: 'red' } }, { id: 'missing', options: {} }])));
    assert.equal(h.created[0].options.color, '#3388ff');
    assert.throws(() => h.api.updatePolylineLocations('map', 'group', 'missing', json([[0, 0], [1, 1]])));
    assert.throws(() => h.api.focusPolyline('map', 'group', 'missing'));
});

test('style failure restores earlier and failing objects and reports rollback failure', async () => {
    const h = await harness(); h.add('group', [line('a'), line('b')]); h.failures.style = 'b';
    const updates = json([{ id: 'a', options: { color: 'red' } }, { id: 'b', options: { weight: 8 } }]);
    assert.throws(() => h.api.updatePolylinesStylesBulk('map', 'group', updates), /style failed/);
    assert.equal(h.created[0].options.color, '#3388ff'); assert.equal(h.created[1].options.weight, 3);
    h.failures.restore = 'b';
    assert.throws(() => h.api.updatePolylinesStylesBulk('map', 'group', updates), /rollback failed/);
});

test('geometry failure restores previous coordinates', async () => {
    const h = await harness(); h.add('group', [line()]); h.failures.geometry = true;
    assert.throws(() => h.api.updatePolylineLocations('map', 'group', 'line', json([[1, 2], [3, 4]])), /geometry failed/);
    assert.deepEqual(JSON.parse(json(h.created[0].locations)), [[50, 30], [51, 31]]);
});

test('group removal frees resources and permits a fresh same-ID group', async () => {
    const h = await harness(); h.add('group', [line()]); const old = h.created[0];
    h.api.removePolylineGroupLayer('map', 'group');
    assert.equal(old.cleaned, true); assert.equal(old.popup, null);
    assert.equal(h.window._leafletLayersControl.map.overlays.size, 0);
    h.add('group', [line()]); assert.notEqual(h.window._leafletPolylines.map.group.line, old);
});

test('map destroy cleans polylines and stale creation keys preserve replacement map', async () => {
    const h = await harness(); h.add('group', [line()]); const old = h.created[0];
    await h.create('map', 'replacement'); h.add('group', [line()]);
    assert.equal(old.cleaned, true);
    h.api.destroyMap('map', 'map');
    assert.equal(Object.keys(h.window._leafletPolylines.map).length, 1);
    assert.throws(() => h.api.updatePolylinesStylesBulk('map', 'group', '[]', 'map'), /no longer active/);
    h.api.destroyMap('map', 'replacement');
    assert.equal(Object.hasOwn(h.window._leafletPolylines, 'map'), false);
    assert.throws(() => h.api.focusPolyline('map', 'group', 'line'), /no longer active/);
});
test('map cleanup errors still invalidate ownership and release every registry', async () => {
    const h = await harness(); h.add('group', [line('a'), line('b')]);
    const map = h.window._leafletMaps.map;
    h.created[0].off = () => { throw Error('cleanup failed'); };
    assert.throws(() => h.api.destroyMap('map'), /cleanup failed/);
    assert.equal(map.removed, true);
    assert.equal(h.created[1].cleaned, true);
    for (const key of ['_leafletMaps', '_leafletLayers', '_leafletMarkers', '_leafletPolylines', '_leafletLayersControl'])
        assert.equal(Object.hasOwn(h.window[key], 'map'), false);
});

test('late old creation failure preserves replacement polyline registry', async () => {
    const h = await harness();
    let rejectOld;
    const old = h.api.createMap('map', '{}', { invokeMethodAsync() { return new Promise((_, reject) => { rejectOld = reject; }); } }, 'old');
    await Promise.resolve();
    await h.create('map', 'new'); h.add('group', [line()]);
    const current = h.window._leafletPolylines.map.group.line;
    rejectOld(Error('old callback failed'));
    await assert.rejects(old, /old callback failed/);
    assert.equal(h.window._leafletPolylines.map.group.line, current);
    assert.equal(h.window._leafletMaps.map.removed, undefined);
});

test('marker replacement resolves the exact old key across case variants', async () => {
    const h = await harness();
    await h.api.addMarkerGroupLayer('map', json({ id: 'Markers', layerType: 'FeatureGroup' }));
    const previous = h.window._leafletLayers.map.Markers;
    await h.api.addMarkerGroupLayer('map', json({ id: 'MARKERS', layerType: 'FeatureGroup' }));
    assert.deepEqual(Object.keys(h.window._leafletMarkers.map), ['MARKERS']);
    assert.deepEqual(Object.keys(h.window._leafletLayers.map), ['MARKERS']);
    assert.equal(h.window._leafletMaps.map.hasLayer(previous), false);
    assert.equal(h.window._leafletLayersControl.map.overlays.has(previous), false);
});

test('marker replacement preserves the old group when constructing the new one fails', async () => {
    const h = await harness();
    await h.api.addMarkerGroupLayer('map', json({ id: 'Markers', layerType: 'FeatureGroup' }));
    const previous = h.window._leafletLayers.map.Markers;
    h.failures.group = true;
    await assert.rejects(h.api.addMarkerGroupLayer('map', json({ id: 'MARKERS', layerType: 'FeatureGroup' })), /group creation failed/);
    assert.equal(h.window._leafletLayers.map.Markers, previous);
    assert.equal(h.window._leafletMaps.map.hasLayer(previous), true);
    assert.equal(h.window._leafletLayersControl.map.overlays.has(previous), true);
});

test('marker remove and visibility accept JS-only marker groups and protect polyline ownership', async () => {
    const h = await harness(); h.add('lines', [line()]);
    const lines = h.window._leafletLayers.map.lines;
    h.api.setMarkerGroupLayerVisibility('map', 'lines', false);
    h.api.removeMarkerGroupLayer('map', 'lines');
    assert.equal(h.window._leafletMaps.map.hasLayer(lines), true);
    assert.equal(h.window._leafletLayers.map.lines, lines);
    await h.api.addMarkerGroupLayer('map', json({ id: 'js-only' }));
    h.api.setMarkerGroupLayerVisibility('map', 'js-only', false);
    assert.equal(h.window._leafletMaps.map.hasLayer(h.window._leafletLayers.map['js-only']), false);
    h.api.removeMarkerGroupLayer('map', 'js-only');
    assert.equal(Object.hasOwn(h.window._leafletLayers.map, 'js-only'), false);
    assert.equal(h.window._leafletPolylines.map.lines.line, h.created[0]);
});

test('typed polyline visibility preserves marker ownership and removed groups are harmless', async () => {
    const h = await harness(); h.add('lines', [line()]);
    await h.api.addMarkerGroupLayer('map', json({ id: 'markers' }));
    h.api.setPolylineGroupLayerVisibility('map', 'markers', false);
    assert.equal(h.window._leafletMaps.map.hasLayer(h.window._leafletLayers.map.markers), true);
    h.api.removePolylineGroupLayer('map', 'lines');
    h.api.setPolylineGroupLayerVisibility('map', 'lines', true);
    await h.api.fitBoundsToLayerGroups('map', ['lines']);
    assert.equal(h.window._leafletMaps.map.bounds, undefined);
    h.api.removeMarkerGroupLayer('map', 'markers');
    h.api.setMarkerGroupLayerVisibility('map', 'markers', true);
});

test('hidden focus validates missing IDs and preserves viewport until the group is shown', async () => {
    const h = await harness(); h.add('lines', [line()], false);
    const map = h.window._leafletMaps.map;
    map.bounds = bounds([[1, 2]]); const previous = map.bounds;
    h.api.focusPolyline('map', 'lines', 'line');
    assert.equal(map.bounds, previous);
    assert.throws(() => h.api.focusPolyline('map', 'lines', 'missing'), /not found/);
    h.api.setPolylineGroupLayerVisibility('map', 'lines', true);
    h.api.focusPolyline('map', 'lines', 'line');
    assert.notEqual(map.bounds, previous);
    assert.deepEqual(JSON.parse(json(map.bounds.points)), [[50, 30], [51, 31]]);
});

test('visibility and marker removal retain optional creation key guards', async () => {
    const h = await harness(); h.add('lines', [line()]);
    await h.api.addMarkerGroupLayer('map', json({ id: 'markers' }));
    assert.throws(() => h.api.setPolylineGroupLayerVisibility('map', 'lines', false, 'old'), /no longer active/);
    assert.throws(() => h.api.setMarkerGroupLayerVisibility('map', 'markers', false, 'old'), /no longer active/);
    assert.throws(() => h.api.removeMarkerGroupLayer('map', 'markers', 'old'), /no longer active/);
    assert.equal(h.window._leafletMaps.map.layers.size, 2);
});
