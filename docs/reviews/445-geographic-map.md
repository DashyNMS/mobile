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
| Accessible alternative to pin tapping | Fix required | Native location chooser uses same pin IDs; native device buttons expose full names and state |
| Counts / unlocated devices | Fix required | Preserve counts; include unknown devices in pin total; explicit no-coordinate state |
| Initial loading / refresh / error / empty | Fix required | One overlay BusyCard, retry through Refresh, no viewport row insertion |
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
pill. Pan/pinch without a camera jump while opening/closing selection. Check
merged pins, identical coordinates, long location/device names, last device row,
VoiceOver/TalkBack chooser and device activation. Exercise empty server, all
unlocated, mixed unlocated, failed initial load and failed refresh then Refresh;
old map must survive failure. Check default/custom HTTP(S) tile settings,
external attribution navigation and Reduce Motion. Native chooser scalability
and very large text on short landscape displays require device verification.

## Implemented contracts and limits

Only MapPage consumes the changed MapHtml/MapViewModel APIs; no shared control
or accepted consumer was edited. NetworkMapPage is unchanged. The selected
location list is always expanded (60% of canvas height, constrained to leave
header/status/zoom space), with an internal virtualised list. There is no
collapsed/expanded toggle to preserve. Entrance/exit uses named motion and
Reduce Motion; selection is cleared after Close finishes. Leaflet continues
to own geographic pan/zoom and clustering; no topology layout is introduced.
Native chooser selection leaves the camera where the user put it.

The location chooser is a native action sheet, so a large location catalogue
requires device accessibility testing. Map dimming around a selected pin is
not implemented: merged geographic pins can split when zoomed, and no stable
single selected marker reference exists in the current bridge. This remains
a guide discrepancy for parent review, rather than claimed device compliance.
