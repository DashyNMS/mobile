# Device Graylog assessment

Baseline: main `a5fee22`, merged #544. Live #483, #442, #288, #517 and #461 bodies and comments read on 5 November 2026. #526 is merged and supplies the #461 implementation baseline. Its native three-quarter-width/short-bottom symptom has no proven cause or accepted fix. #544 is an approved Event log example only. It does not approve this Graylog proposal.

The parent rendered the [HTML proposal](proposals/483-graylog.html) and obtained approval, with the latest constraint: "Yeah but should be compact enough to fit on small screens as well as larger screens". This PR implements that presentation as well as the settled search and same-ID refresh fixes. C means source compliant, F means confirmed fix needed, U means unverified. Source compliance and proposal approval do not imply native acceptance.

Compact arrangement: 10pt row vertical padding and 4pt fact spacing in one list card. Wrapping choices have 44pt minimum targets and 6pt separation. Choices, scope and summary scroll in CollectionView.Header rather than reserving fixed Auto rows, leaving the finite star viewport available on short screens at maximum text. Header padding reserves the 20pt input-transparent surface fade. This is responsive engineering within the approved choices, with no additional offsets or shared layout edits.

## Modes, routes and preserved contracts

`DeviceDetailViewModel.OpenSectionAsync` passes device ID/name to Routes.Graylog. `GraylogPage.ApplyQueryAttributes` fixes that scope through Initialise. Network entry is the pinned Graylog tab or More, including Logs navigation; Dashboard See all passes range/stream without device scope. Dashboard message taps go directly to the detail snapshot without a list owner. `ShellNavigationService` resolves pinned/More destinations and intercepts message navigation only when the active IDetailHost can split. Narrow list taps push GraylogMessagePage; wide list taps embed GraylogMessageView with this list as owner. ListDetail retains its pane when narrowed and restores it when widened. Show only changes network scope, never fixed device scope; Open device returns through relative navigation. Settings and device-picker child return preserve the existing owner/list; #405 setup/picker are unchanged. Root glass has no Back, pushed hosts restore their actual caller Back. Embedded detail actions remain with the host glass from #526.

GraylogViewModel uses Core GraylogQuery for escaped server search, cumulative maximum level, time range, stream and device addresses. Search submits on keyboard Search; blank query reloads. Fixed device starts at saved device log level, network at Any level. Clear resets range/level/stream/query and only clears a non-fixed device. Known source mapping uses Core DeviceAddresses with unique short-name matching; unknown senders retain their address. DNS has a three-second bound; optional interface-address failure falls back to main addresses. No-address and overlong-query guards prevent accidental whole-network disclosure as a device result. Pages of 50 append to a cap of 1000; summary uses loaded/total counts. Auto-refresh repeats the last submitted query, prepends first-page results and keeps later pages where overlap exists. Superseded ticks are ignored, timer requests do not overlap, failure keeps data/filters with a timestamp. The existing selected detail remains its original snapshot.

Only auto-refresh On/Off and interval persist on this phone under graylog preference keys. Search/filter/list selection remain in memory. GraylogSetup and SecureGraylogPasswordProtector retain independent Graylog configuration and keychain/keystore password handling, accepted #405 Save semantics and TLS policy. No credentials, permission requests, API contracts, query syntax, source content or route IDs change. No export exists in this list. No offline disk cache is introduced.

## Full scope findings

| Concern, source and trigger | Status and disposition |
| --- | --- |
| Explicit search entry and refocus, GraylogPage constructor | F fixed: header now calls OpenSearch without clearing SearchText; shared ButtonSearch refocuses an open input. No gesture reveal exists. Command-F continues through ISearchHost. |
| Programmatic scroll while typing, List.Scrolled | F fixed: ButtonSearch checks actual native drag; KeyboardDismiss.OnScroll is explicitly false. Completion still submits once and closes. Unfocus/disappearance close and retain query. Shared reduced-motion/named-animation policy replaces the copied animation. Native focus timing U. |
| Source freshness on same-ID auto-refresh, GraylogViewModel.AutoRefreshAsync | F fixed: refreshed field differences replace matching loaded rows, unchanged instances and later pages remain. Fields include the complete sorted server field projection used by details. Duplicate response keys use the first record rather than throwing. Synthetic CI regression checks changed source/raw field and unchanged row identity. |
| Virtualisation and finite viewport, GraylogPage star row/ListDetail | C source: CollectionView stays outside ScrollView, RefreshView wraps it, page header/search/status are fixed Auto rows; choices and summary scroll in the CollectionView header. Native header plus EmptyView reach and remeasurement after wrapping/resizing remain U. Native H/B/V and clipping U. |
| Filter/search hierarchy and large text, XAML horizontal strip, Chip style | F fixed in source: wrapping native 44pt choices in the scrolling header, explicit current values, read-only fixed scope and separate network change/clear. Native wrapping U. |
| Message hierarchy, XAML row and GraylogMessageItem | F fixed in source: list-only properties show full plain multiline text and stacked full timestamp/facility/severity with unavailable fallbacks. Existing Dashboard MessageText/ShortTimeText and detail FullText/Fields remain unchanged. |
| Row activation and speech, Grid tap and nested labels | F fixed in source: one native row button with Open message/source/level/time/ID plus message/facility speech; visual children excluded to avoid duplicate stops. Unknown facts state unavailable. VoiceOver/TalkBack operation U. |
| Auto-refresh current state and targets, XAML chip | F fixed in source: visible On/Off text and interval only when On. Graylog gates ticks and paging on the current load identity and restores busy after superseded cancellation. CI-only adversarial tests added; execution and native lifecycle U. |
| Busy, first load, refresh, unconfigured | C source for BusyCard and setup route, IsEmpty suppresses loading/error. Wrapped status/viewport on short screens U. Setup route unchanged. |
| Empty source versus no matches, IsEmpty/EmptyText | F fixed in source: default network empty says no messages returned across all time; narrowed/fixed scope describes no matches. No global server total is claimed. |
| Primary failure, offline, cached rows, refresh failure | C source preserves rows on failed requests via RunAsync and separate RefreshProblem. No offline cache promised. Initial errors suppress empty claims. Retry is pull refresh; reachable native retry/status at maximum text U. |
| Optional metadata permissions, LoadAsync/LoadStreamsAsync | F fixed: stream metadata exceptions produce visible fixed status; requested Dashboard stream ID remains the search filter even when unavailable in the chooser. Mandatory LibreNMS device-list policy retained, including denied network feeds; no bypass. CI execution U. |
| Server search concurrency/cancellation | F fixed within #483: cancellation checked after configuration, device list and device address awaits before publication; current-load identity gates paging/ticks and restores busy after older RunAsync cleanup. Synthetic adversarial tests authored; execution U. No shared ViewModelBase change. |
| Paging and cap | C 50/1000 bounds and loaded-count offset, CanLoadMore after busy. U for native final Load more reach, server result mutation across pages and expired credentials. No new numbered paging. |
| Fade, initial content and list surface | F fixed in source: 20pt input-transparent fade uses SurfaceLight/SurfaceDark and their matching transparent RGB endpoints, inset 1pt within the card. Header padding reserves initial content. Native empty/header composition U. |
| Glass and detail subscriptions | C active host guards, idempotent activation subscriptions, removal on replacement/narrowing/disappearance from #526; Moved places without republishing. Native retained tab/child/resume U. Auto-refresh lifecycle remains Appearing/Disappearing plus app activity, not restarted by Activate. |
| Insets, camera, viewport base and final row | U: TabAlignedListLayout owns page top safe area, side column gutters, card base and footer; disables native list adjustment on attachment/load. #517 owns camera layering. Do not infer actual geometry or add offsets. |
| Embedded/pushed details | C preserved source contract: GraylogMessageView stores supplied clearance, content owns padding, native scroll adjustment Never; GraylogMessagePage owns pushed glass, host owns embedded glass. Reported constrained width/short bottom remains U under #461, no redesign. |
| Theme, motion, accessibility, keyboard | C theme resources and shared ButtonSearch ReducedMotion source policy; C source targets/row action implemented above; U actual contrast, traversal, speech activation, keyboard/floating keyboard, initial content, long words and reduced-motion rapid reopen. |

## Shared ownership

TabAlignedListLayout serves LogsPage and GraylogPage. It is unchanged. ListDetail serves Devices, Neighbours, Logs, AlertRules, Alerts and Graylog and is unchanged. ButtonSearch already serves reviewed list pages including Logs; this PR adds a consumer without editing the helper. GraylogMessageItem serves Dashboard previews, list and detail; added list-only properties leave existing projections unchanged. #288 retains Graylog mock-up/global light-theme ownership and has no comments or accepted Graylog mock-up in its live issue. This proposal is evidence for coordination, not completion of #288. #517 remains the sole camera/pill-layer correction owner; #461 remains detail owner. #405 acceptance is preserved. The original dirty #475 checkout was not accessed or changed.

## Validation and exact native matrix

No local builds, tests, restores or validation scripts. Full intended/staged diff review and git whitespace checks only. Parent owns independent Opus review, shared-test CI, Android and iOS CI and any CI judgement. The synthetic tests are for CI only; no source-string or UI-mirroring test was added. Search needs native input evidence, not a fabricated shared unit test. The commit containing this assessment identifies the exact candidate; record the full head SHA from the PR for every run. Build number and OS are currently unavailable. All native rows below are U, not accepted. After any further implementation, repeat at that new SHA/build.

| SHA/build/OS to record for each execution | Exact steps and measurements, all U |
| --- | --- |
| Final PR SHA; CI shared tests, Android, iOS builds | Record each run URL/result separately. CI does not establish geometry or accessibility. |
| Same SHA, actual app build, Duo outer portrait/landscape, inner portrait, inner landscape | Open device → Graylog; record native page, ListColumn host H, ListCard and CollectionView frames, pill bottom B and viewport bottom V in the same coordinate space, V−B, camera reserved regions, glass actions and native scroll inset/content size. In split mode record pane, ContentView, scroller and content frames independently. |
| Each size/theme above with 51+ synthetic messages and 1000 cap | Scroll to last row; record complete last-row bottom, Load more target frame and obstruction rectangles. Tap Load more and repeat. Record base alignment separately from row reach; a footer is not proof of V≈B. Include no pill, side pill, bottom pill and top-wide pill where supported. |
| Same SHA/build, light/dark, default/maximum text | Long source, multiline message, long facility/stream, missing level/time, blank message; initial first row and empty state; full row/detail source fidelity. Verify all controls and status fit without starving finite viewport. |
| Same SHA/build, Duo fold/rotate while populated and while searching | Open explicit search, type, refocus, submit, reopen; filter/programmatic scroll must retain focus, actual drag/unfocus must close and retain query. Keyboard dismissal, fold and resume restore bounds; no new glass owner or offset. Floating keyboard remains separate U. |
| Same SHA/build, network tab/More, Logs → Graylog, device → Graylog, Dashboard See all | Verify each filter default, device fixed/non-fixed, range/level/stream, Clear, settings/picker return, message push/Back, embedded Show only/Open device/Back, narrow/widen and replaced detail. Dashboard direct message remains separate #461 route. |
| Same SHA/build, auto-refresh On/Off and each interval | Scroll down, tick, check stable scroll and updated same-ID facts; load more then tick; fail server then recover; background/resume, child/retained-tab return; verify no hidden timer requests, overlapping requests or placement loops. |
| Same SHA/build, synthetic server states and permissions | Unconfigured, missing password, busy, empty, no matches, Graylog denied, stream denied, LibreNMS device denied, DNS timeout, interface-address denied, no device addresses, query cap, load-more failure, offline retained rows. Record exact visible state and retry path without logging secrets. |
| Same SHA/build, ordinary iPhone iOS 26+ and older header fallback; Android phone/tablet | Repeat narrow/wide routes where supported, safe areas, Back ownership and final row. VoiceOver/TalkBack read current filters/state and activate message/device; reduced motion rapid open/close; diagnostics for Top line/Tab bar placement loops. |

Presentation approval is recorded above. Outstanding F/U concerns are not accepted deferrals. No watch, merge, closure, epic ticks or release.


## Permission and concurrency dispositions after implementation

#483 owns these filter/refresh concurrency corrections; they are included here, not deferred outside presentation scope. Local awaited values are cancellation-checked before publishing device/address caches or configuration. Only the current load clears its identity; superseded completion reasserts busy while a newer load remains pending. Paging and auto-refresh check that identity independently of RunAsync's busy notification. CI-only cases cover stale metadata (device 23 retained and streams loaded once), pending configuration with device switch, cancellation-ignoring resolver, and older cancellation with newer pending search. Submitted search drives query and empty status; an unsubmitted retained draft does not claim active filtering on a later range change.

Stream metadata failure is optional: its separate StreamProblem reports unavailable choices in the fixed status slot and preserves the requested Dashboard stream ID in every search. No silent fallback to a wider query. Mandatory LibreNMS device policy is retained as instructed. These repairs need no new permissions decision. Synthetic tests are authored, not executed. Parent must obtain a fresh independent Opus full-diff review and assess shared-test/Android/iOS CI.

Graylog row buttons use PortReadout's explicit empty style and transparent Normal/Disabled states, with PrimaryTint pressed/focused feedback. Logs sibling #544 is untouched; parent follow-up owner #482 must repair its equivalent overlay if confirmed. Dashboard severity projections remain unchanged; the list alone formats Warning (4).

All native evidence remains U, including a short phone viewport at maximum system
text with all filters, long unbroken source/message/stream, no messages, error and
refresh status. Record actual screen dimensions, text setting, remaining viewport
height, wrapping, initial fade readability and final-row reach for that SHA/build.
Scrolling the header must leave every choice reachable without oversized cards;
measure native button bounds and assistive traversal. No device evidence was
available in this implementation session. The protected dirty #475 checkout and
parent checkout were not mutated.


## Fresh review corrections and evidence ledger

The superseded successful search-response test preserves a baseline cancellation
guard. It is not evidence of a newly fixed failure path. New regressions complete
an old pending search and an old device lookup with exceptions after a newer
successful refresh. The callback inside RunAsync converts superseded failures to
cancellation before shared error publication and Failed diagnostics. Current
failures retain the existing error path.

StreamProblem is independent of RefreshProblem. A failure notice persists through
searches and timer ticks until a successful metadata fetch. Non-append manual
loads retry missing metadata with one in-flight owner per connection generation; ticks do not retry. Setup
configuration generations reject metadata from a previous connection. Requested
stream IDs remain query scope while metadata loads, fails or omits that ID.
The label says loading while pending, unavailable after failure or a successful
response missing the ID, and the resolved title on success. Metadata application
suppresses redundant reloads.

Core GraylogApi.SendAsync wraps HTTP transport failures and HTTP timeout
cancellation in GraylogApiException, preserving real caller cancellation. The
ci.56 consumer now also catches direct HttpRequestException, TimeoutException and
OperationCanceledException on ticks, including exceptions carrying a cancelled token. Synthetic cases retain rows and
report a failure time. Auto-refresh supplies no caller cancellation token, so a cancelled exception is also a failure. No timer exception is
left unobserved for these failure types. Core LevelText emits a numbered prefix
for nonnegative levels, including unknown numeric values. The list formatter now
checks the full separator before slicing; missing separators preserve the label.
Dashboard projections remain unchanged.

The HTML proposal describes a single fixed state slot. Implementation instead
retains BusyCard over the finite list and CollectionView.EmptyView, with primary,
refresh and independent stream errors above it. This is an explicit engineering
tradeoff, not additional user approval or proof of the proposed state hierarchy.
Long simultaneous errors can reduce the viewport. Native maximum-text/short-screen
acceptance remains U. Header plus EmptyView reach/remeasurement, measured FilterStrip child widths
and focus PrimaryTint appearance are also U. No
insets or guessed offsets were added. #461, #405 and #517 ownership is preserved.

At previous head a59b4ca105cc859e9ca8f79853033da4c4f8be0b, shared tests,
Android and iOS simulator checks passed in run 42011763641. That does not validate
these follow-up changes. Final-head CI and fresh independent Opus full-diff review
are pending and belong to the parent. No local build, test, restore or validation
script was run. Git whitespace checks and source diff review are the only local
validation. No watch, merge, closure, epic tick or release was performed.


## Connection ownership follow-up

Stream fetch ownership is scoped to the setup connection generation. A new
connection starts its own metadata fetch while the previous fetch is pending.
Only the matching current owner can publish metadata or a failure, clear loading,
or notify completion. Reviewed old success and failure before and after new
completion, same-connection reloads, and reset during append. Connection reset
clears StreamProblem and notifies stream scope even when append defers fetching.
Authored regression cases cover both completion orders and both old outcomes.

Auto-refresh supplies no user token. Every OperationCanceledException therefore
reports the refresh failure timestamp while retaining rows. A new non-append
query clears the timer's saved search before awaiting anything. Failed new
filters retain old rows and their error without permitting ticks to request an
old query beneath new chips. Submitted search and retained draft policy remain.

FilterStrip.SizeChanged now applies the measured available width to its native
children, subtracting their actual margins. Finite positive guards and unchanged
value checks avoid invalid sizing and repeated writes. The page owns this XAML
event; there are no external subscriptions or additional watcher loops. Wrapping
and 44pt minimum targets remain. Native size and accessibility evidence is U.

All synthetic regressions are authored, never run locally. No local build, test,
restore or validation script was executed. At previous head d982245, shared tests,
Android and iOS simulator CI passed in runs 42013054971 and 42013047990. These
results do not validate this follow-up. Current-head CI and a fresh full-diff
review belong to the parent and remain pending. Local checks consist only of
source diff review and git whitespace checks.

Refs #483 and #442 remain non-closing. The user's #442 device-acceptance
instruction overrides CLAUDE.md's general Closes convention. No native acceptance
or accepted deferral is claimed. Approved compact presentation, mandatory device
permissions and #461, #405 and #517 ownership remain unchanged. Parent and
protected #475 checkouts are untouched. No watch, merge, closure, epic tick or
release is authorised here.

### Final review at ff85f8c

Independent full-diff Opus review found no medium or high findings. The remaining
low stream-metadata reset during Load more is corrected: a connection reset
starts its own fetch even on append, covered by a synthetic regression. This
asserts the second metadata request and resolved current label, not native fit.
Unit tests passed at the reviewed head; other CI was still running when reviewed.
Final-head checks remain pending, and no local execution was performed.

The confirmed connection-change paging finding is resolved in source, not deferred.
Loaded rows, saved queries, stream choices and source mappings now belong to the
setup generation. Detected changes clear old rows before a zero-offset request,
including offline failures; old in-flight success and failure cannot publish.
Query addresses are recomputed for changed query settings, while the independent
LibreNMS device list is retained. Requested stream IDs remain explicit scope.
Activation checks EnsureLoaded for Settings child return and retained host return;
unchanged loaded generations do not fetch. Synthetic regressions cover append,
tick and return, new query fields, retained stream scope, offline failures and
old search/tick completions before any user reload. These tests are authored for
CI, not run locally. Fresh review, current-head CI and native acceptance remain
pending with the parent.
