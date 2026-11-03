# Log entry details, #460

Baseline is main `83e9a39`, including retained list activation and navigation
from #490. Read live #460, #442, #461 and #482, CLAUDE.md, DEVELOPING.md,
DESIGN.md and DESIGN-REFERENCES.md before implementation.

## Scope assessment before fixes

| Concern | Assessment | Evidence and resolution |
| --- | --- | --- |
| Pushed detail | Fix required | LogEntryPage used legacy native safe areas and bottom-only glass clearance. Replace together with top-only page ownership, explicit side/bottom PlaceContent, and native scroll Never on HandlerChanged/Loaded. |
| Embedded detail | Fix required | LogEntryView kept an empty BackBar and proportional fields. Hide the whole embedded bar and use the host's explicit clearance. |
| Timestamp and severity | Compliant | ServerTime.ToLocal uses the existing UTC preference; absent time stays absent. EventStatus maps unknown severity to None, never OK. Preserve existing severity wording. |
| Source | Fix required | Caller device name was shown but raw hostname/sysName absent from details. Add existing Core values unchanged, independently of device identity. |
| Long and multiline message | Fix required | Word wrapping alone and a display heading do not suit arbitrary messages. Use plain-text CharacterWrap, ordinary body text and stacked fields without proportional columns. |
| Related device | Fix required | Two proportional buttons constrained large text. Use a whole 44pt minimum wrapping target with joined device/state description and excluded child label. Rule action is independent of device visibility. |
| Missing metadata | Compliant | Omitted fields are omitted, never invented. Raw Event/Alert objects remain attached. |
| Missing record | Fix required | Invalid parameters left stale content or a blank view. Clear the entry and show return/reselect guidance. |
| Device appearance and accessibility | Unverified | Device matrix below remains pending. |

## Modes, routes and contracts

Network Logs from More or Alerts, device Event log, and Dashboard cards pass
LogEntryItem. Logs also passes its live LogsViewModel for narrowing actions.
ShellNavigationService intercepts detail into LogsPage's split pane when wide;
otherwise it pushes the relative log-entry route. Dashboard has no list owner.
BackBar.PreviousTitle names the actual previous page. Back remains relative.
Open device and Open rule retain their relative routes and positive IDs.
Device-scoped entries suppress the redundant link to the current device.
Narrowing changes the originating list's chip and returns only when pushed;
embedded narrowing stays beside the list. Split host owns glass; detail owns
only the supplied content clearance. No LogsPage list behaviour is changed.

Event and alert log entries both use this view. Events retain exact FullText,
including CRLF, blank lines, tabs, leading/trailing spaces and literal markup.
Fields use the original source values, not trimmed list-filter Type. Alert
entries retain rule text and existing state formatting. No parser, server
request, settings persistence, permissions, read-only behaviour, link policy,
IDs or raw payload is changed. There is no single-entry endpoint, asynchronous
detail load, loading spinner or retry request. A selected item is a snapshot;
return and select again obtains the list's current record. List fetching,
errors, refresh and paging remain #482. Graylog details remain #461.

The pushed lifecycle is idempotent, retains the subscribed Window, restores
Back on resume and disposes CoveredEdges.Watch on disappearing. Queued placement
checks visibility. The view's handler hooks are owned by the view and subscribe
once. Top fade is 20pt and matches the content sides; it reserves 20pt only
when BackBar is hidden. A visible scrolling BackBar starts at zero padding
and has no fade. Final content has 24pt plus host bottom clearance.

## Shared consumers and dependencies

Only LogEntryItem, LogEntryViewModel and their two detail hosts are affected.
Dashboard and Logs list previews still use Message, status and existing names.
Fields gains raw hostname/sysName for detail only. Existing route interception,
More activation and #490 retained controls are untouched. No other queued
branch is changed. Sequential parent delivery must update this branch only
after #489, #491, #492, #493, #494 Map, #495, #496, #497, #498, #499, #500 and
#501 land. Then the parent arranges Claude Opus 5.5 Medium review, CI and merge.
TestFlight follows #460 through #464. No closure or epic ticks are authorised.

## Evidence

Implementation self-review covers the full diff and whitespace check. Synthetic
regression coverage preserves payload identity, exact text and source fields,
unknown severity, blank-name action descriptions and missing-entry command state.
Existing tests cover narrowing return and rule/device navigation. No local
builds, tests or scripts were run by instruction. Core is pinned to
2.0.0-ci.52; APIs used already occur in checkout. Package metadata is unavailable
locally. Android/iOS compilation and shared-test CI remain pending. Independent
Claude review is pending its mandatory sequential queue slot. These are distinct
from device acceptance and none establishes a device pass.

## Device matrix, all pending

Run against the eventual merged commit and TestFlight build; record both here.

| Scenario | Expected evidence |
| --- | --- |
| Long multiline | CRLF, blank lines, tabs, spaces, literal HTML, long unbroken token and final message line are complete and reachable. |
| Missing metadata | Unknown severity, missing time/source/user/type and invalid route never show OK, epoch time, invented identity or a stale entry. |
| Related device and return | Whole target activates once, reads device and state, opens valid ID; Back names and returns to actual caller. Rule-only action remains available. |
| Network, device log, Dashboard | Pushed Back and both narrowing actions retain caller scope; device log suppresses redundant current-device link. |
| Split versus narrow | Embedded Back hidden, list glass retained, child return restores owner; rotate/fold without clipping, duplicate insets or inaccessible last field/link. |
| Light and dark | Plain message, field captions and fade match the actual surface with readable contrast. |
| Large text | Fields stack, names and unbroken values wrap; action stays at least 44pt with no proportional-column clipping. |
| VoiceOver and TalkBack | Joined nonblank action description includes state, children do not repeat, activation works; reading order follows content. |
| Duo orientations | Outer, inner portrait and inner landscape clear side/bottom glass; final field and related link reachable. |
| Fold, rotate and resume | Repeated transitions and child return restore one glass owner and correct placement without accumulated observers. |
| Ordinary iPhone | Repeat pushed and child-return cases, bottom home indicator, themes and large text. |

Keyboard is not applicable to this read-only view. User device acceptance is
pending. Issue #460 and epic #442 remain open.
