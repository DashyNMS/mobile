# Alert rules list assessment

Queued correction after #489: see [button-only search and nested alert return
findings and regression matrix](alert-navigation-correction.md). ButtonSearch
supersedes the historical SearchReveal policy below for this page. CI, Claude
review and every hardware outcome for the correction remain pending.

Refs #446, epic #442. Baseline main 238f377a691a3ab1bf0e2ba8e096ac3825e274ea after #468.
Read live #442/#446/#447/#448, CLAUDE.md, DEVELOPING.md, complete DESIGN.md
and DESIGN-REFERENCES.md. Reviewed merged #466/#467/#468 descriptions and
current implementations; no open PRs were listed at assessment time.

## Modes, routes and contracts

AlertRulesPage is a pin-able AppPages Monitor destination. Pinned it is a tab
root; unpinned its resolved More route is //main/more/alertrules. More Back
returns to More; the pinned root has no Back. ShellNavigationService offers
IDetailHost first. In a narrow viewport, OpenCommand navigates to
Routes.AlertRule with the unchanged integer Routes.RuleIdParameter. Relative
pushed detail Back returns to this filtered list. Wide taps replace the embedded
AlertRuleView and hide its Back. ListDetail retains that view when narrowed
and restores it when widened. The row highlight is visible only in split mode.
Filtering never clears the embedded detail; a refresh reapplies selection by ID.
A removed or filtered-out rule can remain in detail without a highlighted row.

Rule detail also opens from alerts and template-related rules. Templates and
alerts opened from the embedded view push through normal navigation, since
the host accepts only Routes.AlertRule. Back returns to the same host. #447
owns the rule body and pushed AlertRulePage, including descendant navigation
and notification actions; #448 owns template detail. This PR changes only
named containers and an explicit PlaceContent boundary in AlertRuleView.

List refresh reads rules and open alerts, with templates best effort on API
failure. It preserves last good rows on failure through ViewModelBase.RunAsync.
Counts describe the full source; search and chips affect displayed rows only.
Alerting is opt-in, severity and Disabled initially included, other severities
always visible. Clear resets all filters. Search covers name, condition, summary,
template and notes, case-insensitively after debounce. Ordering remains alerting,
enabled, severity, name. Search/filters/selection are in-memory, with no new
preference, setting, draft, server write or credential handling.
RuleText continues to choose override SQL when applicable; no source expression
is changed for presentation. Template loading failure does not fail the rule list.
Existing detail Notifications Change/Undo can persist notification preferences
immediately through NotifyRules; “read-only” means no rule/template editing,
not that the existing detail has no local settings action. These actions and
their security/permission contracts are unchanged.

## Scoped assessment

| Concern/control and trigger | Baseline | Outcome |
| --- | --- | --- |
| Long list name/condition/details | Condition and metadata truncated | Fix: full wrapping text in finite virtualised CollectionView |
| Disabled/severity/alerting state | Severity colour and reduced opacity, quiet rows lacked textual severity | Fix: explicit severity, Enabled/Disabled and alerting count; no dimmed contrast |
| Whole-row accessibility | Child labels and gesture without joined name | Fix: nonblank joined description, hidden child announcements, whole-row tap; native activation unverified |
| Selection in wide host | No visible selected state | Fix: ID-based highlight and Selected text/description, survives refresh/filter, hidden when narrow |
| Search | Shared field/reveal already compliant; Command-F fallback existed | Preserve Android always-visible/iOS sliding policy; explicit ISearchHost, header/glass entry and keyboard dismissal |
| Filters/counts | Source-wide counts and original predicates compliant | Preserve behaviour, enlarge Clear/chip tap targets with pill radii; add live count/inclusion descriptions after Claude found colour-only chip state; horizontal chip scroll retained |
| Empty/loading/error | Distinct no source/no matches; error suppresses empty, shared busy overlay compliant | Preserve; add fade space and footer, pull-to-refresh remains retry route |
| Insets/final rows | Legacy UseSafeArea plus Container, no glass clearance | Fix: top-only iOS native owner, None child layouts, native CollectionView adjustment Never on handler/load, measured margin/footer |
| Fade at rest | Missing | Fix: input-transparent 20pt surface-coloured fade inside the list Border, inset below its stroke, with header spacer; empty content retains 32pt padding |
| Pinned versus pushed glass | No host glass lifecycle | Fix: search and pushed-only glass Back; Header.BackInGlass on supported iOS; native header elsewhere |
| Embedded versus pushed detail | No explicit host inset contract | Fix: host supplies column-reaching occlusion, view pads scrolling content; pushed body/wrapper retained for #447 |
| Rotation/fold/resume | Split adapts width already | Retain split; size/covered-edge/glass callbacks recompute clearance, appearance refreshes and reclaims glass; device outcome unverified |

No global control changes. Added transparent Surface colour resources consumed
only by this list fade; existing resource values and consumers are unchanged. AlertRuleItem is also consumed by template-related
rules, whose existing display properties and navigation stay unchanged. Its new
selection/accessibility properties are consumed only by AlertRulesPage.
PlaceContent is consumed only by this embedded host. No accepted setup,
Health, Groups or Map page changes.

## Validation and remaining acceptance

Implementation self-review: full diff inspected against DESIGN.md, including
navigation, source text, finite viewport, row state, header action slot and
single inset ownership. git diff --check passed before commit.
Regression tests added for selection across filter/refresh/clear and complete
nonblank accessible content with selection notification. Existing route,
filter, ordering and detail tests remain. No local builds or tests executed,
as instructed for this code machine.

Claude Opus 5.5 Medium reviewed the full diff at d1a17cd. Addressed its two
medium and two low findings: restored bottom row padding; added filter state
announcements and pill radii with toggle/clear notification tests; removed
repeated Disabled text from row metadata; moved the surface-coloured fade
inside the card. No findings are deferred. At that head unit tests, Android,
iOS simulator and CodeQL actions/C# passed; Swift remained pending at review.
Replacement CI is pending. No local tests/builds ran. Review and CI remain
separate from device acceptance.

Every device outcome is unverified. Execute against the eventual CI build:

1. On Duo outer, inner portrait and inner landscape, and ordinary iPhone,
   open both pinned root and More pushed list. Check Back only when pushed,
   search entry, then narrow rule push and wide embedded selection using known
   synthetic rule IDs. Open template and alert descendants and return.
2. In light/dark themes and largest text, inspect long names, multiline SQL,
   targets/templates, severity/disabled/alerting text, chips and Clear. Scroll
   every chip and final row clear of side/bottom glass. Verify initial rows and
   empty states below the fade and taps/drags through it.
3. Search notes/conditions/templates; combine severity, Disabled and Alerting.
   Hide a selected row, clear filters, refresh, and confirm the same ID/detail.
   Narrow then widen with a different narrow pushed rule and verify the retained
   embedded choice. Test Command-F after scrolling; Search/scroll/content touch
   dismiss keyboard without losing query. Confirm Android search stays visible.
4. VoiceOver/TalkBack: focus and activate each whole row once, hear complete
   nonblank name, state and selected status without duplicate children. Test
   long content, Clear and every chip; verify focus after return and refresh.
5. Simulate slow initial load, pull refresh, empty source, no matches, API
   failure and failure with stale rows. Confirm one busy overlay, no native
   spinner/list jump, error readability and successful pull retry.
6. Fold/unfold, rotate and resume while selected, filtered, searching and on
   descendants. Check final-row/footer reachability, embedded content clearance,
   one glass owner and no diagnostics placement loop. Repeat with Reduce Motion.
   Record build/head, OS/device, each outcome and diagnostics separately.

#446/#442 remain open pending review, CI and explicit user device acceptance.

## Corrective reassessment on main 56ccb45 (4 November 2026)

This section supersedes the historical completion/status claims above. Live
#446/#442 and cached issue evidence were read; #447/#448/#449 are explicitly
accepted and remain so. Main contains Health #519 and merged #475–477 work;
none is replayed. Existing open PR #515 is documentation-owned and untouched.
The unrelated dirty #475 checkout and all other branches are preserved.

Using repo-workflow, reviewed the original scope, reconciled assessment,
CLAUDE.md, DEVELOPING.md, DESIGN.md, DESIGN-REFERENCES.md and accepted #447
assessment. Only AlertRulesPage XAML/code and this assessment change. No
shared helper, view model, resource or accepted detail implementation changes.
No unresolved visual choice is introduced: clipping/content ownership and
activation follow the explicit design contract.

| Concern, trigger/mode/location | Status, expected result and correction/evidence |
| --- | --- |
| Retained pinned/More stack return or resume; AlertRulesPage lifecycle | Corrected: implements IActivePage; Activate reapplies Header, watcher, glass and placement without Refresh or resetting query, filters or detail ID. Actual Appearing still executes the original RefreshCommand. |
| Queued safe-area/Moved after child departure | Corrected: inactive/current-page guards reject placement; watcher disposal and idempotent Moved remove/add mirror merged Health. Hide remains owner-aware. |
| Inner camera layering; Body/filter ScrollView | Corrected: removed blanket Body padding; native safe-area-free filter viewport spans the list column, with safe padding inside ScrollView as Devices/Alerts. Header, search, count, error, busy and list independently own gutters. No guessed camera offsets. |
| Fold/rotate split layout | Source compliant host boundary: Body resize/SplitChanged recompute placement; the trailing star detail column reaches the page right edge and receives clear.Right directly. Detail background/viewport stays full-size; accepted PlaceContent owns scrolling padding. |
| Bottom/side/top pill; list base | Source compliant: measured max(0, native height minus pill bottom) retained without safe-bottom clamping; bottom-pill footer clears glass, top pill retains fallback policy. Restored activation refreshes geometry. Actual base delta unverified. |
| Rows/source text/state | Compliant source: full wrapping condition, name, target/template and explicit severity, enabled/disabled and alerting count; no new truncation. Existing list Condition trims outer whitespace only; original server expression remains on Rule and accepted detail uses ConditionSource unmodified. |
| Search/counts/filter/selection | Compliant source: debounce and original predicates/order unchanged; counts remain source-wide; ID highlight only in split, filtering retains detail, refresh reapplies ID. No persistence added. |
| Narrow push/wide embedded and descendants | Compliant source: Routes.AlertRule/RuleIdParameter unchanged; narrow relative push/pop, wide host only accepts rule route; template/alert descendants retain actual callers and accepted #447/#448/#449 presentation. |
| Search action/Command-F/focus | Compliant source: ButtonSearch opens/refocuses; native dragging/tracking closes, programmatic/filter Scrolled does not; completion/unfocus/departure closes without clearing query. KeyboardDismiss.OnScroll remains false. Native keyboard operation unverified. |
| Each filter/Clear/empty Clear | Compliant source: labelled commands, state/count descriptions and 44pt chip/Clear minima retained; Clear resets filters/query intentionally, never detail. No save or rule-edit action. |
| Header/glass Back | Corrected activation, same actions: pushed-only relative Back with actual previous title; search glass on supported iOS, header fallback elsewhere. One page heading retained. Native hit targets/ownership unverified. |
| Loading/empty/error/pull retry | Compliant source: shared busy overlay, finite virtualised list, no-source versus no-match, empty suppressed by busy/error and stale rows preserved on failure; pull refresh remains retry. Templates best effort unchanged. |
| Fade/first text/divider | Source compliant reservation: input-transparent themed 20pt gradient inside stroke, matching 20pt header; empty has 32pt vertical padding. Actual first-row divider beneath header requires explicit check; sibling matching does not establish visual compliance. |
| Read-only/persistence/security | Compliant: no server writes, credentials, permissions, source model or settings changes. Accepted detail notification Change/Undo still immediately persists local NotifyRules; Back does not cancel those actions. |
| Accessibility/themes/motion | Source descriptions and child exclusions retained; native activation, contrast, large-text fit and Reduce Motion operation unverified. |
| Draft Save/comparison/export | N/A: list has no draft commit, comparison or export action; no new capability added. |

Camera policy inspection: CoveredEdges.ForScrolling owns safe side/side-pill
clearance, matching Devices/Alerts' conservative internal chip padding.
CoveredEdges.Beside converts each row's native bounds into page coordinates
and intersects actual ReservedRegions plus glass only on iOS 27.1+; older
systems, unlaid rows or missing regions use safe-side fallback. ReservedRegions
expands active frames by system margins. This PR does not change those helpers
or infer that an iOS 26 run has iOS 27.1 occlusion evidence. Full clip permits
scrolled chips under occlusion while resting endpoints retain safe padding.

Lifecycle verified by source tracing, not hardware: AppShell.OnNavigated and
Window.Resumed dispatch RestoreActivePage, resolving CurrentPage at delivery.
Only IActivePage receives activation. Repeated activation reuses one watcher
and one Moved subscription; actual OnAppearing alone refreshes. Departing closes
search, removes subscriptions and hides only this owner's glass. Rapid return,
real callback ordering and resumed native bounds remain device-unverified.

### Final-head validation and device matrix

Implementation self-inspection: full diff against main and DESIGN.md plus
whitespace inspection before commit. No local builds/tests or test scripts run.
Independent Claude Opus 5.5 Medium full-diff code review: **pending parent**.
Shared tests, Android, iOS simulator, security CI: **pending final head**.
Every device result: **unverified**. Resolve “final head” using this PR's head
SHA (not historical hashes above); record tested SHA/build/OS for every cell.

| Final PR head/build | Duo outer | Inner portrait | Inner landscape | Ordinary iPhone |
| --- | --- | --- | --- | --- |
| Light/dark, largest text, VoiceOver/TalkBack | Unverified | Unverified | Unverified | Unverified |
| Pinned and More; tab return/resume; rapid child return | Unverified | Unverified | Unverified | Unverified |
| Camera/chip frames, viewport/pill deltas; fold/rotate | Unverified | Unverified | Unverified | Unverified |
| Keyboard real drag/completion/unfocus versus filter/programmatic scroll | Unverified | Unverified | Unverified | Unverified |
| Narrow push/wide detail, selection, states and final row | Unverified | Unverified | Unverified | Unverified |

For each cell capture native host, Body, detail pane, viewport and content
bounds in the same page coordinates. Record actual camera ReservedRegions,
chip scroller clip, chip frames and scroll offsets at rest and while passing
beneath camera. Record pill bottom B, host height H, viewport bottom V and
V-B (target zero for measured side/bottom base), plus footer/final-row reach.
Check older iOS/Android fallback separately. No numeric geometry is available
on this machine; do not substitute assumed offsets or claim sibling equivalence.

Use synthetic rule 7 and example.net data: combine all chips/search after
scrolling; programmatically scroll while typing; retain query and selected ID
across tab return/resume/filter/refresh. Hide selected row, Clear, narrow/widen,
and push another rule; check retained embedded ID. Cover cold/slow/empty/no
matches/denied/offline/stale failure/pull retry, every chip and Clear, one joined
row activation, first text/divider and final row, fades passing taps/drags,
Reduce Motion and repeated search. More -> rules -> pinned Devices -> More
must restore Back/base without reopening. Test rule -> template -> related rule
and rule -> alert -> Back in narrow and wide modes, notification Change/Undo
clearance and accepted #447/#448/#449 regressions. Smoke Health retained return
and first-row divider; no Health/shared code changed. Capture placement-loop
and safe-area diagnostics. Parent owns independent review, CI and merge before
the next issue; user acceptance remains required, with no closures or epic ticks.

### Independent review disposition

Claude Opus 5.5 Medium reviewed the full diff at ce3f33b against 56ccb45: no
blockers, one plausible low–medium native-frame timing finding and one nit.
Parent traced ListDetail: its trailing star column fills the right edge with
no native side safe area. Accepted the timing finding and removed the redundant
conversion and detail SizeChanged callback; direct clear.Right preserves the
correct settled clearance without depending on frame update ordering. Fold,
rotate and width-only resize remain explicit device checks. The ChipScroll
SizeChanged nit is retained: harmless, idempotent placement also covers chip
geometry updates. Fresh revised-head independent review and CI are pending.
No device result or acceptance is inferred.

Fresh Opus 5.5 Medium full-diff review at 4710d25 confirmed the inset fix
and found no blockers. Its low robustness finding is fixed with 16pt XAML
side defaults on each slot (chip padding stays internal); measured placement
overwrites these once current-page activation runs. This avoids a possible
initial edge-to-edge frame without changing measured layout or ownership.
The stale converted-bounds PR wording is corrected. No behavioural code
changed after this review; final-head CI and physical checks remain pending.
