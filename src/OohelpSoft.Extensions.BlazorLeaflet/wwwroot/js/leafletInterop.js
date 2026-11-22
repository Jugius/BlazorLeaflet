// ----------------------------
//  Global storages
// ----------------------------
window._leafletMaps = window._leafletMaps || {};
window._leafletLayers = window._leafletLayers || {};     // mapId → { layerName → LayerGroup }
window._leafletMarkers = window._leafletMarkers || {};    // mapId → { layerName → { id → marker } }
window._leafletPolylines = window._leafletPolylines || {};

const mapContexts = new Map();
const markerGroupFocusKeys = new WeakMap();

// ----------------------------
//  Lazy Leaflet loader
// ----------------------------

const config = {
    api: {
        src: "./_content/OohelpSoft.Extensions.BlazorLeaflet/js/leaflet.js",
        href: "./_content/OohelpSoft.Extensions.BlazorLeaflet/css/leaflet.css"
    },
    apiCluster: {
        src: "./_content/OohelpSoft.Extensions.BlazorLeaflet/js/leaflet.markercluster.js",
        href1: "./_content/OohelpSoft.Extensions.BlazorLeaflet/css/MarkerCluster.css",
        href2: "./_content/OohelpSoft.Extensions.BlazorLeaflet/css/MarkerCluster.Default.css"
    }
}
async function ensureLeafletLoaded() {
    if (window.L) return; // уже загружен

    // Проверяем, нет ли в документе <script src="...leaflet.js">
    if (!document.querySelector(`script[src="${config.api.src}"]`)) {
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = config.api.href;   

        const script = document.createElement('script');
        script.src = config.api.src;

        document.head.appendChild(link);
        document.head.appendChild(script);

        await new Promise((resolve, reject) => {
            script.onload = resolve;
            script.onerror = reject;
        });
    }

    if (!document.querySelector(`script[src="${config.apiCluster.src}"]`)) {

        const link1 = document.createElement('link');
        link1.rel = 'stylesheet';
        link1.href = config.apiCluster.href1;        

        const link2 = document.createElement('link');
        link2.rel = 'stylesheet';
        link2.href = config.apiCluster.href2;        

        const scriptCluster = document.createElement('script');
        scriptCluster.src = config.apiCluster.src;        

        document.head.appendChild(link1);
        document.head.appendChild(link2);
        document.head.appendChild(scriptCluster);

        await new Promise((resolve, reject) => {
            scriptCluster.onload = resolve;
            scriptCluster.onerror = reject;
        });
    }

    // подождем чуть-чуть, чтобы Leaflet проинициализировался
    await new Promise(r => setTimeout(r, 50));
}

export async function createMap(id, optionsJson, dotNetObjRef, creationKey) {
    destroyMap(id);
    const context = {
        dotNetObjRef,
        creationKey,
        map: null,
        markerClickRegistered: false,
        markerDragEndRegistered: false,
        active: true
    };
    mapContexts.set(id, context);
    const isCurrent = () => context.active && mapContexts.get(id) === context;
    try {
        await ensureLeafletLoaded();
        if (!isCurrent()) return false;

        const options = JSON.parse(optionsJson);
        const map = context.map = L.map(id, options);
        // Publish ownership before tiles/controls can fail, so rollback can always remove this map.
        window._leafletMaps[id] = map;
        window._leafletLayers[id] = Object.create(null);
        window._leafletMarkers[id] = {};
        window._leafletPolylines[id] = Object.create(null);
        window._leafletLayersControl ??= {};

        if (!options.tileLayers || options.tileLayers.length === 0) {
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
        }
        else {
            for (const l of options.tileLayers) {
                const tl = createTileLayerInternal(l);
                tl.addTo(map);
            }
        }

        if (options.layersControl) {
            const layersControl = L.control.layers({}, {}, options.layersControl);
            layersControl.addTo(map);
            window._leafletLayersControl[id] = layersControl;
        }
        if (options.scaleControl) L.control.scale(options.scaleControl).addTo(map);
        if (options.addMarkerControl) createAddMarkerControl(map, options.addMarkerControl, context).addTo(map);

        await context.dotNetObjRef.invokeMethodAsync('OnJsMapCreated');
        return isCurrent();
    }
    catch (error) {
        try { releaseMap(id, context); }
        catch (cleanupError) { throw new AggregateError([error, cleanupError], 'Map creation and cleanup failed'); }
        throw error;
    }
}

export function registerMarkerClickCallback(mapId) {
    const context = mapContexts.get(mapId);
    if (!context?.active) return;

    context.markerClickRegistered = true;
}
export function registerMarkerDragEndCallback(mapId) {
    const context = mapContexts.get(mapId);
    if (!context?.active) return;

    context.markerDragEndRegistered = true;
}

export async function addMarkerGroupLayer(mapId, layerJson, focusAttachmentKey) {
    const map = window._leafletMaps?.[mapId];
    const context = mapContexts.get(mapId);
    if (!map || !context?.active) return;

    const l = JSON.parse(layerJson);
    if (Object.keys(window._leafletPolylines[mapId] ?? {}).some(id => id.toLowerCase() === l.id.toLowerCase()))
        throw new Error('A polyline group already uses this ID');
    const layer = createMarkerGroupLayerInternal(l);

    const layerId = l.id;

    const previousId = Object.keys(window._leafletMarkers[mapId]).find(id => id.toLowerCase() === layerId.toLowerCase());
    if (previousId !== undefined) removeMarkerGroupLayer(mapId, previousId);

    markerGroupFocusKeys.set(layer, focusAttachmentKey);
    window._leafletLayers[mapId][layerId] = layer;
    window._leafletMarkers[mapId][layerId] = {};

    if (l.markers?.length > 0) {
        const markersDict = window._leafletMarkers[mapId][layerId];
        for (const m of l.markers) {
            const marker = createMarkerInternal(m, context, () => window._leafletLayers?.[mapId]?.[layerId] === layer);
            layer.addLayer(marker);
            markersDict[m.id] = marker;
        }
    }

    if (l.isVisible !== false) layer.addTo(map);

    const layersControl = window._leafletLayersControl[mapId];

    if (layersControl) {
        layersControl.addOverlay(layer, l.name ?? l.id);
    }

    
}
function setLayerGroupVisibility(mapId, layerId, isVisible) {
    const map = window._leafletMaps?.[mapId];
    const layer = window._leafletLayers?.[mapId]?.[layerId];
    if (!map || !layer || map.hasLayer(layer) === isVisible) return;
    if (isVisible) layer.addTo(map);
    else map.removeLayer(layer);
}

export function setMarkerGroupLayerVisibility(mapId, layerId, isVisible, creationKey) {
    if (creationKey !== undefined) requireMapContext(mapId, creationKey);
    if (!Object.hasOwn(window._leafletMarkers?.[mapId] ?? {}, layerId)) return;
    setLayerGroupVisibility(mapId, layerId, isVisible);
}

export function setPolylineGroupLayerVisibility(mapId, layerId, isVisible, creationKey) {
    if (creationKey !== undefined) requireMapContext(mapId, creationKey);
    if (!Object.hasOwn(window._leafletPolylines?.[mapId] ?? {}, layerId)) return;
    setLayerGroupVisibility(mapId, layerId, isVisible);
}

export function removeMarkerGroupLayer(mapId, layerId, creationKey) {
    if (creationKey !== undefined) requireMapContext(mapId, creationKey);
    if (!Object.hasOwn(window._leafletMarkers?.[mapId] ?? {}, layerId)) return;
    const layer = window._leafletLayers?.[mapId]?.[layerId];
    if (!layer) return;
    const markers = window._leafletMarkers?.[mapId]?.[layerId] ?? {};
    // Invalidate identity before removing children; retained callbacks must stay inert on re-add.
    delete window._leafletLayers[mapId][layerId];
    delete window._leafletMarkers[mapId][layerId];
    for (const marker of Object.values(markers)) {
        marker.off();
        marker.getPopup?.()?.off();
        marker.closePopup?.();
        marker.unbindPopup?.();
    }
    layer.off?.();
    window._leafletLayersControl?.[mapId]?.removeLayer(layer);
    window._leafletMaps?.[mapId]?.removeLayer(layer);
    layer.clearLayers();
}
export function clearLayer(mapId, layerId) {
    const layer = window._leafletLayers?.[mapId]?.[layerId];
    if (!layer) return;

    layer.clearLayers();
    window._leafletMarkers[mapId][layerId] = {};
}

// ----------------------------
//  Add MANY markers to layer
// ----------------------------
export async function addMarkersToLayerAsync(mapId, layerName, markersJson) {
    const map = window._leafletMaps?.[mapId];
    const context = mapContexts.get(mapId);
    if (!map || !context?.active) return;

    const layer = window._leafletLayers[mapId]?.[layerName];
    if (!layer) return;

    const markersDict = window._leafletMarkers[mapId][layerName];
    const items = JSON.parse(markersJson);

    for (const m of items) {
        const marker = createMarkerInternal(m, context, () => window._leafletLayers?.[mapId]?.[layerName] === layer && markersDict[m.id] === marker);
        layer.addLayer(marker);
        markersDict[m.id] = marker;
    }
}


// ----------------------------
//  Remove MANY markers by IDs
// ----------------------------
export async function removeMarkersByIdsAsync(mapId, layerName, idsJson) {
    const layer = window._leafletLayers[mapId]?.[layerName];
    if (!layer) return;

    const markersDict = window._leafletMarkers[mapId]?.[layerName];
    if (!markersDict) return;

    const ids = JSON.parse(idsJson);

    for (const id of ids) {
        const marker = markersDict[id];
        if (marker) {
            layer.removeLayer(marker);
            delete markersDict[id];
        }
    }
}

export async function addMarkersAsync(mapId, markersJson) {
    const markers = JSON.parse(markersJson);
    const map = window._leafletMaps?.[mapId];
    const context = mapContexts.get(mapId);
    if (!map || !context?.active) return;

    for (const m of markers) {
        const marker = createMarkerInternal(m, context);
        marker.addTo(map);
    }
}

export function updateMarkerIcon(mapId, layerId, markerId, iconJson) {
    const marker = window._leafletMarkers?.[mapId]?.[layerId]?.[markerId];
    if (!marker) return;

    const iconOptions = JSON.parse(iconJson);
    const newIcon = L.icon(iconOptions);

    marker.setIcon(newIcon);

    const layer = window._leafletLayers?.[mapId]?.[layerId];
    if (layer?.refreshClusters && window._leafletMaps?.[mapId]?.hasLayer(layer)) {
        layer.refreshClusters();
    }
}
export function updateMarkersIconsBulk(mapId, layerId, iconUpdatesJson) {
    const updates = JSON.parse(iconUpdatesJson);

    const markersDict = window._leafletMarkers?.[mapId]?.[layerId];
    if (!markersDict) return;

    for (const u of updates) {
        const marker = markersDict[u.id];
        if (marker) {
            const newIcon = L.icon(u.icon);
            marker.setIcon(newIcon);
        }
    }

    const layer = window._leafletLayers?.[mapId]?.[layerId];
    if (layer?.refreshClusters && window._leafletMaps?.[mapId]?.hasLayer(layer)) {
        layer.refreshClusters();
    }
}

export function focusMarker(mapId, layerId, markerId, optionsJson, creationKey, focusAttachmentKey) {
    const explicit = optionsJson != null;
    if (explicit && !focusAttachmentKey) return;
    if (explicit) requireMapContext(mapId, creationKey ?? undefined);
    const map = window._leafletMaps?.[mapId];
    const layer = window._leafletLayers?.[mapId]?.[layerId];
    if (explicit && markerGroupFocusKeys.get(layer) !== focusAttachmentKey) return;
    const marker = window._leafletMarkers?.[mapId]?.[layerId]?.[markerId];

    if (!map || !layer || !marker) {
        console.warn("focusMarker: not found", { mapId, layerId, markerId });
        return;
    }
    if (!map.hasLayer(layer)) return;

    if (explicit) {
        const isMarkerDisplayed = () => !layer.zoomToShowLayer || map.hasLayer(marker);
        const { highlight = true, ...options } = JSON.parse(optionsJson);
        const openPopup = marker.getPopup() ? () => {
            if (isMarkerDisplayed()) marker.openPopup();
        } : null;
        focusTarget(map, marker.getLatLng(), options, openPopup);
        if (highlight && window._leafletMaps?.[mapId] === map && map.hasLayer(layer) &&
            window._leafletLayers?.[mapId]?.[layerId] === layer &&
            window._leafletMarkers?.[mapId]?.[layerId]?.[markerId] === marker && isMarkerDisplayed()) {
            highlightMarker(marker);
        }
        return;
    }

    const openAndHighlight = () => {
        if (!map.hasLayer(layer) || window._leafletLayers?.[mapId]?.[layerId] !== layer ||
            window._leafletMarkers?.[mapId]?.[layerId]?.[markerId] !== marker) return;
        // центрирование (плавное)
        map.flyTo(marker.getLatLng(), map.getZoom(), {
            duration: 0.5
        });

        // открыть popup
        if (marker.getPopup()) {
            marker.openPopup();
        }

        // подсветка
        setTimeout(() => {
            if (!map.hasLayer(layer) || window._leafletLayers?.[mapId]?.[layerId] !== layer ||
                window._leafletMarkers?.[mapId]?.[layerId]?.[markerId] !== marker) return;
            highlightMarker(marker);
        }, 300);
    };

    // 💥 если это кластер
    if (layer.zoomToShowLayer) {
        layer.zoomToShowLayer(marker, openAndHighlight);
    } else {
        openAndHighlight();
    }
}
function highlightMarker(marker) {
    const el = marker._icon;
    if (!el) return;

    el.classList.remove("marker-highlight");
    void el.offsetWidth;
    el.classList.add("marker-highlight");
}

function focusTarget(map, point, { navigationMode = 'Popup', animationDuration = 0.5 }, openPopup) {
    switch (navigationMode) {
        case 'Popup':
            if (openPopup) {
                openPopup();
                return;
            }
            if (map.getBounds().contains(point)) return;
            break;
        case 'PanTo':
        case 'FlyTo':
            break;
        default:
            throw new Error('Unknown focus navigation mode');
    }

    const duration = navigationMode === 'FlyTo' ? Math.max(0, animationDuration) : animationDuration;
    const options = { duration };
    if (duration === 0) options.animate = false;
    // panTo and non-animated flyTo use setView, which normally snaps fractional zoom.
    const zoomSnap = map.options.zoomSnap;
    map.options.zoomSnap = 0;
    try {
        if (navigationMode === 'FlyTo') {
            const zoom = Math.max(map.getMinZoom(), Math.min(map.getMaxZoom(), map.getZoom()));
            map.flyTo(point, zoom, options);
        } else {
            map.panTo(point, options);
        }
    } finally {
        map.options.zoomSnap = zoomSnap;
    }
}

function createMarkerInternal(m, context, isOwned = () => true) {
    let markerOptions = {};

    if (m.icon) {
        markerOptions.icon = L.icon(m.icon);
    }

    if (m.draggable) {
        markerOptions.draggable = true;
    }

    const marker = L.marker(m.location, markerOptions);
    marker._isPresenterActive = () => context.active && isOwned();

    if (m.popup) {

        const popup = L.popup(m.popup.options).setContent(m.popup.html);
        popup.on('add', () => {
            // обращаемся к DOM popUp контейнеру
            if (!marker._isPresenterActive()) return;
            const popupElement = popup.getElement();
            window.updatePopupAfterImagesLoad(popupElement, marker);
        });
        marker.bindPopup(popup);
    }

    marker.on('click', function (e) {
        if (marker._isPresenterActive() && context.markerClickRegistered && context.dotNetObjRef) {
            context.dotNetObjRef.invokeMethodAsync("OnJSMarkerClick", m.id);
        }
    });

    marker.on('dragend', function () {
        if (marker._isPresenterActive() && context.markerDragEndRegistered && context.dotNetObjRef) {
            const position = marker.getLatLng();

            context.dotNetObjRef.invokeMethodAsync(
                "OnJSMarkerDraged",
                m.id,
                position.lat,
                position.lng
            );
        }
    });

    return marker;
}


function createTileLayerInternal(l) {
    let options = undefined;

    if (l.attribution) {
        options = { attribution: l.attribution };
    }
    const tileLayer = L.tileLayer(l.tyleUrl, options);
    return tileLayer;
}

function createMarkerGroupLayerInternal(l) {
    let lOptions = undefined;
    if (l.options) {
        lOptions = l.options;
    }

    let layer = undefined;

    if (l.layerType === "FeatureGroup") {
        layer = L.featureGroup(lOptions);
    }
    else if (l.layerType === "MarkerClusterLayer") {
        layer = L.markerClusterGroup(lOptions);
    }
    else {
        layer = L.layerGroup();
    }
    return layer;
}

function createAddMarkerControl(map, controlOptions, context) {
    const AddMarkerControl = L.Control.extend({
        options: {
            position: controlOptions.position || 'topleft',
            title: controlOptions.title || 'Добавить маркер',
            content: controlOptions.content || '📍',
            cursor: controlOptions.cursor || 'crosshair'
        },

        onAdd: function (map) {
            const container = L.DomUtil.create('div', 'leaflet-bar leaflet-control');
            const button = L.DomUtil.create('a', 'leaflet-control-add-marker', container);

            button.href = '#';
            button.title = this.options.title;
            button.innerHTML = this.options.content;
            button.style.fontSize = '16px';
            button.style.lineHeight = '30px';
            button.style.textAlign = 'center';
            button.style.textDecoration = 'none';
            button.style.color = '#333';
            button.style.display = 'block';

            // Предотвращаем всплытие кликов и двойных кликов на карту
            L.DomEvent.disableClickPropagation(container);
            L.DomEvent.disableScrollPropagation(container);

            let isAddMode = false;

            L.DomEvent.on(button, 'click', (e) => {
                L.DomEvent.preventDefault(e);

                isAddMode = !isAddMode;

                if (isAddMode) {
                    button.style.backgroundColor = '#e6f7ff'; // Легкая подсветка активного состояния
                    button.style.borderColor = '#1890ff';
                    map.getContainer().style.cursor = this.options.cursor;

                    // Однократный слушатель клика по карте
                    map.once('click', (mapClickEvent) => {
                        // Сбрасываем состояние кнопки и курсор
                        isAddMode = false;
                        button.style.backgroundColor = '#fff';
                        button.style.borderColor = '';
                        map.getContainer().style.cursor = '';

                        // Вызываем событие в Blazor C#
                        if (context.active && context.dotNetObjRef) {
                            context.dotNetObjRef.invokeMethodAsync(
                                'OnMapPointClicked',
                                mapClickEvent.latlng.lat,
                                mapClickEvent.latlng.lng
                            );
                        }
                    });

                } else {
                    // Если пользователь нажал кнопку повторно (отменил режим)
                    button.style.backgroundColor = '#fff';
                    button.style.borderColor = '';
                    map.getContainer().style.cursor = '';
                    map.off('click');
                }
            });

            return container;
        }
    });

    return new AddMarkerControl();
}


export async function fitBoundsToLayerGroups(mapId, layerGroupIds, creationKey) {
    if (creationKey !== undefined) requireMapContext(mapId, creationKey);
    const map = window._leafletMaps?.[mapId];
    if (!map) return;

    // Если слоёв нет — просто выходим
    if (!layerGroupIds || layerGroupIds.length === 0) return;

    let bounds = null;

    for (const layerId of layerGroupIds) {
        const layer = window._leafletLayers?.[mapId]?.[layerId];
        if (!layer) continue;

        // Получаем границы слоя
        const layerBounds = layer.getBounds();
        if (!layerBounds.isValid()) continue;

        // Если первая валидная граница:
        if (!bounds) {
            bounds = layerBounds;
        } else {
            bounds.extend(layerBounds);
        }
    }

    if (bounds) {
        map.fitBounds(bounds, { padding: [50, 50] });
    }
}

window.updatePopupAfterImagesLoad = function (popupElement, marker) {
    const images = popupElement.querySelectorAll("img");

    images.forEach(img => {
        if (img.complete) {
            // уже загружено → можно сразу обновить
            if (marker._isPresenterActive?.() !== false) marker.getPopup()?.update();
        } else {
            // слушаем загрузку
            img.addEventListener("load", () => {
                if (marker._isPresenterActive?.() !== false) marker.getPopup()?.update();
            });
        }
    });
}

// Layer operations share the map lifecycle and the common layer registry.
function requireMapContext(mapId, creationKey) {
    const context = mapContexts.get(mapId);
    if (!context?.active || context.map !== window._leafletMaps?.[mapId] ||
        (creationKey !== undefined && context.creationKey !== creationKey))
        throw new Error('The map context is no longer active');
    return context;
}

function polylineGroup(mapId, layerId, creationKey) {
    const context = requireMapContext(mapId, creationKey);
    const layer = window._leafletLayers?.[mapId]?.[layerId];
    const polylines = window._leafletPolylines?.[mapId]?.[layerId];
    if (!layer || !polylines) throw new Error('Polyline group was not found');
    return { context, layer, polylines };
}

function validatePolylineId(id) {
    if (typeof id !== 'string' || id.trim().length === 0) throw new Error('A polyline ID is required');
}

function validatePolylineLocations(locations) {
    if (!Array.isArray(locations) || locations.length < 2 || locations.some(p =>
        !Array.isArray(p) || p.length !== 2 || !p.every(Number.isFinite)))
        throw new Error('A polyline requires at least two finite coordinates');
}

function polylineStyle(options) {
    if (!options || typeof options !== 'object') throw new Error('Polyline options are required');
    const style = {};
    for (const key of ['color', 'weight', 'opacity', 'dashArray', 'lineCap', 'lineJoin']) {
        if (options[key] != null) style[key] = options[key];
    }
    if (style.weight !== undefined && !Number.isInteger(style.weight)) throw new Error('Weight must be an integer');
    if (style.opacity !== undefined && !Number.isFinite(style.opacity)) throw new Error('Opacity must be finite');
    if (style.dashArray === '') style.dashArray = null;
    return style;
}

function disposePolyline(polyline) {
    polyline.off();
    polyline.getPopup?.()?.off();
    polyline.closePopup?.();
    polyline.unbindPopup?.();
}

function createPolylineInternal(item) {
    const polyline = L.polyline(item.locations, item.options == null ? {} : polylineStyle(item.options));
    try {
        if (item.popup) polyline.bindPopup(L.popup(item.popup.options).setContent(item.popup.html));
        return polyline;
    } catch (error) {
        try { disposePolyline(polyline); }
        catch (cleanupError) { throw new AggregateError([error, cleanupError], 'Polyline creation and cleanup failed'); }
        throw error;
    }
}

function validatePolylineAdditions(items, polylines) {
    if (!Array.isArray(items)) throw new Error('A polyline array is required');
    const ids = new Set();
    for (const item of items) {
        validatePolylineId(item?.id);
        validatePolylineLocations(item.locations);
        if (item.options != null) polylineStyle(item.options);
        if (Object.hasOwn(polylines, item.id) || ids.has(item.id)) throw new Error('Duplicate polyline ID');
        ids.add(item.id);
    }
}

function addPolylineItems(layer, polylines, items) {
    validatePolylineAdditions(items, polylines);
    const created = [];
    try {
        for (const item of items) {
            const polyline = createPolylineInternal(item);
            created.push([item.id, polyline]);
            layer.addLayer(polyline);
            polylines[item.id] = polyline;
        }
    } catch (error) {
        const failures = [error];
        for (const [id, polyline] of created) {
            delete polylines[id];
            try { layer.removeLayer(polyline); disposePolyline(polyline); }
            catch (cleanupError) { failures.push(cleanupError); }
        }
        if (failures.length > 1) throw new AggregateError(failures, 'Polyline addition rollback failed');
        throw error;
    }
}

export function addPolylineGroupLayer(mapId, layerJson, creationKey) {
    const context = requireMapContext(mapId, creationKey);
    const item = JSON.parse(layerJson);
    validatePolylineId(item.id);
    if (Object.keys(window._leafletLayers[mapId]).some(id => id.toLowerCase() === item.id.toLowerCase()))
        throw new Error('A group already uses this ID');
    const items = item.polylines ?? [];
    validatePolylineAdditions(items, Object.create(null));
    const layer = L.featureGroup();
    const polylines = Object.create(null);
    try {
        addPolylineItems(layer, polylines, items);
        if (item.isVisible !== false) layer.addTo(context.map);
        window._leafletLayersControl?.[mapId]?.addOverlay(layer, item.name ?? item.id);
        window._leafletLayers[mapId][item.id] = layer;
        window._leafletPolylines[mapId][item.id] = polylines;
    } catch (error) {
        try {
            window._leafletLayersControl?.[mapId]?.removeLayer(layer);
            context.map.removeLayer(layer);
            for (const polyline of Object.values(polylines)) disposePolyline(polyline);
            layer.clearLayers();
            layer.off?.();
        } catch (cleanupError) { throw new AggregateError([error, cleanupError], 'Polyline group rollback failed'); }
        throw error;
    }
}

export function addPolylinesToLayerAsync(mapId, layerId, itemsJson, creationKey) {
    const { layer, polylines } = polylineGroup(mapId, layerId, creationKey);
    addPolylineItems(layer, polylines, JSON.parse(itemsJson));
}

export function removePolylinesByIdsAsync(mapId, layerId, idsJson, creationKey) {
    const { layer, polylines } = polylineGroup(mapId, layerId, creationKey);
    const ids = JSON.parse(idsJson);
    if (!Array.isArray(ids)) throw new Error('An ID array is required');
    ids.forEach(validatePolylineId);
    for (const id of ids) {
        if (!Object.hasOwn(polylines, id)) continue;
        const polyline = polylines[id];
        layer.removeLayer(polyline);
        disposePolyline(polyline);
        delete polylines[id];
    }
}

export function clearPolylinesLayer(mapId, layerId, creationKey) {
    const { polylines } = polylineGroup(mapId, layerId, creationKey);
    removePolylinesByIdsAsync(mapId, layerId, JSON.stringify(Object.keys(polylines)), creationKey);
}

export function updatePolylinesStylesBulk(mapId, layerId, updatesJson, creationKey) {
    const { polylines } = polylineGroup(mapId, layerId, creationKey);
    const updates = JSON.parse(updatesJson);
    if (!Array.isArray(updates)) throw new Error('An update array is required');
    const ids = new Set();
    const changes = updates.map(update => {
        validatePolylineId(update?.id);
        if (!Object.hasOwn(polylines, update.id)) throw new Error('Polyline was not found');
        if (ids.has(update.id)) throw new Error('Duplicate style update');
        ids.add(update.id);
        const polyline = polylines[update.id];
        const style = polylineStyle(update.options);
        const before = {};
        for (const key of Object.keys(style)) before[key] = polyline.options[key];
        return { polyline, style, before };
    });
    const applied = [];
    try {
        for (const change of changes) {
            applied.push(change);
            change.polyline.setStyle(change.style);
        }
    } catch (error) {
        const failures = [error];
        for (const change of applied.reverse()) {
            try { change.polyline.setStyle(change.before); }
            catch (rollbackError) { failures.push(rollbackError); }
        }
        if (failures.length > 1) throw new AggregateError(failures, 'Polyline style rollback failed');
        throw error;
    }
}

export function updatePolylineLocations(mapId, layerId, polylineId, locationsJson, creationKey) {
    const { polylines } = polylineGroup(mapId, layerId, creationKey);
    const locations = JSON.parse(locationsJson);
    validatePolylineLocations(locations);
    if (!Object.hasOwn(polylines, polylineId)) throw new Error('Polyline was not found');
    const polyline = polylines[polylineId];
    const previous = polyline.getLatLngs();
    try { polyline.setLatLngs(locations); }
    catch (error) {
        try { polyline.setLatLngs(previous); }
        catch (rollbackError) { throw new AggregateError([error, rollbackError], 'Polyline geometry rollback failed'); }
        throw error;
    }
}

export function focusPolyline(mapId, layerId, polylineId, creationKey, optionsJson) {
    const { context, layer, polylines } = polylineGroup(mapId, layerId, creationKey);
    if (!Object.hasOwn(polylines, polylineId)) throw new Error('Polyline was not found');
    if (!context.map.hasLayer(layer)) return;
    const polyline = polylines[polylineId];
    if (optionsJson == null) {
        context.map.fitBounds(polyline.getBounds(), { padding: [50, 50] });
        return;
    }

    const point = polylineMidpoint(polyline.getLatLngs());
    const openPopup = polyline.getPopup() ? () => polyline.openPopup(point) : null;
    focusTarget(context.map, point, JSON.parse(optionsJson), openPopup);
}

function polylineMidpoint(points) {
    const lengths = [];
    let total = 0;
    for (let i = 1; i < points.length; i++) {
        const length = points[i - 1].distanceTo(points[i]);
        lengths.push(length);
        total += length;
    }

    let remaining = total / 2;
    for (let i = 0; i < lengths.length; i++) {
        const length = lengths[i];
        if (length === 0) continue;
        if (remaining <= length) {
            const from = points[i], to = points[i + 1];
            const fraction = remaining / length;
            return L.latLng(from.lat + (to.lat - from.lat) * fraction, from.lng + (to.lng - from.lng) * fraction);
        }
        remaining -= length;
    }
    return points[0];
}

export function removePolylineGroupLayer(mapId, layerId, creationKey) {
    const { context, layer } = polylineGroup(mapId, layerId, creationKey);
    clearPolylinesLayer(mapId, layerId, creationKey);
    window._leafletLayersControl?.[mapId]?.removeLayer(layer);
    context.map.removeLayer(layer);
    layer.off?.();
    delete window._leafletLayers[mapId][layerId];
    delete window._leafletPolylines[mapId][layerId];
}

export function destroyMap(mapId, creationKey) {
    const context = mapContexts.get(mapId);
    if (!context || (creationKey !== undefined && context.creationKey !== creationKey)) return;
    releaseMap(mapId, context);
}

function releaseMap(mapId, context) {
    context.active = false;
    context.dotNetObjRef = null;
    const map = context.map;
    context.map = null;
    const failures = [];
    // A late failure only owns this context, never a replacement with the same ID.
    if (mapContexts.get(mapId) === context) {
        for (const polylines of Object.values(window._leafletPolylines[mapId] ?? {})) {
            for (const polyline of Object.values(polylines)) {
                try { disposePolyline(polyline); }
                catch (error) { failures.push(error); }
            }
        }
        mapContexts.delete(mapId);
        delete window._leafletMaps[mapId];
        delete window._leafletLayers[mapId];
        delete window._leafletMarkers[mapId];
        delete window._leafletPolylines[mapId];
        if (window._leafletLayersControl) delete window._leafletLayersControl[mapId];
    }
    try { map?.remove(); }
    catch (error) { failures.push(error); }
    if (failures.length === 1) throw failures[0];
    if (failures.length > 1) throw new AggregateError(failures, 'Map cleanup failed');
}
