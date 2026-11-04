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

## 4 November reassessment after PR #537

Baseline `f08148946ae6cbb962efde48bf4de413bfeb0b72`, clean isolated
`t3/device-arp-review-fixes`. Live #476 and #442 remain OPEN with no comments;
no open ARP PR was found. The reconciled assessment's #476 checkpoint is
mandatory review scope, not evidence of a reported defect. PR #514's actual
squash `d9353cc6aed1cea3a9f218e1d377578d0e104960` is the implementation
baseline; its head `872e9d107fb0ef7fd02535e806c3f7e0158326de` is not replayed.
Historical green CI proves neither geometry nor acceptance. The old assessment
above is retained as history; the dispositions below supersede its source and
geometry compliance claims.

C means source-compliant, correction means a confirmed defect corrected here,
N/A means absent capability, U means unverified. Every native outcome remains U.

| Concern | Disposition and source evidence |
| --- | --- |
| Sources and failure states | Correction: ArpAsync started raw tasks under LoadAsync's outer 404 catch. ARP 404 could hide port 403/500/timeout/cancellation; ports 404 discarded valid ARP. Both reads now use existing OptionalSectionSourceAsync before WhenAll. Only 404 becomes empty; all other failures/cancellation propagate. Valid ARP retains port ID when mapping is missing. Both missing remains empty. No fleet request or stronger permission dependency added. |
| Arbitrary MAC text | Correction: Mac stripped all nonhex characters and normalised any twelve remaining digits, potentially inventing an address from explanatory text. ARP now normalises only compact, consistently paired colon/hyphen, or dotted hex spellings. Other source strings remain verbatim. FDB's existing formatter is unchanged. |
| Provenance and completeness | C mapping, U completeness: local Core HEAD is `5f7bde53ac05ea75f7cb6dd06a8fae62606f0769`, supplied Core CI run 52 provenance. ArpApi.ListForDeviceAsync requests resources/ip/arp/all?device=id and reads collection arp through LibreNmsTransport.GetCollectionCoreAsync. ArpEntry binds id, port_id, device_id, mac_address and ipv4_address. Strings are nullable; IDs are nonnullable, so omitted IDs become zero. Missing/null collection becomes empty; non-array throws. Unknown payload fields are not represented. This does not prove complete typed payload coverage, IPv6 endpoint support, or missing-versus-zero identity. No new meaning is assigned to those values. |
| IP/MAC/interface facts and order | C: raw IP is the title, nullable IP/MAC gets named missing text through SectionRow; PortId joins Port.DisplayName with port ID fallback. IpSortKey numerically orders parsed address bytes, stable for duplicate keys, invalid/missing after parsed values. IPv6 text can be displayed without claiming IPv6 endpoint coverage. No address validation, health inference, row action or deduplication introduced. Malformed IP text is retained. U complete real-server payload coverage. |
| List/overview hierarchy | C source: ArpLayout stacks all three facts with wrapping in DeviceSectionPage and DeviceDetailView, same ArpDescription; card count and three neutral highlights retained. U actual wrapping and unbroken strings at largest text. |
| Loading/empty/error/offline/cache/refresh | C source: VM RunAsync owns busy/error/Retry; IsEmpty excludes errors/busy, unmatched query differs from source empty. ARP keeps last rows on failed refresh, with no persistent cache or freshness claim. Generation guard prevents old device publication. Overview uses the same loader and Failed card state. U observed offline/retry/resume timing. |
| Search and keyboard | C source: ButtonSearch, SearchField, native drag check, retained query, Command-F; Matches includes full IP/MAC/interface and missing labels. Filtering must retain focus. U keyboard show/dismiss and native drag/focus. |
| Viewport, fade, glass and usable base | U native: PR #530 supersedes the old claimed bottom geometry. PlaceControls measures SectionBody and pill/glass in host coordinates, extends viewport to measured base with content footer clearance, keyboard takes precedence; iOS list adjustment Never. Matching fade/list margins and 20pt spacer remain. No measured H/B/V or last-row evidence exists. |
| Lifecycle/navigation | C source: selected device relative section route from overview; actual caller Back, no row destination. Activate restores search/glass and measured placement; OnDisappearing disposes edge/keyboard subscriptions, closes search and releases glass. U repeated tab return, resume, fold and rotation. Devices narrow/split, Dashboard, alerts and other device links remain entry routes. |
| Accessibility/theme/motion | C source: joined ArpDescription excludes repeated child announcements; themed labels, nonactionable rows, accessible overview navigation; shared reduced-motion search. U VO/TB grouping, action traits, contrast, largest text, touch and keyboard operation. |
| Forms/actions/persistence | N/A Save, drafts, toggles, selection, comparison, export and mutations. Credentials, permission storage, persistence and existing exports untouched. No new action hierarchy or unsettled visual policy implemented. |

Before editing, dependencies/consumers were recorded: DeviceSectionLoader is
shared by overview and full sections. OptionalSectionSourceAsync is already
used by Resources, VLANs and FDB; it is reused unchanged. ArpMac is ARP-only.
SectionRow, both templates, DeviceSectionCard, shared page geometry, search,
Core package and navigation are unchanged. Other section consumers are
Availability, Sensors, Resources, Ports, Neighbours, VLANs, FDB, Routing,
Wireless, Inventory and Event log. Graphs, Graylog and backups use separate
routes. Accepted closed pages and #405/#288 retain their contracts. No other
checkout, including the preserved dirty #475 checkout, was modified.
DESIGN already records independent-source handling, metadata permissions and
full source text; no duplicate lesson or visual rule is added.

CI-only regressions cover synchronous/asynchronous missing ARP plus port
403/500/timeout, cancellation, missing ports retaining valid/malformed/missing
facts and overview highlights, both sources missing, and MAC text preservation.
Existing numeric/duplicate ordering, scoped search and refresh retention tests
remain. No local build, restore, tests or validation scripts ran. Full-diff
self-review and staged whitespace checks are separate from pending CI and
parent independent Opus 5.5 Medium review.

All native verification remains pending at the eventual integrated SHA/build:
Duo outer and inner portrait/landscape, ordinary iPhone portrait/landscape,
both themes, largest text, VO/TB, physical keyboard and search keyboard
show/dismiss, fold/rotate, tab return and resume. For each host record exact
host height H, measured pill base B, viewport base V and separately whether the
last complete row can scroll clear of glass. Check initial fade clearance,
short/long lists, overview and pushed routes, all loading/empty/error/offline/
refresh states and full facts using RFC 5737/3849 addresses and synthetic MACs
and interface names. No hardware pass, issue closure, epic tick or release is
claimed. Parent owns independent review, CI judgement and sequential merge.

Opus 5.5 Medium independently reviewed the full diff at 5e111ea and found no
correctness, security or scope defects. Its sole low test-gap finding is fixed:
the additional free-text fixture port 020000000001 leaves exactly twelve hex
digits under the old stripping algorithm, so it directly reproduces the false
address conversion. Production code is unchanged from that reviewed head.
Final-head CI remains pending; every native outcome above remains unverified.
