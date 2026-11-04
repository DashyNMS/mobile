# Device Routing review, #477

Refs #477 and #442. Baseline main d9353cc after #476 / PR #514.
Read live issue and correct epic #442, CLAUDE.md, DEVELOPING.md, full
DESIGN.md, DESIGN-REFERENCES.md and section assessments #469–#476.

## Assessment and implementation

| Concern | Evidence and disposition |
| --- | --- |
| Modes | Compliant: RoutingAsync combines BGP, OSPF, OSPFv3 and VRFs into three nonempty groups. No protocol picker, editor, selection, graph, sort or filter panel. OSPFv3 follows OSPF in the same group. |
| Long facts | Fix: DeviceSectionPage legacy grid truncates titles and caps values to two lines/180pt. Routing-only stack wraps identity, protocol, subtitle, state/distinguisher and detail independently. |
| Overview | Fix: DeviceDetailView legacy highlight omits subtitle and detail and truncates identity. Same full facts now wrap in highlights with explicit protocol, important when the group heading is absent. DeviceSectionCard still selects three worst rows, counts all entries and hides successful empty sections. |
| Missing values | Fix: named missing identity/state/distinguisher presentation without replacing raw Title/Value. Existing null VRF name fallback remains VRF. Missing OSPF state remains Warning under the existing classifier; no new health inference. |
| Protocol semantics | Compliant: BGP non-established before established, then ordinal ignore-case peer address. Admin-down text Shut down and Inactive, established OK plus formatted uptime, other state Critical plus LastErrorText. OSPF full-prefix is OK, others Warning. VRFs neutral and ordinal ignore-case name sorted. All raw server descriptions, AS facts, contexts, router IDs and address formatting retained. |
| Source contract | Compliant: four selected-device endpoint calls run concurrently and all must complete. No Core pin, API, permission, credential, settings or persistence changes. |
| Search | Compliant ButtonSearch/header/glass/Command-F only, real drag/completion/unfocus dismissal retains query. Fix: protocol and displayed missing-value/distinguisher labels now match only routing rows, alongside original title/subtitle/value/detail. Filtering must retain focus. |
| Loading/empty/error | Compliant source: BusyCard, finite CollectionView, Retry disabled while busy; source empty differs from unmatched query and error suppresses empty. Existing loader-wide 404 handling returns an empty section even if another routing endpoint returned data. This contract is preserved, not changed to partial results. |
| Refresh/offline | Compliant existing last-loaded rows retained on failed refresh with error; no persistent routing cache or freshness timestamp. Guarded serial refresh captures section/device and discards superseded results/errors. Actual timing and offline sequences unverified. |
| Insets and final content | Compliant source: top-only native iOS safe area, native list inset Never, 16pt plus measured sides, viewport above bottom glass plus 12pt, 24pt footer, 20pt fade spacer in list column. No new inset owner. Hardware reachability unverified. |
| Accessibility/themes/motion | Fix: complete joined RoutingDescription and excluded child labels avoid duplicate speech. Existing themed caption/status styles and reduced-motion search retained; no new animation. Screen-reader grouping, contrast, unbroken strings and activation traits unverified on devices. |

## Entry, return and shared contracts

DeviceDetailViewModel.OpenSectionAsync opens relative Routes.DeviceSection with
selected DeviceId, Routing and device title. AppShell registers DeviceSectionPage;
its query handler loads that scope into DeviceSectionViewModel. Device detail
can originate in Devices narrow/split, Dashboard, alerts and other device links.
Header Back and iOS glass Back use the actual previous page and relative '..'.
Routing rows have no destination. Overview's existing single card gesture opens
the whole section, without nested actions. No Save, immediate writes, copy/share
or new navigation contract. Activation/resume and idempotent glass ownership
remain unchanged. The shared legacy inert row gesture remains unchanged.

Changes are additive RoutingKind/RoutingLayout, scoped Matches, loader metadata,
and independent hide triggers in the two templates. Availability percentages and
outages, sensor classes/definitions/limits/freshness, resource units/bars and
clear-on-refresh, Ports persisted sort/group and ifName graph route, neighbour
reverse IDs/graph selection/split, VLAN counts, full FDB and ARP stacks/order,
wireless endpoint/units and inventory nesting/expansion/ports toggle/serial copy
remain intact. All sibling rows default RoutingKind=null and RoutingLayout=false.
No shared lifecycle, insets, Retry, search controls or request guards changed.
Graphs, logs, Graylog and backups retain specialised pages. Setup #405 acceptance
and #288 global ownership are unaffected. DESIGN.md already covers complete
facts, wrapping, missing values and acceptance; no reusable guideline gap found.

## Validation and exact remaining matrix

Full intended and staged diffs self-inspected against DESIGN.md; whitespace
checked with git diff --check. No local tests or builds run. Added CI regressions
cover all protocols, state classification, complete long context, protocol search,
missing-value labels, section isolation and overview ranking/facts. Existing BGP
error/uptime and shared loading/race/error tests remain relevant. Shared tests,
Android and iOS compilation are pending CI. Parent obtains mandatory Claude
Opus 5.5 Medium cross-review. No delegates, Claude, watches, merges or releases.

Every hardware row below is unverified. Tie each result to final integrated SHA
and TestFlight build. Use devices 7/8 example.net/edge.example.net, 192.0.2.0/24,
2001:db8::/32, AS64500 and synthetic long descriptions.

| Check | Exact remaining steps |
| --- | --- |
| Protocol facts | BGP established, active/error, admin-down and missing state; OSPF full/init/missing; OSPFv3 IPv6/router ID/context; VRF description and present/missing distinguisher. Verify ordering, raw text and three worst overview highlights agree with full section. |
| States | Cold load, all-empty success, unmatched query; each of four endpoints independently denied/timeout/offline/404; verify preserved whole-section failure/404 behaviour. Refresh success to failure to Retry; retained rows must not imply freshness. Slow device 7 then 8 cannot publish old scope. |
| Search/keyboard | Header/glass/Command-F only; match AS, address, router ID, context, state, error, protocol and missing labels. Typing replacements retain focus; actual drag/completion/unfocus retain query; clear query and reach final result with keyboard open/dismissed. |
| Themes/text | Light/dark, largest accessibility text, long unbroken identity/context/distinguisher/error and device title, short/long lists plus overview; first heading below fade, entire final row above side/bottom glass. |
| Screen readers | VoiceOver/TalkBack read identity/protocol/context/state/detail once; no false row action; overview opens Routing once; Back/search/Retry reachable and Retry disabled while busy. |
| Duo | Outer portrait, inner portrait, inner landscape and supported outer landscape; fold/unfold and rotate during loading, refresh and search. Correct one-to-three columns, finite viewport, final row, no duplicate inset/glass owner. |
| Routes/resume | Devices narrow/split, Dashboard and alert to device to Routing to Back; retain actual caller and query. Background/resume at overview and section, retained tab stack and pin changes; one glass owner. |
| Ordinary iPhone | Portrait/landscape, both themes, largest text, VoiceOver, keyboard, Reduce Motion/repeated search transitions, older header and iOS 26 glass where available. Verify short/long lists and overview. |
| Diagnostics/siblings | Inspect Tab bar buttons and Top line entries for loops/overlap; smoke shared sections including FDB/ARP, Ports filters/graph return, neighbour split and inventory expansion after integration. |

Hardware/user acceptance pending. No closure or epic ticks. Parent owns CI,
cross-review, merge and cleanup serially, then final TestFlight after #484.
