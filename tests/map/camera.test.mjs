import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { test } from 'node:test';
import assert from 'node:assert/strict';

// Execute the actual embedded bridge against a synchronous Leaflet camera model.
// Default animated setView deliberately finishes late, reproducing the old race.
const source = readFileSync(new URL('../../src/DashyNMS.Mobile.Shared/Map/MapHtml.cs', import.meta.url), 'utf8');
const bridge = source.slice(source.indexOf('(function () {'), source.indexOf('})();') + 5);
const pins = [0, 1].map(id => ({ id, name: `Site ${id}`, lat: 51 + id / 100, lng: 1, count: 1, ring: [{ count: 1, colour: '#2EA043' }] }));
function harness(data = pins, size = { x: 800, y: 600 }) {
  const events = {}, timers = [], markers = [], fits = [], pending = [], lifecycle = [], views = [];
  const fire = name => { lifecycle.push(name); (events[name] ?? []).slice().forEach(fn => fn()); };
  const zoom = { style: {}, offsetHeight: 90 };
  const controls = { style: {}, querySelector: () => zoom, querySelectorAll: () => [] };
  const container = { clientWidth: size.x, clientHeight: size.y, querySelector: () => controls };
  // Leaflet 1.9.4 getSize caches client geometry; invalidateSize returns before
  // touching that cache when !_loaded. A live-size stub concealed the deadlock.
  let cachedSize = { ...size }, loaded = false, tileViews = 0, layerAttached = false, missingIcons = 0;
  const map = {
    zoom: undefined, offset: [0, 0],
    on(names, fn) { names.split(' ').forEach(name => (events[name] ??= []).push(fn)); },
    whenReady(fn) { if (loaded) fn(); else this.on('load', fn); },
    getZoom() { return this.zoom; }, getSize() { return { ...cachedSize }; },
    getContainer() { return container; },
    setView(centre, zoom, options = {}) {
      assert.ok(Number.isFinite(zoom), 'focus cannot use an undefined zoom');
      const move = () => {
        const first = !loaded, zoomChanged = this.zoom !== zoom;
        loaded = true; // _resetView sets _loaded before events, but layers await load.
        views.push({ centre, zoom });
        fire('viewprereset');
        if (zoomChanged) fire('zoomstart');
        fire('movestart');
        this.centre = centre; this.zoom = zoom; this.offset = [0, 0];
        if (zoomChanged) fire('zoom');
        fire('move');
        if (zoomChanged) fire('zoomend');
        fire('moveend'); fire('viewreset');
        if (first) fire('load');
      };
      if (options.animate === false || this.zoom === undefined) move(); else pending.push(move);
    },
    fitBounds(bounds, options) { fits.push(options); this.setView(bounds[0], 12, { animate: false }); },
    panBy(delta) { fire('movestart'); this.offset = this.offset.map((n, i) => n + delta[i]); },
    stop() { pending.length = 0; },
    invalidateSize() {
      if (!loaded) return;
      const old = cachedSize;
      cachedSize = { x: container.clientWidth, y: container.clientHeight };
      // Keep Leaflet's resize separate from the window resize bridge trigger.
      if (old.x !== cachedSize.x || old.y !== cachedSize.y) { fire('move'); fire('moveend'); fire('mapresize'); }
    },
    latLngToLayerPoint(at) { return { x: at[0], y: at[1] }; }, layerPointToLatLng(at) { return at; },
  };
  const attachMarker = marker => { marker.element = { style: { opacity: String(marker.options.opacity) } }; };
  const layer = {
    addTo() { map.whenReady(() => { layerAttached = true; markers.forEach(attachMarker); }); return layer; },
    clearLayers() { markers.forEach(marker => { marker.element = undefined; }); markers.length = 0; },
    addLayer(marker) { markers.push(marker); if (layerAttached) attachMarker(marker); else { assert.equal(marker.getElement(), undefined); missingIcons++; } },
    eachLayer(fn) { markers.forEach(fn); },
  };
  const L = {
    map: () => map,
    tileLayer: () => ({ addTo(target) {
      assert.equal(target, map);
      const update = () => {
        const size = target.getSize();
        if (size.x > 0 && size.y > 0 && Number.isFinite(target.getZoom())) tileViews++;
      };
      target.whenReady(() => { target.on('viewreset moveend', update); update(); });
    } }),
    layerGroup: () => layer, divIcon: x => x,
    marker: (where, options) => ({ options, events: {}, element: undefined, on(name, fn) { this.events[name] = fn; }, getElement() { return this.element; } }),
    latLngBounds: points => ({ getCenter: () => [points.reduce((n, p) => n + p[0], 0) / points.length, points[0][1]] }),
  };
  const window = { location: {}, addEventListener(name, fn) { events[name] = [fn]; } };
  const document = { getElementById: () => ({ classList: { toggle() {} } }), createElement: () => ({ style: {}, appendChild() {} }) };
  runInNewContext(bridge.replace('{{data}}', JSON.stringify({ pins: data, tiles: '', attribution: '' })).replace('{{ClusterRadius}}', '26').replace('{{PinScheme}}', 'dashynms-map'),
    { L, window, document, setTimeout: fn => timers.push(fn) });
  return { window, map, fits, markers, timers, pending, fire, controls, zoom, container, lifecycle, views, missingIcons: () => missingIcons, tileViews: () => tileViews };
}

test('zero-size document load recovers on native clearance or resize, with pins or empty source', () => {
  for (const data of [pins, []]) {
    for (const trigger of ['native', 'resize']) {
      const h = harness(data, { x: 0, y: 0 });
      h.window.setSelection(data.length ? [0] : [], true, true, true);
      h.window.setMapClearance(60, 20, 100, 20);
      h.timers[0](); // document loaded; layout still absent
      assert.equal(h.map.zoom, undefined);
      assert.equal(h.tileViews(), 0);
      h.container.clientWidth = 800;
      h.container.clientHeight = 600;
      assert.equal(h.map.getSize().y, 0, 'Leaflet cache remains stale before a view');
      if (trigger === 'native') h.window.setMapClearance(60, 20, 100, 20);
      else h.fire('resize');
      assert.equal(h.map.getSize().y, 600);
      assert.equal(h.map.zoom, data.length ? 12 : 2);
      if (data.length) {
        assert.ok(h.missingIcons() > 0, 'first zoomend draws before layer attachment');
        assert.equal(h.map.centre[0], pins[0].lat, 'pending selection focuses its pin');
        assert.equal(h.map.centre[1], pins[0].lng);
        assert.deepEqual(h.map.offset, [(20 - 20) / 2, (100 - 60) / 2]);
      }
      assert.ok(h.tileViews() > 0, 'tile layer receives an initial camera');
      assert.equal(h.markers.length, data.length ? 1 : 0, 'actual bridge draws clustered pins');
      assert.equal(h.zoom.style.visibility, '');
      const fits = h.fits.length;
      h.fire('resize');
      assert.equal(h.fits.length, fits, 'later resize does not refit');
    }
  }
});

test('normal startup fits directly and deferred markers retain initial dimming', () => {
  const h = harness([...pins, { ...pins[0], id: 2, lat: 10 }]);
  h.window.setSelection([0], true, true, false);
  h.window.setMapClearance(60, 20, 100, 20);
  assert.equal(h.views.length, 1, 'no seed world view with current cached dimensions');
  assert.equal(h.views[0].zoom, 12);
  assert.ok(h.missingIcons() > 0);
  assert.ok(h.tileViews() > 0, 'tile activity begins on deferred attachment');
  assert.deepEqual(h.lifecycle.slice(0, 9), ['viewprereset', 'zoomstart', 'movestart', 'zoom', 'move', 'zoomend', 'moveend', 'viewreset', 'load']);
  assert.equal(h.markers.find(marker => marker.pinIds.includes(2)).getElement().style.opacity, '0.25');
  assert.equal(h.markers.find(marker => marker.pinIds.includes(0)).getElement().style.opacity, '1');
});

test('merged zoom and safe offset have one owner, including rapid taps and native choices', () => {
  const h = harness();
  h.window.setMapClearance(60, 30, 20, 10);
  h.markers[0].events.click();
  assert.equal(h.map.zoom, 12, 'click stores a target without starting an animation');
  h.window.setMapClearance(60, 30, 200, 10);
  h.window.setSelection([0, 1], false, true, true);
  assert.equal(h.map.zoom, 14);
  assert.deepEqual(h.map.offset, [10, 70]);
  assert.equal(h.pending.length, 0, 'no competing animation can undo the offset');
  h.markers[0].events.click();
  h.markers[0].events.click();
  h.window.setSelection([0, 1], true, true, true);
  assert.equal(h.map.zoom, 16);
  h.window.setSelection([1], true, true, true);
  assert.equal(h.map.zoom, 16, 'native choice retains the zoom');
  assert.deepEqual(h.map.offset, [10, 70]);
});

test('late first native clearance repairs fallback fit once, without refitting after user movement', () => {
  const h = harness();
  h.timers[0]();
  assert.equal(h.fits.length, 1);
  h.window.setMapClearance(100, 20, 60, 20);
  assert.equal(h.fits.length, 2);
  assert.equal(h.fits[1].animate, false, "repair fit must not start a competing animation");
  assert.equal(h.fits[1].paddingTopLeft[1], 130);
  h.window.setMapClearance(120, 20, 80, 20);
  assert.equal(h.fits.length, 2);
  const moved = harness();
  moved.timers[0]();
  moved.map.panBy([70, 20]); // Unwrapped movement represents a user gesture.
  moved.window.setMapClearance(100, 20, 60, 20);
  assert.equal(moved.fits.length, 1);
  assert.deepEqual(moved.map.offset, [70, 20]);
});

test('early pending selection waits for a valid view; empty sources initialise safely', () => {
  const h = harness();
  h.window.setSelection([1], true, true, true);
  assert.equal(h.map.zoom, undefined);
  h.timers[0]();
  h.window.setMapClearance(60, 30, 200, 10);
  assert.equal(h.map.zoom, 12);
  assert.deepEqual(h.map.offset, [10, 70]);
  const empty = harness([]);
  empty.timers[0]();
  assert.equal(empty.map.zoom, 2);
});

test('closing clears selection before clearance shrink and resize preserves user pan', () => {
  const h = harness();
  h.window.setMapClearance(60, 30, 200, 10);
  h.window.setSelection([1], true, true, true);
  h.map.panBy([90, 40]);
  const offset = [...h.map.offset];
  h.window.setSelection([], true, false, false);
  h.window.setMapClearance(60, 30, 20, 10);
  h.fire('resize');
  assert.deepEqual(h.map.offset, offset);
});

test('single pin taps keep zoom and late clearance preserves an already focused merged zoom', () => {
  const single = harness([pins[0]]);
  single.window.setMapClearance(60, 30, 200, 10);
  single.markers[0].events.click();
  single.window.setSelection([0], true, true, true);
  assert.equal(single.map.zoom, 12);
  const merged = harness();
  merged.timers[0]();
  merged.markers[0].events.click();
  merged.window.setSelection([0, 1], true, true, true);
  assert.equal(merged.map.zoom, 14);
  merged.window.setMapClearance(60, 30, 200, 10);
  assert.equal(merged.map.zoom, 14);
  assert.deepEqual(merged.map.offset, [10, 70]);
});

test('rapid taps on different groups apply the latest group target, not a stale merged zoom', () => {
  const h = harness([...pins, { ...pins[0], id: 2, lat: 10 }]);
  h.window.setMapClearance(60, 30, 200, 10);
  h.markers.find(marker => marker.pinIds.length === 2).events.click();
  h.markers.find(marker => marker.pinIds.length === 1).events.click();
  h.window.setSelection([2], true, true, true);
  assert.equal(h.map.zoom, 12);
  assert.equal(h.map.centre[0], 10);
  assert.deepEqual(h.map.offset, [10, 70]);
  assert.equal(h.pending.length, 0);
});

test('overview padding leaves a usable viewport on short screens with large captions', () => {
  const h = harness(pins, { x: 300, y: 120 });
  h.window.setMapClearance(180, 240, 200, 240);
  const fit = h.fits[0];
  assert.ok(fit.paddingTopLeft[0] + fit.paddingBottomRight[0] <= 225);
  assert.ok(fit.paddingTopLeft[1] + fit.paddingBottomRight[1] <= 90);
  assert.ok(fit.paddingTopLeft[1] > 0);
  assert.ok(fit.paddingBottomRight[1] > 0);
});


test('zoom uses live geometry, respects both overlays and hides when targets cannot fit', () => {
  const h = harness(pins, { x: 800, y: 600 });
  h.container.clientHeight = 180; // Leaflet still reports the old 600px size.
  h.window.setMapClearance(40, 20, 40, 20);
  assert.equal(h.zoom.style.visibility, 'hidden'); // only 80px for 90px controls
  assert.equal(h.controls.style.top, '40px');
  assert.equal(h.controls.style.bottom, '40px');
  h.container.clientHeight = 190;
  h.fire('resize');
  assert.equal(h.zoom.style.visibility, '', 'exact fit restores both 44px targets');
  h.window.setMapClearance(150, 20, 100, 20);
  assert.equal(h.zoom.style.visibility, 'hidden');
  h.window.setSelection([0], true, true, true);
  assert.deepEqual(h.map.offset, [0, -25], 'control fallback leaves camera clearance intact');
  h.container.clientHeight = 400;
  h.fire('resize');
  assert.equal(h.zoom.style.visibility, '');
  assert.deepEqual(h.map.offset, [0, -25], 'resize placement does not move selection');
});
