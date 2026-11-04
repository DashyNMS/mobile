# DashyNMS Mobile page design spec

Updated 4 November 2026. Use this spec when creating, changing or reviewing
any mobile page, including tabs, pushed pages, forms, pickers, editors,
canvases and detail panes. Rules describe the target design; the pattern
table distinguishes existing implementations awaiting migration.

For an existing setup page or dashboard editor, also read its contract in
[Page contracts and acceptance evidence](DESIGN-REFERENCES.md). Consult that
reference for historical acceptance; keep reusable design rules here.

## Implementation workflow

1. Classify the page using the pattern table. Read its reference, shared
   controls and view model. Identify every entry route and whether changes
   save immediately, on Save or through a separate immediate action.
   Finish with each entry/return path and persistence contract accounted for.
2. Identify the owner of each inset: native page safe area, native scroll inset,
   content padding or overlay margin. Choose one owner per edge. Finish when
   every tappable control and the final list item can clear system and glass UI.
3. Implement the relevant rules below. Reuse shared controls and preserve the
   page's persistence contract. Finish when resizing, navigation and keyboard
   dismissal all update the layout without duplicate controls or insets.
4. Validate changed behaviour with relevant shared tests and Android/iOS
   builds through the PR process. On a code-only machine, use CI for these
   checks. Inspect available layout diagnostics. Finish with each check
   recorded as passed, failed or unavailable.
5. Before merging, have the coding agent review the PR's full diff against
   this guide: `/code-review` in Claude Code, `/review` in Codex, or the same
   request in plain words elsewhere. Fix confirmed findings in the PR, or note
   in the PR why one stands. A review does not replace CI or device checks.
6. Record the acceptance checklist with code review, CI and device results
   separately. Finish device-gated work only after the user's explicit
   acceptance; use non-closing PR references until then. Close only the
   accepted issues and tick their epic entries, leaving other work pending.

### Batch delivery

The user may authorise a batch of page issues for one device test. A batch
changes when device verification happens; acceptance history belongs in
DESIGN-REFERENCES.md:

- Keep one issue per PR with a non-closing `Refs #NN`. Each PR passes CI and
  the step 5 review before squash-merging; a code-only machine runs no local
  builds or tests.
- When managing a batch sequentially, finish each PR's branch update, CI and
  merge before updating the next. Follow the agreed issue order and land
  dependencies before their consumers.
- Land a shared-control change before the pages using it, in its own PR or the
  first consumer's. Name the accepted pages it affects and keep their contracts.
- Build one TestFlight after the final merge. The user accepts or rejects each
  issue separately; issues and epic boxes stay open until then.
- A rejected page gets a follow-up PR referencing its issue. Record accepted
  results in DESIGN-REFERENCES.md afterwards.

## Choose the page pattern

Paths in this table are under `src/DashyNMS.Mobile/`.

| Page type | Reference | Layout contract |
| --- | --- | --- |
| Map or canvas tab | `Pages/NetworkMapPage.xaml` and `.xaml.cs` | Background fills the screen; safe controls overlay it |
| Scrolling dashboard | `Pages/DashboardPage.xaml` and `.xaml.cs` | Content scrolls beneath a bottom bar; its column clears side glass |
| Searchable list with detail | `Pages/DevicesPage.xaml` and `.xaml.cs` | Virtualised list, footer clearance, optional detail column |
| Pushed detail | `Pages/DeviceDetailPage.cs` | Back returns to the actual previous page; detail takes glass ownership |
| Dashboard layout editor | `Pages/CustomiseDashboardPage.xaml` and `.xaml.cs` | Adaptive virtualised grid, measured side margins and footer clearance |
| Setup form or picker adopting #406 | `Controls/SetupLayout.cs`, `SetupPageLayout.cs`, `Pages/GraphPickerPage.xaml` | Fixed header, finite viewport, optional fixed action; explicit inset ownership |
| Setup pages awaiting migration | `Controls/DashboardBackNavigation.cs` and its callers | Keeps native page safe areas and adds only extra glass clearance |

The final row describes the form implementation retained after #391. The editor
moved to the scrolling pattern in #401. A form migration needs its own layout
review; copying the editor's inset calculation into the form helper would count
native safe areas twice. The agreed target for setup-page unification is recorded below and in #405.
This table is a pattern index. DESIGN-REFERENCES.md records accepted pages
and remaining implementation gaps.

## Forms and fixed primary actions

Use this pattern for draft forms and pickers. Preserve each existing page's
contract as recorded in DESIGN-REFERENCES.md.

- Forms fill the available width with 16pt gutters and measured camera/glass
  clearance. Remove form-only ReadableColumn/680pt caps.
- Draft forms have one primary Save fixed at the base of the page, outside
  scrolling content. With a vertical side pill, align the button's bottom
  with the pill's bottom. With a bottom bar, keep Save above glass actions.
  Treat an iPad top pill separately from a bottom bar.
- The form/list viewport clears the fixed action. Keep the final field and
  focused entry reachable with the keyboard open; restore pill alignment
  after dismissal. Use the shared policy rather than per-page fixed spacers.
- Share heading, section, field, search and status presentation across forms
  and pickers. Keep virtualised lists height-constrained.
- Immediate-save settings retain their behaviour and need no Save button.
  Change persistence only with an explicit decision recorded in the issue.
- Use the implicit primary `Button` style for the main Add or Save action;
  it owns primary colours, contrast and disabled states. Use secondary
  buttons for subordinate actions.

### Action hierarchy

**Every fixed primary action, including “Got it”, must align its bottom edge
with the base of a vertical menu/tab pill. Use measured shared placement, with
no extra bottom margin or safe-area padding lifting it above that base. For a
bottom menu bar, clear the bar and its glass actions; treat a top pill separately.
Verify this on inside and outside screens, after folding/rotation, keyboard
dismissal, tab return and resume. A page fails visual acceptance if this
alignment is wrong.**

Each page has at most one primary action, in the fixed action slot. Remove
any header or in-form copy of it. Other actions take the lowest role that fits:

| Role | Examples | Placement and behaviour |
| --- | --- | --- |
| Primary commit | Save, Add 3 cards | Fixed action slot, implicit `Button` style; label names what it commits |
| Draft check | Test connection | `SecondaryButton` after the fields it tests; tests the draft and saves nothing; busy and result text directly beneath |
| Draft change | Reset to desktop's defaults | `SecondaryButton` after the fields; changes the draft only; caption says Save applies it |
| Immediate action | Forget saved password, Update now, Use whole network | `SecondaryButton` beside text or in a section saying it applies straight away; confirm irreversible removal |
| Scope choice | Any device | First option above the results, not in the header; selects and returns immediately |

An immediate action acts on saved settings, never the unsaved draft. When a
draft blocks it, disable it and say why, for example "Save first to update
from this repository". State results in text as well as colour.

Check every secondary action in its enabled, busy and disabled states in both
themes. An explicit style such as `SecondaryButton` may omit the implicit
Button style's disabled colours. Supply a themed disabled appearance and keep
a fixed Save visibly disabled while its command is running.

`DashboardBackNavigation` still uses native page safe areas. Pages adopting
`SetupPageLayout` must remove that helper and their previous page padding and
native-safe-area declaration together. Validate the #391 cases before migrating
another page.

### Shared setup implementation

`SetupLayout` provides `Header`, `Body` and optional `PrimaryAction` slots.
Its rows are Auto, star and Auto. Put a `PageHeader` in the header, a
`ScrollView` or a grid containing a `CollectionView` in the body, and the real
Save command in the action slot. A list must stay in a star row and outside
any wrapping `ScrollView`. The action row reserves its measured height,
including its margin, so the viewport and final scroll position clear it.
Omit the action on immediate-save pages.

Construct `SetupPageLayout(page, layout, header, scrollers)` once, passing each
scroll or list view. It subscribes while the page is visible and disposes its
safe-area watcher and native notification tokens on disappearing. It restores
glass Back ownership on return and app resume. Existing callers of
`CoveredEdges.Watch` may retain their lifetime behaviour; new helpers should
retain and dispose the returned subscription.

Make page subscriptions idempotent: repeated Appearing adds at most one
handler or native observer. Retain the Window instance used for resume
subscriptions and unsubscribe from that instance on disappearing, even if
the page's Window has changed. Finish with each subscription paired with
its removal and each native observer disposed.

On iOS the native page owns only the top container inset. The layout owns
16pt gutters plus `CoveredEdges` side clearance, and the bottom action gap.
Native scroll inset adjustment is disabled on the supplied scrollers.
`SetupActionPlacement` aligns the action with a measured side pill, clears a
bottom pill and its actions, and treats a top pill separately. If measurement
is unavailable, it falls back to native bottom and extra glass clearance.
Within SetupLayout, the action's own bottom margin must be zero because
the layout owns the gap. Other fixed-action hosts may put the measured gap in
the action margin instead; it must still have exactly one owner.
Side-pill alignment uses `max(0, native height - pill bottom)`, matching
Devices/Alerts in `TabAlignedListLayout`. Do not clamp this measured gap to
the native bottom inset, which can lift the action a few points above the
pill. Keyboard clearance still takes precedence.

Docked keyboard notifications lift the action and reduce the viewport;
dismissal restores placement. The helper disables MAUI's iOS automatic page
translation while visible so it cannot move the fixed header/action a second
time, and restores it on exit. Focused fields inside native scroll views are
scrolled into view after keyboard geometry changes. Android retains native
safe-area and soft-input handling through `SafeAreaEdges.All` on the page.
Floating iPad keyboard obstruction remains a device check.

## Placement and scrolling

Content may move beneath glass as it scrolls. Controls at rest and the final
scroll position must stay reachable. Scrolling cards always clear a side pill's
column. Canvas backgrounds and chips scrolled out of sight may pass beneath it.

**Use all available pane space after measured camera/glass clearance and the
page gutters. Keep scrolling viewports finite and reserve the actual wrapped
height of fixed actions outside them. Give each inset one owner so extra
padding cannot shrink the usable area or lift an action above the pill base.**

For pages adopting the edge-to-edge pattern:

- On iOS, keep only the page's top container safe area. Set `SafeAreaEdges="None"`
  on supporting layouts and `ScrollView`/`RefreshView` where supported. Keep
  Android's native safe-area handling in shared views.
- `CollectionView` has no `SafeAreaEdges` property. On iOS, find its native
  `UIScrollView` from the handler and set `ContentInsetAdjustmentBehavior` to
  `Never` on `HandlerChanged` and `Loaded`. Reuse the pattern in Devices or
  Edit dashboard. Let the footer supply the final-item clearance.
- Use `CoveredEdges.ForScrolling(page)` for scrolling-column clearance. Its
  bottom includes the glass action row; its sides include a 12pt gap before a
  side pill. Add the page's 16pt gutters once. For a `ScrollView`, pad its
  content column. For a `CollectionView`, use side margins and footer space.
- Put placement in `PlaceControls()`. Call it on appearing, through
  `CoveredEdges.Watch`, and after `TabBarButtons.Moved`. Recompute column counts
  when the list width changes. Folding and rotation must work without reopening.
- Align header side clearance with the content. Use `TopBar.Fit` on a `Layout`
  after setting its padding; a `Label` is not a valid argument.
- Overlay an input-transparent 20pt gradient at the content's top, from page
  background to transparent. Keep it within the scrolling viewport, below
  fixed search and status rows, with matching list margins. Reserve its 20pt
  region with a list header spacer or equivalent content padding, including
  group headings and empty states. Verify initial content is fully readable
  at rest and the fade passes taps and drags through to the list.
- Toast sides are 12pt plus side clearance. Its foot clears bottom glass, or
  aligns with the foot of a vertical pill. A wide pill is a bottom bar only if
  its top is below the page midpoint; iPad can put a wide pill at the top.
- Reserve a visible overlay's measured height, bottom margin and gap in the
  scrolling footer or viewport. Recompute after wrapping, large-text changes
  and resizing; retain clearance through its exit animation. For editor Undo,
  use a 12pt gap so the final Add action can scroll fully above the toast.

Inside native scroll views, MAUI handles child safe areas differently from
outer layouts. Check the actual control type and existing native inset code
before adding more padding. Compile both platforms after XAML changes; parsing
XML alone does not validate MAUI properties or binding types.

## Header, glass and navigation

**On iOS 26+, all page actions other than the fixed primary action, and
optional search, on top-level and pushed pages alike, belong to `TabBarButtons`. Back-only glass does not satisfy this
rule when a pushed page has other actions.**
Android and older iOS keep header icons through `IconButton` and `Icons`.
Form submission follows the fixed primary-action layout above; preserve each
form's save rules unless its issue explicitly changes them. Keep page actions
to two to four and leave rare actions in context menus.

- Call `TabBarButtons.Show` on appearing and `Hide` on disappearing. Subscribe
  and unsubscribe `Moved` with that lifecycle. Hide clears only its owner, so
  either page may receive its lifecycle event first during navigation.
- With no search, pass `null` and `string.Empty`. Avoid a redundant magnifier.
- Pushed pages use `chevron.backward`, `BackBar.PreviousTitle(page)` for the
  accessible label and relative back navigation. Hide their header Back on
  iOS 26+; keep it on older iOS and Android.
  **Every pushed page must reclaim its caller Back in glass on iOS 26+ after
  child return, retained-tab return and resume, with one active glass owner.**
- Open descendants with relative routes so Back returns to the actual
  caller. Test each entry path, child return and app resume for one glass
  owner. Cover Dashboard → Edit → Add/Set up → Back and alert → device → backups.
- Offer setup only for explicitly supported entity kinds, each routed to
  an editor that can load that kind. Capabilities such as duplication do not
  imply setup support. Test route and entity ID for every supported kind.
- Show data-dependent actions only when their command can execute. Observe
  `CanExecuteChanged`; rebuild stateful glass actions only when state changes.

Glass placement measures the actual pill, not `UITabBar`'s empty bounds. A
vertical pill has actions at the column's top and search above the pill; a
bottom pill has actions beside or above its right end. Off-screen or missing
pill positions leave the buttons hidden. Placement belongs in `TabBarButtons`,
not in individual pages.

### Glass implementation constraints

These protect cold launch as well as tab switching:

- Only a controller showing its tab bar takes ownership. A hidden controller
  clears glass only if it already owns it. Competing sign-in and main
  controllers previously caused a launch loop (#332, #335).
- Cache and recheck the pill before searching the window. Searches skip
  `MauiView` subtrees and the app's views (#334). Placement runs during native
  layout and must stay cheap without causing another controller to lay out.
- Keep at most one placement queued for the next run-loop turn. The breaker
  pauses for a second after excessive placements, then retries. A
  "Tab bar buttons: paused" diagnostic means investigate the loop.
- Lay out newly supplied actions immediately, including during a pause (#336).
  UIKit `UISearchTab` is not this app's search control; earlier attempts
  overflowed the pill and broke Shell navigation (#308 to #315).

## Search and choosers

All app search boxes use `Controls/SearchField.xaml`. It provides the
46pt rounded well, radius 23, `SurfaceAlt` fill, themed stroke and magnifier.
Put the page's `Entry` inside it so compiled text/placeholder bindings and
page-specific focus callbacks remain on the actual input. The control sets
Search return type, disables spell checking and prediction, removes clear
buttons and iOS keyboard accessories, and opts the Entry into `KeyboardDismiss`.
Do not use native `SearchBar` or copy the search Border markup into pages.

Tab/list search starts hidden on Android and iOS. Open it only from the header
search button (Android/older iOS), the glass magnifier (iOS 26+), or Command-F.
Upward scrolling, pull gestures and swipe-to-search must never open it.
During future list-page passes, remove legacy `SearchReveal.Attach` and swipe
handlers from the scoped page rather than preserving the old platform policy.
Migrate pages individually; do not change shared SearchReveal underneath pages
that have not been reviewed. Health, Groups & locations, Alert rules and
Neighbours use `ButtonSearch`.
Known follow-ups for future scoped passes: Devices still dismisses search on
any Scrolled event.
Migrate those handlers to real user-drag checks rather than treating row
replacement during filtering as a gesture.
Setup pickers retain their always-visible search. Filtering, query retention
and selection contracts remain unchanged.

- Opening tab search focuses the field; tapping its magnifier again refocuses
  it. Close on Search, unfocus or a content touch, retaining the query. List
  user dragging also hides the field and dismisses the keyboard. Filtering and
  programmatic scrolling must retain focus, as established by DeviceSectionPage
  in #402. ButtonSearch checks native drag state; its lists explicitly disable
  `KeyboardDismiss.OnScroll`. Use `KeyboardDismiss.Close` before
  hiding a focused field.
- Use no clear/close X or keyboard accessory. `SearchField` owns this policy,
  including on setup pickers.
- Use a clipped search slot. Animate height and translation together when the
  chips/list should move; overlay search only when the canvas pattern calls
  for it. Use `CoveredEdges.Beside(page, field)` for row-specific clearance.
- Keep setup picker scope actions, search and status in fixed rows above the
  results viewport. A `CollectionView.Header` may scroll away or be recycled;
  reserve it for content that belongs to the list, such as its fade spacer.
  Verify typing retains focus and Command-F reaches search after scrolling.
- Implement `ISearchHost` for Entry-based pages supporting Command-F.
- Choosers whose rows come from the server use a `CollectionView`, grouped
  where headings are needed, inside the finite viewport. `BindableLayout` is
  for short fixed lists only. Choosing a row in an immediate chooser commits
  and returns, with no Save.
- Use `NaturalOrder.Instance` for human-facing device, graph, group and location
  choices across every source feeding a picker, including port graph
  descriptions. Ports themselves may retain interface order; that exception
  does not apply to their graph descriptions. Test each source separately.

## Adaptive content and accessibility

### Approved Settings overview hierarchy

The 4 November 2026 #454 proposal was explicitly approved for implementation.
Settings uses one full-width column on narrow and wide displays. Restore the
repository DashyNMS mark beside the wrapping session server identity. Separate
Preferences, Services, embedded About and conditional Diagnostics with generous
space, quiet headings and hairline navigation rows. Lock screen widgets belongs
after Health thresholds in Preferences. Keep Sign out isolated below the groups.
This decision is specific to Settings and does not approve other page redesigns.

Retain every existing dynamic summary and explanatory sentence in full.
Component versions are read-only name/version/state stacks; About stays inline.
Navigation gets a chevron; explanations, switches and immediate actions keep
distinct targets. Logo and chevrons are decorative. The overview must not claim
all children save automatically, because services and thresholds use drafts.
Proposal approval does not establish device acceptance. See the complete source
map, historical rejected layout and pending matrix in
[the Settings assessment](reviews/454-settings-about.md).

### Shared adaptive rules

- Dashboard and editor share `DashboardColumns.Count`, with 340pt minimum
  columns, 12pt gaps and a three-column limit. Use available content width
  after clearances, and read across then down. Other page types use their
  reference pattern's breakpoint; folding and rotation update without reopening.
- Let titles, summaries and field labels wrap. Put labels above pickers with
  `SetupSection`, which supplies 4pt field spacing and 8pt section margin.
  Give each picker an accessible name matching its field, even after a value
  replaces its Title placeholder. A switch may sit beside wrapping text and
  has its own accessible description.
- Label/control rows put the control beside its label only when both fit at
  the current width and text size; otherwise stack the label above. Use no
  fixed label or value column widths. Numeric fields show their unit in the
  label or section heading and keep the full value visible.
- For signed numeric fields on iOS, use a keyboard that offers a minus sign;
  the decimal keypad selected by `Keyboard.Numeric` cannot re-enter negative
  thresholds. Thresholds uses `NumbersAndPunctuation` on iOS and retains the
  numeric keyboard on Android. Verify clearing and re-entering a negative
  value, decimal input and access to Save with the keyboard open.
- Choice grids such as Add card use their own breakpoint: 160pt minimum tiles,
  10pt gaps and at most four columns, recomputed on resize and text-size
  change. Dashboard column counts do not apply to catalogue previews.
- Name a stepped form's current step in its header. Show the chosen entity
  in a labelled section with a Change action naming the entity it replaces.
  A form with no choose step uses its subject as the header.
- Make each selection row or tile one accessible element and make its whole
  area tappable. Combine its wrapping name, details, availability reason and
  selected state; exclude child labels, previews, icons and checkboxes from
  duplicate announcements. Use a checkbox for several choices and a tick for
  one choice. Keep unavailable reasons at readable contrast.
- Update row, tile and segment descriptions when selection changes. State
  the current action accurately, including whether choosing a notification
  rule includes or excludes it. Verify meaningful names, action semantics
  and VoiceOver/TalkBack activation; each tap or toggle changes selection once.
- Repeated Set up, More, Change and Remove actions name their target. Keep
  each action's tap area at least 44pt high when rows stack, and preserve
  vertical padding between row content and card borders or dividers.
- Offer menu reordering alongside drag reordering, using Move earlier/later
  in reading order. Keep titles and summaries together and Set up below them.

- Show severity and unavailable state in text as well as colour, including in
  each actionable row's accessible description. Unknown readings must not be
  presented as OK.

For More’s navigation catalogue, use quiet headings and lightweight rows separated
by hairlines. Keep one wrapping column at narrow and wide widths; use summaries
where destination names need context. Open and pin are separate accessible
actions, each at least 44pt, with no parent gesture enclosing the pin. Use an
outlined pin for not pinned and a filled pin for pinned, with explicit target,
membership and action in its accessible name. State capacity once above the
catalogue; this pattern replaces visible per-row unavailable text with that
line and explicit accessible state. A full-bar pin remains operable to
explain how to make room.
This visual direction was approved for More on 4 November 2026; native device
acceptance remains separate in [its assessment](reviews/453-more-navigation.md).

## Data states and persistence

- Preserve complete server-provided messages, expressions, configuration and
  licence content. Presentation wrapping must not change copied, shared or
  compared source text. Truncated previews must lead to the complete content.
- Reuse themed `Error`, `Caption`, `ActivityIndicator` and shared controls.
  Keep loading/error/status content separate from a finite results viewport.
- Distinguish an empty source from an unmatched search. Suppress empty claims
  while loading or when an error explains missing data.
- State an actionable retry route beneath a setup error, such as reopening
  the page or choosing the device again. Setup pages use these existing
  routes rather than separate retry buttons. A failed Save keeps its draft.
- Enable Save only after every source required to initialise its draft has
  loaded. A failed or superseded load must not overwrite saved choices with
  empty data. Ignore slower earlier loads when a page reloads.
- Retain saved selections missing from a response until explicitly cleared.
  Update selection rows in place so toggling preserves scroll position.
- Separate immediate actions from draft fields in a labelled section with
  text saying they apply straight away. Save commits the draft together;
  Back discards it. Immediate-save pages preserve automatic saving.
- An automatic-save page says "Changes save automatically." once, beneath its
  heading. It has no Save, action slot or per-change confirmation. A setting
  the system refuses, such as a denied permission, shows its actual state and why.
- After a failed Save, scroll its error into view and announce it, because the
  fixed action may be far from the error's position at the top. Capture the
  non-empty error before dispatching or awaiting scrolling; announce that
  captured message only if it is still current. Repeated identical failures
  must still reveal feedback, and a cleared error must not be announced.
- Picker status sits between search and the list, one state at a time:
  loading, load failure with its retry route, empty source, no matches for
  the query, or a capped result count with a prompt to refine the search.
  Rows that cannot be chosen stay visible with their reason in the row text
  and accessible name; dimming alone is insufficient.

## Motion and selected-item cards

Use named `view.Animate` calls so a new transition can abort the previous one.
With `ReducedMotion.IsOn`, apply the final state immediately.

- Slide and fade entrances over 220–250ms with cubic out; exits use cubic in.
  Set initial opacity and translation before making a view visible.
- Selected-item sheets start below their measured height plus bottom margin
  and 8pt. Hide after exit completes; clear selection afterwards so the
  departing sheet keeps its content. Switching selection keeps the sheet up.
- Align a sheet's foot with a side pill; otherwise clear covered bottom and
  glass actions. Use 12pt side gutters plus covered edges. Controls above the
  sheet clear its actual height.
- Cap collapsed sheets at 200pt on a screen below 740pt high, otherwise 300pt.
  Expanded sheets use max(240pt, 60% of content height) and an internal scrolling
  list. Measure natural collapsed height before animating a size change.
- Fade surrounding content and retain the selected reference until the fade
  finishes. Follow Network map's dimming implementation for canvas content.
- Reuse `DashboardToastView` for dashboard Undo transitions. Its measured
  clearance follows the placement and scrolling rules.

## Detail columns

For a list beside a detail pane, apply only insets reaching that column:
right clearance = max(0, page right clearance − space to the column's right).
Pad the detail's scrolling content, not the pane itself, so its background
reaches the screen's foot. Device detail actions join the list's glass while
shown. Unsubscribe the old detail view model when replacing it.

## Shared controls and diagnostics

Paths below are relative to `src/DashyNMS.Mobile/`, except shared models.

| Need | Reuse |
| --- | --- |
| Page/row/scroll clearance and resize callbacks | `Controls/CoveredEdges.cs`: `Of`, `Beside`, `ForScrolling`, `Watch` |
| Glass ownership, pill and action bounds | `Platforms/iOS/TabBarButtons.cs`: `Show`, `Hide`, `Moved`, `PillIn`, `Occupied` |
| Camera/system occlusion | `Platforms/iOS/ReservedRegions.cs` |
| Header fitting and native safe-area updates | `Controls/TopBar.cs`, `Controls/TopLineSpace.cs`, `Platforms/iOS/SafeAreaWatcher.cs` |
| Search appearance and input policy | `Controls/SearchField.xaml`, `Controls/SearchField.xaml.cs`, `Controls/ButtonSearch.cs` for migrated button-only lists; `Controls/SearchReveal.cs` for legacy pages |
| Keyboard and motion | `Controls/KeyboardDismiss.cs`, `Controls/ReducedMotion.cs`, `Pages/ISearchHost.cs` |
| Header icons | `Controls/IconButton.cs`: `IconButton`, `Icons` |
| Adaptive dashboard columns | `../DashyNMS.Mobile.Shared/Dashboard/DashboardColumns.cs`, `Controls/CardGrid.cs` |
| Primary button colours and states | `Resources/Styles/Styles.xaml` implicit Button style |
| Shared setup slots, placement and sections | `Controls/SetupLayout.cs`, `Controls/SetupPageLayout.cs`, `Controls/SetupSection.cs`, `../DashyNMS.Mobile.Shared/Services/SetupActionPlacement.cs` |
| Existing form Back and inset handling | `Controls/DashboardBackNavigation.cs` |
| Human reading order | `../DashyNMS.Mobile.Shared/Services/NaturalOrder.cs` |
| Device evidence | Settings diagnostics, "Tab bar buttons:" and "Top line on" entries |

For native geometry, use `nfloat` where CoreGraphics expects it. Use
`pill.Left + pill.Width / 2` or import CoreGraphics for its midpoint extensions.

## Acceptance checklist

For each page change, record applicable results, including unverified items:

- [ ] Each supported kind routes to a compatible editor with the selected ID; Back returns through every caller and glass has one owner
- [ ] Insets have one owner per edge; final content and actions clear glass and visible overlays through transitions
- [ ] Initial text and empty states are readable below fades at rest
- [ ] Titles and controls fit narrow screens and larger text in both themes
- [ ] Folding/rotation recomputes clearance and columns without reopening
- [ ] Search closes the keyboard correctly and preserves its query
- [ ] Loading, empty, error and selected states remain readable and retryable
- [ ] Save/automatic-save and Back cancellation follow the approved contract; failed loads cannot save an empty draft
- [ ] One primary action; secondary and immediate actions follow the action hierarchy and say what they change
- [ ] **Fixed primary action aligns with the base of a vertical menu/tab pill, or clears a bottom bar and its actions; verify inside/outside screens and after fold, rotate, keyboard dismissal, tab return and resume**
- [ ] Screen-reader names, selection states and activation work; drag actions have an accessible alternative
- [ ] Motion respects Reduce Motion; repeated transitions finish correctly
- [ ] Relevant shared tests and Android/iOS builds pass
- [ ] Device check covers Duo outer, inner portrait and inner landscape, app resume,
      and an ordinary iPhone if available; first-tab changes include cold launch
- [ ] Diagnostics checked for any reported overlap or placement loop
- [ ] Fixtures and screenshots use synthetic network data

Repository issue, PR and release conventions live in `docs/DEVELOPING.md`.
Ship one coherent page change at a time and verify it on the Duo before
expanding the rollout, unless the user authorises a batch as described under
Batch delivery. CI success alone does not establish visual correctness.

## Pending corrective regression rules

Implementation-grounded proposals, pending Claude review and device acceptance:
button-only list search closes on actual native drag, completion or unfocus;
filter replacement and programmatic Scrolled must retain focus. Opening is only
through a search action or Command-F. Retained tab stacks must reclaim page
glass on actual activation and resume without requiring Refresh. Pushed nested
alerts must retain the selected tab and pop to their actual caller, with caller
Back in glass or the header fallback. Fade endpoints must share the actual
viewport surface RGB, span that viewport and reserve 20pt without blocking input.
See [the corrective assessment and exact regression matrix](reviews/alert-navigation-correction.md).

Geographic-map startup must use live DOM viewport dimensions to gate first layout.
Keep first-camera draw handlers safe before load: layer groups attach through
whenReady and marker icons can be undefined during the first zoomend. Supply
initial dimming through marker options. The #468 unguarded icon access can stop
any populated map before tile attachment; stale cached-zero size is a separate
startup failure. Leaflet's cached getSize and pre-view invalidateSize cannot
establish readiness. Seed a view only when cached dimensions differ from live
dimensions, then invalidate and fit inside the programmatic guard. Otherwise fit
directly to avoid seed world tiles. CI must execute the embedded bridge
with document load before geometry, cached zero size, later native clearance and
resize, normal/empty pins and pending selection, requiring camera and draw
activity rather than source substrings. Model deferred layer attachment, missing
pre-load marker icons and first-view events through load; assert pending focus
centre and safe offset. Keep fitting zoom controls visible and
retain the defined insufficient-space pinch fallback. Real tiles, native clips,
gestures, glass and fold/rotation still require the device matrix in
[the map correction assessment](reviews/map-loading-correction.md).

### Pushed backup viewport ownership

`PushedListLayout` owns the full-height Body host's side and bottom padding;
fixed headers/status rows precede a finite star viewport. Query the pill and
glass in that exact native host. With no fixed action, match Devices/Alerts:
a side or bottom pill anchors the viewport base at `max(0, H - pill.Bottom)`.
A bottom pill's obstructing glass belongs in the scrolling footer; a side
pill must not clamp the measured base to the safe-area bottom. A top-wide pill
is separate. With a visible fixed action, use `SetupActionPlacement` for the
host's only bottom gap and reserve the action's actual wrapped Auto-row height.
Do not combine an action bottom margin with the same host inset.

Check normal and comparison modes independently against actual native host,
card/list and pill bounds, including dynamic selection/busy actions, child
return, retained tab/resume, fold/rotation and keyboard dismissal. Footer reach
alone does not establish viewport alignment. #462 owns this shared correction;
#463 contents then #464 differences consume it sequentially and retain their
own action, search, source and export contracts.
