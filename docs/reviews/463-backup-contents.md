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
| Back, native insets, glass | Route semantics compliant; subscription lifetime fix required | Actual previous caller unchanged, Never on HandlerChanged/Loaded retained; watcher disposed, retained Window unsubscribed, idempotent Show/Hide and resume reactivation |
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
and retained Window resume lifecycle here. The duplicate guard/footer intent
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
