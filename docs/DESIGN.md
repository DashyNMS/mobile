# DashyNMS Mobile design guide

This guide tells an implementer how to build or change a mobile page so it matches the approved UI. Follow the steps in order. Each step ends with a check you can see, measure or run. The steps hold approved targets. Some pages have recorded device acceptance and others are source targets still awaiting it. Section 12 lists the pages still awaiting device acceptance. For an existing setup page or the dashboard editor, also apply its contract in section 10a.

H is the native body host height, B is the pill bottom and V is the list or graph viewport bottom. A card host is a page whose list sits inside one bordered card. The card hosts are the tab-aligned lists and the `PushedListLayout` lists (backup history, contents and diff). Logs and Graylog use `TabAlignedListLayout`. Devices, Alerts, Health, Groups and locations, Alert rules and Neighbours apply the same policy in page code. A cardless list gives every row its own card. Glass means the iOS 26+ tab bar buttons.

## 1. Pick the page pattern

Paths are under `src/DashyNMS.Mobile/`. Read the reference page, its shared controls and its view model before writing code.

| Page type | Reference | Layout contract |
| --- | --- | --- |
| Map or canvas tab | `Pages/NetworkMapPage.xaml` | Background fills the screen. Safe controls overlay it. |
| Scrolling dashboard | `Pages/DashboardPage.xaml` | Content scrolls beneath a bottom bar. Its column clears side glass. |
| Searchable list with detail | `Pages/LogsPage.xaml` | Virtualised list, footer clearance, optional detail column. |
| Pushed detail | `Pages/DeviceDetailPage.cs` | Back returns to the actual previous page. The detail owns glass. |
| Dashboard layout editor | `Pages/CustomiseDashboardPage.xaml` | Adaptive virtualised grid, measured side margins, footer clearance. |
| Setup form or picker | `Controls/SetupLayout.cs`, `Controls/SetupPageLayout.cs`, `Pages/GraphPickerPage.xaml` | Fixed header, finite viewport, optional fixed action, explicit inset ownership. |
| Older setup page | `Controls/DashboardBackNavigation.cs` and callers | Keeps native safe areas. Migrate it to the setup form pattern in one change that removes the helper, the old page padding and the native safe-area declaration together. |

Then record every entry route and whether changes save immediately, on Save or through a separate immediate action. Change a page's persistence only with an explicit decision recorded in its issue.

Done when the page has one pattern, a list of entry and return paths, and a stated persistence contract.

## 2. Assign insets and lay out the page

1. Give each edge exactly one owner. The candidates are the native page safe area, the native scroll inset, content padding and an overlay margin. Extra padding must never shrink the usable area or lift an action above the pill base.
2. On iOS keep only the page's top container safe area. Set `SafeAreaEdges="None"` on supporting layouts, `ScrollView` and `RefreshView`. On Android, `SetupPageLayout`, `PushedListLayout`, the Graylog message page and the neighbour link page set `SafeAreaEdges.All` explicitly, and new pushed detail pages follow them. More, Health and the device sections declare `SafeAreaEdges="Container"` in XAML. Settings declares Container on all four edges in C#. In C# write the four-edge constructor, because `SafeAreaEdges.Container` is not a member.
3. `CollectionView` has no `SafeAreaEdges`. On iOS find its native `UIScrollView` from the handler and set `ContentInsetAdjustmentBehavior` to `Never` on `HandlerChanged`, `Loaded` and placement. Copy the pattern in Devices or Edit dashboard. Let the footer supply final-item clearance.
4. Use `CoveredEdges.ForScrolling(page)` for scrolling columns. Its bottom includes the glass action row and its sides include a 12pt gap before a side pill. Add 16pt gutters once. Pad a `ScrollView` content column. Give a `CollectionView` side margins and footer space.
5. Put placement in `PlaceControls()`. Call it on appearing, through `CoveredEdges.Watch`, and after `TabBarButtons.Moved`. Dispatch it so it runs after native frames exist. Recompute column counts on width change so folding and rotation work without reopening.
6. Fit header side clearance with `TopBar.Fit` on a `Layout` after setting its padding. Where `TopLineSpace` owns the header Padding, apply side clearance as Margin. When the BackBar is hidden on iOS 26+, use `PageHeader` so `TopBar.Fit` owns the title line. `TopLineSpace` releases the native top inset only on iOS 27.1+ when reserved regions permit. On iOS 26 keep the native top plus standard header padding and never guess a top offset.
7. Keep scrolling viewports finite, in a star row, outside any wrapping `ScrollView`. Reserve the real wrapped height of fixed actions outside the viewport.
8. Background content, canvas layers and chips scrolled out of sight may pass beneath glass and the camera. Scrolling cards clear a side pill's column. Cards, buttons and every other interactable rest inside the safe area.
9. Add a 20pt input-transparent top fade inside the scrolling viewport, below fixed search and status rows, with a matching 20pt list header spacer (also for group headings and empty states). Empty and failed-load text carries a 32pt margin. Fade endpoints use the viewport surface RGB, including the transparent endpoint. Use `BackgroundLightClear` and `BackgroundDarkClear` over the page background (light hex F4F6F9, dark hex 11141A) and `SurfaceLightClear` and `SurfaceDarkClear` over a card. Over code, fade `SurfaceAltLight` (hex EEF1F5 to 00EEF1F5) and `CodeDark` (hex 0D1015 to 000D1015), as set inline in the backup contents and diff pages. Inside a card, inset the fade 1pt from the stroke. When the BackBar scrolls with the content, show the fade and reserve 20pt only while Back is hidden. With a visible BackBar, start at zero with no fade.
10. Toasts sit 12pt plus side clearance from the sides. Their foot clears bottom glass or aligns with the foot of a vertical pill. A wide pill is a bottom bar only when its top is below the page midpoint, since iPad can put a wide pill at the top.
11. Reserve a visible overlay's measured height, bottom margin and gap (12pt for editor Undo) in the scrolling footer. Recompute after wrapping, text-size change and resize. Keep the reservation through the exit animation.
12. Place the list viewport base by host type.
    - For a tab-aligned card host with a side or bottom pill, anchor the card base at `max(0, H - B)` with no clamp to the native bottom inset. When the bottom glass obstruction exceeds the card base, the footer spacer is the difference plus 8pt. Otherwise, and always with a side pill or the fallback, it is zero. A top-wide pill uses the fallback of 12 plus `clear.Bottom`. Missing geometry uses the fallback and never a cached pill.
    - For a `PushedListLayout` page, use the same card base. The footer is 20pt plus `max(0, SetupActionPlacement gap - card base)`, with no 8pt. Query the pill and glass in the exact native Body host. A visible fixed action takes its gap from `SetupActionPlacement` alone.
    - For a device section (`DeviceSectionPage`), run the cardless viewport to H beneath the pill and glass, stopping short only for a docked keyboard. `SectionListPlacement.Resolve(obstruction, keyboardGap, panelHeight)` owns it. The footer is 24pt plus the overlay obstruction, raised to panel height plus overlay plus 12pt while a panel is visible. Measure the obstruction in the `SectionBody` host with `SetupActionPlacement.BottomGap` on iOS, or 12 plus `ForScrolling.Bottom` elsewhere. A wide graph keeps its own bottom clearance. A narrow graph takes its measured Auto row.
    - For full-height scrolling surfaces, apply side gutters once with `ForScrolling`. Neighbourhoods sets the footer to 12pt plus `ForScrolling.Bottom`, and Settings and More set it to 16pt plus `clear.Bottom`. The footer sits inside the scrolling content and never shrinks the viewport.
    - For a pushed detail page, give the final field a 24pt gap plus bottom clearance.
13. For a list beside a detail pane, apply only insets that reach that column, `max(0, page right clearance - space to the column's right)`. Pad the detail's scrolling content so its background reaches the screen foot. Pass `clear.Right` and `clear.Bottom` directly from the trailing pane. Replace the old detail view model's subscription when the detail changes.
14. Horizontal chip or filter strips sit in a full-width outer native clip with internal resting gutters, so scrolled chips pass beneath the camera. Distribute measured side gutters to the header, search, summary, error and list slots and to the strip's Padding, and leave the strip's ancestor column unpadded. Bound each wrapping choice to the guttered list card width. `TabAlignedListLayout` offers a page-owned gutter callback and both log pages use it.
15. After XAML changes, compile both platforms. Parsing XML does not validate MAUI properties or binding types.

Done when the source shows one owner per edge and the footer formula for your host type. On a device, every tappable control and the final list item scroll clear of glass and overlays, resizing and folding update the layout without reopening, and you recorded H, B, V and V minus B for each pill mode. A card host passes at V minus B equal to zero. A cardless list passes at V equal to H, with the last row still scrolling above B.

## 3. Place the header and navigation

On iOS 26+, every page action except the fixed primary action, plus optional search, belongs to `TabBarButtons` on top-level and pushed pages. Back-only glass is not enough when a pushed page has other actions. The Intelligence chat experiment (#594) places actions beside the title on large portrait screens. The saved Chats modal (#593) owns Back and New chat glass in its header because it has no tab bar of its own. Both reuse `HeaderGlassGroup`. Android and older iOS keep header icons through `IconButton` and `Icons`. Keep two to four page actions and put rare actions in context menus. Show data-dependent actions only while their command can execute, observe `CanExecuteChanged`, and rebuild glass only when state changes.

1. Call `TabBarButtons.Show` on appearing and `Hide` on disappearing. Subscribe and unsubscribe `Moved` on the same lifecycle, removing before adding. `Hide` clears only its own ownership, so either page may receive its event first.
2. With no search pass `null` and `string.Empty`.
3. Pushed pages use `chevron.backward`, `BackBar.PreviousTitle(page)` for the accessible label, and relative back navigation. Hide the header Back on iOS 26+ and keep it on older iOS and Android. The Back title is the explicit previous title first, then the root page's title, then the tab title. Pinned roots have no Back.
4. Open every descendant with a relative route so Back returns to the actual caller from both pushed and embedded origins. Test each entry path, child return and resume for one glass owner. Cover Dashboard, Edit, Add or Set up, then Back. Cover alert, device, then backups. A nested alert, rule or template keeps the selected tab.
5. Reclaim glass on retained-tab return, child return and resume with one active glass owner. Implement `IActivePage` on retained-tab and pushed pages. `Activate` restores header, edge watcher, caller-aware Back, search and placement with idempotent registrations. It never fetches, rebuilds groups, clears the query or filters, or replaces the detail. Only a real Appearing runs the refresh or stale check. AppShell alone owns resume activation.
6. On disappearing, clear the active flag before closing search, dispose the watcher, remove `Moved` and hide glass only when this page owns it. `PlaceControls` checks both the active flag and the Shell's current page at delivery, so a departed page ignores queued geometry. Retain the `Window` instance used for resume subscription and unsubscribe from that instance.
7. Offer setup only for entity kinds that have an editor able to load that kind. Test route and entity ID for each kind.
8. Glass placement measures the actual pill. A vertical pill has actions at the column top and search above the pill. A bottom pill has actions beside or above its right end. Off-screen or missing pills leave the buttons hidden. Placement belongs in `TabBarButtons`.
9. Actions tied to one displayed field, such as Open device and Open rule on a log entry, stay inline as content actions.
10. Embedded detail views add no glass owner and no Back. The host owns glass. Detail actions move into host glass while shown, narrowing removes them and widening restores them without route re-entry.
11. These glass constraints protect cold launch. Only a controller showing its tab bar takes ownership. Cache and recheck the pill before searching the window, skipping `MauiView` subtrees and the app's views. Queue at most one placement per run-loop turn. The breaker pauses for a second after excessive placements, and a "Tab bar buttons: paused" diagnostic means a loop to investigate. Lay out newly supplied actions immediately, including during a pause. Use the shared glass search rather than UIKit `UISearchTab`.

Done when each entry path, child return and resume shows exactly one glass owner and Back lands on the page that opened it.

## 4. Build forms and actions

Draft forms and pickers use `SetupLayout` with `SetupPageLayout`.

1. `SetupLayout` has Auto, star, Auto rows with `Header`, `Body` and optional `PrimaryAction` slots. Put a `PageHeader` in the header, a `ScrollView` or a grid holding a `CollectionView` in the body, and the real Save command in the action slot. The action row reserves its measured height including margin. Omit the action on immediate-save pages.
2. Construct `SetupPageLayout(page, layout, header, scrollers)` once, passing each scroll or list view. It subscribes while visible, disposes its watcher and native tokens on disappearing, and restores glass Back on return and resume. Pair every subscription with its removal and make repeated Appearing add at most one handler.
3. On iOS the native page owns the top inset. The layout owns 16pt gutters plus `CoveredEdges` side clearance and the bottom action gap. Native inset adjustment is off on the scrollers.
4. Forms fill the available width with 16pt gutters. Share heading, section, field, search and status presentation across forms and pickers.
5. Every fixed primary action, including "Got it", aligns its bottom edge with the base of a vertical pill. `SetupActionPlacement` measures the gap as `max(0, native height - pill bottom)`, matching `TabAlignedListLayout`, with no clamp to the native bottom inset. Within `SetupLayout` the action's own bottom margin is zero because the layout owns the gap. Another host may put the gap in the action margin, but the gap has one owner. A bottom bar is cleared with its glass actions. A top pill is treated separately. Without measurement, fall back to the native bottom plus extra glass clearance.
6. The primary action uses the implicit `Button` style, which owns primary colours, contrast and disabled states. Keep a fixed Save visibly disabled while its command runs. Explicit styles such as `SecondaryButton` lose the implicit disabled colours, so give them a themed disabled appearance and check enabled, busy and disabled states in both themes.
7. Each page has at most one primary action, in the fixed slot. Remove any header or in-form copy of it. Other actions take the lowest role that fits.

| Role | Examples | Placement and behaviour |
| --- | --- | --- |
| Primary commit | Save, Add 3 cards | Fixed slot, implicit `Button`. The label names what it commits. |
| Draft check | Test connection | `SecondaryButton` after the fields it tests. It saves nothing. Busy and result text sit directly beneath. |
| Draft change | Reset to desktop's defaults | `SecondaryButton` after the fields. It changes the draft only, and the caption says Save applies it. |
| Immediate action | Forget saved password, Update now, Use whole network | `SecondaryButton` beside text or in a labelled section saying it applies straight away. Confirm irreversible removal. |
| Destructive action | Delete, Sign out | `DangerButton`, with the existing confirmation. |
| Scope choice | Any device | First option above the results. It selects and returns immediately. |

8. Immediate actions act on saved settings, never the unsaved draft. When a draft blocks one, disable it and say why, for example "Save first to update from this repository". State results in text as well as colour. Save commits the draft together and Back discards it without confirmation.
9. Immediate-save pages keep automatic saving, show "Changes save automatically." once beneath the heading, and have no Save, action slot or per-change confirmation. A setting the system refuses, such as a denied permission, shows its actual state and why.
10. A docked keyboard lifts the action and shrinks the viewport, and dismissal restores pill alignment. While visible, the helper disables MAUI's iOS automatic page translation and restores it on exit. Scroll focused fields into view after keyboard geometry changes. `ManualKeyboardScroll` owners Acquire and Release, so only the final Release reconnects. New setup pages implement `IActivePage`.
11. After a failed Save, scroll the error into view and announce it. Capture the non-empty error before dispatching or awaiting the scroll, announce only while it is still current, and repeat for identical failures. A cleared error is not announced. A failed Save keeps its draft. A persistence failure restores in-memory state and does not navigate.
12. Signed numeric fields on iOS use `NumbersAndPunctuation` so a minus sign is available. Android keeps the numeric keyboard.
13. A stepped form names its current step in the header. A chosen entity shows in a labelled section with a Change action naming the entity it replaces. A form with no choose step uses its subject as the header.
14. Four pages sit outside `SetupPageLayout`, and each keeps page-local placement.
    - Sign in and web sign-in let the native page own every safe-area edge (Container on iOS), claim no glass, and avoid `SetupPageLayout` because it installs logged-in glass Back. Sign in is full width with 16pt gutters. Its submit sits in an Auto row outside the finite viewport with a minimum height. The token is always masked and the clipboard is read only on the explicit paste action. Entries, switches and stacked token-help buttons have names and 44pt targets. A failure reveals and announces its error. Busy shows a spinner with text and disables the form. QR import goes through the system camera route, and a valid link always asks for confirmation.
    - Web sign-in stacks a 44pt Cancel above a wrapping title and host. It defers the first web source until the size is positive and finite, and keeps the WebView in a star row outside any `ScrollView`. Failure stays in the modal with a scrollable explanation and a Return to sign-in action. Cancel and Android Back both return Cancelled and commit nothing. Cookies and storage clear in a finally block for every outcome.
    - What's new keeps page-local placement because `SetupPageLayout`'s glass Back would bypass its completion latch. It shows one `PageHeader` title, then Version and the parsed version, with Got it as the fixed action aligned through `SetupActionPlacement.BottomGap` in Auto, star, Auto rows. Only Got it marks the version seen.
    - Diagnostics consent keeps page-local placement because it has no draft Save and both mutually exclusive actions must stay reachable. It stacks them full width in the scrolling body. Allow selected is primary and stays disabled until a choice is made. Don't share derives from `SecondaryButton`, appears first and needs no selection. Both switches default off and are draft-only, and Back writes nothing. One completion latch guards rapid taps and every Back route.

Done when the page shows one primary action aligned to the pill base or clear of the bottom bar, the last field is reachable with the keyboard open, and pill alignment returns after dismissal, fold, rotation, tab return and resume.

## 5. Add search and choosers

1. Every search box is `Controls/SearchField.xaml`. It supplies the 46pt well (radius 23, `SurfaceAlt` fill, themed stroke, magnifier), Search return type, no spell check or prediction, no clear button or keyboard accessory, and `KeyboardDismiss`. Put the page's `Entry` inside it. Pages use this control rather than native `SearchBar` or copied Border markup.
2. List search starts hidden on Android and iOS. Open it from the header search button, the glass magnifier or Command-F. Scrolling, pulling and swiping never open it. Use `ButtonSearch` and `ISearchHost`, with `KeyboardDismiss.OnScroll` false and no `SearchReveal` or swipe handlers.
3. Opening focuses the field and tapping the magnifier again refocuses it. Search, unfocus, a content touch or a real native user drag closes it and keeps the query. Filtering and programmatic scrolling retain focus, so close search on a user drag and not on every `Scrolled` event. Call `KeyboardDismiss.Close` before hiding a focused field. Devices and Alerts still close search on every `Scrolled` event, so copy search handling from Health or Logs. Show a wrapping "Filtered by" caption of the retained query while the field is closed.
4. Setup pickers keep an always-visible search. Keep scope actions, search and status in fixed rows above the results viewport. `CollectionView.Header` holds only list content such as the fade spacer.
5. Use a clipped search slot and animate height and translation together. Use `CoveredEdges.Beside(page, field)` for row-specific clearance.
6. Choosers fed by the server use a `CollectionView`, grouped where headings help, inside the finite viewport. `BindableLayout` is for short fixed lists. Choosing a row in an immediate chooser commits and returns with no Save.
7. Sort human-facing device, graph, group and location choices with `NaturalOrder.Instance` across every source feeding a picker, including port graph descriptions. Ports keep interface order.
8. Picker status sits between search and list, one state at a time. States are loading, load failure with its retry route, empty source, no matches for the query, and a capped result count with a prompt to refine. Rows that cannot be chosen stay visible with their reason in the row text and accessible name.

Done when typing keeps focus through filtering, Command-F reaches search after scrolling, a drag closes search without losing the query, and every picker shows natural order and one status state at a time.

## 6. Handle adaptive layout and accessibility

1. Dashboard and editor share `DashboardColumns.Count` (340pt minimum column, 12pt gap, at most three). Read across then down. Choice grids such as Add card use 160pt minimum tiles, 10pt gaps and at most four columns, recomputed on resize and text-size change.
2. Titles, summaries and field labels wrap. Labels sit above pickers through `SetupSection` (4pt field spacing, 8pt section margin). A control sits beside its label only when both fit at the current width and text size. Use no fixed label or value column widths. Numeric fields show their unit in the label or heading and keep the full value visible.
3. Give each picker an accessible name matching its field, even after a value replaces the Title placeholder. A switch beside wrapping text has its own accessible description.
4. Each selection row or tile is one accessible element with its whole area tappable. Combine name, details, availability reason and selected state in one description. Exclude child labels, preview images, icons and checkboxes from announcements. Checkbox means several choices, tick means one. Update the description when selection changes, state the real action (including whether a notification rule is included or excluded), and make each tap or toggle change selection once.
5. Repeated Set up, More, Change and Remove actions name their target and keep a tap area of at least 44pt height. Keep vertical padding between row content and card borders or dividers.
6. Provide menu reordering (Move earlier, Move later) alongside drag reordering. Keep titles and summaries together with Set up beneath.
7. State severity and unavailable state in text, including in each actionable row's accessible description. Unknown readings are neutral and never OK.
8. List and selection tap targets are whole-row native buttons with an explicit empty style, transparent Normal and Disabled states, and PrimaryTint for pressed and focused. They avoid multiline `Button` text and nested duplicate gestures. A monitored Neighbours card is the exception and keeps its single Border gesture.

Done when every action has a name that identifies its target, no text clips or overlaps at maximum text size in both themes, and a screen reader announces each row once.

## 7. Handle data states and persistence

1. Handle an optional source's 404 at that source before joining reads. Missing membership then cannot discard valid primary records or mask another source's denial, timeout or server failure. Keep primary-source denial, server errors and timeouts distinct from missing metadata. Never invent zero counts for unavailable facts.
2. Preserve complete server messages, expressions, configuration, licence content, SQL and notes. Wrapping never changes copied, shared or compared text. Prose wraps by word and code or long values wrap by character. A truncated preview leads to the complete content.
3. Reuse themed `Error`, `Caption`, `ActivityIndicator` and shared controls. Keep loading, error and status content apart from the finite results viewport. Loading uses one shared busy card (`PullToRefresh.ShowBusyCard`) over the viewport with the native spinner hidden, so the list never jumps.
4. Separate an empty source from an unmatched search, with distinct text. Suppress empty claims while loading or when an error explains the gap. Counts describe the full source and filters change displayed rows only.
5. Setup pages state the retry route beneath an error, such as reopening the page, with no separate button. Section and detail pages show a Retry named for its target as a whole 44pt button, disabled while busy. A supplementary Retry hides while a main error is visible. On a section page, the error line's Retry is the only Retry.
6. Enable Save only after every source needed for the draft has loaded. A failed or superseded load never overwrites saved choices with empty data. Ignore slower earlier loads with a generation guard. Keep saved selections missing from a response until cleared. Update selection rows in place to keep scroll position.
7. A failed refresh keeps the last good rows and shows the error, with cached, stale and offline states keeping readings and a timestamp where one exists. Availability and Resources clear their rows before a refresh and show only the error on failure, because they carry no freshness timestamp.
8. A snapshot detail has no loading, retry or save state. A missing or invalid record clears the view and shows return and reselect guidance.

Done when each state (loading, error, empty, no match, denied, stale) shows its own text, keeps valid data where the page retains it, and offers its retry route.

## 8. Add motion

1. Use named `view.Animate` calls so a new transition aborts the previous. With `ReducedMotion.IsOn` apply the final state immediately.
2. Entrances slide and fade over 220 to 250ms with cubic out. Exits use cubic in. Set initial opacity and translation before showing a view.
3. Selected-item sheets start below their measured height plus bottom margin and 8pt. Hide after exit completes, then clear the selection so the departing sheet keeps its content. Switching selection keeps the sheet up. Version each selection so a stale close cannot clear a newer one.
4. Align a sheet's foot with a side pill, otherwise clear covered bottom and glass actions. Sides are 12pt plus covered edges. Controls above the sheet clear its measured height.
5. Collapsed sheets cap at 200pt on a screen below 740pt high, otherwise 300pt, with a measured Close row, a 44pt title and a 44pt list reserve taking precedence. Expanded sheets use `max(240pt, 60% of content height)` with an internal scrolling list. Measure natural collapsed height before animating a change.
6. Fade surrounding content over 250ms and keep the selected reference until the fade ends. Follow Network map's dimming. Reuse `DashboardToastView` for dashboard Undo.

Done when repeated transitions finish in the right state and Reduce Motion produces no animation.

## 9. Reuse shared controls

Paths are under `src/DashyNMS.Mobile/` unless noted.

| Need | Reuse |
| --- | --- |
| Page, row and scroll clearance | `Controls/CoveredEdges.cs` (`Of`, `Beside`, `ForScrolling`, `Watch`) |
| Glass ownership, pill and action bounds | `Platforms/iOS/TabBarButtons.cs` (`Show`, `Hide`, `Moved`, `PillIn`, `Occupied`) |
| Camera and system occlusion | `Platforms/iOS/ReservedRegions.cs` |
| Header fitting and native safe-area updates | `Controls/TopBar.cs`, `Controls/TopLineSpace.cs`, `Platforms/iOS/SafeAreaWatcher.cs` |
| Search | `Controls/SearchField.xaml`, `Controls/ButtonSearch.cs` |
| Keyboard, motion, search host | `Controls/KeyboardDismiss.cs`, `Controls/ReducedMotion.cs`, `Pages/ISearchHost.cs` |
| Header icons | `Controls/IconButton.cs` (`IconButton`, `Icons`) |
| Dashboard columns | `../DashyNMS.Mobile.Shared/Dashboard/DashboardColumns.cs`, `Controls/CardGrid.cs` |
| Primary button | `Resources/Styles/Styles.xaml` implicit Button style |
| Setup slots, placement, sections | `Controls/SetupLayout.cs`, `Controls/SetupPageLayout.cs`, `Controls/SetupSection.cs`, `../DashyNMS.Mobile.Shared/Services/SetupActionPlacement.cs` |
| Reading order | `../DashyNMS.Mobile.Shared/Services/NaturalOrder.cs` |
| Device evidence | Settings diagnostics entries "Tab bar buttons:" and "Top line on" |

Use `nfloat` where CoreGraphics expects it. Use `pill.Left + pill.Width / 2` or import CoreGraphics for its midpoint extensions. A change to a shared setup helper needs regression checks of every existing setup consumer's layout and lifetime. Land a shared-control change before the pages that use it.

Done when no page copies markup or inset code that a shared control already provides.

## 10. Build the page-specific parts

Apply the section that matches your page, on top of steps 2 to 9.

### Status text and readings

- Every row states status in text (OK, Warning, Critical, Inactive, Unknown, Unavailable) and in its joined accessible description. Null, NaN or infinite readings show a dash, a neutral status and the word "unavailable", and the spoken description leaves out the dash. Sensors and Resources say "Reading unavailable". Health and Availability say "Unavailable".
- Wrap titles, subtitles, values and detail in a stacked readout with no 180pt cap, two-line cap or tail truncation. Overview highlights use the same facts as the full page.
- A sensor with no limits and no state definition shows neutral "No limits", and a non-finite limit shows neutral "Limits unavailable". A defined state follows its resolved severity.
- Resource usage defaults to Warning at 80 and Critical at 95. A valid server warning replaces 80, and critical is `max(valid warning or 90, 95)`. Null, non-finite, negative and over-100 usage is neutral with no bar. Show capacity only when used is non-negative, total positive and used no more than total. Format values from 99.9 up to but excluding 100 with shortest round-trip digits.
- Availability windows sort by duration, shortest first. A non-finite or out-of-range percentage reads Unavailable. An outage with a missing start is a neutral recorded-outage row, and a null end means ongoing.
- Health totals count the whole category before severity and query filtering. Severity memory persists, while query and category stay page-local. Chips are 44pt minimum with a 22pt radius. Health has no server writes.
- Device section rows for VLANs, FDB, ARP, routing and wireless are neutral and nonactionable, with no link, chevron or health claim. Each is a stacked wrapping readout used in both the full list and the overview highlights, with one joined accessible description that excludes child labels. Each section's search covers its displayed missing-value labels, and filtering keeps focus.
- VLAN rows show the 802.1Q VlanNumber (never the internal VlanId) in numeric order, the raw name untrimmed, and the member-port count. An absent or blank name reads "Name unavailable" and a missing count reads "No member ports reported", never zero. Count each returned port once per VLAN, through distinct membership plus IfVlan, with no operational, admin or deleted filtering. Read VLANs and ports each with their own 404 handling. A VLAN 404 with a ports 403, 500 or timeout shows an error and Retry. Valid VLANs with a ports 404 stay with "No member ports reported". Both 404s give a successful empty section. The overview hides its card when empty, clears highlights on failure and keeps a card that retries on open, and its summary counts VLAN records.
- FDB rows show MAC, interface and VLAN, ordered by looked-up interface name then raw MAC (ordinal, ignore case) with unmapped ports first and duplicates kept. An unmapped port reads "port {PortId}". A MAC shows as lowercase colon pairs only when stripping non-hex characters leaves exactly 12 digits. Otherwise it keeps its raw nonblank text, and a blank MAC reads "MAC address not reported". FdbEntry.VlanId is an internal row ID, so resolve it through the selected device's VLAN rows (filtered by DeviceId) to an unambiguous positive VlanNumber. Identical duplicates are accepted and conflicting ones stay unknown. An unresolved or non-positive reference reads "VLAN not reported", with no IfVlan fallback. A VLAN mapping 404 keeps the FDB rows. A fleet VLAN 403 keeps rows that have a positive reference and reads "VLAN unavailable: permission denied" in the list, search, speech and overview. FDB or ports denial, a lookup 500, a timeout and cancellation still fail the section. FDB 404 with a ports error shows an error and Retry. Valid FDB with a ports 404 keeps the port-ID fallback. Both 404s give an empty section. The overview totals every returned entry and shows three highlights in source order.
- ARP rows show IP, MAC and interface in IpSortKey order, so .9 precedes .100. Invalid or missing addresses follow valid ones, and duplicates keep stable order and raw spelling. A missing IP or MAC reads as named unavailable text, an unmapped port shows its port ID, and a blank interface reads "Interface not reported". Normalise a MAC only when it is compact, consistently paired with colons or hyphens, or dotted hex. Leave every other string verbatim. A 404 on ARP never hides a ports 403, 500, timeout or cancellation. A ports 404 keeps valid ARP rows with the port-ID fallback, and both missing gives an empty section. Show IPv6 text without claiming IPv6 coverage. Failed refreshes keep the last rows, and a generation guard stops an old device publishing into a new scope.
- Routing rows wrap titles, subtitles, state, distinguisher and detail independently, with no truncation or two-line or 180pt cap. Overview highlights show the full facts, with an explicit protocol label when the group heading is absent. A 404 on one routing endpoint makes only that protocol empty, keeps the other protocols' rows, and still fails the section on denial, 500, timeout or cancellation.
- Wireless rows use the shared sensor stack with full identity, the class as subtitle, a joined description and explicit status text, and the overview and full section share the same wrapping facts. A missing or non-finite reading reads unavailable, never OK. A sensor with no limits shows neutral "No limits" and one with a non-finite limit shows neutral "Limits unavailable".
- Neighbours classify only explicit local up and down, label the state "Local port state", and treat inactive discovery as "Not active". A monitored neighbour opens relative DeviceDetail and appends "Open neighbouring device" to its description. Graph nodes only select and scroll to the matching list card and announce the full summary with "Show neighbour card". The centre node is an inert "Current device" readout. The narrow graph is `min(280pt, 35% of body height)` and the wide layout starts at 800pt width after sides. Graph colours rebuild on theme change.

### Device ports card

A card scans a list and does not audit one port. Show one card per row, and two when each keeps the 340pt minimum with an 8pt gap (`PortColumns`). The breakpoint is available width, not orientation. Cards fill their track.

- From top to bottom, a card has 14pt padding, a 16pt radius with theme stroke and a 4pt left status edge inside the card. Next come the port name (semibold 17pt) over the muted 14pt description, both wrapping. Then a status row with dot and label at the start and speed at the end, followed by a divider. A three-column table follows, with Traffic and Errors labels and semibold IN ↓ and OUT ↑ values. A divider and the VLANs footer close the card. It has no fixed heights or line caps.
- Raw states, the monitoring flag, a repeated ifName and the graphs line stay off the card and in the model for search, sort, grouping and status.
- `PortStatus` gives one class per port, first match wins. Admin down is grey Disabled. Monitoring disabled or ignored is grey Not monitored. A positive error reading on either side is red. Its label is "Up · errors" for an up port, "Down · errors" for a down port and "Errors" otherwise. Up with zero errors on both sides is green Up. Up with no reading on either errors or rates (the rate, or the last-poll delta when no rate exists) reads grey "Up · errors unavailable", placed beside green Up. Enabled but down is amber Down. Anything else is grey Unknown. Only an explicit down state is Down. The edge uses the decorative status colour and the label the readable text colour, and one trigger sets each.
- Error rates show at full precision per second, red on their own side. A delta-only reading shows "N in last poll". Missing or invalid readings say Unavailable, missing speed says "Speed unavailable" and non-up traffic says "Not shown". Traffic displays in bits and sorting stays in bytes.
- VLANs list unique positive memberships in number order. With no memberships on the device, a direct ifVlan reads "Access/native VLAN". When the device reports memberships, a port without any reads "Not reported". Group by VLAN and Sort by VLAN read the same VLANs as the card, in this order: Trunk (more than one VLAN), then one "VLAN n" group per VLAN by number, then "No VLAN". A port never sits under a VLAN its card does not list.
- The card is one native transparent button of at least 44pt, with its own Disabled, Pressed and Focused states. Its description speaks the visible facts in reading order, then the graph action. Without a real ifName there is no button or chevron, and a readout says graphs are unavailable. Overview highlights keep their own layout, share the card's status labels, and open Ports through their parent.
- Sort & filter keeps seven sorts and four groupings and saves immediately. Done only closes. Keep the heading and Done outside the scrolling body, cap the panel to the measured available height, and reserve its visible height in the list footer until it closes.

### Settings and More

- Settings is one full-width column. Show the DashyNMS mark (54pt, decorative) beside the wrapping server identity. Groups are Preferences (Appearance, Devices, Alert checks, Notifications, Health thresholds, conditional Lock screen widgets), Services, embedded About and conditional Diagnostics, with quiet Level2 headings, generous space and hairline navigation rows. Sign out sits isolated 38pt below the groups. Keep every dynamic summary sentence. The footer shows AppVersion and Trademarks.
- Settings rows are at least 60pt, wrap, and use one native button over the measured row with a joined description. Navigation rows have a chevron and non-navigating rows have none. Use the plain input-transparent `Divider` between visible neighbours, since `RowLine` carries a negative margin. Only the hostname character-wraps. Component versions are read-only stacks of name, full version and state. Service summaries show only the parsed host name, and On or Off means saved enablement, not connectivity. Say which children use drafts (Services, Health thresholds).
- More is a single full-width catalogue with four groups in enum order, quiet headings and hairline rows. Only Health, Map, Network map, Neighbours and Graylog show a summary. Each row has an Open button (wrapping, 48pt minimum) and a separate 44x44 Pin button, with no parent gesture. Pin is an outlined muted shape when unpinned and a filled PrimaryText shape when pinned, with round caps and press and focus feedback. Its name states target, membership and action. State capacity once above the list. A full bar keeps the pin operable and explains how to make room. The bar has three slots, defaulting to Devices, Alerts and Health. Pins save immediately in enum order, and a caption says so. Pinned pages select their tab, unpinned push a relative route, and pinning never navigates. Where SourceGen cannot convert an `x:Static` string to `Path.Data`, inline the path geometry with a provenance comment.

### Intelligence chat

- More-pushed and pinned routes share the setup layout's header, finite
  transcript viewport and overlaid composer. Reserve the measured overlay height
  plus 16pt in the transcript footer, including a stable 44pt Jump to latest
  arrow slot aligned with the capped chat column. Hiding the arrow keeps the
  footer height unchanged. Its measured keyboard/glass gap
  has one owner in `SetupPageLayout`. Keep retained-page activation and
  connection ownership guards.
- Centre transcript, notice and composer in a column capped at 860pt. User
  messages are compact right-aligned bubbles capped at 560pt and 85% of the
  column; assistant replies use their own themed bubble on the left, separate
  from the question. The themed accent backdrop belongs to the page background
  and covers the full page behind header, composer, safe-area gutters and glass. Keep it static
  and decorative. This chat surface omits the opaque viewport-top fade to keep
  the background continuous; retain the 20pt transcript breathing space. The
  empty-chat heading uses themed secondary text to keep contrast on the wash.
- The rounded composer contains a 48 to 140pt growing field, compact native
  detail picker and one primary Send/Stop control with explicit accessible
  names and at least a 48pt target. Native text scrolling must keep the caret
  reachable at the height cap; verify this on device at maximum text size.
- Lookup activity has a 44pt expandable summary with a name saying Show or
  Hide and the outcome. Success needs no second Completed line. Warnings,
  stopped/interrupted states and Retry remain visible. Copy is a rounded 44pt
  clipboard icon at the answer foot; Edit is a labelled icon by the question
  and refuses to overwrite a draft. The view model allows owned, settled
  questions through the normal availability gate. Assistant messages have no repeated heading.
  Starter controls have rounded coloured outlines, hide while the draft has text,
  and submit immediately through the guarded ask flow. Refresh on new or restored
  chats, not ordinary page activation. Settings remains in app Settings.
- Answers render Markdown as native text (#228). Tables sit in a sideways
  scroll with 260pt maximum cells; code wraps by character. Verified objects
  read as name and status in text, coloured only when Colour status indicators
  is on, and retain guarded inline navigation without repeated chip rows. A
  collapsed Open referenced item chooser supplies accessible 44pt targets.
  Source links are 44pt buttons naming title and
  host. Answer images are native buttons announcing "Graph image … Enlarge";
  the enlarged view has a 44pt Close inside the safe area. Device acceptance
  of #228's layout and accessibility is pending.
- Report this answer (#574) adds no visible button. A long press on the answer
  bubble opens a menu with Copy answer and Report this answer. VoiceOver gets
  the same report as a custom action on every element of the answer. The
  reason is a native choice sheet. The preview is a modal with a 44pt Cancel
  above a wrapping title, like web sign-in, and it owns its own safe area. It
  shows the recipient, subject and reason, the question and answer in editable
  fields, a warning-coloured reminder to remove names and addresses, and one
  fixed Open in email app action. Copy report appears only when no email app
  can take the draft. If the chat changes, the preview clears its text and
  Cancel becomes Done. Device acceptance is pending.
- Chat action placement (#594) is an experiment awaiting installed-build
  approval. On iOS 26+, a window at least 600pt wide (`ScreenLayout.RegularFrom`)
  and taller than it is wide puts Saved chats and New chat in a compact glass
  group (`HeaderGlassGroup`) beside the title. This covers iPad in portrait and
  iPhone Duo's inner display in portrait. Narrower windows (phones, the Duo's
  outer display, narrow Split View) and landscape keep them in the tab bar's
  glass. Back stays in the tab bar's glass, and there is no Settings action.
  `ChatActionPlacement.Resolve` holds the whole rule, and resizing, rotating or
  folding moves the actions without reopening the page.
- Saved chats (#229) open from a list button beside New chat (glass on
  iOS 26+, header elsewhere) in a modal sheet. Like the report preview, it has a 44pt Done
  above a wrapping "Chats" title, says chats are saved on this phone for the
  signed-in server, shows any status (unreadable files, unavailable key store,
  single-chat mode) and offers New chat. Chats are grouped under Core's activity
  headings (Pinned, Today, Yesterday…). Each row is at least 56pt with title and
  when, and taps to open. Swiping right reveals Pin or Unpin; swiping left
  reveals Rename and Delete. A long press opens the same actions as a menu, and
  VoiceOver offers Pin or Unpin, Rename and Delete as custom actions. Delete
  always confirms. The open chat is tinted and read as "Open now". New chat
  and opening another chat share one decision: a chat whose latest text is
  confirmed saved is left at once, after waiting no more than two seconds. A
  save that failed or hasn't finished offers Try again, Leave anyway and
  Cancel, explaining that older history may remain and a pending save may finish. A chat that isn't being saved gets the red discard confirmation.
  Cancel keeps the question, answer and draft on screen. Device acceptance is pending.
- Single-chat mode (#230) adds no chat UI; the saved-chat sheet lists nothing
  and says the mode is on. While it is on, the chat and its
  sheets are covered in the app switcher: on iOS by a plain window with the app
  name and "Hidden while single-chat mode is on" on the system background,
  drawn before the snapshot and removed on return; on Android by a secure
  window. On iOS the keyboard is dismissed first, as its predictive bar sits
  above any cover, and isn't raised again on return: the draft stays and the
  user taps the composer. Other pages aren't covered; anything the app can't
  identify on screen is. Settings › Intelligence has a separate
  Saved chats card with a caption saying the mode hides but doesn't delete, and
  an outlined Delete saved chats `DangerButton` with its own disabled colours.
  It always confirms. The result appears beneath it in text (critical colour on
  failure) and is announced. A partial removal offers Try again. Device
  acceptance is pending.
- Use the Sparkle identity from desktop for the pinned icon. Keep #227 open
  until the user accepts the installed appearance, keyboard, themes, text
  sizes, accessibility and narrow/wide layouts; CI does not provide acceptance.

### Logs, Graylog and details

- Logs and Graylog share one structure. The page has a fixed header and search, a horizontal strip of native 44pt choices in a full-width outer clip, fixed summary and status rows, then the finite virtualised list card. `CollectionView.Header` keeps only the 20pt fade spacer. A fixed device scope is a read-only wrapping label. Network scope keeps separate change and clear choices. Clear filters is the summary's 44pt link.
- Each row is one native Open button over complete plain multiline text with stacked textual severity, facility or type, user and full local time, with "unavailable" fallbacks. The button's accessible name includes the entry ID. Logs rows use 11pt vertical padding and 6pt between facts. Graylog rows use 10pt and 4pt. Unknown severity reads Event and never OK. Filter choices name their current value ("All devices", "Any type", "Any state"). Auto-refresh states On or Off. Compare same-ID refreshed rows on every displayed fact.
- Log entry details show the message as the primary heading in character-wrapped plain text that keeps exact whitespace, with stacked fields and the raw hostname or sysName. Unknown severity reads Event. Open device and Open rule are inline 44pt content actions, and the device link is omitted for device-scoped entries. An invalid or missing record clears the view and shows return and reselect guidance.
- Graylog message details stack labels over character-wrapped plain-text values in a monospaced face, keeping every raw field. Unavailable time, level or source says so. On iOS 26+, eligible Show only and Open device move into host glass, with an inline fallback on Android and older iOS. Open device is hidden in device scope.
- Alert rules show Selected in text in split mode only, with the highlight tied to rule ID. Wide width embeds the detail in place with its Back hidden. Narrow width pushes the rule route with the unchanged integer ID. Notification Change and Undo save local preferences immediately and Back does not cancel them.
- Rule and template detail pages are read-only and preserve text exactly, with no trimming and no invented fallback. Alert detail keeps one primary acknowledge action, and unacknowledge, fault, device and notification controls stay subordinate. Detail pages use full width with measured gutters. Supplementary failures show an explicit status and a Retry.
- Neighbourhoods are local settings with a finite virtualised list, named Edit and More actions at 44pt, and adjacent-only moves. Reorder and delete save immediately with a destructive confirmation and no Undo. The editor is a draft form. Save requires a nonblank name and valid regex, keeps unsupported rules and extension fields, and uses stacked native pickers.
- Inventory is one column. Put Back, Search and one "Tree options" glass action in the header, or a header icon opening the same measured FilterCard on Android and older iOS. The panel holds Expand all, Collapse all, Show ports and Done in a finite scrolling body, with the heading and Done outside it. The summary stays in the list header, and expansion and Copy serial stay on each row. Tree options appears whenever loaded rows exist, hides on an empty source and disables while busy.
- Graph legends are wrapping facts (name, detail, value, Shown or Hidden text) with a filled or outlined series-colour marker. One transparent native toggle covers the facts with a Hide or Show name. A separate Only action has a 44pt minimum and announces "Show only [name]". Long-press isolation does not also fire the toggle. Keep drawn series order and the last visible series. Hidden text stays readable. Show all goes in the glass action beside Back while a series is hidden, or a header eye icon elsewhere. Range chips wrap at 44pt high and 64pt wide minimum. The wide layout puts the graph beside a 300pt legend at 800pt available width.
- Backup history lists loaded rows newest first, labels the newest dated one Current as a version label only, and compares at most two backups, older first. Contents show numbered monospaced character-wrapped lines in a finite `CollectionView`, and Share exports the original raw bytes. Diff keeps its existing presentation and exports the complete unified diff. On iOS 26+ compare, search and share move into glass.
- Maintenance schedule has one Schedule button in the fixed slot, disabled for the whole command including confirmation. Blank title or notes become null. Back discards the draft.
- Licences use package H2, licence H3, scalable themed body text that word-wraps prose and character-wraps URLs, and the full destination in a wrapping label under a whole-row 44pt native button. A failed read shows a truthful error and Retry, never replacement text. The website opens externally from the exact catalogue URI.

### Map

- Keep WebView geometry stable beneath overlays. Zoom targets are 44px. Hide zoom only when both targets cannot fit with overlay clearance, and keep pinch. Gate first layout on live container width and height, not cached `getSize`. Attach layer groups through `whenReady`, guard marker icon access before load, and pass initial dimming through marker options. Seed a view only when cached and live dimensions differ, then invalidate and fit inside the programmatic guard.
- Selecting a location centres it above the card using an offset camera centre. Resize never centres. Clearance changes pan by the change in uncovered centre. Marker clicks store a keyed target zoom. Dim non-selected groups over 250ms.
- A searchable finite location picker, pushed through `SetupPageLayout`, offers an accessible alternative to tapping pins. Choosing selects on the original map and pops one page.
- Header side clearance uses Margin, with a full-width opaque BoxView behind the gutters and an invisible 44pt `TopBar.Height` spacer in an Auto first row.
- Preserve bundled Leaflet, the nonce CSP, JSON escaping, the bridge allowlist, external-browser attribution and the custom tile setting. Unknown devices count in the pin total with a grey ring segment. CI runs the embedded bridge with a Leaflet camera model, covering document load before geometry, cached zero size, later clearance, resize, empty pins and pending selection.

Done when each bullet in your page's section has a recorded pass.

## 10a. Existing page contracts

Apply the contract of the page you change. Each setup page uses `SetupLayout` and the role table in step 4.

### Setup pickers and forms

- The graph picker (`GraphPickerPage`) is the stepped setup reference. Its headers read "Choose a device" and then "Choose a graph". The chosen Device section offers Change device, and the optional card title is the first draft field. Device and device-wide graph choices use natural order. Ports keep interface order, while port graph descriptions in both source modes use natural order. DeviceGraphs keeps Traffic as the initial port graph. An empty graph source explains that the device has no graphs.
- The sensor picker (`SensorPickerPage`) has a scrolling card-title header that starts below the fade, a finite sensor list and a fixed Save. Each row's accessible content is sensor name, device, reading and selected state, with the checkbox excluded. Reopening retries a failed load, and another Save retries a failed save while keeping the draft.
- Ranked cards (`TopCardSetUpPage`) have no choose step, and the heading is the card kind's title. The fields are card title, Rows, Rank by and, for Top errors, Quiet ports, in that order. Each field uses `SetupSection`, and the switch keeps its own accessible description.
- Spotlight (`SpotlightPickerPage`) opens from a log card's Dashboard Spotlight line or its Set up action in Edit dashboard, and both pass the selected widget ID. The header is "Spotlight". Whole network is a separate immediate-action section with "Every device. Applies straight away." and Use whole network. Below it, an "Or keep it to" section holds Devices, Group and Location, search, selected count and prompt, status, then a finite list. Devices allow several selections and a group or location is a single choice. Missing saved choices stay at the top until unticked. The fixed Save waits for every source to load. Whole network saves and returns at once, and other choices wait for Save.
- Add card uses `SearchField` and the choice grid breakpoint from step 6. Category counts, unavailable reasons, multi-selection and add-below position are kept. A single Sensors or Graph card opens its setup automatically. Add is the fixed primary action and names what it adds.
- The Graylog and Unimus connection forms share one shape. The order is enablement, server, login or token, then options. Save is fixed. Test connection checks the draft, saves nothing and may add a trusted certificate to the draft only. Forget saved password or token removes the stored secret immediately after confirmation and stays subordinate.
- Sensor definitions treat the switch and source as the draft. Installed version, last check and status are read-only. Update now acts on the saved source and explains when an unsaved draft disables it.
- Thresholds stack value rows when crowded. Reset to desktop's defaults changes the draft only, Save validates and applies, and Back discards.
- All seven settings sections share one layout and save automatically. Dependent visibility, permissions, privacy text and the notification rule list stay as they are.
- The rule picker and Graylog device picker are immediate choosers with picker statuses. Already-added rules state why they cannot be added. Any device is the scope choice above the results. Keep suggestions, group headings, the result cap and natural order.

### Dashboard editor

- `CustomiseDashboardPage` uses the dashboard's adaptive columns and saves immediately. The More menu offers Duplicate where supported, Add a card after, Move earlier or later, and Remove. Removal keeps Undo, shown through the shared `DashboardToastView`. Sensors and Graph cards open their pickers, Top cards open ranked-card setup, and Event log and Graylog cards open Spotlight.
- Add card opens setup automatically only for a single Sensors or Graph card. Returning from any child refreshes the saved cards.
- The six-dot grab handle uses `Aspect="None"` and is hidden from screen readers, because `Aspect="Uniform"` stretches its geometry into clipped bars. Bind custom properties on `DashboardToastView` through an explicit `Binding` object.

### Sensor persistence

- The sensor picker drafts its title and selections, commits them together on Save and discards changes on Back. Opening without a card ID creates a card only on Save. A card added explicitly from the editor remains when its later setup is cancelled.
- Empty selections are supported. A failed save keeps the draft. Saved sensors absent from the server response are retained.

Done when your page matches its contract bullet by bullet and keeps its draft, Save and stored fields as listed.

## 11. Run the acceptance checklist

Record each applicable item as passed, failed or unavailable. Record code review, CI and device results separately. Have the coding agent review the PR's full diff against this guide with its review command. Fix confirmed findings in the PR, or note in the PR why a finding stands. Native success is unverified until an installed build is measured. Device results state the build, device and OS, entry mode, theme, text size, expected against actual, and evidence. Test write flows with mock writes only.

- [ ] Each supported kind routes to a compatible editor with the selected ID. Back returns through every caller with one glass owner.
- [ ] Insets have one owner per edge, and the footer formula for the host type is applied. Final content and actions clear glass and visible overlays through transitions.
- [ ] Initial text and empty states read clearly below the fade at rest.
- [ ] Titles and controls fit narrow screens and larger text in both themes.
- [ ] Folding and rotation recompute clearance and columns without reopening.
- [ ] Search closes the keyboard correctly and keeps its query.
- [ ] Loading, empty, error and selected states stay readable and retryable.
- [ ] Save or automatic save and Back follow the approved contract. A failed load cannot save an empty draft.
- [ ] One primary action. Secondary, immediate and destructive actions follow the role table and say what they change.
- [ ] The fixed primary action aligns with the vertical pill base or clears a bottom bar and its actions, on inside and outside screens and after fold, rotate, keyboard dismissal, tab return and resume.
- [ ] Screen-reader names, selection states and activation work. Drag actions have an accessible alternative.
- [ ] Motion respects Reduce Motion and repeated transitions finish correctly.
- [ ] Relevant shared tests and Android and iOS builds pass.
- [ ] Device criterion for layout. H, B and V are recorded on Duo outer, inner portrait and inner landscape, after app resume, and on an ordinary iPhone when available. First-tab changes include cold launch. Add both themes, maximum text, VoiceOver, TalkBack and Reduce Motion.
- [ ] Diagnostics are checked for overlap or placement loops.
- [ ] Fixtures and screenshots use synthetic network data.

Keep one issue per PR. Use a non-closing reference until the user accepts the device result, then close only the accepted issue, and tick its epic entry on closure. CI success alone does not establish visual correctness.

When the user authorises a batch of page issues for one device test, follow these three workflow lines.
- Merge the batch PRs one at a time in the agreed order, landing dependencies before their consumers.
- Build one TestFlight after the final merge.
- The user accepts each issue separately.

Done when every item is recorded as passed, failed or unavailable, and each device-gated issue has explicit user acceptance.

## 12. Still to verify

These device checks remain open. Confirm each on a device, then fold any result into the steps above.

- Measure H, B and V on both Duo displays for Health, Alert rules, Neighbourhoods, Neighbours, More, the device sections (including Device graphs and Interface graphs) and the Graylog list, which have no device acceptance.
- Check Ports at maximum text, with two columns, and in short landscape with the picker wheel and keyboard clearing Sort, Group and Done.
- Check a floating iPad keyboard on forms, and activation with a keyboard already visible at retained-tab return and resume.
- Check Neighbourhoods top text on iOS 26.x and 27.1+, and record title glyph and camera bounds. `CoveredEdges.Beside` uses real reserved regions only on iOS 27.1+.
- Check the map's WKWebView gestures with zero insets and no bounce, attribution links above the selected card, huge text in short landscape and large chooser catalogues.
- Check the Graylog list's camera layering and pill base on an installed build, and record frames before changing offsets.
- Check boundary text that rounds into a different status (94.96 as 95% Warning, 79.96 as 80% OK, 99.86 as 99.9% Warning, 98.96 as 99% Critical).
- Check that Dashboard, SensorPicker and WidgetSnapshot treat custom no-limit sensors as neutral.
- Check the Back-during-submit race on maintenance and other forms that pop on success.
- Sensors, device handoff: open the section from Health, from Devices (narrow and split), from the Dashboard and from an alert. Each must show the right device, return to the real caller with Back, and never show the previous device's rows or error under a new device while a slow load finishes.
- Sensors, threshold refresh: save a changed synthetic limit in Settings, return to the retained Sensors page and refresh. The new severity must agree with Health. Repeat with Back-discard and reset-draft without saving, and with a definitions update, disable, source change and offline refresh. Nothing may show a false OK or an immediate update that was not fetched.
- Sensors, data states: check cold load, a genuinely empty source, no search matches, cached offline, timeout and server error, denied with no cache, and Retry success and failure. Loading, error and age must be honest, and a retained reading must be distinguishable from a new one.
- Sensors, readings: check temperature, dBm, signal, fan and custom sensors with positive and negative values, null, NaN and infinite readings, and known, unresolved and label-only states. Unknown, missing and unassessed values stay neutral.
- Map, cold launch and refresh: open the pinned and More-pushed Map, return from device detail and from Show in Devices, switch tabs, resume, rotate and fold repeatedly. Exercise an empty server, all devices unlocated, a mix of located and unlocated, a failed first load, and a failed refresh followed by Refresh. The old map must survive a failed refresh, and there must be one glass owner and no placement loop diagnostics.
- Map, selection and tiles: select a pin or chooser location and check that it centres above the card and closing does not flash. Check merged pins, identical coordinates, long location and device names, the last device row, VoiceOver and TalkBack chooser operation, default and custom HTTP(S) tile settings, external attribution navigation and Reduce Motion.

- Chats menu refinements (#593): retain the accepted popover presentation. Check header glass Back returns to Intelligence and New chat follows the existing leave decision, including disabled state while answering. A partial swipe of about 48pt must settle with tappable Rename and Delete; a full swipe must not invoke Delete. Check pinning in the other direction, recycled rows after scrolling and regrouping, separators, both themes, maximum text and VoiceOver custom actions. CI builds do not establish visual acceptance; keep #593 open until the user accepts an installed build.
- Intelligence saved chats (#229): open the sheet from glass and header, open, pin, rename and delete chats by swipe, long press and VoiceOver, and check the sheet at maximum text and in both themes. End the app mid-answer and relaunch: the question and partial answer return marked Interrupted. Check that a long chat streams without stutter while saving, and that a second server and a fresh sign-in list none of the first's chats.
- Intelligence chat actions (#594): on iPad in portrait and the Duo's inner display in portrait, check that Saved chats and New chat sit beside the title with the title clear of them, and that nothing overlaps the status bar or a top tab bar. Check maximum text, VoiceOver order (title, Saved chats, New chat), New chat disabled while answering, and composer space with the keyboard up. Rotate, fold and resize Split View to move the actions to the tab bar glass and back without reopening; the composer must follow. Check that phones and the Duo's outer display look as before.
- Intelligence single-chat mode (#230): turn it on with saved chats present and check they return when it is off. Ask, type a draft and force-quit mid-answer; relaunch restores nothing. Open the app switcher from the chat, the saved-chat sheet, the report preview and an enlarged graph, and check each is covered with no keyboard or predictive suggestions visible, including with the composer focused and a draft typed; check other pages aren't. Return and check the cover clears, the keyboard stays down with the draft intact, the glass is at rest and tapping the composer brings the keyboard back as normal, and that Control Centre or a notification pull does the same. Use Delete saved chats, then relaunch and sign in again to check none come back. Check the Settings card at maximum text, with VoiceOver and in both themes.

Record each of these separately as passed, failed or unavailable against the build and device tested. An earlier code review or CI result is not device acceptance.
