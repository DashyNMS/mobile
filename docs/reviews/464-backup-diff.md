# Backup diff assessment

Baseline main `83e9a39`, 3 November 2026. Read live #464/#442/#462/#463,
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

Parent must land #504 first, then #505, then update only this PR at its turn.
Keep #504's idempotent activation and 20pt footer intent together with #505's
retained/disposed CoveredEdges watcher, retained Window resume subscription
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

## Sequential integration

Updated against main 180eb4e after #504/#505. Shared PushedListLayout now owns
idempotent Activate and disposable edge watching; the diff page implements
IActivePage to use that path without local Window hooks. UnimusLine uses the
merged #463 source/line description baseline. Full-diff Claude review must
verify diff-specific accessibility still conveys added/removed/hidden semantics.
No device acceptance is claimed.
