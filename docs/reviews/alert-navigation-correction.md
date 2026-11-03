# Queued correction after #489

Refs #442, #444, #446, #449. Baseline main 38fa645. Read repository guidance,
DESIGN.md and references, reviews 443/444/446 and #489's full remote diff and
449 assessment in its read-only checkout. FULLDESIGN.md is absent.

## Assessment before fixes

AlertRules uses SearchReveal and an unconditional Scrolled dismissal, unlike
Health/Groups ButtonSearch. Replace this page alone with ButtonSearch and disable
inherited OnScroll dismissal. Its existing native drag-state check distinguishes
filter replacement/programmatic scroll from a drag. Completion, unfocus, content
tap and departure close the field; query remains bound. Command-F calls the same
ISearchHost entry. ReducedMotion remains owned by ButtonSearch.

AlertDetailPage on main never claims glass; #489 adds that missing pushed owner,
relative Back, inset and resume handling. AlertsPage accepts alert detail only
in split mode; AlertRules accepts only rule detail. Narrow alert routes therefore
push; wide Alerts embeds. No central route rewriting defect was found: relative
alert/rule/template and Back routes pass AppPages.Resolve unchanged. The reported
“header tab” symptom is not evidence that the selected tab itself changed.
BackBar's null-root fallback used the tab title rather than its content title;
prefer actual root title, then tab title. All BackBar consumers receive that
fallback improvement; explicit previous-page titles remain first priority.

Groups' 20pt fade spans its entire inset list viewport, with a matching 20pt
header reservation, outside cards on Background. Its clear endpoint was generic
Transparent (black RGB), unlike the background-coloured clear resources already
used by Devices/Alerts. Use matching BackgroundLightClear/BackgroundDarkClear.
Do not use AlertRules' SurfaceClear: that fade lives inside a Surface card.
The 16pt sides and covered side inset belong to this list viewport; extending
the fade beyond it would fade non-scrolling page margins. No spacer guess or
global resource change is needed. Gesture transparency and at-rest reserve stay.

Groups only reclaimed glass in OnAppearing. A retained More stack can become
active after Devices without that callback. AppShell now schedules activation
after navigation and on Window resume, resolves CurrentPage at delivery, and
only calls opted-in owners (Groups and pushed AlertDetail). Groups' idempotent
activation restores Header, edge watcher and glass without refresh or state reset.
Moved removes before adding; edges dispose on departure. Shell window subscription
is retained and paired on load/unload. No native tokens are introduced.
TabBarButtons.Hide remains owner-safe and unchanged. Other pages do not opt in.

## Queue dependency and preserved contracts

MERGE AFTER #489 only; neither PR is merged here. AlertDetailPage is the sole
overlap. Parent must retain #489's complete wrapper, implement IActivePage and
make Activate apply Bar/visibility and call its existing ShowBack. Keep #489's
edge/Undo/inset/resume cleanup; do not duplicate OnDisappearing or ShowBack.
This standalone baseline includes minimal pushed Back so it remains reviewable.
No AlertDetailView or acknowledgement changes duplicate #489.

Affected entries: Alerts narrow push/wide embed, AlertRules embedded-rule or
pushed-rule alert links, dashboard/device/notification/widget pushed alerts.
Qualified IDs, fault/group acknowledgement permissions and Core verification,
list/detail selection, pinned Devices switching versus unpinned push, group and
location IDs, mode/query and notification settings remain unchanged. No setup
picker or queued branch is modified.

## Regression matrix and evidence

Added unexecuted route regression across pin removal for alert/rule/template
and relative Back. Existing route/ID/view-model tests remain. Native lifecycle
tests cannot run in the shared net10 project; the following lifecycle scenarios
are pending device regression tests, not automated coverage claims.

Use synthetic example.net devices, group Branch 2, location Lab, rule 7 and
distinct qualified alert/fault keys. Run every case on Duo outer, inner portrait,
inner landscape and ordinary iPhone; light/dark, largest text, VoiceOver/TalkBack,
Reduce Motion on/off, fold/rotate and foreground resume. Record build/head/OS
and diagnostics separately.

1. AlertRules pinned and More-pushed: search via button/Command-F, type several
   words after scrolling, replace filtered rows, change chips and programmatically
   scroll. Focus must remain. Real drag, Search and content tap close; query
   survives. Upward scrolling must never open. Repeated Command-F refocuses.
2. Alerts narrow -> alert -> rule -> template -> related rule -> Back through
   every caller; repeat AlertRules narrow and wide embedded rule -> alert.
   Check actual caller labels, relative pop, selected tab unchanged, one glass
   owner; older iOS/Android retain header Back. Wide Alerts stays embedded.
3. More -> Groups -> row -> pinned Devices -> More: retained Groups immediately
   reclaims search and pushed Back without Refresh. Repeat unpinned Devices
   with relative Back, pinned Groups root (no Back), tab switches, rapid push/pop,
   fold and resume. Query/mode/group filter/IDs survive; no stale owner taps.
4. Both Groups modes: scroll through fade and final row, empty/loading/error
   states, camera/side pill and bottom pill. Verify full intended viewport width,
   matching background colour, no dark bar, no intrusion at rest, passing taps
   and drags, and no doubled safe area or watcher/placement loop.

No local builds/tests/scripts executed. Full diff self-inspection and whitespace
check are recorded at handoff. CI pending; Claude Opus 5.5 Medium review blocked
by quota and required after parent updates only this PR after #489. All hardware
outcomes and user acceptance remain unverified. No closure, ticks or release.
