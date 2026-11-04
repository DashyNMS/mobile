# Backup diff assessment

Baseline main `180eb4e`, 3 November 2026. Read live #464/#442/#462/#463,
CLAUDE.md, docs/DEVELOPING.md, full docs/DESIGN.md and DESIGN-REFERENCES.md.
There is no separate FULLDESIGN.md or AGENTS.md in this checkout. Applied
repo-workflow and unslop. User direction authorises clearer device/version
hierarchy and themed code presentation with existing comparison semantics.
Read pending #504 and #505 diffs and their assessment notes read-only.

## Baseline modes and contracts

Device detail opens relative backup history from Devices, Dashboard, alerts
and nested device links. History Compare selects two backups and sorts the
pair by ValidSince, older first. It passes UnimusDiffRequest with DeviceName,
Original and Revised backups through the relative unimusdiff route. IDs are
passed unchanged to GetDiffAsync. This page has no descendant route. Header
Back and glass Back return to the actual history caller, retaining its
selection; history returns to its actual device caller. Retained More
activation belongs to #490. Routes, caller IDs and history are unchanged.

Modes are loading, changed diff, identical, folded/unfolded context, Only
changes off, Ignore blank lines on/off, missing response, permission/network
error, Retry, Share and superseded pair completion. No Copy, editing, wrap
toggle, horizontal scroller or backup-write action exists on this page.

Core is pinned to 2.0.0-ci.52. Inspected local Core source at
`5f7bde53ac05ea75f7cb6dd06a8fae62606f0769`, IUnimusApi.GetDiffAsync,
UnimusBackupDiff/line groups, UnimusDiffBuilder and UnimusExport. Existing
consumer usage confirms the API names; the installed pinned package is not
available locally for assembly verification. CI must confirm compilation
against the actual pinned package. No package version or Core API changes.

Core owns original/revised numbering, COMMON/CHANGED/INSERTED/DELETED order,
removed-before-added presentation, context folding and added/removed counts.
OnlyChanged defaults true; IgnoreEmptyLines defaults false. The latter retains
Core's existing explicit whitespace-line filtering, including its counts.
Expanding context preserves source order; changing blank-line filtering resets
expanded run IDs. No extra normalisation, sorting, count adjustment, truncation
or context removal is added. UnimusDiffItem delegates shared Text/Prefix/Row
to UnimusLine, without calling FromContent or splitting/rejoining configuration.
Tabs, CR, empty lines, whitespace and Unicode remain exact API row strings.
Native wrapping and nonprinting characters need device verification.

Share remains the complete Core unified diff from the original server diff,
independent of folded or filtered display. File headers, sanitised name,
text/x-diff and existing text adapter are unchanged. This is a diff export,
not a byte-identical backup export. No new copy/share normalisation is added.
#505 owns original-byte backup sharing and the additive IShareService method;
this consumer continues using ShareTextFileAsync. Permissions, matching,
source backups/timestamps/device IDs, TLS, credentials, independent Unimus
settings and persistence remain unchanged. No configuration is logged.
Local timestamps now display seconds and numeric timezone offset, with absent
dates labelled Unknown date; LibreNMS timestamp preference is unchanged.

## Findings and outcomes

| Scope / trigger | Baseline | Result |
| --- | --- | --- |
| Compared identities, same-minute dates | Fix required: dates alone do not name revisions | Device heading and separate Original/Revised backup IDs, seconds and offset |
| Added/removed/context | Core semantics compliant; native cues unverified | Preserve exact plus/minus/text, colour tints; spoken change kind and both source line identities |
| Long lines | Character wrapping compliant | Retained, larger monospaced text, automatic number gutter; no horizontal model invented |
| Context activation | Fix required: tap-only row without native action | Single 44pt Show context button, wrapping explanatory label, no duplicate tap recogniser |
| Finite list/large diff | Finite virtualised list compliant; performance unverified | Retained star viewport, no enclosing ScrollView; full diff remains in memory |
| Code colours and fade | Fix required: 12pt page-colour fade over code surface without reserve | 20pt matching code-colour fade inside viewport, 20pt header reserve, themed code text |
| Filter controls/actions | Fix required: chip hit targets and spoken selected state | Native switches with wrapping labels and 44pt rows; Share and Retry 44pt; toolkit prevents overlapping async actions |
| Identical | Valid empty diff compliant; missing result falsely identical | Valid result says no differences with current options; missing result errors and offers Retry |
| Error/loading/new pair | Fix required: old rows/export could survive or win a race | Clear rows/result/export immediately; generation guard covers success, error and finalisation; Retry loads exact current pair |
| Empty/filtered/truncated source | No truncation marker exists in Core's diff DTO | Never invent a cap or completeness claim; show returned rows; filtering is explicit; upstream-undisclosed truncation cannot be detected |
| Back/glass/insets/lifecycle | Relative routes compliant; shared lifecycle fixes pending | Preserve PushedListLayout consumer, require #504/#505 integration below; native result unverified |
| Security/persistence | Compliant by inspection | No changes; native share destination and denied access remain device checks |

## Sequential integration and shared ownership

This PR changes only the diff view model, its XAML, a diff-only presentation
item, regression tests and this note. It does not edit UnimusLine,
PushedListLayout, share interfaces/adapters, contents or history.
UnimusDiffItem composes #463's shared line contract and adds only diff-specific
spoken identity. #505's backup-only ContentDescription cannot describe removed
and added lines correctly and is deliberately not used unchanged here.
No new shared renderer or styles require a consumer migration.

The branch is updated after merged #504 and #505.
Keep #504's idempotent activation and 20pt footer intent together with #505's
retained/disposed CoveredEdges watcher and central AppShell activation
and paired Moved/native observer lifetime. Do not replace their helper with
this checkout's older PushedListLayout. Audit all three helper consumers:
history, contents and diff. Preserve single native top inset, disabled native
CollectionView inset adjustment on HandlerChanged/Loaded, body side gutters,
actual previous-title Back, one glass owner and footer clearance. Diff's code
fade/header reserve is inside its viewport, below the header and filters.
Do not add a second page-top 20pt push or float it below a scrolling BackBar.
#505's byte-sharing addition must retain the text-sharing method used here.
Tests append to UnimusTests.cs and must retain both preceding PRs' cases.

Queue remains #489, #491, #492, #493, #494 Map, #495, #496, #497, #498,
#499, #500, #501, #502, #503, #504, #505, then this #464 PR. No queued branch
was edited or updated. No merge, release, closure or epic tick is authorised
here. Parent owns branch update at this PR's turn, sequential Claude Opus 5.5
Medium review after usage resets, CI, merge and cleanup. TestFlight follows
#460 through #464; explicit device acceptance gates issue closure.

## Validation and outstanding evidence

Full intended diff self-inspected and git diff --check passed. Added regression
coverage for missing response/Retry, cleared rows/share while changing pair,
stale successful and failed completions, exact whitespace/CR/Unicode row text,
spoken removed/added identities and complete Core export after filter changes.
Existing tests cover identical pairs, counts, folding and expansion. These
are authored tests, not executed results. No local build, test, restore,
script, independent review or Claude child attempt ran.

CI is pending for shared tests and Android/iOS compilation, including compiled
XAML and the pinned Core API. Mandatory Claude review is pending separately.
All device cells below are unverified; record commit/build and actual results.
CI or code inspection does not establish native accessibility or layout.

| Matrix | Required evidence |
| --- | --- |
| Exact text | Identical pair; additions only; removals only; modified block; common context with divergent old/new numbers; mixed LF/CRLF, literal CR, tabs, leading/trailing spaces, empty/consecutive/trailing lines and Unicode; source row equality and unchanged complete unified export |
| Context/options | Expand once and all runs; Only changes on/off; Ignore blank lines on/off including blank-only changes; counts match Core; filtering never changes exported diff; no duplicated activation |
| Scale | 100,000-line synthetic pair and 20,000-character line; first and final line reachable, full wrapping, bounded viewport, scroll performance and memory; full context reachable without truncation |
| Failure/retry | Missing response, 401/403, offline, timeout; Retry succeeds/fails on same pair; old successful and failed response after newer pair; no false identical or wrong-pair rows/share during loading |
| Version/share | Same-minute and unknown dates, long device name, exact backup IDs/order and local offset; full diff file compared with Core export after folding/filtering; cancelled share sheet, double Share/Retry, no overlapping action |
| Routes/glass | Devices/Dashboard/alert/nested device to history to diff and Back; history selection retained; More activation retained; one owner through child return and repeated background/resume |
| Accessibility | VoiceOver and TalkBack source order, added/removed/context plus old/new line identities, blank lines, one announcement per item, no decorative number/prefix announcement; native Show context and switch activation once; 44pt actions |
| Themes/text | Light/dark code contrast and exact fade colour; maximum text size and many-digit line numbers; initial header reserve and final-line glass clearance; fixed metadata/filters leave usable viewport |
| Duo | Outer portrait, inner portrait and inner landscape; fold/unfold, rotate and resume during loading, expanded context and final-line scroll; no duplicate insets/subscriptions; placement diagnostics |
| iPhone | Ordinary iPhone portrait/landscape, current glass and older header Back where available; rotate/resume, actual history return, final-line clearance and share sheet |

## Completed integration

Updated against main 180eb4e after #504/#505. Shared PushedListLayout now owns
idempotent Activate and disposable edge watching; the diff page implements
IActivePage to use that path without local Window hooks. UnimusLine uses the
merged #463 source/line description baseline. Full-diff Claude review must
verify diff-specific accessibility still conveys added/removed/hidden semantics.
No device acceptance is claimed.

## Claude review remediation

Claude Opus 5.5 Medium reviewed b475878 against main 180eb4e with no blockers.
Hidden captions now describe hidden lines without instructing a non-responsive
tap; the native action announces Show context plus the hidden count. Decorative
switch labels are excluded from duplicate announcements. Blank lines are spoken
as blank. Added/removed tint now covers each full-width padded row continuously.
API errors use Core's user-facing reason; other failures use a fixed fallback.
The dead Subtitle property is removed. Loading intentionally omits the base
Failed event and currently first-load timing, consistent with backup contents;
no configuration-bearing exception enters diagnostics.

Replacement CI is pending. No local builds/tests ran. All hardware outcomes
remain unverified, including screen-reader and Voice Control activation,
continuous tint, large-text wrapping, performance, glass and safe areas.

## Current corrective assessment, 4 November 2026

Baseline main `d78b335`, including merged #527/#528. Earlier sections retain
review provenance for historical merged #506; they are not current acceptance
or a replacement branch to replay. Inspected existing branches/PRs, original
cached and live #464 body and all comments, live #442 body/comments, relevant
reconciliation and current #462/#463 assessments. Unrelated #475 work is
untouched. The user likes the diff presentation but rejects viewport alignment
and header-only Share. This correction preserves the liked presentation.

Applied repo-workflow and unslop. No agents, local builds, restore, tests or
scripts run. DESIGN already covers the reusable rules; no duplicate rule is
needed. The current status below supersedes historical pending integration
claims without removing their provenance.

### Scoped triggers, modes and locations

| Trigger / location | Current status, expected result, actual source and correction |
| --- | --- |
| Initial/populated/filtered/expanded diff; Body/card/Rows base | Corrected policy inherited from #527; native outcome UNVERIFIED. Full-height Body has Auto metadata/filter/status rows and a finite star card. Shared helper measures pill in native Body host; side base is max(0,H-B), without safe-bottom clamp. Bottom obstruction is cleared in RowFoot; top-wide pill has its separate policy. No fixed action or extra action row/space is introduced. |
| Share available on iOS26; Header and glass | Corrected in UnimusDiffPage: hide the header Share and supply SetActions with Share complete diff; helper adds actual-caller Back. No search exists, so none is supplied. Android/older iOS retain named Share, minimum 44pt in both dimensions, bound to the same command. No duplicate header action/observer. Native reach and fallback rendering UNVERIFIED. |
| Load, identical/filtered-empty, error and async Share busy | Corrected action lifecycle: ShareCommand.CanExecuteChanged subscription is active-only and idempotent. Dispatch updates after taps, guard delivery by active state, cache availability, and recheck active plus actual CanExecute immediately before Execute. Toolkit excludes overlapping Share. Existing HasDifferences availability semantics remain unchanged, including no Share when filtering yields no differences. Cancellation, failure and busy handoff UNVERIFIED. |
| Appearing, retained tab return, resume and departure | Compliant shared activation plus corrected action subscription: Activate updates current actions and reclaims layout without reload/reset; Disappearing detaches and hides owner-safely. Queued updates cannot act while inactive. No per-page Window/native observer. Event order and native ownership UNVERIFIED. |
| Compared version identity and source request | Compliant unchanged view model/caller: wrapping device name, Original/Revised exact backup IDs, local seconds/offset and Unknown date. History sorts ValidSince older first and passes original IDs unchanged; Retry uses that exact pair. Long identities/maximum text render UNVERIFIED. |
| Additions, removals and context rows | Compliant unchanged renderer: full-width added/removed tints, plus/minus and old/new numbers, spoken kind and both line identities, blank-line cue. Source order and counts remain Core-owned. Show context is a single 44pt button; expansion stays in place. VoiceOver/TalkBack and continuous tint UNVERIFIED. |
| Only changes / Ignore blank lines / context expansion | Compliant unchanged Core options and expanded-run tracking; blank-line option resets expanded IDs. No selection/filter defaults or counts rewritten. History's two selections, third replacement and Cancel remain unchanged. Native activation and scroll retention UNVERIFIED. |
| Long lines, large diff, first/final row and fade | Compliant source: CharacterWrap, automatic number gutter, finite virtualised list without enclosing ScrollView, full result retained in memory. No horizontal scroller, wrap toggle or cap exists. Fade endpoints match SurfaceAltLight #EEF1F5 / CodeDark #0D1015, including transparent RGB; 20pt header reserves initial text. Rows/tints/fade remain unchanged. Native stroke edge, initial clipping, final tinted line and scale performance UNVERIFIED; no speculative offset/cause claim. |
| Identical, missing response, load error and Retry | Compliant unchanged generation guards and clear-before-load result/rows/export. Valid identical is distinct from missing response/error; denied/offline/timeout keep existing user-facing error and Retry. Old successful/failed completions cannot replace current pair. Native controls/errors and Retry outcomes UNVERIFIED. |
| Complete diff export after filters/context | Compliant unchanged ShareAsync: Core ToUnifiedDiff reads original _diff, not displayed Lines/_result; captured exact compared IDs, headers, filename, text/x-diff and ShareTextFileAsync remain. No newline/encoding conversion or source rewriting added here. This is unified diff text, not byte-identical original backup export; backup raw-byte fidelity remains #463's contract. Actual destination newline bytes and full unfiltered source fidelity UNVERIFIED. |
| Binary/source failure and unavailable integration | Compliant unchanged server/Core error path; no client binary parser or fabricated binary comparison/identical result. No source truncation marker exists, so undisclosed upstream truncation cannot be diagnosed. Binary/error permissions and source failures UNVERIFIED. |
| Entry, return, selection and persistence | Compliant unchanged Devices/Dashboard/alert/nested device → history → diff → Back, then actual device caller; retained More and history selection preserved. No child route from diff. Matching, permissions, jobs, connection revision, TLS consent, token/keychain and persistence unchanged. Native routes UNVERIFIED. |
| Search, keyboard, fixed Save, editing, Copy, backup-now/job writes | N/A: absent on diff. No invented search/action space or keyboard UI. Sibling contents keyboard dismissal and return must restore shared base; physical restoration UNVERIFIED. |

### Shared dependencies and ownership

Inspected all three PushedListLayout consumers: history, contents and diff.
The helper is unchanged. #527 owns shared geometry, native inset suppression
on handler/load, central IActivePage activation, caller Back and manual keyboard
handover. #528 supplies the Share observation/cache/dispatch pattern used here.
No shared renderer, model, export adapter, route or sibling page is edited.

The native page owns top clearance; Body owns gutters and measured base;
RowFoot owns final-row obstruction clearance. Measure native page → Body →
card → CollectionView and pill in one coordinate system. Final-row reach alone
is insufficient evidence that the viewport base aligns with the pill.

Inherited Android SafeAreaEdges.All includes native soft-input resize and a
12pt no-pill host gap. Older iOS fallback uses 12pt plus safe bottom and a
20pt footer. These are affected behaviours requiring verification, not unchanged
platform claims. ManualKeyboardScroll's static owner set can retain owners if
Disappearing never occurs. Thirteen accepted setup pages lack IActivePage;
retained activation gaps are not claimed fixed. Regress accepted AddCard,
GraphPicker, SensorPicker, TopCardSetUp, SpotlightPicker, MapLocationPicker,
GraylogDevicePicker, NotifyRulePicker, SettingsSection, Thresholds,
GraylogSettings, UnimusSettings, SensorDefinitionsSettings and
NeighbourGroupEditor, plus sign-in on session expiry. Preserve their accepted
geometry/save contracts and test setup/backups handover in both event orders.

### Validation and required physical evidence

Code self-review: complete intended diff against main and DESIGN, iOS guards,
command state, subscription pairing and staged whitespace inspected. Independent
full-diff Opus 5.5 Medium review is PENDING with parent. Shared-test CI and
Android/iOS compilation are PENDING for the final pushed head. No local
builds/tests/scripts executed. CI is not native acceptance or measured cause.

Every physical cell below is UNVERIFIED at the exact final head SHA in the PR
handoff. Build/OS are unavailable. Record SHA, build, OS, device, orientation,
theme, text size and individual expected/actual measurements for each run.

| Matrix | Required evidence |
| --- | --- |
| Geometry/devices | Duo outer portrait/landscape, inner portrait/landscape; ordinary iPhone portrait/landscape; Android and old-iOS fallback. Side/bottom/top pill; native host/card/viewport/pill bounds and base delta, final complete tinted row, first row below fade; no extra action space. |
| Transitions | Fold/unfold and rotate while loading, expanded, filtered and at final row; retained tab return, sibling contents/history return and background/resume. One glass owner, actual-caller Back, no placement loop; keyboard dismissal in sibling restores base. |
| Themes/accessibility | Light/dark, maximum text, long identity and many-digit numbers; VoiceOver/TalkBack change/line identity once, blank rows, switch state and Show context once, named 44pt fallback Share; Reduce Motion and overflow selection/cancellation. |
| Source/context/options | Identical, additions/removals/modified/common/blank-only; mixed LF/CRLF, literal CR, tabs, whitespace, Unicode and final/trailing lines. Context expansion and both filters preserve Core counts/order; export complete unfiltered diff with exact headers and newline bytes. |
| Scale | 100,000-line synthetic pair and 20,000-character wrapped line; initial/final line, responsive scrolling/context and memory; no truncation or source rewriting. |
| Failures/retry | Missing response, binary/source error, unavailable integration, 401/403, offline/timeout, Retry success/failure on exact pair, superseded success/error; no false identical or stale rows/share. |
| Share/routes | Cancel, failure, double tap, busy disappearance/return, stale native callback and no duplicate header; native reach/overflow; all entry routes and Back with history selection/older-first IDs retained. |
| Shared regressions | History 0/1/2 selection, third replacement, Cancel; contents exact bytes/search/keyboard; accepted setup/sign-in handover in both orders and retained Settings/Unimus return, without claiming existing activation gaps fixed. |

No merge, watch, release, issue closure, epic tick or device acceptance is
performed. Parent owns sequential full-diff review, CI and merge.
