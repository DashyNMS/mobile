# Current reassessment, 4 November 2026

Refs #473 and #442. Baseline HEAD and fetched origin/main are
`476ba157faf577ac2d7e791079cf3749f66f3e05`, including Ports #534.
This section supersedes the historical assessment below, especially its
"viewport above bottom glass plus gap" compliant claim. That claim did not
establish usable-base extent and is rejected by the user's device feedback.
The original assessment and integration/review history remain below for provenance.

Read live complete #473 and #442 bodies/comments, merged #511/#530/#534 PR
records, repository CLAUDE.md, DEVELOPING.md, DESIGN.md and DESIGN-REFERENCES.md,
and the initial reconciled assessment's shared rules and #473 entry. No open
PR was present. Existing neighbour branches are historical work, not a reason
to replay pre-integration changes. This checkout was clean on the prepared
reassessment branch. Neither the dirty #475 checkout nor the Ports checkout
was changed. Parent owns independent Opus 5.5 Medium review, CI judgement and merge.

## Live user comment disposition

The only live #473 comment is [the 4 November rejection](https://github.com/DashyNMS/mobile/issues/473#issuecomment-6619307234).

| Trigger and mode | Observed / expected | Code and disposition |
| --- | --- | --- |
| Enter Device Neighbours on reporting display | User reports section stops short of usable base; expected finite viewport reaches measured usable base and final content clears glass | `DeviceSectionPage.PlaceControls`, `SectionBody`, `Refresh`, `ListFoot`. #530 merged as `74c1891` already measures pill in native body coordinates, aligns list base and puts obstruction in footer. Preserve it. Report remains UNVERIFIED on corrected build, not resolved by this PR. No guessed offset. |
| Inside/outside displays, fold/rotate | User requests checking inherited safe areas and actual content viewport | iOS page owns top only, Root/body/Refresh opt out, native list adjustment Never. CoveredEdges supplies sides; footer supplies final content reach. Android keeps Container fallback. Native width/bottom cause remains unproven; measure page, host and viewport separately. |
| Tab return and resume, all #469–#474 and #475–#479 consumers | Expected current geometry and one glass owner without reopen/Refresh | Existing IActivePage activation, disposable edge watch, idempotent Moved and retained Window.SizeChanged subscription from #534 preserved. Pending native checks include delayed native attachment and keyboard already visible at activation. |

## Complete source dispositions and correction

C means source contract retained, F means a confirmed correction in this PR,
U means unverified native result, N/A means an absent capability. No C is a
physical acceptance claim. Every native result is U.

| Concern and trigger | Source evidence / expected behaviour / disposition |
| --- | --- |
| Overview | C: `DeviceDetailView` uses NeighbourSummary within the section-opening card; DeviceSectionCard retains five highlights and totals. No nested device action added. Long names, all facts, empty/error overview and largest-text fit U. |
| Own/reverse discovery, both ends | C: loader concurrently fetches own/fleet links and optional ports, delegates matching/deduplication to pinned Core DeviceNeighbours.Build, resolves reverse names with configured name style. Local-to-remote subtitle and actual positive remote ID remain intact. No source matching changes. |
| Monitored/unmonitored routes | C: loader admits only positive LinkDeviceId, row opens relative DeviceDetail, graph selects card only even when unmonitored. Devices narrow/split, Dashboard, alert and recursive device entries retain caller Back via existing Shell routes. Network link detail #452 and groups/editor #450/#451 are separate ownership and unchanged. Actual speech/activation once U. |
| Native graph actions | F: centre was an inert Button; now an identically bounded readout with current-device description. Neighbour buttons previously announced only name/subtitle/first field, omitting protocol/platform/state/readings. Now use complete NeighbourSummary with Show neighbour card, never Open neighbouring device. Native role/focus U. |
| Narrow graph dimensions | C existing policy: finite parent body, graph height min(280, 35% of body height), internal vertical ScrollView, measured name lanes at least 60pt, content height at least 240pt. Remaining list uses star row. U actual AbsoluteLayout scroll extent, long-name fit, short body and keyboard-open list availability. No geometry redesign approved or implemented. |
| Wide graph dimensions | C existing policy: at 800pt page width after sides, graph spans both rows beside finite list; graph margin uses overlayBottom, list uses pill-base gap/footer. U actual pane width versus page width, three node lanes, widest names, resize/scroll reach, split transition, live font/theme changes without rebuilding. Native measurements required before altering policy. |
| Node/list selection and search | C: filtered Groups feed graph; Picked assigns same row and ScrollTo(group, Start), animation obeys ReducedMotion. Row gesture opens device. ButtonSearch checks native drag and list disables KeyboardDismiss.OnScroll; filtering/programmatic scroll must retain focus/query. U native selected outline, selection removed by filtering and refresh, focus after node tap, graph drag versus list drag. |
| Unknown and inactive states | C: only explicit local up/down classified; absent/testing unknown neutral. Inactive discovery has precedence. Complete textual local state, speed, in/out bits and Seen by retained; invalid/negative/nonfinite/overflow traffic unknown, zero valid. No inferred remote health. F: complete node speech includes Local port state: Not active, matching the inactive border. Native speech delivery and visual contrast U; no new health fact invented. |
| All data states | C: first-load busy, empty source distinct from no match, error suppresses empty, Retry and refresh use serial generation guard. Neighbours retain old rows on refresh/failure, so readings are not fresh-health evidence. Optional non-auth port failure permits unknown readings; required links failure stays error. Offline/denied/timeout, refresh failure and slow device switch U on native. |
| Text, hierarchy and themes | C: full wrapping neighbour summary and graph node names; no source truncation added. F: centre keeps the previous semibold font and measurement uses the same font. The confirmed stale live-theme graph colours now rebuild through a paired Loaded/Unloaded theme subscription, including lines and unknown borders. U native contrast, maximum text, live font remeasurement and minimum hit areas. |
| Glass, fade and insets | C: list-column fade and 20pt header, finite Refresh viewport and footer, wide graph independent clearance. #530 and #534 remain dependencies, not acceptance. U native body/pane inheritance, graph clipping, base extent and final row reach separately. |
| Retained activation and motion | C: Activate restores glass/header/watch, graph snapshot and placement; disappearance removes window/edge/keyboard observers. Existing ReducedMotion search/scroll policy retained. U repeated activation, child return, background/resume and fold during load/search. |
| Filters, persistence, security | C: transient query, no neighbour sort/group panel, server facts/ordering, naming preferences, permissions, authentication, credentials and package unchanged. N/A draft Save, export, comparison and server writes. No new storage, API or health rules. |

## Consumers, dependencies and recurrence checkpoints

Production changes are confined to DeviceNeighbourGraph and a presentation-only
SectionRow.NeighbourGraphDescription consumed there. Shared tests exercise the
complete announced facts and selection intent for monitored/unmonitored rows.
No test runs locally. Native centre role requires platform verification.
No shared page placement, loader, overview, route or lifecycle implementation changes.

Shared DeviceSectionPage consumers remain Availability, Sensors, Resources,
Ports, Neighbours, VLANs, FDB, ARP, Routing, Wireless and Inventory. Preserve
availability windows, sensor definitions/freshness, resource readings/failures,
Ports approved card grid/ifName route/preferences/panel and recycled gestures,
VLAN/FDB/ARP fields/order, routing state and inventory tree/copy/toggle contracts.
Graphs, logs, Graylog and backups keep independent layouts. Accepted #405,
#450/#451/#452 and all other closed contracts remain unchanged. #288 retains
global mock-up ownership. DESIGN already records measured-base and fixed-action
lessons from #530/#515; this PR adds only the graph action/readout lesson.

## Exact remaining verification, all UNVERIFIED

### Independent review corrections

Opus 5.5 Medium reviewed fd8a254 against 476ba15. Three findings were
substantiated: the new centre Label inherited regular rather than the original
Button's semibold font; graph colours were snapshots with no live-theme update;
and inactive-state speech was incorrectly recorded as uncertain despite the
explicit Not active fact. Fixed all three. The centre and measuring Label now
use BodySemibold. A Loaded/Unloaded-paired RequestedThemeChanged observer rebuilds
the graph on a theme change; repeated attachment unsubscribes first, and queued
callbacks check the observer is still attached. This updates node text, fills,
borders and connecting lines together without changing status meanings.
Shared consumers remain DeviceSectionPage's device-neighbour graph only.
Fresh full-diff review and replacement CI remain pending. Native theme-switch
focus, font-scale changes, long semibold names and readout roles remain unverified.

Record final SHA, CI build, OS, display, theme, text size and individual outcome.
Use synthetic example.net and edge.example.net, IDs 7/8 and long port names.

1. Measure page, body host, graph viewport/content and list viewport/content in
   the same native coordinate system. Record host H, pill bottom B, list bottom
   V, V minus B and final-row reach independently. Cover side, bottom and top
   pill, missing pill fallback, short/long/empty content, 799/800pt transition.
   Check #469–#474 separately and smoke #475–#479; inspect diagnostics for
   placement loops. No inferred width percentage or bottom offset.
2. Duo outer portrait, inner portrait/landscape and supported outer landscape:
   cold launch open/closed, fold/unfold and rotate while loading and typing,
   then tab return, nested child return and resume without reopening. Repeat
   ordinary iPhone portrait/landscape, Android/older-iOS header and iOS glass.
3. Largest text and both themes: long node/port/protocol/platform names, zero,
   one and many neighbours; scroll graph to every node and list to final row.
   Change theme/text size on retained page and confirm nodes refresh correctly.
   If graph fit/refresh fails, capture dimensions before proposing a new policy.
4. VoiceOver/TalkBack and physical keyboard: centre reads Current device without
   button action; every neighbour button announces complete facts and Show
   neighbour card, selects/scrolls matching row once; row opens actual monitored
   ID once, unmonitored has no navigation. Check selection removal by filtering,
   native focus/outline, search typing and programmatic scroll retention.
5. Open search via header/glass/Command-F. Type endpoint/protocol/state queries,
   select graph node, programmatically scroll, then actually drag each scroller,
   complete and unfocus. Verify intended query/focus retention and final-row
   reach with docked/floating keyboard and after dismissal. Enter page with a
   keyboard already visible; existing keyboard-frame risk is not fixed here.
6. Own/reverse-only ping device, deduplicated both directions, LLDP/CDP,
   unmonitored/missing port/platform, unknown/testing/inactive, invalid and zero
   rates; then cold/empty/no-match/denied/offline/timeout and refresh/Retry failure
   and recovery. Return through each actual caller and confirm query/glass state.

Only source/diff inspection and git whitespace checks run locally. Shared tests,
Android and iOS CI are pending; all native checks above remain UNVERIFIED.
Parent independent review is pending. No issue closure, epic tick, release,
watch or device acceptance is authorised by this record. Native evidence is the
remaining geometry blocker; no speculative visual proposal was implemented.

---

# Device Neighbours review, #473

Refs #473 and #442. Baseline main `2c7f32f`. Read live issues and repository
CLAUDE.md, DEVELOPING.md, DESIGN.md and DESIGN-REFERENCES.md before assessment.
Routine guide choices are authorised. This PR is prepared for the parent queue;
it does not merge, watch, release, close issues or tick the epic.

## Assessment before editing

| Concern | Baseline evidence and disposition |
| --- | --- |
| Modes | DeviceSectionPage shows grouped LLDP/CDP cards and a scrollable star graph; 800pt usable width puts graph beside list. Device detail embeds five highlights. All assessed; network graph/saved groups/editor remain #450/#451, link detail #452 is preserved. |
| Long content/hierarchy | Fix required: title truncation, side protocol column and capped detail squeeze endpoints. Neighbour-only complete wrapping readout puts name, local-to-remote ports, protocols, platform and labelled readings in order; overview uses same facts. |
| Activation | Fix required: gesture-only card. Monitored neighbour gets a full-width described wrapping readout with a chevron and explicit Open neighbouring device action. Unmonitored neighbour keeps the Not monitored explanation and announces no unavailable action. The card Border owns the single tap gesture. |
| Unknown/status | Fix required: non-up port states become Down. Only explicit up/down receive severity; absent/testing/unknown states remain neutral. Local port state label avoids claiming remote endpoint health. Inactive discovery keeps precedence. |
| Readings | Fix required: negative/nonfinite/overflow traffic can appear valid. Unknown replaces invalid readings; valid zero remains valid and octets are converted to bits. No new readings or thresholds. |
| Discovery and IDs | Compliant: Core DeviceNeighbours.Build retains own and reverse discoveries, deduplication, matching and remote IDs. No mobile matching fallback added. |
| Graph/list | Compliant selection contract: node selects and scrolls to same filtered row; list activation opens neighbour, not link detail. Fix required: fixed node lanes clip long text; wrap and size lanes for names. Native measurement remains unverified until CI/device checks. |
| Stable viewport | Fix required: fixed 280pt graph crowds short portrait/landscape/keyboard list. Narrow graph capped to 35% of actual body height, recomputed on body resize; wide graph retains star height. |
| Loading/empty/errors | Compliant busy first load, source empty versus no search matches, pull refresh and error suppression of empty. Existing retained rows can age during refresh/failure and are not fresh-health evidence. #507 owns Retry and request race protection. |
| Search/lifecycle/fade | Shared fixes required and owned by #507. Do not duplicate ButtonSearch, IActivePage, Retry, disposable watcher, native drag checks or list fade/spacer. Local matching already searches fields, including new explicit state. |
| Insets | Compliant source ownership: manual iOS list inset Never, measured sides, finite list viewport above bottom glass plus gap, 24pt footer. No second inset owner introduced. |
| Hardware/accessibility/motion | Unverified. Native button, textual state and wrapping improve source contract; no device acceptance inferred. Existing reduced-motion search/graph scroll behaviour preserved. |

## Callers, returns and server contracts

DeviceDetailViewModel opens relative DeviceSection with DeviceId, Neighbours
enum and display name. Device detail can originate in Devices narrow/split,
Dashboard, alerts and other device links. Card opens Neighbours; row opens
relative DeviceDetail with the actual positive RemoteDeviceId. Back returns
to the section and original caller; no new route or navigation to #452 added.
Graph nodes only select/scroll and never infer a device ID from text.

Loader concurrently reads device links, fleet links and optional device ports.
Core builds both directions, including ping-only devices. Reverse device names
use existing server lookup and configured naming style. Non-authentication port
lookup failure still permits links with unknown readings; link failures remain
errors. Authentication, credentials, permissions, package pin and settings are
unchanged. No writes, saved selection, server schema or new endpoints introduced.
Search is transient and retained on dismissal. Existing ordering, card totals
and five-highlight choice remain unchanged. Only synthetic data added to tests.

## Exact pending integration after #507–#510

Read supplied sibling checkout review records 469–472 and relevant diffs
read-only. No sibling branch was changed or unmerged commits imported. Parent
must update only this branch at its queued serial review turn after #510.

1. Combine page specialised stacks under one wrapper, retaining independent
   AvailabilityLayout, SensorLayout, ResourceLayout, PortLayout and
   NeighbourLayout flags and every legacy-grid hide trigger. Keep one native
   action per port/neighbour and legacy inventory child commands.
2. Combine overview specialised readouts and hide triggers. Neighbour overview
   remains a readout inside the card that opens the section, not a nested action.
3. Keep #507's IActivePage, ButtonSearch, SearchButton, Retry, serial generation
   guard, disposable watcher, idempotent glass subscriptions and list-column
   fade with 20pt spacer. Keep #508 sensor freshness/Updating and #509 resource
   clearing. Do not restore the baseline unguarded load or manual search.
4. Preserve #510 ports-only keyboard close and stacked filter panel. Keep this
   PR's body SizeChanged placement and narrow graph height cap, and position
   #507's fade in the list column in both neighbour modes.
5. Merge SectionRow flags and tests without dropping sibling search clauses or
   reading validation. New neighbour flag defaults false in other consumers.

Shared consumers #469–#479 retain availability windows/outages, all sensor
classes/limits/definitions, resource units/bars, port sort/group persistence and
ifName route, neighbour reverse IDs/selection, VLAN/FDB/ARP fields and order,
routing protocol state, wireless endpoint/units, inventory tree/expansion/ports
toggle/serial copy. Graphs, logs, Graylog and backups keep specialised routes.
Setup #405 acceptance and #288 global theme/mock-up ownership remain intact.

## Validation and remaining device checklist

Full intended and staged diffs self-reviewed against the guide; git diff --check
is the only local validation. No local builds, tests or scripts run. Added
unexecuted regressions for unsupported states, invalid traffic, valid zero,
complete action facts and positive remote ID. Existing reverse-only, optional
ports, unmonitored neighbour and navigation tests remain relevant. Shared tests
and Android/iOS CI pending. Parent mandatory Claude Opus 5.5 Medium review is
pending quota; no Claude or delegate started. Hardware remains unverified.

Record final integrated SHA/build and results separately for every row below.
Use example.net and edge.example.net, IDs 7/8 and long synthetic interface names.

| Check | Required steps; all currently unverified |
| --- | --- |
| Endpoints | Own-only, reverse-only ping device, both directions deduplicated, LLDP/CDP, monitored/unmonitored, absent ports/platform/name, remote ID fallback; correct local-to-remote meaning and actual ID navigation |
| State/readings | Up/down/testing/missing/unknown and inactive discovery; zero/negative/NaN/infinite/overflow rates; neutral unknown, no fabricated remote health, correct bits and speed |
| Graph/list | Short/long/empty lists, node selects and scrolls same row, row opens device once, filter removes selection, graph/list stay on same filtered snapshot; 800pt split transition |
| Failure | Cold load, success empty, no matches, denied/offline/timeout, optional ports failure, refresh success/failure/Retry; slow device switch after #507 integration |
| Themes/text | Both themes, largest text, long names/ports/protocol/platform, wrapping card readout and graph nodes; initial heading clear of fade and final row fully reachable |
| VoiceOver/TalkBack | Complete endpoint/state facts and one correct activation; unmonitored readout has no false action; node selection, Back/search/Retry and overview card usable |
| Search/keyboard | Header/glass/Command-F, local endpoint/protocol/state query, typing retains focus, user drag/completion/unfocus retain query, no swipe reveal; final row reachable with keyboard and after dismissal |
| Duo | Outer portrait, inner portrait, inner landscape and supported outer landscape; fold/unfold and rotate while typing/loading; finite stable list viewport, no duplicate inset or stale glass owner |
| Resume/routes | Devices narrow/split, Dashboard and alert to detail to section to neighbour and Back; background/resume at each level, caller and query retained |
| Ordinary iPhone | Portrait/landscape, both themes/largest text, VoiceOver, keyboard, Reduce Motion, older header and iOS 26 glass where available; final row and graph selection reachable |

User acceptance remains required before #473 closure or #442 ticks. Parent owns
integration, review, merge and any later TestFlight delivery.

## Sequential integration

Updated against main f220f15 after #507–#510. Neighbour presentation coexists
with port/resource/sensor/availability templates and independently hides the
legacy row. The merged card Border remains the single navigation gesture;
neighbours use a described start-aligned readout and conditional chevron rather
than a nested multiline Button. The retained lifecycle/watcher from #507 is
preserved; only section-size observation is retained from this branch.
Full-diff Claude review and CI must verify the integrated templates and routes.
No device acceptance is inferred.

## Independent review remediation

Claude Opus 5.5 Medium reviewed c459b22 against main f220f15. The C# 14
field-keyword lambda compile failure is corrected by renaming the parameter.
NeighbourAction now appends Open neighbouring device only for monitored IDs;
regression tests cover absent/zero IDs and a monitored ID. The assessment now
matches the described readout and single card gesture. The review confirmed
combined templates, graph IDs, reverse discovery and shared lifecycle wiring.
Replacement CI is pending; no local builds/tests ran. Hardware remains unverified.
