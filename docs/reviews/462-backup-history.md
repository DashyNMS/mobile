# Backup history assessment

Baseline: main `83e9a39`. Live #462, #442, #463, #464, completed #409 and
merged PR #434 read on 3 November 2026. Read CLAUDE.md, DEVELOPING.md,
the full DESIGN.md and DESIGN-REFERENCES.md. The explicit user direction
authorises routine presentation choices and requires this PR to stay unmerged.

## Scope and contracts

Device detail opens the relative `unimusbackups` route with LibreNMS device ID
and display name. Device detail itself can come from Devices, alerts, dashboard
or a nested device link. History opens relative `unimusbackup` with the exact
Unimus device and backup IDs, or `unimusdiff` with the two selected backup
objects ordered by validSince. Back returns through that stack to the actual
caller. Child pages keep their own content/search/share contracts under #463
and #464. History has no search, setup form, content renderer or share action.

History reads the first 50 backups, orders newest first and calls the newest
dated revision Current. It does not infer success or job health from this label.
The count describes loaded rows, not the server total. Missing dates stay
unknown. Unimus epoch seconds use the phone's local timezone independently of
LibreNMS's server timestamp preference. History now includes seconds and the
numeric timezone offset without changing the shared contents/diff formatter.

Compare selects at most two revisions. A third replaces the earliest choice;
Cancel clears selection. Successful comparison navigation clears compare mode,
as before. Refresh retains selections whose exact backup IDs remain on the same
matched device; failure retains the last good rows and selections. View rejects
obsolete row objects and both actions reject a changed connection revision.
Refresh cannot replace rows during an active navigation command.

The pinned Core package is `2.0.0-ci.52`. Core development-build run 52 names
source `5f7bde53ac05ea75f7cb6dd06a8fae62606f0769`, matching the inspected local
Core checkout. Verified UnimusBackup, IUnimusApi, address candidates and matching
against that source. No restore, build, test or script was run.
Core exposes TriggerBackupAsync, but mobile history has no write command or job
UI. This PR adds none. There is no permission confirmation or job persistence
to migrate. HTTP 401/403 stays an error, never an empty successful result.

Settings #409/PR #434 remain the accepted baseline. The keychain token, URL,
certificate trust, TLS consent, settings drafts and Save are unchanged.
Matching retains Core's candidate order, case-insensitive address/description
comparison, domain handling and cached all-zone list. A duplicate winning
candidate now refuses to choose an arbitrary device. Settings changes invalidate
in-flight matching/cache completion and history action identity through a
connection revision. Ordinary sign-out keeps the independent Unimus settings;
forget-everything clears them using the existing settings flow.
No configuration content is logged or automatically shared. History never
decodes or mutates source bytes. Existing explicit child share actions remain.

## Findings and outcomes

| Concern and trigger | Baseline assessment | Result |
| --- | --- | --- |
| Long device name, same-time revisions | Fix required: history omitted device identity and backup IDs | Wrapping device heading, both source IDs, backup IDs, exact local timestamps |
| Timestamp/status | Fix required: minute precision and unknown formats labelled binary | Seconds/offset; unknown date/format text; Current remains a version label |
| Duplicate name/address match across zones | Fix required: Core returned the first matching device | Refuse ambiguity at the winning priority, with recovery advice |
| Unconfigured integration versus no matching device | Fix required: both became no match | Separate unavailable error; genuine unmatched and matched-empty explanations retained |
| Initial load, populated, matched-empty, unmatched | Compliant data paths; native appearance unverified | Existing BusyCard and pull spinner suppression retained; truthful loaded count |
| Failed refresh or denied request | Fix required: API errors erased rows, generic failures could change identity before completion | Publish rows and matched ID together; retain last good history and selection; Retry |
| View and compare | Fix required: stale row commands accepted | Membership, busy and connection revision guards; exact IDs and older-first comparison preserved |
| Compare selection | Fix required: visual selection depended on colour | Whole row minimum 44pt, spoken joined facts and selected state, visible selection text, duplicate child announcements excluded |
| Native insets and glass | Baseline retained; repeated Show could duplicate Moved subscription | Idempotent Show/Hide; native collection Never on HandlerChanged/Loaded, top padding 0, one side/bottom clearance owner |
| Footer and fade | Fix required: fade used outer background over card | Fade uses actual themed card surface; shared footer reserves 20pt plus measured glass clearance |
| Search and keyboard | Compliant, not present in history | Child search stays with #463/#464; setup search unchanged |
| Writes, jobs and permission confirmations | Compliant, no existing mobile history write command | No invented action, capability or permission check |
| Privacy and persistence | Compliant in inspected paths | No settings/security persistence changes, content logging or automatic sharing |

## Dependencies and consumer audit

PushedListLayout has exactly three consumers, history, contents and diff.
The shared change only makes activation idempotent and adjusts footer reserve
from 24 to 20pt. It retains actual-caller Back, owner-aware Hide, native inset
suppression and measured compare-button clearance. #463/#464 must retain this
baseline and verify their final rows after rotation/resume. No shared content
renderer is introduced or owned by history. Any renderer work must be coordinated
between #463 and #464. UnimusSetup.FindAsync has only the history caller; device
detail uses IsConfigured. Settings APIs remain unchanged. Retained More activation
from #490 is unchanged; native window disposal/resume is unverified.

## Validation and remaining evidence

Self-inspection covers the full diff and whitespace check. Added regression
tests cover ambiguous matching, failure retaining rows/selection/exact navigation
IDs, and unknown date/format presentation. Existing tests cover candidate order,
unmatched/denied results, older-first comparison, third-choice replacement,
synthetic content/share and binary content. Tests were not run locally by user
instruction. CI must compile XAML on Android/iOS and run shared tests. Independent
Claude Opus 5.5 Medium review is pending the parent's sequential queue after
PR #503. This implementation agent starts no review, watcher or merge.

Every device cell below is unverified at this implementation commit. Record the
tested commit and build separately from CI before claiming acceptance.

| Scenario | Exact checks still required |
| --- | --- |
| Matching | Hostname, short name, configured domain, IP fallback, missing device, duplicate across zones, changed settings during load |
| Empty/error/job | Matched-empty, unavailable setup, offline, timeout, 401, 403, failed initial load, failed refresh retaining history, Retry; confirm no backup-write UI |
| Selection/view/diff | Open exact revision, zero/one/two selections, third replacement, cancel, older-first diff, Back from both children, refreshed/disappeared revisions, double taps |
| Accessibility | VoiceOver and TalkBack reach and activate every row/action, announce identity and selected state once, 44pt targets, maximum text size without clipping |
| Themes | Light/dark contrast, status understandable without colour, fade matches card, final row clears glass with and without Compare |
| Duo | Outer portrait, inner portrait, inner landscape, fold/unfold, rotate while selecting/loading, background/resume, retained More activation, nested device Back |
| iPhone | Ordinary iPhone portrait/landscape, supported glass and older header Back where available, child return, rotation and resume with one glass owner |

No device acceptance, issue closure, epic tick, release or TestFlight is claimed.
Parent updates only this PR at its turn after preceding PRs merge, then owns
Claude review, CI, merge and cleanup. TestFlight follows the #460 to #464 batch.

## Independent review remediation

Claude Opus 5.5 Medium reviewed full diff 1d694c2 against main e817e62.
Revision-discarded and cancelled loads are no longer marked checked; a changed
revision triggers another load after busy clears. Replaced connections clear
history, matched identity and Compare selection before loading; same-revision
refresh failures retain good rows. Regressions cover revision changes during
lookup and revoked-connection history clearing. History count pluralisation,
initialisation notifications and conditional retained-history caption are fixed.
The list fade and matching header spacer are 20pt.

PushedListLayout now exposes idempotent Activate, always reapplies controls and
deduplicates Moved. History implements IActivePage; contents and diff adopt it
under #463/#464. All three consumers retain inset/footer contracts. Refresh no
longer forgets device cache while busy. Replacement CI and fresh Claude review
remain pending. No local builds/tests ran; all device results are unverified.

Claude re-reviewed de51335 and confirmed all prior medium findings and tests.
The remaining fade is moved inside the card with a 1pt stroke inset, and IsBusy
changes immediately notify IsEmpty. Activation after a failed first load is an
intentional automatic retry. Unit tests passed at the re-reviewed head;
replacement CI remains pending. Native rendering and timeout behaviour remain
device checks, with no acceptance claimed.
