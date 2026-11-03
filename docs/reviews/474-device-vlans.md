# Device VLANs review, #474

Refs #474 and #442. Prepared on main `2c7f32f`. Read live issues,
CLAUDE.md, DEVELOPING.md, full DESIGN.md and DESIGN-REFERENCES.md before
editing. Routine guide choices are authorised. No local builds, tests or
scripts, delegates, Claude review, merge, watch, release, closure or epic ticks.

## Assessment before editing

| Concern | Baseline evidence and disposition |
| --- | --- |
| Modes | Grouped full VLAN list, local search and overview highlights. No VLAN editor, filter panel, graph or row destination. All assessed. |
| Hierarchy/wrapping | Fix required: count occupies a capped side column, squeezing name at large text/narrow width. VLAN-only stack wraps identifier, name and count without line limits. |
| Overview | Fix required: truncated ID beside count and no name. Same complete wrapping facts now appear in existing three highlights; selection and totals unchanged. |
| Missing values | Fix required: absent name/count disappears. Explicit Name unavailable and Port count unavailable preserve raw nulls and do not imply zero. |
| IDs/order/server text | Compliant: filter DeviceId, numeric VlanNumber order, raw VlanName. Preserved without trimming or renaming. |
| Port meaning | Compliant: counts returned ports carrying the VLAN through membership or IfVlan, once per port per VLAN. No match remains null, not zero. No new deleted/admin/oper-state filtering or remote membership inference. |
| Severity/actions | Compliant: neutral RowStatus.None and no links. No guessed health, new actions or navigation. Legacy gesture moved to legacy grid so VLAN readouts have none. |
| Search/context | Existing case-insensitive ID/name/count matching compliant; add matching for displayed missing-value labels only on VLAN rows. Device name stays in page title. No persistence change. |
| Loading/empty/error | Compliant BusyCard, finite CollectionView, native refresh, empty source distinct from unmatched search, error suppresses empty. Loader 404 means empty; other failures propagate. Both required reads must succeed. |
| Cached/offline/refresh | Existing retained rows survive refresh failure without freshness timestamp. Preserve as last loaded facts, not evidence of a live port count. Hardware failure sequences unverified. |
| Insets/final row | Compliant source ownership: manual iOS list inset Never, measured side clearance, viewport above bottom glass plus 12pt and 24pt footer. No extra inset owner. Actual reachability unverified. |
| Shared search/lifecycle/fade | Fix required in baseline, owned by #507: ButtonSearch/query retention, native drag close, Retry, IActivePage/resume, request guard and fade/spacer. No duplicate implementation here. |
| Accessibility/themes/motion | Combined readout with children excluded; no new animation. VoiceOver/TalkBack grouping, theme contrast and largest-text measurement unverified on hardware. |

## Callers, returns and contracts

DeviceDetailViewModel.OpenSectionAsync selects relative Routes.DeviceSection
with actual DeviceId, Vlans enum and display name. Detail is reachable from
Devices narrow/split view, Dashboard, alerts and other device links.
DeviceSectionPage.ApplyQueryAttributes loads that scope. Header/glass Back
uses the actual previous page and relative `..`. VLAN rows have no child
route. Returning preserves caller and transient query; no save contract exists.
Shell registrations and navigation service are unchanged.

DeviceSectionLoader reads fleet VLANs and device ports concurrently and filters
VLANs to the selected device. DeviceSectionCard supplies the same rows to the
overview; empty VLAN sections disappear after loading, failures remain visible
with the existing retry-by-opening summary. No API, Core pin, endpoint,
credentials, permissions, settings, persistence, copy/share or security change.
A missing membership count is unavailable even after a successful port read;
it is not a claim that zero ports carry the VLAN.

## Pending integration after #507–#511

Read sibling review records 469–473 and relevant diffs in the supplied
checkouts read-only. No sibling branch was modified or unmerged commit
imported. Parent updates this branch only at its serial review turn after
#511. Mandatory Claude Opus 5.5 Medium parent review waits for quota reset.

1. In DeviceSectionPage.xaml combine VLAN stack and legacy hide trigger with
   AvailabilityLayout, SensorLayout, ResourceLayout, PortLayout and
   NeighbourLayout. Retain one wrapper and legacy-grid gesture. Keep native
   port/neighbour actions separate from these nonactionable VLAN facts.
2. In DeviceDetailView.xaml preserve all sibling specialised highlights and
   hide triggers. VLAN overview remains a readout inside the existing section
   card action, with no nested action. Preserve the one resource bar.
3. Merge SectionRow flags and independent Matches clauses, retaining
   VlanLayout=false for every other consumer. Only VlansAsync sets it.
   Preserve loader fixes and all sibling regression tests.
4. Keep #507's guarded serial refresh, captured device/section, IActivePage,
   ButtonSearch, Retry, disposable watcher, idempotent glass and list-column
   fade plus 20pt spacer. Preserve #508 sensor freshness/Updating, #509
   resource clearing, #510 port panel keyboard handling and #511 graph sizing.
   Do not restore baseline shared code from this branch during resolution.

Shared consumers #469–#479 retain availability windows/outages, sensor
classes/limits/definitions, resource units/bars, port sort/group persistence
and ifName route, neighbour reverse IDs/graph selection/split, VLAN/FDB/ARP
fields and ordering, routing protocols/states, wireless endpoint/units and
inventory nesting/expansion/ports toggle/serial copy. Graphs, logs, Graylog
and backups retain specialised routes. Setup #405 and #288 ownership remain.

## Validation and exact remaining device checks

Full intended and staged diffs self-inspected against the guide; whitespace
checked with git diff --check. No local builds/tests/scripts run. Unexecuted
synthetic regression covers numeric ordering, device filtering, membership
deduplication including IfVlan, missing name/count, neutral status, no link,
long exact server name, joined facts and section-scoped search. Existing VLAN
404/error and empty-card tests remain relevant. Shared tests, Android and iOS
CI pending; XAML compilation unverified. Independent parent review pending.

Record final integrated SHA/build and result separately for every row below.
Use example.net/edge.example.net, IDs 7/8, VLANs 2/10/100, synthetic long names.

| Check | Exact remaining steps; all unverified |
| --- | --- |
| Facts | Verify numeric 2/10/100 order, full long name, null/blank name, trunk duplicate memberships plus matching IfVlan counted once, several ports, no matching port unavailable rather than zero. Compare overview totals and three highlights. |
| Loading/failure | Cold load, successful empty, unmatched query, fleet VLAN and device port denial/timeout/offline separately, 404, successful and failed refresh/Retry. Retained rows are last loaded. Slow device 7 then 8 cannot show old scope after #507. |
| Search/keyboard | Header/glass/Command-F only; match ID, name, count and missing labels; typing replacement retains focus. Completion/unfocus/real drag closes and retains query after #507. Clear query; final result reachable with keyboard and after dismissal. |
| Themes/largest text | Light/dark, largest accessibility text, long unbroken name, short/long lists and overview. First heading clear of fade; entire final row above side/bottom glass; no clipping or count crowding. |
| Screen readers | VoiceOver/TalkBack read ID, name and count once in order, including unavailable labels. No false action/status; Back/search/Retry and overview card activation accessible. |
| Duo | Outer portrait, inner portrait, inner landscape and supported outer landscape. Fold/unfold and rotate while searching/loading/refreshing. Stable finite viewport, correct columns, final row, no duplicated inset or glass owner. |
| Resume/routes | Background/resume on section and overview, Devices narrow/split, Dashboard and alert to detail to VLANs and Back. Correct device/title, retained query and actual caller, one glass owner. |
| Ordinary iPhone | Portrait/landscape, both themes, largest text, VoiceOver, keyboard, Reduce Motion, older header and iOS 26 glass where available. Short/long list and final row reachability. |

Hardware and user acceptance remain pending. Do not close #474 or tick #442.
Parent owns integration, review, merges and later TestFlight delivery.
