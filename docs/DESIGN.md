# DashyNMS Mobile design guide

Updated 2 November 2026. Applies when implementing or reviewing tab pages,
pushed pages, dashboard editing, pickers and setup forms.

## Implementation workflow

1. Classify the page using the table below. Read its reference implementation,
   the shared controls it uses and its view model before choosing a layout.
   Finish when navigation, scrolling and save behaviour are accounted for.
2. Identify the owner of each inset: native page safe area, native scroll inset,
   content padding or overlay margin. Choose one owner per edge. Finish when
   every tappable control and the final list item can clear system and glass UI.
3. Implement the relevant rules below. Reuse shared controls and preserve the
   page's persistence contract. Finish when resizing, navigation and keyboard
   dismissal all update the layout without duplicate controls or insets.
4. Run shared tests for changed behaviour and both platform builds. Inspect
   layout diagnostics for placement failures. Finish when checks pass, or record
   precisely which checks could not run.
5. Before merging, have the coding agent review the PR's full diff against
   this guide: `/code-review` in Claude Code, `/review` in Codex, or the same
   request in plain words elsewhere. Fix confirmed findings in the PR, or note
   in the PR why one stands. A review does not replace CI or device checks.
6. Verify on a device before marking appearance verified. Use the acceptance
   checklist below and report code review, CI and device results separately.

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
The page migrations remain pending; this table describes current code.

## Agreed setup layout

Implement the approved review under epic #405 in milestone 2.0.0. The shared
layout belongs to #406; each page has its own migration issue.

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
  Exception: #395 records the requirement for explicit sensor Save, including
  a real draft for title/selections and Back cancellation. Other persistence
  changes need an explicit decision in their issue.

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

On iOS the native page owns only the top container inset. The layout owns
16pt gutters plus `CoveredEdges` side clearance, and the bottom action gap.
Native scroll inset adjustment is disabled on the supplied scrollers.
`SetupActionPlacement` aligns the action with a measured side pill, clears a
bottom pill and its actions, and treats a top pill separately. If measurement
is unavailable, it falls back to native bottom and extra glass clearance.
The action's own bottom margin must be zero to preserve alignment.
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

Use `SetupSection` for the shared label, 4pt field spacing and 8pt section
margin. Use `SearchField` and the existing `Error`, `Caption` and
`ActivityIndicator` presentation rather than introducing another theme palette.
Search submission and list scrolling use `KeyboardDismiss`. Selection remains
owned by the page/view model; expose selected state to accessibility when a
page adds selection rows. Supply meaningful empty and retry states for each
page's data contract. A retry state can be a caption under the error naming
the existing route that loads again, such as reopening the page or choosing
the device again; setup pages have no Try again buttons. An empty list says
whether a search matched nothing or there is nothing to show, and stays blank
while an error explains it.

Graph setup is the first consumer, covering a device list and a scrolling
draft form. Its optional title scrolls with the form, search uses the shared
SearchField, and Save still uses the existing draft command. No view-model
or persistence contract changes were included in that Graph migration.
Sensor setup now uses the same layout with a scrolling card-title header,
a finite sensor list and a fixed Save. Its title header starts below the 20pt
fade so nothing is faded at rest. Each sensor row is one accessible element
announcing name, device, reading and selected state; its checkbox is hidden
from screen readers. Toggling a row updates it in place rather than rebuilding
the list, so the scroll position holds. Like Graph, it states the retry route
under an error instead of a Retry button: reopen setup after a failed load, or
choose Save again after a failed save, which keeps the draft.
Ranked-card setup (#397) is a scrolling draft form with no choose step: its
header is the card kind's title, and Card title, Rows, Rank by and, for Top
errors only, Quiet ports are `SetupSection`s with labels above their controls,
so large text does not crowd a row. Its draft, Save and stored fields are
unchanged.
Spotlight setup (#398) is a picker with no choose-then-configure step. Its
header is "Spotlight". A Whole network section comes first: a caption saying
it applies straight away, with a secondary "Use whole network" button that
still saves and returns at once. Below it, an "Or keep it to" section holds
the Devices/Group/Location segments, each announcing whether it is selected.
Search, a selected count with the kind's prompt, then the error and its retry
route and a loading indicator sit above the finite list, never over it. Rows
are cards with one screen-reader description of name, detail and selected
state. Devices show a check box because several can be ticked. A group or
location shows a tick because it is the single choice. Devices, groups and
locations use `NaturalOrder`, with saved choices the server no longer has kept
at the top until unticked. The fixed Save stays disabled until every list has
loaded, so a failed load cannot overwrite the saved Spotlight with an empty
one. Other pages retain their page layouts; their search appearance uses
SearchField.

Graph setup is also the reference for a stepped setup page (#396). The
header names the current step ("Choose a device", then "Choose a graph").
After choosing, a labelled Device section shows the device's name and details
with Change device beside them, and Change device's accessible description
names the device it replaces. Devices and device-wide graph descriptions use
`NaturalOrder`; ports keep interface order. Port graph descriptions still use
the static list order, a gap against the natural-order rule below. A device
with no graphs gets a caption explaining the empty picker. The draft, Save rules and persisted
fields are unchanged. Ranked cards (#397) and Spotlight (#398) should follow
this pattern where they have a choose-then-configure step.

### Graph setup review and acceptance

On 2 November 2026 the user approved closing #396 and completing its entry
on #405 after reviewing PR #421, merged as
`8259758dc1385d8258adac49097d1ff656d4092e`. Android, iOS simulator and shared
unit-test PR checks passed. The review found that the shared layout, fixed
Save, draft behaviour and title-first field order followed the approved plan.
Individual device configurations and screen-reader results were not recorded
with this acceptance. Keep the earlier #406 device evidence separate.

Apply these lessons to later setup pages:

- Name each step in the header and show the chosen device in a labelled
  section with a Change device action that names the current device to
  accessibility. Keep the optional card title first in Graph setup, as approved.
- Check every graph source when applying natural order. Interface order
  applies to the ports in Show; it does not exempt their graph descriptions.
  PR #421 sorted device-wide graphs but left port graph descriptions in
  static order. Its ordering test covered device graphs and port interfaces,
  so it did not catch this gap. Include port graph descriptions in future
  ordering checks.
- Device rows in PR #421 gained an accessibility hint, but no explicit
  combined name/details description or button semantics. Verify that
  VoiceOver and TalkBack announce a meaningful device name and action and
  can activate the row. A hint or passing view-model test alone does not
  establish accessible operation.
- Disable a fixed Save until the data its draft starts from has loaded. A
  Save after a failed load must not write an empty selection over the saved
  one; Spotlight setup did this before #398.
- State the existing retry route beneath an error rather than adding a
  separate retry button. Verify that reopening reloads the device list and
  Change device followed by selection reloads graphs. Distinguish an empty
  server from an unmatched search, and suppress empty claims after failures.

### Ranked-card setup acceptance

On 2 November 2026 the user approved closing #397 and completing its entry
on #405 after on-device testing of TestFlight **2.0.0 (216)**. The accepted
implementation is PR #425, merged as
`7f0e1f2217fa628209a63c89c2871e69108c9fcd`. Android, iOS simulator and shared
unit-test PR checks passed. Individual device configurations and
screen-reader results were not recorded with this acceptance.

Apply these lessons to later setup pages:

- A draft form with no choose step needs no step names. Its header is the card
  kind's title, and the optional card title stays first, as in Graph setup.
- Put labels above pickers with `SetupSection` rather than side-by-side
  label/control rows, so large text cannot crowd a row. A switch row keeps
  its wrapping text beside the switch and gives the switch its own
  screen-reader description.
- Reference a device-gated issue with `Refs #NN` in its PR, so merging does not
  close it before device acceptance.

### Spotlight setup acceptance

On 2 November 2026 the user approved closing #398 and completing its entry
on #405 after on-device testing of TestFlight **2.0.0 (217)**. The accepted
implementation is PR #428, merged as
`89eac7478bb70267e10d15b925fd7658193a6196`. Android, iOS simulator and shared
unit-test PR checks passed. Individual device configurations and
screen-reader results were not recorded with this acceptance.

Apply these lessons to later setup pages:

- Find every route into a page before testing it. Spotlight setup opens only
  from the Spotlight line under an Event log or Graylog card's title on the
  Dashboard. Edit dashboard's Set up does not reach it: `SetUpRoute` sends
  every kind other than Sensors and Graph to ranked-card setup, which cannot
  load a log card. State the entry point in the PR's device checks.
- An immediate action on a draft page sits in its own labelled section with
  a caption saying it applies straight away, apart from the choices that wait
  for Save.
- Give each segment of a segmented control a description that says whether
  it is selected, and make each selection row one screen-reader element with
  its state. A check box suits several choices; a tick suits one.
- Disable Save until the draft's data has loaded, and ignore a slower earlier
  load when the page reloads.

### Verified setup reference

The user confirmed that manual checks passed for TestFlight **2.0.0 (212)**
on 2 November 2026 and approved closing #406 and completing its entry on #405.
The accepted implementation is PR #418, commit
`b4fd1983c99cb06c25f8afe9d5a1d234a2262890`, following the foundation in #417.
Use Graph setup as the first shared-layout consumer accepted on device.
Individual device, OS and configuration results were not supplied; this
acceptance does not establish separate Android, iPad or accessibility results.

Apply these findings when migrating another screen:

- Align the visible action with the measured pill, using the native view's
  height and pill bounds in the same coordinate space. Match Devices/Alerts
  with `max(0, height - pill bottom)` for a side pill. A native-bottom-inset
  minimum can introduce the few-point offset reported during device testing.
  Keep the action's own bottom margin at zero; let the shared layout own the
  gap and let keyboard clearance take precedence.
- Use `SearchField` for every search well. A styled native `SearchBar` still
  renders the old UI. Keep the actual Entry and its compiled bindings inside
  the shared control; animate the whole well and focus its Entry.
- Put a themed, input-transparent 20pt top fade over a scrolling device
  results list, as Graph setup and Device sections do. Keep the fade in the
  list's viewport, below search, with matching list margins. It must not
  intercept row taps or drags.
- Keep the list in a finite star row, outside a wrapping ScrollView. Keep
  draft fields inside the form's scrolling content and the primary action
  outside it, so the final field/result clears the measured action height.
- Reuse the helper's visible-page subscriptions and inset ownership. Do not
  add the old `DashboardBackNavigation` helper, duplicate safe-area padding
  or a second keyboard adjustment to a migrated page.

Geometry tests and platform builds remain separate from device acceptance.
Each later screen still needs its own navigation, keyboard, persistence and
configuration checks; #406 acceptance does not complete the remaining page
migration issues in #405.

## Placement and scrolling

Content may move beneath glass as it scrolls. Controls at rest and the final
scroll position must stay reachable. Scrolling cards always clear a side pill's
column. Canvas backgrounds and chips scrolled out of sight may pass beneath it.

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
  background to transparent. Content fades beneath the heading as it scrolls.
- Toast sides are 12pt plus side clearance. Its foot clears bottom glass, or
  aligns with the foot of a vertical pill. A wide pill is a bottom bar only if
  its top is below the page midpoint; iPad can put a wide pill at the top.

Inside native scroll views, MAUI handles child safe areas differently from
outer layouts. Check the actual control type and existing native inset code
before adding more padding. Compile both platforms after XAML changes; parsing
XML alone does not validate MAUI properties or binding types.

## Header, glass and navigation

On iOS 26+, top-level page actions and optional search belong to `TabBarButtons`.
Android and older iOS keep header icons through `IconButton` and `Icons`.
Form submission follows the fixed primary-action layout above; preserve each
form's save rules unless its issue explicitly changes them. Keep page actions to two to four and leave rare actions in
context menus.

- Call `TabBarButtons.Show` on appearing and `Hide` on disappearing. Subscribe
  and unsubscribe `Moved` with that lifecycle. Hide clears only its owner, so
  either page may receive its lifecycle event first during navigation.
- With no search, pass `null` and `string.Empty`. Avoid a redundant magnifier.
- Pushed pages use `chevron.backward`, `BackBar.PreviousTitle(page)` for the
  accessible label and relative back navigation. Hide their header Back on
  iOS 26+; keep it on older iOS and Android.
- Open descendants with relative routes so Back follows the full chain.
  Check Dashboard → Edit → Add/Set up → Back and alert → device → backups.
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

Tab/list search retains its existing sliding slot. `SearchReveal` animates the
whole SearchField and focuses its Entry; keyboard shortcuts reveal that same
field. Setup pickers retain their always-visible search behaviour. Changing
appearance does not change filtering, query retention or selection contracts.

- Opening tab search focuses the field; tapping its magnifier again refocuses
  it. Close on Search, unfocus or a content touch, retaining the query. List
  scrolling also dismisses the keyboard. Use `KeyboardDismiss.Close` before
  hiding a focused field.
- Use no clear/close X or keyboard accessory. `SearchField` owns this policy,
  including on setup pickers.
- Use a clipped search slot. Animate height and translation together when the
  chips/list should move; overlay search only when the canvas pattern calls
  for it. Use `CoveredEdges.Beside(page, field)` for row-specific clearance.
- Implement `ISearchHost` for Entry-based pages supporting Command-F.
- Use `NaturalOrder.Instance` for human-facing device, graph, group and location
  choices. Numeric interface indices can retain their domain order.

## Dashboard editing and primary actions

- Dashboard and editor use `DashboardColumns.Count` with the same available
  content width, 340pt minimum, 12pt gaps and three-column limit. Wide Duo
  displays get two half-width cards when space permits. Narrow displays get
  one. Follow width, not device-name checks. Order reads across then down.
- Let card titles and summaries wrap. Put Set up below the title. More holds
  Duplicate where supported, Add a card after, Move earlier/later and Remove. Menu moves
  persist the same order as dragging; removal retains Undo.
- Use the implicit primary `Button` style for the main Add or Save action.
  It supplies `ButtonPrimary`, text contrast and disabled states. Secondary
  buttons are for subordinate actions. Avoid local colour overrides.
- Edit dashboard saves immediately. Sensors drafts its title and selections,
  commits them together on Save and discards changes on Back. Opening sensor
  setup without a card ID creates a card only on Save; an explicitly added
  card remains on the dashboard when its later setup is cancelled. Empty sensor
  selections are supported. A failed save keeps the draft for another Save.
  Saved sensors absent from the current server response are retained. Sensor
  setup was accepted on device under #395.
- Edit dashboard cards show a six-dot grab handle drawn at its own size
  (`Aspect="None"`) and hidden from screen readers. A `Path` with
  `Aspect="Uniform"` scales its geometry's bounds to the box, which stretched
  the earlier dashes into bars and clipped their outer strokes. Each card's
  title and summary are one accessible element, with the labels inside
  excluded, and its hint points to More: Move earlier/later replace dragging
  for screen readers. Menu wording follows reading order, since a one-place
  move across columns can be sideways. Set up and More name their card. Device checks for #399: both Duo displays, inner portrait
  and landscape, folding, large text, the final Add a card, dragging across
  columns, menu moves, Remove/Undo and returns from Add or Set up.
- Graph, ranked cards and Spotlight use explicit Save in the fixed setup
  action. Spotlight's Whole network action still saves and returns
  immediately and says so; making it a staged choice would be a separate
  behaviour decision. A unified visual layout must identify these contracts
  before changing their behaviour.

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
- Dashboard Undo toasts use the shared `DashboardToastView` transition. Bind
  custom properties in code with an explicit `Binding` object; the two-argument
  string call to `SetBinding` did not compile in #401.

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
| Header fitting and native safe-area updates | `Controls/TopBar.cs`, `TopLineSpace.cs`, `Platforms/iOS/SafeAreaWatcher.cs` |
| Search appearance and input policy | `Controls/SearchField.xaml` and `.xaml.cs`, `SearchReveal.cs` |
| Keyboard and motion | `Controls/KeyboardDismiss.cs`, `Controls/ReducedMotion.cs`, `Pages/ISearchHost.cs` |
| Header icons | `Controls/IconButton.cs`: `IconButton`, `Icons` |
| Adaptive dashboard columns | `Mobile.Shared/Dashboard/DashboardColumns.cs`, `Controls/CardGrid.cs` |
| Primary button colours and states | `Resources/Styles/Styles.xaml` implicit Button style |
| Shared setup slots, placement and sections | `Controls/SetupLayout.cs`, `SetupPageLayout.cs`, `SetupSection.cs`, `Mobile.Shared/Services/SetupActionPlacement.cs` |
| Existing form Back and inset handling | `Controls/DashboardBackNavigation.cs` |
| Human reading order | `Mobile.Shared/Services/NaturalOrder.cs` |
| Device evidence | Settings diagnostics, "Tab bar buttons:" and "Top line on" entries |

For native geometry, use `nfloat` where CoreGraphics expects it. Use
`pill.Left + pill.Width / 2` or import CoreGraphics for its midpoint extensions.

## Acceptance checklist

For each page change, record applicable results, including unverified items:

- [ ] Navigation returns through the actual calling pages; glass has one owner
- [ ] Insets have one owner per edge; last row, form field and primary action remain reachable
- [ ] Titles and controls fit narrow screens and larger text in both themes
- [ ] Folding/rotation recomputes clearance and columns without reopening
- [ ] Search closes the keyboard correctly and preserves its query
- [ ] Loading, empty, error and selected states remain readable and retryable
- [ ] Save/automatic-save behaviour is preserved or explicitly approved to change
- [ ] Motion respects Reduce Motion; repeated transitions finish correctly
- [ ] Relevant shared tests and Android/iOS builds pass
- [ ] Device check covers Duo outer, inner portrait and inner landscape, app resume,
      and an ordinary iPhone if available; first-tab changes include cold launch
- [ ] Diagnostics checked for any reported overlap or placement loop
- [ ] Fixtures and screenshots use synthetic network data

Repository issue, PR and release conventions live in `docs/DEVELOPING.md`.
Ship one coherent page change at a time and verify it on the Duo before
expanding the rollout. CI success alone does not establish visual correctness.
