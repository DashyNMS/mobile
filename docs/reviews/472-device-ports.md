# Device Ports review, #472

Refs #472 and epic #442. Baseline main `2c7f32f`. Read live issues,
CLAUDE.md, full DEVELOPING.md, DESIGN.md and DESIGN-REFERENCES.md before
assessment. Routine guide choices were authorised. This is a prepared PR,
without merge, release, watch, issue closure or epic ticks.

## Assessment before implementation

| Concern | Baseline finding | Disposition |
| --- | --- | --- |
| Modes | Ports list, search, sort/group panel, embedded busiest-port highlights and ports-up tile | All assessed; graph presentation stays with #481 |
| Long content | Fix required: title tail truncation and 180pt/two-line traffic cap squeeze names and rates | Port-only wrapping presentation, including overview highlights |
| Activation | Fix required: gesture-only row lacks a native accessible action | One full-width wrapping button names facts and graph action; missing ifName is a plain readout with unavailable explanation |
| State | Fix required: every non-up, non-admin-down state reported Down | Only explicit down is Down; missing/testing/unrecognised states are neutral Unknown, searchable and grouped separately |
| Traffic/errors | Fix required: absent errors imply OK; invalid rates can look like valid traffic | Missing/nonfinite/negative traffic says Unavailable; incomplete readings are neutral; positive finite errors retain Warning |
| Units/thresholds | Compliant: octets/s multiplied by eight through Units.Bits, speed via Units.Speed, positive errors warn | Preserved, no new threshold or derived server reading |
| Ordering/persistence | Compliant: PortArrangement, ports.sort and ports.grouping, VLAN/trunk/type/name/index/traffic/speed/description ordering | No persisted names, default, schema or ranking calculation changed; Unknown adds rank 3 after existing ranks |
| Filters/keyboard | Fix required: fixed 80pt label column crowds largest text; panel can open over keyboard | Stack labels/pickers; ports-only panel opening dismisses search and keyboard, retaining query |
| Search | Existing title/alias/value/detail local matching compliant; state and real ifName absent | Add port-only status and ifName matching; #507 owns ButtonSearch/native drag policy |
| Loading/empty/error | Compliant: first-load BusyCard, error suppresses empty, source empty differs from unmatched query, pull refresh | Preserve; #507 owns Retry and race guard |
| Cached/offline | Retained rows during refresh/failure are last loaded facts without a timestamp | Existing behaviour preserved, not live or cached-freshness evidence; hardware failure sequence unverified |
| Insets/header/final row | Existing manual iOS list inset, measured side clearance, finite viewport above glass plus gap and 24pt footer compliant in source | #507 owns list fade/spacer and lifecycle correction; no duplicate inset owner |
| Themes/motion/accessibility | Unverified on hardware | Native button and explicit text supplied; no new motion; device matrix below remains required |

DeviceSectionPage and code-behind consume DeviceSectionViewModel, loader groups
and SectionRow. DeviceDetailView embeds DeviceSectionCard's five busiest rows;
its summary counts and positive-traffic selection remain unchanged. The tile
now stays neutral when unknown ports exist and no explicit down takes priority.
No graph selection or range editor is added here.

## Callers, returns and contracts

DeviceDetailViewModel opens the relative DeviceSection route with selected
device ID, Ports enum and display name. Detail is reachable through Devices
narrow/split views, Dashboard, alerts and other device links. Tile and card
open the same ports list. Back retains the actual caller. Row activation opens
relative DeviceGraphs with the same DeviceId, actual SNMP ifName, display name
and device name. A missing ifName never fabricates an ID from the description.
Graph Back returns to Ports with its search and arrangement; #481 owns graph
presentation and graph descriptions' order. Shell registrations are unchanged.

Deleted ports remain excluded. API permission failures remain errors; no new
endpoint, credentials, permission assumption, package, persistence or schema is
introduced. API aliases and names are preserved. Sort values and PortFacts
ranking stay in their existing byte units, independent of displayed bit units.
Other SectionRow consumers have PortLayout=false and retain existing matching.

## Exact queued integration after #507, #508 and #509

Read their availability, sensors and resources review documents and relevant
diffs in the supplied checkouts read-only. No commits were imported and no
other branch was changed. Parent updates this branch only at its serial
review/merge turn after #509, with mandatory Claude Opus 5.5 Medium review
after quota resets. No Claude reviewer or delegate was started.

1. DeviceSectionPage.xaml: combine specialised availability, sensor, resource
   and port presentations under one wrapper. Retain all four legacy-grid hide
   triggers. Keep the port button's command as the single port activation and
   the legacy grid gesture for other rows. Preserve inventory child commands.
2. DeviceDetailView.xaml: retain all specialised highlights and legacy-grid
   hide triggers. PortSummary intentionally omits a graph action because the
   overview card opens Ports, not the individual graph. Do not lose the
   resource bar or availability/sensor joined readouts.
3. SectionRow: combine independent flags and Matches clauses. Only rows with
   Port facts use these new summaries; do not apply them to wireless or other
   sections. Loader and tests must retain all sibling reading fixes.
4. Keep #507's captured section/device, serial handoff, generation guard,
   availability clearing, #509's resource clearing and #508's sensor Updating
   and cache freshness. Preserve #507's IActivePage, disposable watcher,
   idempotent glass, ButtonSearch, Retry, OnScroll=False and list-column fade
   with 20pt spacer. Add this ports-only keyboard close inside the existing
   panel-change callback; do not restore old shared search code.
5. The stacked sort/group panel remains in the existing measured glass-safe
   slot. Verify largest text and short landscape height after integration.

Consumers #469–#479 retain their contracts: availability windows/outages,
sensor classes/limits/definitions, resource units/bars, port arrangement and
graph identity, neighbour graph/reverse links/split pane, VLAN/FDB/ARP fields
and order, routing protocol states, wireless endpoint/units, and inventory
tree/ports toggle/serial copy. Graphs, logs and backups keep specialised routes.
Setup #405 acceptance is unchanged.

## Validation and exact remaining device checklist

Full intended and staged diffs self-reviewed and git diff --check only.
No local builds, tests or scripts run. Added synthetic regression cases for
unknown status, invalid traffic, warning text and actual graph identity.
Existing arrangement, persistence and route tests remain relevant. Shared
tests, Android and iOS CI are pending. Independent parent review and every
hardware result below are unverified, not inferred from source inspection.

Use example.net and edge.example.net, IDs 7/8, long synthetic interface names
and aliases; record final integrated SHA and CI build with each result.

| Check | Required steps | Result |
| --- | --- | --- |
| States/readings | Up/down/admin-down/disabled/missing/testing; zero/positive/missing/NaN/infinite/negative rates and errors; verify explicit text, bits versus byte sorting and speed/VLAN | Unverified |
| Arrangement | Every sort and grouping, natural numbered names, duplicate indexes, VLAN/trunk/no VLAN; relaunch and confirm persisted sort/group; search then reset query | Unverified |
| Loading/failure | Cold/empty/unmatched/offline/denied; refresh success/failure/Retry and slow device switch after #507; distinguish retained last-loaded data | Unverified |
| Keyboard/filters | Header/glass/Command-F, typing replacements retain focus, native drag/completion/unfocus close and retain query; open filters while typing, choose sort/group and Done | Unverified |
| Graph route/return | Tile/card to Ports, named row to actual ifName graph, missing name unavailable; graph Back retains term/order; Devices narrow/split, Dashboard and alert callers return correctly | Unverified |
| Themes/largest text | Light/dark, longest name/alias/value/filter choice, short/long list; initial heading clear of fade, final row reachable above glass and keyboard | Unverified |
| VoiceOver/TalkBack | One complete button readout, accurate action, one activation, 44pt target; nonlink explanation and overview card action; picker labels and Done reachable | Unverified |
| Duo | Outer, inner portrait, inner landscape; fold/unfold and rotate during search/filter/refresh; no clipping, stale glass owner or final-row obstruction | Unverified |
| Resume/iPhone | Background/resume at Ports and graph then Back; ordinary iPhone portrait/landscape, older header and iOS 26 glass where available, Reduce Motion and largest text | Unverified |

User acceptance is required before closing #472 or ticking #442. Parent owns
merges and any later TestFlight delivery.

## Sequential integration

Updated against main 5661748 after merged #507–#509. Port actions and filters
coexist with the Resource, Sensor and Availability templates; each hides the
legacy row independently. Both sides' regression tests are retained. Shared
activation, ButtonSearch, Retry, fade and generation-guarded refresh are unchanged.
Full-diff Claude review and CI must verify the combined XAML and contracts.
No hardware outcome is claimed.

## Independent review remediation

Claude Opus 5.5 Medium reviewed 071ded2 against main 5661748. Known
lowerLayerDown/notPresent states remain Down; traffic ranking and error totals
sum only finite nonnegative inputs. Repeated non-up state text is omitted.
Ports use a start-aligned wrapping readout rather than a multiline Button;
card-wide activation is restored for ports and other links without a duplicate
inner gesture. Native action traits still require device verification.
Regressions cover known-down states and invalid-rate ranking/error totals.
Replacement CI and fresh review are pending. No local builds/tests ran;
all hardware outcomes remain unverified, including missing error-rate behaviour.
