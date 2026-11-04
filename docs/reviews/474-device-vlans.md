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
| Missing values | Fix required: absent name/count disappears. Explicit Name unavailable and No member ports reported preserve raw nulls and do not imply zero. |
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
#511. Mandatory Claude Opus 5.5 Medium parent review completed at f3f3151.

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

## Sequential integration

Updated after #507–#511 against main a2f1e62. VLAN stacked facts coexist with
neighbour, port, resource, sensor and availability layouts; each independently
hides the legacy grid. The shared card-wide gesture, activation, ButtonSearch,
Retry and generation guards remain from main. No old nested gesture or wrapper
is reintroduced. Full-diff Claude review and CI must verify the integrated XAML.
No hardware acceptance is inferred.

## Independent review remediation

Claude Opus 5.5 Medium reviewed f3f3151 against main a2f1e62 with no blockers.
The loader waits for both VLAN and port lists successfully, then derives counts
from returned IfVlan and per-port membership. When no returned record carries a
VLAN, No member ports reported describes that result without asserting a complete
zero or a fetch error. Completeness of membership in the pinned API remains
unverified. The regression expectation is updated. Overview title weight and
spacing now match other stacked highlights. Card-wide gestures remain unchanged.
Replacement CI is pending; no local builds/tests ran. All hardware outcomes and
issue acceptance remain unverified.

CI at c85d73c passed 1,218 tests and exposed one stale search assertion for the
old count wording. The regression now checks member ports reported. Replacement
CI is pending; no local tests ran.

## Full-scope reassessment, 4 November 2026

Baseline and fetched origin/main are both
`7d2c5a943ad40c676ecf5d3afbd1e33a62916351`; this isolated checkout was clean.
Live #474 and #442 are OPEN. #474 has one comment, the 4 November base-extent
rejection; #442 has no comments. PR #512 is merged at `0416232`, and its old
`t3/device-vlans` branch ends at `4bb0abb`. The live PR search found no open
#474 PR. Read the complete original assessment above, reconciled assessment's
#474 and shared rules, CLAUDE, DEVELOPING, DESIGN and DESIGN-REFERENCES.
Historical review/CI evidence is not acceptance of this reassessment.

Independent review established a mobile read-failure defect, corrected below.
No visual choice, offset, API, package pin or upstream fix is introduced.
In particular, the earlier assessment's claim that viewport-above-
glass placement was compliant is superseded by the rejection and current
DESIGN measured-base rule. Actual reported geometry remains UNVERIFIED after
the integrated correction, not resolved by source review or historical CI.

### Ownership recorded before editing

Shared placement is centrally owned under #469/#530 (`74c1891`). The list body
uses native SectionBody coordinates, aligns the viewport with the measured
side/bottom pill base, reserves bottom obstruction in ListFoot, and treats a
wide top pill separately. Keyboard clearance takes precedence. The sibling
wide graph clears overlays separately. #534 (`476ba15`) supplies the Ports grid,
non-Port gesture handling and window resize subscription; #535 (`7d2c5a9`)
supplies neighbour graph speech/theme handling. None is reverted or duplicated.

The eleven shared consumers are Availability, Sensors, Resources, Ports,
Neighbours, VLANs, FDB, ARP, Routing, Wireless and Inventory (#469–#479).
Event log, Graylog, Graphs and backups retain their specialised destinations.
SectionRow, overview templates, DeviceSectionPage, ButtonSearch,
CoveredEdges and TabBarButtons remain unchanged. Accepted pages and setup
contracts remain intact. Changes also include the loader and shared regression
tests; no other checkout is touched. The private ResourceCategoryAsync helper
is renamed OptionalSectionSourceAsync and reused for the two VLAN reads.
Existing Resources callers (processors/memory/storage) are behaviourally
unchanged; FDB/ARP remain owned by #475/#476 and require the same error audit.
No new reusable layout rule was found beyond the existing #530
viewport/footer and #515 placement guidance.

### Source dispositions across the original scope

C means source contract retained, F means reported geometry failure still awaiting
accepted evidence, U means unverified, and NA means no such mode or action.
C does not assert a native pass.

| Concern and affected mode | Observed source, expected result and disposition |
| --- | --- |
| Identifiers and names, overview/full list | C: VlansAsync filters DeviceId, orders numerically by VlanNumber, displays that tag rather than internal VlanId, and preserves VlanName without trimming. SectionRow supplies Name unavailable for blank/absent names. Both templates wrap all three facts without line limits. U: narrow and maximum-text fit. |
| Port counts and unknowns | C within the typed contract: await fleet VLANs and device ports together; count each returned port once per positive tag by distinct membership plus IfVlan. Missing match is null and No member ports reported, never inferred zero. Positive count describes returned records, not proof of all trunk memberships. No operational/admin/deleted filtering is added. U: server completeness. |
| Full source validity | U: Core owns JSON parsing, endpoints, membership completeness and malformed/omitted identifier provenance. Mobile consumes ci.52; no installed ci.52 package exists locally. Read-only Core checkout `5f7bde5` exposes int VlanNumber/DeviceId and nullable name/IfVlan, with Vlans initialised empty. Its PortsApi requests explicit columns plus with=vlans; older servers may omit membership. That checkout is supporting evidence, not proven identical to ci.52. Missing numeric fields cannot be distinguished from zero by the consumer. Do not silently filter/default-repair or mirror Core. Definitive validity requires pinned Core source/package provenance and synthetic payload evidence upstream. This blocks a claim of complete source validity, not this documentation PR. |
| Overview selection and metadata | C: DeviceSectionCard.Show counts all loaded rows; neutral VLAN rows retain numeric order through stable Worst selection, taking three highlights. Summary counts VLAN records, not ports. Failed clears highlights and keeps a retry-by-opening card; successful empty hides VLAN card. Full card opens the section; highlights have no nested action. Device name is in section title; no invented VLAN metadata is added. U: actual card speech/activation. |
| Initial load and empty | C: finite grouped CollectionView, BusyCard and separate status; IsEmpty requires loaded, not busy and no error. Empty source differs from Nothing matches. Corrected: each VLAN source treats its own 404 as absent; other failures propagate even alongside a 404. A ports 404 preserves valid VLAN rows with unavailable membership. Absence is not evidence of permission or completeness. NA: VLAN editor, picker, selection sheet and server search. |
| Denial, timeout, offline and refresh | C: non-404 read failures propagate to ViewModelBase error/Retry; previous VLAN rows remain during refresh and after failure. They are last loaded facts with no VLAN timestamp or offline disk cache guarantee. Overview failure instead clears highlights and retains its card. U: native error visibility, retry and offline sequences. No freshness claim is added. |
| Generation and routes | C: LoadAsync increments generation, captures section/device for refresh, serialises pending work and discards superseded section results/errors. Different scope clears groups. DeviceDetail passes actual DeviceId, section enum and title on relative DeviceSection route. Overview load cancellation checks before Show. NA: VLAN row child route. U: actual rapid-device switch, tab return and resume; cancellation/lifecycle timing is not proven by reading guards. |
| Search and focus | C: local case-insensitive SectionRow.Matches includes identifier, raw name/count and VLAN-only unavailable labels. Query is transient and retained on dismissal. ButtonSearch opens from actions or iOS hardware-keyboard Command-F and closes on native drag, completion/unfocus; programmatic Scrolled alone does not close. No VLAN sort/filter panel or persistence change. U: focus, keyboard and native scroll event timing. |
| Hierarchy and adaptive layout | C: complete neutral stacked facts, common themed Card/Caption, finite viewport and shared non-Port adaptive span. No VLAN severity, graph action or misleading chevron. U: full names, reading order, line wrapping and useful column widths at maximum text on every display. No unagreed redesign proposed. |
| Glass, insets and final row | F: user reports section fails to reach usable base. Integrated source correction above replaces old clearance-only placement. Expected viewport reaches actual measured pill base and final row clears glass. U: H/B/V measurement, inherited safe areas, native clip/refresh bounds and footer reachability; no per-page offset proposal. |
| Accessibility and themes | C: VlanDescription combines identifier/name/count and excludes duplicate child labels; neutral facts use theme resources. U: VoiceOver/TalkBack grouping, complete speech once, overview action role, contrast, 44pt controls and maximum text. Colour alone conveys no VLAN health. |
| Motion | C: no VLAN-specific animation; ButtonSearch aborts named transitions and applies final state with ReducedMotion. NA: VLAN sheet/selection animation. U: repeated search open/close, Reduce Motion and lifecycle restoration. |
| Security and persistence | C: read-only existing Core client; no changes to credentials, permissions, server text, endpoint, storage, navigation or account visibility. No inferred remote membership or new network call. |

### Unresolved comment and exact native verification

The sole unresolved #474 comment is
https://github.com/DashyNMS/mobile/issues/474#issuecomment-6619308096.
Trigger: open Device → VLANs, including fold/rotate, retained tab return and
resume. Observed by user: section does not extend to usable base; no numerical
bounds supplied. Expected: full usable finite viewport with reachable final
row and clear controls. Proposed correction is the already-integrated shared
#530 measured SectionBody placement, not another guessed margin. Its source
is C as an implementation; reported outcome remains F/U pending device result.
The same comment covers #469–#474 and explicitly requests #475–#479 regressions.

All following steps remain UNVERIFIED. Tie each result to the reviewed build
and record OS, display, orientation, text size and theme, plus host height H,
pill bottom B, viewport bottom V and V minus B in the same native coordinates.
Capture page/body/list/native clip bounds and inherited/native content insets
separately. Verify base extent and final-row reach independently; a screenshot
of a reachable last row alone does not establish viewport alignment.

1. On Duo outer, inner portrait, inner landscape and ordinary iPhone portrait/
   landscape, cold-open overview then VLANs from Devices narrow/split detail,
   Dashboard and alert/device routes. Repeat light/dark at maximum text with
   synthetic VLANs 2/10/100, blank names and very long unbroken names. Compare
   overview total/first three facts with full numeric order and exact source.
2. Use short, long and empty lists. Scroll to the final row with side, bottom,
   top-wide and missing-pill fallback where available. Verify first heading
   below fade, backgrounds, viewport base, final complete fact and Back/search
   clearance. Repeat shared consumers #469–#479; Ports grid and wide/narrow
   neighbour graph retain their separate layout/selection contracts.
3. Open search from header/glass and iOS hardware-keyboard Command-F. Match name, ID, count and
   unavailable labels, then no match. Type successive replacements: keyboard
   stays focused. Complete, unfocus or genuinely drag: field closes and query
   remains. Reopen, clear, show/dismiss keyboard, fold and rotate while open;
   verify viewport restoration, final row and no placement loop.
4. Exercise cold load, empty success, fleet VLAN failure and device-port failure
   separately, denied/timeout/404/offline, Retry, successful and failed refresh.
   Compare retained last-loaded facts with error text and overview failure
   behaviour. Start slow device 7 then device 8; no old device rows/title may
   replace current scope. Do not treat a successful HTTP response as membership
   completeness. Use duplicate memberships plus matching IfVlan to check one
   count per returned port and no-match unavailable rather than zero.
   Specifically pair a VLAN 404 with ports 403/500/timeout: expect error/Retry,
   never successful empty. With valid VLAN rows plus ports 404, retain those
   rows and show No member ports reported. Both 404s yield successful empty.
5. Switch tabs away/back, return from the caller's child flow, background/resume,
   fold/unfold and rotate during loading/refresh/search. VLANs has no child
   action itself. Check actual Back destination, device/title/query, one glass
   owner and stable bounds without Refresh being required to restore controls.
6. With VoiceOver/TalkBack, read every fact once including missing labels and
   operate overview card, Back/search/Retry. Repeat Reduce Motion and rapid
   search transitions. Cover older iOS header fallback, iOS glass and Android
   native keyboard/safe areas where available. Record unavailable devices.

### Delivery evidence

Fresh full-diff Opus 5.5 Medium review at 10f0bd9 found no blockers and
confirmed both failure-handling fixes, unchanged Resources behaviour and
meaningful passing CI regressions. Its sole documentation finding (listing
the loader as unchanged) is corrected above. No production changes followed
that review. Final required CI and every native outcome remain pending.

Opus 5.5 Medium reviewed 7b9b88e and identified two related source gaps and a
keyboard-platform wording error. The original WhenAll could expose the VLAN
404 first and mask ports denial/500/timeout; a ports 404 could discard valid
VLAN records. Both are now corrected per source, using the existing Resources
404 wrapper before WhenAll. Factories execute inside async try/catch, covering
synchronous throws and faulted tasks; non-404 errors and cancellation propagate.
Core ci.52 reachability remains unproven, but mobile behaviour is directly
testable without guessing source completeness. This correction belongs in #474's
existing PR rather than a separate replacement. New CI regressions cover
preserved VLAN facts with missing ports and mixed failures, including async
failures and synchronous factory errors. Resources regressions remain relevant
because its private helper was renamed, with unchanged behaviour. Fresh review
and CI are pending; no native outcome is inferred.

No local build, restore, test, validation script, browser or device check ran.
Source review is implementation self-assessment only. Existing synthetic VLAN
regressions cover membership deduplication/IfVlan, numeric ordering, device
filtering, raw long names, neutral/no-link rows, missing labels/search and 404/
error policies. They are not rerun locally. Full intended diff against fetched current main and staged
whitespace are checked before commit. New-head CI and independent parent
Claude Opus 5.5 Medium review remain pending. Hardware acceptance, source
completeness evidence and reported geometry are separate remaining gaps.
No closure, epic tick, watch, merge or release is performed by this sub-agent.
