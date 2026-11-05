# Graylog message details — #461

Assessment baseline: prepared main `83e9a39`, 3 November 2026. Read live
#461/#442/#460/#483/#288, CLAUDE.md, DEVELOPING.md, the complete DESIGN.md
and DESIGN-REFERENCES.md. There is no FULLDESIGN.md in this checkout.
PR #502's scope and full diff were read without changing its branch.
Routine guide interpretations are authorised by the user; #288 retains
Graylog mock-ups and global light-theme acceptance.

## Baseline assessment before changes

| Concern | Baseline result | Evidence / resolution |
| --- | --- | --- |
| Full message and additional fields | Compliant | FullMessage falls back to Text; GetText retains strings and JSON; no detail truncation |
| Source identity and matching | Compliant | DeviceFor/Core DeviceAddresses.Find retains IP/full hostname/sysName/case/unique short-name semantics; source then remote IP fallback |
| Time and severity | Fix required | Missing values silently disappear; add explicit unavailable detail labels without changing list colours or time conversion |
| Long field keys/values and multiline content | Fix required | 2*:3* columns constrain narrow/large text; stack labels over full-width values and character-wrap |
| Sender/device actions | Fix required | Truncated sender and anonymous gesture row; wrap whole 44pt targets, join nonblank device identity/severity, exclude child duplicates, enforce positive device ID |
| Missing route data | Fix required | Load ignored absent/invalid message, leaving previous content; clear record/list and commands |
| Pushed host | Fix required | Legacy native safe-area flag plus bottom-only extra clearance; no disposed safe-area watcher or resume reclaim |
| Embedded host | Fix required | Hidden Back button retained empty bar; hide whole bar and keep list ownership |
| Loading/error/empty/retry | Compliant with missing-route fix | Detail uses list payload synchronously, no fetch/generation/race or retry request; missing record directs Back/reselect |
| Persistence/security | Compliant | No detail writes; no changes to settings, TLS, credentials, permissions, readOnly or logging |
| Native measurement, screen readers, fold/resume | Unverified | Requires CI/platform/device evidence below |

## Modes, routes and contracts

GraylogMessageItem projects Core's full search payload. GraylogMessageViewModel
receives that item and optional GraylogViewModel owner. Network Graylog (More,
pinned tab or Logs), device Graylog, and dashboard Graylog feed entries all
reach the same relative message route. GraylogPage intercepts that route when
Split.IsSplit and creates an embedded view with IsBesideList. Narrow screens
push GraylogMessagePage. Back is relative `..` and labelled from the actual
previous caller. Open device is a relative device-detail route; Back returns
to the message, then its original caller. Existing device-scope suppression
of redundant Open device remains. Show only applies the existing device or
source filter and returns only in pushed mode; embedded mode stays in place.
No absolute routes, query semantics or list refresh policies change.

All raw server fields remain in All fields, including source whitespace,
newlines/tabs, timestamp, unknown level, message/full_message and arbitrary
condition/URL/HTML-looking strings. TextType is plain Text. Existing
case-insensitive field ordering and Core GetText JSON/null presentation remain.
SourceAddress trimming belongs to the existing filter, not raw field display.
No summarising, link interpretation, timestamp zero or invented OK state is
introduced. Timezone conversion remains existing settings-fed zone semantics.

Core is pinned to 2.0.0-ci.52. Existing compiled call sites/tests establish the
constructor, Fields/GetText and nullable metadata contracts used here. Local
Core source GraylogModels/GraylogQuery was inspected read-only; the pinned
package is not present locally, so exact binary verification remains CI work.
No new Core API or package change is introduced.

## Implementation and ownership

Stacked field rows deliberately work at every width, avoiding font-scale
breakpoint assumptions for arbitrary server keys. Full-width mono values
wrap without altering stored text. Accessible actions use one containing
44pt target rather than multiline Button measurement. Commands refresh when
the owner changes even if the same message instance is loaded again.

The pushed page owns native top only on iOS; content owns side/bottom clearance
from ForScrolling. The view disables native scroll adjustment on HandlerChanged
and Loaded. Android retains native safe areas. PlaceContent is the explicit
host boundary: embedded list passes its already-adjusted clearance; pushed
page passes full scrolling clearance. No shared control changes. GraylogPage's
TabAlignedListLayout remains owner of embedded glass. Visible scrolling BackBar
starts at top zero and hides the fade. Otherwise a 20pt content reserve sits
under a full-viewport input-transparent fade matching the view's Background.
Final fields receive bottom clearance. Watch subscription is idempotent and
disposed on disappearance; the actual resume Window is retained for removal.
Queued placement checks visibility. No extra native notification observers.

Dependencies: merged #490 retained More activation is preserved; this PR
reclaims only the pushed message on appearing/resume and must be tested with
that dependency. #502/#460 owns log details and was used only as a host-pattern
reference. #483 owns device/network Graylog list work; #405's accepted connection
and picker behaviour is untouched. No other branches were edited.

## Validation and remaining acceptance

Synthetic regression coverage adds raw long-key/multiline/URL text fidelity,
missing time/level, stale reset, positive-ID command gating. Existing tests
cover source matching, known/unmatched filter and pushed/embedded return.
No local builds, tests or scripts were run, per user instruction. Full intended
diff self-inspection and git diff --check are the local checks; these do not
establish compilation, screen-reader activation or device layout correctness.
Claude Opus 5.5 Medium review is pending sequentially after reset, after
489 → 491 → 492 → 493 → 494 Map → 495 → 496 → 497 → 498 → 499 → 500 → 501 → 502.
Only the parent updates this PR at that turn after previous PR merge. Shared
unit-test, Android and iOS CI remain pending; no independent review was run.

Every row below is **unverified**, with no device/build evidence yet. Record
commit/build, OS, result and diagnostics separately for each row; CI is not
visual acceptance.

| Exact device case | Required result |
| --- | --- |
| Network Graylog → message → related device → Back twice | Same message then original filtered list; one glass owner |
| Device → Graylog → message | Redundant device action hidden; Back to that device list |
| Dashboard Graylog card → message → Back | Actual dashboard caller restored |
| Wide split → embedded message → Show only/device → return | No Back/header gap; list owns glass, selection/filter retained |
| Long 160+ character keys/values, tabs, CRLF, blank lines, URLs/conditions | Full plain text, no truncation/interpretation; last field reachable |
| Missing source/time/level/full_message; invalid route after valid route | Honest missing metadata/fallback; no stale record or runnable links |
| Source full/short hostname, IP, case variations, duplicate short names, unmatched | Existing Core matching and filter semantics preserved |
| Narrow split and large text, light/dark | Wrapping target and field text readable, fade matches surface |
| VoiceOver and TalkBack | One description per target, device identity/severity, activation works |
| Duo outer, inner portrait, inner landscape | Side/bottom glass clearance and final field reachability |
| Duo fold/rotate while open; background/resume; retained More activation | Recomputed insets, actual Back owner, no duplicated handlers/loops |
| Ordinary iPhone, glass and older header fallback | Top zero BackBar or reserved fade; relative return and bottom clearance |

No issue closure, epic ticks, merge or release. User acceptance remains pending.
TestFlight belongs to the parent after #460–#464.

## Independent review remediation

Claude Opus 5.5 Medium reviewed full diff 9e1467f against main c25b71c.
All three findings are addressed: the pushed page implements IActivePage and
uses AppShell activation without a local Window hook; Moved subscriptions and
edge watching are idempotent; glass Back is visibility guarded. The fade ends
in the matching transparent background RGB, and severity uses StatusText's
readable themed text colours rather than dot colours.
Unit tests passed at reviewed head; replacement platform/security CI is pending.
Full intended/staged diffs and whitespace inspected. No local builds/tests ran.
All device outcomes remain unverified, including retained More return, resume,
light-theme severity contrast and the fade. No issue closure or epic ticks.

## Current corrective assessment, 4 November 2026

This section supersedes the baseline findings and pending statuses above for
this correction only. Historical PR #503, its independent review and CI retain
their provenance; they do not validate this diff. Prepared main is
`63466c80fdab8a1300d61ce4d6f7d52ead85bcf3`, including merged #525.
Live #461 and #442 bodies and all comments, #288 and accepted #460 were
refreshed. #460's explicit acceptance is preserved, as is completed #405.
The current reconciled assessment's #461 entry records the same rejection.

### Hierarchy and consumer inventory

Pushed: Shell native page → GraylogMessagePage, native top container on iOS
and native All on Android → GraylogMessageView → star Grid → finite
ScrollView → ContentColumn → metadata/message/actions/stacked additional
fields. There is no ReadableColumn or proportional-width cap. Previously the
root Grid and content column had implicit safe-area handling while the host
already supplied measured content clearance. Both now explicitly have no
safe-area ownership. The viewport has no margin, padding or height offset.
Only ContentColumn owns 16pt gutters, side clearance and 24pt final-field gap
plus bottom clearance. Native scroll adjustment is Never on attachment/load;
those events reapply stored clearance rather than substituting zero.

Embedded: GraylogPage → ListDetail → trailing star _pane → _detail
ContentView → the same message view hierarchy. ListDetail's pane and content
host already opt out of safe areas. The pane reaches the page right/bottom,
so TabAlignedListLayout passes its existing right/bottom clearance and zero left. No
native frame conversion during SizeChanged is required. Its list column,
card base, footer and camera layout are untouched. TabAlignedListLayout's
existing detail callback supplies Graylog message placement directly;
its other consumer LogsPage is unchanged. ListDetail's
other consumers are unchanged. Shared helpers have no edits.

GraylogMessageView has exactly two construction consumers: GraylogMessagePage
and GraylogPage.TryShowDetail. Network/device GraylogViewModel.OpenMessage
passes the message and list owner; DashboardViewModel.OpenGraylogMessage
passes only the snapshot. ShellNavigationService retains split interception
and relative routing. NeighbourLinkView/NeighbourLinkPage and NeighboursPage
were inspected as stored-clearance and trailing-pane references, not copied
as a new detail design. CoveredEdges, BackBar and AppShell remain shared
owners. AppShell's central IActivePage dispatch restores pushed glass on
retained-tab return, child return and resume. No global subscriber was added.
#483 owns device-list review and #517 owns list camera layering.

### Current findings

| Concern and trigger/mode | Location and observed result | Expected result and code fix | Status |
| --- | --- | --- | --- |
| Reported constrained width/short base, pushed and embedded | Inner Grid/content column safe-area defaults were implicit; their actual effect and reported dimensions are unknown | Explicit star viewport and content-only clearance; no guessed percentage or height correction | Explicit contract; symptom cause and resolution unverified |
| Resize/fold/native attachment, both modes | Attachment only disabled native adjustment; embedded relied on legacy list clearance | Retain/reapply clearance on attachment; trailing right/bottom values match the prior helper values, not a new geometry correction | Attachment retention corrected; embedded geometry unchanged |
| Pushed actions when glass available | Only Back was in glass; Show only/Open device stayed inline | Eligible existing actions join pushed glass, inline fallback retained on Android/older iOS; absent actions reserve no space | Corrected |
| Child/tab return and resume, pushed | Central IActivePage already present | Preserve it; idempotent subscriptions, disposal and queued current-page guards; glass rebuilt on state change | Compliant, guards corrected; native operation unverified |
| Source/time/severity, both modes | Source matching, raw fields, explicit unavailable time/level and themed severity already present | Preserve original values, zone semantics and unknown state | Compliant; theme/max-text appearance unverified |
| Full message, arbitrary long keys/values, CRLF/tabs/JSON/URL-like text | FullText and GetText fields use plain CharacterWrap stacks | Preserve full payload and field order; no parser, truncation or new links | Compliant; physical readability unverified |
| Related-device and Show only | Positive-ID gating, current-device suppression and list filter return already present | Keep routes, IDs, scope and read-only snapshot semantics; guard glass command execution | Compliant/corrected guards |
| Missing route after valid route | Load clears message/owner and commands | Retain return/reselect guidance; no stale record | Compliant |
| Loading/error/retry | No detail endpoint or asynchronous fetch | Existing synchronous snapshot; reselect from caller | Compliant; separate detail loading/retry N/A |
| Copy/export/security/persistence | No detail copy/export route exists; list export and raw data unchanged | No new feature, storage, credentials, TLS, permission or server write | Compliant; detail copy/export N/A |
| Fade/final field | Matching Background RGB endpoints, input-transparent 20pt fade and initial reserve when header hidden | Preserve reserve and bottom content clearance without shrinking pane | Compliant; final-row/glass bounds unverified |
| Accessibility, themes, motion, large font | Wrapping actions retain 44pt minimum and joined accessible descriptions; no new animation | Verify native activation, contrast and full content reachability | Unverified |

### Validation and acceptance at this correction

Full diff against prepared main, staged diff and DESIGN self-inspection are
separate from independent review. Whitespace is checked with git diff --check.
No agents, builds, tests or scripts ran. Independent full-diff review by the
parent's Opus 5.5 Medium is pending. Shared tests, Android build, iOS simulator
build and security CI are pending. No physical result is claimed. The PR head
identifies the exact final SHA; the parent must record its build and OS before
running this matrix. All rows are device ALL UNVERIFIED.

| Exact build/OS matrix, run at final PR SHA | Required observations, all unverified |
| --- | --- |
| Duo outer portrait/landscape, inner portrait, inner landscape | Both pushed and embedded where supported; actual host/pane/viewport bounds, usable width/base, camera and glass bounds, final-row deltas |
| Duo fold/unfold and rotate while detail open | No stale clearance, no reserved absent-action space, last field scrolls above glass |
| Ordinary iPhone, iOS 26+ glass and older iOS fallback | Network/device/Dashboard entry, child device and Back twice, one active owner |
| Android outer/inner or phone/tablet, portrait/landscape | Native safe areas, fallback actions, full finite viewport, TalkBack activation |
| Every supported device/OS above, light and dark, maximum text | Long keys/values, CRLF/blank lines/tabs, unknown metadata, missing route, final field, fade RGB and initial 20pt reserve |
| Every supported device/OS above, retained tab, child return, resume | Same payload/filter/scroll contract, correct caller Back/actions/header geometry, no duplicate handlers or placement loop |
| VoiceOver and TalkBack, both modes | Action names include identity/severity; one target, minimum 44pt, activation and traversal |
| Keyboard where relevant | Detail has no input; return from caller search or child keyboard restores usable bounds; floating keyboard obstruction remains unverified |

Record exact SHA, build number, OS version, diagnostics and measured deltas
for each executed case. No merge, release, watch, closure or epic ticks here.
Outstanding Graylog mock-ups/global light-theme acceptance remain #288; this
correction introduces no unresolved visual redesign decision.

Shared consumer inventory supplement: ListDetail is used by DevicesPage,
NeighboursPage, LogsPage, AlertRulesPage, AlertsPage and GraylogPage. Only
GraylogPage's message boundary changes. TabAlignedListLayout is used by
LogsPage and GraylogPage. GraylogMessageItem also supplies DashboardCard
previews and GraylogViewModel rows; neither projection changes. Command
CanExecuteChanged and relevant view-model properties are observed only while
the pushed detail is active, with matching unsubscription on disappearance.

### Independent review disposition and unresolved symptom

Opus 5.5 Medium full-diff review at fe6c669 found no blockers. Parent recorded
three low findings as fixed, but the description at 4d9dd90 overstated completion:
PlaceMessage still redundantly recalculated clearance, UseGlassActions still
accepted a false branch that would destroy bindings, and embedded detail
actions were missing from host glass. The corrections below supersede that
claim. Command CanExecute gating was already present for pushed actions.
Fresh review and CI pending.

The reported three-quarter width and shortened bottom are NOT demonstrated
resolved by this patch. The explicit SafeAreaEdges contract may equal native
default behaviour; no measurement proves those defaults consumed space.
Embedded right/bottom clearance matches main. This is an acceptance gap, not
a source-confirmed geometry fix: record native page, host, pane, ContentView,
viewport and content frames on the reporting OS/build in both modes, compare
actual width/base loss and camera/glass insets, and correct the proven owner.
No guessed offsets or device success claim is justified.

The review's statement that current-page guards occur nowhere else is
rejected with source evidence: merged HealthPage and AlertRulesPage use
the same Shell.Current.CurrentPage guard. Initial-frame ordering remains
a device check, as recorded for those consumers; shared dispatch resolves
the active page at delivery. Physical outcomes remain wholly unverified.

### Host glass correction after review of 4d9dd90

Removed PlaceMessage and its call. Embedded geometry again uses only the
unchanged TabAlignedListLayout callback, as in prepared main 63466c8.
HideInlineActions is parameterless and true-only, called exclusively when
iOS glass is available. Android and older iOS retain their inline bindings.

GraylogPage now implements the existing IActivePage contract. Its central
UpdateGlass method retains host Back (only when pushed) and configured search,
and adds eligible Show only and Open device actions for the visible split
detail. No embedded Back is added. IsBesideList changes Show only navigation,
not eligibility: CanShowOnly delegates to the owner's current filter and fixed
device scope. Both commands use CanExecute for presence and again at execution.
The cache includes detail identity, both labels, both command states, search
availability and host Back state/title. Callbacks check the active host and
current visible detail, so a retained/replaced detail cannot act through an
old glass callback. Narrowing removes detail actions and subscriptions;
widening restores them without route reentry.

Host properties and detail properties/CanExecuteChanged are observed while
active. Detail subscriptions are removed on replacement, narrowing and host
disappearance; activation restores them idempotently. The view model has no
IDisposable contract: release means removing the host's observers, not clearing
its retained payload. Queued updates resolve current state and require the
current Shell page. Moved only places the existing layout; it never publishes
glass. No new native observer, window hook or shared-helper change is needed.
AppShell restores controls on retained-stack activation and resume. Activate
does not fetch, reset filters/selection or start timers: the original
EnsureLoadedAsync and AutoRefresh showing lifecycle remain in Appearing and
Disappearing. Pushed details also observe label changes through their existing
property observer and retain the same command guards.

Individual host command audit: Search remains the configured glass magnifier;
SearchCommand remains completion; RefreshCommand remains pull-to-refresh;
ChooseDevice/ClearDevice, ChooseRange, ChooseLevel, ChooseStream,
ToggleAutoRefresh/ChooseAutoRefreshInterval and ClearFilters retain their
scope chips and existing bindings; OpenSettings remains the unconfigured
setup route; OpenMessage keeps the row payload and list owner; LoadMore keeps
its existing visibility and append semantics. There is no Graylog export
command in this host to migrate or remove. No list action is replaced by child
Back. Filters are scope controls, not newly duplicated glass actions.

Ownership dependencies remain #517 (list camera layering) and #483 (list
review). TabAlignedListLayout consumers are GraylogPage and LogsPage;
ListDetail consumers are Devices, Neighbours, Logs, AlertRules, Alerts and
Graylog pages. None of these shared helpers or other consumers changed.
Any future camera/list correction must assess those consumers separately.

The geometry report remains unresolved. Explicit None defaults may be a no-op;
ForScrolling side camera clearance can intentionally apply to a whole column,
and 340/400pt list panes can be legitimate. Obtain page/native host, split pane,
ContentView, viewport and content bounds, native scroll insets/content size,
camera reserved rectangles, pill/action glass bounds and last-field position
on the reporting OS/build. Compare pushed and embedded, wide and narrow,
before/after fold/rotation. No width/height offset or geometry cause is invented.

Validation for this revision: full intended diff against 63466c8 and DESIGN
self-inspected; whitespace checked. No agents, local builds, tests or scripts.
Parent's fresh Opus 5.5 Medium full-diff review and CI are pending. Exact source
revision is the commit containing this section (resolve with git log); its SHA
is reported in handoff. Build number and runtime OS: none, code-only session.
All existing matrix rows remain unverified at that SHA, including iOS 26+
embedded glass, older-iOS/Android fallback, retained stack/resume, accessibility
and final field reachability. Record exact SHA/build/OS per executed row;
historical no-blocker review does not establish acceptance of this correction.
Refs #461 and #442; no closure or device acceptance claimed.


### Build 231 feedback reconciliation

[The user's 4 November build 231 report](https://github.com/DashyNMS/mobile/issues/461#issuecomment-6629909350),
posted after #526, says the pills still have a layering issue. This is separate
from the older three-quarter-width/short-base symptom. The affected surface and
ownership versus #517 are not established. The report predates #545 and must not
be treated as covered by the later “Keep wrapping; defer sideways underlap”
decision without user confirmation. Reproduce and identify the surface on an
installed build; #461 remains open and device acceptance remains pending.
