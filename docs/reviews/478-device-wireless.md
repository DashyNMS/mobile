# Device Wireless assessment, #478

Refs #478 and #442. Clean isolated baseline and fetched main are
17f747981176242b8ee3c2af5eaf6fb9c16bf651. Live #478 and #442 are OPEN with
no comments. No open PR or remote Wireless branch was found before editing.
Read CLAUDE.md, DEVELOPING.md, DESIGN.md, DESIGN-REFERENCES.md, the reconciled
assessment and integrated #469–#477 contracts. Previous green merged work is
baseline evidence only. Parent owns independent Opus 5.5 Medium review, CI
judgement and merge. This implementation starts no agents or watches.

## Assessment before implementation

C means source compliant, correction means a confirmed defect to fix here,
N/A means absent capability and U means unverified. Native results remain U
independently of every source disposition.

| Concern | Disposition and code evidence |
| --- | --- |
| Every metric | C WirelessAsync retains all non-deleted classes, Core SortRank and description ordering; no clients/channel/signal-only filter. Core WirelessSensorClasses supplies names and units. |
| Identity and long facts | Correction: legacy DeviceSectionPage grid truncates Title and caps Value; DeviceDetailView overview loses class context. Reuse existing SensorLayout stack, Subtitle class name, full joined SensorDescription and explicit status. No unsettled visual policy change. |
| Unknown and health | Correction: AgainstOwnLimits returns OK even with no limits, and NaN limits silently fail comparisons. Missing/non-finite readings stay unavailable; absent limits are neutral No limits; any non-finite limit is neutral Limits unavailable. Finite limits retain inclusive critical-before-warning classification. Do not infer signal quality from class or colour alone. |
| Overview/full list | C DeviceSectionCard counts every row, selects three worst, hides successful empty and retains failures visibly. Correction shares wrapping facts/state with full section. |
| Source outcomes | C one GetWirelessSensorsAsync call only. Unsupported 404 becomes empty, other denial/timeout/server failures propagate. Multiple-source mixed-failure checkpoint N/A. OptionalSectionSourceAsync stays unchanged. No metadata or stronger permission fetch. |
| Loading/empty/no match | C DeviceSectionViewModel IsEmpty/EmptyText and page BusyCard/ErrorLine/Retry distinguish states and suppress false empty during failure/load. |
| Refresh/offline/handoff | C generation and pending request guards prevent superseded scope publication. Failed Wireless refresh retains previous rows with error. No durable Wireless cache or timestamp; retained values are not freshness evidence. U native timing/offline. |
| Entry and return | C DeviceDetailViewModel.OpenSectionAsync supplies selected DeviceId, section and name to relative DeviceSection route, registered in AppShell. Device overview callers include Devices narrow/split, Dashboard, alerts and other device links. Relative Back returns to actual caller. Wireless rows have no destination. U each native return. |
| Search and keyboard | C ButtonSearch/header/glass/Command-F opening; actual drag/completion/unfocus dismiss, query retained. SensorLayout Matches includes kind/status and Subtitle, so displayed class/state become searchable. U native focus during replacement/programmatic scroll. |
| Geometry/glass | C SectionBody finite viewport, PlaceControls live pill base and footer obstruction, top fade, Never native scroll inset; Activate reclaims watchers/glass, disappearance removes them. No geometry changes. U native H/B/V and final-row reach separately. |
| Themes/accessibility/motion | C existing themed StatusText, wrapping labels, joined speech and excluded child labels; explicit status avoids colour-only meaning. Existing reduced-motion search, no new animation. U actual contrast, roles, focus, maximum text and speech. |
| Writes/security | N/A editor, Save, selection, comparison, export, sort/filter panel, per-row navigation. C credentials, storage, permission policy, persistence, routes and server content unchanged. |

## Pinned source contract

Mobile references DashyNMS.Core 2.0.0-ci.52. Confirmed provenance is Core run 52,
commit 5f7bde53ac05ea75f7cb6dd06a8fae62606f0769. Inspect that source rather than
copying an API into mobile. DevicesApi calls devices/{id}/wireless-sensors,
collection wireless_sensors, with NoneFound mapping the specific no-sensors
404 response to empty; mobile's outer LoadAsync catch also treats 404 as empty.
Primary denial remains failure, never healthy/empty. Only one source exists.

WirelessSensor fields are sensor_id/device_id, sensor_class, sensor_descr,
sensor_index, nullable sensor_current and four nullable sensor_limit fields,
sensor_deleted, lastupdate and AdditionalData. SensorId is internal fallback
identity, not a channel/cell number or displayed reading. Index is not inferred
as a channel. Current supplies the actual reading. Deleted rows are omitted;
LastUpdate is not currently rendered as freshness. No new timestamp inference.
Missing numeric IDs/deleted flags use DTO defaults; typed-model tests cannot
establish payload completeness. Unmodelled AdditionalData is not interpreted.

Core classes and units: ap-count/clients/errors/mcs/cell/channel have no suffix;
capacity/ccq/error-ratio/quality/utilization use percent; error-rate/rate use
scaled decimal bps; frequency MHz; distance m; mse/snr/sinr/rsrq/ssr/xpi dB;
noise-floor/power/rssi/rsrp dBm. Unknown classes retain Core's tidied name and
unitless value. Errors is not invented as errors/s, nor is channel an internal
ID. The formatter's rounding/scaling is preserved. Null and non-finite current
are intercepted before formatting. No domain ranges or global Health signal
thresholds are invented. Contradictory finite bounds retain existing priority;
source contract for unusual ranges and complete real payloads remains U.
Core LibreNmsJson allows numbers read from strings. LibreNmsTransport returns
empty for missing/null collections, rejects non-arrays and wraps JSON mapping
failures in LibreNmsApiException. No parser is mirrored here. This source
inspection does not validate complete real server payloads or fields; those
remain UNVERIFIED. Subtitle carries the class visibly and once in joined speech,
without also assigning SensorKind. These are Wireless rows from the dedicated
endpoint, not the fleet Sensors source.

## Shared consumer dependencies and ownership

All eleven DeviceSectionPage consumers are accounted for before edits:

| Consumer | Preserved owner/contract |
| --- | --- |
| Availability | #469/#530 measured viewport base, explicit unknown percentages/outages |
| Sensors | #470/#531 definitions, finite readings, neutral unassessed values |
| Resources | #471/#533 own units/bars and per-source 404 |
| Ports | #472/#534 approved adaptive cards, window sizing, saved filters, ifName graph action |
| Neighbours | #473/#535 graph selection/readout and theme |
| VLANs | #474/#536 membership/source failure distinctions |
| FDB | #475/#537 actual VLAN tags and optional metadata permissions; formatter unchanged |
| ARP | #476/#538 ARP-only raw MAC and source failures; no FDB formatter correction |
| Routing | #477/#539 all four protocols, source-local unsupported handling |
| Wireless | #478 owns only its loader row construction; consumes existing SensorLayout |
| Inventory | #479 retains nesting, expansion, port toggle and serial copy |

Owner parent DeviceDetailViewModel/DeviceSectionCard and both templates remain
unchanged. Graphs, event logs, Graylog and backups keep specialised pages.
Shared non-Port gestures, layout, helper, activation and observers are unchanged.
No contact with dirty #475 checkout or any other checkout for edits. DESIGN.md
already covers wrapping, unknown health, source failures, optional permissions
and measured base versus footer. No new durable lesson beyond #515/#530/#536/
#537 is needed. No duplicate design rule is added.

## Evidence and acceptance still required

Review: full intended diff against fetched main and DESIGN.md, then staged diff
and git whitespace inspection. Parent independent full review pending.
CI: new meaningful typed-row and loader/overview regressions are authored for
CI only. Shared tests, Android/iOS builds and security checks pending on new
head. No local build, test, restore or validation script was run.
Device: all rows below are separately UNVERIFIED. No source or CI result is
native acceptance. No device/build/diagnostic session is available here.

| Native matrix | Required observation |
| --- | --- |
| Bounds | Record host H, pill bottom B, viewport bottom V and V minus B in common coordinates; side, bottom and top glass separately. Independently reach complete last row; check first heading below fade, short/long/empty/error lists and native camera occlusion. |
| Devices/themes/text | Duo outer and inner portrait/landscape, ordinary iPhone portrait/landscape; light/dark, maximum text, long unbroken identity/class/value, all supported metric classes and unknown classes. |
| State/source | Null/NaN/infinite current; absent/finite/non-finite bounds; inclusive low/high warning/critical and no-limit neutral. Cold load, all empty/deleted, unsupported, denied, 500, offline, refresh success/failure/Retry and retained rows with error. No false OK or freshness claim. |
| Handoff | Slow device 7 then 8 must never publish old scope. Overview count/worst three/state/facts agree with full section. Use synthetic example.net identities only. |
| Keyboard/search | Header/glass/Command-F; class/state/value search, no match/clear; typing/filter replacement/programmatic scroll retain focus. Actual drag/completion/unfocus retain query. Keyboard-open last row and dismissal restore base. |
| Routes/lifecycle | Devices pinned and More-pushed, narrow/split, Dashboard/alert/device links; section Back to actual caller, child return, tab return, retained stack and background/resume without Refresh. One glass owner and no placement loops. |
| Fold/motion | Fold/unfold/rotate during load/search/refresh, Reduce Motion on/off and repeated transitions; recompute live bounds without reopening. |
| VO/TB/actions | Read full identity, metric, value and explicit state once; no inert-row navigation promise. Overview opens Wireless once; Back/search/Retry work and busy guards hold; check native target sizes and contrast. |
| Siblings | Smoke all eleven rows above, Ports filter/graph return, neighbour theme/graph, VLAN source failures, FDB tag/permission, ARP MAC and Routing protocol errors. |

Tie results to final SHA/build/OS and Tab bar buttons/Top line diagnostics.
No user acceptance, closure, epic tick, merge, release or watch in this pass.
