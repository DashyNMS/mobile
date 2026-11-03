# Device FDB review, #475

Refs #475 and #442. Baseline current main 0416232438241593489c83ec9acb370b52eb31ba.
Read live issue/epic, CLAUDE.md, DEVELOPING.md, DESIGN.md,
DESIGN-REFERENCES.md and prior section assessments #469–#474. #442 is the
review epic; #422 is a dependency PR, not this epic.

## Assessment and changes

| Concern | Evidence and disposition |
| --- | --- |
| Modes | Compliant: grouped MAC table, local search and three overview highlights. No FDB editor, row destination, graph, sort or filter panel. Adaptive list supports one to three columns. |
| MAC/interface/VLAN hierarchy | Fix: DeviceSectionPage legacy grid truncates MAC and caps VLAN to 180pt/two lines beside interface. FDB-only stack wraps each fact. Overview formerly omits interface and truncates MAC; now uses the same complete stack. |
| Missing context | Fix: absent/nonpositive VLAN silently disappears. Display VLAN not reported without changing raw Value or guessing untagged membership. Unknown interface retains existing port-ID fallback. |
| Ordering and server facts | Compliant: endpoint is scoped by selected device, join ports by PortId, order by interface display name then raw MAC, ordinal ignore case. Preserve MAC formatting, exact interface display name, positive VLAN label, duplicate entries and neutral status. No inferred health or port navigation. |
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
