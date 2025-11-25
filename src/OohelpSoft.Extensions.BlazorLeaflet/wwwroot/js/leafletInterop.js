// ----------------------------
//  Global storages
// ----------------------------
window._leafletMaps = window._leafletMaps || {};
window._leafletLayers = window._leafletLayers || {};     // mapId → { layerName → LayerGroup }
window._leafletMarkers = window._leafletMarkers || {};    // mapId → { layerName → { id → marker } }

let _dotNetObjRef = null;

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

export async function createMap(id, optionsJson, dotNetObjRef) {

    await ensureLeafletLoaded();

    _dotNetObjRef = dotNetObjRef;

    const options = JSON.parse(optionsJson);
    const map = L.map(id, options);

    if (!options.tileLayers || options.tileLayers.length === 0) {
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
    }
    else {
        for (const l of options.tileLayers) {
            const tl = createTileLayerInternal(l);
            tl.addTo(map);
        }
    }

    }).addTo(map);

    window._leafletMaps[id] = map;

    // Prepare storages
    window._leafletLayers[id] = window._leafletLayers[id] || {};
    window._leafletMarkers[id] = window._leafletMarkers[id] || {};

    console.log("leaflet Map created", id);
    _dotNetObjRef.invokeMethodAsync('OnJsMapCreated');
    return true;
}

// ----------------------------
//  Create LayerGroup
// ----------------------------
export async function createFeatureGroupAsync(mapId, layerName) {
    const map = window._leafletMaps?.[mapId];
    if (!map) return;

    if (!window._leafletLayers[mapId][layerName]) {
        const layer = L.featureGroup().addTo(map);

        window._leafletLayers[mapId][layerName] = layer;
        window._leafletMarkers[mapId][layerName] = {};

        console.log(`Layer created: ${layerName}`);
    }
}

export async function createMarkerClusterLayerAsync(mapId, layerName, optionsJson) {
    const map = window._leafletMaps?.[mapId];
    if (!map) return;

    if (!window._leafletLayers[mapId][layerName]) {
        const o = JSON.parse(optionsJson);
        const layer = L.markerClusterGroup(o).addTo(map);

        window._leafletLayers[mapId][layerName] = layer;
        window._leafletMarkers[mapId][layerName] = {};

        console.log(`ClusterLayer created: ${layerName}`);
    }
}





// ----------------------------
//  Add ONE marker to layer
// ----------------------------
export async function addMarkerToLayerAsync(mapId, layerName, markerJson) {
    const map = window._leafletMaps?.[mapId];
    if (!map) return;

    const layer = window._leafletLayers[mapId]?.[layerName];
    if (!layer) return;

    const markersDict = window._leafletMarkers[mapId][layerName];

    const m = JSON.parse(markerJson);
    const marker = createMarkerInternal(m);

    layer.addLayer(marker);
    markersDict[m.id] = marker;
}

// ----------------------------
//  Add MANY markers to layer
// ----------------------------
export async function addMarkersToLayerAsync(mapId, layerName, markersJson) {
    const map = window._leafletMaps?.[mapId];
    if (!map) return;

    const layer = window._leafletLayers[mapId]?.[layerName];
    if (!layer) return;

    const markersDict = window._leafletMarkers[mapId][layerName];
    const items = JSON.parse(markersJson);

    for (const m of items) {
        const marker = createMarkerInternal(m);
        layer.addLayer(marker);
        markersDict[m.id] = marker;
    }
}

// ----------------------------
//  Remove ONE marker by ID
// ----------------------------
export async function removeMarkerByIdAsync(mapId, layerName, id) {
    const layer = window._leafletLayers[mapId]?.[layerName];
    if (!layer) return;

    const markersDict = window._leafletMarkers[mapId]?.[layerName];
    if (!markersDict) return;

    const marker = markersDict[id];
    if (marker) {
        layer.removeLayer(marker);
        delete markersDict[id];
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
    console.log("first marker:", markers[0]);
    console.log("first marker location:", markers[0].location)
    if (!map) return;

    for (const m of markers) {
        const marker = createMarkerInternal(m);
        marker.addTo(map);
    }
}
function createMarkerInternal(m) {
    let icon = undefined;

    console.log(m);

    if (m.icon) {
        icon = L.icon(m.icon);
    }
    else {
        ico = L.icon.default
    }

    const marker = L.marker(m.location, { icon });

    if (m.popup) {
        marker.bindPopup(m.popup.html, m.popup.options);
    }

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


export async function fitBoundsToLayerGroups(mapId, layerGroupIds) {
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

