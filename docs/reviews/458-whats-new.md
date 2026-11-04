# What's new assessment

Issue #458, epic #442. Corrective baseline is main `9e616fd`, including merged
Settings #524 and documentation #515. Read live issue bodies and all comments,
the cached original #458, CLAUDE.md, DEVELOPING.md, DESIGN.md and
DESIGN-REFERENCES.md. Inspected merged #500 and #515 and existing related
branches before editing this clean worktree. The unrelated #475 worktree is
untouched. This assessment supersedes the previous current-status tables.

## Current findings

| Concern and location | Trigger/mode and observed baseline | Expected result and implemented outcome | Status |
| --- | --- | --- | --- |
| Heading, WhatsNewPage.xaml and WhatsNewViewModel | Both entry routes repeat What's new in the body | One PageHeader title, followed by Version and the parsed version verbatim. No heading stripping or new parser. Optional release date retained. | Corrected in code; device unverified |
| Got it, WhatsNewPage.Place | Side pill: scrolling bottom clearance plus 16pt lifts the button | Reuse unchanged SetupActionPlacement.BottomGap with native Root bounds, PillIn and Occupied in the same coordinate space. Measured side gap is max(0, height minus pill bottom), with no safe-area clamp or added bottom inset. Bottom pill/actions and top pill use their separate shared rules. | Corrected in code; device unverified |
| Viewport and full release link, Root/Scroller/Body | Long notes, narrow pane, wrapping action | Auto/star/Auto reserves actual action height; finite scroll viewport ends above it. Root and GotIt size changes repeat placement. Final link remains inside scrolling content above fixed action. | Compliant in code; device unverified |
| Insets/background/fade, page and Place | iOS versus Android, resizing | iOS native top only; Android native safe areas. Body owns side gutters once; action owns its measured bottom gap once. Full pane background retained. Background-matched input-transparent 20pt fade has 20pt content reserve. No readable-width cap. | Compliant in code; device unverified |
| Groups, icons and complete notes | Long paragraphs/URLs, absent or unknown icon/date | Preserve ordered parsed sections/items and complete text with character wrapping. Unknown glyph stays absent; decorative glyph excluded from accessibility tree. Existing themed secondary Glyph stroke, including light fix, unchanged. Source text stays inert. | Compliant in code; device unverified |
| Source unavailable, WhatsNew/VM | Missing/invalid embedded notes | Existing visible unavailable message and fixed GitHub link retained for direct page construction; automatic offer and Settings entry require parsed notes. Version falls back to CurrentVersion. No loading/error fetch state exists. | Compliant; normal unavailable entry N/A |
| Actual caller Back, page/Header/AppShell | Automatic offer or Settings push, child/browser return, retained More, resume | Existing relative route, caller label, iOS 26 glass and older iOS/Android header fallback retained. IActivePage activation remains centrally resolved by AppShell at delivery time. | Compliant in code; device unverified |
| Lifecycle, Activate/OnDisappearing | Repeated activation, queued placement after departure | Idempotent edge watcher and event subscriptions; paired removal. Queued Place checks visibility. Existing queued Got it departure guard and Back completion guard retained. No Window watcher or native notifications added. | Compliant in code; device unverified |
| Seen version, WhatsNew and VM | Fresh install/update/legacy upgrade, Back, Got it, save/pop failure, rapid taps | Existing fresh-install initialisation retained. Only Got it marks seen during notes interaction. It latches before save, stores CurrentVersion before pop, and permits retry after exception. Back/browser never mark seen. Cancellation stays due next launch; process offer latch unchanged. | Compliant; concurrency on devices unverified |
| Keyboard | Read-only notes, no focusable editor/search | No page keyboard handling needed. Recompute on central resume and size changes after browser return. | N/A; browser return device unverified |
| Permissions/security | Either entry or release link | No credentials, permissions, routes, schema, export, source, telemetry or network-fetch changes. Only fixed HTTPS GitHub releases URI opens through ILauncherService/SystemPreferred browser. | Compliant |
| Accessibility/themes | Largest text, narrow screen, screen reader | One semantic page heading, section Level2 headings, wrapping version/notes/action, minimum 44pt link and Got it, caller Back label. | Code compliant; contrast, focus and target operation unverified |

## Shared ownership and preserved contracts

SetupActionPlacement is owned by the shared setup implementation. Its production
consumer before this change is SetupPageLayout; WhatsNewPage now also consumes
it. The policy and SetupPageLayout are unchanged, so accepted #405 forms keep
all their existing contracts. No shared-control edit requires coordination.
SetupPageLayout was inspected but not adopted: its unguarded glass Back,
keyboard-manager disconnect and native keyboard/resume observers would change
this page's completion guard and central activation contract.

The embedded source is app-owned, not fetched from a server. No source copy is
changed or dropped. Core WhatsNewNotes reads the first version heading, optional
release text, ordered sections and complete continued bullets. The VM uses
Notes.Version directly with CurrentVersion fallback, without assuming numeric
version syntax. Sibling Core source was inspected; exact pinned binary behaviour
remains for CI. App version, Core pin, Glyph and notes source remain unchanged.
AppShell continues to offer diagnostics consent before notes. Settings can reopen
seen notes. A transient VM retains its existing completion latch and failure
behaviour. No new dismiss action or visual feature is introduced.

DESIGN #515's bold fixed-action requirement remains intact. This PR adds only
the related usable-space and pushed glass-return requirements, not another
copy of #515 or a broad documentation sweep.

## Validation and historical provenance

Current code self-check covers the complete intended diff against main, event
pairing, coordinate spaces, inset ownership and binding consumers. The existing
VM assertion is updated for the version label. No new tests, builds, test runs,
scripts, agents or device checks were performed, as instructed. Whitespace and
staged diff inspection are recorded in the PR. Android/iOS compilation, shared
CI and security CI are pending. Independent full-diff Opus 5.5 Medium review is
pending parent coordination; no current independent approval is claimed.

Historical #500 assessment recorded an Opus review at `d50f767` against
`3ac10df`, with completion/placement remediation and passing unit tests at its
reviewed head. That is provenance only. The 4 November user report rejected
heading duplication and action alignment; earlier review and CI do not verify
this corrective head. #515 is merged, as is #524. No merge, watch, release,
issue closure or epic tick is requested here.

## Pending device matrix

All device outcomes below are **UNVERIFIED** for the exact corrective commit
listed in the PR. Build number and OS versions are not available. For each row,
record exact head SHA, build, device model, OS version, theme, text size,
screen-reader state, observed result and expected-result comparison. Do not
carry results across SHAs or infer them from CI.

Run every applicable row on Duo outer portrait and landscape, inner portrait
and landscape, ordinary iPhone portrait and landscape, plus Android fallback.
Repeat light/dark, default/largest text, VoiceOver/TalkBack. Include iOS 26+
glass and older iOS fallback where available; unavailable OS/device combinations
remain explicitly unverified.

| Trigger/mode | Expected result | Current evidence |
| --- | --- | --- |
| Fresh install then sign-in | Consent order retained; no automatic notes; fresh version initialised | UNVERIFIED |
| Saved-session update; legacy upgrade without seen version | Consent first when due, then one automatic offer; no overlay over consent | UNVERIFIED |
| Settings entry before/after seen | Same full notes/version; caller Back returns to Settings; no seen write on opening | UNVERIFIED |
| Header/system/hardware/glass Back and iOS edge swipe | Actual caller restored once; no seen write; process latch retained, due next launch | UNVERIFIED |
| Got it and relaunch; rapid competing Got it/Back; save or navigation failure | Single completion; stored version before return; existing failure/retry semantics retained | UNVERIFIED |
| Fold/unfold and rotate while scrolled to final link | Side action base matches pill exactly; bottom actions clear glass; top pill distinct; finite viewport and final link reachable | UNVERIFIED |
| Retained-tab away/return; browser child return; background/resume | One glass owner, correct caller Back and measured action placement; scroll state retained; no placement loop | UNVERIFIED |
| One page title and version, arbitrary parsed version, absent date/icon, unknown icon | No duplicate source heading; no version truncation or notes loss; silent decorative icon | UNVERIFIED |
| Long notes/continued paragraphs/URL-looking text, wrapped action | Complete inert text wraps, full release link scrolls clear of fixed action, 20pt fade reserve readable | UNVERIFIED |
| Missing/invalid notes via direct construction | Visible unavailable text, app-version fallback and working fixed release link | UNVERIFIED; normal production entry N/A |
| Screen-reader traversal, both themes and largest text | Logical heading order, named operable Back/link/Got it, adequate targets and contrast | UNVERIFIED |
| Keyboard dismissal | No notes editor or keyboard exists; repeat resize and browser return checks above | N/A |

Inspect diagnostics for repeated placement/paused loops after transitions. User
acceptance remains pending. #458 and its #442 checkbox must remain open.
