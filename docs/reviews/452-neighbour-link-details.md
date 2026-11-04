# Neighbours list and link details, #452

Refs #452 and #442. Corrective baseline: main c3b584e (Health #519,
Alert rules #520, saved neighbourhoods #521 integrated). This assessment
supersedes the earlier queue/status claims and the exclusion of the network-wide
list. Original #493 and device-neighbours #511 are merged; their stale remote
branches are not replayed. No open replacement #452 PR existed at inspection.
Read CLAUDE.md, DEVELOPING.md, full DESIGN.md and DESIGN-REFERENCES.md,
reconciled assessment and all supplied/live #452 comments, live #442/#288,
#450 latest evidence and #451 acceptance. New neighbourhood remains accepted;
#450 bottom reach remains unverified. No other worktree was changed.

## Modes, sources and preserved contracts

Neighbours is a pinned root or a relative More-pushed page. Constructor performs
the initial fetch; pull refresh explicitly reloads. Actual Appearing rebuilds
saved-group chips, retaining the selected ID unless the definition disappeared.
Central IActivePage activation only restores controls and geometry; it performs
no fetch, group rebuild, query/filter reset or detail replacement.

NeighbourDirectory supplies devices, ports and NetworkLink snapshots.
NeighboursViewModel retains Core matching/FromLinks, deduplication, ordering,
Up/Down filter classification, counts and debounced search across row and LLDP
fields. Filters/query are page-memory state; saved definitions/order remain
AppSettings.NeighbourViews. No storage format or persistence change.

Open passes the exact NeighbourLink via relative neighbourlink. Shell navigation
first offers it to IDetailHost: Neighbours embeds only when ListDetail.IsSplit,
otherwise pushes NeighbourLinkPage. ListDetail keeps its detail while narrowed,
restoring it when widened. Each new selection replaces the snapshot view.
Endpoint commands pass the original integer device ID to relative devicedetail;
Back returns to the actual caller. Embedded view has no Back/glass owner.
Device-scoped #473 rows open a neighbouring device, not this shared detail;
its graph and selection code are untouched. These are all current detail hosts.

Detail preserves complete endpoint names, interface names, announcement fields,
multiline source, unknown ports/states, inactive discovery and neutral unobserved
health from #493. No data fetch or freshness guarantee is invented in detail.
No server writes, permissions, credentials, certificate policy, export, Core API,
settings/editor save contracts or graph semantics change.

## Concern disposition

“Corrected” means source correction, not hardware acceptance. Every native result
below remains UNVERIFIED until tied to the installed final SHA/build/OS.

| Trigger and mode | Observed baseline / expected | Location and disposition |
| --- | --- | --- |
| #452 consolidated header More report; pushed list on tested OS | Header owned caller Back; iOS26+ requires glass Back, pinned root none | NeighboursPage Activate uses PageHeader.IsPushed and actual PreviousTitle; header fallback only Android/old iOS. Corrected |
| Each list action | Neighbourhoods remained header-only; search legacy | Neighbourhoods moves to glass with search on iOS26+, fallback header retains both. Endpoint actions remain scoped to content. Corrected |
| Retained tab/child return/resume | List lacked activation/watch/glass restoration | IActivePage restores idempotent watch and Moved subscription; departure disposes/unsubscribes, queued placement guards active/current page. Pushed detail follows same ownership. Corrected |
| #452 consolidated list base report | Default 12pt margin ignored measured pill base | PlaceControls follows #520: side/bottom base max(0,H-pill.Bottom), bottom-only footer covers actions, top pill uses fallback. Corrected; numeric device delta UNVERIFIED |
| #452 slide/swipe search report | SearchReveal opened from scroll | Scoped ButtonSearch/ISearchHost, no legacy attach, glass/header/Command-F only; completion/unfocus/native drag close retaining term. KeyboardDismiss.OnScroll=False avoids filtering dismissal. Corrected |
| #452 camera-layering comment; inner display | Outer safe-area/padding constrained chip clip | Body/ChipScroll None; full-width clip with internal safe gutters as Devices/Alerts/#520. Chips at rest clear camera/pill; 44pt minimum targets. Corrected; actual camera bounds UNVERIFIED |
| #452 phone detail bottom report; pushed and embedded | Native-frame subtraction and viewport margins obscured ownership | Explicit host clearance; full Root Auto/star Viewport/Scroller. Content padding owns sides/bottom, not pane. Corrected ownership; reported OS root cause and bottom reach UNVERIFIED |
| Long/missing endpoint/ports/metadata, both hosts | Existing #493 wrapping and honest absence | Compliant, unchanged VM/source semantics, no invented healthy/unknown identity |
| Navigation through either endpoint | Relative device route and IDs | Compliant; actual child return/assistive activation UNVERIFIED |
| Fade/initial/final item | List lacked reserved fade; detail already reserves 20pt | Matching input-transparent list fade/header spacer; detail fade matches content gutters. Corrected; native initial/final reach UNVERIFIED |
| Detail loading/error/stale/search/export/save/toast | Snapshot has no such capability | N/A; source list retains busy/error/empty/no-match/refresh and Clear filters |
| Themes, largest text, screen readers, minimum targets, motion | No final device evidence | Source styles/SearchField/ButtonSearch retained; UNVERIFIED native result |

## Geometry and consumer ownership

No shared helper changed. Scoped reuse: PageHeader/BackBar, ButtonSearch,
CoveredEdges, TabBarButtons, ListDetail, central AppShell activation. Consumers
of the only changed shared view are NeighboursPage and NeighbourLinkPage,
both updated together. #450, accepted #451 and #473 graphs are unaffected.

On iOS native page owns top container only; layouts/refresh/scroller own no
additional safe areas; native scroll adjustment is Never. Android retains
page All. List Body occupies the left ListDetail column; trailing star pane
reaches page right/bottom, so clear.Right/Bottom pass directly, avoiding stale
native frames during SizeChanged. Root background and star viewport fill the
actual available pane, with no guessed height or width cap. Detail content
owns 16pt side gutters, 20pt fade reserve and 24pt + covered bottom foot.
List card owns measured base; footer owns only obscured final rows. Top,
bottom and side pills are distinguished. No pane bottom padding is added.

## Validation and final device matrix

Implementation self-inspection: complete diff against c3b584e and DESIGN,
including staged diff and whitespace. No local builds, tests, scripts or agents.
Independent full-diff Claude Opus 5.5 Medium review: PENDING, parent-owned.
CI shared tests, Android, iOS and security: PENDING for revised head. Historical
#493 review/CI does not validate this correction. No new tests mirror layout;
existing semantic tests remain for CI. Device acceptance: UNVERIFIED throughout.

For each row record final PR head SHA, installed build, device/OS, theme/text,
actual/expected and diagnostics. Final SHA is the PR head containing this
assessment; no installed final build or OS result exists yet.

| Matrix (all UNVERIFIED) | Required observation |
| --- | --- |
| Duo outer portrait/landscape; inner portrait/landscape | Pinned root and More-pushed list, narrow pushed link and wide embedded link; fold/rotate without reopening |
| Ordinary iPhone portrait/landscape; Android; old iOS; iOS26+ | Single safe-area owner, fallback header versus glass, actual caller and no duplicate More/Back |
| Tab return, child return, background/resume | One glass owner, query/chips/detail retained without refresh; both endpoints return to link then actual opener; saved-list/editor return retains filters |
| Camera and pills | Capture native page → ListDetail → pane → ContentView → viewport → content bounds; camera reserved bounds, chip clip/content frames and resting tap targets; side/bottom/top pill |
| Base measurements | Same-coordinate H, pill bottom B, list card bottom V, delta V-B; target zero for measured side/bottom base. Detail viewport/background reaches host foot; content can scroll complete final field above actions |
| Data states | Synthetic long/multiline names/interfaces/metadata, monitored pair/unmonitored remote, unknown ports, down device/port, inactive discovery, missing payload/fields; no false OK/zero |
| List states | Cold/refresh busy, empty source/no matches, denied/offline/error retaining data, retry, groups changed/deleted; complete final row reachable |
| Keyboard | Header/glass/Command-F open and refocus; typing/filter replacement/programmatic Scrolled retains focus; native drag/completion/unfocus dismiss retaining term; restored geometry |
| Accessibility and appearance | Both themes, largest text, VoiceOver/TalkBack full names/state and activation once, 44pt targets, Reduce Motion/repeated search transitions, initial fade clearance |

No merge/watch/release/closure/epic ticks. Parent owns independent review, CI
judgement and sequential merge; later TestFlight/device acceptance is separate.

## Independent review and corrective disposition

Earlier #493 assessment and review provenance remain in this file at main
c3b584e (before this correction). Historical results do not validate this head.

Claude Opus 5.5 Medium reviewed the complete ce343cb diff against c3b584e.
Parent substantiated and fixed all four findings: the list fade now lives
inside the card viewport with Surface/SurfaceClear colours and 1pt inset,
rather than painting Background over the card stroke; Header, ChipScroll
and Busy now have 16pt XAML defaults before measured placement; DESIGN.md
records Alert rules/Neighbours ButtonSearch migration accurately; historical
assessment provenance is linked above. No shared helper was changed.

Fresh full-diff review and revised-head CI remain pending. All native results
remain unverified, including first-frame clearance/Back flash, camera clip,
pill-base delta and reported detail bottom reach on the reporting OS.
