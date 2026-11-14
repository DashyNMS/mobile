# Intelligence

How Intelligence on the phone reaches parity with desktop's, and the rules for
every network tool it runs. #236 is the only 2.0.0 Intelligence epic, and its
Order section is the build order: work starts at its first unchecked item.
Progress is tracked there, not here; this document holds what stays true
across the issues.

## Direction

Mobile 2.0.0 gives iPhone and iPad desktop's Intelligence, answered by Apple
Intelligence instead of ChatGPT. That means Apple's on-device model through the
Foundation Models framework, and on iOS 27 optionally Private Cloud Compute
(#575). Parity covers four things:

- **Tools.** The model gets desktop's tools, with the same names, arguments and
  descriptions. The text comes from DashyNMS/mcp's shared catalogue
  (DashyNMS/mcp#130) and is never written on the phone (#635). Every tool MCP
  has comes to the phone (#236, batch 5).
- **Results.** A lookup returns the facts desktop's tool returns, from the same
  shared typed operation, written as compact text. Any fact left out is listed
  under [Facts left out](#facts-left-out) with its reason (#636).
- **Instructions.** The model follows the rules in Core's `Instructions/*.md`
  that desktop's model follows: evidence, safety, alerts and faults, writing,
  markers and detail levels (#638).
- **Chat.** Desktop's chat features: saved chats, Spotlight, Ask Intelligence,
  export and starter questions.

In 2.1.0, ChatGPT (#577) and Android (#576) join as further models over the
same tools and pages. ChatGPT then gets every tool on every question and MCP's
JSON results (#225).

## Allowed differences

The phone differs from desktop only where the model forces it. Each difference
is listed here with its reason. Anything else is a gap and needs an issue.

| Difference | Reason |
| --- | --- |
| Fewer tools and lookups per question | The context is 4,096 tokens on iOS 26.4 and 8,192 on iOS 27.2. The catalogue's descriptions alone are about 23,700 characters, roughly 6,000 tokens. Each question gets the tools and lookups its context holds (#637). Private Cloud Compute's 32,000 tokens hold the whole catalogue. |
| Compact text results, at most 900 characters on the device, instead of MCP's JSON | The context again. The facts match, except those under [Facts left out](#facts-left-out). |
| Short instructions on the device | The context again. Each of Core's rules maps to a short form or a written exclusion (#638). Private Cloud Compute uses Core's text where it fits. |
| Results name devices in the person's device-name style, with Core's markers already written | Left to itself, the on-device model drops parts of names and writes "device 708" (#570). Desktop's model writes markers from the JSON itself. |
| No Full report detail level on the device | Too long for the on-device answer budget. |
| No web search | It's ChatGPT's hosted tool, and arrives with #577. |
| Graph images may not reach the model | It depends on whether Apple's models accept images (#649). The person still sees the graph. |
| Report this answer, and the Private Cloud Compute notice | Store rules for the phone (#574, #575). |

Where the phone is more accurate than MCP, it keeps its value and the fix goes
upstream. Device search, for example, reports Ignored and Unknown states that
MCP doesn't yet (DashyNMS/mcp#121).

## Parity

| Desktop | Phone now | Issue |
| --- | --- | --- |
| 24 network tools | 5: `network_overview`, `find_device`, `active_alerts`, `device_sensors`, and `device_health` (its device and uptime part) | #236, #634 |
| Tool names, arguments and descriptions from MCP | Done: the shared catalogue's text and typed arguments, devices by name, address or ID; paging and time windows left out | #635 |
| Results with times, notes and every field | Compact text without times, alert notes or several fields | #636 |
| Every tool on every question, 48 lookups in 16 rounds | Five tools, two lookups | #637 |
| Core's instructions, about 1,300 words | One 400-character string | #638 |
| Sensor readings judged by limits and defaults, not the person's thresholds | The same | #639, DashyNMS/mcp#132 |
| Device search without maintenance | The same | #620 |
| Brief, Balanced, Detailed and Full report | Brief, Balanced and Detailed | Allowed difference |
| Saved chats to rename, pin and delete, grouped by date | Done | #229 |
| Single-chat mode | Done | #230 |
| Copy, Retry, Edit question and Stop | Done | #227, #573 |
| Spotlight on devices, a group, a location or an object | Not yet | #231 |
| Ask Intelligence from devices, alerts, sensors, ports and more | Not yet | #232 |
| Export as Markdown or PDF | Not yet | #233 |
| Twelve starter questions, four at a time, and "Checking alerts" activity | Three fixed sets of three, and tool titles | #640 |
| Graph images in answers | Rendered, but no tool returns one | #649 |
| Web search | Not on Apple Intelligence | #577 |
| Dashy, desktop's companion | Undecided | #236 |

## How the phone runs desktop's tools

Typed operations are extracted **inside DashyNMS/mcp's Agent library** and
published in one package, `DashyNMS.Agent.Shared`, with no `ModelContextProtocol`
reference. Each new operation joins that package rather than shipping its own, so
mobile pins and verifies one package. The MCP server and mobile are both thin
adapters over them. Core keeps transport, LibreNMS models and alert grouping; a
new Core API is only for behaviour every Core consumer needs. This was decided
in #596 (closed 2026-11-12), which replaced #221's plan to host the whole Agent
library in the app.

Mobile never copies an MCP query, parses an MCP tool's JSON, or calls
`AddDashyNmsAgent`. That registration adds its own transports, failover and
LibreNMS, Graylog and Unimus clients, which would replace the session's. Agent's
`DeviceDirectory` and `FleetCache` also cache for 60 seconds with no server key,
so a shared instance could hand one network's data to another.

The same package will carry the tool catalogue (DashyNMS/mcp#130) and device
resolution by name, address or ID (DashyNMS/mcp#131), so the phone describes
and resolves tools exactly as MCP does.

DashyNMS/mcp stays a complete MCP server in its own right. Any MCP client can
run it as an executable or container image, without DashyNMS desktop or
mobile. An HTTP service and single-file binaries are planned
(DashyNMS/mcp#17, DashyNMS/mcp#36). The shared
package is a library MCP uses. It never makes MCP depend on the apps.

## Adding a tool

1. Upstream, in DashyNMS/mcp: the typed operation in `DashyNMS.Agent.Shared`,
   its catalogue entry and synthetic fixtures. MCP's own tool runs the
   operation and takes its text from the catalogue.
2. Pin the published package version and hash, and keep
   `scripts/verify-shared-package.sh` covering it.
3. Copy the fixtures byte for byte and add them to
   `scripts/check-shared-fixtures.sh`.
4. Write the adapter in `CompactNetworkLookups`: its endpoint allowlist, its
   compact text built from the typed result, and its evidence from the rows
   shown. Record its name in `ReviewedTools`.
5. Compare its text with MCP's JSON field by field, and list any fact left out
   under [Facts left out](#facts-left-out).
6. Update [Catalogue](#catalogue) and [Parity](#parity). Add a section under
   [Built tools](#built-tools) for anything that differs from the pattern.

A tool is finished when every item in
[When a tool is done](#when-a-tool-is-done) holds.

## Rules every shared tool keeps

- **MCP stays standalone.** MCP keeps its own service registration,
  environment-variable configuration, JSON results, paging and every tool. A
  change made for the phone leaves MCP's output unchanged, unless the change is
  wanted for every MCP client and tested in MCP. What only the phone needs stays
  in mobile: the device-name style, written markers, compact text, budgets and
  tool choice.
- **The host owns the clients.** Transport, certificate trust, failover,
  credentials, caches and timestamp settings come from the session. Directories
  and caches are created per connection generation. Integration tools (Graylog,
  Unimus) are offered only when the phone has that integration set up, use the
  phone's own settings, and never read server environment variables.
- **Text from the catalogue.** A tool's name, description and parameters come
  from the shared catalogue, unchanged. A parameter the phone doesn't support
  yet is left out of the schema and listed under
  [Facts left out](#facts-left-out). `CompactNetworkLookups.Offered` lists the
  parameters each tool offers, and a test holds every offered tool's description
  and parameters to the pinned package's catalogue.
- **Read-only allowlist.** The wrapper allows only the endpoints the tool's
  review names, and unknown tools fail before transport. A tool is offered to
  the model only if it's in Core's `IntelligenceToolAllowlist`, the static
  registry and the question's tool choice. No reflection-based MCP discovery
  ships on mobile.
- **Safe failures.** Model text, evidence and diagnostics never contain server
  messages, raw bodies, URLs, exception text or stack traces. Upstream returns
  fixed categories, and mobile maps them to fixed lines. Partial component
  failures follow the same rule.
- **Untrusted data.** Device, log and configuration text stays untrusted model
  input. Upstream redaction and mobile's `Clean` are both kept; neither replaces
  the other.
- **The same facts, compactly.** At most 900 characters per reply on the
  device, built from the typed result rather than by slicing upstream JSON. Every
  fact MCP returns is shown or listed under [Facts left out](#facts-left-out).
  Rows are whole, with names and intact ids. Omissions and failures are stated
  truthfully. Evidence comes only from rows shown, and an aggregate count never
  becomes an entity link. Alert groups use a device/rule key distinct from fault
  ids, so a fault and a legacy alert with the same number never share ownership.
- **Budgets.** Definitions reserve 80 tokens each, with 160 reserved for the
  four-argument device search. This is a conservative allowance, not a device
  measurement. The conversation budget in `IntelligenceConversationViewModel`
  reserves 630 fixed, 720 for two results and 400 for output, within 80% of the
  reported context. Measured figures are in
  [DEVELOPING.md](DEVELOPING.md#what-the-on-device-model-can-do-570). #637
  replaces these fixed figures with measured per-tool costs and a budget read
  from `contextSize`. All 24 definitions never go into the on-device context at
  once.
- **Notices before data leaves the phone.** On the device nothing is sent.
  Private Cloud Compute (#575) and ChatGPT (#577) send questions and lookup
  results, so each has its own notice, which names log and config text when
  those tools are offered.

## When a tool is done

A tool counts as done when its PR, and the upstream PR it pins, show all of
these:

1. The upstream typed operation is in an Agent package with no MCP reference,
   published from `main` with a provenance attestation, and MCP's own tool uses
   it.
2. Mobile pins the exact version, and `verify-shared-package.sh` covers the
   package.
3. Upstream's synthetic fixtures are copied byte for byte, the check script
   covers the copy, and the cases include the issue's named edge cases.
4. Mobile's tool and the package give the same results as MCP over loopback
   HTTP, including failures.
5. The tool's name, description and parameters match the catalogue (from #635),
   and a test compares its text with MCP's JSON fields (from #636).
6. Tests cover host-owned transport, a connection switch mid-request,
   cancellation, the deadline, refusals, the endpoint allowlist and the absence
   of server text.
7. MCP's own tool output is unchanged, or changed on purpose with MCP's own
   tests, and MCP's container image and release builds still pass.
8. Android Release and iOS Release CI pass with no new trim or AOT warnings.
9. Mobile's old query, ordering and formatting for that tool are deleted, and
   this document's tables are updated.

Device runs, token measurements and size deltas are recorded on devices and
never inferred from CI.

## Catalogue

DashyNMS/mcp's Agent has 24 tools, all in Core's allowlist and marked read-only.
To refresh, count the `McpServerTool(Name = ...)` attributes under
`src/DashyNMS.Agent/Tools` and compare them with `IntelligenceToolAllowlist.Names`.

| Tool | Capability | Phone | Issue |
| --- | --- | --- | --- |
| `find_device` | Ranked name/IP search, MAC via ARP, status and scope filters, paging | Shared, with MAC and scope filters | #620, #635, #636 |
| `network_overview` | Device counts, grouped alert and fault totals, ports, system version, partial failures | Shared | |
| `active_alerts` | Current alerts grouped by device and rule, separate fault totals, filters, a time window, paging | Shared, open alerts by device only | #636, #645 |
| `device_health` | State, uptime, maintenance, alerts, CPU/memory/storage, problem ports | Device and uptime only | #634 |
| `device_sensors` | Threshold-assessed readings, problems first | Shared, by device only | #636, #639 |
| `list_groups_and_locations` | Group and location catalogue | Not yet | #642 |
| `alert_details`, `alert_rules` | Fault and rule detail, history, rule search | Not yet | #641 |
| `port_status`, `device_neighbours`, `trace_endpoint` | One port's state, links, tracing through stored ARP/FDB | Not yet | #643 |
| `top_interfaces`, `top_errors`, `top_devices`, `health_overview` | Ranked metrics and scoped sensor assessment | Not yet | #644 |
| `device_events`, `network_events`, `what_changed` | Event and change history | Not yet | #645 |
| `search_logs` | Graylog messages | Not yet; Graylog is already on the phone | #646 |
| `config_backup`, `config_diff`, `recent_config_changes` | Unimus configuration | Not yet; Unimus is already on the phone | #647 |
| `device_details` | Inventory, VLANs, FDB, ARP, PoE, wireless, routing, addresses | Not yet | #648 |
| `graph` | Graph images | Not yet | #649 |

No write, acknowledgement, rediscovery or remote MCP service is proposed.

## Facts left out

What each shared tool's MCP result has that the phone's text doesn't, and why.
"#636" marks a fact still to add.

| Tool | Left out | Why |
| --- | --- | --- |
| `network_overview` | Nothing | |
| `find_device` | Location; the hostname when the display name differs; `verbose`'s sysName, OS, hardware and last poll | #636 |
| `find_device` | `offset`, `limit` and `page.next_offset` | The phone fetches 20 rows and counts the rest in the heading. A further page wouldn't fit the reply. |
| `active_alerts` | When each alert opened; its note; each fault's state and start time; fault names beyond three; `group_id` | #636. `group_id` stays out: it isn't an object the person can open. |
| `active_alerts` | `hours`, `since` and `until` | #645 brings the window |
| `active_alerts` | `offset` and `limit`, `page` | The phone asks for 20 groups and counts the rest in the heading. A further page wouldn't fit the reply. |
| `device_health` | Maintenance, open alerts, CPU, memory, storage and problem ports | #634 |
| `device_sensors` | Warning limits, and limits on readings that aren't problems | #636 |

## Built tools

What each shared tool on the phone does beyond the pattern above.

### How find_device is built

`find_device` is the first migration and the pattern for the rest.

**Package.** `DashyNMS.Agent.Shared` `2.0.0-ci.43536733548.1`, from DashyNMS/mcp's
`shared-package.yml` run 43536733548 at commit
`ce4ec6219c1212e2584524ed7ac3643720ab5635` (DashyNMS/mcp#134: the tool catalogue and
device resolution, DashyNMS/mcp#130 and #131), SHA-256
`59f948a9daecbb799b1ec37fb69aad5d2f23e13a7a2ca832c2261fdbbd96bef5`. It was
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
list, without a locations endpoint. A device argument resolves over the same
profile's `IDevicesApi.ListAsync` (see [Device arguments](#device-arguments-635)).
`device_health` permits `IDevicesApi.GetAsync` by a positive numeric id, never by
hostname. For
`active_alerts`, `IAlertsApi.ListAsync(AlertQuery.Open)` is allowed. `device_sensors` has a
profile of its own: `IDevicesApi.ListAsync`, `ISensorsApi.ListAsync` and the sensor health
routes (see below), with no alerts, ARP or groups. Every other
endpoint, alert query, alert action and session control throws before transport.
Overview's and sensors' separate clients (see
[How network_overview is built](#how-network_overview-is-built-600)) refuse the single-device read. Each model attempt
owns an `IntelligenceLookupSet` and disposes it however the attempt ends, which
closes the transport. A switch or sign-out does the same straight away: waits
are cancelled, later calls are refused, and late replies and their evidence are
discarded.

**Cancellation and failures.** Caller cancellation propagates. Mobile's linked
15-second deadline and `WaitAsync` bound stay around the call. Failures arrive
as fixed upstream categories with no server text; mobile maps `Refused` to its
refusal line and everything else to its failure line. An empty result is not a
failure. Nothing is logged.

**Profile (#608, #635).** `find_device` offers the catalogue's `query` (name, IP
or MAC), `status` (up, down or disabled), `group` and `location`. At least one
must be supplied. Filters can be combined or used without text. Arguments are
checked as in [Tool definitions](#tool-definitions-635). The shared operation resolves exact names first,
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

### How active_alerts is built

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

**Profile.** The catalogue's `severity`, `device`, `group` and `location`. Time
windows and paging stay unadvertised until #645.

**Fixtures.** `tests/.../Fixtures/OpenAlerts` is a byte-for-byte copy of
DashyNMS/mcp's: a 26.10 server and an older one, with a fault and a legacy alert
both numbered 12, eight faults on one rule with two acknowledged, a recovered fault
beside its open siblings, and failures on the alert routes.
`scripts/check-shared-fixtures.sh` covers it. Over loopback HTTP, mobile
`active_alerts` and `HostOpenAlerts` must give the same groups, representatives,
states, counts, totals and request logs as MCP, and fail alike. Cases mobile can't
ask (severity, location, paging) run through the package alone.

### How device_sensors is built

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
someone has changed Settings › Thresholds. Fixing that is an upstream change for both (DashyNMS/mcp#132, #639).
Mobile maps the result to:

- a heading with every count, unknown kept apart from OK:
  `Sensors on core-sw1 [[device:1]]: 9; 2 critical, 2 warning, 1 OK, 4 unknown.`;
- one row per reading, worst first: description, `[[sensor:ID]]`, class, value and unit or
  the state definition's label, and the state. A problem names the sensor's own critical
  limits (`critical (high critical limit 80)`). A missing reading reads `no reading`, never `0`.
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

**Profile.** The catalogue's `device` and `type`.

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

### How device_health is built

The device summary migration (#602), built on DashyNMS/mcp's `DeviceSummaries`
(DashyNMS/mcp#128), is the device and uptime part of `device_health`; #634 brings
the rest. Everything under find_device's host ownership, lifetime, cancellation
and CI applies unchanged; this section covers only what differs.

**Contract.** Until #635 this was `device_summary`, a name Core's
`IntelligenceToolAllowlist` doesn't have. It now carries `device_health`'s
catalogue name, description and `device` parameter, and its text says what it
doesn't cover yet (listed under [Facts left out](#facts-left-out)). MCP's
`device_health` builds that part on the same shared read and shaping, so the two
can't drift. `CompactNetworkLookups.ReviewedTools` holds every lookup name, and a
test holds each to the allowlist and the catalogue, so a new name can't reach the
model without a reviewed tool.

**Adapter.** Each lookup set creates one `HostDeviceSummary` beside the search and
open alerts, over the same `ConnectionBoundClient` and transport, and asks for one
device id. Upstream reads `devices/{id}` and returns the redacted summary with its
reported `State`, uptime (null when LibreNMS reports none or zero) and a
`DeviceLocation`: the id, the trimmed and redacted name, and the exact LibreNMS
name. `devices/{id}` can send the location as the whole JSON record; upstream
reads the name, and the id when `location_id` is missing, out of it. Mobile
maps the result to:

- a heading: the device in the person's naming style with `[[device:ID]]`, its
  reported state and IP;
- one row: OS, hardware, location with `[[location:ID]]`, and uptime in seconds or
  `unknown`;
- evidence: the device with its state at lookup time, and the location chip,
  whose label is the cleaned name and whose Devices filter is LibreNMS's exact
  name. The exact name never goes into model text.

**Failures.** No fallback, unlike `device_health`, which answers from its device
list when the read fails: a device the server doesn't return, or returns under
another id, is "Device not found."; `Refused` maps to the refusal line, and
everything else to the failure line.

**Profile.** The catalogue's `device`, resolved as in
[Device arguments](#device-arguments-635).

**Fixtures.** `tests/.../Fixtures/DeviceSummary` is a byte-for-byte copy of
DashyNMS/mcp's. It covers a location sent as a record, a display name repeating the
hostname, zero uptime, a missing flag (Unknown), ignored and disabled devices, a
secret in the hardware text, a padded location name, a location id only in the
record, a listed device `devices/{id}` doesn't return, and transport failures on
`devices/1`. `scripts/check-shared-fixtures.sh` covers it. Over loopback HTTP,
mobile `device_health` and `HostDeviceSummary` must give the same values and
failures, with the same `devices/{id}` reads after mobile's device-list read, and upstream holds `device_health`'s
device and uptime to the same fixture.

### How network_overview is built (#600)

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

### Tool definitions (#635)

Each lookup's name, description and parameters are the shared catalogue's
(`ToolCatalogue` in `DashyNMS.Agent.Shared`, DashyNMS/mcp#130), word for word. The
phone writes none of this text. `CompactNetworkLookups.Offered` names the
catalogue parameters each tool offers, and the rest are listed under
[Facts left out](#facts-left-out) with the reason.

The Swift bridge builds each tool's `GenerationSchema` at run time from those
parameters with `DynamicGenerationSchema`: string, integer, boolean or string
list, optional unless the catalogue requires it, each with its own description,
documented values as `anyOf` choices and ranges as guides. The model's arguments
come back as one JSON object (`GeneratedContent.jsonString`).
`IntelligenceToolArguments` checks them against the same parameters before any
request. Unknown, duplicate and mistyped arguments are refused, and so are values
outside the documented choices or range. A refusal names the parameter, never the
model's own text. Plain text that isn't a JSON object reads as the tool's main
argument (`query` or `device`), for tests and older callers.

A description may name a tool the question doesn't offer; #637's tool choice
decides what then happens, and the description is never edited.

### Device arguments (#635)

`device_health`, `device_sensors` and `active_alerts` take the catalogue's `device`:
a hostname, display name, IP address or ID. Each lookup set owns one
`HostDeviceResolver` (DashyNMS/mcp#131) over search's connection-bound client,
which reads the device list and resolves in MCP's tiers: ID, exact name or
address, short hostname, part of a name. One match runs the tool for that id.
Several in a tier return the candidates in the person's naming style with their
markers and IPs, and the model asks which; nothing is guessed. No match gives MCP's
own sentence. A refused or failed device list is the refusal or failure line,
never "no match". The resolver's list is cached for a minute per lookup set, so a
found device's state comes from the tool's own read, never the resolver.

`Fixtures/DeviceResolution` is a byte-for-byte copy of DashyNMS/mcp's, and
`scripts/check-shared-fixtures.sh` covers it.
