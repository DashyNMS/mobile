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
