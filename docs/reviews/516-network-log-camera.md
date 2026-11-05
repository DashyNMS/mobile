# Network log camera layering (#516)

## Baseline and finding

Reviewed main 357937869cfb8854c23be45ed957dc662e5b51f1, including live bodies
and comments for #516, #442, #482, #446, #452 and #517, and merged #520,
#522, #544 and #546. This assessment covers the network Event and Alert lists
and the shared fixed-device Event list. It does not claim physical acceptance.

The confirmed source cause is `TabAlignedListLayout.Place` assigning safe side
padding to `ListColumn`. `FilterStrip` is inside that padding, so its native
outer clip ends before the camera. Adding padding to individual chips or
changing glass z-order cannot restore the missing scroll area. Logs already
opts out of side safe areas at the page and split host, but that does not undo
its ancestor's padding. Native frames proving the reported symptom are U.

Devices and Alerts use an edge-to-edge chip strip while resting controls clear
camera/glass. Alert Rules #520 and Neighbours #522 supply the directly reusable
implementation: full-width scroller, safe internal padding, independent gutters
on other slots. `CoveredEdges.Of` reads native safe edges;
`ForScrolling` accounts for a measured side pill and bottom glass;
`Beside` intersects row bounds with reserved regions and glass on iOS 27.1.
`ReservedRegions.Occupied` includes active camera/system region margins.
`TabBarButtons.PillIn` converts the pill into page coordinates; `Occupied`
reports pill/action frames. None of these helpers or native glass ownership
needs changing for this clip defect. The settled camera policy is retained.

## Correction and shared ownership

#516 centrally owns the optional gutter callback added to
`Controls/TabAlignedListLayout.cs`. The helper retains its existing measurement,
resize/safe-area subscriptions, native inset suppression, pill-base and footer
policy. It sets the bottom margin before invoking the callback, so Logs can
apply side margins without losing that measured base. There are exactly two
callers, LogsPage and GraylogPage. Graylog omits the callback and keeps the
original column-padding branch, card margin, footer and detail inputs. #517
owns any subsequent opt-in and must consider #483's scrolling filter header.
No global CoveredEdges, ReservedRegions or TabBarButtons changes are made.

Logs removes column padding and supplies independent gutters for Header,
LogSwitch, SearchField, Summary, ErrorCard, Busy and ListCard. FilterStrip has
SafeAreaEdges None and internal padding. XAML provides 16pt initial gutters
before native measurements, including Android's existing container safe area.
Choice widths now follow ListCard.Width, the width after safe gutters, rather
than the newly widened scroller. Wrapping, 44pt native filter targets and the
horizontal strip approved in #544 remain. No new visual choice is introduced.

## Modes, routes and contracts

| Scope | Assessment |
| --- | --- |
| More / Alerts to Logs, network Event and Alert switches | Source correction applies to both; native result U |
| Dashboard Event card See all, including selected type | ApplyQueryAttributes and ShowEventType unchanged; native return U |
| Device detail Event log See all, fixed ID/name | Same page callback; read-only Device label and hidden switch preserved; native result U under #482 |
| Network Device/Type or State/Range/interval choices, Clear device/filters | Commands and predicates untouched; full native choice text and 44pt minimum preserved; native end reach U |
| Narrow row to pushed entry; wide row to embedded entry | Routes.LogEntry, selected item/list parameters and detail PlaceContent inputs unchanged; #460 accepted detail untouched |
| Entry actions narrowing the source, device navigation and Back | Existing source/list relationship and routes retained; native child return U |
| First load, pull refresh, no source rows, no matches, errors and failed auto-refresh | Bindings, retained data and retry/paging commands unchanged; new per-slot margins cover each state; native result U |
| Search, keyboard, focus and motion | ButtonSearch unchanged: action-only opening, retained query, native drag dismissal, completion/unfocus and Reduce Motion; native result U |
| Paging and refresh | CollectionView stays finite/virtualised; footer and Load more retained. Auto-refresh timing, foreground/showing gates and same-ID row updates untouched |
| Retained tab / resume | IActivePage Activate and glass Moved placement retained, with existing AppShell activation; native result U |
| Permissions, data and security | No view model, API, storage, connection, credentials, server-text or permission change |

## Review and validation

Self-review covers the complete intended diff against the baseline and DESIGN.
The file set is the helper, Logs XAML/code-behind, DESIGN and this assessment.
The original dirty #475 worktree is untouched. No other checkout was used.
The existing footer calculation and embedded-detail clearance are preserved;
this work does not infer viewport alignment from final-row reachability.
No domain logic changed and no source-mirroring XAML tests were added. The
native geometry checks below are the relevant regression evidence.

No local builds, tests, restores or validation scripts ran, as instructed.
Git diff and staged whitespace inspection are the available local checks.
Parent owns independent Opus review and CI judgement. Required CI is Unit tests
including shared regressions, Android Release and iOS simulator/widget build.
All are pending at implementation handoff. Device results are all U, meaning
unverified. No build was installed and no screenshots or native frames were
captured in this environment. No issue closure, epic tick or release is claimed.

## Exact native acceptance record

For every run record full installed head SHA, CI run/job URL, package/build,
OS/version, device, orientation, scope/mode, theme and text size. The reviewed
implementation is the commit introducing this assessment; obtain its full SHA
from the PR head rather than confusing baseline 3579378 with the installed fix.
Build identity, frames and every result below currently remain U. Attach only
synthetic device names/messages, such as example.net and documentation addresses.

Capture page and list-column bounds, FilterStrip native outer clip, scrolling
content bounds/insets and offset, first/last choice frames and tap rectangles,
active camera reserved region including margins, pill and glass-action frames
in one declared coordinate space. Record actual host height H, pill bottom B,
list viewport bottom V, H-B and V-B. Capture at rest, mid-scroll beneath camera,
and strip end. Verify first/last resting choices are readable and tappable,
scrolled choices travel under the camera, and no ancestor or automatic safe-area
clip truncates travel. Record the final entry and paging action fully clear of
bottom glass separately from V-B. A screenshot alone cannot establish these
native clip or hit-test facts.

| Configuration / transition | Camera clip and controls at rest / scroll / end | H/B/V and footer | Behaviour |
| --- | --- | --- | --- |
| Duo inside portrait, network Event | U | U | U |
| Duo inside portrait, network Alert | U | U | U |
| Duo inside landscape, both network modes | U | U | U |
| Duo outside, both network modes | U | U | U |
| Fixed device Event, each inside orientation and outside | U | U | U |
| Dashboard typed Event entry and child/detail return | U | U | U |
| Fold/unfold and rotate while scrolled, narrow/wide detail transition | U | U | U |
| Retained tab away/return and foreground resume without refresh | U | U | U |
| Ordinary iPhone, bottom and top-wide pill when available | U | U | U |
| Android native safe area and TalkBack | U | U | U |

Repeat the applicable rows in light and dark, default and maximum supported
text. Use long fixed-device/type names and multiline synthetic entries.
Exercise every filter and scroll to Interval when auto-refresh is On, including
native choice presentation/cancellation. Verify VoiceOver and TalkBack names,
current values, single activation and 44pt targets, fixed scope announced as
read-only, and row facts visible during disabled/pressed/focused states from
#546. Open/refocus search, type without losing focus during filtering, trigger
programmatic scrolling, then dismiss by real drag and completion; retain query.
Check first load, busy, empty/no matches, failure with cached rows, refresh and
paging; scroll the final row/action clear. Return from pushed #460 detail and
exercise embedded detail, then repeat fold/rotation, tab return and resume.
Check glass ownership, camera frames and base deltas after each transition.
Parent records independent review and CI separately, then seeks explicit user
device acceptance. Refs #516 and #442 remain non-closing until that acceptance.
