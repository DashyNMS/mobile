# LibreNMS web sign-in, #456

Refs #456 and epic #442. Baseline main `5c989a8`. Read live #456,
#442 and #455, CLAUDE.md, DEVELOPING.md, complete DESIGN.md and
DESIGN-REFERENCES.md. There is no FULLDESIGN.md in this checkout.
Read merged #497's full diff and assessment read-only; no queued branch
was changed. This page owns only the modal body, not #455's form or QR flow.

## Baseline assessment before changes

| Concern, trigger and source | Assessment | Result |
| --- | --- | --- |
| LibreNmsSignInPage header: long host, narrow display | Fix required: Auto middle column competes with Cancel | Stack Cancel above full-width wrapping title/host; minimum 44pt Cancel |
| Initial native web viewport | Fix required: load starts in HandlerChanged before positive geometry | Defer first source until Loaded/SizeChanged reports positive finite dimensions; MAUI retains native frame ownership |
| Native form scrolling | Compliant | WebView remains finite in star row, never wrapped in ScrollView; native scrolling unchanged |
| Opening and token creation | Compliant text plus BusyCard; fix required for invisible web hit testing | Hidden website cannot receive taps; shown website becomes interactive again |
| Failure/timeout | Fix required: immediate unexplained modal dismissal; failure during script handling can hang | Scrollable explanatory recovery, announcement and Return to sign-in; invalidates pending callbacks |
| Cancel/Android Back during delayed script | Fix required: post-await continuation can still advance flow | Completion and failure guards after awaits; TrySetResult retains one winning result |
| Browser Back/Forward/reload | Compliant existing contract: none exposed | Android Back cancels modal rather than traversing web history. No new reload or POST replay; no controls to disable while loading |
| Unsupported account/server, flow failure | Compliant | Existing NotAllowed/Failed outcome returns to manual token fallback |
| URL validation/network probe | Compliant in source | Existing view model validation, HTTPS requirement, probe and certificate prompt retained |
| Keyboard/insets | Unverified on device | iOS Container safe areas leave keyboard avoidance to WebKit; Android retains All; no extra keyboard spacer, translation or observer introduced |
| Theme and large text | Unverified rendering | Opaque themed page, wrapping host/recovery, minimum rather than fixed action heights |
| Resume, fold and rotation | Unverified rendering | SizeChanged updates native viewport without starting another request once Source exists |
| Screen reader | Fix required for Cancel context and failure feedback; device unverified | Named modal Cancel/web content, heading and error announcement, explained recovery action |

## Modes, routes and persistence

SignInPage web mode invokes SignInViewModel.SignInWithLibreNmsCommand. It
parses the address with Core, refuses HTTP and probes before opening the
modal. A refused certificate never opens it; explicit trust retries the probe.
WebSignIn pushes LibreNmsSignInPage through the actual navigation owner.
The modal opens API Tokens, shows the external login website (including an
identity provider), returns to tokens, settles through a possible server
reload, creates a token, then returns one result. Busy messages cover opening,
opening API Tokens, creating a token, signing in and closing. Server page
appearance remains outside this issue.

On network failure/timeout recovery stays in the modal until Cancel, Back or
Return to sign-in. The latter returns Failed, preserving the existing manual
fallback and error. To retry web authentication, select LibreNMS mode again
on the parent form. Reload while token creation is busy is deliberately not
added: Core's flow has submit tracking, not a general reset/replay contract.
Cancel and Android Back both return Cancelled without committing anything;
native browser history was never the Back contract and is unchanged.

The adapter pops the actual modal and clears platform cookies/storage in its
finally block for every outcome. Token success calls the same session
validation as manual sign-in; failed validation retains the masked token for
manual retry. Successful validation alone commits settings and the remembered
token through keychain/keystore policy, clears the input and enters //main.
Notification routing and diagnostics consent then What's new retain their
existing sequence. #497 owns parent draft presentation and local keyboard
handling; neither its files nor manual/QR services change here.

## Security and lifecycle contracts

The pinned dependency is DashyNMS.Core `2.0.0-ci.52`. Existing calls to
WebTokenSignIn.Next/IsOnServer/SubmitScript/SubmitNotSent/SubmitSkipped and
CertificateTrust.IsTrusted remain in place; no new Core API is introduced.
The pinned package is absent from the local cache, so binary API verification
belongs to CI rather than being claimed from source inspection.

Only server pages receive probe scripts. Core still decides allowed server
locations, callbacks, token extraction and flow state. TLS trust requires the
existing allow switch, server host and exact previously trusted fingerprint.
Both native certificate callbacks and MAUI navigation policy remain intact;
no new permission, consent, credential requirement or transport exception was
added. CSRF, session cookies and server redirects remain owned by the existing
Core scripts/platform. No secret storage, QR confirmation, diagnostics or
session service changes. No added URL logging or exception text logging.
Existing log description strips query/fragment and classifies script output.

Native page owns safe areas; no logged-in glass helper is appropriate. The
finite star body owns browser height and the header measures independently.
The separate recovery ScrollView never wraps web content. No map logic,
global helper, layout I/O, window subscription, keyboard notification or
resume observer was added. HandlerChanged installs the existing native
delegate/client; size/load handlers belong to the page-owned web control and
do not accumulate on Appearing. Native keyboard clearance, safe-area behaviour
inside WebKit and absence of double translation remain device checks,
especially when #497's modal transition restores the platform keyboard manager.

## Evidence and dependencies

Full diff self-inspected, including unchanged certificate/navigation hooks
and adapter cleanup. This is not independent review. Added regression coverage
checks failure and cancellation preserve draft/options, never commit a session
and select the existing fallback mode. Existing tests cover HTTPS refusal,
certificate refusal, token success and probe failure. Fixtures are synthetic.
No local builds, tests or scripts were run, by instruction. Whitespace check
is recorded in the PR. CI shared tests and Android/iOS builds are pending;
native bindings and geometry are not proven by shared tests.

Claude Opus 5.5 Medium reviewed full diff df627ef against main 5c989a8 and
found no blocking defects. Follow-ups use container-only native safe areas on
iOS, remove ineffective frame assignment and record unexpected failure type
without exception text or stack. A stale load failure can still terminate a
newer load; check offline/timeout redirects on hardware before changing event
identity policy. Hidden web content may remain exposed to TalkBack while busy
or on the recovery screen; this pre-existing gap remains a device check.
Unit tests and actions analysis passed at the reviewed head; platform builds
and C#/Swift analysis were pending. Replacement CI remains pending.
Sequential update after #497 is complete. No issue closure or epic ticks.
TestFlight follows #455–#459; all device acceptance remains unverified.

## Exact pending device matrix

Record device/OS, commit/build, result and redacted evidence for each row.

| Scenario | Required evidence |
| --- | --- |
| Successful password/SSO login, token creation and redirect | Busy hides token, one result, cookie cleanup, validated session, diagnostics sequence |
| Cancel and Android Back at opening/login/settle/submit/completion race | Exactly one result; modal closes; no session on cancellation; return to #497 draft |
| Offline, timeout, failure during script callback | Named readable error, Return to sign-in reachable, manual fallback; fresh web attempt works |
| Malformed/foreign redirect and unsupported token form | Core rejects or waits as before; no foreign script/token extraction or leaked secrets |
| Self-signed trusted/untrusted/changed certificate; HTTP address | Existing explicit trust/HTTPS refusal; no implicit consent or certificate bypass |
| First load with zero height, later valid layout, repeated Appearing | No premature load, no blank native viewport or duplicate request/subscription |
| Long host/status, default/large/maximum text | Header wraps, Cancel remains usable, error/action scroll within finite recovery viewport |
| Docked/floating keyboard, focus last website field, dismiss | Native web form scrolls; header stays reachable; no double translation/insets |
| VoiceOver and TalkBack, opening/error/cancel/success | Context, progress and current error understood; action names and targets usable; hidden website/token content not exposed while busy or on recovery |
| Light/dark, busy and recovery states | Opaque legible background and readable actions/status |
| Duo outer/inner portrait/inner landscape | Actual web frame follows available body; no clipping or blank first load |
| Duo fold/rotate/resume during login/creation/error, keyboard open/closed | Correct finite geometry, no duplicate submit or accumulated observers |
| Ordinary iPhone portrait/landscape and background/resume | Top/bottom safe clearance, native scrolling, trust prompt not double translated |
| Return to #497 after cancel/error/success, retry web from manual fallback | Raw draft/options retained, token handling unchanged, modal framing and diagnostics order preserved |
