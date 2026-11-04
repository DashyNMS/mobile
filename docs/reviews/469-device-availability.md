# Device availability review, #469

Refs #469 and #442. Baseline `83e9a39`, including merged #490. This is the
first additional DeviceSectionPage assessment; earlier Device overview work
is a baseline, not acceptance. No issue closure or epic acceptance is claimed.

## Baseline and ownership

Read live #469/#442 and #470–#479, CLAUDE.md, DEVELOPING.md, DESIGN.md and
DESIGN-REFERENCES.md. There is no separate FULLDESIGN.md in this checkout.
Routine guide choices were authorised. Availability owns uptime percentages,
windows and outages. Graphs belong to #480/#481. Setup #405 remains accepted.

The actual full page is DeviceSectionPage with DeviceSectionViewModel and
DeviceSectionLoader, not a DeviceSectionView. DeviceDetailView contains the
embedded availability highlights and 30-day tile. DeviceDetailPage hosts that
view when pushed; Devices can host it in its detail pane. Availability still
pushes Routes.DeviceSection through OpenSectionAsync/OpenTileCommand, passing
the selected device ID, section and display title. It is not an embedded
section editor. Relative navigation and `..` return retain the caller stack,
selected root and existing query. PageHeader and BackBar own fallback labels;
#490's actual-caller fallback remains unchanged.

Core remains pinned to 2.0.0-ci.52. Existing typed call sites and fixtures
establish GetAvailabilityAsync/GetOutagesAsync, AvailabilityWindow duration
and double percentage, and nullable DeviceOutage start/end. Local Core source
was inspected as corroboration, not proof that its HEAD equals the package.
The package is not restored here; CI must verify compatibility. No API query,
server timestamp conversion, UTC preference, threshold, time-window calculation,
paging, permission, credential, persistence or selected-ID contract is changed.
There is no availability chart, calendar or period picker. Windows are the
server's returned durations, ordered shortest first. Outages use existing
ServerTime conversion and Units.Duration; a null end means ongoing.

## Assessment before fixes

| Concern and trigger | Baseline status and evidence | Result |
| --- | --- | --- |
| Window ordering, 24h/7d/30d/365d and custom duration | Compliant: loader orders DurationSeconds, Units.Window names periods | Preserved |
| Long dates, durations and large text | Fix required: shared title truncation and two-line 180pt value cap | Availability-only stacked wrapping facts, also embedded highlights |
| Severity without colour | Fix required: percentage colour was the only state | Existing thresholds also produce OK/Warning/Critical text |
| Invalid percentages | Fix required: NaN became critical, infinity could be OK | Unavailable, dash and neutral status for non-finite/out-of-range values |
| Missing outage start | Fix required: Where dropped records and could imply zero outages | Retain a neutral recorded-outage row with missing-time explanation |
| Failed/denied API | Compliant: WhenAll fails the section; no fabricated 100% or zero-outage success | Preserved; explicit busy-disabled Retry added |
| Refresh and stale readings | Fix required: retained percentages lacked freshness timestamp | Clear availability rows during refresh, retain truthful error on failure |
| Device changes while fetching | Fix required: old completion could publish under new title | Serial request handoff and generation guard discard old results/errors |
| Loading/empty/filter distinction | Compliant: busy card, error suppression, source-empty versus Nothing matches | Preserved; refresh spinner remains native |
| Search replacement focus | Fix required: manual implementation lacked native drag dismissal | Existing ButtonSearch, OnScroll=False; button/refocus/Command-F only |
| SearchReveal | Compliant: no attachment in this page | No shared SearchReveal edit |
| Native insets and finite viewport/footer | Compliant: Never on handler/load; star list and existing bottom viewport clearance | Single inset owner and normal 24pt footer retained |
| Fade | Fix required: 8pt spacer under 20pt fade; root-wide fade crossed neighbour graph | 20pt spacer, fade in actual list column with matching margins |
| Retained activation and lifetime | Fix required: no IActivePage, discarded edge subscription | Activate reclaims controls; idempotent Moved and disposable edge watcher |
| Screen reader, keyboard, motion, themes and device geometry | Unverified on hardware | Joined nonblank availability description, child readouts excluded; existing reduced-motion helper reused |

Availability facts have no navigation action. Retry is a normal whole 44pt
button, disabled while busy. Header actions use existing IconButton/TabBarButtons.
No multiline button labels or per-kind global row redesign was introduced.
Overview card/tile activation accessibility outside the changed highlight facts
still needs device evidence; this PR does not claim a whole overview review.

## Shared consumer integration matrix

These are accepted implementation boundaries for subsequent child work, not
device acceptance. All consumers receive button-only search, Retry, lifecycle
cleanup and the list-surface fade. Their existing row layout stays intact.

| Consumer | Contract retained and integration check required |
| --- | --- |
| #470 Sensors | Classes, Core units, limits, missing readings, stale sensor status; filter while typing and refresh |
| #471 Resources | CPU/memory/storage order, bars and thresholds; wide columns and final bar reachability |
| #472 Ports | Persisted sort/group, filter card, ifName graph route; open panel from search, return from #481 |
| #473 Neighbours | Graph/list selection, reverse-discovered links, monitored IDs; wide split fade only over list |
| #474 VLANs | VLAN number, name and port count; search and column changes |
| #475 FDB | MAC formatting, interface and VLAN; search and final row |
| #476 ARP | IP/MAC/interface context and ordering; filter replacement focus |
| #477 Routing | BGP, OSPF/v3, VRFs and existing state; long values remain child-owned |
| #478 Wireless | Sensor classes, API units, limits and missing values; refresh and resume |
| #479 Inventory | One-column nesting, expand/collapse, ports toggle, serial copy; header clears fade |

EventLog loader is retained for compatibility, but actual detail entry routes
to Logs (#482); Graylog, graphs and backups use their specialised pages. No
specialised page is migrated here. DeviceSectionCard and DeviceDetailViewModel
keep their summary and 30-day selection contracts; only availability highlights
opt into the new row presentation flag.

Pending #489 and #504–#506 file lists were inspected read-only. PushedListLayout
belongs to pending work and is not used or edited here. #490 is merged baseline.
Parent must process this PR after 489 → 491 → 492 → 493 → 494 Map → 495 → 496 →
497 → 498 → 499 → 500 → 501 → 502 → 503 → 504 → 505 → 506, then update only
this PR at its turn and obtain mandatory Claude Opus 5.5 Medium review once
quota resets. No review delegates, watches, merges or releases were started.

## Validation and exact pending device matrix

Full intended diff self-inspected and git diff --check performed. Added
unexecuted regression cases for invalid percentages and device-switch races.
Existing availability/outage and card tests remain relevant. No local builds,
tests or scripts were run, as requested. Shared-test CI and Android/iOS CI are
pending; no device or independent review result is inferred from source review.

Use synthetic devices 7 and 8 named example.net and edge.example.net, with
fixed dates 2026-10-01 08:00 to 10:00, and an ongoing outage. Tie every result
below to the final commit and CI build before acceptance.

| Scenario | Exact evidence required | Current result |
| --- | --- | --- |
| Windows | 24h, 7d, 30d, 365d, custom duration; 100, 99.9, 99, 95, NaN and missing list | Unverified |
| Outages and long data | Recovered, ongoing, missing start, many rows, long localised dates/durations; final row reachable | Unverified |
| Missing/denied/offline | Empty success, HTTP denial, timeout and either endpoint unavailable; never false 100%/unknown OK/zero-outage success | Unverified |
| Stale/retry/race | Refresh success to failure to Retry; device 7 slow then 8; no old facts or errors under 8 | Unverified |
| Routes | Devices narrow and split → detail tile/card → availability → Back; Dashboard/alert → device → availability; More retained stack and pin changes | Unverified |
| Search | Header/glass/Command-F, typing replacements retain focus, native user drag closes, completion/unfocus retain query, no swipe reveal | Unverified |
| Accessibility | VoiceOver and TalkBack announce each complete fact once; state independent of colour, Retry reachable and busy disabled | Unverified |
| Themes/text | Light and dark, largest text, long synthetic device name; no clipping or duplicate readout | Unverified |
| Duo | Outer portrait, inner portrait, inner landscape; fold/unfold, rotate mid-search, background/resume; correct Back/search owner and final row | Unverified |
| iPhone | Ordinary iPhone, portrait/landscape, older iOS header and iOS 26 glass; keyboard, safe areas, Reduce Motion and resume | Unverified |

No acceptance boxes are ticked. TestFlight remains parent-owned after the
#469–#484 Device-section sequence, not an action of this implementation PR.

## Claude review remediation

Claude Opus 5.5 Medium reviewed full diff 6024fe6 against main 8f29e13 with
no blocking defects. ErrorLine explicitly opts out of native safe areas; invalid
availability descriptions omit placeholder dashes; overview values retain status
text colours; missing-start outages keep any known end time. Dead page-local
search animation is removed in favour of ButtonSearch. The view-model search
command remains for compatibility pending consumer review.

Device changes intentionally wait for the prior request before fetching the
new device; stale rows/errors are discarded. Cancellation and first-load timing
for that uncommon reused-instance handoff remain a recorded limitation. Sensors
status-note Retry is harmless refresh behaviour and must be checked under #470.
Replacement CI is pending. No local builds/tests ran; all hardware outcomes
remain unverified and no issues or epic checkboxes are closed.

## Current shared correction, 4 November 2026

This section supersedes the historical geometry-compliant findings above and
in #470–#477 assessments. Prepared base is main f7c7130, including #529;
#475/#476/#477 implementations are already merged through #513/#514/#518.
Live #469/#442 bodies and full comments and #470–#479 comment streams were
refreshed. #469–#474 explicitly fail viewport-base acceptance. #472 also
requires an agreed concrete card proposal before any redesign. The current
PR changes geometry only and preserves all existing presentation flags.
Existing #507 is merged; no open availability replacement PR existed.
The dirty fix/475 worktree and all stale sibling branches remain untouched.

### Actual ownership and trace

DeviceDetailViewModel.OpenSectionAsync/OpenTileCommand opens relative
Routes.DeviceSection with selected DeviceId, section enum and display title.
AppShell registers DeviceSectionPage. Devices narrow/pushed and split detail,
Dashboard, alert and other device links all reach the same section host.
Back is relative `..`, labelled by BackBar.PreviousTitle. No new route or ID
conversion. Child routes are neighbour DeviceDetail and port DeviceGraphs
with original ifName, device ID/name and port title. Inventory serial copy
remains an explicit local clipboard action; no server mutation or new storage.

Native iOS page owns top container safe area only. Root and SectionBody opt
out; Refresh opts out; CollectionView has no SafeAreaEdges and retains native
UIScrollView Never on HandlerChanged/Loaded and placement. Android retains
its existing native Container safe area. Header/search/status Auto rows precede
SectionBody's finite star allocation. There is no ListCard wrapper in this
page: individual rows are Borders. Refresh fills SectionBody's star row, List
fills Refresh, and ListFoot is content rather than viewport margin. ListFade
is input transparent with a 20pt header reserve; empty text has 32pt margin.
Wide Neighbours uses two columns, graph/list each spanning both body rows;
narrow graph takes its measured Auto row with the existing 35%/280pt cap.
Graph visibility is the IsNeighbours binding; no hidden graph reserve is added.

Confirmed source mismatch: Refresh previously reserved 12 + ForScrolling.Bottom
outside the viewport, while ListFoot separately reserved 24pt. That uses glass
obstruction clearance as viewport-base policy. This is a source finding, not
proof of the exact native device failure. Devices/Alerts and TabAlignedListLayout
(Logs/Graylog) already separate measured base from final-row obstruction.
PushedListLayout (#527, consumed by backup history/contents/differences) measures
its own Body. Copying its native coordinates into SectionBody would be wrong;
this page queries PillIn/Occupied in SectionBody itself instead. None of those
helpers or accepted consumers is changed by this PR.

With side or bottom pill, bottom gap = max(0,H−pill.Bottom), without safe-bottom
clamping. Bottom bar obstruction uses existing SetupActionPlacement.BottomGap
and is added inside the footer above its normal 24pt gap. Top-wide pill uses
that helper's separate fallback, without pretending it anchors the base.
Missing native measurement retains 12 + ForScrolling.Bottom. Side gutters
remain 16 + covered sides once. Wide graph has no content footer, so its viewport clears the bar and glass. FilterCard
clears glass using the same host policy; footer reserves actual visible panel
height + bottom margin + 12pt through exit, recalculated on SizeChanged. No
fixed panel-height guess determines clearance (the existing animation-only
travel fallback is unchanged). Largest-text panel fit itself remains unverified.

Docked keyboard frames convert screen → window → SectionBody, then reduce the
finite viewport and lift the panel. ManualKeyboardScroll owns native keyboard
translation while active. Observers install once and dispose on departure;
central IActivePage restores ownership/placement on retained-tab return and
resume without fetching. CoveredEdges and Moved remain idempotent. Queued
placement/search/panel work and glass callbacks guard departure; departure
aborts panel/dim animation, and activation restores panel presentation.

### Current assessment and complete consumer dependencies

Statuses below are source-only: corrected means code changed, compliant means
contract retained by inspection, N/A means absent, unverified means no runtime
claim. All rows inherit the full device matrix below.

| Mode / owner | Source and presentation retained | Geometry / dependency status |
| --- | --- | --- |
| #469 Availability | Returned windows ordered by duration, Units.Window/Percent; finite 0–100 valid, neutral unavailable otherwise; original thresholds/text; recovered/ongoing/missing-start outages and known end retained | Corrected shared base/footer; first consumer, review/CI pending |
| #470 Sensors | Class groups, readings/units/limits, definitions, retained readings/Updating/freshness; no new classification | Corrected shared base; reassess after #469 merge |
| #471 Resources | CPU/memory/storage, bars/thresholds and clearing on refresh | Corrected shared base; reassess sequentially |
| #472 Ports | Existing complete row presentation, saved sorting/grouping, search and ifName graph route | Corrected base and measured panel clearance; redesign proposal still required under #472 |
| #473 Neighbours | Forward/reverse discoveries, IDs, selection/scroll, narrow graph and wide split | List reaches measured base; graph viewport clears bottom glass because it has no footer; graph content unchanged |
| #474 VLAN | IDs/names/count meaning, unavailable facts, ordering and highlights | Corrected shared base; subsequent reassessment required |
| #475 FDB | Merged MAC/interface/VLAN and unavailable labels, nonactionable facts | Corrected shared base; no replay of stale #475 code |
| #476 ARP | Merged full IP/MAC/interface/PortId, neutral unknown and order | Corrected shared base; merged presentation retained |
| #477 Routing | Merged BGP/OSPF/OSPFv3/VRF facts and text severity | Corrected shared base; merged presentation retained |
| #478 Wireless | All server classes/units/readings/own limits | Corrected shared base; full page assessment remains sibling-owned |
| #479 Inventory | One column, nesting, expansion, Show ports, serial clipboard | Corrected shared base; controls remain in list header/rows |
| EventLog compatibility | Loader/grouped generic rows remain supported | Same shared geometry if routed directly; normal entry uses Logs #482 |
| Graphs/Graylog/Unimus enum routes | Specialised pages, not DeviceSectionPage consumers | N/A to this change; #480/#481/#483 and backup contracts retained |
| DeviceDetailView / DeviceSectionCard | Embedded highlights/counts/30-day tile and section navigation | Compliant unchanged; no full-page geometry applied to overview |
| Setup #405 and other accepted forms | Separate setup helpers and acceptance records | N/A, no helper or acceptance reversal |

Source contracts: Availability concurrently requires GetAvailabilityAsync and
GetOutagesAsync, orders server durations, uses existing ServerTime UTC setting
and Units.Duration. No chart/calendar/period editor or persistent availability
cache exists. Refresh clears percentages rather than asserting stale freshness;
errors suppress empty, Retry is busy-disabled, device-generation guard retained.
Existing loader 404 maps to empty; this is an existing source contract, not proof
that historical availability exists. No history-completeness guarantee added.
Denied/offline/timeout propagate through existing status handling. Percent known,
invalid and missing collections retain existing wording. API/package, read-only
permissions, credentials, persistence and selected IDs are unchanged.

Action audit: glass has Back and search for every mode, plus Ports sort/filter;
Android/older iOS retain individual header Back/search/filter. Retry remains an
inline loading-error recovery action. Inventory expand/collapse, Show ports,
row expansion and serial copy remain local content actions. Port/neighbour row
navigation remains unchanged. Availability facts have no action. ButtonSearch
opens only from header/glass/Command-F; completion/unfocus/native drag closes
retaining query, and filtering/programmatic scroll must retain focus. No legacy
SearchReveal attachment. Theme/text/screen-reader and actual action usability
are unverified despite retained source contracts.

### Verification and evidence required

Code self-check: full main-to-head intended diff and staged diff inspected;
whitespace check passed. No local builds, tests, scripts or agents run.
Independent parent Opus 5.5 Medium full-diff review pending. Shared tests,
Android/iOS CI pending. Device evidence ALL unverified; source correction is
not a native pass. No acceptance, issue closure, epic tick, merge or release.

For EACH consumer/mode above and EACH trigger below record final integrated
SHA, CI/TestFlight build, device model/OS, theme/text setting, H (native body
height), B (pill bottom), V (actual list/graph viewport bottom), delta V−B,
keyboard/panel bounds, and complete final-row bounds/reachability. No actual
bounds/build/OS are available here; all numeric measurements remain unverified.

| Trigger / state | Observed | Expected and evidence location |
| --- | --- | --- |
| Side pill, bottom pill, top-wide, missing pill | Unverified | Body/Refresh/List bottom uses correct policy; side V−B=0; bottom footer clears actions; top-wide handled separately |
| Light/dark and largest accessibility text | Unverified | Full facts, first heading below 20pt fade, empty/error/Retry and complete final row readable; inspect viewport and row bounds separately |
| VoiceOver / TalkBack | Unverified | Each complete fact once, unknown neutral, every action named/operable; focus survives resize |
| Duo outer/inner portrait/landscape; ordinary iPhone portrait/landscape | Unverified | Finite viewport fills available base, columns update, graph split transition correct; no duplicate safe inset |
| Keyboard present → dismiss, typing/filter/programmatic scroll | Unverified | Viewport clears docked keyboard; dismissal restores measured base and actions without refresh/reentry; query/focus contract preserved |
| Fold/unfold, rotation mid-search/loading/panel | Unverified | Recompute native host/pill/keyboard and actual wrapped overlay bounds, full last row reachable |
| Tab return / child return / resume | Unverified | One active glass owner, actual caller Back, retained query, correct base and no reload required |
| Loading, source-empty, no match, denied/error/offline, Retry | Unverified | No false success/freshness, status not under fade; final content uses whole finite viewport |
| Known/unknown percentages and history unavailable | Unverified | 24h/7d/30d/365d/custom order, 100/99.9/99/95/invalid values; recovered/ongoing/missing times remain honest; no invented history |
| Short/long lists, narrow/wide graph, Ports panel open/exit/resize | Unverified | Graph/list share correct base; footer clears measured panel through exit; final row fully visible above obstruction |

The historical assessments' geometry-compliant labels are not current acceptance.
User-liked presentation elsewhere remains baseline. Parent reassesses siblings
sequentially after this shared correction merges, then obtains explicit device
acceptance under #442. DESIGN adds only the missing bounds-evidence lesson;
existing reusable inset/search/lifecycle rules and #405 acceptance remain intact.

### Independent review disposition

Opus 5.5 Medium reviewed 5ead2a9 against f7c7130 and found a blocking
wide-Neighbours bottom-bar regression. Parent substantiated the graph has no
content footer and fixed its viewport margin to overlayBottom, while the
list still uses measured base plus footer obstruction clearance. DESIGN and
DEVELOPING now state the distinct owners. Also fixed low native-frame timing
by dispatching body/handler/load/filter-size placement, and low activation
animation interference by aborting CardSlide/ListDim before restoring state.
Fresh full-diff review and replacement CI pending; all native checks remain
unverified. Ports landscape picker fit with the wheel/keyboard at maximum
text is a specific open verification: measure card/picker/Done bounds; if
clipped, correct the proven obstruction owner or provide scrolling, without
guessing a height. Extra keyboard-visible footer space is cosmetic and kept.

Fresh full-diff Opus review at 383f8d7 confirmed the graph correction and
found no merge blocker. The introduced keyboard-policy risk is explicitly
retained for device acceptance: this PR replaces MAUI automatic page shifting
with manual clearance on DeviceSectionPage. Picker wheels send keyboard
notifications, so Ports' non-scrolling panel may clip Sort/Group/Done in
short landscape, possibly even at default text. Estimates are not evidence;
measure actual card/picker/Done/keyboard bounds and correct any regression
before acceptance. Activation while a keyboard is already visible may lack
a frame until another notification; test retained return/resume in that state.
No guessed height correction is included.

Low documentation findings fixed: graph bounds are a separate device
verification, not something this code measures, and XAML comments distinguish
iOS measured-pillar placement from Android's existing fallback. No runtime
implementation changed after review. Final-head CI pending, all device
results still unverified. Review coverage did not include full sibling
assessments/comments; #470–#479 remain separate sequential reassessments,
not accepted by this shared review.
