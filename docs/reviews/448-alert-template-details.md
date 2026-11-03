# Alert template details review, #448

## Baseline and contracts

Reviewed main at 5144ef6 after #487, live #448/#442, CLAUDE.md,
DEVELOPING.md, DESIGN.md and DESIGN-REFERENCES.md, and the #447 evidence.
There is no FULLDESIGN.md in this checkout; DESIGN.md is the current guide.

AlertTemplatePage.xaml and its code-behind are the only template view.
AlertTemplateViewModel occupies the final section of AlertRuleViewModel.cs.
AppShell registers the relative alerttemplate route. AlertRuleViewModel passes
the selected template ID from both the pushed AlertRulePage and the wide
AlertRulesPage's embedded AlertRuleView. Both open a pushed template page.
Template related-rule rows pass the rule ID through the relative alertrule
route. ShellNavigationService lets the current IDetailHost intercept only when
it supports that route; the template is not a detail host, so descendants push.
Back pops to the template, then to the original pushed rule or embedded list.
BackBar.PreviousTitle supplies the actual previous page title, including the
Alert rules host title for embedded origin. Hardware return behaviour is unverified.

The page reads AlertTemplates.GetAsync and Rules.ListAsync only. It has no
editor, Save, notification toggle, persistence or write API. Session/client
authentication and TLS handling remain unchanged. Source labels remain plain
text, including server markup and expressions. No credentials are rendered or
persisted by this page. #446 list/split ownership and #447 rule body/wrapper
remain unchanged; only the template view-model section in their shared file changes.

## Scope assessment

| Concern | Baseline finding and outcome |
| --- | --- |
| Metadata | Fix required: no visible ID; now shows server ID and assignment count. Unreported default status remains unknown. Missing assigned rules are counted without inventing names. |
| Title and recovery title | Fix required: Trim changed source; retain exact strings including whitespace. Wrapping themed code labels remain. |
| Body and long content | Fix required: line-ending replacement and TrimEnd changed source; preserve exact text and wrap visually. Remove sideways nested scroll and readable-width cap. Plain text never interprets server markup. |
| Related rules | Fix required: truncated conditions and no explicit disabled state; wrapping rows retain dot, ListRow padding, divider and chevron, with a joined name/summary/condition description and 44pt minimum target. Real screen-reader activation remains unverified. |
| Loading and failures | Fix required: rules failure blocked all content; fetch template first, display it while rules load, preserve only current assignment IDs from prior rows on failure. State unavailable explicitly and offer pull refresh. Main errors have one Retry and explain retained content. Missing template retains last content with a missing-source message. |
| Empty source | Compliant: no explicit assignments is distinct from default-template status. Empty source body remains empty; no invented expression or fallback body. |
| Navigation and security | Compliant relative routes and IDs retained; read-only API and session handling unchanged. Added caller-labelled glass Back for the pushed template only. |
| Insets and fade | Fix required: remove legacy iOS safe-area flag; page owns top only, native scroll adjustment disabled, scrolling column owns sides/bottom. Input-transparent 20pt fade has a spacer below visible BackBar. No toast exists on this page. |
| Lifecycle | Fix required: scoped covered-edge watcher, paired glass Moved subscription, retained Window resume subscription. Dispose/unsubscribe on disappearing; same-view native handler hooks are attached once. Resize recalculates padding. |
| Search and setup | Compliant: neither exists on template details; no search migration or unrelated setup edits. |
| Themes, large text, fold and resume | Unverified on hardware; exact checks below. |

## Review and CI evidence

Implementation self-review inspected the full diff, navigation callers, source
preservation, read-only contract, scoped file ownership and inset lifecycle.
git diff --check passed before commit. Regression tests cover exact CRLF and
trailing whitespace/markup, honest default status, relative rule route/ID,
related-rule failure after assignment changes and successful retry.
No local builds, tests or scripts ran. CI is pending at handoff. Parent must
obtain Claude Opus 5.5 Medium full-diff review before merge. If quota is exhausted,
leave this PR unmerged for review after reset. Self-review is separate evidence.

## Remaining device checks

All device results remain unverified. Record head/build, device/OS, result and
diagnostics for each check:

1. Duo outer, inner portrait and inner landscape: narrow pushed rule to template,
   wide embedded rule to template, template to related rule and Back twice.
   Verify exact IDs, actual previous-title label, retained embedded selection,
   one glass owner, final row reachability and source text below the fade.
2. Ordinary iPhone: same entries and returns, iOS glass and older-iOS BackBar,
   top-only native inset, side/bottom clearance and resume on each descendant.
3. Both themes and largest text: long template name, alert/recovery titles,
   CRLF/tabs/trailing spaces, long unbroken expressions and literal server markup.
   Check wrapping, code contrast, complete content and no clipped row state.
4. VoiceOver/TalkBack: activate entire related-rule rows once, hear name and
   enabled/disabled severity summary, read complete source, activate Retry and
   Back and verify focus on return. Confirm nonblank joined descriptions and
   44pt targets without duplicate child announcements.
5. Slow first load; empty body/titles; no assignments; inaccessible assigned IDs;
   missing/denied/offline template; rules-only failure initially and after success;
   assignment changes followed by failure; pull refresh and main Retry. Check
   truthful loading/stale/unavailable wording and no false default status.
6. Fold/unfold, rotate and resume while on template and related-rule descendant.
   Check glass reclamation, footer reachability, fade position and diagnostics
   for overlap or placement loops. Repeat with Reduce Motion.

#448 and #442 remain open. No epic ticks, device acceptance or release is claimed.
The parent's ongoing TestFlight run 41618582199 is untouched. The next section
TestFlight remains after #449.
