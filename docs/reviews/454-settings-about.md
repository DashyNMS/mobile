# Settings overview and About assessment

Refs #454 and #442. Baseline main 83e9a39 (#490), branch
`t3/settings-about-overview`. Read live #454/#442/#453/#457/#458/#459,
CLAUDE.md, DEVELOPING.md, the full DESIGN.md and DESIGN-REFERENCES.md,
#405/#412 and PR #441 acceptance. FULLDESIGN.md is absent; DESIGN.md is the
current guide. PR #495's diff and assessment were inspected read-only; it
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
| Retained stack/child return/resume | Fix required: OnAppearing only, no glass ownership | IActivePage activation, owner-safe Hide, idempotent Moved/watch, paired retained Window subscription |
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

Full-diff implementation self-inspection and git diff --check completed before
commit. Added unexecuted regressions for disconnected/insecure unknown-version
summaries and every SettingsSection route/parameter. Existing About tests cover
version states and sensitive-source exclusion. No local builds, tests, scripts,
restore or device tools run. CI shared tests, Android and iOS builds pending;
self-inspection does not establish compilation or independent review.

Queue: #489 -> #491 -> #492 -> #493 -> #494 -> #495 -> this PR. Parent updates
only this PR after prior PRs merge. Mandatory Claude Opus 5.5 Medium independent
review after quota reset is pending, one PR at a time. No child review attempt,
watch, merge, issue closure, epic tick or release. TestFlight follows #453–#454.

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
