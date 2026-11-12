# Shared Intelligence tools

How Intelligence reuses DashyNMS/mcp's network tools instead of keeping its own
copies. The decision and first migration came from #596 (closed 2026-11-12).
Each remaining tool has its own issue, listed under
[Remaining work](#remaining-work). This document holds what stays true across
those issues. Progress is tracked in the issues, not here.

## Decision

Typed operations are extracted **inside DashyNMS/mcp's Agent library** and
published as packages with no `ModelContextProtocol` reference. The MCP server
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

**Package.** `DashyNMS.Agent.Search` `0.1.0-ci.43135054163.1`, from DashyNMS/mcp's
`search-package.yml` run 43135054163 at commit
`0bdf50e9249eafd880f1a5ee3bcd66e0d81549fe`, SHA-256
`731965d4bc971f62ff6ee49f47254335ee3295deea50184b9a49b2cdd7fcab1d`. It is MIT
licensed and targets net10.0. Its minimum Core is `2.0.0-ci.34`; mobile's direct
`2.0.0-ci.64` pin wins. `scripts/verify-search-package.sh` runs straight after
restore in CI, Play and TestFlight. It checks the hash, the build provenance
attestation, the nuspec's repository and commit, that the extracted files match,
and that there are no build-time assets. NuGet lock files are #609.

**Adapter.** `CompactNetworkLookups` creates one `HostDeviceSearch` per lookup
set (one model session and captured connection) and calls `FindDevicesAsync`
with a limit of 20. Ranking, trimming, paging and redaction come from upstream.
Mobile only maps the typed result to its compact profile: rows in the person's
device-name style with `[[device:ID]]`, the exact total in the heading,
unfetched and over-budget matches counted as omitted, and evidence only for the
rows shown. Each row's state comes from Core's `ReportedState` on the same
device list, because the shared summary reads a missing status as down (#604).

**Host ownership.** Mobile passes its own clock, server timestamp setting and
`ConnectionBoundDeviceList`. That client never uses the session's shared client.
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

**Allowlist and lifetime.** Only `IDevicesApi.ListAsync` is allowed; every
other endpoint and session control throws before transport. Each model attempt
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
DashyNMS/mcp's synthetic bundle, and `scripts/check-search-fixture.sh` fails CI
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
2. Mobile pins the exact version, and `verify-search-package.sh` (or its
   successor) covers the package.
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
| `active_alerts` | Current alerts grouped by device and rule, separate fault totals, filters, paging | Mobile's own, raw rows (#601) |
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
| `active_alerts` | Raw rows ordered unacknowledged, severity, id. Agent groups by device and rule and orders worst first. | #601 |
| `device_summary` | No exact equivalent: `device_health` fans out much further. Keep the location id with the exact filter name, and the person's device-name choice. | #602 |
| `device_sensors` | Fleet list labelled raw with unknown health. Agent assesses limits, orders problems first, and falls back per device when the fleet endpoint is denied. Keep a missing reading distinct from zero. | #603 |
| Diagnostics `NetworkOverviewLookup` | Separate baseline for Diagnostics (eight names of up to 40 characters, no evidence). Kept as it is until replaced on purpose, so its readings stay comparable. | (#600) |

## Remaining work

- #604: the shared summary reports ignored, maintenance and unknown states.
- #609: NuGet lock files for the Core and Search pins.
- #601: shared grouped alerts, including the faults comparison across adapters.
- #600: shared overview.
- #603: shared sensor assessment. #602: shared device summary.
- #607: trimmed Android and installed iOS runs, tokens and size deltas.
- #608: MAC, group and location search in chat.
- #610: further catalogue tools (epic), one issue per group as each starts.
