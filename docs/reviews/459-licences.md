# Open-source licences — #459

Refs #459, parent #442. Baseline main `2c7f32f`; implementation is the
commit carrying this assessment. No issue closure or epic acceptance implied.

## Scope, routes and source contracts

Read live #459/#442/#454, CLAUDE.md, full docs/DEVELOPING.md, docs/DESIGN.md
and DESIGN-REFERENCES.md. No FULLDESIGN.md exists in this checkout; DESIGN.md
is the repository's authoritative full guide. Read #496 Settings and #500
What's new implementation read-only for entry and lifecycle patterns.

Settings About invokes SettingsViewModel.OpenLicencesCommand, relative
Routes.Licences; AppShell registers a transient LicencesPage and model. Back
pops to the actual caller, including Settings pushed from More. External
browser return retains this page; retained More selection uses IActivePage
from merged #490. No persistence or settings writes occur here. Settings
forms and entry ownership remain #454/#496; no queued files are changed.

PhoneNotices.For filters Core shared plus phone-owned notices by platform in
source order. Catalogue is handwritten, not generated. Core's pinned package
owns OpenSourceNotice, platform filtering, embedded licence resources and
ReadLicence (manifest resource, StreamReader.ReadToEnd), plus trademarks.
Inspected source at core/src/DesktopNMS.Core/Licences/OpenSourceNotices.cs in
the adjacent Core checkout as provenance evidence; CI tests against the pinned
package remain authoritative. Mobile owns its additional PhoneNotices entries.
No catalogue, resource, package version, copyright, embedded Leaflet notice,
font notice or source whitespace is edited. Completeness of the inherited
catalogue against every transitive dependency remains unverified; this is
presentation work and makes no new legal claims or legal interpretation.

Modes: platform-specific initial collapsed catalogue; each embedded licence
expanded/collapsed independently; attribution-only OpenStreetMap; missing,
empty or unreadable resource with Retry; browser round trip; nested/retained
return and resume. Reads are synchronous embedded resource reads, so there
is no network loading, parsing or server support state. The fixed catalogue
has no runtime empty-source mode. A failed read leaves content null and retries
on activation rather than caching an error as legal text.

URLs remain the exact trusted catalogue Uri passed through existing
ILauncherService to the system browser. No arbitrary body links, WebView,
markup parsing, credentials, telemetry, network fetch or allowlist change.
Legal bodies are plain Text labels; whitespace is retained in model strings.

## Findings and disposition

| Concern | Baseline assessment | Implementation / remaining evidence |
| --- | --- | --- |
| Package/licence hierarchy | Fix required: licence and destination combined in small caption | Package H2, licence H3, use and copyright, full destination and separate disclosure |
| Complete attribution | Compliant for inherited catalogue; transitive audit unverified | Every inherited item and resource retained, exact-resource regression coverage added |
| Long legal notices | Fix required: 11.5pt monospace | Scalable normal body text, character wrapping, no limit or source transformation |
| Long URLs and actions | Fix required: tap-only small caption | Full URI wrapping label measured independently of native whole-row button, minimum 44pt; child label excluded from accessibility |
| Scrolling/final content | Fix required: capped column and legacy inset ownership | Finite page ScrollView, full width gutters, scrolling header, final bottom glass clearance |
| Back and lifecycle | Fix required: header-only ownership | Actual previous title, relative pop, page glass owner; retained activation/resume, paired Moved/Loaded/HandlerChanged with central AppShell resume delivery, disposed edge watcher |
| Fade | Fix required with migration | 20pt within viewport, matching background RGB and width, input transparent, reserved only with hidden header Back; none above visible Back |
| Read failure | Fix required: cached replacement text | Separate truthful error and target-named Retry; website always available; no fabricated licence text |
| Themes/accessibility | Device evidence unverified | Semantic headings, normal themed body, one accessible native action per row; no tiny monospaced body |

No shared helper changes. LicenceAction is local to this page's namespace and
has one consumer; its native button overlays independently measured wrapping
text to avoid multiline Button measurement clipping. No entry/form changes.

## Evidence and dependencies

Full intended diff self-inspected against the guide; git diff --check is the
local whitespace check. Regression tests added for every platform item,
complete resource equality including whitespace through expand/collapse,
full destination equality and missing-resource retry. Tests were **not run**:
user prohibits local builds/tests/scripts. No independent review attempted.
CI shared tests and Android/iOS builds remain pending, separate from review
and device evidence. No device/build evidence is available for this change.

Mandatory Claude Opus 5.5 Medium full-diff review belongs to the parent after
quota reset, one PR at a time: #489 → #491 → #492 → #493 → #494 Map → #495 →
#496 → #497 → #498 → #499 → #500 → this PR. Leave unmerged; parent updates
only this PR at its turn after prior merges, then handles review/CI/merge and
cleanup. No watch, release, closure or ticks. TestFlight follows #455–#459.

## Exact pending device matrix

All rows below are unverified. Record commit, CI build, OS, device and outcome
per row; CI cannot establish these results.

| Scenario | Required configurations and expected evidence |
| --- | --- |
| All packages | iOS and Android: compare every item/order/copyright/licence with PhoneNotices.For; Android-only packages absent on iOS |
| Full source | Expand every embedded resource, compare beginning/middle/end with pinned Core source including embedded notices and whitespace; collapse/reopen remains complete |
| Long notice/URL | Longest Apache/font notice and longest URL at maximum text: no clipping, complete last paragraph and website reachable, wrapping changes no text |
| Link/return | Each catalogue destination opens system browser; return preserves page and disclosure; Back reaches actual Settings caller, nested Settings then More |
| Screen reader | VoiceOver and TalkBack: H2/H3 order, full legal body, exactly one label/action per website/disclosure, target-named Show/Hide/Retry, activation once, actual previous-title Back |
| Large text/themes | Default and largest accessibility text in light/dark: readable contrast, every action at least 44pt, final trademark and link clear glass |
| Duo geometry | Outer portrait, inner portrait, inner landscape: repeat long notice/link and final scroll checks; fold/unfold and rotate while expanded without reopening |
| Retained/resume | Switch away from retained More stack and back; browser return; background/resume while expanded; one glass owner, no duplicate inset or observers |
| iPhone | Ordinary iPhone, both themes/default and largest text, portrait/landscape: glass Back where available and header fallback, final content reachable |
| Failure | Controlled missing/unreadable resource: separate error, repeat Retry, no replacement legal body, website usable; do not ship changed production resources |

User acceptance remains required after device evidence; #459 and #442 stay open.

## Independent review and remediation

Claude Opus 5.5 Medium reviewed the full diff at 964ab45 against main 2c7f32f.
The local Window resume hook and repeated TopBar.Fit are removed. Licence prose
uses word wrapping; URL action text retains character wrapping. Actions use the
shared themed action colour, and website actions announce their browser hint.
Empty error labels are hidden through HasLoadError. Failure announcements remain
a device accessibility check rather than a claimed result.

All CI checks passed at the reviewed head. Replacement CI for these corrections
is pending; no local builds/tests ran. Check the longest complete notice at the
largest text size on iOS, including its final paragraph, for native Label drawing
limits. Transitive catalogue completeness and all hardware outcomes remain
unverified. Issue closure and epic ticks still require explicit acceptance.
