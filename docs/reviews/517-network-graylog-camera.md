# Graylog camera layering (#517)

## Baseline and current cause

Assessment dated 5 November 2026 at main
`612b4a89c66e73dee58a1037fe37f535907a4356`, the merge of #548 for #516.
Read live bodies/comments for #517, #442, #483, #461, #288 and #516,
merged #545 and #548 descriptions, CLAUDE.md, DEVELOPING.md, DESIGN.md,
and the #483/#516 assessments. No installed build or native frames are available.

The reported 4 November problem asks for pills to pass beneath the camera like
Devices and Alerts. It predates #545, merged as
`56d55fef095f6b2c62cefc2007772584540ea971`. Current Graylog has no horizontal
filter ScrollView. `GraylogPage.xaml` puts `FilterStrip`, a wrapping FlexLayout,
inside CollectionView.Header, with the summary and read-only device scope.
The parent approved that compact presentation for small and large screens.
Restoring a horizontal root strip would reverse that approval.

`TabAlignedListLayout.Place` still assigns Graylog's measured side gutters to
ListColumn.Padding. ListCard and its CollectionView therefore occupy a narrower
column. The header then adds 14pt side padding. This is the confirmed source
constraint, not proof of the physical camera frames or of a native clipping bug.
Choices move vertically with the list. They cannot travel sideways through the
side gutter. If their horizontal extent does not intersect the camera, vertical
scrolling cannot make them pass beneath it. Top container safe area, fixed
header/search/status rows and the card's own viewport further constrain the
vertical path. Actual clip ancestry and camera intersection remain UNVERIFIED.

DevicesPage and AlertsPage instead have horizontal ChipStrip ScrollViews with
internal resting padding and independently guttered list cards. Their chips
can translate horizontally into the outer clip's side area. #548 applies that
same distinction to Logs. Its correction is relevant evidence about clip versus
content padding, but copying its callback into Graylog would not reproduce its
scroll direction. Changing z-order cannot create that missing movement.

## Decision and concrete proposal

[The HTML proposal](proposals/517-graylog-camera.html) was displayed by the
parent on 5 November 2026. Automated preview was unavailable because the host
lacked browser libraries; the rendered schematic is not native evidence.

The user explicitly chose: **“Keep wrapping; defer sideways underlap.”** This
accepts deferral of sideways camera underlap under this issue, while retaining
#545's approved compact wrapping filters and full facts. No horizontal filter
strip or full-width card redesign is authorised or implemented. The linked owner
is [#517](https://github.com/DashyNMS/mobile/issues/517); reconsider horizontal
travel there only if the user requests a new proposal. This decision supersedes
the proposal's pending-choice wording, retained as historical material.

The deferral does not establish a camera-layering fix or device acceptance.
#517 and #442 remain open/unticked pending the scoped native outcome and user
acceptance. The vertical path, safe resting controls, viewport and final-row
matrix below still require installed-device evidence. Application source and
DESIGN.md remain unchanged; existing scroll-direction guidance and the #545
approval suffice without a new reusable design rule.

## Shared helper audit and preserved contracts

There are two TabAlignedListLayout consumers. LogsPage supplies #548's optional
gutter callback, distributes fixed-slot margins and internal horizontal filter
padding, and retains the helper's computed bottom margin. GraylogPage omits the
callback and keeps the original column-padding branch. Both share native scroll
inset suppression, resize/split/safe-edge placement, pill-base margin, scrolling
footer and detail clearance. No helper or Logs source is changed here. Any later
Graylog opt-in must use that central callback, audit both consumers and retain
side-pill card clearance; introducing another helper is unnecessary.

| Mode/contract | Current source assessment and remaining evidence |
| --- | --- |
| Root tab / More and network device selection | Wrapping choices include change/clear; native camera/taps U |
| Dashboard Graylog See all | ApplyQueryAttributes retains stream/range; return and camera U |
| Device detail Graylog See all (#483) | Fixed ID/name, read-only scope, no change/clear; same wrapping header, native U |
| Narrow message / wide embedded message (#461) | Existing message/list parameters, TryShowDetail, detail actions and PlaceContent inputs untouched; native width/base/Back U |
| Setup/device picker (#405) | Existing relative routes and accepted persistence/security untouched; child return U |
| Search | ButtonSearch and submitted server query retained; draft survives dismissal; native keyboard/drag U |
| Filters, paging, auto-refresh | Commands, 44pt native choices, finite virtualised list, Load more and footer retained; native maximum text/reach U |
| Loading/error/empty/offline | BusyCard and fixed stream/refresh/error status remain; simultaneous long errors can reduce viewport, as recorded in #483; native U |
| Data ownership | All #545 generation isolation, request identity, per-generation stream metadata owner, failed-search timer guard, busy/paging gates and authored tests untouched |
| Content and permissions | Complete plain multiline message, severity/facility/full time/unavailable facts, one native Open message action retained; API/credentials/storage/permissions unchanged |
| Lifecycle | Existing Activate/EnsureLoaded, active glass ownership, tab return/resume and Moved placement untouched; native U |

## Review and validation

Self-review covered the full intended and staged diff for these two documentation
files only; working and staged whitespace checks passed. Independent review and CI
results are recorded in the PR.
No local builds, tests, restores, validation scripts or agents ran. No source
or test changes require new synthetic tests. CI shared tests, Android and iOS
simulator/widget results must be recorded against the actual PR head, separately
from document review and native evidence. Results are recorded in the PR against the tested head.
No closure, epic tick or release is requested.

## Exact native evidence still required

U means UNVERIFIED. Baseline source SHA is
`612b4a89c66e73dee58a1037fe37f535907a4356`; installed SHA/build, CI run/job URLs,
OS/version and device are U. For a later implementation, replace those fields
with its full installed SHA and build before recording a result.

| Check | Required observations | Result |
| --- | --- | --- |
| Geometry per mode | In one coordinate space record page/list host height H, pill bottom B, viewport bottom V, V−B, camera/reserved frames, ListColumn, ListCard, native CollectionView/scroll clip and every clipping ancestor, header and choice frames | U |
| Real movement | Record the same choice before/during/after vertical drag, camera intersection and actual occlusion; distinguish top-edge clipping/fade from camera underlap; attempt horizontal drag to confirm no horizontal filter lane | U |
| Viewport and final reach | For side, bottom and top-wide pill separately record H/B/V; scroll complete final row and Load more clear of glass; footer reach does not prove V aligns with B | U |
| Displays/lifecycle | Duo outer, inner portrait/landscape, ordinary iPhone if available; fold/rotate, keyboard dismissal, retained tab, child return and resume without reopening | U |
| Readability/access | Light/dark, default/maximum text, long device/stream choices, all 44pt targets and full facts; VoiceOver/TalkBack naming, focus order and single activation | U |
| States/routes | Network and fixed-device, Dashboard stream/range, pushed and split details, unconfigured/setup return, initial/pull/timer loading, errors/no matches, paging and connection change | U |

User agreement on the proposal and later installed-device acceptance are separate.
Neither CI nor this source assessment establishes camera layering acceptance.


Independent full-diff Opus review at 4da3b10 found no blocking problems and
confirmed the source, timeline and decision record. The historical proposal now
includes the user's decision on the page itself. Application source is unchanged;
current-head CI remains separate from all unverified native results.

## Latest direction: mirror the Event and Alert log structure

On 5 November 2026 the user directed: **"we should mirror Event and Alert Log for
this with the pills and the layering under it - the structure of the event and
alert log page is what we want."** Live #483 and #517 comments record it. This
**supersedes** #545's approved wrapping CollectionView.Header and #549's "Keep
wrapping; defer sideways underlap" decision. The earlier sections above are
historical. No new proposal was needed: the accepted Logs page (#544, #546,
#548; #482 and #516 accepted on device with no notes) is the settled reference.

### Source cause and correction

Cause, from source: Graylog's choices sat in a wrapping FlexLayout inside
CollectionView.Header, and `TabAlignedListLayout` padded `ListColumn` with the
measured gutters. Choices could only move vertically inside the narrowed list
card, so they could never travel sideways beneath the camera. Logs avoids this
with a horizontal ScrollView whose outer clip spans the page and whose Padding
holds the resting gutters.

Correction in `GraylogPage.xaml` and its code-behind, matching `LogsPage`:

- `ListColumn` has no padding. Rows are header, search slot, `FilterStrip`,
  `Summary`, `ErrorCard`, then the star list row holding `SetupCard`, `Busy`
  and `ListCard`.
- `FilterStrip` is a horizontal, scroll-bar-free ScrollView with a full-width
  clip and 16pt resting Padding. It holds native 44pt `GraylogChoice` buttons
  (device change and clear in network scope, time, level, stream, auto-refresh
  On/Off and its interval). A fixed device is a read-only label. Each choice
  and the label wrap within `ListCard.Width`, as Logs does, so a long device or
  stream name stays complete when scrolled into view. The FlexLayout and its
  per-child width handler are removed.
- `Summary` is a fixed row: summary text, stream-metadata problem and
  auto-refresh failure, with Clear filters as its 44pt link (as in Logs).
  Graylog's own search error keeps its card. Strip and summary are hidden while
  Graylog is not configured, as before when they sat inside the hidden card.
- `CollectionView.Header` is the 20pt transparent fade spacer only. The
  surface-matched 20pt input-transparent fade, EmptyView (now with Logs'
  40pt top padding below the fade), rows, Load more and `ListFoot` are retained.
- `GraylogPage` passes `PlaceGutters` to `TabAlignedListLayout`, the existing
  callback #548 added. It sets the measured left/right gutters on the header,
  search field, strip Padding, summary (+4pt as Logs), error, setup, busy and
  list cards, retaining the helper's computed list-card bottom margin. Android
  keeps the XAML 16pt margins, as Logs. No offset is guessed.

Preserved, unchanged: `TabAlignedListLayout` and `LogsPage` source; every
`GraylogViewModel` path, including #545 setup-generation isolation, current
load identity, stale error/metadata guards, zero-offset paging after a connection
change, Apply, the timer's cleared old query, busy gates and authored tests;
no view-model, projection or label change was necessary. Rows keep complete
plain multiline messages, list-only level/facility/full time with unavailable
fallbacks, one native Open message action with its explicit empty style,
transparent Normal/Disabled and PrimaryTint Pressed/Focused states. #461 detail
geometry/actions (`PlaceContent` input and embedded glass), #405 setup and
device picker, routes, Dashboard previews and See all, search via ButtonSearch,
permissions, credentials and network behaviour are untouched.

Shared consumer audit: `TabAlignedListLayout` has two consumers, both now opting
into the gutter callback; its default column-padding branch is retained but
unused. `ButtonSearch`, `ListDetail`, `PullToRefresh` and `GraylogMessageItem`
are consumed unchanged.

Small and large screens: the strip is one 44pt-minimum row (taller only when a
single choice wraps), so maximum text grows the strip by wrapped choice height
rather than by a wrapped grid of every choice. Summary and errors can still
wrap; long simultaneous errors can reduce the viewport. Both are native **U**.

### Exact native matrix for this correction

All rows are **U (UNVERIFIED)**. Record the full installed SHA and build, OS
and device for each; no local build, test or device run was available.

| Check | Duo outside | Duo inside portrait | Duo inside landscape |
| --- | --- | --- | --- |
| Strip outer clip spans the page; resting first/last choice clear of camera and pill; choice scrolled sideways passes beneath the camera | U | U | U |
| Choice frames: 44pt minimum, wrapping long device/stream within list card width, default and maximum text, both themes | U | U | U |
| Fixed device label read-only; network change/clear, time, level, stream, auto-refresh, interval and Clear filters execute their commands once | U | U | U |
| Same-coordinate H, pill bottom B, viewport bottom V and V−B for side, bottom and top-wide pills | U | U | U |
| Complete final row and Load more clear of glass, separately from V≈B | U | U | U |
| Fade at rest leaves first row and EmptyView readable; passes taps and drags | U | U | U |
| Fold/rotate, keyboard dismissal, retained tab, Settings/picker child return and resume re-place gutters without reopening | U | U | U |
| VoiceOver/TalkBack: strip order, choice names/values, Open message speech, single activation | U | U | U |
| Split view: list pane gutters, embedded #461 detail width/base and glass | n/a | U | U |

Code review, CI (shared tests, Android, iOS) and this device matrix are recorded
separately. Refs #483, #517 and #442 remain non-closing until the user accepts.
