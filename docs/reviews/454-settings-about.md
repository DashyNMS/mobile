# Settings overview and About assessment

## Current approved correction, 4 November 2026

Refs #454 and #442. Baseline main `cf11a4171371febe229ef8ebd47f41f3a411174a`,
including merged More #523. The user explicitly said "Approve as proposed"
for `/tmp/mobile-442/settings-proposal.md` and `settings-proposal.html`.
Both exact artifacts were read before implementation. This approves the visual
decision only. #454 remains open and its epic checkbox remains unchecked.

The trigger was the user's 4 November device feedback on #454: the landing
page looked blockier, the server logo was lost, and sections were smashed
together. Historical review of #496 did not establish acceptance of that layout.
The current correction replaces its bordered groups with separated, quiet
headings and hairline rows, restores the packaged repository mark, and retains
the complete embedded About content and every summary.

### Ownership and approved source map

`SettingsOverviewRow.cs` belongs to the mobile head. A repository-wide consumer
search found only `SettingsPage.xaml`, including server/backup, preferences,
services, About links and diagnostics rows. No accepted form or More consumer
uses it. Its optional ShowLogo property is enabled only on the server row;
ShowChevron is false for the backup explanation and explicit diagnostics share
action, because these do not navigate. No global row or setup helper changed.
The native button covers the measured row; excluded labels/logo/chevron do not
add duplicate actions. Pressed/focused states use the existing PrimaryTint.

| Location, mode and trigger | Assessment and expected correction/source contract |
| --- | --- |
| Server identity, active/disconnected/long host | Corrected visual loss: packaged dashynms_logo.png from the repo SVG, 54pt beside wrapping ServerHost and complete ServerSummary. Existing Not signed in / No active connection / unknown / HTTP warning remain. Transport never claims reachability. |
| Backup active | Compliant independent ExplainCommand, full ServerRowText and warning text colour. No nested server target or new probe/failback behaviour. |
| Preferences, narrow/wide | Corrected blockiness: Appearance, Devices, Alert checks, Notifications, Health thresholds then conditional Lock screen widgets. All original summary bindings and threshold explanation remain. Lock screen moves out of Services. |
| Services, enabled/off/invalid source | Compliant GraylogStatusText, UnimusStatusText, SensorDefinitionsSummary. Saved enablement, host-only source and unavailable/not-downloaded states remain honest. No credential/path/query display or connectivity assertion. |
| About, normal/long/missing/cached versions | Compliant ComponentVersions.Build source. App/Core/Sensor definitions retain name, full DisplayVersion and State in read-only stacks. Only metadata is omitted for display; diagnostic headers keep full versions. Missing is Unavailable, never zero. Disabled, Not downloaded, Enabled and cached update failure remain distinct. Added hairlines. |
| About links and release notes | Compliant full conditional WhatsNewSummary and OpenWhatsNewCommand; exact AppLinks PrivacyPolicy/Support/Website and OpenLicencesCommand remain. No new About route or child edit. |
| Diagnostics availability and consent combinations | Compliant conditional HasDiagnosticsCard, CanChooseSharing, CanShareDiagnostics and CanHideNamesInDiagnostics. SharingSummary remains complete. Hide names has its own 44pt native switch beside excluded explanatory labels; no parent gesture. Full share confirmation, redaction choice and explicit system sheet remain. |
| Sign out, cancel/ordinary/forget | Compliant existing SignOutCommand and exact chooser/second confirmation. Isolated by 38pt space; no shortcut or new deletion. Ordinary sign-out retains saved server/settings; forget additionally clears service secrets/data/catalogue, keeps diagnostics log. |
| Footer and secret action | Compliant exact AppVersion and Trademarks bindings. Existing three quick header/version taps retained; logo is decorative. |
| Persistence and permissions | Compliant unchanged VM/children. Seven immediate sections keep their automatic-save contracts and dependent fields. Draft thresholds/services/catalogue retain Save/Back, validation, draft Test connection/trust and immediate confirmed secret removal. Permission refusal still says Notifications are off and explains continued checks/system permission. No overview automatic-save claim. |
| Root/pushed/child/tab return/resume | Compliant source retained: IActivePage.Refresh, idempotent edge watcher and Moved subscription, central Shell resume owner, actual caller Back. No local resume fanout or new lifecycle mechanism. Hardware operation UNVERIFIED. |
| Usable viewport, fade and final action | Compliant source retained: star viewport, iOS top-only native owner, native scroll adjustment disabled, Android Container edges, measured ForScrolling sides/bottom, single 16pt gutters and footer. The 20pt reserve/fade uses actual page Background resources. No guessed offsets introduced. Actual native base, top/side/bottom pill bounds, overlay clearance and initial/final reach remain UNVERIFIED. |
| Themes, maximum text and accessible actions | Wrapping, uncapped full-width column, minimum 60pt rows and existing caption/warning colours retained. Native joined action names and independent switch remain. Shared translucent PrimaryTint/CriticalTint feedback added; contrast against each theme remains unverified. Actual contrast, speech, focus, rapid activation and fit UNVERIFIED. |
| Keyboard/search/loading/retry | N/A on overview: no editable fields, search or network list. Existing unknown saved/session states and all loading/refusal behaviour in children unchanged. |

Accepted #405/#412 forms, #457 consent, #459 licences and #450/#451/#460
remain outside this UI change. #458 owns What's new. Open documentation PR #515
retains fixed-action alignment ownership. More #523 is the direction baseline;
no More edits, unrelated worktrees or dirty #475 changes are included.

### Current evidence and remaining gates

Source diff and whitespace inspection only. No local builds, tests or scripts.
No model semantics changed, so no new model tests were added. Android and iOS
MAUI compilation belongs to CI; XML/source inspection is not proof of MAUI
compilation. The repository image pipeline already packages this mark for
SignInPage. No static-string Path.Data was introduced after #523's MAUIX2002.

Independent Claude Opus 5.5 Medium full-diff review: PENDING, parent-owned.
Shared tests, Android, iOS and CodeQL CI: PENDING at handoff.
Device/user acceptance: ALL UNVERIFIED. No build or OS result is available.
The PR head SHA identifies the exact source revision; record the eventual build,
OS and result against that SHA before acceptance. Proposal approval is not a
native pass. Parent owns subsequent review, CI judgement and merge; no merge,
watch, release, closure or epic tick is performed by this implementation task.

The exact pending matrix below remains required for the new PR head. Every
combination is UNVERIFIED: Duo outer portrait/landscape, inner portrait/landscape
and ordinary iPhone portrait/landscape; light/dark; default/maximum OS text;
VoiceOver/TalkBack as supported. Repeat cold launch, fold/unfold, rotation while
scrolled, resume, pinned-tab return and all child returns without reload.
Check top/side/bottom glass, actual usable base, initial fade text and final
Sign out/footer reach. Exercise long example.net hosts and full prerelease/build
versions, every unavailable/disabled/cached/service state, diagnostics visibility,
privacy/share confirmations, permission refusal and both sign-out paths using
synthetic data only. Record commit, build, device, OS, theme, text size, reader,
trigger, actual result and expected correction for each failure. No credentials
or real network identity belongs in evidence.

### Independent full-diff review disposition

Claude Opus 5.5 Medium reviewed b7386c4 against cf11a41 with no blockers.
Parent substantiated all four findings: group labels now expose Level2
headings; the post-What's-new divider appears only when that row is visible,
avoiding a doubled last-version line; the sharing divider requires both
sharing rows, avoiding a line through the Diagnostics heading; only the
hostname title character-wraps, while summary prose keeps word wrapping.
The logo's fixed horizontal alignment moved to its initialiser. Feedback
uses shared translucent tints in both themes, with actual contrast unverified.
Fresh full-diff review and replacement CI are pending. No local tests/builds
or physical acceptance are claimed. Verify all diagnostics visibility
combinations and hidden release notes, headings navigation, maximum-text
host/summary wrapping and press/focus/ripple contrast on the installed head.

The fresh full-diff Opus review at a3b1ed0 confirmed the previous fixes and
identified inappropriate negative-margin RowLine use between overview rows.
Parent traced App.xaml's -11pt margin and fixed all Settings overview lines
to use the plain input-transparent Divider; explicit version/sign-out margins
remain. This prevents overlapping row bounds and uneven spacing/tint bleed.
Title-only text now centres against chevrons, and Hide names has a conditional
leading divider. All three findings are fixed; no source/command semantics
changed. Another full-diff review and replacement CI remain pending. Native
spacing, actual press/focus state precedence and conditional visibility still
require installed-build checks, never inferred from source.

## Historical #496 assessment and review provenance

The remainder records the previous implementation and its then-pending checks.
Its branch names, statuses and "new" control claims are historical, not the
current correction's status. The original scope and detailed device steps are
retained for regression coverage.

Refs #454 and #442. Baseline main 23eff15 (after #495), branch
`t3/settings-about-overview`. Read live #454/#442/#453/#457/#458/#459,
CLAUDE.md, DEVELOPING.md, the full DESIGN.md and DESIGN-REFERENCES.md,
#405/#412 and PR #441 acceptance. DESIGN.md is the current guide. PR #495's diff and assessment were inspected read-only; it
owns More and does not change Settings or its view model. No queued branch edited.

## Assessment before implementation

| Concern and trigger | Baseline classification | Resolution |
| --- | --- | --- |
| Full-width overview on Duo | Fix required: ReadableColumn caps width | Remove cap; 16pt gutters plus measured side clearance |
| Long service summaries and release preview | Fix required: TailTruncation hides text | Wrapping rows with finite width |
| Long component versions/state | Fix required: Auto value column can crowd names | Stack name, version and state, wrap full display value |
| Whole-row navigation/accessibility | Fix required: gesture grids lack native action semantics and joined names | Settings-only row with wrapping labels and one native button covering its whole measured area; joined nonblank description; decorative children excluded |
| Backup explanation | Fix required: nested short label tap competes with server tap | Separate 52pt backup explanation row, same command and conditional visibility |
| Transport/version missing | Fix required: disconnected summary claimed HTTPS; blank version not unknown | Explicit no active connection and unknown version |
| Service state/source | Fix required: raw Graylog URL can contain user info; invalid enabled Unimus appears Off | Host-only source, unavailable address text; On means configured enabled, never a connectivity claim |
| About version format and missing data | Compliant: ComponentVersions sanitises version-shaped input, retains app/build, hides metadata only in display; sensor cache/failure/disabled/not-downloaded distinct | Preserve helper and diagnostics format; no fabricated version or zero |
| Privacy/support/site links | Compliant: AppLinks and launcher own destinations | Preserve exact links and commands |
| Diagnostics sharing and hide names | Compliant contracts; row accessibility fix required | Preserve visibility, opt-in defaults, exact explanations, confirmation and stored preference; name switch |
| Sign out | Compliant destructive workflow; device operation unverified | Preserve confirmation, cancel, token deletion and separate forget-everything confirmation |
| Insets/fade/final row | Fix required: native safe area with no measured glass/footer/fade | Single iOS top owner, disable native scroll adjustment on handler/load; measured content/footer; matching background 20pt fade below fixed header with 20pt content reserve |
| Retained stack/child return/resume | Fix required: OnAppearing only, no glass ownership | IActivePage activation, owner-safe Hide, idempotent Moved/watch, central AppShell resume delivery without a page-local Window subscription |
| Theme and larger text | Unverified on hardware | Caption resources, wrapping, no fixed value width; exact matrix below |

## Modes, routes and contracts

Settings may be a pinned root or pushed from More. AppPages.Resolve, tab IDs,
three-pin limit and preference format remain unchanged. Root has no Back;
pushed Settings uses relative `..` and actual PreviousTitle, glass on iOS 26+
and header on older iOS/Android. AppShell's #490 activation resolves the visible
page after retained navigation and on resume. Returning from a child refreshes
summaries. About is embedded, not a separate route or invented About page.

Server, Appearance, Devices, Alert checks, Notifications, Lock screen and
Diagnostics use relative SettingsSection with the enum parameter. These share
SettingsViewModel and retain immediate saving, permission prompts, dependent
visibility and rule-list semantics accepted in #412/PR #441. Thresholds,
Graylog, Unimus and Sensor definitions retain their existing relative routes,
draft/Save/Back contracts, secret stores, immediate secret removal confirmation
and saved-source Update now behaviour. No accepted child layout was migrated.
What's new, licences and browser links retain their existing commands; child
body ownership remains #458/#459, consent presentation #457. No seen-version,
licence text, opt-in default or privacy promise changed.

Diagnostics entries remain conditional on sharing/report availability; Hide
names still persists immediately. Sharing still confirms the exact payload,
uses the existing report redaction and explicit system share sheet. About
versions and diagnostic version headers remain free of server/device names,
credentials, paths and source repositories. Service summaries now show only
parsed host names, never URL credentials/path/query; unavailable addresses are
explicit. Their On/Off state describes saved enablement, not live availability.

Sign out cancels without mutation. Confirmed ordinary sign out deletes the
sign-in token, clears bookmarks/recents/exports and session/watch/widget state;
retains saved server and other settings. Forget everything also clears servers,
trusted certificates, Graylog password, Unimus token, preferences/data and sensor
catalogue; diagnostics log remains as promised. Existing persistence/schema and
Core 2.0.0-ci.52 pin are unchanged. No new Core API is used; existing Session,
settings and ComponentVersions members are reused. Package binaries are absent
locally, so compilation/API verification belongs to CI.

Only shared presentation getters change: SettingsSectionPage consumes server
version; no setters or saving changed. SettingsOverviewRow is new and used only
by Settings; existing accepted forms/shared layout helpers are unchanged.
Native Button owns CanExecute/disabled operation, toolkit async commands prevent
repeat execution of the same command; glass Back has a re-entry guard and
observes navigation failure. Labels reflect disabled state. Native accessibility
and rapid mixed-command navigation remain explicit device checks.

No list loading/retry machinery is invented: this overview reads saved settings
and session metadata. Unknown server version remains unknown; service state is
configuration. Sensor versions retain cached/update-unavailable/not-downloaded
states through ComponentVersions. Retrying catalogue updates remains in the
existing child. No fake network success or availability is introduced.

## Evidence and dependencies

Claude Opus 5.5 Medium reviewed full diff ac51cde against main 23eff15.
All findings were addressed: explicit IView overloads fix the CI compile error;
Graylog and Unimus use their existing Core parsers for bare hosts/ports while both
show only host names; local resume and repeated header subscriptions are removed;
the overview no longer claims all child forms save automatically; backup source
keeps its warning colour. Regression coverage adds bare-host, host-with-port and
credential/path/query exclusion for both services.

Full intended/staged diffs and whitespace checked. Unit tests and CodeQL C#/actions
passed at the reviewed head; Android failed with CS0121, now corrected. Replacement
CI remains pending. No local builds/tests ran. Hardware outcomes remain unverified.
A second Claude Opus 5.5 Medium full-diff review at f5ec842 confirmed all earlier
findings fixed, including Core support for credential-bearing URLs. Follow-up
low findings are addressed: Graylog now reuses Core validation (including rejection
of unsupported schemes), Android declares Container safe areas as More does,
card dividers are restored and Hide names has consistent title size and a full
spoken explanation. Added invalid-scheme regression coverage. Replacement CI
for these follow-ups remains pending; hardware geometry remains unverified.
Android CI at 6909dca caught use of a nonexistent SafeAreaEdges.Container C#
member; this is corrected to the explicit four-edge Container constructor.

This branch was updated serially after #495. TestFlight follows #453–#454.
Issues and epic checkboxes remain open until explicit user device acceptance.

## Exact pending device matrix

All outcomes below are unverified. For each case record commit/build, device,
OS, text size, theme, screen reader and result separately from CI/review.
Run on Duo outer portrait, inner portrait and inner landscape plus ordinary
iPhone portrait/landscape; repeat fold/unfold, rotate while scrolled and resume.
Use light/dark, default/largest text, VoiceOver/TalkBack, Reduce Motion on/off.

1. Default unpinned More -> Settings -> every one of seven sections -> Back ->
   Settings -> More. Repeat pinned Settings root. Confirm actual caller labels,
   one glass owner, header fallback on Android/older iOS, no Back on root.
2. Settings -> Graylog/Unimus/Thresholds/Sensor definitions; cancel draft then
   save a changed draft, return and verify summaries. Change immediate section
   settings, relaunch and verify persistence. Return from consent, What's new,
   licences and browser links. Switch to another pinned tab then back to retained
   More/Settings stack; resume at both overview and child. No Refresh required.
3. App normal/long prerelease and build, Core metadata, sensor unavailable,
   disabled, not downloaded, enabled, cached update failure. Verify complete
   names/display values/state; diagnostics retains its original full version.
4. Disconnected/unknown server; HTTP/HTTPS; long synthetic host; backup active
   explanation; services off/enabled/missing source and long source. Confirm
   source excludes credentials/query/path and state never claims a live test.
5. Diagnostics available/unavailable, consent possible/impossible, all sharing
   combinations and Hide names on/off. Cancel share then explicitly share;
   compare confirmation and redacted payload using synthetic example.net data.
6. Sign out cancel; forget-everything second confirmation cancel; ordinary sign
   out and relaunch; destructive sign out and relaunch. Verify the exact retained
   and removed data above, no duplicate dialogs on rapid activation.
7. Scroll initial and final content in every orientation: fade exactly matches
   background and viewport width, no dark partial bar, text below its reserve,
   header Back above it, taps pass through, final trademarks/sign out reachable.
   Verify camera/side/bottom glass clearance and no repeated watcher/placement
   diagnostics. Native action focus must announce each full row once, activate
   once, show disabled state and expose Hide names independently.
