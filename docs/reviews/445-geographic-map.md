# Geographic map review — #445

Baseline: main at 185216035c96733009e5aae6f7c3770907b47479 (#467).
Read live #445, #442 and #288, CLAUDE.md, developing/design guides and
DESIGN-REFERENCES.md; inspected NetworkMapPage before selecting this approach.

## Scope assessment recorded before implementation

| Concern / mode | Assessment | Resolution |
| --- | --- | --- |
| Pinned Map tab; unpinned More → Map → Back | Fix required | One glass owner, pushed-only Back; retain header Back elsewhere |
| Devices routes | Compliant in inspected code | No current direct Map caller found; Map → Show in Devices passes exact location; row opens device ID; retain navigation service routing |
| Canvas / zoom / pan / selection | Fix required | Keep WebView geometry stable beneath overlays; relocate Leaflet controls and attribution into unobscured area |
| Single pin / merged cluster / selected devices | Fix required | Wrapping heading/actions and device text, bounded scrolling card; preserve single-location-only Devices action |
| Narrow / large text / folding | Fix required | Recompute card height and control clearance on size/safe-area/glass changes |
| Accessible alternative to pin tapping | Fix required | Native location chooser uses same pin IDs; whole device rows expose full names, details and state |
| Counts / unlocated devices | Fix required | Preserve counts; include unknown devices in pin total and explicit grey ring segment; explicit no-coordinate state |
| Initial loading / refresh / error / empty | Fix required | One overlay BusyCard, visible Retry on error, no viewport row insertion |
| Tile settings / attribution / security | Compliant; geometry needs fix | Preserve bundled Leaflet, nonce CSP, JSON escaping, bridge allowlist, external browser, UA/Referer and custom tile setting |
| Persistence / permissions | Compliant | Read-only map; no new storage, permission or credential flow; refresh retains selection by location name |
| Search / keyboard | Compliant | No Map search or input exists; no new search contract |
| Motion / themes / screen reader / camera / resume | Unverified on device | Follow shared geometry; exact checks below |

#288 has no comments or newer acceptance evidence on the live issue. Its
mock-up and global light-theme work remains open; this page implementation
uses the guide-authorised NetworkMap reference, and does not complete #288.

## Evidence

Code: implementation and full diff inspected by implementation agent; no independent
review claimed. Parent arranges mandatory Claude Opus 5.5 Medium full-diff review.
CI: pending PR shared tests and Android/iOS builds. No local builds/tests executed.
Device: all outcomes unverified; no screenshots/build acceptance claimed.

## Remaining device steps

On Duo outer, inner portrait and inner landscape, then an ordinary iPhone,
Android and older iOS: test light/dark and largest text; open pinned and
More-pushed Map, return from device detail and Show in Devices, switch tabs,
resume, rotate/fold repeatedly. Confirm exactly one glass owner and pushed-only
Back, header Back fallback, no placement-loop diagnostics, camera clearance,
reachable zoom and attribution links above the selected sheet and bottom/side
pill. Pan/pinch, then select a pin or chooser location and verify it centres above the card; closing must not flash. Check
merged pins, identical coordinates, long location/device names, last device row,
VoiceOver/TalkBack chooser and device activation. Exercise empty server, all
unlocated, mixed unlocated, failed initial load and failed refresh then Refresh;
old map must survive failure. Check default/custom HTTP(S) tile settings,
external attribution navigation and Reduce Motion. Native chooser scalability
and very large text on short landscape displays require device verification.

## Implemented contracts and limits

Only MapPage consumes the changed MapHtml/MapViewModel APIs; no shared control
or accepted consumer was edited. NetworkMapPage is unchanged. Cards use a finite
list viewport capped at 200pt below 740pt canvas height, otherwise 300pt, further
limited by available space. Entrance and exit use named motion and Reduce Motion;
Close hides the card before resetting transforms and clearing selection. Switching
selection keeps a stationary card in place.

The bridge retains selected IDs through card exit and dims nonselected marker
groups over 250ms (immediate with Reduce Motion). Redrawn clusters use those IDs.
Initial bounds use header/status/glass padding; selected locations use an offset
camera centre above the card. Clearances are reapplied after document navigation
and resize. Leaflet retains geographic clustering, tiles and attribution.

The chooser uses natural location order and device-count units, and is disabled
in the header or omitted from glass while busy or without pins. Glass is rebuilt
only when chooser availability changes. A native action sheet still needs long-name
and large-catalogue device checks. Header/status use page-background surfaces and
a 20pt fade. Zoom targets are 44px, correcting Leaflet's 30px touch default.

## Review fixes after 83e8ed4

All six medium and six low findings were confirmed and addressed. There is no
accepted deferral of selected-pin dimming. M1/M2 cover the bridge selection and
camera changes; M3 adds an explicit unknown ring share; M4/L1 fix card switching
and exit; M5 makes each wrapping device row a single accessible tap target; M6
adds opaque header/status surfaces. L2 caps the card; L3 orders and gates choices;
L4 adds conditional captions and Retry while retaining stale data; L5 adds
behavioural tests for unknown membership/ring data, empty/unlocated/loading/error
states, retained locations and property notifications; L6 corrects route and
counts evidence and enlarges zoom targets.

Tests were added but not run locally, per instruction. This implementation pass
inspected the full PR diff and whitespace; it does not replace the parent's final
Claude Opus 5.5 Medium full-diff review. CI and all device outcomes remain pending.
In particular, verify Navigated-to-clearance ordering after refresh, native glass
versus Leaflet controls, huge text on short landscape screens, chooser long names
and large catalogues, Duo orientations/folding/rotation/resume and both themes.
