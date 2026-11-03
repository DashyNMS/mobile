# Alert details review

Refs #449, epic #442. Baseline 38fa645 after #488. Read live #449/#442,
#447/#448, CLAUDE.md, DEVELOPING.md, DESIGN.md and DESIGN-REFERENCES.md,
and the linked 447/448 review evidence. FULLDESIGN.md is absent; DESIGN.md
is the current guide. No agents or review delegates used.

## Scope and contracts

AlertDetailPage wraps AlertDetailView; AlertsPage is the sole embedded host.
ShellNavigationService offers relative alert routes to IDetailHost first.
Alerts list, device active alerts, dashboard and rule alerts use GoToAlertAsync
with a qualified source key and device ID. NotificationRouter handles notification
and widget targets, including moved legacy targets and group-list routing.
Pushed detail opens device/rule descendants relatively; rule opens template,
template opens related rule. Back pops to the actual caller in both pushed and
embedded origins. #447/#448 content and routes are unchanged. Alerts/Devices
root lists are outside scope; their IDs, selection and inset ownership remain.

The pinned Core package is 2.0.0-ci.52. Existing calls to AlertActions retain
single-fault scope verification, group readback, per-resource/global permission
refusals, partial failures and result.Summary. Only verified Changed entries feed
self-action tracking. Embedded group events trigger a server group refresh,
not an optimistic acknowledgement of every sibling. No permission policy,
credentials, API or package changes. Local Core source was inspected for the
scope/result contract; no installed ci.52 package is present on this machine,
so exact package compilation remains CI verification.

Notification Change/Undo still apply/restore NotifyRules immediately. Back does
not discard those changes. Notes entered for acknowledgement retain the existing
trim-on-submit contract. Server notes, procedure and condition render as plain
text with source whitespace preserved; no HTML or timeline features were added.

## Findings assessed before implementation

| Concern | Baseline assessment and disposition |
| --- | --- |
| Fault/group scope and permissions | Compliant contract: distinct group and verified fault controls. Added explicit group scope and confirmed counts; unavailable never invents zero. |
| Header status | Fix required: state omitted from visible header; now explicit beside severity. |
| History | Fault history matching and incomplete recurrence are compliant. Fix required: legacy unreadable log silently omitted; now unavailable with no invented empty history. |
| Long notes, source and multiple entities | Fix required: condition trimmed/reformatted; shared RuleText.ConditionSource preserves source. Full-width stacked wrapping field labels/values remove constrained columns. Fault selection and Show all fields unchanged. |
| Missing rule | Fix required: silent omission; now unavailable with pull-refresh retry guidance. |
| Busy/error | Fix required: native spinner and first-load-only busy card; shared busy overlay now handles initial/refresh/action work, with explicit Retry. |
| Actions and races | Fix required: independent commands remained executable during other work. Busy CanExecute notifications added; pending fault dialogs share a gate and recheck displayed item/permissions before writing. Legacy acknowledge rechecks after prompt. Loads serialise to prevent interleaved source keys/payloads. |
| Notification persistence | Compliant immediate behaviour; now explains automatic local saving. Change target raised from 32pt to 44pt. |
| Hierarchy/accessibility | One primary acknowledge group/legacy action, subordinate unacknowledge/fault/device/notification controls. Removed duplicate gesture-only device link; full device text plus explicit Open device remains. Action semantics on hardware unverified. |
| Pushed glass/insets | Fix required: legacy safe area without pushed glass lifecycle. Native top-only iOS page, relative caller-labelled Back, retained/disposed edge watcher and Window resume; Moved removes before adding. |
| Embedded glass/insets | Compliant host boundary retained; no new embedded glass owner. Native scroll adjustment disabled on handler/load. |
| Fade and final item | Fix required: 20pt input-transparent fade reserved only with hidden BackBar, absent with visible BackBar. Measured Undo height, bottom margin and gap reserved in scrolling content. |
| Theme contrast, wrapping, folding/rotation/resume | Unverified on hardware. No acceptance inferred from static inspection. |

Affected consumers are only pushed AlertDetailPage and embedded AlertsPage;
shared model changes feed those views. No global helper/resource changes.

## Validation evidence

Full-diff implementation self-inspection and git diff --check only. No local
builds, tests or scripts run. Regression tests cover exact condition/notes/
procedure whitespace and honest unavailable legacy history; existing read-failure
expectations updated. Existing AlertScopeActionTests cover scope/refusal contracts.
CI shared tests, Android, iOS simulator and CodeQL remain pending at handoff.
Parent Claude Opus 5.5 Medium full-diff review is separate and required; this
self-inspection does not satisfy it. No review approval or merge claimed.

## Exact device gaps

All hardware evidence remains unverified. Record head/build, device/OS and
diagnostics per result, using synthetic example.net devices and documentation IPs.

1. Duo outer, inner portrait and inner landscape: each list/dashboard/device/
   rule/notification/widget entry; pushed and embedded detail to device, rule,
   template and related rule, then all Back returns. Confirm IDs/selection and
   actual caller labels, one glass owner and Android/older-iOS header fallback.
2. Ordinary iPhone: repeat entry/return, native inset and glass reclamation after
   resume on each descendant. Confirm top content and final field reachability.
3. Both themes, largest text: long unbroken rule/device/field names, CRLF/tabs/
   trailing spaces, literal markup, long notes/procedure and many entities. Check
   complete wrapping, code contrast and no clipped status/actions.
4. VoiceOver/TalkBack: hear severity/state/scope, activate every 44pt action once,
   read complete source, Change and Undo, Retry and Back; check focus on return.
5. Slow first load/refresh, gone/recovered alert, denied rule/log/group, partial
   history, incomplete group reads, partial writes and per-resource refusal.
   Verify unavailable wording, Core result text and no optimistic sibling state.
6. Concurrent fault/group taps, refresh while prompt open, changed selection while
   prompt open, retry after write/read failure. Confirm no wrong-target write.
7. Fold/rotate/resume while loading, on descendants and with wrapping Undo visible;
   final content must clear measured toast and both bar orientations. Repeat with
   Reduce Motion and inspect diagnostics for overlap/placement loops.

Parent owns full review, CI, merge/cleanup and only then section TestFlight.
If review quota is exhausted, retain the unmerged PR for later sequential review.
#449/#442 stay open with no checklist ticks. TestFlight 41618582199 and the
previous uploaded build are untouched.
