# Device event logs assessment, #482

Assessment baseline: main `ccfe4b0`, 5 November 2026. Refs #482 and #442.
The parent user approved the concrete proposal on 5 November 2026.
The implementation record below supersedes assessment fix/pending labels.
Native device compliance and acceptance remain unverified.

Read live #482 and #442 bodies and comments, #516 and accepted #460,
CLAUDE.md, DEVELOPING.md and DESIGN.md. #482/#442/#516 had no comments.
#460's 4 November acceptance comment is authoritative for entry details.
Historical geometry descriptions in DEVELOPING.md and review documents are
baseline implementation claims only. Native bounds cannot be verified by
source inspection.

## Modes and route contract

| Mode or route | Source contract and disposition |
| --- | --- |
| Device detail Event log | DeviceDetailViewModel.OpenSection passes device ID, section and display title to Routes.Logs. LogsPage.Initialise fixes Device, hides Event/Alert switch, starts on events and clears inherited event type. Compliant source contract. |
| Dashboard See all, single device spotlight | DashboardViewModel.Logs passes device ID/name and optional log type. ApplyQueryAttributes initialises scope then applies type. This is a second device-scoped caller, including an initially typed feed. Compliant. |
| Network More / Alerts entry | AppPages/Shell resolves Logs on its actual stack. Event and Alert modes remain available; Device chooser can narrow or clear scope. Shared consumer, must regress both modes. |
| Network Dashboard See all | Type parameter without fixed device opens event mode. Dashboard entry taps open LogEntry directly with no list owner. Preserve both routes. |
| Narrow list tap | OpenCommand passes original LogEntryItem and live LogsViewModel; ShellNavigationService pushes relative LogEntry when no split intercepts it. Back returns to actual caller. Compliant source contract; device return unverified. |
| Wide list tap | LogsPage.TryShowDetail intercepts LogEntry only while Split.IsSplit; constructs LogEntryView, hides its Back and marks IsBesideList. Parent list owns glass. ListDetail retains the pane while narrow and restores it when wide. Source compliant; retained selection indication and focus need verification. |
| Embedded/pushed narrowing | Only type/state changes originating list; pushed returns, embedded stays. Fixed device suppresses redundant Open device and Only this device actions. No redesign under #482. |
| Related device/rule | Network entries retain relative device/rule navigation and positive IDs. Fixed-device entry avoids a redundant device loop. Check full alert-to-device-to-log return stack. |
| Missing route/record | Detail clears invalid snapshots and provides return/reselect guidance. No single-entry fetch exists. Accepted #460, preserved. |

## Source contract and preservation

LogsViewModel owns requests through the existing client. Device filtering is
server-side; event type/search use Core EventLogFeed plus displayed-device-name
matching. Range is local-time filtering of loaded rows. Alert state and search
filter loaded alerts. Keep ServerTimestampsAreUtc and configured name style.
No credentials, permissions or connection policy are changed. A device-list
failure currently prevents the log request because name loading comes first;
record this failure honestly, never as an empty log. Do not add a permission
bypass or change optional-source policy without a separate decision.

Page size is 100, capped at 1000. Load more requests a larger prefix because
there is no offset. Event type/search may request Core's deeper filtered limit.
Exhaustion follows response count. Clear resets query/type/state/range and
optional Device but cannot clear a fixed device. Network type uses ChipMemory;
fixed-device type changes do not overwrite it. Auto-refresh on/off and interval
are phone preferences shared under logs; range and query are page state.
There is no new persistent log cache. Returning to a loaded page reuses its
in-memory source. Pull refresh resets the current prefix to PageSize and reloads
device names; do not substitute auto-refresh behaviour for this operation.

AutoRefresh runs only while showing and foreground, off by default, with
10/30/60/300-second choices and a 30-second default. It gates overlapping ticks;
LogsViewModel skips busy/unloaded feeds and rejects overtaken refresh results.
It refreshes the loaded depth and merges by event/alert identity to preserve
scroll. Failure retains rows/filters with RefreshProblem. Normal load uses a
cancellation source; errors use RunAsync's existing server/network descriptions.
These semantics must survive any UI correction.

## Complete concern dispositions

Compliant below means source contract only. Native rendering and interaction
remain unverified unless evidence is supplied for the eventual head/build.

| Concern | Status | Evidence, trigger and required outcome |
| --- | --- | --- |
| Finite list and hierarchy | Compliant source / unverified native | ListCard occupies the star row after fixed header, switch, search, chips, summary and error. Virtualised CollectionView is inside RefreshView, not a wrapping vertical scroller. Large fixed content may still exhaust short-screen height. |
| Search entry | Fix | Header button clears SearchText when already open; glass refocuses instead. Use ButtonSearch consistently so reopening refocuses and retains query. No swipe-open migration needed because no SearchReveal is attached here. |
| Search filtering/keyboard | Fix | List.Scrolled closes any focused search, including programmatic row replacement. Implicit KeyboardDismiss.OnScroll can also unfocus it. Use ButtonSearch with OnScroll=False, close on completion/unfocus/real native drag and content touch, retaining query. Check that helper behaviour covers content taps, rather than assuming it does. |
| Search motion | Compliant source / unverified device | Existing named SearchSlide aborts prior motion and honours ReducedMotion; replacement must preserve these properties. Deferred focus currently lacks an open-state guard; use helper's guarded dispatch. |
| Filter controls | Fix / proposal pending | Device/type/state are 36pt Borders with nested tap Labels; Clear is explicitly 30pt. Fixed Device looks interactive despite no-op command. Selected Auto-refresh and segment states rely on colour; native accessible selected-state announcement is unverified. Replace with labelled native controls per proposal. |
| Filter facts | Compliant source / unverified fit | Device/type captions truncate at 180/160pt; all filters share horizontal scroll. Current source preserves values but not their complete visible names. Test absent/current type, cancellation, no matches and large text. |
| Row message | Fix / proposal pending | TailTruncation and MaxLines=1 hide long messages; Message flattens line breaks while FullText retains exact payload. Propose complete wrapping row facts without changing FullText or server records. |
| Row severity and activation | Fix | Status dot alone conveys event severity; StatusText exists but is not bound in list. Row has a tap gesture without an explicit complete accessible action name. Provide textual severity and native activation, preserving unknown as Event rather than OK. |
| Full row facts | Fix / proposal pending | Current row shows optional device, flattened message, type/user, HH:mm and day heading. ID, seconds, raw hostname/sysName and full multiline payload remain available in accepted detail. Proposal below defines summary versus full detail; no silent omission claim. |
| Missing metadata/time | Compliant source / unverified reading | Null timestamp stays blank, no epoch; missing message has existing placeholder; device lookup falls back to hostname, Device ID or LibreNMS. Unknown severity is neutral. Do not invent source values. |
| Loading and refresh busy | Compliant source / unverified native | BusyCard overlays finite results; IsEmpty requires loaded, not busy and no error. PullToRefresh owns spinner/busy state. Check initial load, pull, load-more and filter-driven deeper fetch separately. |
| Source empty versus unmatched | Compliant source / unverified fit | EmptyText distinguishes nothing logged from no matches in last loaded count and offers clear/look further back when applicable. Type/range summary describes loaded subset, never whole server history. |
| Error/offline/retry | Compliant source / unverified discoverability | Error card shows existing failure; pull refresh retries. Old source survives failed pull/load-more, while device change forgets prior source. No disk offline cache or guaranteed offline initial load. Test denied names/logs, timeout and no connection, including empty-list retry gesture. |
| Auto-refresh cached rows | Needs regression assessment | Merge comparison checks only ClockText, Message and DeviceName. Same-ID changes to severity, type/user, sub-minute timestamp or raw fields can retain stale LogEntryItem. Add behavioural synthetic test before correcting equality; do not change timing or merge identity. This affects both network modes as well as device events. |
| Auto-refresh indication | Unverified accessibility | Quiet failure text exists; on/off is visually tinted. Verify assistive state announcement and that status remains readable under large text. |
| Top fade | Fix | List has no 20pt input-transparent fade or reserved initial spacer. Adopt matching viewport-surface fade and header reservation, including day headings and empty view. Device checks must establish colours and first-row readability. |
| Insets and usable base | Unverified | TabAlignedListLayout uses page-native pill measurements, column gutters, ListCard margin and footer. Does not prove actual card bottom equals pill base in the finite host. Measure H/B/V below before asserting a correction; no guessed offsets. |
| Detail clearance | Unverified dependency | Host passes right/bottom clearance to accepted LogEntryView.PlaceContent; embedded header is hidden. Verify trailing pane against actual native bounds after split resize, including column-relative right clearance. Preserve #460 presentation. |
| Camera/pill layering | Fix owned by #516 | ListColumn padding constrains chip scroller's outer clip. #520 records reference correction using full-width clip with internal safe padding. #516 owns cause confirmation and implementation; #482 makes no competing clip/occlusion change. |
| Retained activation/resume | Fix | LogsPage lacks IActivePage, so AppShell.RestoreActivePage cannot restore it on retained-tab activation/resume. Only Appearing shows glass; Moved only places footer. Use existing activation contract without refreshing/resetting state. Make subscriptions idempotent and queued work visibility-guarded. |
| Fold/rotate/keyboard bounds | Unverified | SplitChanged/column SizeChanged/CoveredEdges.Watch exist; helper retains Watch without page disposal. Audit lifetime centrally before changing shared helper. Native keyboard translation/occlusion and repeated transitions require device evidence. |
| Themes, text scale, contrast | Unverified | Themed styles exist; fixed heights, timestamp trailing column, compact chips and empty-view horizontal buttons can constrain large text. Confirm both themes at maximum supported text scale. |
| VoiceOver/TalkBack/focus | Fix + unverified device | Provide row intent and textual severity, actual filter state and adequate native targets. Verify order, one activation, no duplicated nested target, heading and selected detail focus. |
| Permissions/security/persistence | Compliant preservation | Assessment changes no application file, server request, route, preference, credentials or raw facts. |

## Concrete proposal for parent user agreement

Keep one virtualised day-grouped list card and the existing fixed search/status
rows. Rows show wrapping complete message text, textual severity, type/user and
full local timestamp in a stacked facts layout, with optional device name only
when scope is not already fixed. Use FullText as plain text so multiline
messages remain complete. Use one native accessible Open entry target covering
the facts, with entry ID in its name. Keep raw hostname/system name and other
source fields in the accepted detail view. Preserve device/alert semantics.

Replace nested filter clear labels with native labelled choices of at least
44pt height; selecting Any type/state clears that choice. Keep Clear filters
as the explicit reset. Show fixed Device as a wrapping read-only scope caption.
Auto-refresh names its on/off state in text and accessibility. Keep the existing
horizontal strip and its choices; #516 separately owns its camera clip. Allow
wrapping text within controls and stacked empty-state actions on narrow widths.

The guide requires readable facts and accessible targets but does not settle
this row composition or the filter replacement. Parent user agreement is
required before implementing these visual decisions. This assessment stops at
that gate; the confirmed search/lifecycle/fade/merge defects remain recorded,
not silently deferred as compliant. After agreement, implement centrally in
this issue's coherent PR with behavioural regression coverage. Parent may
request a smaller approved scope instead.

## Shared-consumer ownership and handoff

| Owner | Files/consumers | Required coordination |
| --- | --- | --- |
| #482 centrally | LogsPage.xaml/.cs, LogsViewModel, list tests | One shared implementation for fixed Device events, Dashboard single-device typed feeds, network Event and network Alert feeds. Audit every mode after edits. No duplicate device-specific fork. |
| #516 | Same LogsPage header/filter clip and camera layering | Handoff: current ListColumn pads the whole chip scroller; compare Devices/Alerts and merged #520. Establish native clip cause, preserve inner safe padding and validate both fixed and network scope. Land sequentially with #482; rebase and review full combined result. No duplicate implementation here. |
| Accepted #460 / merged #502 | LogEntryView, LogEntryPage, LogEntryViewModel, LogEntryItem detail facts | Accepted 4 November; do not redesign. Changing row presentation/equality must preserve exact FullText, Fields, snapshot IDs and narrowing routes for Dashboard and both list hosts. |
| Shared helpers | TabAlignedListLayout, CoveredEdges, TabBarButtons, ReservedRegions, ButtonSearch, KeyboardDismiss, ListDetail | Reuse measured APIs. TabAlignedListLayout also serves Graylog; any helper correction needs #483/#517 regression audit before editing. No shared helper changed by this PR. |
| #483/#517 | Graylog lists and camera correction | Separate server/search semantics; no Graylog implementation copied into Logs or changed here. |
| AppShell / navigation | IActivePage, ShellNavigationService, AppPages, PageHeader | Preserve actual caller stack and Back, pinned/unpinned resolution and split interception. Retained activation must restore controls without fetching. |

Relevant merged baselines inspected: #502 details and its review document,
#520 retained activation/camera correction and its PR body, current #490
activation/navigation source, #530 measured device-section contract in DESIGN.
Open PR inventory was empty at assessment time. This is a recorded handoff;
no claim that #516 has accepted a deferral or that a sibling owner was messaged.
Protected dirty #475 worktree was not read, changed, stashed, reset or imported.

## Evidence plan, all native results unverified

Use synthetic devices such as device-01.example.net and documentation addresses
only. Record tested commit SHA, CI run/artifact and device build number, OS,
text scale, theme and form factor beside each result. Never attach real logs.

For each case capture native coordinates in one common host coordinate system:
H is finite body host height; B is actual pill bottom in that host; V is actual
ListCard/CollectionView viewport bottom. Record host origin, card/list frames,
pill frame, action/glass occupied bounds, safe area and keyboard frame. Record
H, B, V, V-B and the expected gap max(0,H-B) separately. With no fixed action,
a side or bottom pill anchors V to B; a top-wide pill requires separate
classification. Do not use a page height with body-local B/V. A visible footer
or reachable last row does not prove V=B.

Scroll to the very end independently. Capture the complete last message,
metadata, Load more action and footer above obstruction; record last-row bounds
and the lowest reachable tappable fact against glass/keyboard. Repeat after
loading another page, changing filters and opening/closing split detail. In
split mode measure list and detail separately, including the detail's last
field/action. Record failures rather than adding guessed spacers.

| Matrix | Required steps and evidence |
| --- | --- |
| Devices | Duo outer, inner portrait, inner landscape; Android narrow phone and tablet; ordinary iPhone when available and iPad split view. Explicitly mark unavailable hardware. |
| Display | Light/dark, default/large/max text, VoiceOver/TalkBack, Reduce Motion on/off. Capture readable first heading/row, filter controls, selected state and full final row. |
| Entry/return | Device section; Dashboard one-device typed See all; network More; Alerts; network typed Dashboard. Tap entry, Back, narrow Only type/state, related device/rule where allowed. Preserve exact caller, tab and one glass owner. |
| Search | Header/glass/Command-F, repeat magnifier, type multiple letters, no matches, filter replacement and programmatic scroll, native drag, content touch, completion/unfocus. Query survives dismissal and only explicit Clear resets it. |
| Filters/paging | All types including long/missing names, Any choice/cancel, ranges, network device chooser and clear, alert states, 100-row prefix, deeper filtered fetch, 1000 cap and exhausted source. Fixed device cannot clear/switch scope. |
| States | Initial busy, loaded empty, unmatched, denied device names, denied logs, timeout/offline with no source, failed pull/load-more with retained rows, cached in-memory return. Confirm retry is possible even with no rows. |
| Auto-refresh | Off default, each interval, foreground/background, return/resume, in-flight user refresh/filter/device change, failure/recovery, scrolled-down position and same-ID changed severity/type/user/time. No unintended overlap or source mix. |
| Data facts | Long unbroken and multiline message, literal markup, missing timestamp/user/type, unknown severity, system event/device ID 0, missing name, alert fallback rule name. FullText and IDs must remain exact. |
| Geometry/lifecycle | Cold launch outer then unfold; cold launch inner; fold/rotate while search/keyboard/detail open; dismiss keyboard; push child and return; change retained tab and resume repeatedly. Re-measure H/B/V and last-row reach after each. |
| Layering dependency | #516 captures chip clip and resting safe controls beside camera, scrolled chips beneath camera, side/bottom/top pill; regress fixed-device event and network Event/Alert. Include Tab bar buttons and Top line diagnostics, check placement loops. |

## Review and execution record

Assessment-only diff self-reviewed against DESIGN.md and scope. Only git diff
and whitespace checks are permitted locally; no local build, test, restore or
validation script ran. No new tests are warranted for a document-only change.
Existing LogsTests cover filtering, fixed scope, paging and detail contracts;
future code changes need synthetic behavioural regressions for stale merge
facts and lifecycle/search behaviour, not source-substring assertions.

CI execution remains in GitHub Actions: Unit tests, Android and iOS simulator
builds, with CodeQL as configured. No CI success or native acceptance is claimed.
Parent owns fresh independent Opus full-diff review and CI judgement. Approval
of this proposal is separate from device acceptance. Do not close #482, tick
#442, merge, watch or publish a release from this assessment task.

## Approved implementation, 5 November 2026

The user approved the complete proposal after the parent displayed synthetic
fixed-device and network examples. The proposal and concern table above record
the assessment baseline. Their pending-proposal labels are superseded by this
implementation record, not by native acceptance.

LogsPage now uses one virtualised day-grouped list with plain-text FullText,
textual severity, stacked type/user and full local timestamp. A native button
covers each entry's facts and speaks its ID and complete facts. The facts are
excluded as duplicate accessibility descendants. Fixed scope is read-only;
network device, type, state, range and interval remain native choices in the
horizontal strip. Choices wrap within the measured list-column width and have
at least 44pt targets. Any type/state and cancellation retain their existing
semantics. Clear filters stays explicit, auto-refresh says On/Off, and empty
state actions stack.

ButtonSearch now owns reopen/refocus, retained queries and guarded deferred
focus. KeyboardDismiss.OnScroll is disabled so programmatic list changes do
not close search. Native drag detection remains in ButtonSearch; content taps
use ContentPage.HideSoftInputOnTapped, completion and unfocus close the slot.
IActivePage restores glass without fetching, subscriptions are idempotent,
and queued page placement checks active/current-page state. The shared measured
layout helper is unchanged. Its lifetime audit remains a shared dependency.
The 20pt input-transparent fade matches the list-card surface and reserves
initial content space, including the empty state.

Auto-refresh still merges by event/alert ID and retains unchanged row objects.
Comparison now covers displayed and accepted detail facts, including raw
severity and alert hostname. Synthetic behavioural regressions change severity,
type, user, sub-minute time, hostname and sysName independently, then refresh
multiline literal text. They assert updated facts, retained neighbouring identity
and unchanged default refresh settings. Existing refresh failure, overlap,
paging and fixed-scope tests remain applicable. Tests were added for CI and were not executed locally.

Caller audit covered DeviceDetailViewModel's EventLog route, Dashboard typed
See all with optional single-device scope, Dashboard direct entry snapshots,
TabPins/More network Logs, Alerts' Logs entry, LogsViewModel.OpenAsync and split
LogEntryView hosting. Both network Event and Alert share this implementation.
No route, permission request, preference or server-fetch contract changed.
Accepted #460 FullText, Fields, IDs, raw hostname/sysName, related device/rule
and Only narrowing contracts remain intact; detail views were not redesigned.

Full intended and staged diffs and git whitespace checks are the only local
verification. No build, test, restore or validation script ran. CI, independent
Opus review and native evidence remain with the parent. The native matrix above
is still unverified. #516 retains camera clip/layer ownership; no guessed native
geometry offsets were added. Refs #482 and #442, pending device acceptance.

### Independent review corrections

Full-diff Opus 5.5 Medium review at fb16e77 found no blockers and one medium
choice-label mismatch. Unset choices now explicitly read All devices, Any type
and Any state, with a shared regression assertion. Row activation covers the
whole row; facts own their padding and the separator stays at the row edge.
Choice wrapping uses the measured filter viewport, not its padded parent.
The fade stays inside the card stroke. Alert state and identity are no longer
repeated in list metadata or speech; spoken facts omit empty parts. Device
emphasis and code indentation are restored. Selected filter values remain
explicit in text; their tint consistency remains a minor visual difference.
The epic's explicit acceptance gate overrides CLAUDE.md's general Closes rule:
Refs is intentional until the user accepts device evidence. Native hypotheses
from review remain unverified, including runtime resource references, content
unfocus on Android and wrapping filter alignment. CI is separate evidence.

### Row-state follow-up from the Graylog review

The #483 full-diff review confirmed the same implicit Button Disabled style
hazard in Logs: while the shared async Open command navigates, every overlay
button becomes disabled and its opaque grey background can cover the row facts.
This scoped follow-up uses the reviewed Graylog/PortReadout pattern: an explicit
empty style, transparent Normal/Disabled states and PrimaryTint for pressed and
keyboard-focused states. Row actions, facts, routes and refresh logic are unchanged.
No shared style or other consumer is edited. Native press, disabled, focus and
return appearance on both themes and screen readers remains UNVERIFIED. No new
visual policy is introduced; this preserves the approved complete-row facts.

Full intended/staged diff and whitespace checks only; no local build or test.
Android/iOS CI and independent review are required. No mirrored source-string
test is added for these native visual states. Refs #482 and #442; no acceptance.
