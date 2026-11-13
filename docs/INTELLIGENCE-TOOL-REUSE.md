# Shared Intelligence tools

How Intelligence reuses DashyNMS/mcp's network tools instead of keeping its own
copies. The decision and first migration came from #596 (closed 2026-11-12).
Each remaining tool has its own issue, listed under
[Remaining work](#remaining-work). This document holds what stays true across
those issues. Progress is tracked in the issues, not here.

## Decision

Typed operations are extracted **inside DashyNMS/mcp's Agent library** and
published in one package, `DashyNMS.Agent.Shared`, with no `ModelContextProtocol`
reference. Each new operation joins that package rather than shipping its own, so
mobile pins and verifies one package. The MCP server
and mobile are both thin adapters over them. Core keeps transport, LibreNMS
models and alert grouping; a new Core API is only for behaviour every Core
consumer needs.

Mobile never copies an MCP query, parses an MCP tool's JSON, or calls
`AddDashyNmsAgent`. That registration adds its own transports, failover and
LibreNMS, Graylog and Unimus clients, which would replace the session's. Agent's
`DeviceDirectory` and `FleetCache` also cache for 60 seconds with no server key,
so a shared instance could hand one network's data to another.

## How find_device is built

`find_device` is the first migration and the pattern for the rest.

**Package.** `DashyNMS.Agent.Shared` `2.0.0-ci.43231337443.1`, from DashyNMS/mcp's
`shared-package.yml` run 43231337443 at commit
`c463e92a2ec7794a6f231bf6ccfd0ed9f41f50c3` (DashyNMS/mcp#124), SHA-256
`9207d92dfb7352881ed1bb7d8021beadf72b476396a04d91c8fb97f08c1a106f`. It was
`DashyNMS.Agent.Search` until DashyNMS/mcp#122 renamed it and restarted its
versions at 2.0.0. It is MIT licensed and targets net10.0. Its minimum Core is
`2.0.0-ci.64`, the same build as mobile's direct pin.
`scripts/verify-shared-package.sh` runs straight after restore in CI, Play and
TestFlight. It checks the hash, the build provenance
attestation, the nuspec's repository and commit, that the extracted files match,
and that there are no build-time assets. NuGet lock files are #609.

**Adapter.** `CompactNetworkLookups` creates one `HostDeviceSearch` per lookup
set (one model session and captured connection) and calls `FindDevicesAsync`
with a limit of 20. Ranking, trimming, paging and redaction come from upstream.
Mobile only maps the typed result to its compact profile: rows in the person's
device-name style with `[[device:ID]]`, the exact total in the heading,
unfetched and over-budget matches counted as omitted, and evidence only for the
rows shown. Each row's state comes from the summary's typed `State` (Core's
`ReportedState`), not its `status` word, which reads a missing flag as down and
doesn't tell ignored devices apart (#604). Maintenance isn't in the device list,
so search rows don't show it (#620).

**Host ownership.** Mobile passes its own clock, server timestamp setting and
`ConnectionBoundClient`. That client never uses the session's shared client.
It gets its own Core `LibreNmsTransport` from `IntelligenceDeviceTransports`,
pinned to the captured connection's address, backup and token, so a switch
can't redirect its requests. The host keeps that transport in step with the
session's timeout, trusted certificates, main or backup address and request
timing. Updates run one at a time, each reading the latest state. Failover
belongs to the session (`ISessionService.SwitchToBackup`), and a chat for an
earlier connection can't move a later one. Certificate prompts carry the session
generation they came from. Chats and lookups are bound by `SessionScope`
(connection generation plus server, backup and token), not by the connection
object, which Core replaces whenever a certificate is trusted or the timeout
changes. Without a transport factory, `find_device` fails closed.

**Allowlist and lifetime.** Device search permits `IDevicesApi.ListAsync`,
`IArpApi.FindByMacAsync`, `IDeviceGroupsApi.ListAsync` and
`IDeviceGroupsApi.GetMemberDeviceIdsAsync`. Location filtering reads the device
list, without a locations endpoint. For
`active_alerts`, `IAlertsApi.ListAsync(AlertQuery.Open)` is allowed. `device_sensors` has a
profile of its own: `IDevicesApi.ListAsync`, `ISensorsApi.ListAsync` and the sensor health
routes (see below), with no alerts, ARP or groups. Every other
endpoint, alert query, alert action and session control throws before transport. Each model attempt
owns an `IntelligenceLookupSet` and disposes it however the attempt ends, which
closes the transport. A switch or sign-out does the same straight away: waits
are cancelled, later calls are refused, and late replies and their evidence are
discarded.

**Cancellation and failures.** Caller cancellation propagates. Mobile's linked
15-second deadline and `WaitAsync` bound stay around the call. Failures arrive
as fixed upstream categories with no server text; mobile maps `Refused` to its
refusal line and everything else to its failure line. An empty result is not a
failure. Nothing is logged.

**Profile (#608).** `find_device` takes optional string arguments `query` (name,
IP or MAC), `status` (up, down or disabled), `group` and `location`. At least one
must be supplied. Filters can be combined or used without text. The native
schema passes a JSON object through the existing string bridge; plain text
callers still search by query. Unknown, duplicate and non-string arguments are
rejected before transport. The shared operation resolves exact names first,
then unambiguous partial names. Filter names are passed untouched, separate
from cleaned display labels; search returns device evidence, never group or
location evidence inferred from a filter.

MAC results name devices whose stored ARP tables contain the MAC. The heading
explicitly says observations do not prove ownership. Each row includes up to
two observed IP/port pairs, counts further sightings and keeps the normal device
reference. No ARP sighting becomes an endpoint ownership claim or a separate link.
Empty observations and refused reads remain distinct.

**Fixtures.** `tests/.../Fixtures/DeviceSearch` is a byte-for-byte copy of
DashyNMS/mcp's synthetic bundle, and `scripts/check-shared-fixtures.sh` fails CI
if it drifts. Over loopback HTTP, mobile `find_device` (through the per-chat
transports) and `HostDeviceSearch` (over Core's real client) must give the same
ids, order, totals and per-server request counts as MCP, including Core's
retries. Scope cases run through both the package and mobile on mobile's Core pin.
Additional synthetic HTTP cases cover MAC formats, observations, empty ARP and
refused ARP/group reads without changing the upstream fixture bundle. The fixture
README lists what isn't compared.

**CI.** Android Release and `iOS Release (device, unsigned)` (`ios-arm64`, Mono
AOT, shipping defaults, no `PublishAot`) fail on trim, AOT or NuGet warnings
naming the package. They record evaluated properties and sizes, and the iOS job
keeps the Foundation Models weak-link check. These prove compilation, not
execution; runtime and device checks are #607.

## How active_alerts is built

The second migration (#601), built on DashyNMS/mcp's `OpenAlerts` (DashyNMS/mcp#123).
Everything under find_device's host ownership, lifetime, cancellation and CI
applies unchanged; this section covers only what differs.

**Adapter.** Each lookup set creates one `HostOpenAlerts` beside its
`HostDeviceSearch`, over the same `ConnectionBoundClient` and transport, and asks
for up to 20 groups, optionally for one device id. Grouping by device and rule
(Core's `AlertGrouping`), the worst-first order, partial acknowledgement, the
representative fault, dropping recovered rows and redaction come from upstream.
MCP's `active_alerts` runs the same operation. Mobile maps the result to:

- a heading counting groups and faults apart, as MCP does:
  `Open alerts: 3 (11 faults) on 2 devices; 1 critical, 2 warning.`;
- one row per group: severity, state (`partly acknowledged (2 of 8)` when it is),
  rule, the representative's `[[alert:ID]]` marker (its source, fault or legacy
  alert, travels in the evidence and not the text), the device in the person's
  naming style with `[[device:ID]]`, and
  for a group of several its fault count with up to three fault names;
- groups not fetched or not fitting counted as omitted from the totals;
- evidence only for rows shown: the representative under its marker key, typed
  `fault` or `alert` with device and rule, and the device with its reported state.

**Identities.** Core's markers have no `fault` kind, so fault 12 and legacy alert 12
share `[[alert:12]]`. Replies pass every distinct object through, and when two
different objects claim one marker key, `IntelligenceTurn.AddEvidence` withdraws
that key for the rest of the answer, whether the two came in one reply or in
separate lookups, and whichever came first. The withdrawn objects travel with the
turn into later turns and saved chats, so the key stays withdrawn there too. The group key is never offered as an id.

**Failures.** No partial success: if the device list or alerts can't be read, the
lookup fails rather than reporting no alerts. `Refused` maps to the refusal line,
everything else to the failure line.

**Profile.** Name and single `query` argument unchanged: a positive device id or
empty. Severity, scope, time windows and paging stay unadvertised.

**Fixtures.** `tests/.../Fixtures/OpenAlerts` is a byte-for-byte copy of
DashyNMS/mcp's: a 26.10 server and an older one, with a fault and a legacy alert
both numbered 12, eight faults on one rule with two acknowledged, a recovered fault
beside its open siblings, and failures on the alert routes.
`scripts/check-shared-fixtures.sh` covers it. Over loopback HTTP, mobile
`active_alerts` and `HostOpenAlerts` must give the same groups, representatives,
states, counts, totals and request logs as MCP, and fail alike. Cases mobile can't
ask (severity, location, paging) run through the package alone.

## How device_sensors is built

The sensor migration (#603), built on DashyNMS/mcp's `DeviceSensors` (DashyNMS/mcp#129,
published at `812266d` as `2.0.0-ci.43365576634.1`).
Everything under find_device's host ownership, lifetime, cancellation and CI applies
unchanged; this section covers only what differs.

**Adapter.** Each lookup set creates one `HostDeviceSensors` over its own sensor-profile
`ConnectionBoundClient` and transport, as the overview has its own, and asks for one device id.
Judging each reading, the problem-first order, the device-by-device fallback and
redaction come from upstream. MCP's `device_sensors` runs the same operation. The
assessment is MCP's: the sensor's own LibreNMS limits, Core's default thresholds for dBm,
signal, temperature and fan speed where it has none, and an installed state definition's
severity. Desktop doesn't give MCP the person's own thresholds, so chat doesn't use
them on either platform: a reading can differ between chat and the Health page when
someone has changed Settings › Thresholds. Fixing that is an upstream change for both.
Mobile maps the result to:

- a heading with every count, unknown kept apart from OK:
  `Sensors on core-sw1 [[device:1]]: 9; 2 critical, 2 warning, 1 OK, 4 unknown.`;
- one row per reading, worst first: description, `[[sensor:ID]]`, class, value and unit or
  the state definition's label, and the state. A problem names the sensor's own critical
  limits (`critical (high limit 80)`). A missing reading reads `no reading`, never `0`.
  A reading with nothing to judge it by reads `not assessed: no limits`; one in the dBm
  "nothing plugged in" band reads `state unknown`. Neither is ever called OK;
- readings that don't fit counted as omitted from the totals;
- evidence only for rows shown: each sensor with its state (`critical`, `warning`, `up` for
  OK, `unknown` otherwise) and the device with its reported state.

**Sensor routes.** When LibreNMS refuses this account the network-wide list (403),
upstream reads the device's own health routes instead, which Core's client doesn't have
yet. `ConnectionBoundClient.SensorTransport` hands it typed reads of exactly
`devices/{id}/health`, `devices/{id}/health/device_{type}` and
`devices/{id}/health/device_{type}/{sensor}` on the captured connection's transport.
Processors, memory pools and storage, every other path, raw reads and writes are
refused before transport. The refusal is remembered for the lookup set, so later
questions in the same attempt go straight to the device's routes.

**Definitions.** The lookups get `SensorDefinitions.ReadOnlyStore`, a read-only store over
the app's cached catalogue, as desktop gives MCP. A question never downloads or writes
definitions; it rereads the saved choice, so turning them off applies at once.

**Failures.** No partial success. `Refused` maps to the refusal line, a device the list
doesn't hold to `No device with that id is visible to this account. Use find_device for its id.`,
everything else to the failure line.

**Profile.** Name and single `query` argument unchanged: a positive device id. The type
filter stays unadvertised.

**Fixtures.** `tests/.../Fixtures/DeviceSensors` is a byte-for-byte copy of DashyNMS/mcp's:
a zero fan reading beside a missing one, readings judged by their own limits and by the
defaults, a dBm reading in the "nothing plugged in" band, a voltage and a state sensor with
nothing to judge them by, another device's sensor, the device's health routes for the
fallback (listing processor and storage types that must never be read), and failures on
the network-wide list and the device route. `scripts/check-shared-fixtures.sh` covers it.
Over loopback HTTP, mobile `device_sensors` and `HostDeviceSensors` must give the same
readings, order, states, values, totals and request logs as MCP, and fail alike. The
fallback reads concurrently, so its requests are compared as a set. The type case runs
through the package alone. Installed state definitions need a catalogue on disk and
are covered in Core, not here.

## Rules every shared tool keeps

- **The host owns the clients.** Transport, certificate trust, failover,
  credentials, caches and timestamp settings come from the session. Directories
  and caches are created per connection generation. Integration tools (Graylog,
  Unimus) stay disabled until mobile has a captured, consented configuration for
  them, and never read server environment variables.
- **Read-only allowlist.** The wrapper allows only the endpoints the tool's
  review names, and unknown tools fail before transport. A tool is offered to
  the model only if it's in Core's `IntelligenceToolAllowlist`, the static
  registry and the current task profile. No reflection-based MCP discovery
  ships on mobile.
- **Safe failures.** Model text, evidence and diagnostics never contain server
  messages, raw bodies, URLs, exception text or stack traces. Upstream returns
  fixed categories, and mobile maps them to fixed lines. Partial component
  failures follow the same rule.
- **Untrusted data.** Device, log and configuration text stays untrusted model
  input. Upstream redaction and mobile's `Clean` are both kept; neither replaces
  the other.
- **Compact output from typed rows.** At most 900 characters per reply, built
  from the typed result rather than by slicing upstream JSON. Rows are whole,
  with names and intact ids. Omissions and failures are stated truthfully.
  Evidence comes only from rows shown, and an aggregate count never becomes an
  entity link. Alert groups use a device/rule key distinct from fault ids, so a
  fault and a legacy alert with the same number never share ownership.
- **Budgets.** Definitions reserve 80 tokens each, with 160 reserved for the
  four-argument device search. This is a conservative allowance, not a new
  device measurement. The conversation budget in `IntelligenceConversationViewModel`
  reserves 630 fixed, 720 for two
  results and 400 for output, within 80% of the reported context. Measured
  figures are in [DEVELOPING.md](DEVELOPING.md#what-the-on-device-model-can-do-570).
  Tools are chosen by task; all 24 schemas are never placed in the on-device
  context.
- **On-device only.** Nothing here changes the deferred ChatGPT route. Cloud use
  needs its own consent and data-minimisation profile first.

## Migration done means

A lookup counts as migrated when its PR, and the upstream PR it pins, show all
of these:

1. The upstream typed operation is in an Agent package with no MCP reference,
   published from `main` with a provenance attestation, and MCP's own tool uses
   it.
2. Mobile pins the exact version, and `verify-shared-package.sh` covers the
   package.
3. Upstream's synthetic fixtures are copied byte for byte, the check script
   covers the copy, and the cases include the issue's named edge cases.
4. Mobile's tool and the package give the same results as MCP over loopback
   HTTP, including failures.
5. Tests cover host-owned transport, a connection switch mid-request,
   cancellation, the deadline, refusals, the endpoint allowlist and the absence
   of server text.
6. Android Release and iOS Release CI pass with no new trim or AOT warnings.
7. Mobile's old query, ordering and formatting for that tool are deleted, and
   this document's tables are updated.

Device runs, token measurements and size deltas are recorded separately (#607)
and never inferred from CI.

## Current catalogue

DashyNMS/mcp's Agent has 24 tools, all in Core's allowlist and marked read-only.
Being on the list doesn't mean mobile supports a tool. To refresh, count the
`McpServerTool(Name = ...)` attributes under `src/DashyNMS.Agent/Tools`
and compare them with `IntelligenceToolAllowlist.Names`.

| Tool | Capability | Mobile |
| --- | --- | --- |
| `find_device` | Ranked name/IP search, MAC via ARP, status and scope filters, paging | Shared, MAC and scope filters (#608) |
| `network_overview` | Device counts, grouped alert and fault totals, ports, system version, partial failures | Shared, compact rows and evidence (#600) |
| `active_alerts` | Current alerts grouped by device and rule, separate fault totals, filters, paging | Shared, device id only (#601) |
| `device_health` | State, uptime, maintenance, alerts, CPU/memory/storage, problem ports | Nearest to mobile's `device_summary` (#602) |
| `device_sensors` | Threshold-assessed readings, problems first | Shared, device id only (#603) |
| `alert_details`, `alert_rules` | Fault and rule detail, history, rule search | Not offered (#610) |
| `list_groups_and_locations` | Group and location catalogue | Not offered (#610) |
| `device_details` | Inventory, VLANs, FDB, ARP, PoE, wireless, routing, addresses | Not offered (#610) |
| `device_neighbours`, `port_status`, `trace_endpoint` | Links, one port's state, tracing through stored ARP/FDB | Not offered (#610) |
| `top_devices`, `top_errors`, `top_interfaces`, `health_overview` | Ranked metrics and scoped sensor assessment | Not offered (#610) |
| `device_events`, `network_events`, `what_changed` | Event and change history | Not offered (#610) |
| `search_logs` | Graylog messages | Needs Graylog; not offered (#610) |
| `config_backup`, `config_diff`, `recent_config_changes` | Unimus configuration | Needs Unimus; not offered (#610) |
| `graph` | Graph images as MCP content | Needs image handling; not offered (#610) |

There is no Agent `device_summary`; #602 decides its contract. No write,
acknowledgement, rediscovery or remote MCP service is proposed.

## Remaining lookups and their differences

| Mobile lookup | Difference from the shared tool | Issue |
| --- | --- | --- |
| `device_summary` | No exact equivalent: `device_health` fans out much further. Keep the location id with the exact filter name, and the person's device-name choice. | #602 |
| Former diagnostic `NetworkOverviewLookup` | Removed with the diagnostic under #595. Historical measurements remain documented; there is no diagnostic lookup to migrate. | (#595) |

## Remaining work

- #620: search rows say when a device is in maintenance.
- #609: NuGet lock files for the Core and Shared pins.
- #602: shared device summary.
- #607: trimmed Android and installed iOS runs, tokens and size deltas.
- #610: further catalogue tools (epic), one issue per group as each starts.

## Shared network overview (#600)

Intelligence consumes `HostNetworkOverview` from Agent.Shared, published by
DashyNMS/mcp#125 at `7a84405` as `2.0.0-ci.43332463880.1`. The operation owns
counts and bounded alert groups, down devices and down locations. Mobile adapts
those typed rows to its 900-character profile, names and evidence. Its previous
query and alert ordering are removed. Alert groups and faults have separate totals.
Counts describe data visible to the account. Group and fault identities stay distinct.

Device data remains required, as in MCP. Alerts, ports and system are independent
optional sources. A failure keeps other data, marks its totals unavailable and
returns a fixed category without server text. Failure notices precede detail rows,
so the compact budget cannot hide them. Omitted rows are counted and contribute
no evidence; counts and location summaries never create entity references.

Overview has its own per-attempt, connection-bound client. Its reviewed reads are
the device list, open alerts including Core's source probe, fleet port status and
system information. Search and active alerts retain their narrower client profile.
Session changes and disposal retire both clients. The same 15-second deadline,
caller cancellation, certificate handling, failover and late-result checks apply.

The NetworkOverview fixtures are copied from MCP and checked byte for byte. Both
repositories exercise complete and refused-port cases over loopback HTTP through
Core. Mobile checks matching counts, displayed rows, evidence and partial failures,
plus retirement, cancellation, deadlines and the endpoint boundary. Release CI
continues to verify package provenance and reject package trim/AOT warnings.

Diagnostics' former `NetworkOverviewLookup` was removed under #595 and stays
removed. No diagnostic query is retained or migrated. Device measurements remain
tracked separately in #607.
