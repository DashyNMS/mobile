# Geographic map loading correction

Baseline: main `aaf2898452a19c58cb57fb61a94749f9f6b4c026`.
The sequential update after #489, #491, #492 and #493 is complete.
Refs #445 and #442; hardware acceptance remains outstanding.

## Assessment and code cause

Read CLAUDE.md, docs/DEVELOPING.md, full docs/DESIGN.md and
DESIGN-REFERENCES.md, #442/#445, #487 changes and the complete
445-geographic-map review/follow-ups. There is no FULLDESIGN.md in this checkout;
docs/DESIGN.md is the authoritative full design guide. NetworkMap's canvas
reference was inspected; its renderer is separate and unchanged.

MapPage refresh obtains devices, locations and bundled MapAssets, then assigns
MapHtml.Build to HtmlWebViewSource with the app base URL. Navigated replays
clearance/selection; early evaluations may be lost and the 250ms fallback retries.
The navigation allowlist permits that document, intercepts pin IDs and opens
external HTTP(S) attribution natively. No evidence in these paths requires a
navigation, CSP nonce, JSON, tile-template or native-handler change.

The #468 draw exception is the first failure path for any populated map.
The first setView emits zoomend before load. The layer group awaits load via
whenReady, so draw adds markers without icons and getElement returns undefined.
The unguarded style assignment throws before load can attach the tile layer,
leaving a blank map even with valid dimensions. Marker opacity is now supplied
as an option so Leaflet applies initial dimming when it creates each icon.

A second failure path is stale zero geometry: MapHtml.initialise checks map.getSize before supplying any
view. Bundled Leaflet 1.9.4 getSize caches clientWidth/clientHeight, including
zero; its invalidateSize starts with `if(!this._loaded)return this`. A document
created before native layout can therefore cache zero. Later native clearance
and window resize keep reading zero: resized calls invalidateSize but it cannot
refresh the cache before the first view. ready never becomes true, fit/draw never
run, and the attached tile layer waits for a view. The themed HTML background
remains. This is a source-proven failure path, not a claim that device observation
has established which lifecycle event occurred on the reporting phone.

Fix: gate first initialisation on live container width AND height. Only when
cached dimensions differ from live dimensions, establish a synchronous seed
view and invalidate cached geometry with pan disabled. Then fit
all source pins (or the existing empty-source world view). Pending selection
focus follows that final fit. Subsequent resizing retains the existing camera
policy. Normal startup fits directly without requesting seed world tiles.
The stale-size invalidation emits move/moveend and resize when size changes.
These synchronous events remain inside programmatic and before ready, so they
cannot mark user movement or consume late-clearance repair. No bridge moveend
handler adds another fit. No spacer or shared native control changes are involved.

## Scope and preserved contracts

| Path / concern | Assessment and resulting behaviour |
| --- | --- |
| Fresh pinned tab / More-pushed page | Same document startup; live geometry breaks the zero-cache cycle; native Back/glass unchanged |
| Retained tab / child return / resume | Existing clearance replay retained; initialisation remains one-time per document |
| Refresh | New document starts independently; Navigated selection replay retained; failed fetch preserves old map |
| No devices / all unlocated | World view and tiles start; native empty/unlocated text unchanged |
| Normal / coincident / merged pins | Full source bounds and existing clustering/rings drawn after valid camera |
| Selection / picker / close | Pending focus, keyed merged zoom, dimming and selection versions unchanged |
| Rotation / folding / short canvas | Live startup geometry; later invalidate/placement; 44px zoom visible when it fits, hidden with pinch fallback otherwise |
| Security / read-only / attribution | Nonce CSP, escaped JSON/textContent, bundled Leaflet, server tile setting, UA/Referer and external navigation unchanged |
| Layout / colour / input | Existing HTML 100% height and overflow, native WebView full grid, opaque header/status only, invisible measuring view, marker-only opacity and native inset/scroll/bounce settings retained |
| Search / persistence / other maps | No changed consumers, storage, permissions, shared native controls or search behaviour |

Inspected bridge APIs include map container/size, events, camera/stop/pan,
invalidation, layer grouping, projection, markers/elements and bounds. The
review identified the pre-load undefined marker reference described above. Controls exist after
L.map; document map exists before bridge execution. This does not establish
real rendering/clip correctness on hardware.

## Regression and validation limits

The CI Node test executes the actual embedded bridge in vm (so syntax/runtime
throws fail), substituting JSON/constants. It is not a browser and does not
execute bundled Leaflet or CSS. The previous model supplied always-live sizes
and a no-op invalidateSize, concealing the zero-cache failure. It also always
provided marker elements, hiding the draw exception. The corrected model caches
geometry and implements Leaflet's pre-view invalidation refusal. Tile and group
attachment defer through whenReady until load, and markers have no element until
attached. Bundled _resetView sets _loaded first, then emits viewprereset,
zoomstart, movestart, zoom, move, zoomend, moveend, viewreset, and first-load load.
Tile activity is counted only after attachment. This remains a focused model,
not an implementation of Leaflet's complete subsequent camera lifecycle.
New cases start
with zero width/height, run native clearance and document fallback before layout,
then deliver positive geometry via native clearance or resize. They require a
valid initial camera, tile-camera activity, pin drawing or empty world view,
visible fitting zoom, pending focus centre and [0, 20] offset, and no repeated fit on resize.
Normal startup also exercises missing pre-load icons, deferred initial dimming
and direct fit without a seed view.
Existing camera/selection/oversized-padding/live-control tests remain in CI.
The original guard would never establish a camera in these new cases.

No local builds, tests, scripts, browser, previews or servers were run. Full diff
and whitespace inspection are implementation self-inspection only. CI shared/Node,
Android, iOS and security outcomes for the revised head remain pending. The
quota-return Claude Opus 5.5 Medium review completed and found the draw exception,
lifecycle model gap and documentation/test improvements addressed in this revision.
A fresh parent Claude review of this revised head is required.

## Remaining device matrix and delivery gate

All hardware remains unverified. On Duo outer, inner portrait and inner
landscape, ordinary iPhone, Android and older iOS: cold launch pinned Map and
More-pushed Map, switch tabs, return from picker/device, resume, refresh with
and without selection, no pins/all-unlocated/normal/merged pins, fold/rotate
before and after initial load. Check real tiles and markers rather than only
background/controls; both themes, huge text, camera/side/bottom glass clearance,
44px zoom exact/insufficient fit and recovery, pan/pinch with native scrolling
disabled, selection dimming/close, attribution and VoiceOver/TalkBack. Check
failed initial fetch/retry and failed refresh retained data; custom HTTP(S) tiles
and network tile failures separately from camera startup.

The serial branch update is complete. Parent must obtain final
CI and a fresh Claude review before authorised merge/cleanup and Neighbourhoods
TestFlight. No merge, release, issue closure or epic ticks in this pass.
