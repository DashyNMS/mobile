# Alert rule details review

Refs #447, epic #442. Baseline main f90c01696fa43c0ee40c2b018e53d30fed7d28e2,
after #446 / PR #485. Read live #447, #442, #446 and #448, CLAUDE.md,
DEVELOPING.md, full DESIGN.md, DESIGN-REFERENCES.md and 446-alert-rules.md.

## Scope and preserved contracts

AlertRulePage wraps AlertRuleView for narrow list navigation and rule links
from AlertDetailViewModel and AlertTemplateViewModel. Routes.AlertRule carries
the integer RuleIdParameter. ShellNavigationService offers this route to the
current IDetailHost first; AlertRulesPage embeds it only while split. The wide
pane hides Back, retains the list's glass owner and accepts column-reaching
clearance through PlaceContent. #446 owns list, selection and that boundary;
this change does not edit its host. Narrow pushes have their own Back/glass.

Rule descendants are its alerts and the matched template's integer ID. Relative
routes preserve the actual stack; returning to a pushed rule reapplies Back,
glass and placement. Returning to the wide list reapplies the host's placement.
Rotation/folding and resume use the existing covered-edge and host lifecycle.
Template presentation remains #448, including its related-rule caller.

The server rule is read-only. No API writes, permissions, credentials, settings
format, route or template matching changes. Recovery/acknowledgement omission
still means On; per-entity omission remains Not reported; missing maximum
remains Inherited. Override SQL takes precedence, builder formatting stays
Core-owned, inversion and compatibility advice remain unchanged. Notification
Change and Undo still immediately apply/restore local NotifyRules preferences;
Back does not cancel them. They are now labelled as automatic local changes.
There is no primary save action. All actions remain subordinate.

## Findings and disposition

| Concern and baseline trigger | Assessment and resulting change |
| --- | --- |
| Settings Grid auto value columns on narrow screens or large text | Fix required: full-width wrapping label-above-value sections, without fixed widths |
| ReadableColumn cap in wide pane | Fix required: full-width Grid within the existing scrolling view |
| Long template name TailTruncation and gesture-only row | Fix required: wrapping native button, full name, same template ID; default/unavailable text is not actionable |
| Alert gesture rows, fragmented accessible content | Fix required: whole native button containing device, severity, state and age, wrapping and at least 44pt |
| Condition BreakCondition and source trimming | Fix required: remove inserted display newlines and preserve SQL/legacy text, notes and procedure whitespace exactly; builder remains interpreted by Core |
| Entity limits, unknown capability semantics, disabled/severity header | Compliant: unchanged values, one disabled header phrase and one severity pill, no invented capability |
| Compatibility advice before/after upgrade | Compliant: unchanged Core advice, wrapping complete text |
| Code surface in both themes | Compliant statically: existing themed fill and readable themed text retained; actual contrast unverified |
| Pushed legacy safe area and missing glass lifecycle | Fix required: native top-only iOS page; supporting layouts None; scroll adjustment Never on handler/load; relative glass Back with actual caller title; Android/older iOS retain BackBar |
| Embedded glass and insets | Compliant host boundary preserved: host passes clearance, detail pads scrolling content, no independent glass owner |
| Undo obscuring final content | Fix required: measured overlay height plus bottom margin/gap reserved in content; sides and bottom clear supplied glass bounds; recalculates on visibility/size and host placement |
| Top content under fade | Fix required: input-transparent themed 20pt fade with matching initial content reservation |
| Busy/pull refresh | Compliant: retained shared BusyCard and hidden native spinner, no clearing or forced scroll |
| Failed alerts/templates represented as empty/default | Fix required: explicit retry status, no false empty/default, last good rows/template retained; stale generations cannot overwrite newer supplementary content |
| Failed target-name lookup | Fix required: explicit retry status and retain existing names; first load falls back to actual IDs |
| Initial/empty/error | Compliant after fixes: empty only after successful alerts response, main error has Retry, prior rule content retained on failure |
| Descendant return, resume, folding, screen-reader activation, text fitting and contrast on hardware | Unverified: exact device matrix below |

Only AlertRuleView consumers are affected: pushed AlertRulePage and wide
AlertRulesPage. Shared AlertRuleViewModel changes affect those same views;
AlertTemplateViewModel shares the source file but is unchanged. No global
controls/resources or list selection changes.

## Validation evidence

Implementation self-review covers the full diff, source fidelity, routes,
read-only server semantics, immediate local persistence, inset ownership,
overlay clearance and accessibility. Regression tests added for exact SQL,
notes/procedure, unavailable initial supplementary sources and retention of
last good alerts/template navigation after failure. Existing entity/compatibility
and route tests remain. No local builds, tests or test scripts run.
Whitespace validation is performed with git diff --check before commit.

CI: pending pushed-head shared tests, Android, iOS simulator and security checks.
Independent review: pending parent-mandated Claude Opus 5.5 Medium full-diff
review. The implementation agent has started no agents or review delegates.
Parent must complete review, CI, merge and branch cleanup before #448.

## Device evidence and exact gaps

All device outcomes are unverified. No simulator/hardware results or build
acceptance are claimed. Record build/head, OS/device, result and diagnostics
for each of the following before user acceptance:

1. Duo outer, inner portrait and inner landscape: narrow list push and wide
   embedded rule, alert-to-rule and template-to-rule entries, rule-to-template
   and rule-to-alert descendants and Back. Verify IDs, actual caller labels,
   header fallback on Android/older iOS and one glass owner on supported iOS.
2. Ordinary iPhone: repeat pushed entry/return and resume with iOS glass Back;
   verify native top-only clearance, scroll bounds and last-item reachability.
3. Both themes and largest text: long title, full SQL including whitespace,
   notes/procedure, unknown per-entity flag, inherited limit, compatibility
   advice and long template name. Check no clipping, complete wrapping,
   readable code contrast and no duplicate disabled/severity announcements.
4. VoiceOver and TalkBack: activate whole template/alert buttons and notification
   Change/Undo once, hear complete names/state and target, reach 44pt actions,
   confirm focus after return and complete source text reading.
5. Slow first load, empty successful alerts, rule missing, denied/offline alerts,
   templates and target lookups, failure after success and Retry/pull refresh.
   Verify one busy overlay, no native spinner or scroll jump, retained content
   and no false default template or empty alerts claim.
6. Fold/unfold, rotate and resume in both modes and while on descendants or
   showing Undo. Confirm recomputed sides, final-item clearance above measured
   toast/glass, automatic local notification persistence and Undo restoration.
   Repeat with Reduce Motion; inspect diagnostics for placement loops/overlap.

#447 and #442 remain open. No epic ticks or acceptance inferred from CI.
