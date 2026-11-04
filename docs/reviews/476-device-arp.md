# Device ARP review, #476

Refs #476 and #442. Baseline main
2393422164afa149a4f3b0d01969c35e79f5335f after merged #475 / PR #513.
Read live issue/epic, CLAUDE.md, DEVELOPING.md, full DESIGN.md,
DESIGN-REFERENCES.md and prior section assessments #469–#475.
Routine guide choices were authorised.

## Assessment and implementation

| Concern | Evidence and disposition |
| --- | --- |
| Modes | Compliant: grouped ARP list, local search, three overview highlights; adaptive one-to-three-column list. No editor, selection, row destination, graph, sort or filter panel. |
| Full facts and hierarchy | Fix required: legacy title truncation and 180pt/two-line interface cap squeeze address context. ARP-only stack wraps IP, MAC and interface independently. Overview previously omits MAC and truncates IP; now uses the same full readout. |
| Missing data | Fix required: absent IP/MAC is an unexplained dash; missing port mapping disappears. Named unavailable address text preserves raw row fields; unmapped ports show port ID, like FDB. Blank interface values say Interface not reported. No inferred health or port graph action. |
| Addresses/order | Compliant: existing IpSortKey parses address bytes into padded numeric components; invalid/missing addresses follow valid ones. Preserve raw IP spelling, stable duplicate order and existing Mac formatting (including unusual raw values); no new address validation or filtering. |
| Source contract | Compliant: selected-device ARP and ports reads run concurrently; both must succeed. Join by PortId. No endpoint, permission, credential, Core package or persistence change. |
| Search/keyboard | Compliant existing ButtonSearch/header/glass/Command-F, native drag dismissal and retained query. Matching covers IP/MAC/interface; add ARP-only matching for displayed missing labels. No shared helper edits; filtering must retain focus. Physical keyboard and drag checks unverified. |
| Loading/empty/error | Compliant source: first-load BusyCard, finite CollectionView, source empty distinct from Nothing matches, errors suppress empty, busy-disabled Retry. Loader 404 handling remains existing empty behaviour; other failures propagate. |
| Refresh/offline | Compliant existing last-loaded-row retention on refresh failure; no persistent ARP cache or freshness claim. Generation guard prevents old device completion from publishing in new scope. Timing, resume and offline sequences unverified on hardware. |
| Insets/final content | Compliant source: top-only native iOS safe area, native list inset Never, measured 16pt plus side clearance, viewport above bottom glass plus 12pt, 24pt footer and list-column fade with 20pt spacer. No new inset owner. Physical reachability unverified. |
| Accessibility/theme/motion | Fix: joined complete ARP description and excluded child labels avoid duplicate facts. Existing themed styles and Reduce Motion search retained; no new animation. VoiceOver/TalkBack grouping, unbroken strings, contrast and native action traits remain unverified. |

## Routes and preserved contracts

DeviceDetailViewModel.OpenSectionAsync opens relative Routes.DeviceSection with
selected DeviceId, Arp and display name. AppShell registers DeviceSectionPage;
ApplyQueryAttributes passes that scope to DeviceSectionViewModel. Detail may
originate in Devices narrow/split views, Dashboard, alerts or other device
links. Header/glass Back uses the actual previous page and relative '..'.
ARP rows have no destination; the overview card still opens the whole ARP
section through its existing single gesture. No Save, writes or immediate
mutation, copy/share or settings contract is introduced.

DeviceSectionCard keeps count, empty-section hiding, failure summary and its
three-highlight selection. Overview readouts add no nested action. Raw row
Title/Subtitle retain existing IP and MAC representation; interface DisplayName
is unchanged when mapped. Missing mapping now retains PortId instead of null.

Shared-file ownership is limited to additive ArpLayout, ARP-only search and two
specialised templates with independent legacy-grid hide triggers. All other
rows default false. Availability percentages/outages, sensor classes/limits/
freshness, resource bars, port sorting/grouping/graph ifName, neighbour graph/
reverse links, VLAN counts, FDB facts, routing states, wireless formatting and
inventory expansion/serial controls remain unchanged. Shared lifecycle, Retry,
ButtonSearch, fade and request guards from #469 remain intact. Graphs/logs/
backups keep specialised routes. #405 setup and #288 global sweep are unaffected.
DESIGN.md already covers complete facts, wrapping, missing states, navigation,
insets and acceptance; no grounded reusable rule gap required an update.

## Validation and acceptance

Full intended and staged diffs inspected against DESIGN.md; whitespace checked.
No local tests or builds run. Added CI regression coverage for numeric ordering,
duplicates, long interface, unmapped port identity, unusual/formatted MAC,
neutral/nonactionable rows, complete overview highlights, missing-value text and
section-isolated search. Existing ARP filtering/card and loader failure coverage
remain relevant. Shared-test and Android/iOS CI are pending; parent obtains the
mandatory Claude Opus 5.5 Medium review. No Claude or delegation invoked.

Every hardware check below is unverified. Record final integrated SHA and
TestFlight build with each result; CI alone cannot establish visual acceptance.
Use synthetic example.net/edge.example.net, device IDs 7/8 and 192.0.2.0/24.

| Check | Exact remaining evidence |
| --- | --- |
| Facts | Long/unbroken IP, MAC and interface, unusual MAC, duplicate IP on different ports, missing IP/MAC/interface and unmapped port; overview and full list agree, numeric .9 before .100. |
| States | Cold loading, empty success, unmatched query, denied/offline/timeout/404 on each required endpoint; refresh failure/success/Retry; slow device 7 then 8; no false empty or freshness claim. |
| Search | Header/glass/Command-F only; IP/MAC/interface/missing-label terms; typing retains focus, native drag/completion/unfocus close retaining query; final result reachable with keyboard. |
| Text/theme | Both themes, largest text, short/long lists and long device title; initial content below fade, complete final row above side/bottom glass. |
| Screen readers | VoiceOver/TalkBack announce full facts once with no false row action; overview card opens section once; Back/search/Retry reachable. |
| Duo | Outer portrait, inner portrait/landscape, supported outer landscape; fold/unfold and rotate during loading/search; columns and insets update without reopening. |
| Routes/resume | Devices narrow/split, Dashboard and alert → device → ARP → Back; retain actual caller/query; background/resume at each level with one glass owner. |
| iPhone/motion | Ordinary iPhone portrait/landscape, older header and iOS 26 glass where available, largest text, keyboard dismissal, Reduce Motion and repeated search transitions. |
| Diagnostics | Inspect Tab bar buttons and Top line entries for overlap or placement loops during these transitions. |

No issue closure or epic ticks. Parent owns CI, independent review, merge,
cleanup and final-batch TestFlight; user hardware acceptance remains required.
