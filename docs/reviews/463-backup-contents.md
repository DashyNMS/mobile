# Backup contents assessment

Baseline: main `83e9a39`, 3 November 2026. Read live #463/#442/#462/#464,
CLAUDE.md, DEVELOPING.md, full DESIGN.md and DESIGN-REFERENCES.md. No separate
FULLDESIGN.md or repository AGENTS.md exists in this checkout. Applied the
repo-workflow skill. Explicit user direction authorises routine guide choices
and improved device/version hierarchy and themed configuration presentation.

## Baseline and contracts before changes

UnimusBackupPage receives UnimusBackupRequest(DeviceName, UnimusDeviceId,
Backup) through relative unimusbackup from history. Device detail opens history
from Devices, Dashboard, alerts or nested device links. Contents Back returns
to that actual history caller; history returns to its device caller. History
also opens the separate relative unimusdiff route; its selection and comparison
order belong to #462/#464. Contents opens no descendants. Retained More
activation remains #490's baseline. No absolute route or route schema changes.

Modes: initial loading, successful text, filtered text/no matches, empty text,
binary, missing content, denied/unavailable/network failure, Retry, Share,
changed query during load and return/resume. The baseline has no Copy,
configuration editing, Backup now, job UI, horizontal scroller or wrap toggle.
It character-wraps numbered lines in a finite CollectionView. The list is
virtualised, but parsing/filtering retains the whole configuration in memory.

Core is pinned to 2.0.0-ci.52; inspected matching local Core source
`5f7bde53ac05ea75f7cb6dd06a8fae62606f0769`: UnimusBackup, RawBytes,
Content, IUnimusApi and UnimusApi.GetBackupContentAsync. The latter re-fetches
page zero and decodes the exact backup ID. This PR uses that same page and ID
lookup directly to retain original bytes, without adding an endpoint or
selecting latest as a substitute. Missing/invalid payload is an error.
Empty base64 is a valid empty configuration. Binary remains unavailable.

Raw bytes are authoritative for Share; decoding is for presentation only.
UnimusLine.FromContent normalises CRLF in its display projection, retaining
line order, tabs, whitespace, blank/trailing lines and Unicode. No display
projection is stored back, copied, shared or compared. Invalid UTF-8 may appear
as replacement characters in Core's decoded view; Share preserves its original
bytes. Control characters may be nonprinting in native labels; no source is
removed. Existing UTF-8 text exports for other consumers remain without BOM.
No Copy action is introduced. Share cache uses the existing sanitised filename,
cache-only exports directory and cleanup before sharing, startup and sign-out.
No configuration or raw error payload is sent to diagnostics by this page.

Unimus matching, permissions, independent settings, token keychain/keystore,
TLS validation and explicit certificate consent remain unchanged. Epoch backup
times use the phone timezone, independent of LibreNMS's server-local timestamp
preference. Seconds and numeric offset are now visible; absent timestamps stay
unavailable, not epoch zero. No timestamp or backup object is rewritten.

## Concern assessment and fixes

| Concern / trigger | Baseline assessment before edits | Outcome |
| --- | --- | --- |
| Device/version/time identity | Fix required: date title, device buried beside count, no revision ID | Device heading, backup/Unimus IDs, local seconds and timezone offset |
| Complete configuration and search | Compliant source semantics; native rendering unverified | Preserve raw source; search filters display only; Share complete original bytes |
| Long lines/horizontal scrolling | Compliant character-wrap baseline | Retained; no invented horizontal interaction or toggle |
| Large contents/vertical scrolling | Finite virtualisation compliant; performance and scroll memory unverified | Auto-width line-number gutter; final line/footer clearance retained |
| Code surface/fade | Fix required: page-colour 12pt fade over code card; no reserved header | 20pt code-colour fade inside viewport and 20pt list header spacer |
| Actions/accessibility | Fix required: share could use stale data, small explicit targets; line identities separate | 44pt Share/Search/Retry, spoken line identity, decorative number excluded |
| Initial/loading/error/empty | Fix required: absent payload looked like no matches; no Retry | Explicit failure/Retry; empty config distinct from no matches; no empty while busy/error |
| Changed query/slow earlier request | Fix required: earlier completion could overwrite newer backup | Clear old source immediately; generation guards success/error/finalisation |
| Double actions | Toolkit async command exclusion already compliant; busy guard incomplete | Share CanExecute refreshes on busy; execution guard; Retry ignores busy |
| Back, native insets, glass | Route semantics compliant; subscription lifetime fix required | Actual previous caller unchanged, Never on HandlerChanged/Loaded retained; watcher disposed, central AppShell activation, idempotent Show/Hide and resume reactivation |
| Security/permissions/comparison | Compliant; native share destination unverified | No permission, matching, TLS or diff semantics changes; no content logging |
| Search visibility/keyboard | Fix required: permanently open field | Header Search opens/refocuses retained query, Command-F via ISearchHost, hide on unfocus/return |

## Shared ownership and queue dependencies

#463 owns the existing shared line source
`src/DashyNMS.Mobile.Shared/Unimus/UnimusLine.cs`. Its additive
ContentDescription does not alter existing Row, Text, Prefix or diff semantics.
#464 must reuse this source rather than fork its projection; diff-specific
spoken descriptions must include addition/removal/hidden cues and cannot use
the backup-only description unchanged. No new shared XAML renderer/resource is
introduced: code surface remains UnimusBackupPage.xaml; #464 owns its own
coloured diff row template. This is the renderer ownership handoff before #464.

Read pending #504 full diff and docs/reviews/462-backup-history.md read-only.
Do not copy its old PushedListLayout over this implementation. Shared consumers
are exactly history, contents and diff. #504 already proposes idempotent
activation and a 20pt footer: when parent updates this PR after preceding queue
merges, combine that baseline with the retained/disposed CoveredEdges watcher
and central AppShell activation and disposable watcher lifecycle here. The duplicate guard/footer intent
should remain once. No other branch was edited. Existing native single top
inset, body side gutters, no additional top-line push, relative Back and footer
clearance are retained. #464 must test its consumer after this helper lands.
IShareService adds a byte export method implemented by its sole platform
adapter; text-export callers remain compatible. Tests use substitutes.

## Validation and outstanding evidence

Full diff self-inspected; git diff --check passed. Regression tests added for
raw-byte equality including invalid UTF-8, tabs, CRLF, empty lines, whitespace,
Unicode/control text after search; superseded completion; and empty versus
missing content. Existing fetch/share tests now assert the raw-byte path.
No local build, test, restore or script ran, as instructed. No independent
review was attempted. Android/iOS XAML compilation and shared-test CI remain
pending. Mandatory Claude Opus 5.5 Medium review belongs to the parent,
sequentially after reset and after #504's turn in the specified queue.

All device cells below remain unverified; record exact commit/build and results
separately from CI. Neither code inspection nor virtualisation proves native
scroll memory, performance, accessibility or layout acceptance.

| Matrix | Exact evidence required |
| --- | --- |
| Fidelity | Synthetic long line, leading/trailing spaces, tabs, mixed LF/CRLF, consecutive/trailing blank lines, Unicode, NUL/control text; compare exported file bytes to original, including invalid UTF-8; search then Share still exports all bytes |
| Scale/empty | Empty configuration; 100,000 synthetic lines and a 20,000-character line; first/final line reachable, bounded viewport, smooth scrolling, search original line order, query clear restores all, scroll memory on focus/Back/resume |
| Failure/permissions | Missing row/content, invalid base64, binary, unavailable integration, offline/timeout, HTTP 401/403, Retry success/failure; no misleading Empty or old content/actions while loading |
| Actions/routes | Double Share/Retry taps; query changes during pending load; exact filename/revision; cancelled share sheet; device from Devices/Dashboard/alert/nested device → history → contents → Back; history → diff → Back selection preserved; More activation retained |
| Accessibility | VoiceOver/TalkBack source order and line numbers once, blank-line traversal, named 44pt actions, disabled/busy behaviour; Command-F, keyboard dismissal and retained query |
| Themes/text | Light/dark contrast and code-matching fade, first line below fade at rest, final line above glass; maximum text size, long device name, many-digit line numbers; no clipped actions |
| Duo | Outer portrait, inner portrait, inner landscape; fold/unfold and rotate while loading/searching/scrolled to final line; background/resume repeatedly; one inset and one glass owner; inspect placement diagnostics |
| iPhone | Ordinary iPhone portrait/landscape; current glass and older header Back where available; rotate/resume, child return, final-line clearance and native share handoff |

No merge, release, issue closure, epic tick or device acceptance is claimed.
Parent updates only this PR at its turn, then owns Claude review, CI, merge and
cleanup. TestFlight follows the #460–#464 sequence and explicit user acceptance.

## Sequential integration

Updated after merged #504 against main 95600f0. PushedListLayout retains its
central Activate entry and idempotent glass subscriptions while adding disposable
edge watching; no local Window resume hook is retained. Backup contents now
implements IActivePage. History continues using that same activation path; diff
opts in under #464. No device outcome is inferred from this integration.

## Independent review remediation

Claude Opus 5.5 Medium reviewed 8187738 against main 95600f0. Search now uses
ButtonSearch with inherited scroll dismissal disabled, preserving typing through
filtered-row replacements and dismissing only on real drag. The code-surface
fade is inside the card with a stroke inset. Share failures use separate status
and never offer load Retry. Retry fetches fresh content even after invalid bytes;
Core user-facing API error reasons are retained. Starting loads notify empty
bindings immediately. Source bytes, export and comparison semantics are unchanged.
Replacement CI and fresh Claude review remain pending. No local builds/tests
ran; hardware outcomes remain unverified.

Claude re-reviewed c95a002: prior search, fade, sharing and retry fixes hold.
CI exposed two children in the single-view PageHeader.Actions property. They
are now wrapped in one horizontal layout and Search uses the standard icon.
Load start also notifies CountText so stale counts cannot survive failed loads.

Timing limitation: this request-specific loader deliberately does not use
RunAsync or emit Failed, to keep configuration-bearing errors out of diagnostics.
It currently also omits first-load timing events. Restoring timing without error
reporting needs a separate base-helper change; no timing evidence is claimed.
Cancellation still uses the generic retryable load error. All hardware checks
remain unverified. Replacement CI is pending; no local builds/tests ran.

Platform CI at 4e0254a exposed an invalid Clicked event on IconButton.
The search icon now uses its supported Command property, matching Health.
Replacement CI is pending; no local builds/tests ran.

## Current corrective assessment, 4 November 2026

Baseline main `0c55d06`, including merged #527. The sections above retain
historical review provenance; they do not establish current device acceptance.
Read original cached #463, live #463 and all comments, live #442 and its
comments, the cached reconciliation, current #462 assessment and repository
guides. The live #463 comment still rejects viewport alignment and header-only
Search/Share. Merged #505 and its remaining remote branch are historical work,
not a replacement PR. #504/#506 are also merged. This correction changes only
contents controls and this assessment. Unrelated #475 work is untouched.

### Findings by trigger, mode and location

| Concern and trigger | Status, expected result and code disposition |
| --- | --- |
| Initial/populated/filtered contents, Body/card/Rows base | Corrected policy inherited from #527; native result UNVERIFIED. Full-height Body has Auto header/search/metadata/status and a finite star card. PushedListLayout queries pill/occupied bounds in the native Body host, applies side-pill `max(0,H-B)` without safe-bottom clamping, treats top-wide pill separately and clears bottom obstruction inside RowFoot. No per-page offset, fixed action or extra action row/gap is added. |
| First text, empty state and scrolling fade | Compliant source: 20pt list header, empty margin and stroke-inset input-transparent 20pt fade retain the code surface RGB at both endpoints. Initial clipping and final complete text reach are UNVERIFIED. |
| Search from glass/header/Command-F | Corrected UnimusBackupPage: SetActions supplies the dedicated glass search callback on iOS26; HeaderActions is hidden there, retained on Android/old iOS with named minimum 44pt Search and Share. ISearchHost and ButtonSearch remain the sole opening path. Reopening refocuses the retained query. Native behaviour UNVERIFIED. |
| Typing, filter replacements, programmatic scrolling, real drag, completion/unfocus | Compliant source: KeyboardDismiss.OnScroll=False and ButtonSearch checks native drag/tracking before closing. Completion/unfocus close through KeyboardDismiss.Close and retain SearchText. Filtering/programmatic Scrolled does not itself dismiss. No new gesture/observer or clear/copy action. Native focus and drag checks UNVERIFIED. |
| Share available, absent content, load busy and native async share busy | Corrected: observe ShareCommand.CanExecuteChanged only while active; dispatch state updates after native taps; active guard on delivery and execution; cached boolean avoids rebuilding unchanged actions. SetActions owns Back/Search/Share centrally. Toolkit command exclusion retains double-tap protection; fallback stays bound to the same command. Cancellation/failure/handoff UNVERIFIED. |
| Device/version/time metadata | Compliant baseline: wrapping device title, backup ID, Unimus device ID, local seconds/offset and honest unavailable time retained. No version or timestamp rewriting. Large-text rendering UNVERIFIED. |
| Full configuration, line identity, long lines and vertical scrolling | Compliant baseline: numbered monospaced CharacterWrap, auto-width number gutter and original line-order search remain. No horizontal scroller or wrap toggle exists; none is introduced. Virtualised finite Rows retains whole parsed content in memory; smoothness/memory for large contents and very long wrapped lines UNVERIFIED. |
| Exact sharing with or without filtering | Compliant inspected source: ShareAsync uses captured _rawBytes, never Lines/SearchText or decoded display text. ShareService writes bytes with File.WriteAllBytesAsync. BOM, mixed newlines, encoding bytes, invalid UTF-8, NUL/control bytes and trailing blanks are not re-encoded. Display normalises CRLF only in its projection; native labels may not display controls. Physical destination byte fidelity UNVERIFIED. |
| Empty, null/missing, binary, invalid payload and load errors | Compliant baseline: empty base64 is valid empty text; missing text/payload is retryable error; binary remains unavailable and unshareable. Existing error reasons and privacy handling stay intact. Retry requests the exact original ID on page zero, never latest; existing source-generation guards reject stale completions. Error/binary/empty controls and denied/offline/timeout behaviour UNVERIFIED. |
| Appearing, retained tab return, background/resume | Corrected action lifecycle: idempotent Activate attaches one command subscription, updates state and calls shared Activate. Disappearing removes subscription, hides layout and closes search retaining query. Central IActivePage restoration is retained; no new resume observer, loader or selection reset. Native event order/focus/scroll memory UNVERIFIED. |
| Entry and caller Back | Compliant: device from Devices/Dashboard/alerts/nested links → history → exact requested contents; relative Back returns to that history and its device caller. Contents has no child route. History comparison selection, IDs and older-first diff remain unchanged; #464 owns diff Share/presentation next. Native retained child/tab returns UNVERIFIED. |
| Access, matching, permissions, storage and jobs | Compliant inspected unchanged paths: caller membership/revision guards, connection matching, source loading, token/keychain, TLS consent, cache cleanup, persistence and export filename/bytes retained. No new copy/edit/backup-now/job capability. These absent actions are N/A. |

### Shared owners and limits

Audited all three PushedListLayout callers: history #462, contents #463 and diff
#464. #527 already supplies geometry, native inset suppression on handler/load,
keyboard frame observers, active-only placement and owner-safe actual-caller
Back. This pass uses SetActions without editing the helper or its consumers.
History's guarded EnsureLoadedAsync retains connection revision/source checks;
contents continues loading only its supplied request, not on every activation.
Diff retains its liked presentation and remains #464's action owner.

The source geometry policy is established, but actual native page/Body/card/list
bounds and resulting alignment are UNVERIFIED. Measure each level and pill in
the same coordinate system, including the final viewport end and last line;
footer reach alone proves no alignment. No fixed contents action is reserved.
Keyboard clearance takes precedence and dismissal must restore the measured base.

Android is explicitly affected by inherited #527: SafeAreaEdges.All replaces
Container, adding native keyboard resizing; no-pill Body bottom is 12pt.
Old iOS fallback also uses 12pt plus safe-bottom padding and a 20pt footer.
These are changed behaviours needing physical verification, not unchanged
platform claims. iOS manual keyboard ownership remains inherited unchanged.
Its static owner set can retain an owner if teardown never delivers
Disappearing; that lifecycle limitation is recorded, not claimed fixed here.
Thirteen accepted setup pages lack IActivePage forwarding, so retained activation
gaps remain; the shared Acquire/Release handover does not prove those gaps fixed.
The fourteen setup consumers and sign-in named in #462 remain affected by the
shared manual-keyboard policy. Their accepted geometry/save contracts are not
reimplemented or reopened by this contents-only change.

DESIGN already records pushed viewport ownership, glass actions, command-state
caching and native-drag search. No missing reusable rule was found, so no
duplicate rule or stale historical acceptance is added there.

### Current validation and required physical evidence

Full diff versus current main and DESIGN, subscription pairing, preprocessor
boundaries and staged whitespace inspected. No agents, local build, restore,
tests or scripts run by explicit instruction. Existing source regression cases
cover exact raw bytes after search, invalid UTF-8/control text, empty versus
missing content and superseded loads; they were read, not executed. Android/iOS
compilation and shared-test CI are PENDING at the final pushed head. Independent
full-diff Opus 5.5 Medium review belongs to the parent and is PENDING.

Every physical matrix cell below is **UNVERIFIED** for the final head SHA
reported in the PR handoff. Build and OS are **unavailable**. For each cell,
record exact SHA, build, OS, device, theme, text size and measured outcome.

| Matrix | Required result and evidence |
| --- | --- |
| Devices/orientations | Duo outer portrait/landscape, inner portrait/landscape and ordinary iPhone portrait/landscape; side/bottom/top pill, Android and old-iOS fallback. Measure native host/card/list/pill base delta and final complete line. |
| Presentation | Light/dark, maximum text, long identity/large line numbers, initial text below 20pt fade, no clipping, VoiceOver/TalkBack names and line identity once, named 44pt fallback actions, Reduce Motion and overflow selection/cancellation. |
| Transitions | Fold/unfold, rotate while loading/searching/final-line scrolling; tab return, actual child return to history, retained More, background/resume; one glass owner, correct caller Back and no placement loop. |
| Search/keyboard | Header/glass/Command-F open and repeated refocus; type/filter with no matches and restore query; programmatic scrolling retains focus; native drag, Search completion and unfocus dismiss without clearing. Keyboard show/dismiss/rotate restores base, fixed header and final line reach. Floating keyboard remains unverified. |
| Source/failure/share | Filtered and unfiltered complete-byte export including BOM, mixed LF/CRLF, encoding/invalid UTF-8, NUL, tabs/spaces and trailing blanks. Empty, null/missing, binary, invalid payload, offline/timeout, 401/403 and Retry. Share double tap, busy disappearance, failure and cancellation; no stale source or load Retry for share errors. Copy is N/A, absent. |
| Scale/access | 100,000 synthetic lines and a 20,000-character wrapped line; first/final line reachable, scrolling/search memory and responsiveness. Verify access-controlled errors stay errors and no content enters diagnostics. |
| Shared regression | History selection 0/1/2, third replacement, Cancel and exact older-first diff; diff final tinted line and full-source Share under #464. Accepted setup/backups lifecycle in both orders, retained Settings/Unimus and session-expiry sign-in keyboard handover. |

CI cannot establish native cause, visual correctness or scoped acceptance.
Issue #463 and its epic box remain open. No merge, watcher, release or acceptance
is performed by this implementation agent.
