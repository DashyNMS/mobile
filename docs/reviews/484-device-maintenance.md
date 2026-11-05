# Device maintenance assessment, #484

Reviewed 5 November 2026 against main `256307a98aeb8f459df8dd508df9535431e6a23c`.
Implementation identity is the commit introducing this assessment, available through
`git log -1 --format=%H -- docs/reviews/484-device-maintenance.md` on the PR branch.
No native build or device evidence was supplied. Build number: unavailable.
Refs #484 and #442. Neither issue is accepted or ready to close.

C means source-compliant, F means a confirmed source defect addressed here,
and U means unverified. C never asserts native acceptance. H/B/V below mean
header, body viewport and fixed primary action bounds respectively.

## Baseline and ownership

Read live #484, #442 and #137 bodies and comments, CLAUDE.md,
docs/DEVELOPING.md, docs/DESIGN.md and DESIGN-REFERENCES.md. All three issues
had no comments. PR #370 supplied the full-screen baseline, not acceptance.
The live open-PR list was empty at assessment. #405 acceptance and its build
220 record remain intact; they do not establish maintenance acceptance.

Read MaintenancePage XAML/code, MaintenanceViewModel, ViewModelBase,
DeviceDetailViewModel, DeviceDetailPage and embedded DeviceDetailView callers,
DevicesViewModel bulk caller, AppShell registration/activation and DI registration.
The only production forward routes to Maintenance are DeviceDetailViewModel
and DevicesViewModel. A device may itself have been opened from a dashboard,
alert, list or detail link; the maintenance route is relative in every case.
Query precedence remains the device tuple list, then single id/name. Missing
single names retain the `device {id}` fallback. Several devices show a count,
not an invented list of names. No query means no eligible devices and no Schedule.

Shared ownership was audited through all SetupPageLayout, SetupActionPlacement
and ManualKeyboardScroll consumers. Setup consumers include Add card, Graph,
Sensor, Spotlight, Map location, Ranked/Top card, Notify rule, Graylog device,
Graylog/Unimus/Sensor definitions/Thresholds/settings sections and Neighbour group
editor. Placement also serves What's new, DeviceSectionPage and PushedListLayout;
manual keyboard ownership also serves Sign in, DeviceSectionPage and pushed lists.
These helpers and all existing consumers are unchanged. Maintenance opts in.

## Findings and disposition

| Concern / source | Trigger and baseline | Expected and disposition |
| --- | --- | --- |
| F: MaintenancePage Schedule | Long form or keyboard; primary action scrolls away | SetupLayout Auto/star/Auto keeps one Schedule outside scrolling content; implicit primary style and zero bottom margin |
| F: page inset/lifetime code | Fold, keyboard, retained return or resume; custom page placement and unretained edge subscription | SetupPageLayout owns measured sides/bottom, native scroll inset suppression, keyboard observer disposal and manual keyboard ownership; IActivePage reclaims Back through Shell |
| F: later date/time and duration rows | Narrow pane or large text; simultaneous pickers and beside-label controls compete for width | Separate SetupSection fields above each native control; existing 800pt section breakpoint retained and recalculated on width/padding changes |
| F: field semantics | Values replace placeholders; switch/pickers and optional text fields lack stable names | Explicit semantic descriptions and persistent visible labels; native activation remains U |
| F: submit error reachability | Schedule pressed with error outside current viewport | Await the existing SaveCommand, scroll to ValidationError and announce its full message |
| C: busy submit | The baseline command binding already disabled Schedule through confirmation and navigation | The new click-handler path binds enabled state to SaveCommand.IsRunning to preserve this behaviour; IsNotBusy alone would not cover confirmation |
| C: cancellation | Draft abandoned | Existing Back and Cancel both returned via `..`; remove the duplicate scrolling Cancel, retain Back and explain discard. iOS 26 has glass Back; Android/older iOS have header Back |
| C: hierarchy/themes | Any form | Existing subject, sections, cards, wrapping presets, theme resources and input-transparent 20pt surface fade retained; body reserves 20pt above its first card |
| C: server content | Confirmation or failure | Existing dialog/error strings remain complete; no truncation, sanitisation or substituted server facts |
| U: native geometry | Smallest displays, maximum text, keyboard/wheels | Source ownership is settled; measure actual H/B/V and final content on devices below |

No new visual direction outside DESIGN was implemented. The existing section
breakpoint and card hierarchy remain. A different hierarchy or a custom picker
would require a concrete HTML proposal and parent-rendered approval first.

## Actual modes and contracts

| Mode / source | Source result and classification |
| --- | --- |
| Single target | C: id/name query, one POST, server confirmation then Back |
| Several targets | C: tuple list query, same request sequentially through BulkRun; partial success summary then Back; zero successes retain form/error |
| Start now | C: default true, Start null; hidden later fields retain their values |
| Start later | C: next quarter-hour initial selection, explicit server wall-clock caption, invariant `yyyy-MM-dd HH:mm:00`, no timezone conversion |
| Duration | C: default 1h, six presets; 0–168 hour stepper, 0/5/.../55 minutes picker, full duration heading; validation rejects negative hours, invalid minutes and zero total |
| Skip / mute / run alerts | C: all three Core enum values and display strings retained, skip default |
| Optional title / notes | C: draft only; blank becomes null and nonblank is trimmed in request; no invented text limits |
| Past start / invalid duration | C: existing validation and one-minute tolerance preserved; no request; F: error now revealed and announced |
| Busy | C: unchanged request execution; F: fixed action disabled for entire command including confirmation; native disabled contrast U |
| Permission refused | C: existing global/per-device permission predicates, reason and post-submit refresh preserved; no Schedule when every target refused; mixed targets still follow existing BulkRun contract |
| Offline / timeout / API error | C: RunAsync preserves draft and exposes existing error text; no successful return; F: error revealed. Retry remains explicit and may schedule again after an ambiguous server response, unchanged |
| Empty / source loading / cached data | C: no read/list/cache source on this form. Empty target means MaySchedule false. No fabricated loading/empty rows or refresh action |
| Search / filtering / pull refresh | C: irrelevant to a bounded local draft; six presets and three behaviours are fixed local choices |
| Back / system Back | C: discard and relative return; no new persistence or credentials; no write on navigation |
| Retained tab / resume / rotation / folding | C: draft stays in existing VM; layout activation never reinitialises or submits; physical focus/scroll preservation U |
| End maintenance | C: absent, still blocked under #137; no list/update/delete window capability added |

MaintenanceViewModel and server-write code are unchanged. No real server writes
were made. A mock-only contract pin covers offline draft retention and another
covers command execution through confirmation before returning. Existing tests
cover request formatting, defaults, duration/past validation, bulk partial success,
permission scope and entry routing. These tests are added/read, not run locally.

## H/B/V bounds and final-content matrix

H is the shared fixed PageHeader. B is the star-row Grid containing FormScroll
and its 20pt fade. V is the Auto-row Schedule button with 12pt top margin and
zero bottom margin, hidden on refusal. The action row reserves actual measured
button height. iOS native page owns top only; SetupPageLayout owns 16pt gutters
plus measured side clearance and bottom placement; native scroll adjustment is
Never. Android changes from Container to All safe areas, enabling native keyboard resizing under the shared layout contract. No local
bottom spacer or guessed keyboard offset remains.

For side pills, verify V.bottom equals pill.bottom in the same native coordinate
space, even inside the native bottom inset. Bottom pills and glass actions must
be below V with the shared gap; a top iPad pill must not lift V. B.bottom must
not overlap V.top including its margin. With docked keyboard, V and B lift/shrink;
after dismissal V must regain pill-base alignment. Floating keyboard, date/time
popovers and native wheels are U, not proved by shared arithmetic tests.

| Field / final content | Required native check | Result |
| --- | --- | --- |
| Start now, date, time | Named targets, date/time format and wheel/popover fully operable inside usable bounds | U |
| Presets, hours, minutes | Wrap, touch targets, stepper extrema, all minute choices, full heading at maximum text | U |
| Skip/mute/run picker | All options readable, stable accessible name, cancel picker retains draft | U |
| Title and growing notes | Focus/caret reachable while keyboard open, long text remains editable | U |
| Error, refusal, discard caption | Final content scrolls wholly into B above V or glass; announcement once on failure | U |
| Schedule | Fully visible/tappable at rest and while keyboard open; disabled throughout submit/confirmation | U |
| Back | One glass owner on iOS 26, correct previous-title speech, header fallback elsewhere | U |

## Evidence and handoff

| Check | State / exact next evidence |
| --- | --- |
| Implementation full-diff review | Source review completed against guide; parent independent Opus review outstanding |
| Whitespace | `git diff --check` and staged equivalent recorded in implementation handoff |
| Shared tests | U: not run locally; CI must run existing suite including new mock contract pins |
| Android / iOS simulator builds | U: no local builds, restore or validation scripts; CI judgement belongs to parent |
| Light / dark | U: capture each theme including disabled Schedule, refusal, errors and native picker |
| Maximum text / VoiceOver / TalkBack | U: verify every target name/value, single activation and readable final content |
| Duo outer, inner portrait, inner landscape | U for each: record native H/B/V bounds, usable width, pill and keyboard geometry on a commit/build |
| Fold / rotate | U: change dimensions with later picker and long notes open, repeat at final scroll position |
| Retained-tab return / resume | U: repeat tab switching and foreground cycles with keyboard open/dismissed; retain draft and one Back owner |
| Ordinary iPhone / iPad | U: include bottom/top pill, wheel/popover and floating keyboard where available |
| Device acceptance | U: no supplied evidence, no CI-inferred acceptance, no issue closure or epic tick |

Device submission checks must use an authorised synthetic test environment.
This task performed no server writes. Parent owns independent full review and CI
judgement. No merge, monitoring, release or acceptance action is part of this PR.


## Independent review corrections

Full-diff Opus review at 0416bdd found no blocking defects and confirmed the
server-write contract is unchanged. Busy-submit behaviour was already compliant;
the tests pin existing view-model contracts rather than reproduce production
defects. The click-handler binding preserves that baseline. The error message
is checked again after scrolling before announcement. The obsolete page-level
safe-area attribute is removed so the helper is the single owner. Android
Container-to-All keyboard resizing is explicitly recorded as a change. Native
first-error scrolling, TalkBack editing, stepper value speech and all placement
checks remain unverified. Back during an in-flight submit is a pre-existing
follow-up risk: the write is not cancelled and confirmation may navigate later.
No accepted deferral or device acceptance is inferred. Current-head CI is pending.
