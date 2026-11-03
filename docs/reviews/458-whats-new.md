# What's new assessment

Issue #458, epic #442. Baseline main 83e9a3983b53f3a93931b46347ae8325baa8076b.
Read live #458/#442/#457/#454 and read-only diffs for #496 Settings and
#499 consent. No queued branch changed. Guidance: CLAUDE.md,
docs/DEVELOPING.md, docs/DESIGN.md and DESIGN-REFERENCES.md. No FULLDESIGN.md
exists in this checkout; DESIGN.md is the current page specification.

## Baseline and contracts

WhatsNewPage binds WhatsNewViewModel to WhatsNew, which parses the embedded
WhatsNew.md synchronously with Core WhatsNewNotes. Section and item order,
full parsed paragraphs, optional release date and allowlisted decorative icons
are retained. Unknown icons draw nothing. Glyph retains its secondary themed
stroke, including the existing light-theme fix. No release copy is changed.
Core is pinned to 2.0.0-ci.52. Existing Parse/IsDue/MarkSeenIfFreshInstall APIs
are unchanged; sibling Core source was inspected for parser semantics, but the
pinned package is absent locally, so exact binary verification awaits CI.

App startup calls MarkSeenIfFreshInstall before sign-in saves a server.
No saved server and no recorded seen version means fresh install, recorded
without prompting. Saved server with no seen version is an old upgrade and
remains due. Preview versions and mismatched embedded versions are not due.
AppShell OfferAsync on connected Main offers diagnostics consent first and
only offers notes when consent returns false. Returning from consent permits
notes. This sequencing is unchanged, including #499's choice latch.

Both automatic post-update entry and Settings About OpenWhatsNew use the
relative Routes.WhatsNew Shell route. This is a pushed page, not a modal.
Header/system/glass Back returns to its actual caller and does not mark seen.
Got it marks CurrentVersion in WhatsNewShownVersion, saves, then returns using
Routes.Back. Preserve this existing commit order, including storage before
navigation. Navigation failure permits retry, with the version already stored.
Opening notes or returning from the browser never marks seen. An offer is
latched once per process before navigation; cancellation remains due next
launch, and Settings can reopen any time. A new page/view model is transient.
No new persistence keys, version policy or consent behaviour.

Only the hard-coded HTTPS github.com/DashyNMS/mobile/releases URI is launched
through ILauncherService, whose native adapter uses Browser SystemPreferred.
This is the existing system browser presentation, not a privileged app WebView.
Embedded row text is plain Label text. No markdown/HTML renderer or arbitrary
URL activation is introduced; URL-looking content wraps as inert text. No
server data, credentials, extra network fetching or telemetry is added.

## Scope assessment and fixes

| Concern | Baseline result | Implementation outcome |
| --- | --- | --- |
| Automatic and Settings routes | Compliant | Relative push and caller Back retained; #496 keeps Settings ownership |
| Seen-version and consent order | Compliant | Service, AppShell, startup, storage and release content unchanged |
| Release headings and sections | Fix required | Semantic release/section headings, complete ordered content retained |
| Icons and themes | Compliant in code | Existing Glyph strokes retained; decorative instance excluded from accessibility tree; device contrast unverified |
| Long notes and links | Fix required | Removed ReadableColumn cap; character wrapping permits long URLs/paragraphs; browser action full width and minimum 44pt |
| Scroll and final dismissal | Fix required | Finite star viewport; single primary Got it remains in a fixed Auto row outside the scrolling viewport, minimum 44pt and wrapping; footer clears glass |
| Insets and fade | Fix required | Native top only on iOS; page owns content sides/bottom once, Android native safe areas; 20pt input-transparent background fade matches content width and has 20pt reserved space |
| Header/glass ownership | Fix required | Shared PageHeader/TopBar line, caller Back in glass on supported iOS; IActivePage restores retained More entry/resume |
| Lifecycle | Fix required | Idempotent edge/handler/Loaded/Moved subscriptions, retained Window unsubscribed on disappearance; no native observers added |
| Empty/parse failure | Fix required | Parser-null groups show truthful unavailable text and fixed release-notes destination; synchronous immutable data has no loading, refresh or stale state |
| Completion race | Fix required | Got it latches before save/pop, disables header/system/glass Back during completion; retry permitted on exception |
| Layout and accessibility on devices | Unverified | Exact matrix below remains required |

No shared controls changed. Accepted setup, Settings child forms and other
Glyph/PageHeader consumers keep their implementations. Reused CoveredEdges,
TopBar, PageHeader, TabBarButtons and IActivePage. #490 retained-stack activation
is part of this baseline. Native scroll adjustment is disabled on handler,
Loaded and placement. The fade uses BackgroundLight/Dark and their clear
variants, matching the page background rather than the card surface.

## Evidence and dependencies

Implementation full-diff self-inspection and git diff --check are the only
local validation. Regression tests added for competing completion, cancelled
update/reentry, absent/invalid notes, long continued paragraphs and inert markup.
Existing tests cover fresh install, legacy upgrade, once-per-version, preview,
Settings reentry, glyph mapping and actual embedded version. Tests are not run.
No local builds, tests, scripts, independent reviews or device checks performed.
Native XAML/API compilation, shared tests and Android/iOS CI remain pending.

Mandatory independent Claude Opus 5.5 Medium review remains pending quota return,
sequentially after #489 -> #491 -> #492 -> #493 -> #494 Map -> #495 -> #496 ->
#497 -> #498 -> #499 -> this PR. Parent updates only this PR at its turn after
prior merges, then handles review/CI/merge/cleanup. No merge, watch, release,
closure or epic ticks here. TestFlight follows #455–#459. User device acceptance
remains a separate gate; this assessment makes no acceptance claim.

## Exact pending device matrix

For every row record commit/build, device/OS, theme, text size, screen reader
and result. Repeat in light/dark and default/largest text on Duo outer portrait,
inner portrait, inner landscape and ordinary iPhone portrait/landscape. Use
TalkBack on Android and VoiceOver on iOS. Fold/unfold, rotate while scrolled,
switch tabs away/back and background/resume; inspect diagnostics for loops.

| Scenario | Required result | Evidence |
| --- | --- | --- |
| Fresh install then sign-in | Consent first; no automatic notes; fresh version recorded | Unverified |
| Saved-session update and legacy no-seen upgrade | Consent when due, then one notes offer; no offer over consent | Unverified |
| Got it then relaunch | Exactly one save/pop; same version never offered again | Unverified |
| Back without Got it | Actual caller restored, no seen write; no repeat this process, due next launch | Unverified |
| Settings reopen, including retained More | Notes reopen despite seen version; Back returns Settings once, one glass owner | Unverified |
| Browser link and return | Fixed HTTPS GitHub browser destination, no seen write; text/scroll state retained | Unverified |
| Rapid Got it and Back/system gesture | No duplicate save/pop; caller usable; navigation failure retry does not invent unseen state | Unverified |
| Long synthetic notes/URL, no icon/date, unavailable notes | Complete wrapping content, truthful empty state, final Got it reachable, no clipping | Unverified |
| Heading and icon traversal | Correct section order/headings, decorative glyphs silent, both themes legible | Unverified |
| VoiceOver/TalkBack actions | Named Back, browser hint and Got it operable, minimum 44pt targets, logical focus | Unverified |
| Resize/fold/rotate/resume at final item | Finite viewport, 20pt fade reserved on matching surface, no floating Back or overlap, final action clears glass | Unverified |

No screenshots or customer data collected. All added fixtures are synthetic.

## Claude review remediation

Full-diff Claude Opus 5.5 Medium review at d50f767 against main 3ac10df found
no blockers. Its two medium findings are fixed: AppShell alone owns resume
activation, and Got it is restored to the fixed action slot with measured side
and bottom clearance. The scroll viewport ends above it. The page ignores queued
Got it taps after disappearance; the command still owns its completion latch.
Repeated TopBar.Fit is removed because PageHeader already installs it once.
The obsolete legacy iOS safe-area flag is removed in favour of explicit edges.

Unit tests passed at reviewed head; replacement platform CI remains pending.
No local builds/tests ran. NotesUnavailable is defensive for direct construction;
normal production entry requires parsed notes and does not expose an empty state.
Device checks must include rapid Got it with header/system/glass Back and iOS
edge swipe, including a swipe during completion. Gesture-transition ordering
cannot be established by code inspection; all hardware outcomes are unverified.
