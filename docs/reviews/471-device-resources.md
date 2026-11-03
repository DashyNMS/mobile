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
