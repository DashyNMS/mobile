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

**Allowlist and lifetime.** Only `IDevicesApi.ListAsync` and, for
`active_alerts`, `IAlertsApi.ListAsync(AlertQuery.Open)` are allowed; every other
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

**Profile.** The model-facing name, purpose and single `query` argument are
unchanged. Search is by text only: MAC-shaped queries are refused before any
request, and ARP and group endpoints are blocked (#608).

**Fixtures.** `tests/.../Fixtures/DeviceSearch` is a byte-for-byte copy of
DashyNMS/mcp's synthetic bundle, and `scripts/check-shared-fixtures.sh` fails CI
if it drifts. Over loopback HTTP, mobile `find_device` (through the per-chat
transports) and `HostDeviceSearch` (over Core's real client) must give the same
ids, order, totals and per-server request counts as MCP, including Core's
retries. Scope cases run through the package on mobile's Core pin. The fixture
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
  rule, the representative's `[[alert:ID]]` marker with its source-qualified key
  (`fault:9200`), the device in the person's naming style with `[[device:ID]]`, and
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
- **Budgets.** Definitions stay within 80 tokens each, inside the conversation
  budget in `IntelligenceConversationViewModel` (550 fixed, 720 for two
  results, 400 for output, within 80% of the reported context). Measured
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
| `find_device` | Ranked name/IP search, MAC via ARP, status and scope filters, paging | Shared, text only (#608) |
| `network_overview` | Device counts, grouped alert and fault totals, ports, system version, partial failures | Mobile's own, narrower (#600) |
| `active_alerts` | Current alerts grouped by device and rule, separate fault totals, filters, paging | Shared, device id only (#601) |
| `device_health` | State, uptime, maintenance, alerts, CPU/memory/storage, problem ports | Nearest to mobile's `device_summary` (#602) |
| `device_sensors` | Threshold-assessed readings, problems first | Mobile's own, raw readings (#603) |
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
| `network_overview` | Counts raw open alert rows and lists alert and down-device evidence. Agent counts groups and faults separately and tolerates failed sources. Counts alone can't supply per-row evidence. | #600 |
| `device_summary` | No exact equivalent: `device_health` fans out much further. Keep the location id with the exact filter name, and the person's device-name choice. | #602 |
| `device_sensors` | Fleet list labelled raw with unknown health. Agent assesses limits, orders problems first, and falls back per device when the fleet endpoint is denied. Keep a missing reading distinct from zero. | #603 |
| Former diagnostic `NetworkOverviewLookup` | Removed with the diagnostic under #595. Historical measurements remain documented; there is no diagnostic lookup to migrate. | (#595) |

## Remaining work

- #620: search rows say when a device is in maintenance.
- #609: NuGet lock files for the Core and Shared pins.
- #600: shared overview.
- #603: shared sensor assessment. #602: shared device summary.
- #607: trimmed Android and installed iOS runs, tokens and size deltas.
- #608: MAC, group and location search in chat.
- #610: further catalogue tools (epic), one issue per group as each starts.
