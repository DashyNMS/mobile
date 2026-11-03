# Device Neighbours review, #473

Refs #473 and #442. Baseline main `2c7f32f`. Read live issues and repository
CLAUDE.md, DEVELOPING.md, DESIGN.md and DESIGN-REFERENCES.md before assessment.
Routine guide choices are authorised. This PR is prepared for the parent queue;
it does not merge, watch, release, close issues or tick the epic.

## Assessment before editing

| Concern | Baseline evidence and disposition |
| --- | --- |
| Modes | DeviceSectionPage shows grouped LLDP/CDP cards and a scrollable star graph; 800pt usable width puts graph beside list. Device detail embeds five highlights. All assessed; network graph/saved groups/editor remain #450/#451, link detail #452 is preserved. |
| Long content/hierarchy | Fix required: title truncation, side protocol column and capped detail squeeze endpoints. Neighbour-only complete wrapping readout puts name, local-to-remote ports, protocols, platform and labelled readings in order; overview uses same facts. |
| Activation | Fix required: gesture-only card. Monitored neighbour gets a full-width native wrapping button with facts and explicit Open neighbouring device action. Unmonitored neighbour is a plain readout with existing Not monitored explanation. Legacy gesture moves onto legacy grid to avoid duplicate activation. |
| Unknown/status | Fix required: non-up port states become Down. Only explicit up/down receive severity; absent/testing/unknown states remain neutral. Local port state label avoids claiming remote endpoint health. Inactive discovery keeps precedence. |
| Readings | Fix required: negative/nonfinite/overflow traffic can appear valid. Unknown replaces invalid readings; valid zero remains valid and octets are converted to bits. No new readings or thresholds. |
| Discovery and IDs | Compliant: Core DeviceNeighbours.Build retains own and reverse discoveries, deduplication, matching and remote IDs. No mobile matching fallback added. |
| Graph/list | Compliant selection contract: node selects and scrolls to same filtered row; list activation opens neighbour, not link detail. Fix required: fixed node lanes clip long text; wrap and size lanes for names. Native measurement remains unverified until CI/device checks. |
| Stable viewport | Fix required: fixed 280pt graph crowds short portrait/landscape/keyboard list. Narrow graph capped to 35% of actual body height, recomputed on body resize; wide graph retains star height. |
| Loading/empty/errors | Compliant busy first load, source empty versus no search matches, pull refresh and error suppression of empty. Existing retained rows can age during refresh/failure and are not fresh-health evidence. #507 owns Retry and request race protection. |
| Search/lifecycle/fade | Shared fixes required and owned by #507. Do not duplicate ButtonSearch, IActivePage, Retry, disposable watcher, native drag checks or list fade/spacer. Local matching already searches fields, including new explicit state. |
| Insets | Compliant source ownership: manual iOS list inset Never, measured sides, finite list viewport above bottom glass plus gap, 24pt footer. No second inset owner introduced. |
| Hardware/accessibility/motion | Unverified. Native button, textual state and wrapping improve source contract; no device acceptance inferred. Existing reduced-motion search/graph scroll behaviour preserved. |

## Callers, returns and server contracts

DeviceDetailViewModel opens relative DeviceSection with DeviceId, Neighbours
enum and display name. Device detail can originate in Devices narrow/split,
Dashboard, alerts and other device links. Card opens Neighbours; row opens
relative DeviceDetail with the actual positive RemoteDeviceId. Back returns
to the section and original caller; no new route or navigation to #452 added.
Graph nodes only select/scroll and never infer a device ID from text.

Loader concurrently reads device links, fleet links and optional device ports.
Core builds both directions, including ping-only devices. Reverse device names
use existing server lookup and configured naming style. Non-authentication port
lookup failure still permits links with unknown readings; link failures remain
errors. Authentication, credentials, permissions, package pin and settings are
unchanged. No writes, saved selection, server schema or new endpoints introduced.
Search is transient and retained on dismissal. Existing ordering, card totals
and five-highlight choice remain unchanged. Only synthetic data added to tests.

## Exact pending integration after #507–#510

Read supplied sibling checkout review records 469–472 and relevant diffs
read-only. No sibling branch was changed or unmerged commits imported. Parent
must update only this branch at its queued serial review turn after #510.

1. Combine page specialised stacks under one wrapper, retaining independent
   AvailabilityLayout, SensorLayout, ResourceLayout, PortLayout and
   NeighbourLayout flags and every legacy-grid hide trigger. Keep one native
   action per port/neighbour and legacy inventory child commands.
2. Combine overview specialised readouts and hide triggers. Neighbour overview
   remains a readout inside the card that opens the section, not a nested action.
3. Keep #507's IActivePage, ButtonSearch, SearchButton, Retry, serial generation
   guard, disposable watcher, idempotent glass subscriptions and list-column
   fade with 20pt spacer. Keep #508 sensor freshness/Updating and #509 resource
   clearing. Do not restore the baseline unguarded load or manual search.
4. Preserve #510 ports-only keyboard close and stacked filter panel. Keep this
   PR's body SizeChanged placement and narrow graph height cap, and position
   #507's fade in the list column in both neighbour modes.
5. Merge SectionRow flags and tests without dropping sibling search clauses or
   reading validation. New neighbour flag defaults false in other consumers.

Shared consumers #469–#479 retain availability windows/outages, all sensor
classes/limits/definitions, resource units/bars, port sort/group persistence and
ifName route, neighbour reverse IDs/selection, VLAN/FDB/ARP fields and order,
routing protocol state, wireless endpoint/units, inventory tree/expansion/ports
toggle/serial copy. Graphs, logs, Graylog and backups keep specialised routes.
Setup #405 acceptance and #288 global theme/mock-up ownership remain intact.

## Validation and remaining device checklist

Full intended and staged diffs self-reviewed against the guide; git diff --check
is the only local validation. No local builds, tests or scripts run. Added
unexecuted regressions for unsupported states, invalid traffic, valid zero,
complete action facts and positive remote ID. Existing reverse-only, optional
ports, unmonitored neighbour and navigation tests remain relevant. Shared tests
and Android/iOS CI pending. Parent mandatory Claude Opus 5.5 Medium review is
pending quota; no Claude or delegate started. Hardware remains unverified.

Record final integrated SHA/build and results separately for every row below.
Use example.net and edge.example.net, IDs 7/8 and long synthetic interface names.

| Check | Required steps; all currently unverified |
| --- | --- |
| Endpoints | Own-only, reverse-only ping device, both directions deduplicated, LLDP/CDP, monitored/unmonitored, absent ports/platform/name, remote ID fallback; correct local-to-remote meaning and actual ID navigation |
| State/readings | Up/down/testing/missing/unknown and inactive discovery; zero/negative/NaN/infinite/overflow rates; neutral unknown, no fabricated remote health, correct bits and speed |
| Graph/list | Short/long/empty lists, node selects and scrolls same row, row opens device once, filter removes selection, graph/list stay on same filtered snapshot; 800pt split transition |
| Failure | Cold load, success empty, no matches, denied/offline/timeout, optional ports failure, refresh success/failure/Retry; slow device switch after #507 integration |
| Themes/text | Both themes, largest text, long names/ports/protocol/platform, wrapping native button and graph nodes; initial heading clear of fade and final row fully reachable |
| VoiceOver/TalkBack | Complete endpoint/state facts and one correct activation; unmonitored readout has no false action; node selection, Back/search/Retry and overview card usable |
| Search/keyboard | Header/glass/Command-F, local endpoint/protocol/state query, typing retains focus, user drag/completion/unfocus retain query, no swipe reveal; final row reachable with keyboard and after dismissal |
| Duo | Outer portrait, inner portrait, inner landscape and supported outer landscape; fold/unfold and rotate while typing/loading; finite stable list viewport, no duplicate inset or stale glass owner |
| Resume/routes | Devices narrow/split, Dashboard and alert to detail to section to neighbour and Back; background/resume at each level, caller and query retained |
| Ordinary iPhone | Portrait/landscape, both themes/largest text, VoiceOver, keyboard, Reduce Motion, older header and iOS 26 glass where available; final row and graph selection reachable |

User acceptance remains required before #473 closure or #442 ticks. Parent owns
integration, review, merge and any later TestFlight delivery.
