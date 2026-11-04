# Device Resources review, #471

Refs #471 and #442. Prepared independently on main `2c7f32f`. Read live
issues, CLAUDE.md, DEVELOPING.md, full DESIGN.md and DESIGN-REFERENCES.md
before assessment. Routine guide choices were authorised. No acceptance,
issue closure, epic checkbox, merge or release is claimed.

## Assessment before implementation

DeviceSectionPage and its code-behind render DeviceSectionViewModel groups
from DeviceSectionLoader. Resources has CPU, memory and storage modes together,
not selectable tabs. DeviceDetailView embeds DeviceSectionCard highlights from
the same rows. DeviceDetailViewModel opens the relative DeviceSection route
with device ID, Resources enum and display name. Devices narrow/split detail,
Dashboard and alerts can lead to that detail; Back retains the actual caller.
There is no per-resource navigation or editor. Shell/query contracts stay intact.

| Concern | Baseline status and evidence | Resolution |
| --- | --- | --- |
| CPU/memory/storage hierarchy | Fix required: shared row title tail truncation and 180pt/two-line value cap | ResourceLayout only stacks wrapping name, kind, value, capacity, status and bar |
| Embedded highlights | Fix required: two-column truncated name, no capacity/status | Same resource facts in overview; existing highlight selection unchanged |
| Units/order/threshold | Compliant: Units.Percent/Bytes, CPU and memory API order, storage description order; server warning with existing 80/95 defaults | Preserved for valid percentages |
| Severity/accessibility | Fix required: colour-only state | Explicit OK/Warning/Critical/Reading unavailable and joined description; child facts excluded |
| Missing/invalid usage | Fix required: NaN could be OK and invalid values clamped to bars | Null, nonfinite and outside 0–100 have neutral status, dash and no bar; reported zero remains valid |
| Capacity | Fix required: negative/contradictory bytes formatted as valid capacity | Format only nonnegative used and positive total with used no greater than total; never derive a substitute percentage |
| Search | Fix required: group context and unavailable state absent from matching | Resource-only kind/status matching, existing fields unchanged |
| First loading | Compliant: BusyCard and finite CollectionView | Preserved |
| Empty/search/error | Compliant: source-empty differs from no matches, error suppresses empty, WhenAll propagates endpoint failure | Preserved; #507 owns Retry |
| Cached/offline/refresh | Fix required: retained bars have no source timestamp | Clear resource groups before fetching; failure leaves error without old bars; pull refresh remains native |
| Persistence/security | Compliant: read-only endpoints, no credentials/settings writes or new permissions | Unchanged Core pin, APIs, server data, identities and navigation |
| Insets/final bar | Compliant in source: manual side clearance, finite viewport above bottom glass plus 12pt, 24pt footer, iOS native list inset disabled | No extra inset owner; hardware unverified |
| Shared search/lifecycle/fade/races | Fix required in baseline, owned by #507 | Do not duplicate; integration instructions below |
| Themes/large text/keyboard/motion/screen readers | Unverified on hardware | Wrapping follows guide; no new animation; exact checks below |

UsageRow is internal and only the resource loader and tests call it. New flags
default false for other SectionRow consumers. Health and wireless keep their
own reading/threshold paths. DeviceSectionCard retains total count, first CPU,
first memory and fullest known disk choice. Invalid storage bars now remain
null and cannot rank as an infinite reading. Overview fetch failure still uses
its existing failure summary. This is not a whole Device overview assessment.

## Integration after #507 and #508

Read both pending checkouts and relevant diffs read-only:
`/home/tpickup/.t3/worktrees/mobile/t3-6f1a2b09` (#507 availability) and
`/home/tpickup/.t3/worktrees/mobile/t3-fe4b6f6f` (#508 Sensors), including their
469-device-availability and 470-device-sensors review records. No unmerged
commits were imported and neither branch was changed. Keep this PR queued
after #508. Only parent updates serially at its merge/review turn.

Exact overlaps are DeviceSectionLoader, SectionRow, DeviceSectionViewModel,
DeviceSectionPage.xaml, DeviceDetailView.xaml and DeviceSectionTests. Keep all
three independent layout flags. In the page use one wrapper with availability,
sensor and resource stacks, then the legacy grid hidden by three DataTriggers.
In overview keep all three hide triggers and specialised stacks. Keep one
overview bar per resource, excluded from duplicate accessibility announcements.
Merge each flag's Matches clause; do not let kind/status affect legacy rows.
Keep all added regression cases.

Preserve #507's captured section/device, generation guard and serial handoff.
Extend its pre-request availability-only clearing condition to also include
Resources. Do not restore this baseline's unguarded await. Keep #508's sensor
updating try/finally around that guarded request. Availability/resources clear
rows; sensors retain their cache and freshness status. Preserve #507's
IActivePage, ButtonSearch, native drag checks, Retry, disposable watcher,
idempotent glass subscription, list-column fade and 20pt spacer. This PR
changes no code-behind or global search behaviour.

| Shared consumer | Contract to retain during integration |
| --- | --- |
| #469 Availability | Server windows/outages, availability layout/state, guarded refresh and clearing |
| #470 Sensors | All classes, Core units/limits/definitions, sensor layout, cache freshness and Updating |
| #471 Resources | CPU/memory/storage order, units/thresholds, resource-only stack/search/clearing and final bar |
| #472 Ports | Persisted sort/group, filters, ifName graph route and return from #481 |
| #473 Neighbours | Graph/list selection, reverse links, device IDs and wide split fade |
| #474 VLANs | Number, name, port count and ordering |
| #475 FDB | MAC/interface/VLAN context and ordering |
| #476 ARP | Address/MAC/interface context and ordering |
| #477 Routing | BGP/OSPF/v3/VRF fields and existing states |
| #478 Wireless | Separate endpoint, classes, API units, limits and missing values |
| #479 Inventory | Single-column tree, expansion, ports toggle and serial copy |

All other modes retain ResourceLayout=false. Graphs, logs, Graylog and backups
retain specialised pages. Setup #405 acceptance is unaffected.

## Validation and remaining device evidence

Complete intended and staged diffs self-inspected; git diff --check is the
only local validation. No local builds, tests or scripts run. Added unexecuted
regressions cover null/nonfinite/out-of-range usage, valid zero, capacity
sanity and resource-only search context. Existing warning threshold, grouped
loader and highlight tests remain relevant. Shared-test and Android/iOS CI
are pending. Mandatory parent Claude Opus 5.5 Medium review is pending quota.
No Claude reviewer or subagent was started. CI does not establish hardware
acceptance, and XAML compilation remains unverified until platform CI.

Tie every result below to the final integrated commit and CI build. Use only
synthetic example.net/edge.example.net, device IDs 7/8 and long resource names.

| Check | Exact steps still required | Result |
| --- | --- | --- |
| Readings | CPU, memory, several disks; 0/50/80/95/100%, server warning override; verify percentages and byte units agree with Core and source order | Unverified |
| Unknown | Null/NaN/both infinities/negative/>100; absent and contradictory byte totals; neutral unavailable with no false zero or healthy bar | Unverified |
| States | Cold load, successful empty, no matches, each endpoint denied/timeout/offline; refresh success/failure/Retry; slow device 7 then device 8 after #507 integration | Unverified |
| Stale bars | During refresh and after failure no old resources; overview failure and highlighted disk selection still truthful | Unverified |
| Search/keyboard | Header/glass/Command-F; match CPU, Memory, Storage, description, value, bytes and status; typing retains focus; completion/unfocus/drag retains query, no swipe reveal after #507 | Unverified |
| Themes/text | Light and dark, largest font, long name and capacity; overview and full section wrap; scroll final bar fully above side/bottom glass with keyboard open/dismissed | Unverified |
| Screen readers | VoiceOver/TalkBack announce name, kind, value, capacity and state once; no duplicate progress announcement; Back/search/Retry and overview activation remain reachable | Unverified |
| Duo | Outer portrait, inner portrait, inner landscape, outer landscape where supported; short/long lists, fold/unfold and rotate during search/loading; no clipping or duplicate inset | Unverified |
| Resume/routes | Background/resume, Devices narrow/split, Dashboard and alert to detail to Resources to Back; one glass owner, correct device and retained caller | Unverified |
| Ordinary iPhone | Portrait/landscape, both themes/largest font, keyboard, Reduce Motion, older header and iOS 26 glass where available; final bar and overview reachable | Unverified |

User acceptance is required before closing #471 or ticking #442. TestFlight,
merges, watches and release actions remain parent-owned and were not started.

## Sequential integration

Updated after #507/#508 against main 3adc7fd. Resource, Sensor and Availability
layouts coexist and independently hide the legacy row. Shared activation,
ButtonSearch, Retry and fade are retained. Resource clear-on-refresh is combined
with Availability in the generation-guarded serial request path; sensor updating
feedback remains unchanged. Full-diff Claude review and CI verify integration.
No hardware outcome is inferred.

## Independent review remediation

Claude Opus 5.5 Medium reviewed 3458144 against main 3adc7fd. Its CI test
failure is fixed by using double literals for nullable-double theory arguments.
Missing resource descriptions omit dash placeholders. Non-finite or out-of-range
warning thresholds now use existing 80/95 defaults, with regression coverage.
The refresh comment covers both resource and availability freshness. Repeating
the resource kind remains intentional context for overview and accessibility.
Replacement CI is pending; no local builds/tests ran. Hardware is unverified.

## Full reassessment on integrated main, 4 November 2026

Pre-edit consumer inventory for the review revision: repository-wide
`Units.Percent` calls occur only in `DeviceSectionLoader.UsageRow` (Resources
full page and overview, #471) and `AvailabilityAsync` (window rows, #469).
No other production consumers exist. The mobile shared project owns this
formatter; Core owns the endpoint models and exception contract. Availability
is affected by the shared correction and receives regression coverage; no
existing acceptance is inferred or changed. #532 ReadSensor remains separate.

Refs #471 and #442. Source base is `cdf3b0d`, including merged #530 shared
viewport and #531 sensor presentation corrections. This section supersedes
historical geometry-compliant labels and queued integration instructions above.
PR #509 is already merged, head `c1c408047891a830b223c4cb81dcaecbe4d4eb55`,
squash `566174894c689c9d558eb1ca546026e1747b1bd9`. Its historical Opus review
and successful CI recorded in reconciliation apply to that work only. No stale
Resources branch is replayed. Subsequent review confirmed two source defects:
near-full percentages rounded to 100%, and a resource-category 404 could mask
another category's real failure. This revision fixes both; acceptance is pending.

Read live #471 body and its complete comment stream, live #442 body/comments,
`/tmp/mobile-442/issue-471.json`, reconciliation's #471 record, #509 record and
original assessment annex, current #469/#470 assessments, CLAUDE, DEVELOPING,
full DESIGN and DESIGN-REFERENCES. The compressed requested cache path
`/tmp/mobile-442issue471.json` is not the actual path. There is no separate
FULLDESIGN file. The sole live #471 comment reports unusable base extent across
#469–#474, asks for shared inset/viewport correction and fold/rotate/tab/resume
checks, and explicitly rejects acceptance. That rejection remains open.

### Producers, consumers and boundaries

Paths below are relative to `src/`. Resources is one grouped mode containing
Processors, Memory and Storage, not three selectable pages. Shared symbols are
in `DashyNMS.Mobile.Shared/DeviceSections/` unless another path is given.
`DeviceSectionLoader.ResourcesAsync` creates resource-only `UsageRow` records;
`SectionRow.ResourceLayout`, ResourceKind, ResourceStatusText and
ResourceDescription feed both `DashyNMS.Mobile/Pages/DeviceSectionPage.xaml`
and `DashyNMS.Mobile/Views/DeviceDetailView.xaml`. Each hides the legacy grid
when ResourceLayout is true. The overview has one separate decorative bar per
highlight; the full page has its bar within the resource stack.
`DeviceSectionCard.Show/Resources` selects highlights and counts all rows.
`ViewModels/DeviceDetailViewModel.LoadSectionsAsync/RefreshAsync` creates fresh
cards per refresh and loads their projections; `OpenSectionAsync` passes the
selected DeviceId, section enum and title through the relative Shell route.
`AppShell` registers the section host. Resources rows have no resource-specific
child, editor, graph, export, filter panel, primary Save or persistent selection.
These capabilities are N/A; no extra actions or source fields are invented.

The revision changes the shared percent formatter and resource loading only. Existing host owners
remain Availability #469, Sensors #470, Resources #471, Ports #472, Neighbours
#473, VLANs #474, FDB #475, ARP #476, Routing #477, Wireless #478 and Inventory
#479, plus EventLog compatibility. Preserve their independent presentation
flags, Availability windows/outages, Sensors cache/limits, Ports saved sort/group
and ifName route, Neighbours reverse links/graph/split, VLAN/FDB/ARP facts,
Routing protocols/state, Wireless formatting, Inventory expansion/ports/copy.
Actual EventLog entry uses Logs; Graphs, Graylog and Unimus use specialised
pages. Accepted setup #405 and other accepted pages retain their contracts.
The dirty #475 worktree and all unrelated branches remain untouched. #532's
broader ReadSensor work is separate and is not included.

### Full source findings and disposition

Core contract trace: mobile remains pinned to `DashyNMS.Core` 2.0.0-ci.52.
The available Core checkout (`5f7bde5`) exposes Task of IReadOnlyList of
ProcessorSensor, MempoolSensor and StorageVolume through IHealthApi and
DeviceHealthApi. Calls read devices/{id}/health/{category} and sensor detail
URLs with the same token. Transport throws LibreNmsApiException with message
then nullable HttpStatusCode; timeouts can carry IsTimeout without a status.
The pinned package is absent locally, so exact package/source provenance is
not independently verified; CI must validate the contract. No restore or pin
change was made. Invoking the factory inside the async try handles synchronous
throws and faulted tasks alike.

Compliant means source inspection, not physical verification. Every native
result remains UNVERIFIED. No unresolved visual choice is implemented.

| Concern and trigger/mode | File/symbol, observed source behaviour | Expected result and disposition |
| --- | --- | --- |
| CPU/memory/storage source and order | `DeviceSectionLoader.ResourcesAsync`, concurrent ListProcessorsAsync, ListMempoolsAsync, ListStorageAsync with selected device ID; CPU/memory retain returned order, storage description sorts ordinal-ignore-case; Groups removes empty groups | Compliant with existing contract. All three supported categories assessed; no severity sorting introduced |
| Percent and units, both projections | `Units.Percent` rounded 99.95–99.995 to 100% and 99.9996 to 100.000%, while the bar retained raw usage | Confirmed text/bar mismatch fixed with shortest round-trip formatting for >=99.9 and <100. Current-culture decimal separator retained; other bands, zero, 100 and null unchanged. No flooring, byte-derived percentage or raw bar/status change |
| Known false OK / capped-bar allegation | `UsageRow` validates finite 0–100 before Value, Bar and Status; invalid becomes dash, null bar, None | Invalid-value defects were corrected by #509; the distinct valid near-full text mismatch is corrected here. Physical rendering remains unverified |
| Limits and severity | `UsageRow` uses valid server warning, otherwise 80 warning; critical is max(valid warning or 90, 95), hence default 95 | Policy unchanged. A valid warning >=95 equals the critical boundary, so equality is Critical, including usage 100 with warning 100. Invalid warning falls back. This corrects the description only; no server critical-health claim or new limit display |
| Used/total/free and partial facts | `UsageRow.Subtitle` formats used of total only for used >= 0, total > 0, used <= total | Existing honest limitation recorded: neither free bytes nor independently known partial used/total is shown. CPU has no capacity. Missing/zero/negative/contradictory capacity omits the line; a valid percentage can still have OK state despite missing capacity. No false zero/free or invented missing total; no guarantee that percent equals rounded byte ratio |
| Missing and unknown | `UsageRow`, `SectionRow.ResourceDescription` | Compliant null, NaN, infinities, negative and >100 percentage neutral/unavailable with no bar. Valid 0 is a real empty bar and may be OK. Invalid capacity does not invalidate an independently reported valid percentage; status describes usage only |
| Full names/long content | Both ResourceLayout stacks, `ResourceDescription`; legacy grids hidden independently | Compliant wrapping name/value/capacity/status, no inherited 180pt value cap. ResourceKind repeats group context intentionally in overview and spoken facts. Maximum text and actual clipping unverified |
| Counts and overview selection | `DeviceSectionCard.Show/Resources` counts all returned rows, first CPU and memory, highest Bar storage; null Bar ranks as zero, stable ties retain order | Compliant existing selection, not all resource rows shown in overview. All-unknown storage or unknown tied with known zero can select first unknown. Card attention reflects all rows, even a non-highlighted row; no invented worst-CPU guarantee |
| Loading and stale full-page refresh | `DeviceSectionViewModel.LoadAsync/RefreshAsync`, page Busy/Refresh | Compliant clears Resources before fetch because no freshness timestamp. Initial loading and native refresh retained; error leaves no stale resource bars. Sensor Updating is N/A here |
| Overview refresh and failure | `DeviceDetailViewModel.RefreshAsync/LoadSectionsAsync`, `DeviceSectionCard.Failed/IsVisible` | Fresh cards replace previous cards after device request succeeds. Prior overview can remain during that initial request or failure, without a resource timestamp. Card failure clears highlights and stays visible, denial wording differs. Source-empty Resources card disappears. Existing limitation, not a fresh-resource guarantee |
| Empty vs no matches | `DeviceSectionViewModel.IsEmpty/EmptyText/ApplyFilter`, page EmptyView | Compliant successful empty differs from unmatched term; errors suppress empty, empty groups removed. Full page can show no resources even though overview card is omitted |
| Denied/offline/partial endpoint results | Previously `ResourcesAsync` Task.WhenAll could throw CPU 404 first and outer `LoadAsync` returned empty despite memory denial/timeout/500 | Confirmed false-empty bug fixed: each resource category handles only its own 404 as unsupported/empty before WhenAll. Supported categories remain; any non-404 still fails the section. Requests remain parallel with unchanged IDs/tokens. Other sections retain global 404 compatibility. All-404 is compatibility-empty, not evidence of genuine source-empty |
| Retry/device handoff | `RefreshAsync/LoadAsync` generation and serial pending guard; ErrorLine Retry | Compliant busy-disabled Retry reloads all endpoints; ID/mode change clears groups, stale result/error rejected. Waiting for prior request may delay new device. Runtime failure/success and handoff unverified |
| Search and ordering | `SectionRow.Matches`, `DeviceSectionViewModel.ApplyFilter`, page OpenSearch/OnSearchCompleted, `Controls/ButtonSearch` | Compliant case-insensitive local matching name/value/subtitle and resource-only kind/status. Filtering preserves category and source order. No Resources sorting/chips/Ports filters; no severity filter invented |
| Actions and keyboard | Page constructor, UpdateGlass, Header, ButtonSearch, SearchField | Compliant iOS 26 pushed Back/search with actual previous-title label, Android/older iOS header alternatives, inline Retry. Header search command is replaced with OpenSearch, so unused ToggleSearch clear behaviour does not control it. Button/glass/Command-F only, refocus on reopen; completion/unfocus/native drag closes retaining term; replacement/programmatic scroll must keep focus. Existing controls target >=44pt; actual labels/targets and keyboard operation unverified |
| Navigation and persistence | `DeviceDetailViewModel.OpenSectionAsync/OpenTileCommand`, page ApplyQueryAttributes, relative Back | Compliant Devices narrow/split, Dashboard and alert entry through device detail retain exact selected ID/title and actual caller; no resource row child. Retained-tab/child return and resume remain native checks. Read-only APIs, permissions, credentials, Core pins and persisted settings unchanged |
| Accessibility/themes/motion | Both ResourceDescription stacks exclude child facts/bars; themed StatusText/StatusColor; ButtonSearch uses ReducedMotion | Source compliant explicit status independent of colour, joined complete description and wrapping. VO/TB announcements, overview activation, contrast, largest text and reduced-motion completion unverified |

### Actual viewport and inherited #530 dependencies

`DeviceSectionPage.xaml` native page owns only top container safe area on iOS;
Root, SectionBody and Refresh opt out. Android retains native Container safe
area. CollectionView has no SafeAreaEdges; page `StopInsetAdjustment` sets its
native UIScrollView to Never on handler/load and placement. Header/search/status
Auto rows precede finite star SectionBody. Refresh occupies its star row and
List fills Refresh. Individual resource Borders are content, not a separate
ListCard. Hidden NeighbourGraph consumes no Resources graph row. Ports FilterCard
and graph obstruction reserves are N/A in Resources.

`PlaceControls` queries PillIn and Occupied in native SectionBody coordinates.
For side/bottom pills, viewport bottom margin is max(0,H−B), without clamping
to native safe-bottom. Refresh owns this margin; ListFoot owns normal 24pt plus
remaining bottom glass obstruction. Top-wide pills use the separate shared
SetupActionPlacement policy; missing native measurements retain the existing
fallback. Sides are 16pt plus CoveredEdges once on List, matching fade/header.
There is no Resources-specific offset. #530 owns this source correction for
the rejected base extent, not native proof of the exact failure or its cure.

ListFade is input-transparent, 20pt, inside the actual list column. Header
padding reserves 20pt; empty view uses 32pt margin. Its background-to-clear
colours preserve actual RGB, light #F4F6F9 and dark #11141A, in Colors.xaml.
Initial text and final complete resource row/bar still need native measurement.

Page Activate uses central IActivePage and AppShell activation for retained-tab
return/resume without relying on Appearing or fetching. Watcher and Moved are
idempotent; keyboard observers install once/dispose on departure; queued page
work guards active state. ManualKeyboardScroll owns iOS translation, keyboard
screen bounds convert through window to SectionBody, and docked keyboard
reduces finite viewport. Inherited #530 risk: activation with an already-visible
keyboard can lack its frame until a new notification. This is UNVERIFIED,
not fixed here. Ports' inherited non-scrolling landscape picker panel may clip
Sort/Group/Done with wheel/keyboard, even at default text. It remains #469/#472
verification, not Resources acceptance. No shared dependency is silently
reversed; all eleven modes plus EventLog retain the current shared policy.

### Validation and exact pending acceptance

Code self-check covers full intended main-to-head diff against current DESIGN,
explicit file set, staged diff and whitespace. No local builds, tests, scripts
or agents run. Added CI-only regressions cover near-full/nearest-double values,
two cultures, raw resource bars/status and Availability, synchronous and faulted
404s, retained supported categories, all-404 and concurrent 403/500/timeout.
Existing
DeviceSectionTests cover valid/default/server-warning, invalid usage and warning,
capacity guards, zero/search and grouped loader; historical #509 CI is evidence
only for its old head. Parent Opus 5.5 Medium full-diff judgement is PENDING.
Final-head CI is PENDING. All device results are UNVERIFIED; build number,
device/OS and actual H/B/V/last-row measurements are unavailable. Exact final
review SHA is the PR head, not source base cdf3b0d; record it with each result.

For each row below record exact integrated SHA, CI/TestFlight build, device/OS,
theme/text/assistive settings, native page and SectionBody height H, pill bottom
B, actual Refresh/List viewport bottom V, V−B, keyboard bounds, first heading
and complete last row/bar bounds. Measure overview card separately. Source
review, independent review and CI never substitute for these measurements.

| Trigger/configuration | Required result; all currently UNVERIFIED |
| --- | --- |
| Duo outer/inner portrait/landscape; ordinary iPhone portrait/landscape; side/bottom/top/missing pill | Available finite viewport reaches measured base where anchored, V−B=0, one inset owner; last full row/bar clears actual glass |
| Both themes, maximum text, VO/TB, Reduce Motion, short/long resource lists and overview | Full long names/values/capacity/status readable, initial heading below fade; one complete spoken fact, accessible overview activation and named >=44pt Back/search/Retry |
| Header/glass/Command-F; name/CPU/Memory/Storage/value/capacity/status query, no match, clear term | Correct groups and order; filtering/programmatic scroll keep keyboard focus, real drag/completion/unfocus dismiss retaining term; no swipe reveal |
| Docked keyboard show/hide, fold/rotate while typing/loading, retained tab/child/caller return, resume with keyboard already open | Correct caller/ID/query and one glass owner; no extra observers or translation; final row reachable and measured base restored without refresh/Appearing |
| Cold load, genuine empty, endpoint 404/mixed success, denied/timeout/500/offline, Retry success/failure, slow device 7 then 8 | Honest source result/error, no false freshness or stale full-page bars; record compatibility-empty separately from true source-empty; overview initial-device-refresh limitation recorded |
| CPU/memory/multiple disks, 0/50/80/95/100%, custom warning 0/60/100/invalid; null/NaN/infinities/negative/>100 | Existing policy and raw source units preserved; no capped invalid bar or false OK; full page and selected overview highlight agree |
| Used/total null independently, zero/negative total, negative used, used>total, valid capacity with absent percentage, differing reported percent/byte ratio | Omitted unsupported capacity/free facts stay distinguishable from actual zero; unavailable percent has no bar; no derived substitute percentage or invented health |
| First CPU/memory, fullest/tied/unknown disks and non-highlighted warnings, long names and many rows | Counts/order/highlight selection agree with source; card attention may reflect unshown row; complete last resource remains reachable |

Use synthetic example.net/edge.example.net and IDs 7/8 only. #471 remains open
and #442 unchecked until explicit user device acceptance. No merge, watch,
release, issue edit or epic tick is performed. Unresolved visual findings need
actual evidence and an agreed proposal before any speculative UI change.
