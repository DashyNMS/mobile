# Sign-in review, #455

Refs #455 and epic #442. Baseline: main `2360274be642f45892028f45a7fb689f8b5abcfa`.
Read live #455/#442/#456/#457, repository CLAUDE.md, DEVELOPING.md,
the complete DESIGN.md and DESIGN-REFERENCES.md.

## Baseline assessment

This assessment describes the source before this PR. Device observations are
unverified throughout. Prior merged PR #291 supplies the system-Camera import
baseline, not acceptance of this form. #271 supplies diagnostics sequencing.

| Concern and source | Baseline result | Change or retained contract |
| --- | --- | --- |
| SignInPage manual/web forms | Fix required: ReadableColumn caps width at 520pt | Full available width, 16pt gutters |
| Token help buttons | Fix required: horizontal actions crowd narrow and large-text layouts | Stack full-width secondary actions with 44pt minimum targets |
| Submit | Fix required: scrolls with form, fixed 50pt height | One visible submit in an Auto row outside the finite scrolling viewport; minimum height allows growth |
| Entry and switch names | Fix required: placeholders/adjacent labels do not supply explicit names | Named address, token, backup and options; minimum 44pt controls |
| Error/retry | Fix required: error can be distant from submit | Complete scrollable error revealed after failure, retry guidance, current-error announcement, including repeated identical failures |
| Busy feedback | Compliant spinner, but secondary actions remained interactive | Spinner plus text; form and secondary controls disabled while existing IsBusy is true |
| Resume lifecycle | Fix required: removes handler from current Window rather than subscribed Window | Retain original Window; remove before subscribing and on disappearance |
| Draft/options | Compliant | Two-way untrimmed inputs; existing SignIn commits only after successful validation; failure keeps draft |
| Password visibility | Compliant | Token always masked; no existing show-password control to rename; no automatic exposure added |
| Validation and endpoint parsing | Compliant in source | Existing view model and SessionService call Core parsing and connection validation unchanged |
| QR permission/cancel/import | Compliant in source, device unverified | System Camera owns scan and permission; there is no in-app camera action or permission request. Add guidance naming the existing Camera route |
| Certificate/HTTP prompts | Compliant in source | Existing consent and exact-certificate trust preserved |
| Keyboard/autofill | Unverified | ReturnCommand unchanged; native URL and password keyboard semantics retained. Local iOS docked-keyboard clearance and focused-field scrolling added |
| Themes/large text/folding | Unverified | No fixed content width; wrapping labels and minimum heights. Native rendering requires device checks |

## Modes and actual routes

- Cold launch starts at Shell `signin`. Remembered server/token enters the
  existing restore splash mode. A failed or absent restore shows the form;
  successful restore goes to `//main` without showing credentials.
- Web mode asks for server and backup plus remember/trust options. Its
  existing probe can fail or request certificate consent. The adapter pushes
  LibreNmsSignInPage modally and pops it on completion, then clears web data.
  Cancel/Back returns to this form. Unsupported or failed web sign-in falls
  back to token entry with its existing message. #456 owns the modal body.
- Manual mode is selected explicitly, by web fallback, or when web sign-in
  is unavailable. Token help opens Core's API Tokens URL; clipboard availability
  checks inspect HasText only. Only the explicit paste action reads text.
- Token submit includes empty-token validation, cleartext confirmation for
  either address, connection validation, certificate consent and retry. A
  successful commit clears the text-box token and navigates to `//main`.
- System Camera opens `dashynms://sign-in` through Android intents or iOS
  URL handling and SignInLinkLauncher. Cold launch waits for a visible page.
  Camera cancellation returns through the OS without importing anything.
  A malformed link reports its existing parser error; a valid link always
  asks confirmation, including replacement of an active session. Declining
  keeps the session. Import failures retain the prior session and remove
  newly supplied trust fingerprints. Concurrent imports remain gated.
- On main entry, notification permission and pending notification routing
  retain their existing sequence. AppShell offers diagnostics consent before
  What's new. #457 owns consent content. Sign-out routes back to sign-in.

## Security and persistence contracts

No authentication, QR, settings, service or Core source changed. The pinned
package is DashyNMS.Core `2.0.0-ci.52`, not the older version described in
DEVELOPING.md. Existing API usage was inspected at the call sites for
LibreNmsConnection.TryParseWebRoot/ApiTokensPageUrl, SignInHelp, ApiTokenText,
SignInLink.TryParse and CertificateTrust.DescribeForPrompt. No new Core APIs
are used. The package is absent from the local NuGet cache, so binary/API
verification remains a CI requirement.

SessionService retains server-root/API endpoint normalisation, backup failover,
trim-on-commit token handling, remember-token keychain/keystore policy, restore
failure policy and exact-certificate trust. The self-signed switch does not
bypass the existing prompt-before-trust sequence. HTTP warnings are unchanged.
QR import retains confirmation before trust, malformed-payload rejection,
explicit cancellation and its existing remembered-token semantics. Clipboard
permission/read behaviour is unchanged. No permission is automatically granted.
No service call or I/O was added for layout, and no logging was added. All
new fixture values use synthetic addresses and tokens.

## Layout ownership and dependencies

The native page owns system safe areas on every edge. iOS uses Container
regions only, leaving keyboard overlap exclusively to the local helper; Android
retains All for native keyboard handling. iOS child safe areas and
native scroll auto-insets are disabled to avoid counting them twice. The form supplies only
16pt gutters; its star row reserves the measured action height. On iOS, local
keyboard observers reserve docked overlap in page coordinates and disable
MAUI automatic translation while visible, restoring it on exit. Floating
keyboards do not lift the whole form. Focused fields scroll within the native
viewport. Observer disposal and Window subscription removal are paired and
repeated appearing is idempotent.

SetupPageLayout was inspected but is unsuitable here: it installs logged-in
glass Back ownership. No shared helper was changed, and this page claims no
tab glass. #496 was inspected read-only via its file list and live description;
its SettingsViewModel changes are not needed. Queued branches were not edited.

The native safe-area/keyboard interaction, long scrollable errors with huge text in short
landscape viewports, floating-keyboard focus reachability and rotation/resume
geometry remain explicit device risks, not claims established by source review.

## Evidence and delivery gates

- Implementation self-inspection: complete diff reviewed against the findings
  above. Whitespace validation is recorded in the PR. This is not independent review.
- Regression coverage added: malformed clipboard input preserves raw address,
  backup, token and options and never submits a session. Existing WebSignInTests
  and SignInLinksTests retain validation, consent and cancellation coverage.
- Local builds, tests and scripts: not run, by user instruction.
- Claude Opus 5.5 Medium reviewed full diff 481e9e4 against main 2360274.
  Findings addressed: import CoreGraphics for CGRect.Inset, clear prior errors
  before each manual/web/paste attempt, exclude native iOS keyboard handling,
  remove unrelated token error hints and guard asynchronous scroll/announcement.
- Unit tests passed at the reviewed head. iOS failed on the missing extension
  import, now corrected. Replacement CI is pending. No local builds/tests ran.
- New regressions check repeated identical missing-token, invalid-paste,
  invalid-address and HTTP-fallback feedback notifications.
- A second Claude Opus 5.5 Medium full-diff review at 0d1f893 confirmed all
  earlier findings fixed and no blockers. Its low paste-refusal finding is fixed:
  clear feedback only after a successful clipboard read, with regression coverage.
  The repeated HTTP-fallback test is view-model evidence only; the real UI must
  first switch back from token mode before retrying web sign-in.
- Device evidence: none supplied; first-dispatch error layout, teardown races
  and keyboard show/rotation remain unverified.

Sequential update after #496 is complete. Section TestFlight follows #455–#459.
Issue closure and epic ticks remain gated on explicit device acceptance.

## Exact pending device matrix

Every row is unverified. Record device, OS, commit, build, result and any
diagnostics separately. Use synthetic data and redact credentials in evidence.

| Scenario | Required result |
| --- | --- |
| Manual valid, remember on/off, backup present/absent | Existing validation and persistence; one submit; success clears token |
| Manual invalid URL/token/backup, offline and timeout, retry | Complete error announced and visible on every identical retry, even after scrolling; raw draft/options retained; no accidental commit |
| HTTP primary or backup; self-signed accept/decline/change | Existing warnings precede use; only explicit exact-certificate trust |
| QR success warm/cold, signed out/active session | Confirmation precedes import, replacement semantics and navigation unchanged |
| QR malformed payload | Existing failure; no token exposure or session replacement |
| Camera permission denied, camera cancel, import cancel, HTTP cancel | OS permission respected; return and draft/session preserved; no new grant |
| QR unexpected certificate/failure/repeated link | Reject unexpected certificate, roll back imported trust, preserve session, gate duplicate import |
| Web modal child entry, Back/cancel, failed navigation, token fallback | Actual modal return and draft retained; #456 body assessed separately |
| Keyboard docked, floating, dismissal, address/token/backup focus | Field and last field/submit reachable; no double inset or translation; existing Enter behaviour |
| Autofill/password manager, clipboard allowed/denied/malformed | Token stays masked; no implicit clipboard read; names and draft preserved |
| VoiceOver and TalkBack | Named fields/options/help; single current primary; error association/announcement; disabled actions unavailable |
| Light and dark, enabled/busy/disabled secondary actions | Legible themed contrast and distinct state; no action during busy |
| Default/large/maximum text, long error, landscape | Labels/actions wrap; final field and submit remain reachable |
| Duo outer, inner portrait, inner landscape | Full-width form with single native inset ownership |
| Duo fold/rotate with keyboard, background/resume, modal return | Recomputed clearance, focused field reachable, no observer accumulation or stale padding |
| Ordinary iPhone portrait/landscape, cold launch restored/failed restore | Splash and form sequence unchanged; no credential flash or glass owner conflict |
| Diagnostics consent then What's new after successful entry | Existing sequence, opt-in defaults and notification target routing preserved |
