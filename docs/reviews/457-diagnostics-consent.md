# Diagnostics consent assessment — #457

Baseline: prepared main `83e9a39`; epic #442; read live #454/#455/#458,
CLAUDE.md, DEVELOPING.md, DESIGN.md and DESIGN-REFERENCES.md, PRIVACY.md,
consent service, reporter, AppShell, page/model, Settings callers and tests.

## Baseline before fixes

| Concern | Assessment | Evidence / intended resolution |
| --- | --- | --- |
| Explanations and scope | Compliant | Page describes independent error/usage choices, excluded data, connection IP and privacy link. Preserve wording. |
| Defaults and persistence | Compliant | Model starts false/false; switches draft only. Either completion calls Choose then relative Back. No inferred consent. |
| Action hierarchy and huge text | Fix required | Two equal-width fixed buttons crowd labels; fixed action height can consume short viewport. Stack full-width actions in finite scrolling body. |
| Accessible choice names | Fix required | Switches lack descriptions tying them to visible explanations. Add explicit names/hints. |
| Fast competing completion | Fix required | Separate async commands can each persist and pop while the other awaits navigation. Share a completion latch and command enablement. |
| Back/cancel | Compliant | Header relative Back records nothing; offered once per session, asks next launch. Preserve. |
| Insets and glass | Fix required | Native container plus ordinary margins does not account for side/bottom glass or reclaim Back on retained activation/resume. Page-local scrolling placement; no shared helper edits. |
| Contrast, screen-reader order, native layout | Unverified | Requires both platform CI and device matrix below. |
| Settings summary and later revocation | Compliant | Settings section, not this notice, is later entry; immediate independent switches and SharingSummary observe consent changes. PR #496 owns overview/model; read-only diff inspected. |
| Sign-in and release sequence | Compliant | AppShell offers consent only connected on Main, then What's new after return. PR #497 owns sign-in; read-only diff inspected. #458 owns notes body. |

## Modes, routes and security contracts

Initial notice is a relative Shell route on logged-in Main after sign-in or
session restore, not a logged-out sign-in modal. Returning goes to the actual
caller. Privacy policy opens externally and returns without committing.
Later entry is More/pinned Settings -> Diagnostics section -> Back; it does
not reopen this draft notice. No Save is introduced for immediate Settings.
A build without reporting configuration neither offers consent nor shares.
Older notice versions disable sharing until answered. Phone preferences
diagnostics.errors/usage/notice are separate from Core desktop consent.

The only commit points remain Allow selected (requires a selection) and Don't
share (false/false, even with draft switches on). Back leaves storage untouched.
Collectors gate on consent; revocation purges pending reports and abandons sends.
Reporter queues at most ten 32 KiB reports for seven days; PRIVACY.md separately
states Sentry retention at most 90 days. No retention copy is added to the page.
No changes to redaction, app identifiers, server names, TLS data, reporting SDK,
sharing configuration, storage keys, notice version, Core API or global policy.
User-initiated diagnostics file sharing remains separate: may contain names,
has its own warning/redaction and explicit platform share action.

## Dependencies and evidence

Queue remains #489 -> #491 -> #492 -> #493 -> #494 Map -> #495 -> #496 ->
#497 -> #498 -> this PR. No queued branches edited. Settings summary ownership
stays #454/#496; sign-in stays #455/#497; notes body stays #458.
Core is pinned to 2.0.0-ci.52; these changes require no new Core API.

Implementation self-inspection, CI, independent review and device acceptance
are separate gates. Local builds/tests/scripts are prohibited for this task.
Claude Opus 5.5 Medium review is pending quota reset and the sequential parent
queue; no child attempt. No merge, watch, release, closure or epic ticks.
TestFlight follows the #455–#459 batch. CI and device results remain unverified.

## Implementation outcome

Confirmed findings above addressed in this PR: full-width scrolling choices,
explicit draft/Back explanation, switch names/hints, shared completion latch,
header/system/glass Back guards while completing, and page-local glass/inset
ownership. Allow retains the primary style; rejection has the same minimum
target and inherited themed disabled states, appears first and requires no
selection. No fade or keyboard observer is needed. SetupPageLayout is not
adopted: there is no draft form Save and both mutually exclusive consent
actions must remain reachable on a short viewport. CoveredEdges subscription
is disposed; Moved/handler/Loaded and the retained Window resume subscriptions
are removed on disappearance. AppShell #490 IActivePage restores retained entry.

Full intended diff self-inspected and git diff --check passed. Added unexecuted
tests for draft-only switches and both orders of competing completion commands.
Existing tests cover exact selections, decline, unavailable build, old notice,
later revocation and Settings summary notification. No local checks were run
beyond Git inspection/whitespace checking; native compilation and gesture races
still require CI/device evidence. All scoped device rows remain unverified.

## Exact pending device matrix

Record commit/build, OS/device, theme, text size and screen reader for each.
Repeat every route in light/dark, default/largest text, VoiceOver/TalkBack,
Duo outer portrait / inner portrait / inner landscape and ordinary iPhone
portrait/landscape. Fold/unfold, rotate scrolled and resume on each page.

1. Fresh consent: both OFF; Allow disabled; error-only, usage-only, both ->
   Allow; relaunch retains exactly selected values, no prompt repeated.
2. Draft both ON -> Don't share: both persisted OFF. Rapid competing taps:
   one write and one pop. Back during navigation: no second completion.
3. Header/glass/system Back before choice: no write; no repeat this launch,
   prompt next launch. Open privacy policy, return: drafts retained, no commit.
4. Later Settings entry: each toggle applies immediately; summary updates;
   disable each and confirm pending report deletion with synthetic data.
   Back/relaunch preserves choices. Retained More stack/tab return and resume
   restore actual caller Back once, no placement loop or duplicate observer.
5. Diagnostics-disabled build: no prompt or sharing controls, no telemetry.
6. Scroll at largest text: all explanations and final choice reachable; native
   focus order title -> text -> named switches -> privacy -> choices; labels
   wrap, buttons at least 44pt, enabled/disabled contrast in both themes.
   One inset owner per edge, no content above visible Back, no fade overlap.
7. Saved-session upgrade with both notices due: consent first, then What's
   new after Allow/decline/Back; fresh install no What's new. Verify notes seen
   version unchanged and no early telemetry before explicit consent.
