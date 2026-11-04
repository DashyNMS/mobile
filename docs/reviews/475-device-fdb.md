# Device FDB review, #475

Refs #475 and #442. Baseline current main 0416232438241593489c83ec9acb370b52eb31ba.
Read live issue/epic, CLAUDE.md, DEVELOPING.md, DESIGN.md,
DESIGN-REFERENCES.md and prior section assessments #469–#474.

> Historical assessments below are superseded on VLAN semantics and provenance by
> [Confirmed VLAN correction](#confirmed-vlan-correction). Positive internal IDs
> were incorrectly described as compliant VLAN tags.

## Assessment and changes

| Concern | Evidence and disposition |
| --- | --- |
| Modes | Compliant: grouped MAC table, local search and three overview highlights. No FDB editor, row destination, graph, sort or filter panel. Adaptive list supports one to three columns. |
| MAC/interface/VLAN hierarchy | Fix: DeviceSectionPage legacy grid truncates MAC and caps VLAN to 180pt/two lines beside interface. FDB-only stack wraps each fact. Overview formerly omits interface and truncates MAC; now uses the same complete stack. |
| Missing context | Fix: absent/nonpositive VLAN silently disappears. Display VLAN not reported without changing raw Value or guessing untagged membership. Unknown interface retains existing port-ID fallback. |
| Ordering and server facts | Compliant: endpoint is scoped by selected device, join ports by PortId, order by looked-up interface name then raw MAC, ordinal ignore case, with unmapped ports first. Preserve MAC formatting, exact interface display name, positive VLAN label, duplicate entries and neutral status. No inferred health or port navigation. |
| Search | Compliant title/interface/VLAN matching, now also matches the displayed missing-VLAN label only for FDB. ButtonSearch retains query, opens from header/glass/Command-F and closes on actual drag/completion/unfocus. Programmatic row replacement must retain focus. |
| Loading/empty/error | Compliant source: BusyCard, finite CollectionView, Retry, successful empty distinguished from unmatched query, error suppresses empty. Both FDB and ports reads must succeed. Loader treats 404 as empty, other failures propagate. |
| Refresh/offline | Compliant existing contract: pull refresh retains last loaded FDB rows on failure; no persistent FDB cache or freshness claim. Generation guard prevents old device results from replacing new scope. Actual timing/offline sequences unverified. |
| Insets/layout | Compliant source: top-only native iOS safe area, list inset adjustment Never, measured 16pt plus covered sides, viewport above bottom glass and gap, 24pt footer. List-column fade reserves 20pt. No new inset owner. Physical final-row and large-text fit unverified. |
| Accessibility/theme/motion | Complete FDB description groups MAC, interface and VLAN; child labels excluded from duplicate speech. No false link chevron or new animation. Existing themed styles retained. VoiceOver/TalkBack grouping, contrast and long unbroken text remain unverified on hardware. |

## Routes and preserved contracts

DeviceDetailViewModel.OpenSectionAsync uses relative Routes.DeviceSection with
selected DeviceId, Fdb and device title. Shell registers DeviceSectionPage;
ApplyQueryAttributes passes that scope to DeviceSectionViewModel. Device detail
can originate in Devices narrow/split views, Dashboard, alerts or other device
links. Header/glass Back uses the actual previous page and relative `..`.
FDB rows have no destination; overview card opens the full FDB section through
its existing single card gesture. Returning retains the caller, with no Save
or immediate mutation contract. Activation/resume and idempotent glass ownership
remain unchanged from #469.

DeviceSectionLoader reads device FDB and device ports concurrently. No API,
permission, credential, Core pin, settings, persistence, copy/share or security
change. Missing ports still use `port {PortId}`; raw null VLAN stays null.
DeviceSectionCard retains totals, empty-section hiding, error summary and three
highlights. A readout on that overview is not a nested action.

Shared changes are additive FdbLayout opt-in, one Matches clause and independent
legacy-grid hide triggers. Availability, sensors, resources, ports, neighbours
and VLAN stacks and tests from #469–#474 remain intact. ARP, routing, wireless,
inventory and specialised graph/log/backup routes retain their behaviour.
No shared lifecycle or search helper changes. Setup #405 and global #288 ownership
are unaffected. DESIGN.md already covers wrapping facts, unavailable values,
search, inset ownership and acceptance; no reusable guideline gap was found.

## Validation and remaining acceptance

Full intended diff reviewed against the guide and whitespace checked. No local
builds or tests run on this code machine. Added regression coverage for complete
MAC/interface/VLAN facts, interface ordering, MAC formatting, missing/nonpositive
VLAN, port fallback, neutral/nonactionable rows, overview highlights and scoped
search. Shared-test and Android/iOS CI and mandatory parent Claude Opus 5.5
Medium cross-review remain pending. No delegate or Claude invoked.

Tie each hardware result to the integrated SHA and TestFlight build. Every row
below is unverified; CI cannot establish visual acceptance.

| Check | Required evidence |
| --- | --- |
| Facts | Long interface, formatted/unusual MAC, same MAC on several VLANs, positive/zero/missing VLAN, missing port mapping; full overview and section agree. |
| States | Cold load, empty success, no matches, each required endpoint denied/offline/timeout/404; refresh success/failure/Retry; slow device 7 then 8. Retained facts must not imply freshness. |
| Search/keyboard | Header/glass/Command-F only; MAC/interface/VLAN/missing label query; typing retains focus, drag/completion/unfocus retains query; final row reachable with keyboard and after dismissal. |
| Text/theme | Both themes, largest accessibility text, long unbroken MAC/interface, short and long lists; first heading clear of fade and final row clear of bottom/side glass. |
| Screen readers | VoiceOver/TalkBack announce complete facts once, no false action; overview card opens FDB once; Back/search/Retry reachable. |
| Duo | Outer, inner portrait, inner landscape and supported outer landscape; fold/unfold and rotate during loading/search; columns and clearances update without reopening. |
| Routes/resume | Devices narrow/split, Dashboard and alert to device to FDB to Back; background/resume at both levels; actual caller/query retained and one glass owner. |
| Ordinary iPhone | Portrait/landscape, light/dark, largest text, VoiceOver, keyboard, Reduce Motion, older header and iOS 26 glass where available. |

Use synthetic example.net/edge.example.net, device IDs 7/8 and documentation
MAC samples. Hardware acceptance is pending. No issue closure or #442 ticks;
parent owns CI, cross-review, merge, cleanup and the authorised final-batch
TestFlight. This PR does not merge, release or watch.

## Cross-review follow-up

Claude Opus 5.5 Medium found no blockers at 923902c. Low findings addressed:
FDB-only missing-MAC text now says MAC address not reported in both templates
and speech, retaining raw Title and other sections. Scoped search includes
this displayed label; regression coverage checks placeholders and section isolation.
New properties explain unavailable-value semantics in XML comments. Ordering
documentation now explicitly records unmapped ports first. Final review and
replacement CI pending; no local tests/builds or hardware acceptance claimed.

## Full-scope reassessment, 4 November 2026

Refs #475 and #442. Clean isolated sole-writer baseline and fetched main:
`868e526c4b0f31c8fa11a22c55ea869c901cae1a`. Live issue and epic are OPEN,
both with no comments; live open-PR list is empty. Read their full bodies,
PR #513 body/comments/state, CLAUDE, DEVELOPING, DESIGN, DESIGN-REFERENCES,
the reconciled #475 assessment and recurring verification M.

### Reconciliation and ownership before editing

PR #513 is already merged as `2393422164afa149a4f3b0d01969c35e79f5335f`,
head `8c2de169e1b4562e44bdd0c1b78890924a67961d`. Its complete wrapping,
missing-MAC speech/search and VLAN-not-reported presentation remain baseline.
Read every partial diff and untracked assessment in the original checkout
`/home/tpickup/.t3/worktrees/mobile/t3-1d79352a`, branch fix/475-device-fdb,
base 2c7f32f. Its FdbLayout loader/row/templates/test changes mostly precede
#513, and its nested legacy gesture and old unavailable wording must not
replace current integration. No file there was changed, imported or replayed.
The old assessment's both-reads-required and viewport-above-glass claims are
superseded below; historical green CI is not native acceptance.

Mobile owns this loader and shared tests; Core owns API parsing/models.
The eleven shared page modes are Availability, Sensors, Resources, Ports,
Neighbours, VLANs, FDB, ARP, Routing, Wireless and Inventory (#469–#479).
#530 owns measured SectionBody usable-base placement, #534 Ports grid/window
and non-Port gestures, #535 neighbour speech/theme, #536 VLAN per-read 404
handling. Preserve those controls and all accepted setup/other-page contracts.
Only FdbAsync reuses OptionalSectionSourceAsync. Its existing Resources
processor/memory/storage and VLAN consumers, implementation and semantics
are unchanged. ARP has the same old multi-read pattern but belongs to #476,
next; no ARP fix here. No API mirroring or Core package update.

### Complete concern dispositions

C means source compliant, U unverified, NA absent capability. All native
results are U even where source is C. The confirmed source defect is fixed;
no unresolved issue comments or unsettled visual decisions were found.

| Concern/mode | Observed source, expected result and disposition |
| --- | --- |
| MACs, interfaces, VLAN facts: list and overview | C: endpoint scoped to device; ports join by ID, unknown mapping says port plus returned ID. Order is looked-up interface then raw MAC, ordinal ignore case, unmapped first; duplicates remain. Mac lowercases/colonises when stripping nonhex yields 12 digits; otherwise preserves raw nonblank content; blank becomes dash then explicit missing-MAC presentation. Confirmed HIGH baseline bug, now fixed: resolve internal VlanId through selected-device VLAN metadata to a positive VlanNumber; unresolved context stays not reported. Both templates wrap full stacked facts without line limits. U: full source validity and native long unbroken text. |
| Source provenance | C: ci.52 provenance confirmed by successful Core CI run 52 at `5f7bde53ac05ea75f7cb6dd06a8fae62606f0769`; see exact evidence below. Internal-ID meaning is confirmed, not deferred. U: complete source validity remains independently unproven: typed payloads cannot distinguish omitted integer IDs from zero and do not establish every valid server payload. |
| Overview | C: totals count all returned entries; stable neutral Worst selection takes three in source order. Empty success hides card, failure clears highlights and retains error card/open action. No nested FDB action. U: actual card speech and activation. |
| Initial load, empty and no match | C: finite grouped CollectionView, BusyCard, error/Retry, empty success separate from Nothing matches. Corrected F: outer 404 catch after WhenAll could mask ports 403/500/timeout when FDB 404 surfaced first; ports 404 could discard valid FDB rows. Each read now converts only its own 404 to empty before WhenAll; valid rows retain port-ID fallback. Factories run inside async try, including synchronous throws. Other errors/cancellation propagate. Both missing yields empty. |
| Refresh, offline and retained data | C: existing rows retained while refreshing and after failure with error, no persistent FDB cache or timestamp/freshness claim. Overview failure clears highlights. U: native error/Retry and offline timing. |
| Routes/scope/security | C: relative DeviceSection route carries device ID, Fdb enum and name from DeviceDetail; actual caller Back is relative. Devices narrow/split, Dashboard, alerts and other device links retain their callers. Captured scope/generation guards reject obsolete results; scope change clears rows. Read-only credential, permission, server-content, routing, storage and export contracts unchanged. U: native navigation/resume and race timing. |
| Search/filter/keyboard | C: local case-insensitive raw/presented MAC, interface and VLAN search; missing labels scoped to FDB. ButtonSearch opens by header/glass/Command-F on iOS hardware keyboard, retains query on completion/unfocus/real drag, ignores programmatic scrolling. U: native focus/event timing/restoration. NA: FDB filter panel, sorting chooser, editor, comparison, sheet, row destination, fixed primary action and export. |
| Insets, glass and usable base | C: consume #530 native SectionBody coordinates, measured side/bottom pill base, separate obstruction footer, top-wide/fallback and keyboard precedence; native iOS inset adjustment Never, finite list and 20pt fade spacer. U: actual host/pane/viewport bounds, base delta and final row independently. #475 has no reported individual geometry defect; recurring checkpoint is mandatory, not an asserted failure. |
| Hierarchy/theme/accessibility/motion | C: neutral themed stacked facts and combined semantic description, children excluded from duplicate speech; no false health/link. Shared reduced-motion search and lifecycle restoration retained. U: both themes/contrast, 44pt actions, largest text, full speech once, native action roles and repeated transitions. |
| Adaptive/lifecycle | C: existing non-Port adaptive columns, window resize, activation, disposable watcher and glass ownership remain. U: Duo outer/inner portrait/inner landscape, fold/rotation, retained tabs and resume without Appearing, ordinary iPhone, Android/older iOS fallback. |

### Exact remaining native steps and evidence limits

Every following outcome is UNVERIFIED. Record reviewed SHA/build/OS, display,
orientation, theme and text size. Measure host H, pill bottom B, viewport
bottom V and V−B in the same native coordinates, plus page/body/list/clip
bounds and inherited/native insets. Verify viewport base and full final-row
reach separately. A reachable last row does not prove usable-base alignment.

1. From Devices narrow and split, Dashboard and alert → device, inspect FDB
   overview then full list and Back. Use synthetic devices 7/8, long interface
   names, locally administered MACs, missing/unusual MAC, repeated MAC on
   several interfaces/VLANs, missing port mapping and positive/zero/null VLAN.
   Compare counts, first three highlights, exact facts and ordering.
2. Cold-load short, huge and empty tables; search MAC/interface/VLAN/missing
   labels and no match. Inject FDB 404 with ports 403/500/timeout: error/Retry,
   never successful empty. Valid FDB plus ports 404 retains records and port
   IDs; both 404 empty. Independently fail FDB and ports, retry and refresh
   successfully/unsuccessfully offline. Retained rows must not imply freshness.
   Start slow device 7 then device 8; obsolete results must not replace scope.
3. On Duo outer, inner portrait/landscape and ordinary iPhone portrait/landscape,
   both themes and maximum text, measure H/B/V for side, bottom, top-wide and
   absent-pill fallback. First heading clears fade; complete last fact scrolls
   clear. Repeat fold/rotate during loading/search/refresh without reopening.
   Recheck all eleven modes, including Ports grid/graphs, neighbour graph and
   Inventory expansion/copy, preserving accepted sibling contracts.
4. Open search only from actions/Command-F; repeated typing/filter replacement
   and programmatic scrolling keep focus. Completion, unfocus and real drag
   close it while retaining query. Reopen/refocus, show/dismiss keyboard,
   measure base restoration and final row; no placement loop or doubled inset.
5. Switch tabs away/back, return through callers and background/resume with
   and without Appearing. Check correct device/title/query, one glass owner
   and actual Back destination. With VoiceOver/TalkBack hear all facts once,
   no invented row action, and operate overview/Back/search/Retry. Repeat
   Reduce Motion and rapid transitions; include Android and older iOS headers.

### Delivery

Added meaningful CI-only regressions for valid FDB plus synchronous/faulted
ports 404, primary missing plus ports denial/500/timeout, synchronous primary
factory failure and both missing. Existing Resources/VLAN regressions and all
sibling tests remain intact. No local build, restore, tests or validation
scripts run. Full diff versus current main and staged whitespace are inspected
before push. CI and independent parent Opus 5.5 Medium review/CI judgement
remain pending, separately from native acceptance.
No new DESIGN lesson: #536 source handling and #515/#530 alignment already
cover these rules. No watch, merge, release, issue closure or epic tick.

## Confirmed VLAN correction

Independent parent Opus 5.5 Medium review confirmed the HIGH baseline defect:
`FdbEntry.VlanId` is an internal VLAN row ID, not an 802.1Q tag. Earlier
positive-VLAN compliance claims above are superseded by this correction.
The finding is implemented in this PR; fresh full review and new-head CI
remain pending with the parent, separately from hardware acceptance.

Provenance: [Core CI run 52](https://github.com/DashyNMS/core/actions/runs/41293836508)
succeeded at [5f7bde53ac05ea75f7cb6dd06a8fae62606f0769](https://github.com/DashyNMS/core/commit/5f7bde53ac05ea75f7cb6dd06a8fae62606f0769),
also the inspected local Core HEAD. Its
[FdbEntry model](https://github.com/DashyNMS/core/blob/5f7bde53ac05ea75f7cb6dd06a8fae62606f0769/src/DesktopNMS.Core/Models/FdbEntry.cs)
documents that meaning; the model was imported in one commit. Desktop
[de629be resolver](https://github.com/DashyNMS/desktop/blob/de629be/src/DesktopNMS.Presentation/ViewModels/DeviceDetailViewModel.cs)
at LoadVlansAsync and FdbItemViewModel.VlanText joins VlanId to VlanNumber.
Desktop overwrites duplicate IDs in response order and falls back to raw IDs;
mobile deliberately uses neither policy: conflicting numbers remain unknown,
identical duplicates are accepted, and unresolved IDs are never displayed as tags.
This is a conservative presentation choice, not a claim about server guarantees.

Ownership recorded before editing: only FdbAsync in the shared loader changes;
OptionalSectionSourceAsync stays unchanged, including its Resources and VLAN
consumers. ARP remains #476 next. Existing Core Vlans.ListAsync supplies the
necessary metadata; no Core change or pin update. All three factories execute
inside the existing helper and reads join concurrently. Only each source's 404
becomes empty. Mapping 404 preserves FDB with VLAN not reported; denial, server
failure and timeout propagate even when FDB itself is missing.

Filter metadata by selected DeviceId before grouping internal IDs. Require a
positive reference and an unambiguous positive VlanNumber. Missing, nonpositive,
foreign-device, conflicting or unmapped references retain the existing explicit
unknown label. No IfVlan fallback, VLAN name, internal-ID display or new action.
MAC/interface formatting, ordering, duplicates, neutral status, routes, search
lifecycle, geometry, persistence and security remain unchanged.

CI fixtures now distinguish internal ID 50 from actual tag 2074, including
ports 404, selected-device isolation, identical/conflicting duplicates, missing
and invalid mappings, synchronous/faulted lookup 404 and non-404 failures paired
with primary 404. Existing context fixtures now supply real mappings and check
actual tags in search, speech and overview. Old read-error regressions and
Resources/VLAN tests remain intact. No local build/test/restore/validation ran;
only git whitespace checks. Full intended diff versus main reviewed against
DESIGN; parent owns fresh review, CI judgement and merge.

Extend native facts check above: use synthetic internal ID 50 mapped to tag
2074 and compare section, overview, search and speech; raw 50 must never appear
as VLAN 50. Exercise missing/ambiguous mapping and all three read failures.
All native evidence remains unverified. Typed-payload completeness is still an
independent limitation, not proven by this semantic fix or Core CI provenance.
