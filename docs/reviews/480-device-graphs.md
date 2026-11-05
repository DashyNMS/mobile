# Device Graphs review — #480

Baseline: live main and clean isolated HEAD
`26ee61babe42a406cdd057c81b06d52dec7b567b`, 5 November 2026.
Refs #480 and #442. Implementation self-assessment; independent parent review,
CI and all hardware acceptance remain outstanding. No local build, restore,
test or validation script executed.

## Ownership and source inspection

Read CLAUDE, DEVELOPING, DESIGN, DESIGN-REFERENCES and the reconciled assessment's
#480/#481 and shared recurrence checkpoints. Live #480, #481 and #442 full bodies
and all comments inspected: open, no comments. No open PRs; all-state PR body
search for 480 returned none. Current topic branch had no partial changes.
No other checkout was modified, particularly the protected dirty #475 checkout.

Before editing, established sole-writer #480 ownership of DeviceGraphsPage XAML,
code-behind and DeviceGraphsViewModel. #481 consumes these changes serially;
it retains its original full review obligation. Shared PortGraphs also feeds
GraphPickerViewModel; its existing natural ordering of device graphs and port
interface ordering are preserved. DeviceGraphs display now orders both graph
sources naturally without changing the shared static source or initial Traffic
selection. DESIGN-REFERENCES' stale port-description exception is corrected.
GraphPickerViewModel now explicitly sorts its separate port-source descriptions too,
retaining selected graph identity and save semantics. No shared placement, glass,
activation dispatcher or Core API is changed.

## Actual modes, callers and contracts

AppShell registers a transient pushed DeviceGraphsPage. DeviceDetailViewModel's
Graphs section and graph links, DashboardViewModel graph cards (optional graph,
port and range), and DeviceSectionViewModel Ports (actual IfName) enter this
route. Device detail itself can be pushed or embedded; this graph page is still
pushed, not an embedded ContentView. No pinned root mode exists. Back pops to
the actual caller, including a retained tab stack. Show can switch device-wide
and port targets; a missing port listing retains a direct-port target.

Device-wide sources are ListAsync + ListHealthAsync + best-effort wireless.
Graph API owns SVG, axes, timezone and time bounds. Mobile uses GraphTimeRange
Hour/Day/Week/Month/Year presets and passes the chosen preset unchanged. It does
not infer historical values from current API readings. GraphLegend values are
current readings, not cursor samples. No invented health or severity is added.
GraphHtml/GraphSvgStyle preserve server graph content and native WebView navigation
continues to reject external URLs. Credentials, permissions and storage are unchanged.

Raw SVG cache keys retain device, graph, IfName and range; sensor cache retains
sensor ID. Hidden-series settings keep desktop keys and last-visible protection.
Unknown/mismatched naming keeps the LibreNMS legend. Turning series off uses raw
SVG; a lone sensor can fetch its own scale. There is no refresh, export, share,
compare, More, search or query field on this page. Do not invent those features.
Graph/range selection and retry via the existing selection command retain fetch
semantics. Theme activation now recolours retained raw data without starting a
new graph request; an in-flight request renders using the current palette.

## Concern ledger

C = source compliant; Correction = confirmed defect corrected here;
N/A = absent capability; U = unverified runtime/device outcome.

| Concern | Evidence and outcome |
| --- | --- |
| Retained activation | Correction: IActivePage restores header, watch, idempotent Moved hook, theme subscription and glass without Appearing or fetch. Disappearance disposes watch and unsubscribes the actual Application. Queued placement checks active current page. U actual tab/child return and resume. |
| Secondary action audit | Correction: Show all moves from inline page action to state-cached iOS26 glass beside caller Back. SomeHidden notification updates presence; callbacks guard activation. Android/older iOS use header IconButton, enabled only when there are hidden series. Toggle and Only remain target-specific native buttons. N/A share/export/search/More/compare/fixed primary action. U native guards and activation once. |
| Selection and order | Correction: all three device graph sources, port graph descriptions and Show port labels use NaturalOrder. Traffic remains initial port choice; explicit graph/range links take precedence. GraphPicker's separate port source consumes its existing sorted path. C retained settings and IfName; U actual server payload completeness. |
| Time ranges and long text | Correction: range chips wrap with 44pt minimum height and 64pt minimum width instead of five squeezed equal columns. Accessible names update selection. Pickers have persistent semantic field names. U maximum text and actual picker speech. |
| Legend interaction/readability | Correction: native Toggle/Only buttons provide keyboard/screen-reader alternative to long press; full name/detail/value/state in Toggle description. Values wrap below names rather than competing Auto columns. Hidden text no longer dims to 45% opacity. U both-theme contrast, speech, focus order and single activation. |
| Generation | Correction: null graph selection invalidates older requests; failed single-sensor render cannot overwrite a newer generation's graph with its old fallback. C existing quick-range version guard. U delayed real network handoffs. |
| Geometry | C guide's ScrollView pattern: finite star viewport, top-only iOS native safe area, native Never adjustment, full background and content-only side/bottom padding from CoveredEdges; 20pt initial fade reserve. Side clearance applied once. No fixed action/overlay or list footer. Wide layout keeps graph beside 300pt legend only at 800pt available width. U H/B/V and complete final legend/action reach separately; no numerical native measurements available. |
| Camera/layering | C no horizontal chip scroller or filter panel exists here. Header/content use measured covered edges; do not add blanket camera padding. U actual native clip and resting control clearance compared with Devices/Alerts. |
| Loading/empty/error | C existing busy indicator, error and no-graphs text; permission rejection via GraphImages.ThrowIfDenied. Ports failure has its own message; wireless failure intentionally best-effort. Cache reuse remains. U initial/failure/retry/offline and retained cached data presentation. No new refresh contract. |
| Keyboard/filtering | N/A text search, Command-F, SearchReveal, filter panel and programmatic search scroll. Native picker keyboard/focus and button traversal U. |
| Motion | C no new animation; N/A custom selected-sheet transitions. U Reduce Motion, repeated native picker/route transitions. |
| Persistence/security | C no schema, credentials, permissions, endpoint or settings-key changes. Range remains page state; hidden series remain persisted. U real account permissions and malformed payloads. |

## Core ownership blocker

Pinned package remains 2.0.0-ci.52, provenance Core CI run 52, commit
`5f7bde53ac05ea75f7cb6dd06a8fae62606f0769`. Inspected that revision's actual
GraphLegend.cs through the repository API; no guessed model fields introduced.
ForTraffic sums `IfInOctetsRate ?? 0` / `IfOutOctetsRate ?? 0`, then multiplies by
8. Missing rates therefore become apparent known zero and partial totals are
not labelled incomplete. ForProcessors interpolates UsagePercent without a
finite-number guard. These are genuine upstream-owned honesty defects blocking
full source compliance, not a reason to copy formatting/API logic into mobile.
Core must correct these and supply a reviewed package; parent should track the
upstream follow-up before claiming #480 complete. ForPort converts octets to bits
and nullable/non-finite rate/sensor formatters otherwise use an unavailable dash;
unknown sensor classes retain supplied units. Payload completeness and server
legend/series alignment beyond count matching remain U. Do not reorder drawn
legend series naturally: their order maps the server SVG colours.

## Review and CI-only verification

Added a behavioural regression combining device/health/wireless natural order,
port target natural order, theme redraw and no extra fetch. Updated port-mode
regression to find Errors by identity and require naturally ordered descriptions
while preserving Traffic initial selection. Existing GraphLegend/GraphTarget,
ResourceVisibility, DashboardCards and PortGraphCard coverage remains relevant.
No local tests were run. Shared tests, Android, iOS and security CI are pending.
Full diff against live main and staged whitespace must be checked before commit.
Parent's fresh independent Opus 5.5 Medium full-diff review remains required;
this assessment is not that review. No new DESIGN lesson is labelled reviewed:
these corrections implement existing guide contracts.

## Exact native evidence still required

Every item below is separately UNVERIFIED, regardless of CI result:

- Duo outer, inner portrait, inner landscape; ordinary iPhone; Android and older
  iOS header fallback. Record final SHA/build/OS for each.
- Both themes, maximum text, VoiceOver and TalkBack full names/state, keyboard
  traversal and one activation per Toggle/Only/range/Show all/Back; 44pt targets.
- Cold load, empty source, denied/failure/retry, missing port list/direct link,
  cached/offline, no named legend, many/wrapping legend entries and hidden series.
- Device detail overview and section entry, dashboard device/port/range deep
  links, Ports actual IfName; Back, child return, retained tab and app resume.
- Fold/rotate without reopen, short landscape, theme while hidden/loading,
  repeated quick range changes; keyboard open then closed/native picker dismissal.
- Measure host H, pill bottom B, viewport V and V−B in the same native coordinates
  for side/bottom/top pill separately. Record full background extent, initial
  fade readability and final legend/button reach separately from viewport fit.
  Repeat with wrapped legend, wide layout, no legend, after rotation/fold,
  tab return/resume and keyboard dismissal. Inspect placement-loop diagnostics.

#481 must repeat its full original traffic/packets/errors mode assessment after
integration, including changed natural ordering, controls, lifetime and glass.
No issue closure, epic tick, merge, watch or release is performed by this child.

CI at f9f9135 passed 1,385 tests and found one stale GraphPicker expectation
for the old static port-description order. Its literal expectation now follows
the required natural order; device/port graph identity and save regressions
remain unchanged. No production change follows this CI correction. Replacement
CI and independent review are pending; no local build or test ran.
