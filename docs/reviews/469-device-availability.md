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
