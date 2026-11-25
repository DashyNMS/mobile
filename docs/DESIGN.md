# DashyNMS Mobile iPhone style guide

This guide covers iPhone and iOS only. Android will be added in a later revision.

It defines how every iPhone page looks, scrolls, adapts and behaves. Follow it when building a new page or changing an existing one. Where existing page code differs from this guide, follow the guide for the parts you touch.

Paths below are under `src/DashyNMS.Mobile/` unless they start with `Shared/`, which means `src/DashyNMS.Mobile.Shared/`.

Read these when they apply:

- `docs/INTELLIGENCE.md` before changing Intelligence tools, answers, instructions or chat features. This guide covers only how the chat looks and lays out.
- `docs/DEVELOPING.md` ("Commits, pull requests and issues") before opening an issue or PR.

## 1. Core principles

`Pages/IntelligencePage.cs` is the layout reference. Every iPhone page follows five rules from it:

1. **Continuous scrolling.** At rest, every scrolling viewport runs to the bottom of its host page behind the bottom tab bar, its Liquid Glass and any fixed foot control, softened by a foot fade. Internal footer padding, never a shortened viewport, lets the last item scroll clear. With a docked keyboard, lists and forms stop 12pt above the keyboard top, while Conversation stays at the host base and reserves keyboard plus composer in its footer.
2. **Glass holds page actions.** Back, search and frequent page actions live in Liquid Glass by the tab bar, or beside the title on tall, wide windows. Content carries only actions bound to one item or field.
3. **One strong title.** One Display title, quiet captions, semibold row titles and generous space. Size, weight and spacing carry hierarchy; colour carries status and interaction.
4. **Adapt to measured geometry.** Every position comes from live window dimensions, safe areas, reserved regions and native pill frames. The same page works on standard iPhones and across every iPhone Duo pose, updating live while open.
5. **Honest states.** Pages show what they know, how fresh it is and what failed. Counts and timestamps appear only after the data that supports them arrives.

The Intelligence page's radial gradient backdrop belongs to the chat alone. All other pages use the flat page background.

## 2. Build a page

Work in this order. Each step ends with a concrete check.

1. **Choose one pattern** from section 7 and read its reference page, layout helper and view model. Done when you can name the pattern, every route into the page and the immediate parent page Back returns to on each route.
2. **Set the save contract.** Choose immediate save, draft with Save, or read-only. Keep an existing page's contract unless its issue changes it. Done when the header or action slot makes the contract explicit (section 8).
3. **Assign one owner per edge** (section 5). Done when you can name the single owner of top, left, right and bottom for the header, viewport and each fixed control.
4. **Place navigation and actions** (section 6). Done when exactly one page owns glass after entry, child return, tab return and resume.
5. **Build from tokens and shared controls** (sections 3 and 14). Done when no page duplicates markup or inset code that a shared control supplies and no literal hex colour appears outside the exceptions in section 3.
6. **Handle every data state** (section 10). Done when loading, empty, no match, error, denied, stale and unconfigured each have distinct copy, or you have noted why a state cannot occur.
7. **Verify against section 15.** Done when every applicable line is recorded as passed, failed or unavailable.

## 3. Visual tokens

`Resources/Styles/Colors.xaml` holds colour values, `Resources/Styles/Styles.xaml` holds implicit control styles and `App.xaml` holds named styles. Cite the key and read values from those files rather than duplicating hex codes.

### Colour

Bind every colour with `AppThemeBinding` (or `SetAppThemeColor` in C#) so open pages follow live theme changes.

| Role | Light key | Dark key |
| --- | --- | --- |
| Page background | `BackgroundLight` | `BackgroundDark` |
| Card, list and sheet surface | `SurfaceLight` | `SurfaceDark` |
| Tiles, wells, bubbles and segment bars | `SurfaceAltLight` | `SurfaceAltDark` |
| Code, configs and diffs | `SurfaceAltLight` | `CodeDark` |
| Primary text | `TextLight` | `TextDark` |
| Supporting text | `TextSecondaryLight` | `TextSecondaryDark` |
| Quietest text | `TextFaintLight` | `TextFaintDark` |
| Links, active icons and selected text | `PrimaryTextLight` | `PrimaryTextDark` |
| Filled primary button | `ButtonPrimary` with `White` | `ButtonPrimary` with `White` |
| Card stroke, `Divider` and `RowLine` | `StrokeLight` | `StrokeDark` |
| Unselected chip and `SearchField` stroke | `Gray200` | `ChipStrokeDark` |
| Transparent fade stop over page | `BackgroundLightClear` | `BackgroundDarkClear` |
| Transparent fade stop over card | `SurfaceLightClear` | `SurfaceDarkClear` |

Status colours use four forms of one base name (`Critical`, `Warning`, `Ok`, `Maintenance` or `Inactive`):

- `<Name>Color` fills a dot, bar or accent edge. Never use dot-fill tokens for text.
- `<Name>TextLight` and `<Name>TextDark` colour words and numbers.
- `<Name>Tint` fills a selected chip.
- `<Name>Stroke` outlines a selected chip.

Bind status values through the `StatusColor`, `StatusText` and `StatusTint` converters in `App.xaml`. Selection highlights use `PrimaryTint` and `PrimaryStroke`.

Every enabled normal-size text and surface pair (including `TextFaint*` on page backgrounds, `SurfaceAlt*` placeholders, and `<Name>Text*` composited over `<Name>Tint` on `Surface*`, `Background*` and `SurfaceAlt*`) must meet at least 4.5:1 contrast in both themes (disabled controls are exempt). Interactive boundaries that identify a control without visible text or icon, focus indicators, and composer chips on `SurfaceAlt*` must meet at least 3:1 non-text contrast against their surface in both themes (labelled filter chips and `SearchField`, which carry text and a magnifier icon, are exempt). Use `TextSecondary*` wherever `TextFaint*` does not meet 4.5:1 on that surface.

Literal colours are permitted in only three places: translucent fade stops with no token, the Intelligence chat backdrop, and HTML inside a `WebView`. Match each literal to a token value and provide both themes.

Configure every native web view as non-opaque with a clear or themed background over the page background before first navigation, so Dark mode never flashes white while loading.

### Typography

Fonts are registered in `MauiProgram.cs` and scale with Dynamic Type. Choose a named style and override wrapping where needed. `RowTitle` and `CardNote` default to `TailTruncation`; set `LineBreakMode="WordWrap"` (or `CharacterWrap` for identifiers) wherever full identities or values must wrap.

| Use | Style | Font family and size |
| --- | --- | --- |
| Page title | `PageTitle` (inside `PageHeader`) | `Display` (Sora Bold), 28 |
| Tile number | `TileNumber` | `BodySemibold`, 24 |
| Card heading | `CardTitle` | `BodySemibold`, 15 |
| Row title | `RowTitle` | `BodySemibold`, 15 |
| Body copy | implicit `Label` | `BodyRegular` (IBM Plex Sans), 14 |
| Summary, label or explanation | `Caption` | `BodyRegular`, 13 |
| Timestamp, age or ID | `Faint` | `BodyRegular`, 12 |
| Trailing card heading note | `CardNote` | `BodyRegular`, 12 |
| Group heading | `SectionHeader` | `BodySemibold`, 12 (uppercase) |
| Pill label | `PillText` | `BodySemibold`, 12 |
| Error message | `Error` | implicit `Label` in `CriticalText*` |
| Code, OIDs, configs and raw values | `FontFamily="Mono"` on a `Label` (no named style) | `Mono` (IBM Plex Mono), 13 (or 11 to 12 in line-numbered views) |

Write headings and labels in sentence case (`SectionHeader` applies uppercase itself). Keep standard unit casing everywhere (for example `dBm`, never `dbm`).

### Spacing and shape

Dimensions below are the iOS target spec.

| Metric | Points |
| --- | --- |
| Page horizontal gutter (applied once, plus measured side clearance) | 16 |
| Minimum gap before a side tab pill or reserved system region | 12 |
| Top bar line (`TopBarPadding` / `TopBarHeight`) | 16,16,16,8 / 44 minimum |
| Gap between dashboard cards | 12 |
| Gap between chips, tiles and stacked cards | 8 |
| Field label to field / field to next field | 4 / 8 |
| `Card` padding | 14 |
| `ListRow` padding (sides and top) / minimum height | 14 and 11 / 56 |
| Viewport top fade and matching leading content spacer | 20 |
| Space below last item beyond `BottomGap` (36pt above bottom pill, 24pt above side-pill base or Save top) | 24 |
| Space below last item above an open filter panel | 12 |
| Foot fade height above the highest bottom obstruction | 24 |

| Element | Corner radius | Stroke |
| --- | --- | --- |
| `Card`, `ListCard` (closed inline cards), question bubble | 16 on all corners | 1 (bubble 0) |
| `OpenListCard` (viewport-hosting list cards, based on `ListCard`) | 16 top, 0 bottom | 1 |
| Chat composer | 18 | 1 |
| `Tile`, `Field`, `Pill`, implicit `Button`, `IconButton` | 12 | 0 |
| Filter chip, starter question, choice chip | 22 (minimum height 44; 48 in composer) | 1 |
| `SegmentBar` and `Segment` | 14 and 10 (minimum height 44) | 0 |
| `SearchField` | 23 (at 46 high) | 1 |
| Circular Send / Stop button | 24 (at 48) | 0 |
| Liquid Glass group | half its short side | none |

Use `MinimumHeightRequest`, not fixed `HeightRequest`, on chips, segments, `SecondaryButton`, `DangerButton` and `LinkButton` so controls grow with Dynamic Type. A selected card uses a 2pt `PrimaryStroke` and 1pt less padding so its contents do not shift. The implicit `Border` style is square and grey, so give every `Border` a named or complete explicit style. The implicit `BoxView` is transparent, so always set `Color`.

## 4. Geometry and poses

### Measured facts

Window size, safe area, tab pill, glass frames, reserved regions and keyboard bounds are independent facts. Read each from its owner and recompute whenever any changes.

| Fact | Read from |
| --- | --- |
| Native safe area | `CoveredEdges.Of(page)` |
| Scrolling column clearance (includes pill, glass and 12pt side gap) | `CoveredEdges.ForScrolling(page)` |
| Page-level obstacles overlapping one row (clock, Wi-Fi, camera) | `CoveredEdges.Beside(page, view)` |
| Tab pill bounds in a target view | `TabBarButtons.PillIn(view)` |
| Combined pill and glass frames in a target view | `TabBarButtons.Occupied(view)` |
| System reserved regions on iOS 27.1+ | `ReservedRegions.Occupied(view)` |
| Width class and split threshold | `ScreenLayout.Classify(width)`, `ScreenLayout.SplitsListAndDetail(width)` |

Two pill geometries drive placement. A **side pill** is taller than it is wide. A **bottom pill** is wider than it is tall with its top below the page midpoint. A wide pill in the top half is a top bar and leaves the foot clear.

Wire `CoveredEdges.Watch(page, PlaceControls)`, `TabBarButtons.Moved` and page `SizeChanged` to one `PlaceControls` method. Dispatch placement so native frames exist when it runs.

### Supported poses

These dimensions are test points, not hardcoded device checks. Layout code responds only to measured geometry and the thresholds below.

| Pose | Review size (pt) | Tab pill | System region | Page actions | Content |
| --- | --- | --- | --- | --- | --- |
| Standard iPhone portrait | 402x874, 440x956 | Bottom | Status bar and Dynamic Island on top | `TabBarButtons` by bottom pill right end | Single column |
| Standard iPhone landscape | 874x402, 956x440 | Measured | Measured sides | `TabBarButtons` by pill | Split lists (`>= 700pt`), adaptive columns |
| Duo outer portrait | 466x678 | Right side | Clock and Wi-Fi in right column | `TabBarButtons` at top of right column | Single column, left of pill |
| Duo outer landscape | 678x466 | Left side | Measured | Collapsed `TabBarButtons` above left pill | Single column, right of pill, short viewport |
| Duo inner landscape | 951x669 | Right side | Status area along part of top | `TabBarButtons` in right column | Split lists, 2 to 4 columns |
| Duo inner portrait | 669x951 | Bottom | Status area along part of top | `HeaderGlassGroup` beside title; Back by pill | Regular single column (1 dashboard column; 2 columns require 692pt plus gutters) |
| Duo half-open | 951x669, 669x951 | Matches open | Matches open | Matches open | Same continuous layout as open |

Half-open uses the same measured layout as fully open at the same dimensions. Avoid splitting content around a hinge unless the system reports it in `ReservedRegions`.

### Breakpoints and thresholds

| Decision | Rule and owner |
| --- | --- |
| Regular width / title-row glass | `ScreenLayout.RegularFrom` (600pt) and taller than wide (`PageActionPlacement.Resolve`), when title and glass both fit the safe lane |
| Split list and detail | `ScreenLayout.SplitFrom` (700pt); list pane `ScreenLayout.ListPaneWidth(width)` (340pt, or 400pt at `WideFrom` 1000pt) |
| Readable prose column | `ScreenLayout.ReadableWidth` (680pt); Intelligence chat caps at 860pt (user bubble `min(560pt, 85%)`) |
| Dashboard and editor grid | `DashboardColumns.Count` (340pt min track, 12pt gap, max 3 columns) |
| Choice grid (`AddCardPage`) | `CatalogueColumns.Count` (160pt min scaled by text size, 10pt gap, max 4 columns) |
| Port cards | `PortColumns.Count` (340pt min track, 8pt gap, max 2 columns) |
| Composer beside bottom pill | Free lane >= 360pt (`ChatComposerPlacement.MinimumLane`) |

### Live pose transitions

Folding, unfolding, rotating, resizing or changing text size updates the open page in place while preserving scroll position, selection, draft, focus and search query:

- Recompute grid columns from the container's own width and invalidate recycled preview cells whenever column count or tile width changes.
- Keep maps and canvases centred on the uncovered viewport: shift by the change in uncovered centre if the user panned, or refit if untouched.
- Re-query live keyboard geometry in the new window rather than reusing a frame saved before the fold.
- Retain a hidden split detail's selection when narrowing and restore it if the window widens again.

## 5. Insets and scrolling

### One owner per edge

| Edge | Single owner |
| --- | --- |
| Top | Native page `SafeAreaEdges`: `SafeAreaRegions.Container` on top, `None` on left, right and bottom. `PageHeader` applies `TopBar.Fit`, moving the title beside the status area when `TopLineSpace` has room on iOS 27.1+. |
| Sides | One `PlaceControls` method applying `16 + CoveredEdges.ForScrolling(page)` left and right. |
| Bottom | Internal footer inside the scrolling content, or the foot-placement helper for a fixed overlay control. |

Set `SafeAreaEdges="None"` on inner layouts, `ScrollView` and `RefreshView`. Because `CollectionView` lacks `SafeAreaEdges`, set its underlying `UIScrollView.ContentInsetAdjustmentBehavior = UIScrollViewContentInsetAdjustmentBehavior.Never` on `HandlerChanged`, `Loaded` and placement (as `SetupPageLayout` does).

In C#, construct `SafeAreaEdges` with the four-argument constructor (`new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None)`). When `TopLineSpace` owns a header's `Padding`, apply extra side clearance as `Margin`.

### Continuous viewports and clearance

- Place every `ScrollView` and `CollectionView` in a finite star (`*`) grid row, never nested inside another scrolling view.
- **Keyboard modes.** Use one coordinate system measured in the viewport host, where `obstruction` is `SetupActionPlacement.BottomGap` (which already includes its 12pt gap above a bottom pill or glass):
  - **Lists and forms.** At rest, `viewportBottomMargin = 0` so the viewport base equals `hostHeight`. Only while a docked keyboard overlaps the host (`keyboardTop < hostHeight`), set `viewportBottom = clamp(keyboardTop - 12, 0, hostHeight)` and `viewportBottomMargin = hostHeight - viewportBottom`, separately from fixed-action placement. Reserve only obstructions that remain *inside* the viewport (never double-counting the keyboard): `24pt` beyond `BottomGap` (36pt above a bottom pill, 24pt above a side pill base line or fixed Save top) and `12pt` above an open filter panel. Carry toast, bulk bar and overlay reservations through exit animations.
  - **Conversation (`IntelligencePage`).** The viewport stays at the host base even while the keyboard is open, and the transcript footer carries `16pt + max(composerOverlay, coveredFoot)` capped at `max(0, viewportHeight - 64pt)` so at least the last message stays reachable in short landscape.
- Stop scrolling cards and rows 12pt before a side pill's column; let them pass behind a bottom pill. With a side pill, an `OpenListCard` still runs to the screen base with square bottom corners and the `SectionListPlacement.Resolve` footer so list geometry stays consistent across poses.
- In a split view, apply side clearance only to the pane touching that edge. Run the detail surface to the screen base and carry clearance in its content footer.

### Fades

- **Top fade.** On solid backgrounds below fixed headers, filters or search, place a 20pt input-transparent top fade inside the viewport paired with a 20pt leading spacer. Match the transparent stop's RGB to its host (`Background*Clear` or `Surface*Clear`). Inset fades inside a `Card`, `ListCard` or `OpenListCard` by 1pt from the stroke. Give empty-state text a 32pt top margin so it starts clear of the fade. Over the Intelligence chat gradient backdrop, omit the opaque top fade and keep the 20pt leading space.
- **Foot fade.** Add a full-width, input-transparent foot fade whenever a bottom pill, bottom glass or fixed foot control covers the bottom of a scrolling viewport. Fade to `BackgroundLight` / `BackgroundDark` (drawn over `OpenListCard` and its stroke; each split-view pane handles its own column), starting 24pt above the highest obstruction inside the viewport and ending at the viewport base, reaching about 92% opacity 45% of the way down (matching `IntelligencePage`). Omit the foot fade when a side pill leaves the foot unobstructed and no fixed foot control is shown, or when a docked keyboard covers the foot with no overlaid foot control above it.
- Keep all fades out of the accessibility tree (`AutomationProperties.IsInAccessibleTree="False"`).

### Occlusion rules

| Content | May pass under bottom tab bar and glass | May pass under clock, Wi-Fi and camera |
| --- | --- | --- |
| Map, graph canvas, backdrop | Yes | Yes |
| Scrolling rows and cards | Yes (with footer clearance) | No |
| Horizontal chip strip | No | No |
| Header, search, fixed status, resting buttons | No | No |

End a horizontal chip strip's viewport 12pt before any pill, clock, Wi-Fi or camera region in its row. Because `CoveredEdges.Beside(page, strip)` measures against the full page width (falling back to whole-edge safe area before iOS 27.1) and adds no gap, subtract any space to the right or left of the strip's own pane before applying it:

- Trailing margin: `beside.Right > 0 ? max(0, beside.Right + 12 - spaceRightOfStripPane) : 0`
- Leading margin: `beside.Left > 0 ? max(0, beside.Left + 12 - spaceLeftOfStripPane) : 0`

This keeps resting chips 12pt clear on Duo outer portrait, prevents a right-hand pill in split view (`951x669`) from clipping the left list pane, and lets standard iPhone strips run edge-to-edge. Re-measure when search opens or closes because the strip moves vertically.

## 6. Navigation and Liquid Glass

### Action placement

| Action | Placement |
| --- | --- |
| Back on a pushed page | `TabBarButtons`, first item, `chevron.backward`, `IsBack: true` |
| Search | `TabBarButtons` search callback |
| Frequent page actions on a tall, wide window (`>= 600pt`, portrait) | `HeaderGlassGroup` beside the title, when both title and glass fit the safe lane |
| Frequent page actions on all other windows | `TabBarButtons` action group |
| Action for one item or field | Inline beside that item or field |
| Single page commit action | Fixed `PrimaryAction` slot (section 8) |
| Infrequent or secondary item action | Native context menu or overflow |

`PageActionPlacement.Resolve` decides between `HeaderGlassGroup` and `TabBarButtons` from the page size. Re-evaluate on every `SizeChanged` and rebuild glass only when placement or command availability (`CanExecuteChanged`) changes. If `HeaderGlassGroup` and a readable title cannot both fit the measured safe lane (for example at maximum Dynamic Type), fall back to `TabBarButtons` (or `ellipsis` overflow). Back and search remain in `TabBarButtons` in all poses.

Show two to four page actions (counting Back). Use SF Symbols via `GlassAction` (and `GlassActions.Pin` for pinning). Name every action with a verb and target (for example "Refresh devices"). Hide or disable data-dependent actions when their command cannot run.

On iOS versions before iOS 26 (`!TabBarButtons.IsAvailable`), place the same actions in `PageHeader.Actions` as `IconButton` controls and keep `PageHeader`'s inline Back bar.

### Glass ownership and lifecycle

Exactly one page owns `TabBarButtons` at any time:

- On appearing, call `TabBarButtons.Show` and subscribe to `Moved` (unsubscribing first).
- On disappearing, clear the active flag, close search, dispose `CoveredEdges.Watch`, unsubscribe from `Moved` and call `TabBarButtons.Hide(this)`. `Hide` releases glass only if the caller still owns it.
- A page with no actions still calls `TabBarButtons.Show` with an empty list so the previous page's actions clear.
- Implement `IActivePage` on every retained-tab, pushed page and glass-owning modal. `Activate` restores the header, watcher, Back label, search host and placement idempotently without refetching data or clearing queries, filters or selection.
- Guard `PlaceControls` and dispatched callbacks using both the active flag and Shell's modal-stack-first active-page lookup (as in `AppShell.xaml.cs`), so glass-owning modals such as `IntelligenceChatsPage` remain eligible owners while presented.
- A detail pane embedded in a split view owns neither Back nor glass; it contributes eligible actions to the host list page's glass and removes them if the window narrows.

### Back navigation

Set `PageHeader.BackInGlass = TabBarButtons.IsAvailable` on every pushed page that uses `PageHeader` (and hide `BackBar` on pushed pages that use `BackBar` directly) so the title takes the top line. Build the label as `"Back to " + BackBar.PreviousTitle(page)`, which walks the Shell navigation stack for the immediate predecessor before falling back to the tab title. Refresh the label in `Activate` because the stack may not be settled on first appearing.

Navigate to children with relative Shell routes and pop with `".."` so Back always returns to the real caller. Pinned root tabs have no Back button.

### Placement and tight-lane collapse

`TabBarButtons` places glass relative to the measured tab pill:

- **Side pill.** Actions at the top of the pill's column; search directly above the pill.
- **Bottom pill.** Search and actions beside the pill or above its right end.
- **Tight lane (for example Duo outer landscape).** Actions and search collapse into an `ellipsis` menu in the same lane. When two 44x44pt targets fit, Back stays separate beside `ellipsis`. When only one target fits, `ellipsis` shows with Back as its first menu item.
- **Zero-target fallback.** If even one 44x44pt glass target cannot fit, `TabBarButtons` reports that placement did not fit (with 8pt hysteresis so toggling header controls never loops) and feeds the active owner's guarded callbacks to one shared inline fallback view: pushed pages and glass modals restore inline Back and overflow, and pinned root pages restore inline Search and overflow. When space returns, restore glass and hide the inline fallback so focus targets never duplicate. Never move overflow into page content.

Preserve `TabBarButtons` cold-launch guardrails: place glass only inside a tab bar controller showing its tab bar, skip `MauiView` subtrees when locating the pill, and queue at most one placement per run-loop turn. Any `"Tab bar buttons: paused"` entry in Settings diagnostics indicates a layout loop that must be fixed.

## 7. Page patterns

| Pattern | Reference | Use for |
| --- | --- | --- |
| Conversation | `Pages/IntelligencePage.cs` | Growing transcript above an overlaid composer |
| Dashboard | `Pages/DashboardPage.xaml` | Adaptive multi-column summary cards |
| Searchable list with optional split detail | `Pages/LogsPage.xaml` (migrating to full-height in rollout) | Server lists with filters and detail panes |
| Section list | `Pages/DeviceSectionPage.xaml` | Card-per-record lists for one device |
| Catalogue or settings | `Pages/MorePage.xaml`, `Pages/SettingsPage.xaml` | Grouped navigation and preferences |
| Pushed detail | `Pages/DeviceDetailPage.cs` | Facts and actions for one item |
| Pushed document list | `Controls/PushedListLayout.cs` | Backups, contents and diffs |
| Form or picker | `Controls/SetupPageLayout.cs`, `Pages/GraphPickerPage.xaml` | Drafts and choosers |
| Canvas | `Pages/MapPage.xaml`, `Pages/NetworkMapPage.xaml` | Geographic maps and topology graphs |
| Full-screen modal | `Pages/IntelligenceChatsPage.cs` (glass), `Pages/IntelligenceReportPage.cs` (header-action) | Lists or previews presented over a page |

### Header

Use `PageHeader` for standard pages. Its `PageTitle` is heading Level1, wraps across as many lines as needed, and grows the header row from the `TopBar.Height` (44pt) minimum so long entity titles never clip. `Note` holds one quiet line (for example `"Changes save automatically."` or an offline timestamp).

Pages that use a bespoke top bar (`DashboardPage`, `CustomiseDashboardPage`, `IntelligenceChatsPage`, and `BackBar`-hosted detail pages such as `DeviceDetailPage`) must still apply `TopBar.Fit`, wrap their Level1 title cleanly, and accept `HeaderGlassGroup` beside the title when regular portrait placement applies.

### Conversation

- Centre transcript, notice and composer in one column capped at 860pt.
- Right-align question bubbles (`min(560pt, 85%)`); left-align answers with no repeated heading.
- Overlay the composer over the transcript via `SetupPageLayout.PlaceFoot` and `ChatComposerPlacement.Resolve`: level with a side pill's base, beside a bottom pill when a 360pt lane exists, above a bottom pill otherwise, and always above a docked keyboard.
- Set transcript footer clearance to `16pt + max(composerOverlay, coveredFoot)`, capped at `max(0, viewportHeight - 64pt)` so the last message stays reachable with the keyboard up. Keep the Jump to latest slot measured while hidden so the footer height stays stable.
- Let the composer field grow from 48 to 140pt. Style every composer control (Spotlight, remove, Switch to previous chat and answer detail) as an outlined chip at least 48pt high with a mandatory icon or chevron and a themed border meeting at least 3:1 contrast against the composer surface in both themes (never `SecondaryButton` or `StrokeDark` on `SurfaceAltDark`). Share one 48pt circle for Send and Stop.
- Show a notice only when an event actually changed this user's content, separated from the transcript by 12pt. With no turns, rest starter questions at the top with a 20pt lead rather than scrolling to the bottom.

### Dashboard

- `CardGrid` reads across, then down. Give cards in a row equal height with top-aligned content so comparable tiles align horizontally across neighbours.
- Keep a stat tile's `TileNumber` and label side by side only when both fit with at least 8pt trailing padding at four-digit counts and largest text size; otherwise stack the label beneath the number.
- Share identical column definitions between a ranked card's header and rows, including conditional columns.
- Scroll welcome and empty cards inside the same cleared column as populated cards.

### Lists

- Use a virtualised `CollectionView` inside an `OpenListCard` (16pt top corners, square bottom running to the screen base in every pose, with footer padding clearing the foot). Keep closed `ListCard` (16pt all corners) for self-contained inline cards inside detail views.
- Use `ListRow` with `RowLine` between neighbours and no hairline above the first row.
- Place fixed header, filters, summary and error rows outside the scroller; reserve `CollectionView.Header` for the 20pt fade spacer and list content.
- In a split (`IDetailHost` + `ListDetail`), show the detail pane placeholder only when a selectable list exists. Unconfigured or failed sources occupy the full width as a single pane.

### Section lists, catalogues and settings

- Section lists place each record in its own `Card`. Catalogues and settings overviews use hairline rows on the page background.
- Group headings are heading Level2 with at least 16pt top space.
- Settings overview rows use `SettingsOverviewRow`; navigation rows show a disclosure chevron, non-navigation rows omit it.
- Catalogue rows expose Open and Pin as two separate 44x44pt buttons.
- Settings forms group related fields inside bordered `Card` containers with switches aligned at the trailing edge of a two-column grid.

### Pushed detail

- Used by `DeviceDetailPage`, `AlertDetailPage`, `AlertRulePage`, `AlertTemplatePage`, `LogEntryPage`, `GraylogMessagePage` and `NeighbourLinkPage`.
- When pushed, hide the inline `BackBar` on iOS 26+ and provide caller-aware glass Back; when embedded in a split detail pane, show neither Back nor glass and promote eligible actions to the host list page.
- Run the scrolling body to the screen base with a foot fade when bottom chrome covers the foot and `24pt + obstruction` footer clearance.
- Put summary facts in a top `Card` with wrapping values and inline copy actions; keep full monospaced payloads character-wrapped inside `CodeDark` / `SurfaceAltLight` wells.

### Pushed document list

- Used by `UnimusBackupsPage`, `UnimusBackupPage` and `UnimusDiffPage` through `PushedListLayout`. (`LicencesPage` uses a `ScrollView` with `BindableLayout` and applies the same full-height, foot-fade and footer contract directly in page code.)
- Run the list or document viewport to the screen base with an `OpenListCard`, a 20pt top fade spacer, a foot fade when bottom chrome covers the foot, and `24pt + obstruction` footer clearance measured in the host's coordinates.
- Render configuration contents and diffs in `Mono` (11 to 12pt with line numbers) without truncating lines horizontally when wrapping is appropriate.

### Canvas

- Run the canvas full screen beneath overlays positioned inside `CoveredEdges`.
- Fit and centre content to the uncovered viewport so initial pins or nodes clear the header, pill, glass and selection sheet.
- Pass header, subtitle, side and sheet clearance to web maps in WebView coordinates through the bridge and reapply after load and resize. Keep zoom buttons 44 CSS px each and visible in outer portrait, inner landscape and inner portrait; hide both only when measured room cannot fit both (such as `678x466`) and retain pinch zoom.
- Theme attribution strips for both Light and Dark mode and keep links tappable.
- Provide a searchable list or sheet as an accessible alternative to tapping pins or nodes, with its own finite scroller and foot clearance.
- Present a selection card with item details, actions and a 44pt Close button while dimming surrounding content in place.

### Modal

Cover the full screen on the page background with a wrapping `PageTitle` (except canvas modals) and follow one of three contracts:

- **Glass modal** (`IntelligenceChatsPage`). One finite scrolling list running to the screen base; passes itself as `presentationPage` to `TabBarButtons.Show` so glass renders in the presenting pill's row, implements `IActivePage` for resume, and restores the presenting page's glass on close.
- **Header-action modal** (Spotlight picker, `IntelligenceReportPage`). Owns no glass, uses a quiet `SecondaryButton` (Done or Cancel) beside the title, manages its own safe area, and lets the presenting page reclaim glass on close. Spotlight uses one finite scrolling list; `IntelligenceReportPage` is an editable form modal with a fixed `Open` action.
- **Canvas modal** (enlarged image preview). Owns no glass, fits the image inside the uncovered area with a safe-area Close button, and lets the presenting page reclaim glass on close.

Modals that display private chat content (including Spotlight, `IntelligenceReportPage` and enlarged image preview) must implement `IPrivateChatSurface` for app-switcher privacy.

## 8. Forms and actions

Use `SetupLayout` (`Header`, `Body`, `PrimaryAction`) with `SetupPageLayout`, constructed once with every scrolling container on the page.

1. Wrap each field in `SetupSection` (label above control, 4pt gap, 8pt between sections). Group related fields in a `Card` under a `SectionHeader`.
2. On iOS, use `SetupLayout`'s opt-in overlay mode so the body scrolls behind the fixed `PrimaryAction` with a foot fade and `24pt` clearance above the wrapped action height (keeping the `Auto` row on Android).
3. Align the fixed action's base with a side pill's base, or place it above a bottom pill and its glass, lifting it 12pt above a docked keyboard. `SetupActionPlacement.BottomGap` computes the gap; only one element applies it.
4. Keep the focused field visible above both the keyboard and the overlaid `PrimaryAction` when the keyboard moves (`ManualKeyboardScroll` acquire/release in pairs, accounting for the visible area above Save rather than raw scroll bounds alone). Re-query live keyboard geometry on fold or rotation.
5. On a failed save, scroll the error into view, announce it (even on repeated failure) and preserve the draft.
6. Put units in numeric field labels. Use `NumbersAndPunctuation` or `Default` keyboard for signed values so the minus key is reachable.
7. For stepped forms, make the current step the title and show the earlier choice in a labelled section with a named Change action.

Assign each action the lowest role that fits. Allow at most one primary action per page.

| Role | Style and placement |
| --- | --- |
| Primary commit (Save, Add 3 cards) | Implicit `Button` in `PrimaryAction`, named for what it commits, visibly disabled while busy or until required sources load |
| Draft check or reset (Test connection, Reset to defaults) | `SecondaryButton` inline after its fields, with status or caption stating Save applies changes |
| Immediate action (Update now) | `SecondaryButton` in a labelled section stating it applies immediately; acts on saved settings and disables with an explanation when an unsaved draft blocks it |
| Destructive action (Delete, Sign out) | `DangerButton` with confirmation naming the target |
| Quiet link (See all) | `LinkButton`, minimum height 44pt |

Draft pages commit together on Save and discard on Back. Immediate-save pages omit `PrimaryAction` and show `"Changes save automatically."` once beneath the title.

Because explicit button styles (`SecondaryButton`, `DangerButton`, `LinkButton`, `Chip`, `Segment`) do not inherit the implicit `Button` visual states or minimum height, define `MinimumHeightRequest` (`44`, or `48` on `DangerButton`) and `Pressed`, `Focused` and `Disabled` states on each. Keep `Normal` (and `PointerOver`) empty, as the implicit `Button` does, with base colours in style setters: on `Chip` and `Segment`, `Pressed`, `Focused` and `Disabled` must not set a property owned by a selection `DataTrigger` (use `Opacity` or `Scale`, and omit `PointerOver` setters so hover never sticks on iOS).

Pre-auth sign-in pages (`SignInPage`, `LibreNmsSignInPage`) manage their own safe areas and install no glass Back. `WhatsNewPage` and `DiagnosticsSharingPage` keep glass Back through page-local placement (without `SetupPageLayout`, preserving their completion latch) while keeping the same gutters, targets and states.

## 9. Search, filters and pickers

### Search

- Place every search `Entry` inside `Controls/SearchField.xaml`. Never use native `SearchBar` or duplicate border markup.
- List search starts hidden and opens only from the glass search button or Command-F via `ButtonSearch` and `ISearchHost` (`KeyboardDismiss.OnScroll="False"`).
- Opening focuses the field; tapping search again refocuses it. Submitting, unfocusing, tapping content or a deliberate user drag closes the field while preserving the query and showing a wrapping `"Filtered by ..."` caption. Filtering and programmatic scrolls keep focus.
- Picker search stays visible above the list.

### Filters

- Build filter chips as native buttons based on `Chip`, minimum height 44pt with 22pt radius, stating name and count (or current value).
- Selected chips combine status tint, stroke and text colour (verified at >= 4.5:1 composited contrast across `Surface*`, `Background*` and `SurfaceAlt*`), and announce included or excluded state.
- Chip counts reflect the full source. When chips do not sum to the total (for example sensors without limits), the summary names the remainder.
- Sort and filter sheets rise from the foot with heading and Done fixed above a scrolling body.

### Pickers

- Style each picker row as a whole-row native button at least 48pt high with transparent Normal and Disabled states, `PrimaryTint` for Pressed and Focused, left-aligned wrapping text and row dividers.
- Show a tick for single choice and a checkbox for multiple choice, and include selected state in the accessible description.
- Use a virtualised `CollectionView` for server lists; `BindableLayout` is only for short fixed lists.
- Sort human-facing names with `NaturalOrder.Instance`; keep interface order for ports.
- Show one status line between search and results: loading, failure with retry guidance, empty source, no matches, or capped count with a refine prompt.
- Keep unavailable choices visible with their reason.
- Single immediate choices commit and dismiss immediately; multi-choice pickers either wait for Save or state they apply immediately with a quiet Done.

## 10. Data states

| State | Presentation |
| --- | --- |
| First load | One `BusyCard` over the viewport via `PullToRefresh.ShowBusyCard` (native spinner hidden). Chips show names only and summary counts and timestamps stay blank until the first successful response. |
| Refresh | Keep current rows in place. Manual pull or Refresh shows the busy card; background refresh is silent. |
| Loaded | Real counts (including `"0"` when the server returns none) and timestamp where supported. |
| Empty source | Plain sentence explaining what is absent and how to add or enable it. |
| No match | Distinct sentence with a way to clear the search or filter. |
| Error | `Error` text in plain words and one named 44pt Retry button (or retry guidance on setup forms). |
| Denied (403) | Distinct text stating what permission is missing. |
| Stale or offline | Last good rows with freshness age or timestamp in the header note or a pill. |
| Unconfigured | Full-width setup card and action with no list or detail placeholder. |
| Missing record | Cleared content with guidance to go back or choose again. |

- Show `"Checking…"` during the first check rather than a bare `"Checked"` or `"Updated ."`.
- Keep names and units identical before and after load.
- Preserve last good data on failed refresh when the page tracks freshness; clear rows on failure when it does not.
- Handle optional-source 404s at the source so missing metadata never hides primary data or masks a 403, 500 or timeout.
- Guard async loads with a generation counter so late responses for an earlier selection or connection are discarded. Keep Save disabled until required sources load.
- Use real navigation paths in copy (for example `"More → Graylog"`) and British spelling throughout (`colour`, `customise`, `licence`, `neighbours`).

## 11. Rows, status and text

- A row holds a status marker or tick, a title and supporting text, a trailing status, and at most one trailing action button. Supporting text keeps at least half the row's width at 402pt.
- On iOS, Ask Intelligence is a swipe, long-press (or native context menu on rows that do not use hold-to-select) and VoiceOver custom action via `AskIntelligence.Enabled`. Rows with hold-to-select (`DevicesPage`, `AlertsPage`) expose swipe and VoiceOver action without conflicting with hold-to-select. Ask takes no permanent column on iOS so shared rows and ranked headings keep identical alignment whether Intelligence is on or off.
- State every status in words (`Up`, `Down`, `OK`, `Warning`, `Critical`, `Inactive`, `Unavailable`) alongside colour. Unknown, missing and unassessed readings stay neutral and never render as `OK`.
- Show missing or non-finite readings as a dash with `"unavailable"`; omit the dash in speech.
- Wrap titles, summaries and labels. Truncate previews only when tapping opens the full text and the leading text still identifies the item.
- Word-wrap prose; character-wrap addresses, hostnames, OIDs, paths and code. Keep server text exact when displaying, copying, sharing or comparing.
- Stack a label above a long value. Keep a disclosure chevron on the title line and an inline action (such as Copy) on the value line when both fit.

## 12. Accessibility

- Every interactive target measures at least 44x44pt, including chips, links, segments, clear buttons, pin icons and zoom buttons.
- Expose each actionable row, tile or card as one native button with a joined accessible description (title, key facts, status, selection). Exclude duplicated child labels, icons, dots and checkboxes with `AutomationProperties.IsInAccessibleTree="False"`. Draw disclosure chevrons as non-accessible paths or exclude them natively (since `IsInAccessibleTree="False"` on a `Label` can still surface in UIKit snapshots), and confirm their absence in the accessibility snapshot.
- Expose secondary row actions (such as Pin or Copy) as independent 44x44pt buttons whose names include the target (for example `"Copy IP address"` and value, or `"Pin <device>"`).
- Give pickers, switches and repeated actions names that stay clear after values change.
- Set page titles to heading Level1 and group headings to Level2. Reading order follows visual order, then glass.
- Provide menu alternatives (`Move earlier`, `Move later`) for drag reordering and `AccessibilityActions` custom VoiceOver actions for swipe or long-press commands.
- Announce errors, results and selection changes that happen away from focus.
- Support iOS accessibility settings with observable checks: maximum Dynamic Type and Bold Text (rows grow, titles wrap, no fixed button heights), Reduce Transparency and Increase Contrast (native glass adopts the system appearance, text over it stays legible at 4.5:1, and no control or row becomes unreadable), Voice Control (visible labels match accessible names and activate reliably by name) and Full Keyboard Access (logical Tab order and visible focus rings on every control).

## 13. Motion

- Use named `view.Animate` calls and abort by name before starting a new transition.
- Slide and fade entrances over 220 to 250ms with `Easing.CubicOut`; exits use `Easing.CubicIn`. Set initial opacity and translation before showing the view.
- Check `ReducedMotion.IsOn` to apply final visibility and position immediately and replace spinners with status text.
- Start bottom sheets below their measured height plus bottom margin and 8pt, hide only after exit completes, and version selections so a late close never clears a newer item. Cap collapsed sheets at 200pt when window height is under 740pt (300pt otherwise), expanding to `max(240pt, 60% of content height)`.
- Dim surrounding content over 250ms without shifting layout.
- Do not add custom animations to glass relocation, folds or rotations; place controls directly.

## 14. Shared controls

Reuse these helpers instead of duplicating platform, layout or search code. Update shared controls ahead of dependent pages and recheck every caller.

| Need | File and key symbols |
| --- | --- |
| Edge and scroll clearance | `Controls/CoveredEdges.cs` (`Of`, `Beside`, `ForScrolling`, `Watch`) |
| Tab-bar Liquid Glass | `Platforms/iOS/TabBarButtons.cs` (`Show`, `Hide`, `Moved`, `PillIn`, `Occupied`, `CoveredBottom`, `GlassAction`, `GlassActions`, `GlassStyle`) |
| Title-row Liquid Glass | `Platforms/iOS/HeaderGlassGroup.cs`, `Shared/Services/PageActionPlacement.cs` |
| System reserved regions | `Platforms/iOS/ReservedRegions.cs` (`Occupied`) |
| Page header and top-line fit | `Controls/PageHeader.cs`, `Controls/TopBar.cs`, `Controls/TopLineSpace.cs`, `Controls/BackBar.cs` (`PreviousTitle`) |
| Forms and foot placement | `Controls/SetupLayout.cs`, `Controls/SetupPageLayout.cs` (`PlaceFoot`), `Controls/SetupSection.cs`, `Shared/Services/SetupActionPlacement.cs` |
| List and chat placement | `Controls/TabAlignedListLayout.cs`, `Controls/PushedListLayout.cs`, `Controls/ListDetail.cs`, `Controls/RowLines.cs`, `Controls/FootFade.cs`, `Shared/Services/SectionListPlacement.cs`, `Shared/Intelligence/ChatComposerPlacement.cs` |
| Search and keyboard | `Controls/SearchField.xaml`, `Controls/ButtonSearch.cs`, `Controls/KeyboardDismiss.cs`, `Controls/ManualKeyboardScroll.cs`, `Pages/ISearchHost.cs` |
| Breakpoints and columns | `Shared/Services/ScreenLayout.cs`, `Controls/CardGrid.cs`, `Shared/Dashboard/DashboardColumns.cs`, `Shared/Dashboard/CatalogueColumns.cs`, `Shared/DeviceSections/PortColumns.cs` |
| Loading, icons and bulk UI | `Controls/BusyCard.cs`, `Controls/PullToRefresh.cs`, `Controls/IconButton.cs`, `Controls/BulkBar.cs`, `Controls/TickCircle.cs` |
| Toasts and settings rows | `Views/DashboardToastView.xaml`, `Views/UndoToastView.xaml`, `Controls/SettingsOverviewRow.cs` |
| Intelligence, access and motion | `Controls/AskIntelligence.cs`, `Controls/AccessibilityActions.cs`, `Controls/ReducedMotion.cs`, `Shared/Services/NaturalOrder.cs` |
| Lifecycle interfaces | `Services/IActivePage.cs`, `Pages/IDetailHost.cs`, `Pages/IPrivateChatSurface.cs` |

Use `ButtonSearch` for new search and `SetupPageLayout` for new forms; do not add new callers to `SearchReveal`. Use `nfloat` where CoreGraphics expects it.

### Pending shared helpers

All shared helpers from the initial style guide rollout have landed.

## 15. Acceptance

Record each line as passed, failed or unavailable with build, OS, device, pose, theme and text size. CI proves compilation and unit tests; an installed build proves layout.

Layout (across all poses in section 4):

- [ ] At rest, viewport base equals host base; with a docked keyboard, lists and forms stop 12pt above the keyboard top while Conversation stays at host base and reserves keyboard plus composer in its footer; the last item scrolls clear of pill, glass, fixed controls, toasts and panels.
- [ ] Resting chips, headers and controls stay at least 12pt clear of clock, Wi-Fi, camera, pill and glass (including split view at `951x669`).
- [ ] Fixed actions align with a side pill's base, or clear a bottom pill and its glass.
- [ ] Fold, unfold, rotation, half-open, resume and tab return update the open page and keep its state.
- [ ] Keyboard lift holds on open, across a fold with keyboard open, keeps the focused field above both keyboard and overlaid Save, and restores on dismiss.

Navigation:

- [ ] Exactly one glass owner after entry, child return, modal dismissal, tab return and resume (or zero glass owners while a header-action or canvas modal is open, restoring presenter ownership on close).
- [ ] Every pushed page has a reachable Back in every pose, naming the immediate parent page, and pinned roots keep Search and page actions reachable (including zero-target inline fallback with no duplicate focus targets).

Content and states:

- [ ] Loading, empty, no match, error, denied, stale and unconfigured states each show distinct copy; chips show names only and counts and timestamps stay blank until the first successful response.
- [ ] Zero clipping or white web-view flash in Light and Dark themes at standard and maximum Dynamic Type, with long synthetic names and four-digit counts.
- [ ] At most one primary action per page; shared list rows and ranked Dashboard columns keep identical layout with Intelligence on or off.

Accessibility, motion and delivery:

- [ ] VoiceOver names, states, order and custom actions work; accessibility snapshot has zero unnamed buttons and zero decorative glyphs; all targets are at least 44x44pt (48pt in composer); every text-surface and text-tint pair meets 4.5:1; control boundaries identifying composer chips meet 3:1.
- [ ] Reduce Motion removes added movement; Reduce Transparency and Increase Contrast keep native glass legible at 4.5:1 with every control readable; Bold Text and maximum Dynamic Type grow rows without clipping; Voice Control activates controls by visible name; Full Keyboard Access shows visible focus rings in logical order.
- [ ] `dotnet test tests/DashyNMS.Mobile.Shared.Tests`, `node --test tests/map/camera.test.mjs` and the iOS build pass in CI.
- [ ] Settings diagnostics contain zero `"Tab bar buttons: paused"` entries.
- [ ] All fixtures, screenshots and text use synthetic network data and British spelling.

Done when every applicable line is recorded and the user accepts the installed build.
