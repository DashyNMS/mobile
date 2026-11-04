# Health scope assessment (#443)

Baseline: main `268cfb3`. Live #443 and #442 read on 3 November 2026.
References: DESIGN.md, DESIGN-REFERENCES.md, Devices/Alerts placement,
SearchReveal/SearchField, SectionRow and HealthViewModel. Related merged
work: #257 (unknown), #258 (cached readings), #295 (offline), #437
(threshold form). No open Health implementation PR was found.

## Assessment before implementation

| Concern / trigger | Status | Evidence and action |
| --- | --- | --- |
| Four categories; severity filters; counts; search | Compliant | HealthViewModel counts the entire category before filtering; ChipMemory remembers severity only; query and category remain page-local. |
| Unknown and non-finite readings | Fix required | Core-derived status/counts already exclude unknown from OK and keep it visible with all chips off; HealthPage shows only a dash and colour. Add explicit text and row description. |
| Long names, units, narrow/large text | Fix required | HealthPage fixes four segments to equal widths, uses a non-wrapping chip strip, truncates device names and reserves an Auto reading column. Allow category/chip wrapping and stack full row text. |
| Whole-row activation and announcements | Fix required | Tap covers the grid, but there is no combined accessible description. Preserve navigation; combine name, device, value, limits and severity. Describe selected categories and filters. |
| Clear target | Fix required | HealthPage explicitly sets 30pt. Use minimum 44pt. |
| Search, Command-F, keyboard | Fix required | Shared SearchField and Command-F fallback already work. Add ISearchHost for consistency and glass search while retaining SearchReveal: always visible on Android, scroll-revealed on iOS. |
| Finite viewport | Compliant | CollectionView is in a star Grid row, outside any ScrollView. |
| Final row and glass | Fix required | Native safe area alone does not account for extra glass actions. Reuse CoveredEdges and Devices pill/footer placement. |
| First load, refresh, empty source, no matches, failure, cached/stale/offline | Compliant | Existing busy/error/empty gates and SensorRefreshStatus retain last readings and timestamp; keep them. Visual operation unverified. |
| Threshold return | Compliant | Only Settings opens Thresholds; Save validates and persists, Back discards; settings Changed rebuilds loaded Health rows immediately without refetch. No new entry route needed. |
| Entry/return routes | Compliant | Default pinned Health tab, repinned tab and unpinned More page use AppShell page factory; the unpinned route now supplies glass Back and hides header Back on iOS 26+; sensor pushes DeviceSection.Sensors with ID/name, Back returns to Health. Switching tabs and returning calls RefreshIfStaleAsync (one minute). |
| Persistence/security | Compliant | Read-only sensor/device requests use existing session and offline stores; no credential, permissions, cache format or settings schema changes. No Health write except existing severity preferences. |
| Runtime fit, accessibility, insets, motion, navigation | Unverified | Requires devices; source inspection establishes no device outcome. |

All visual changes follow existing guide rules; no new visual policy proposed.

## Evidence

Implementation self-inspection: complete intended diff inspected; git diff --check passed.
CI: initial unit-test job caught an incorrectly typed event raise in the new
regression test; corrected to EventHandler<AppSettings>. CI at 2e913e2 passed shared tests,
Android, iOS simulator and CodeQL actions/csharp; Swift was still pending at review.
Replacement CI after review fixes is pending;
no builds or tests run on this code machine. Regression tests
are added for severity text, unknown visibility, counts and threshold changes
without refetch (the page return itself remains a device check).
Claude Opus 5.5 medium reviewed 2e913e2. Addressed missing glass Back,
explicit child safe-area ownership, equal-fill wrapping segments, pill radii,
missing-value speech, search alignment and assessment inaccuracies. Restored
SearchReveal to preserve existing Android visibility and iOS scroll reveal;
Command-F previously worked through its fallback. Claude re-reviewed the full diff at 4ffd249 with no blocking findings.
Removed the remaining legacy iOS safe-area flag as requested and added the
optional accessibility property summaries. Final CI is pending.
Segments retain equal growth without a zero basis so they can wrap. Severity
chips retain pill geometry with a 22pt radius at the accessible 44pt minimum.
Every device outcome remains unverified.

## Remaining device steps

1. On the PR build, use synthetic sensors with long names/device names,
   long units/limits, critical/warning/OK and absent/non-finite readings.
   Check light/dark and largest text on Duo outer, inner portrait and inner
   landscape, folding/rotation without reopening, plus ordinary iPhone and Android.
2. Open pinned Health, unpin and open from More, repin, switch tabs and resume;
   verify glass Back returns to More on iOS 26+, one glass owner, header actions on older iOS/Android, and final row
   clears bottom/side/top pills without doubled side or bottom insets. Inspect placement diagnostics for overlap/loops.
3. Toggle all severities off: unknown remains visible and says unavailable;
   check category totals, search/no matches, Clear and relaunch filter memory.
4. Open search from glass/header and Command-F after scrolling; type, refocus,
   Search and scroll to dismiss the keyboard; query must remain. Android search
   stays visible; iOS search reveals on upward scroll and remains while in use.
   Repeat rapidly with Reduce Motion enabled and disabled.
5. VoiceOver/TalkBack: hear complete row name/device/reading/limits/severity
   once, activate the whole row once, confirm device Sensors and Back; hear
   selected category/filter state, and operate Clear at large text.
6. Open Settings → Health thresholds; change a negative/decimal threshold,
   Save and return to Health: severity/counts change. Repeat with Back to
   discard. Verify no forced fetch within the fresh interval.
7. Exercise first load, pull refresh, slow refresh with kept readings, empty
   source, failure with/without cache and offline timestamp; no false empty/OK.
   Obtain explicit user acceptance before closing #443 or ticking #442.


## Corrective follow-up after #447 (before #448)

This supersedes the earlier search policy for Health; historical acceptance
and evidence above remain unchanged. Search starts hidden on both platforms.
Header/glass search and Command-F open or refocus it. Search, unfocus and user
drag close it while retaining the query. Content taps use page unfocus handling.
The clipped height/translation transition honours Reduce Motion.

Review of `41a0a20` confirmed that filtering could raise Scrolled and dismiss
search mid-word, as previously fixed in DeviceSectionPage #402. ButtonSearch
now checks iOS Dragging/Tracking or Android RecyclerView dragging state, without
native subscriptions or retained native views. Both lists explicitly disable
the inherited KeyboardDismiss.OnScroll behaviour. The helper is constructed
once per page; its managed handlers share the page lifetime. No shared
SearchReveal or KeyboardDismiss behaviour changes.

No local builds or tests were run. At reviewed head `41a0a20`, the supplied CI
record had shared/Node tests, actions/csharp, both iOS jobs and one Android job
passing; the other Android job and Swift analysis were pending. Those results
do not validate this follow-up. New-head CI and the parent's fresh Claude Opus
5.5 Medium full-diff review remain pending. No devices have been verified.

Device gaps include multiword typing after scrolling on iOS and Android,
filter-driven row replacement, real drag dismissal, query retention,
Command-F refocus, Android outside-tap unfocus, rapid transitions and Reduce
Motion. Check category/severity retention and sensor navigation.
Issue #443 and its #442 entry await device acceptance.

## Retained-tab correction, 4 November 2026

Current assessment baseline: origin/main `35bf21712b5a2c3a83d5344bcb65da0321090e23`.
Live #443 body and its sole comment, and #442 body and all comments (none),
were refreshed on 4 November. This section supersedes earlier claims that tab
return necessarily calls RefreshIfStaleAsync. Historical review/CI above applies
only to those historical heads. Groups #444 is closed with explicit user
acceptance in comment 6618547247; its old pending wording is historical.
No acceptance of Health follows from that record.

### Report, cause and correction

The user's #443 comment 6618546870 reports More → Health → another tab →
More loses Back and menu-base alignment; reopening Health restores both.
`Pages/HealthPage.xaml.cs` implemented ISearchHost but not IActivePage, leaving
Header.Apply, CoveredEdges.Watch, glass Back/Refresh/Search, Moved subscription
and PlaceControls dependent on OnAppearing. AppShell.RestoreActivePage already
resolves CurrentPage inside a dispatched callback on navigation and resume.
GroupsLocationsPage.Activate is the accepted opt-in reference. No evidence
requires changing that shared mechanism or any other consumer.

Health now implements IActivePage. OnAppearing delegates control restoration
to Activate and separately keeps RefreshIfStaleAsync. Activate restores header,
watcher, caller-aware glass Back, refresh/search and placement, with one watcher
and remove-before-add Moved registration. Activation itself never fetches,
clears query, changes category/severity or reloads the page. Actual Appearing
retains the one-minute stale/definitions check; explicit pull/glass refresh
retains its existing command. Health has no periodic showing-state timer.

OnDisappearing clears the active flag before closing search, disposing/nulling
the watcher, removing Moved and owner-safe Hide. CoveredEdges' native safe-area
callback and OnGlassMoved can already have dispatched PlaceControls before
unsubscribe. PlaceControls now checks both the active flag and Shell's current
page at delivery, so departed retained pages cannot apply queued geometry.
Constructor handler/load inset hooks remain once per page lifetime; there are
no new Shell, Window, native observer or global helper subscriptions.

### Complete applicable scope reassessment

C means source compliant/preserved; F identifies a corrected source finding;
U means actual operation remains unverified. All runtime results are U.

| Concern and location | Source result and preserved semantics |
| --- | --- |
| Retained pinned/More return and resume, HealthPage.Activate | F corrected as above. Pinned root gets no Back; pushed Back label uses PreviousTitle and relative `..` to the actual caller. Sensor rows still push Routes.DeviceSection, Sensors, exact device ID and display name. Return from that child restores Health. No embedded Health detail mode exists. |
| Sensor rows, HealthPage.xaml / SectionRow | C full wrapping title, device, value/units and limits; explicit Critical/Warning/OK/Inactive/Unavailable text and combined HealthDescription. Decorative dot is excluded; child text is excluded from duplicate speech. Native whole-row activation and speech remain U. |
| Categories/severity/counts, HealthViewModel | C four registry categories, wrapping equal-growth category buttons and wrapping minimum-44pt severity pills; selected/included state descriptions. Counts cover the whole category before severity/query filtering. Unknown/non-finite readings remain unavailable, excluded from OK and retained with all chips off. Existing problems-first/device/name ordering and Clear semantics retained. |
| Search, ButtonSearch / HealthPage | C hidden initially on both platforms; header/glass/Command-F opens or refocuses. Completion, unfocus/content touch and real native drag close while retaining query. Programmatic/filter-driven Scrolled does not close; KeyboardDismiss.OnScroll stays false. Clipped animation/Reduce Motion unchanged. Query/category remain instance-local; only severity preferences persist. |
| Thresholds and source, HealthViewModel.Rebuild/RefreshIfStaleAsync | C Settings remains the only threshold entry; Save validates/persists and Back discards under #411/#405. Settings.Changed rebuilds already loaded readings without fetch. ReadSensor owns existing thresholds, definitions, limits, units and unavailable classification; no new source interpretation. Fresh Appearing applies changed definitions without refetch where applicable. |
| Loading/stale/error/empty, HealthViewModel / PullToRefresh | C first-load/refresh busy overlay, updating text over kept readings, SensorRefreshStatus failure information, saved/offline timestamp and retained last data. Empty is suppressed during busy/error; no-source and no-match messages differ. Pull/glass refresh retry. Partial/denied data retains existing Core/session handling; no false OK added. |
| Measured usable base, HealthPage.PlaceControls | C existing measured placement preserved exactly: vertical side pill uses max(0, page height minus pill bottom), without native-bottom clamp; lower horizontal pill uses that base and footer difference plus existing 8pt gap; upper horizontal pill uses fallback 12 + clear.Bottom. Missing geometry uses fallback, no cached pill. Side gutters remain 16 + measured clear.Left/Right. Physical base delta and final-row reach are U. |
| Inset/viewport/fade, HealthPage.xaml and StopInsets | C native iOS page owns top only; supporting grids/RefreshView opt out, native collection adjustment Never on handler/load; Android native container handling remains. List stays in star row outside ScrollView. F missing guide-required fade corrected with an input-transparent 20pt overlay inside the same ListCard viewport and a 20pt collection header. Surface/SurfaceClear endpoints share card RGB in each theme. Empty text already has 32pt margin. No guessed base offset or shared layout change. |
| Actions, text and colours | C search/header fallback and glass refresh unchanged; Clear minimum 44pt, row wrapping and semantic severity remain. No Compare/Share/Save capability applies to Health. Contrast, clipping, final action reach and large-text fit are U. |
| Persistence/security | C no server writes; only existing local severity preferences are written; no credential, permission, session/cache schema, network source or threshold persistence changes. Synthetic data only. |

### Evidence and remaining acceptance

Implementation self-inspection covers the complete diff against origin/main and
DESIGN.md, including measured base versus final-row clearance. git diff --check passed before commit. No local build or test was run. New-head shared tests,
Android/iOS builds and security CI are pending. Independent Claude Opus 5.5
Medium full-diff review is pending and belongs to the parent. This is source
assessment, not that independent review. No device check is verified and no
build/OS/device result is claimed. #443 remains open and #442 unchecked.

Record final SHA, build, OS and each result separately for these exact steps:

1. On Duo outer, inner portrait and inner landscape, in both themes and maximum
   text, cold-launch pinned Health, switch tabs/return and background/resume.
   Unpin Health, open More → Health → Devices → More without reopening Health;
   repeat after folding/rotation and resume. Confirm one current glass owner,
   caller Back/Search/Refresh restored, retained category/query/chips and no
   activation-driven reload. Repeat reopen recovery for comparison.
2. On each configuration measure native page/host, list viewport and content
   bounds in the same coordinate space as pill bottom B and page height H.
   For side pill verify viewport base gap equals max(0,H−B), separately verify
   the complete final row can scroll clear. Repeat bottom and top pill, missing
   geometry, fold/rotate without reopening and keyboard dismissal. Verify only
   one owner per inset, first text below 20pt fade, empty text clear, fade accepts
   taps/drags, header actions and final footer clear glass. Inspect placement
   diagnostics for overlap/loops. Capture measured deltas rather than judging
   only that rows do not overlap.
3. Use synthetic long sensor/device names, long units/limits, negative/decimal
   values, critical/warning/OK and absent/non-finite readings. Check all four
   categories, totals before filters/search, all chips off retaining unknown,
   Clear, no matches and relaunch severity memory. Confirm severity readable
   without dot colour and complete text at maximum size.
4. Open/refocus search with header/glass and Command-F after scrolling. Type
   several words while rows filter; programmatic scroll must retain focus.
   Actual drag, Search and outside touch/unfocus close keyboard retaining query.
   Repeat rapid open/close with Reduce Motion on/off, rotate/fold with keyboard
   open, dismiss and remeasure base. Check Android header fallback explicitly.
5. Tap a sensor from pinned and More-pushed Health; confirm exact device Sensors
   and Back to its actual Health caller. Visit Settings → Health thresholds,
   Save a negative/decimal change and return: counts/severity rebuild without
   fresh-interval fetch; repeat Back to discard. Test slow refresh with retained
   readings, empty source, no matches, first failure, refresh failure, denied/
   partial source and offline saved timestamp; no false empty or OK claims.
6. VoiceOver and TalkBack: each complete name/device/value/limits/severity once,
   whole-row activation once, category/filter state and Clear/action names and
   targets. Repeat the route/geometry/search checks on an ordinary iPhone,
   including iOS 26+ glass and older-iOS header fallback when available, plus
   Android. Shared controls are unchanged; regress accepted Groups #444 and
   rules/template/alert #447–#449 tab-return/resume ownership and setup/information
   caller Back on the integrated build. Obtain explicit Health acceptance before
   closure or epic changes.
