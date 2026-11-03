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
  const events = {}, timers = [], markers = [], fits = [], pending = [];
  const fire = name => (events[name] ?? []).forEach(fn => fn());
  const controls = { style: {}, querySelector: () => ({ offsetHeight: 90 }), querySelectorAll: () => [] };
  const map = {
    zoom: undefined, offset: [0, 0],
    on(names, fn) { names.split(' ').forEach(name => (events[name] ??= []).push(fn)); },
    getZoom() { return this.zoom; }, getSize() { return size; },
    getContainer() { return { querySelector: () => controls }; },
    setView(centre, zoom, options = {}) {
      assert.ok(Number.isFinite(zoom), 'focus cannot use an undefined zoom');
      const move = () => { fire('zoomstart'); fire('movestart'); this.centre = centre; this.zoom = zoom; this.offset = [0, 0]; fire('zoomend'); };
      if (options.animate === false || this.zoom === undefined) move(); else pending.push(move);
    },
    fitBounds(bounds, options) { fits.push(options); this.setView(bounds[0], 12, { animate: false }); },
    panBy(delta) { fire('movestart'); this.offset = this.offset.map((n, i) => n + delta[i]); },
    stop() { pending.length = 0; }, invalidateSize() {},
    latLngToLayerPoint(at) { return { x: at[0], y: at[1] }; }, layerPointToLatLng(at) { return at; },
  };
  const layer = { addTo: () => layer, clearLayers() { markers.length = 0; }, addLayer(marker) { markers.push(marker); }, eachLayer(fn) { markers.forEach(fn); } };
  const L = {
    map: () => map, tileLayer: () => ({ addTo() {} }), layerGroup: () => layer, divIcon: x => x,
    marker: () => ({ events: {}, element: { style: {} }, on(name, fn) { this.events[name] = fn; }, getElement() { return this.element; } }),
    latLngBounds: points => ({ getCenter: () => [points.reduce((n, p) => n + p[0], 0) / points.length, points[0][1]] }),
  };
  const window = { location: {}, addEventListener(name, fn) { events[name] = [fn]; } };
  const document = { getElementById: () => ({ classList: { toggle() {} } }), createElement: () => ({ style: {}, appendChild() {} }) };
  runInNewContext(bridge.replace('{{data}}', JSON.stringify({ pins: data, tiles: '', attribution: '' })).replace('{{ClusterRadius}}', '26').replace('{{PinScheme}}', 'dashynms-map'),
    { L, window, document, setTimeout: fn => timers.push(fn) });
  return { window, map, fits, markers, timers, pending, fire, controls };
}

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


test('short canvas keeps both zoom targets inside viewport without changing camera clearance', () => {
  const h = harness(pins, { x: 800, y: 180 });
  h.window.setMapClearance(150, 20, 100, 20);
  assert.equal(h.controls.style.top, '70px');
  assert.equal(h.controls.style.bottom, '100px');
  h.window.setSelection([0], true, true, true);
  assert.deepEqual(h.map.offset, [0, -25], 'camera retains the actual overlay clearance');
  h.window.setMapClearance(40, 20, 10, 20);
  assert.equal(h.controls.style.top, '40px');
});
