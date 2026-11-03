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
