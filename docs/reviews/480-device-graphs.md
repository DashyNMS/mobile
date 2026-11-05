# Device Graphs review — #480

Baseline: live main and clean isolated HEAD
`26ee61babe42a406cdd057c81b06d52dec7b567b`, 5 November 2026.
Refs #480 and #442. Implementation self-assessment; independent parent review,
New-head CI and all hardware acceptance remain outstanding. No local build, restore,
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
| Selection and order | Correction: all three device graph sources, port graph descriptions and Show port labels use NaturalOrder. Traffic remains initial port choice; explicit graph/range links take precedence. GraphPicker's separate port source now sorts descriptions explicitly. C retained settings and IfName; U actual server payload completeness. |
| Time ranges and long text | Correction: range chips wrap with 44pt minimum height and 64pt minimum width instead of five squeezed equal columns. Accessible names update selection. Pickers have persistent semantic field names. U maximum text and actual picker speech. |
| Legend interaction/readability | Correction: native Toggle/Only buttons provide keyboard/screen-reader alternative to long press; full name/detail/value/state in Toggle description. Values wrap below names rather than competing Auto columns. Hidden text no longer dims to 45% opacity. U both-theme contrast, speech, focus order and single activation. |
| Generation | Correction: null graph selection invalidates older requests; failed single-sensor render cannot overwrite a newer generation's graph with its old fallback. C existing quick-range version guard. U delayed real network handoffs. |
| Geometry | C guide's ScrollView pattern: finite star viewport, top-only iOS native safe area, native Never adjustment, full background and content-only side/bottom padding from CoveredEdges; 20pt initial fade reserve. Side clearance applied once. No fixed action/overlay or list footer. Wide layout keeps graph beside 300pt legend only at 800pt available width. U H/B/V and complete final legend/action reach separately; no numerical native measurements available. |
| Camera/layering | C no horizontal chip scroller or filter panel exists here. Header/content use measured covered edges; do not add blanket camera padding. U actual native clip and resting control clearance compared with Devices/Alerts. |
| Loading/empty/error | C existing busy indicator, error and no-graphs text; permission rejection via GraphImages.ThrowIfDenied. Ports failure has its own message; wireless failure intentionally best-effort. Cache reuse remains. U initial/failure/retry/offline and retained cached data presentation. No new refresh contract. |
| Keyboard/filtering | N/A text search, Command-F, SearchReveal, filter panel and programmatic search scroll. Native picker keyboard/focus and button traversal U. |
| Motion | C no new animation; N/A custom selected-sheet transitions. U Reduce Motion, repeated native picker/route transitions. |
| Persistence/security | C no schema, credentials, permissions, endpoint or settings-key changes. Range remains page state; hidden series remain persisted. U real account permissions and malformed payloads. |

## Core dependency integration (current evidence, 5 November)

Core #80's upstream honesty defects are fixed by merged
[Core PR #81](https://github.com/DashyNMS/core/pull/81), merge commit
`8774898a8a3ba3e487fc076722f020ffd19e6981`. Mobile now pins
`DashyNMS.Core` **2.0.0-ci.56** in its sole Core PackageReference,
`src/DashyNMS.Mobile.Shared/DashyNMS.Mobile.Shared.csproj` (previously ci.52).
Before editing, all project PackageReferences were inspected: shared consumes
Core, and the MAUI head/tests consume shared. No other dependency changed.

[Development publication run 56](https://github.com/DashyNMS/core/actions/runs/41949476685)
completed successfully at that merge SHA. Its Pack log explicitly passes
`Version=2.0.0-ci.56` and `RepositoryCommit=8774898a8a3ba3e487fc076722f020ffd19e6981`,
records creation of that exact nupkg, and the publish step records "Your package
was pushed." The summary records the same version and commit. No downloadable
Actions artifacts exist. Direct authenticated nupkg retrieval returned HTTP 403,
so package-embedded nuspec metadata was not independently inspected; provenance
is verified from the successful publication workflow and its explicit pack
metadata, not from a local restore or assumed run stamp.

Core preserves GraphLegendEntry, method signatures, names/count/order, SensorIds
and SVG identity. Values alone now distinguish missing/invalid from zero, label
known traffic subtotals incomplete, reject processor percentages outside 0–100
and non-finite values, normalise signed zero and retain null for unsupported
port series. Existing GraphLegend/GraphTarget/PortGraphCard/DashboardCards and
ResourceVisibility consumer semantics remain unchanged. No mobile formatter
mirror, fetch, API, storage, navigation or permission change is introduced.

New CI-only consumer regressions use DeviceGraphsViewModel.LoadAsync with two
synthetic SVG colour squares and two matching traffic/processor entries. They
assert all-missing dashes, independent partial subtotal/unknown direction,
measured zero, valid bit-rate units, missing/NaN/infinite/out-of-range processor
values and valid 0/42/100 percentages, series identity/order, cropped chart and
accessible values. Existing sensor unit, SensorId and mismatched-count fallback
coverage remains intact. Tests were authored only; no local execution occurred.

The upstream blocker is resolved at source and publication. Full source
completeness is still limited by typed API payloads and count-based SVG matching;
actual server payload completeness and series identity beyond counts remain U.
Every native matrix item below remains U. This dependency integration does not
establish #480 completion or #481 interface-mode acceptance.

## Initial review and CI-only verification (historical)

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

## Historical review corrections (superseded Core status above)

Opus 5.5 Medium reviewed f9f9135 and the test-only d399486 update. Its
Show all availability finding is substantiated: IconButton owns enabled state
through Command.CanExecute, so a separate binding was not reliable. ShowAll
now has SomeHidden CanExecute notifications and the header fallback is hidden
when no series are hidden. Its icon now uses the same eye metaphor as glass.
Shared regressions assert availability before hide, after hide and after restore.
Repeated toggle speech now names the current Show/Hide action and target.

The two large default buttons per legend row were NOT approved. The user
approved the exact compact-row proposal on 5 November ("Approve as proposed").
The replacement implements that proposal; this is not native acceptance. The proposal
retains tap-to-toggle and long press for only, adding a compact 44pt named
Only action and explicit shown/hidden text, without two full-width primary
buttons. Theme redraw exception reporting is corrected below; CI and review remain pending.

Confirmed upstream honesty defects are tracked in
[Core #80](https://github.com/DashyNMS/core/issues/80). A reviewed Core package
and consumer update are necessary before full source compliance. No formatting
logic is copied into mobile. Native acceptance remains separately unverified.

## Historical approved correction ownership (before edits, 5 November)

User approved `/tmp/mobile-442/legend-proposal.html` as proposed. This approves
visual direction only. Sole writer remains this checkout on PR #542.
DeviceGraphsPage and its view model serve device graphs and every #481 interface
mode; GraphPicker retains independent source ordering and persisted identity.
LongPress currently serves Alerts and Devices row gestures. Its Android detector
cannot consume a native Button click; add a Button-only native LongClick path,
leaving those row consumers unchanged. UIKit's long-press recogniser cancels
native touches. ViewModelBase owns failure description/classification and the
Failed diagnostic event; expose that same reporting without RunAsync's busy/reset
side effects for redraws. No Core or shared placement logic belongs here.


## Historical proposal implementation and review disposition

Medium 1 remains fixed: ShowAll CanExecute follows SomeHidden, RaiseSeriesChanged
notifies it, and UpdateShowAllFallback owns header presence with the matching eye
icon. No IsEnabled binding was reintroduced. Existing behavioural assertions cover
before hide, after hide and after restore.

Medium 2 fixed: DeviceGraphsPage has one empty native ToggleButton overlay on
input-transparent, accessibility-excluded facts, then the independent Only sibling.
No parent tap exists. Full labels wrap; ToggleLabel displays Shown/Hidden without
fading or strikethrough. Filled/outlined markers retain Core series colours.
FlexLayout's wrapping row gives facts a proportional basis and lets the measured
Only target move below, with no fixed card height. The empty explicit Button
style avoids primary styling; Only uses the existing LinkButton. The existing
last-visible guard and named actual Hide/Show speech remain intact.
LongPress attaches to ToggleButton: UIKit explicitly cancels native touches;
Android native LongClick consumes release, while Alerts/Devices retain their
original row detectors. Native single activation still requires device evidence.

Low 5 fixed: every RenderAsync caller uses one observed exception boundary.
ViewModelBase.ReportReadFailure reuses Failed, Describe and read-failure
classification without resetting Busy or clearing current errors. Load and redraw
versions suppress stale failure and graph publication. Optional sensor requests
share one in-flight task by the unchanged cache key; theme redraw uses only cache
or an existing request, never initiates a fetch. Expected LibreNMS sensor failures
retain the existing shared-scale fallback. CI-only regression covers current and
superseded HTTP redraw failures, retained graph/error and single sensor request.

No review finding is rejected. Core #80 remains a blocking upstream requirement:
ForTraffic missing rates become zero and ForProcessors lacks a finite guard in
ci.52. All mobile DeviceGraphs device-traffic/processor legends consume it;
#481 interface-mode auditing is still required. Parent must pursue the Core fix
and reviewed package update before claiming source compliance. No mobile mirror,
package bump or claim of upstream correction is included here.

Full original assessment and native matrix above remain applicable. All Duo outer,
inner portrait/landscape, iPhone, both-theme, maximum-text, VoiceOver/TalkBack,
keyboard, fold/rotation, tab/child return and resume outcomes are UNVERIFIED.
H/B/V usable-base geometry and final legend/Only reach require separate evidence.
Fresh parent Opus 5.5 Medium full review and CI judgement remain outstanding.

## Historical review of 14cf333 and corrections

Opus found one medium state-transition bug and five lower findings. LoadGraph
now invalidates redraws, clears raw/entries/hidden/Series and notifies availability
before any await, including null selection. ShowAll guards direct Execute and
the glass tap rechecks CanExecute. The regression holds a new graph request,
attempts the stale action, then fails the load: saved settings stay unchanged
and neither empty series nor obsolete Show all remains visible.

Every waiter on a shared sensor task now applies ThrowIfDenied. A non-API
optional-sensor failure publishes the computed shared-scale SVG for the current
visibility/theme before reporting, while stale results/errors remain ignored.
The revised regression distinguishes current fallback from superseded graph
preservation. Initial-load render errors propagate into RunAsync, preserving
Failed diagnostics; optional redraw errors use ReportReadFailure. Such an error
remains visible until a subsequent full load rather than clearing unrelated
current errors during a redraw.

Only now has the approved quiet border, 10pt radius and 12x8 padding; detail
and Shown/Hidden share one wrapping caption. Accessible names join only present
parts, avoiding stray punctuation. The PR title reflects the final scope.
Full intended diff and whitespace reviewed, no local builds/tests run. Fresh
review and CI are pending. Core #80 and all native outcomes remain unresolved.

Fresh Opus full-diff review at 8f819d0 found no blocking mobile defect and one
low stale-first-load error path. A superseded initial render now suppresses
its error before RunAsync can report it; superseded raw/legend request failures
are likewise ignored by load version. This prevents duplicate diagnostics when
theme redraw shares a pending sensor request. A new regression starts with a
remembered hidden set, supersedes its sensor request with Show all, then fails
that old request: graph/error/Failed diagnostics remain current. Production
correction and whitespace inspected; replacement CI remains pending.
Range-chip wrapping and Android native hold delivery remain exact device checks,
not proven defects. Core #80 and all native acceptance remain unresolved.

CI at 3c02caf found CS0176 in the new regression's Failed-event subscription.
Failed is static; the test now subscribes through ViewModelBase, filters by the
tested sender and removes the listener in finally. Production code is unchanged.
Replacement CI remains pending; no local test or build ran.

## Final integration review and verification

At pre-integration mobile head `7cd9ca8d921f789a8d883e7c46bd1b713a295085`,
GitHub reports shared tests, Android, iOS simulator and CodeQL checks successful.
The prior Opus full review at 8f819d0 found no blocking mobile issue; its low
stale-initial-failure finding was fixed at 3c02caf and its static event test
compile correction at 7cd9ca8. These results predate this package integration.
Core PR #81's full Opus review at 95d7e59 had only the stale test blocker fixed
at 51e4910; the parent negative-zero correction is dfa5326. Cross-platform
net9/net10 CI success at dfa5326 is parent-supplied evidence; the successful
publication workflow independently ran Core tests at the merge commit.

Full intended diff against main 26ee61b and DESIGN inspected; staged whitespace
checked before commit. No local builds, tests, restores or validation scripts.
New-head consumer CI and a fresh parent Opus 5.5 Medium review of the complete
final diff remain required before merge. No watch, merge, closure, epic tick,
release, tag or manual publication is performed here.

Final full-diff Opus 5.5 Medium review at e2999f2 found no blocking defects
and confirmed restored ci.56 in passing consumer CI. Its low recovery finding
is corrected: a successful current redraw clears only its own recorded exception
and classification. Exception identity and displayed-message checks preserve
unrelated/newer failures; full loads reset redraw ownership. The current/stale
redraw regression now asserts recovery clears error and failure text. Added the
Eye icon comment and corrected picker-order wording. Replacement final CI is
pending; every native outcome remains unverified. The upstream dependency is
resolved, not a remaining blocker.
