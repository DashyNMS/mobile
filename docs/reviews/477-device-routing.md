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
| Missing values | Fix: named missing identity/state/distinguisher presentation without replacing raw Title/Value. Raw null VRF name fallback remains VRF; presentation names it VRF name not reported. Missing OSPF state remains Warning under the existing classifier; no new health inference. |
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

## Cross-review remediation

Opus review at 47ebec1 identified colour-only routing severity as blocking.
Both routing templates now name the existing classification in a wrapping
label, combined screen-reader description and scoped search. Neutral VRFs
have no severity label; no state classification or raw field is changed.
Missing VRF names are named explicitly while preserving the raw fallback.
Overview weight and spacing match FDB/ARP/VLAN highlights. Derived properties
explain their purpose. Regression coverage checks protocol severity/search
and loader-level absent OSPF/VRF fields. Admin-down BGP retains its existing
Inactive classification and Shut down state; hardware coverage remains pending.
Final Opus review and replacement CI pending; no local builds/tests ran.

## Sequential reassessment on integrated main, 4 November 2026

This section supersedes the earlier source-contract and loader-wide-404 acceptance
claims. Baseline and fetched origin/main are both
`e3d9f3f0fadc0c7a1916f30227d921d9b8bf3da7`, with a clean starting checkout on
`t3/routing-review-477`. Live #477 and #442 are OPEN with no comments.
No open routing PR or partial work was found. PR #518's final head is
`b18d78ef5c319e05819c1397286d74a798170acc`, squash
`35bf21712b5a2c3a83d5344bcb65da0321090e23`, an ancestor of this baseline.
Its body, comments, review records and inline comments were inspected. The
final severity correction is present. No stale branch is replayed.

Read CLAUDE.md, DEVELOPING.md, DESIGN.md, DESIGN-REFERENCES.md, the full original
#477 assessment and the reconciled #442 scope/checkpoints. Historical CI and
review are evidence for #518 only. There are no unresolved live issue comments.

Before editing, ownership was recorded in the implementation handoff. #477 is
sole writer of RoutingAsync and routing regression coverage. The loader is
shared by #469 through #479. OptionalSectionSourceAsync remains unchanged and
continues serving Resources, VLANs, FDB and ARP. Both Routing consumers, overview
and full section, receive the correction. Shared SectionBody placement from
#530, Ports grid/window and non-Port gestures from #534, neighbour themes from
#535, VLAN failures from #536, FDB tag/permission resolution from #537 and ARP
MAC/source failures from #538 remain integrated. No other checkout is modified.

### Source audit and confirmed correction

Core remains pinned to 2.0.0-ci.52. Live Core CI run
[52](https://github.com/DashyNMS/core/actions/runs/41293836508) succeeded at
`5f7bde53ac05ea75f7cb6dd06a8fae62606f0769`, matching the read-only local Core
checkout. Inspected RoutingModels.cs, IRoutingApi, RoutingApi in ResourceApis.cs,
NoneFound and LibreNmsTransport.GetCollectionCoreAsync at that commit.

| Protocol | Scoped endpoint and collection | Field meaning and presentation |
| --- | --- | --- |
| BGP | bgp?hostname=deviceId, bgp_sessions | bgpPeerIdentifier is the displayed peer identity, formatted by RoutingText.Address. RemoteAs is an AS number, not an internal ID. State, administrative status, description, AS text, established seconds and last-error text retain their existing mapping. stop/halted is Inactive; established is OK; other states, including missing, remain Critical. |
| OSPF | ospf?hostname=deviceId, ospf_neighbours | ospfNbrIpAddr, ospfNbrRtrId, ospfNbrState and context_name supply address, router identity, state and context. Full-prefix remains OK, other/missing states Warning. |
| OSPFv3 | ospfv3?hostname=deviceId, ospfv3_neighbours | ospfv3NbrAddress, router_id, ospfv3NbrState and context_name map independently, then follow OSPF rows in the same group. |
| VRF | routing/vrf?hostname=deviceId, vrfs | vrf_name, mplsVpnVrfDescription and mplsVpnVrfRouteDistinguisher supply name, description and distinguisher. vrf_id is internal identity and is never displayed as a distinguisher. Rows remain neutral. |

All four are independent primary protocol reads, optional only for unsupported
404 responses. There is no metadata-only read, global device listing or new
permission dependency. No extra fetch is needed. Denial of a primary protocol
continues to fail the section explicitly. Optional enrichment permissions are
N/A here, unlike FDB's mapping dependency.

F corrected in RoutingAsync: raw Task.WhenAll plus LoadAsync's outer 404 catch
could discard valid BGP/OSPF/v3/VRF rows when another protocol was unsupported.
A surfaced 404 could also mask a concurrent denial, 500, timeout or cancellation.
Each factory now uses the existing OptionalSectionSourceAsync before WhenAll.
Only that source's 404 becomes empty. Factories throwing synchronously are
captured inside the async boundary, so all sources start and non-404 failures
propagate. No catches are added for denial, timeout, 500 or cancellation.
Other failures still use existing visible section/overview error handling;
successful protocols are retained when another is unsupported, not when another
primary source fails. Existing failed-refresh retained rows remain accompanied
by the error. This is the same source-boundary policy already in DESIGN.md.

C mapping, U completeness: numeric IDs, RemoteAs and EstablishedSeconds cannot
distinguish omission from zero. Nullable text and nullable foreign IDs retain
unknowns. AdditionalData retains unmodelled fields in Core, but mobile does not
display every field, counter or internal identity. Existing compact routing
facts are preserved; no payload-completeness claim or invented field meaning is
made. Core returns empty for missing/null collections, throws for non-arrays or
JSON parsing failures, and maps status 500/'Error retrieving ospfv3_nbrs' and
404/'VRFs do not exist' pairs to empty before mobile sees them, trimming message
whitespace and comparing messages without case. Status matching is exact. Other 500s
propagate. CI substitutes typed APIs, so it does not establish valid/malformed
server-payload completeness. No upstream parser is mirrored or changed.

### Complete scope dispositions

C means source compliant; correction means the confirmed bug above is fixed;
N/A means absent capability; U means unverified. Each C remains subject to the
separate native matrix below.

| Concern and modes | Source evidence and disposition | Review / CI | Device |
| --- | --- | --- | --- |
| BGP, OSPF, OSPFv3, VRFs | C RoutingAsync ordering, grouping and raw classifications above; correction for independent 404s | Self-inspected; fresh parent review and CI pending | U facts and ordering |
| Overview and full section | C DeviceSectionCard.Show/Worst counts all rows and selects three worst; wrapping RoutingLayout templates in DeviceDetailView and DeviceSectionPage share complete routing speech/text | Existing regressions retained; new supported-protocol/overview cases added for CI | U long facts and speech |
| Entry/return | C DeviceDetailViewModel.OpenSectionAsync uses relative DeviceSection route and selected ID/name; DeviceSectionPage query loads scope. Devices narrow/split, Dashboard, alerts and other device callers share overview. Routing itself has no embedded detail mode or row destination | No route changes; parent review pending | U each caller, pinned Devices versus More-pushed Devices, child return and resume |
| Loading, empty, no match, failure | C BusyCard, ErrorLine/Retry, IsEmpty and ApplyFilter distinguish loading/error/source-empty/query-empty. Correction prevents mixed failures becoming empty | CI cases cover each missing source, synchronous/faulted failures, denial/500/timeout/cancellation and all unsupported | U cold load, denied, offline, retry |
| Refresh and handoff | C DeviceSectionViewModel generation/pending guards prevent old scope publication; failed routing refresh retains previous rows with error. Overview card.Failed stays visible and clears highlights. No persistent routing cache or timestamp | Existing shared race/error coverage retained | U slow device 7 to 8; refresh success/failure/retry; no freshness inference |
| Search and keyboard | C ButtonSearch, OpenSearch and SearchField open by button/glass/Command-F, real drag/completion/unfocus close with query retained; filtering/programmatic scroll retains focus. Matches includes protocol, state, missing labels and full existing text | Unchanged scoped search coverage | U repeated refocus, actual drag, keyboard show/dismiss and query retention |
| Hierarchy and unknowns | C SectionRow RoutingTitleText/ValueText/StatusText/Description, wrapping template labels and explicit severity. Missing state/name/distinguisher named; raw classification unchanged | Existing missing/long-fact regressions retained | U unbroken text, maximum text and complete speech |
| Bounds, glass and final row | C shared SectionBody host, PlaceControls measured pill base, footer obstruction, top fade spacer and native Never inset. IActivePage.Activate restores watch/glass; disappearance removes window/watch/keyboard observers | Shared #530/#534 remains unchanged; no duplicate observers introduced | U H/B/V measurement and last-row reach separately |
| Themes, accessibility and motion | C theme resources, joined description/excluded children, existing reduced-motion ButtonSearch; no new animation | Source inspection only | U contrast, native role/activation, VO/TB and repeated transitions |
| Writes, exports, filters and selection | N/A no routing edit/Save/export/comparison/picker/filter panel; rows remain inert. C credentials, permissions, storage, actions and server content unchanged | No new API/package or persisted format | U overview activation and Back, N/A nonexistent routing writes |
| Sibling consumers | C four loader call sites only; shared helper, templates and placement unchanged; #530 and #534 through #538 contracts retained | Full intended diff inspection; parent independent review pending | U sibling smoke below |

### Recurring checkpoints and handoff

No local build, test, restore or validation scripts were run. New tests are for
CI execution only. Full intended diff against fetched current main and staged
whitespace are inspected before commit. Parent owns independent Opus 5.5 Medium
full review, new-head shared/Android/iOS/security CI judgement and authorised
merge. No watches, merge, closure, epic ticks or release occur in this pass.
DESIGN.md already states per-source 404 and metadata-permission rules. No new
lesson is needed and no existing #515/#530/#536/#537 rule is duplicated.

Every following native result is separately UNVERIFIED. Record final SHA,
build, OS and diagnostics for Duo outer, inner portrait, inner landscape and
ordinary iPhone. Repeat light/dark, maximum text, VO/TB and Reduce Motion on/off.

1. Record native page, host, pane, viewport and content bounds in common
   coordinates. Record H, pill bottom B, viewport bottom V and V minus B for
   side, bottom and top pills separately. Then independently scroll the complete
   last row above obstruction. Check first heading/fade, long unbroken facts,
   short lists, empty/error and keyboard-open layouts. CI proves no visual result.
2. Fold/rotate during load, filtering and refresh without reopening. Show and
   dismiss keyboard, drag, complete and unfocus; verify base restoration and
   query retention. Programmatic scroll/filter replacement must retain focus.
3. Exercise all protocol states listed in the original matrix above. For each
   endpoint independently, unsupported must retain other supported protocol rows;
   denied/500/timeout/cancel must show failure, never successful empty. Include
   all empty, all unsupported, no-match, offline and failed-refresh retained rows.
4. Open from Devices narrow and split, Dashboard, alert/device and other device
   links; return to actual caller, switch tabs, restore retained stack and resume
   without requiring Refresh. Verify one glass owner, no duplicate observers or
   placement loops. Check Android/older-iOS header and iOS glass fallback paths.
5. Verify complete names/state once with VO/TB, inert routing rows, overview
   activation once, Back/search/Retry target size and busy guards. Check both
   themes, maximum text and repeated motion. Smoke Availability, Sensors,
   Resources, Ports grid/filter/graph return, neighbour graph/theme, VLANs,
   FDB tag/denied metadata, ARP raw MAC/failures, Wireless and Inventory.

Hardware outcomes and user acceptance remain pending. Earlier device steps
requiring preservation of loader-wide 404 behaviour are superseded by step 3.

Opus 5.5 Medium reviewed the full diff at bf89607 with no blocking bugs.
Its medium test-gap finding is corrected: denial, 500 and timeout are now
crossed with all four protocols and both synchronous and faulted delivery.
For OSPF, OSPFv3 and VRF the unsupported BGP task comes first, directly exposing
the old masking. BGP is first in task order, so its async mixed-failure cases
guard already-correct error propagation; its synchronous cases also verify
all four reads start. Supported-protocol and cancellation regressions remain.
The Core message-matching description is corrected above. Production code is
unchanged from the reviewed head. Existing missing-state severity choices are
preserved (BGP Critical, OSPF Warning), not inferred as new health evidence.
Final-head CI and all native acceptance remain pending.
