# More navigation and pinning assessment

Refs #453 and #442. Baseline main `83e9a39`, including merged #490.
Read live #453/#442/#454, CLAUDE.md, DEVELOPING.md, DESIGN.md and
DESIGN-REFERENCES.md. FULLDESIGN.md and AGENTS.md are absent. This document
records source assessment, not hardware acceptance.

## Assessment before implementation

| Concern | Baseline assessment | Finding and resolution |
| --- | --- | --- |
| All pages and section headings | Compliant catalogue; heading semantics need a fix | AppPages supplies eleven unique pages in Monitor, Network, Logs, App. Add level-two headings. |
| Titles and summaries | Fix required | Text and chevron have separate tap areas. Replace with one wrapping, whole-area Open action, joined description and route-specific hint. Exclude child labels from duplicate announcements. |
| Pin action and state | Fix required | Icon fill alone conveys pin state. Show pinned/not pinned and full-bar reason in text. Use a native secondary Pin/Unpin button with target-specific accessible description and at least 44pt height. |
| Capacity and unavailable action | Fix required for presentation; compliant logic | Three default pins fill the bar; baseline dimming omits a visible reason. Keep the explanation dialog when pinning cannot succeed, and show the reason beside the page. Unpin stays available. |
| Ordering/reordering | Compliant contract; explanation needs a fix | Saved pins are sorted by enum order, independent of tap order. Explain fixed order. No drag/menu reorder is offered or introduced; changing ordering would alter the storage contract. |
| Selection/tab identity | Compliant source; native result unverified | More is the selected tab while this root is visible; pinned state is distinct from selected tab. Opening a pinned page selects its tab. No duplicate selected state is invented for catalogue rows. |
| Entry and return routes | Compliant source; native result unverified | Preserve MoreViewModel, AppPages.Resolve, ShellNavigationService, relative descendants and actual-caller Back. |
| Immediate persistence/cold launch | Compliant logic; caption needs a fix | Add automatic-save caption. Preserve preference format, defaults, empty selection and unknown-name handling. |
| Insets, footer and fade | Fix required | Baseline native-safe-area ScrollView has no measured glass clearance. Use top-only iOS native safe area, disable scroll adjustment, and give measured sides/footer to the content. Fixed header and scroll viewport share gutters. Fade matches Background, viewport width and a 20pt content reservation. |
| Retained activation/lifecycle | Fix required | More root does not clear a departing owner's actions. Opt only More into #490 IActivePage, claim action-free glass on activation, dispose edge watch and remove Moved on departure. |
| Empty/error/loading/search | Compliant/not applicable | Fixed local catalogue is never server-empty and has no fetch, refresh or search. Zero pins is a valid state with every page still reachable. Full capacity has explicit feedback. Do not add search or setup changes. |
| Long text, themes and screen readers | Unverified on hardware | Wrapping labels, vertically separated actions and semantic descriptions address source gaps. Device matrix below remains required. |

## Modes, routes and contracts

Inspected MorePage, MoreViewModel/MoreItem/MoreGroup, TabPins/AppPages,
AppShell XAML/code, ShellNavigationService, IActivePage, PageHeader, BackBar,
CoveredEdges and the #490 correction assessment. Only the More page and model
change; no shared shell, pin service, route or storage change.

Modes are default three pins, one/two pins, no pins, full capacity, and More
returning after a page push, tab switch or resume. Every AppPage remains in its
existing group, even when pinned. Devices, Alerts and Health are defaults;
Dashboard and More are fixed roles outside the pin catalogue. Settings stays
in App and remains eligible for pinning.

More opens each pinned page with `//main/{TabRoute}` and each unpinned page
with its relative PushRoute. External absolute tab requests resolve through
More when unpinned, retaining suffix/query. Relative alert/rule/template/detail
and Back routes pass unchanged. Descendants return to their actual caller;
they are never forced to an Alerts tab default. Groups/location filters keep
their actual keys; no entity ID is converted or changed by this work. Device,
port, rule and other integer entity routes remain owned by existing callers.

`tabpins` remains a local comma-separated enum-name preference, sorted by
AppPage order and capped at three. Missing means defaults; saved empty means
none. Unknown/duplicate names are filtered as before. There is no migration,
server write, credential change, security change or new storage. ArrangeTabs
continues retaining unchanged Tab objects and their stacks. More only updates
its existing item objects, preserving scroll content identity after pin changes.

Top native safe area owns the iOS top edge; CoveredEdges plus 16pt gutters own
sides and footer. Android retains native safe areas. The finite star viewport
contains a ScrollView of the fixed eleven-entry catalogue, so virtualisation is
not needed. No ReadableColumn cap remains. Header is outside scrolling content;
20pt fade and spacer live within the same viewport. No Back is added to the
More root. Page-owned HandlerChanged/Loaded subscriptions last with the page;
edge watch and global Moved subscription are paired with visibility. No native
tokens or Window subscriptions are added. AppShell retains #490's paired Window
resume delivery and resolves the active page at dispatch time; descendants
continue claiming their own glass.

## Dependency and overlap

Queue remains #489, #491, #492, #493, #494, then this PR. None of those branches
is changed. Parent updates only this PR after earlier queued PRs merge. Preserve
#490 AppShell/IActivePage/BackBar boundaries and #489's full pushed alert wrapper.
No shared AppShell/TabPins change requires consumer migration. Existing route
regressions cover relative alert descendants; native retained stacks still need
device checks. #454 owns Settings overview/About next; completed #405/#412 setup
and #288 global theme/mock-up work are not duplicated. TestFlight follows
#453–#454, under parent control.

## Evidence and remaining acceptance

Code: full intended diff self-inspected against the findings and design guide;
whitespace check recorded at handoff. Added meaningful shared regression for
full/available/pinned feedback and notifications, pinned route selection and
restored preferences. Existing tests cover fixed order, empty pins, unknown names,
capacity rejection, query preservation and relative nested-alert/Back routes.
No local builds, tests or scripts run by instruction. Core remains pinned at
2.0.0-ci.49; no Core API or package changes.

CI: shared tests, Android/iOS build and security results pending on the PR.
CI is not device evidence. Mandatory parent Claude Opus 5.5 Medium full-diff
review remains pending after quota resets, one PR at a time in queue order.
This implementation child does not delegate, review, watch or merge.

All device outcomes below are unverified. Record head/build/OS, each result and
diagnostics separately. Use synthetic example.net data only. Run every row on
Duo outer, inner portrait, inner landscape and ordinary iPhone, in light/dark,
normal/largest text, TalkBack/VoiceOver, including fold/rotate and foreground
resume. No issue closure, epic ticks or release is authorised by these notes.

| Scenario | Exact expected result |
| --- | --- |
| Default/full bar | Three pins; Dashboard first and More last. All eleven pages and four headings readable. Unpinned rows explain capacity; Pin gives the existing full-bar dialog without changing pins. |
| Unpin and pin | Unpin Health, then pin Map. Count, every capacity reason, action label and semantic description update once. Cold launch retains Devices/Alerts/Map in enum order. |
| Empty/saved pins | Unpin all; relaunch retains none. Dashboard/More remain, every page still opens and returns. Unknown saved names remain ignored. |
| Ordering | Pin in a different sequence; enum order remains, matching explanation. No reorder affordance promises unsupported behaviour. |
| Selected tab | Open each pinned page from More; correct tab selected. Return to More. Pin/unpin while More selected keeps More selected and unchanged tab objects retain content. |
| More retained navigation | More to unpinned Groups, Groups to pinned Devices, then More. Retained Groups reclaims its actions without refresh; return to root clears them. Repeat unpinned Devices and relative Back, rapid push/pop and resume. Exactly one owner throughout. |
| Nested actual caller | More to unpinned Alerts/rules, alert to rule/template/device, then Back through actual callers. Repeat with pinned Alerts and wide embedded detail. Preserve integer IDs, filters, query and selected tab. |
| Long text/large text | Names, descriptions, state and capacity reasons wrap; Open fills its area; Pin/Unpin remains reachable and at least 44pt. No header-count collision. |
| Screen readers | Each Open announces name, summary and pin state once, with correct route hint. Pin/Unpin names its target, activates once, and updates feedback. Section headings navigate correctly. |
| Geometry/themes | First content clears 20pt fade; last Settings action scrolls above bottom glass. Side pill/camera clear both header and content. Fade matches viewport and background with no dark partial bar; taps pass through. |
| Fold/rotate/resume | Recalculate gutters/footer without reopening; no duplicate insets, watchers or stale glass actions. More root resume reclaims action-free ownership; pushed pages keep their Back/search. |

User acceptance remains pending for each matrix row.
