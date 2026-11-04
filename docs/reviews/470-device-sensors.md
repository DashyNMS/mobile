# Device Sensors review, #470

Refs #470, epic #442. Prepared on main `2c7f32f`. Read live issues,
CLAUDE.md, DEVELOPING.md, full DESIGN.md and DESIGN-REFERENCES.md before
assessment. Routine guide choices were authorised. This is preparation only:
no issue closure, epic checkbox, merge, watch or release.

## Assessment recorded before implementation

The page is DeviceSectionPage, with DeviceSectionViewModel, DeviceSectionLoader
and SectionRow. DeviceDetailView embeds the four worst sensor highlights via
DeviceSectionCard; the full section uses the same loader rows. There is no
sensor editor, graph action, selection, or per-sensor navigation on this page.

| Concern | Baseline finding | Disposition |
| --- | --- | --- |
| All kinds | Compliant: registry categories first, other API classes alphabetically; grouping and description order retained | Preserve |
| Units/readings/limits | Compliant: ReadSensor uses Core GraphLegend, registry thresholds, own limits and API units; missing/nonfinite readings neutral | Preserve |
| State definitions | Compliant: resolved labels and raw value retained, source severity respected; unresolved state never made OK | Preserve; visible Unknown for neutral finite reading |
| Long names/readings, hierarchy | Fix required: shared title tail truncation, 180pt/two-line reading cap; same squeeze in overview | Sensor-only stacked wrapping facts in full list and highlights |
| Severity/accessibility | Fix required: colour alone, no joined sensor description | Explicit OK/Warning/Critical/Inactive/Unknown/Reading unavailable; one joined readout with child labels excluded |
| Search | Fix required: category headings and missing status not searchable | Sensor rows match kind and status in addition to existing fields |
| Search/keyboard behaviour | Fix required in shared baseline: manual search lifecycle | Owned by #469 PR #507, do not duplicate |
| Loading | First BusyCard and finite list compliant; retained rows lack Updating text | Sensor-only Updating status during retained refresh |
| Empty/error | Compliant: empty source differs from Nothing matches; errors suppress empty, failed refresh retains rows | Preserve; Retry supplied by #507 |
| Cached/offline | Compliant: CorePolling shared fleet request, SensorRefreshStatus freshness/error text; no cached data throws | Preserve; hardware offline sequence unverified |
| Glass/insets/reachability | Existing top-only iOS native safe area, native list inset disabled, dynamic side clearance, finite viewport above glass and 24pt footer compliant in source | Hardware unverified; #507 owns fade spacer/placement and lifecycle fixes |
| Threshold return | Settings Thresholds drafts, Save applies, Back discards; no direct Sensors → Thresholds route | Next section load/refresh reads current settings; retained page does not automatically promise new thresholds before refresh |
| Motion | Existing search motion and reduced-motion helpers | #507 owns shared search migration; hardware unverified |

## Modes, callers and preserved contracts

Device detail opens the section with selected device ID, Sensors enum and device
display name. Detail can be reached from Devices (narrow or split), Dashboard,
alerts and other device links. Health OpenSensor passes the sensor's device ID
and opens the whole Sensors section, not a selected sensor. Back remains
relative to the actual caller. Shell route/query handling is unchanged. Search
is local and transient; dismissal retains its query. There are no sensor
settings writes, credential changes, new endpoints or permission assumptions.

Health deliberately covers the four registered categories, with its own counts,
filters and device labels. It consumes ReadSensor rather than SensorRow, so the
new layout flag and search fields do not affect Health. Device Sensors covers
all returned classes for the selected DeviceId. DeviceSectionCard summary totals,
critical/warning counts, worst-four ranking and visibility remain unchanged.
Wireless uses GetWirelessSensorsAsync, WirelessSensorClasses formatting and its
own limits, creates separate rows and never opts into SensorLayout. Resources,
ports and inventory also never opt in. No server capabilities were invented.
Core package, threshold evaluation, limit strings, raw state values and sensor
identities are unchanged. Only synthetic data appears in added tests.

## Exact future integration with PR #507

Read the queued checkout `/home/tpickup/.t3/worktrees/mobile/t3-6f1a2b09`, its
469-device-availability assessment and live PR #507 diff read-only. Do not import
that unmerged branch or modify its PR. Parent updates this branch serially only
after #507 lands and this PR reaches its queue turn.

1. Keep #507's IActivePage Activate, disposable edge watcher, idempotent glass
   subscriptions, ButtonSearch, KeyboardDismiss.OnScroll=False, SearchButton,
   Retry, list-column fade and 20pt spacer. This PR changes no code-behind.
2. In DeviceSectionPage's row template combine the two wrappers into one:
   AvailabilityLayout stack, SensorLayout stack, then legacy grid. Hide the
   legacy grid when either flag is true using two DataTriggers. Do not remove
   either flag or apply the sensor stack to other modes.
3. In DeviceDetailView retain both specialised highlight stacks, and both
   hide triggers on its legacy grid. Preserve #507's availability detail text.
4. Keep #507's serial/generation-guarded refresh. Place this PR's
   IsUpdatingSensors try/finally immediately around its captured-section loader
   await; use the captured section for the sensor check. Do not restore the old
   unguarded assignment from this branch. Preserve generation rejection of old
   errors/results and availability-only row clearing.
5. Keep Updating in the fixed status row; its margin follows ErrorLine, which
   becomes #507's Retry grid. No additional inset owner or fade is introduced.

All shared consumers #469–#479 were assessed at the contract boundary:
Availability retains its percentages/outages; Resources its bars; Ports its
persisted sort/group, filter panel and ifName route; Neighbours graph selection,
reverse links and split pane; VLANs/FDB/ARP their fields and order; Routing its
BGP/OSPF/VRF state; Wireless its endpoint and formatting; Inventory its nesting,
expansion, ports toggle and serial copy. Their default SensorLayout is false,
status/kind fields null and Matches unchanged. Graphs, logs and backups keep
specialised routes. These source findings do not establish device acceptance.

## Validation and device checklist

Full diff and staged diff self-inspection plus git diff --check only. No local
builds, tests or scripts. Added regression tests cover missing/status/category
search, selected-device filtering, custom units and own-limit critical severity.
Existing nonfinite, state definition, threshold and cached polling tests remain
relevant. Shared tests and Android/iOS builds are CI-only and pending. Mandatory
parent Claude Opus 5.5 Medium review waits for quota reset; no review delegate
was started. #501 remains paused. Hardware results below are all unverified.

Tie every result to the final integrated commit and CI build. Use example.net,
DeviceId 7 and edge.example.net, DeviceId 8; synthetic long sensor descriptions.

| Check | Exact remaining steps |
| --- | --- |
| Kinds/readings | Temperature, dBm, signal, fan, custom widgets, positive/negative values, lower/upper limits; compare Core values and category totals before/after search |
| Missing/state | Null/NaN/infinity; known state label plus raw number; label-only/unresolved state; verify neutral unknown/unavailable, never false OK |
| Loading/cache/error | Cold load, successful empty, unmatched term, timeout/denial without cache, cached failure with freshness, refresh success/failure/Retry, slow device 7 then 8; no old rows under new device |
| Search/keyboard | Header/glass/Command-F only; search kind, unit, limit, status and missing reading; typing retains focus; real drag/completion/unfocus dismiss while retaining term; final result reachable with keyboard |
| Themes/text | Light/dark, largest font, long name/reading/unit/state/limit; no clipping, complete final row above glass; check overview worst-four too |
| Accessibility | VoiceOver and TalkBack read each name, kind, reading, limits and status once; no colour dependency; Back/search/Retry accessible and 44pt targets; verify overview card activation remains reachable |
| Duo | Outer portrait, inner portrait, inner landscape; fold/unfold and rotate during search, refresh and long list; final row and search clear side/bottom glass |
| Resume/navigation | Background/resume, Devices narrow/split, Dashboard → detail → Sensors, alert → detail → Sensors, Health → Sensors → Back; one glass owner and correct device |
| Threshold return | Settings → Thresholds Save changed synthetic limit, return to retained Sensors then refresh; new severity agrees with Health; repeat Back-discard and reset-draft without Save |
| Ordinary iPhone | Portrait/landscape, largest text, iOS 26 glass and older header where available; keyboard dismissal, safe areas, Reduce Motion, child return and resume |

No device result is inferred from CI. User acceptance remains required before
closing #470 or ticking #442. Later TestFlight is parent-owned.

## Sequential integration

Updated after merged #507 against main 7d3b36c. SensorLayout and
AvailabilityLayout retain separate full-page and overview templates. Both hide
the legacy row layout independently. The generation-guarded serial refresh path
is retained; Sensors updating status wraps that request and clears in finally.
Shared ButtonSearch, Retry, fade and activation remain owned by the merged
baseline. Sensors stale notes can offer Retry, which intentionally refreshes.
Full-diff Claude review and CI must verify these integration choices.

## Independent review remediation

Claude Opus 5.5 Medium reviewed 36b4445 against main 7d3b36c and confirmed
the shared integration, generation guards and unchanged threshold contracts.
Unregistered sensor classes without any limits now say No limits rather than
OK, without changing shared ReadSensor severity. Missing-reading descriptions
omit dash placeholders. Regression coverage pins both behaviours. Replacement
CI remains pending. No local builds/tests ran; all hardware checks remain open.

## Full reassessment on integrated main, 4 November 2026

Refs #470 and #442. Source base is `74c1891`, merged #530. This section
supersedes the historical geometry-compliant findings and queued-integration
instructions above. Historical #508 review and successful CI remain evidence
for that earlier diff only. #508 squash is
`3adc7fd916297bc99fcf68c8f1fe1b25206665d2`, head
`417946449cc0e70598ae4816af04c4473b0ac0f9`. Its changes are already present;
no stale branch is replayed. #530's independent review/CI do not assess the
whole Sensors scope or establish native geometry acceptance.

Read live #470 and #469 bodies and their entire comment streams, live #442
body/comments, cached `/tmp/mobile-442/issue-470.json`, reconciliation's #470
record and original assessment annex, and the current #469 assessment. The
requested `/tmp/mobile-442issue470` path does not exist; the actual cached
file above and live issue were used. Read CLAUDE, DEVELOPING, DESIGN and
DESIGN-REFERENCES. There is no separate FULLDESIGN file. The user's sole live
#470 comment reports unusable base extent and requires viewport/inset/glass,
fold/rotate, tab return and resume reassessment. That rejection remains open.
No visual redesign is proposed. The two deterministic presentation defects
below follow the existing explicit-severity and neutral-unknown rules.

### Ownership and modes before editing

Only `DeviceSectionLoader.SensorRow` changes production behaviour. Its rows
feed full DeviceSectionPage and DeviceDetailView's embedded Sensors projection
through DeviceSectionCard.Show. Both already opt into SensorLayout. Health
uses ReadSensor directly; Dashboard, SensorPicker and WidgetSnapshot also consume ReadSensor: custom
no-limit readings still appear OK there. This known cross-surface divergence
is tracked in #532; Wireless uses its own projection. ReadSensor, APIs and threshold evaluation
are unchanged. No SectionRow, XAML, lifecycle or geometry helper is changed.

Shared host consumers remain Availability #469, Sensors #470, Resources #471,
Ports #472, Neighbours #473, VLANs #474, FDB #475, ARP #476, Routing #477,
Wireless #478 and Inventory #479, plus EventLog compatibility. Graphs,
Graylog and backups have separate routed pages. Existing Availability,
Resources, Ports, Neighbours, VLAN, FDB, ARP and Routing presentation flags,
Inventory expansion/copy, Wireless formatting and generic rows are untouched.
The dirty #475 worktree and unrelated branches are preserved.

### Source findings and dispositions

Paths are relative to `src/`; symbols identify exact locations at the base.
Compliant below means source inspection only. All native results are unverified.

| Concern / trigger / mode | Location and observed behaviour | Expected / disposition |
| --- | --- | --- |
| All sensor classes, full section | `DashyNMS.Mobile.Shared/DeviceSections/DeviceSectionLoader.cs:SensorsAsync`, lines 132–147, fleet list filtered by DeviceId, registry classes first then other class names, group key/description ordering | Compliant; all returned classes retained, no Health-only restriction |
| Numeric reading/unit/threshold source | Same file `ReadSensor`, lines 195–225, GraphLegend and registry thresholds or own critical/warning bounds; API units for custom classes | Compliant; preserve raw values, signs, units and original limit text; no new evaluation rules |
| Custom numeric class without any limits, both projections | `SensorRow`, lines 163–190, previously said No limits but inherited OK colour from AgainstOwnLimits | Corrected here: neutral RowStatus.None for this unassessed presentation; text remains No limits. ReadSensor's shared legacy result unchanged |
| State definition with no numeric limits, both projections | Same `SensorRow` conditional previously replaced definition severity text with No limits, despite ReadSensor resolving label/severity | Corrected here: No limits applies only without StateDefinition; defined Critical/Warning/OK/Unknown follows resolved severity. Label/raw number retained; unresolved/label-only neutral Unknown |
| Null/NaN/infinity | `ReadSensor` HasReading guard and `SensorRow` first status branch | Compliant: dash, Reading unavailable, neutral. No state resolution or fabricated healthy reading |
| Full names/values/limits, overview and full section | `DashyNMS.Mobile/Pages/DeviceSectionPage.xaml:SensorLayout` lines 171–177; `Views/DeviceDetailView.xaml:SensorLayout` lines 194–200; `SectionRow.SensorDescription` | Compliant wrapping stacks and joined nonblank description, child labels excluded; visual clipping and announcements unverified. Full section heading supplies class; overview description includes class |
| Overview counts/ranking | `DeviceSectionCard.Show`, lines 99–120, Summary and Worst(rows, SensorCount) | Preserved totals and four highlights; counts and highlight ranking are unchanged because OK and neutral have the same rank; only the unassessed colour changes. No new overview actions |
| Loading / retained refresh | `DeviceSectionViewModel.RefreshAsync` lines 256–293; page fixed Updating label line 53 | Compliant initial busy and retained Updating, finally resets; no claim that retained readings are freshly polled |
| Cache/offline/timeout/denial/500, Retry | `DeviceSectionLoader.FleetSensorsAsync`, lines 150–160; `Services/SensorRefreshStatus.cs:Describe/HasNothingToShow`; `CorePolling.PollSensorsAsync` lines 172–186; page ErrorLine lines 48–53 | Compliant cached failure age comes from ObservedAt, no-cache failure throws, busy-disabled Retry refreshes. Successful empty and no matches remain distinct. Denial/error must not imply no sensors or fresh success; runtime sequence unverified |
| Device handoff | `DeviceSectionViewModel.LoadAsync/RefreshAsync`, lines 232–293 | Compliant clears rows on ID/section change, serialises old request, generation rejects stale result/error; prior request may delay handoff, retained known limitation |
| Search and filters | `SectionRow.Matches` line 207 onward, `DeviceSectionViewModel.ApplyFilter`, page constructor/OpenSearch/OnSearchCompleted | Compliant local case-insensitive substring over title/value/subtitle/kind/status, removes empty groups; no severity chips, sorting or filter panel in Sensors. ButtonSearch opens/refocuses via button/glass/Command-F only; no legacy attachment; completion/unfocus/real drag retains query, filtering/programmatic scroll must retain keyboard |
| Definitions/threshold updates | `CorePolling.PollSensorsAsync/ApplyDefinitionsAsync`, lines 172–213; loader reads current settings on every SensorsAsync | Compliant next refresh reprojects saved thresholds/definitions; Activate does not fetch or promise immediate reclassification. Health's DefinitionsVersion handling is separate. Save/discard/reset semantics unchanged; test disabled/source-changed/offline definitions then refresh |
| Routes and returns | `ViewModels/HealthViewModel.cs:OpenSensorAsync` lines 315 onward; `DeviceDetailViewModel.OpenSectionAsync/OpenTileCommand`; `AppShell` DeviceSection registration | Compliant Health opens whole selected device's Sensors. Devices narrow/split, Dashboard and alerts reach detail then overview/card or full section; relative Back retains actual caller and IDs. No per-sensor navigation or invented editor/graph/export |
| Readability/semantics/themes | Both SensorLayout stacks; StatusText converter; PageHeader, IconButton, ErrorLine Retry | Source retains themed readable state text, decorative colour separated from joined facts, wrapping, existing 44pt action controls. Contrast, maximum text, no duplicate headings/readouts and VO/TB operation unverified |
| Persistence/security/source content | Loader read-only, selected DeviceId, Core package and existing route/settings services | Compliant unchanged APIs/Core pin/server content, permissions, credentials, identities, navigation, persistence and exports; no new writes |

### Reported usable-base failure and current #530 dependency

`DeviceSectionPage.xaml` Root has Auto header/search/filter/status rows and a
finite star SectionBody at line 62; Refresh is star row 1, List fills it. There
is no ListCard wrapper to measure or guessed card gutter. The row Borders are
content. Busy and ErrorLine are outside the viewport. Sensors has neither
NeighbourGraph nor FilterCard visible, so their height reserves are N/A.

`DeviceSectionPage.xaml.cs:PlaceControls`, lines 259–365, queries PillIn and
Occupied in the native SectionBody itself. For side/bottom pills it uses
`max(0, H - B)` without safe-bottom clamping. Refresh.Margin owns viewport gap;
ListFoot owns normal 24pt plus bottom glass obstruction. Top-wide pill and
missing measurements use the separate SetupActionPlacement/fallback policy.
This is the source correction for the user's complaint, owned by #469/#530;
it is not proof that actual viewport V reaches B. Side gutters are 16 plus
CoveredEdges once on List and matching fade, not duplicated body padding.

On iOS the page constructor owns only top container safe area; Root,
SectionBody and Refresh opt out; StopInsetAdjustment sets native list Never
on handler/load and placement. Android retains native Container handling.
ListFade is input transparent in the actual list column; the list header
reserves 20pt. Initial heading/final row and native viewport still require
measurements, including empty/search-result states.

Activate restores Back/search ownership through central IActivePage without
requiring Appearing or refresh. Edge watcher, Moved and keyboard observers
are idempotent/disposed, active guards protect queued callbacks, and
ManualKeyboardScroll owns iOS translation. WillChangeFrame converts keyboard
screen to window to SectionBody and reduces the finite viewport; hide restores
placement. Retained activation with keyboard already visible may miss its frame
until another notification. This #530 risk remains unverified for Sensors.
Ports landscape wheel/keyboard can clip its non-scrolling panel even at default
text; that documented #469/#472 regression risk remains open, not accepted or
fixed by this Sensors-only PR. No per-page offset or shared helper edit here.

### Evidence and exact pending acceptance

Source self-review covers the full intended diff against main `74c1891` and
current DESIGN. No local builds, tests, scripts or agents run. Regression tests
added for defined Critical/Warning/OK/Unknown states without numeric limits,
label/raw number/search/accessibility text, unresolved state, and neutral
custom no-limit rows. Existing missing/nonfinite, custom units/limits,
device-generation and polling/cache tests remain relevant. Tests execute only
in CI. Parent independent Opus 5.5 Medium full-diff review and final-head shared
tests, Android/iOS and security CI remain required. Historical review statuses
above are retained and superseded for this reassessment. Build/OS/device
measurements are unknown. No device pass, acceptance or TestFlight is claimed.

For every row below record exact integrated SHA, CI/TestFlight build, model/OS,
theme/text/assistive settings, native page and SectionBody H, pill B, actual
Refresh/List viewport V and V−B, keyboard bounds, first heading and complete
last row bounds. Measure overview card/content separately from full section.
All rows are currently UNVERIFIED.

| Trigger / configuration | Required observed result |
| --- | --- |
| Duo outer portrait/landscape, inner portrait/landscape; ordinary iPhone portrait/landscape | Side/bottom/top/missing pill policy correct, V−B=0 where anchored, one inset owner, complete final row clears actual glass; no unusable strip |
| Light/dark, maximum text, long name/unit/reading/state/limit, short/long list and overview | Complete readable facts, semantic contrast, first heading below fade, no clipping/duplicate headings or false healthy colour |
| iOS 26 glass, older iOS/Android header, VO/TB and Reduce Motion | Back/search/Retry named and at least 44pt, one announcement per fact, retained actual caller, one active glass owner, transitions finish |
| Header/glass/Command-F search; class/status/name/value/unit/limit queries and no matches | Correct groups/results, replacement and programmatic scroll keep focus; actual drag, completion and unfocus close while retaining query |
| Keyboard open/close, fold/rotate while typing and loading | Header fixed, list finite with final row reachable, measured base restored on close; no double translation |
| Retained tab return, child/caller return and background/resume with keyboard already open | Controls and keyboard clearance restored without refresh/Appearing; correct device/query, no extra observers or glass owner |
| Cold load, genuine source-empty, no matches, cached offline/timeout/500, denied/no-cache, Retry success/failure | Honest loading/error/age, never false freshness or empty success; last known reading distinguishable from new reading |
| Device 7 slow then 8, Health → Sensors → Back; Devices narrow/split, Dashboard and alert routes | No old result/error under new title, correct relative Back and selected ID; full section and overview agree |
| Null/NaN/infinity, positive/negative threshold edges including warning, known/unresolved/label-only states, custom no-limit class | Correct existing evaluation, full raw values/units and labels, unknown/missing/unassessed neutral, textual severity agrees with colour |
| Save changed thresholds then refresh; discard/reset draft; definitions update/disable/source change/offline then refresh | Uses actual saved source/settings, no fabricated immediate update, source state preserved and no false OK |

#470 remains open and #442 unchecked pending explicit user device acceptance.
No merge, release, watch, issue edit or unrelated work is authorised here.

### Independent review disposition

Opus 5.5 Medium full-diff review at 5fb2259 found no blockers and confirmed
the two scoped presentation fixes; both shared CI test runs passed at that
head. Parent fixed documentation of ranking (OK and neutral rank equally)
and explicitly recorded the ReadSensor consumer divergence in follow-up #532.
No shared classification change is silently included in #470.

Existing numeric fallback when state definitions are disabled can still
classify a state reading with a reported high limit as OK. Warning-only
custom limits can yield OK/Warning without a limits line because limit text
uses low/high critical bounds. Verify both cases, and compare defined versus
disabled definitions without assuming new semantics. Nonfinite-limit null
normalisation was traced in sibling Core source, not the pinned ci.52 binary;
package-specific verification remains pending.

No production code changed after review. Final documentation-head CI pending;
physical geometry, full loading/cache/search/threshold-return operation and
all original #470 acceptance remain unverified. Independent review did not
retrace those unchanged paths; their source assessment is not a device pass.
