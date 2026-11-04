# More redesign assessment, 4 November 2026

Refs #453 and #442. Implementation baseline `765b02c`, current main after
#519–#522. The user explicitly said "Approve as proposed" for
`/tmp/mobile-442/more-proposal.html` and `more-proposal.md`: quiet headings,
hairline rows, five retained AppPages summaries, outlined/filled trailing pin
and a single full-width catalogue at both widths. This approves the visual
proposal only. The previous device layout was rejected on both screens.

## Current scope assessment

| Concern, location and trigger | Disposition |
| --- | --- |
| MorePage catalogue, all widths | Corrected: lightweight rows replace cards. All eleven titles and four groups remain in enum order. Settings remains pinnable. |
| MoreItem summaries | Corrected: only Health, Map, Network map, Neighbours and Graylog show their exact AppPages.Description. Other source descriptions remain unchanged. |
| MorePage Open/pin actions | Corrected in source: sibling native buttons, wrapping text, 48pt minimum Open height and separate 44×44 pin. Decorative labels/path excluded. No enclosing row gesture or repeated visible Pin prose. Native focus and activation remain UNVERIFIED. |
| MoreItem state, full/available/pinned | Corrected: names include target, membership and action or capacity explanation; CanToggle now notifies PinDescription too. Open speaks only retained summary and membership, with route hint. Global capacity line updates with pins. Full-capacity controls remain operable and retain the explanatory dialog. |
| MoreViewModel ordering and persistence | Compliant: three slots, fixed Dashboard/More, immediate Preferences writes, default Devices/Alerts/Health, enum sorting, missing versus saved-empty and invalid-name filtering unchanged. Zero pins valid. |
| AppPages/ShellNavigationService/AppShell entry and return | Compliant source, native UNVERIFIED: pinned destinations select existing tabs, unpinned use PushRoute, Resolve retains suffix/query, relative descendants return to actual caller. Pinning does not navigate. Unchanged Tab objects retain stacks. |
| MorePage activation, tab/child return and resume | Compliant source, native UNVERIFIED: IActivePage claims action-free glass, updates existing items, idempotent watch/Moved, paired disposal. No new global subscriptions. |
| MorePage safe areas and usable viewport | Compliant source policy, native measurements UNVERIFIED: iOS page owns top only; host star row owns available viewport height, with no extra viewport bottom margin. CoveredEdges owns measured side clearance plus one 16pt gutter; content footer owns 16pt plus measured bottom coverage. Android retains native Container safe areas. No new second inset owner or fixed native offsets. Measure native host/viewport bottom and usable base, not just final-row nonoverlap, for side, bottom and top pills. |
| Fade, first caption, final Settings row | Compliant source, native UNVERIFIED: retained input-transparent 20pt fade and initial reserve within viewport; endpoints use BackgroundLight #F4F6F9 and BackgroundDark #11141A, matching page background, including transparent RGB. Footer lets final row clear glass. |
| Themes, text and contrast | Source uses existing Text/Caption tokens and PrimaryTextLight #1D4ED8 / PrimaryTextDark #60A5FA pin against page background, without capacity dimming. Shape and speech identify state independently of colour. Native contrast, target measurement and largest-text reach UNVERIFIED. |
| Loading/error/search/keyboard, security | N-A: fixed local catalogue, no fetch, search or keyboard. No server, credentials, permissions, source data or preference schema changes. |

Only MorePage, its scoped model presentation, existing navigation regression
assertions and design/review notes change. Shared icon geometry is reused without
editing Icons/IconButton or SettingsOverviewRow. #454 owns Settings; #475's
separate worktree and #515's fixed-action guidance are untouched. No accepted
setup page or shared consumer is changed. The reusable catalogue rule is in
DESIGN.md; no device acceptance is asserted there.

## Validation and exact remaining device procedure

Full intended diff versus current main and DESIGN was self-reviewed; whitespace
inspection passed before commit. Independent full-diff code review is
PENDING with the parent. CI shared tests, Android/iOS builds and security checks
are PENDING. Existing state/route/persistence regression assertions were updated
for the new capacity-dependent accessible names. No local builds, tests or
scripts were run. CI is not device evidence.

Every device outcome is **UNVERIFIED**. Use the final PR head SHA from handoff;
record that exact installed SHA, build number and OS version beside every
result. No build/OS is available on this code-only machine. Test synthetic
example.net data only. Run each applicable step on Duo outer, inner portrait,
inner landscape and an ordinary iPhone, light/dark, normal/largest text and
TalkBack/VoiceOver, then fold/rotate, tab-return, child-return and foreground
resume without reopening.

1. Cold launch with missing preferences: Devices/Alerts/Health filled, all
   eleven routes reachable, Dashboard first/More last and More selected.
   Read each heading, Open title/retained summary/state/hint and pin name once.
   Activate each separately, ensuring one action and no navigation from pin.
2. At full capacity activate Map's outlined pin: exact existing full-bar
   dialog, no saved change. Unpin Health: capacity and Map's available name
   update. Pin Map: fill/name/count update, More remains selected. Restart:
   Devices/Alerts/Map restored in enum order. Repeat Settings as a pin.
3. Unpin all and restart: saved zero remains zero. Compare missing preference
   defaults with saved empty, unknown/duplicate/invalid preference names using
   a controlled synthetic test install. Every catalogue route still opens.
4. Open every pinned tab and unpinned route, return to More; push relative
   alert/rule/template/device descendants and Back through actual callers.
   Smoke Dashboard, Devices, Alerts and previously accepted setup pages.
   Preserve route IDs, query/filter, selected tab identity and retained stacks.
5. Return from child/tab and resume with More root versus a retained child:
   exactly one glass owner, correct Back/search on child and no stale root
   actions. Repeat rapid push/pop and fold/rotate without Refresh.
6. Measure page native bounds, host/viewport bounds and pill frame, identifying
   each inset owner. Record usable viewport bottom relative to host and pill
   bottom for side/bottom/top pills and camera cases; reject wasted bands even
   if nothing overlaps. Confirm initial caption below 20pt fade, complete
   wrapping and final Settings Open/pin reachable above glass with no duplicate
   inset. Check targets at native scale and contrast in both themes.

Historical assessment below is retained verbatim as evidence of the earlier
implementation/review, not the current layout or current CI/device status.
Its per-row state prose and vertical Pin buttons are superseded by the approved
proposal. Historical review at 2946d88 and associated checks do not validate
this redesign. Issues remain open until user device acceptance.

---

# More navigation and pinning assessment

Refs #453 and #442. Baseline main `ad42d71`, including merged predecessors through #494.
Read live #453/#442/#454, CLAUDE.md, DEVELOPING.md, DESIGN.md and
DESIGN-REFERENCES.md. This document
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

Predecessors #489–#494 have merged; this PR is updated against main ad42d71.
No other queued branch is changed. Preserve
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
2.0.0-ci.52; no Core API or package changes.

Claude Opus 5.5 Medium reviewed the full diff at 2946d88. Its medium finding
was addressed by declaring the page's native Container safe area on Android;
the existing iOS top-only override remains. Its low documentation finding was
also corrected. Routes, pin storage, activation and glass ownership were confirmed.
Unit tests and CodeQL C#/actions passed at that head; platform builds and Swift
analysis were pending at review time. Replacement CI remains pending.
CI is not device evidence.

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
| Android safe areas/TalkBack | With gesture and three-button navigation, header and first caption clear the status bar, final Settings Pin clears the navigation bar, and TalkBack reaches and activates both actions. Repeat large text and rotation. |
| Geometry/themes | First content clears 20pt fade; last Settings action scrolls above bottom glass. Side pill/camera clear both header and content. Fade matches viewport and background with no dark partial bar; taps pass through. |
| Fold/rotate/resume | Recalculate gutters/footer without reopening; no duplicate insets, watchers or stale glass actions. More root resume reclaims action-free ownership; pushed pages keep their Back/search. |

User acceptance remains pending for each matrix row.

## CI failure and independent review disposition

Both platform CI builds at 888805b failed MAUIX2002: SourceGen cannot convert
Icons.Pin's x:Static string into Path.Data geometry. Parent fixed this using
the exact inline path with shared-source provenance comment; no icon semantics
changed. No local builds/tests were run.

Opus 5.5 Medium full-diff review at 888805b found that blocker and a medium
single-line title alignment mismatch; both are fixed. Parent also fixed muted
unpinned versus accent pinned stroke, round caps/joins, press/focus visual
feedback, More-only placement/exception wording under accessibility guidance,
and exact five-summary regression coverage. Unused compatibility PinState and
PinLabel properties are retained; they do not affect presentation. Pin remains
vertically centred for a stable native target; largest-text alignment/reach
requires explicit device verification rather than a guessed font-size threshold.
Fresh full-diff review and replacement CI remain pending, all device outcomes
unverified. Approved proposal is not device acceptance.
