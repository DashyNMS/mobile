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
| Graph image input depends on the native model route and context | iOS 27 models advertising vision receive typed image tool output within a 1,536-token image reserve. iOS 26, failed decoding and images over the reserve use the same metadata rows with a truthful notice. The person and saved chat retain the graph. |
| Report this answer, and the Private Cloud Compute notice | Store rules for the phone (#574, #575). |

Where the phone is more accurate than MCP, it keeps its value and the fix goes
upstream. Device search, for example, reports Ignored and Unknown states that
MCP doesn't yet (DashyNMS/mcp#121).

## Parity

| Desktop | Phone now | Issue |
| --- | --- | --- |
| 24 network tools | Up to 24: `network_overview`, `find_device`, `active_alerts`, `device_sensors`, `device_health`, `list_groups_and_locations`, `alert_details`, `alert_rules`, `port_status`, `device_neighbours`, `trace_endpoint`, `device_details`, `top_interfaces`, `top_errors`, `top_devices`, `health_overview`, `device_events`, `network_events`, `what_changed`, `graph`, and configured `search_logs`, `config_backup`, `config_diff` and `recent_config_changes` | #236 |
| Tool names, arguments and descriptions from MCP | Done: the shared catalogue's text and typed arguments, devices by name, address or ID; paging and time windows left out | #635 |
| Results with times, notes and every field | Compact text without times, alert notes or several fields | #636 |
| Every tool on every question, 48 lookups in 16 rounds | The tools each question's words, Spotlight and earlier questions point to, and their named tools, while they fit; two lookups at 4,096 tokens, more as the context grows, up to 48 | Allowed difference (#637) |
| Core's instructions, about 1,300 words | Short forms of Core's base, detail and Spotlight blocks, each rule mapped or excluded with its reason; Core's own text on Private Cloud Compute | Allowed difference (#638) |
| Sensor readings judged by the host's saved thresholds and sensor-limit override | The same Core evaluation as the Health page, captured per question | #639 |
| Device search with maintenance on returned rows | Shared operation and evidence | #620 |
| Brief, Balanced, Detailed and Full report | Brief, Balanced and Detailed | Allowed difference |
| Saved chats to rename, pin and delete, grouped by date | Done | #229 |
| Single-chat mode | Done | #230 |
| Copy, Retry, Edit question and Stop | Done | #227, #573 |
| Spotlight on devices, a group, a location or an object | Saved typed identities, current-connection revalidation and locked scope while answering | #231 |
| Ask Intelligence from devices, alerts, sensors, ports and more | Shared object mapping, row actions and guarded new chats | #232 |
| Export as Markdown or PDF | Visible-content sharing, bundled Markdown graphs and native PDF graph appendix | #233 |
| Twelve starter questions, four at a time, and "Checking alerts" activity | Desktop rotation, eligibility and activity wording | #640 |
| Graph images in answers | Shared graph lookup attaches images to the answer and saved chat | #649 |
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
- **Budgets.** Each question's tools and lookup limit are chosen from the model's
  reported `contextSize` and measured token costs (#637); see
  [Tool choice and budgets](#tool-choice-and-budgets-637). Everything stays within
  80% of the context. All 24 definitions never go into the on-device context at
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
| `find_device` | Ranked name/IP search, MAC via ARP, status and scope filters, paging | Shared, with MAC, scope filters and maintenance | #620, #635, #636 |
| `network_overview` | Device counts, grouped alert and fault totals, ports, system version, partial failures | Shared | |
| `active_alerts` | Current alerts grouped by device and rule, separate fault totals, filters, a time window, paging | Shared, current groups or historical occurrences with paging | #636, #645 |
| `device_health` | State, uptime, maintenance, alerts, CPU/memory/storage, problem ports | Shared, complete operation with bounded rows | #634 |
| `device_sensors` | Threshold-assessed readings, problems first | Shared, by device only | #636, #639 |
| `list_groups_and_locations` | Group and location catalogue | Shared, with exact filter names in evidence | #642 |
| `alert_details`, `alert_rules` | Fault and rule detail, history, rule search | Shared, bounded detail rows and source-qualified alert evidence | #641 |
| `port_status`, `device_neighbours`, `trace_endpoint` | One port's state, links, tracing through stored ARP/FDB | Shared, bounded rows and device evidence | #643 |
| `top_interfaces`, `top_errors`, `top_devices`, `health_overview` | Ranked metrics and scoped sensor assessment | Shared, with captured host thresholds | #644 |
| `device_events`, `network_events`, `what_changed` | Event and change history | Shared timelines and optional-service change context | #645 |
| `search_logs` | Graylog messages | Shared, offered after a configured readiness probe | #646 |
| `config_backup`, `config_diff`, `recent_config_changes` | Unimus configuration | Shared, with captured config-text policy | #647 |
| `device_details` | Inventory, VLANs, FDB, ARP, PoE, wireless, routing, addresses | Shared section selection and paging | #648 |
| `graph` | Graph images | Shared lookup, restricted image transport and typed answer attachments | #649 |

No write, acknowledgement, rediscovery or remote MCP service is proposed.

## Facts left out

What each shared tool's MCP result has that the phone's text doesn't, and why.
Every other field of MCP's JSON appears in the phone's text: `IntelligenceFactsTests`
runs each tool over the shared fixtures, reads MCP's recorded JSON for the same
fixture (`Fixtures/Intelligence/McpJson`) and fails on a field that is neither
shown nor in this table (#636). Field paths are under MCP's `data`, arrays left
out of the path. Where the phone is more accurate than MCP it keeps its own value:
a device's state is Core's reported state, Ignored and Unknown included, rather
than MCP's status word (DashyNMS/mcp#121).

Times are written in the person's local time with the date
(`20 Oct 2026 08:30`) and lengths in days and hours (`24 days 17 hours`), so the
model copies them rather than converting UTC or seconds.

| Tool | Left out | Why |
| --- | --- | --- |
| every tool | `status`, `summary` | MCP's envelope. The phone writes its own heading and fixed failure lines. |
| `find_device` | `page.offset`, `page.limit`, `page.returned` | The phone fetches 20 rows and counts the rest from `page.total` in the heading. A further page wouldn't fit the reply. |
| `active_alerts` | `devices.alerts.group_id` | It isn't an object the person can open. |
| `active_alerts` | `devices.alerts.rule_id`, `devices.alerts.source` | They travel in the chip's typed object, so a chip opens the right rule, fault or legacy alert, without spending the model's context. |
| `active_alerts` | `devices.alerts.faults.fault_id`, `devices.alerts.faults.entity_type`, `devices.alerts.faults.entity_id` | Faults are named with their states. Only a group's representative is offered as an id, so the model is never given an id it can't link. |
| `active_alerts` | `devices.hostname`, `devices.display` | Rows name the device in the person's naming style with its marker, which opens the device. `find_device` gives its other names. |
| `active_alerts` | `page.limit` | The recorded MCP fixture asks for 10 groups; the phone defaults to 20. Its requested limit is shown explicitly. |
| `network_overview` | `alert_groups.key.rule_id`, `alert_groups.rule_id`, `alert_groups.representative.source` | They travel in the chip's typed object. |
| `network_overview` | `alert_groups.faults.key.id`, `alert_groups.faults.key.source`, `alert_groups.faults.entity_type`, `alert_groups.faults.entity_id` | Faults are named with their states; only the representative is offered as an id. |
| `network_overview` | `alert_groups.faults.since`, `alert_groups.last_changed` | Budget. Each group says how long it has been open; `active_alerts` with `verbose` gives when each alert and fault started. |
| `network_overview` | `alert_groups.hostname`, `alert_groups.device.hostname`, `alert_groups.device.ip`, `alert_groups.device.location` | Rows name the device in the person's naming style with its marker and state. `find_device` gives the rest. |
| `device_health` | `ports_total`, `ports_down_while_enabled` when their source failed | MCP carries zero fallback values alongside unavailable metadata. The phone reports the section unavailable instead of presenting a known zero. Successful counts are shown and tested independently. |
| `device_health` | `unavailable.reason`, `unavailable.message` | Fixed unavailable source names lead the reply. Transport wording is excluded; the model cannot treat it as device data. |
| `device_sensors` | `readings.rank` | MCP's sort key. Rows are already problems first. |
| `device_sensors` | `device.hostname`, `device.ip`, `device.location`, `device.status` | Budget. The heading names the device in the person's naming style with its marker, whose chip carries its state; `device_health` gives the rest. |

An alert's `since` is shown as how long it has been open, and with `verbose` also
as the time it opened, so the test accepts either.

### Parameters left out

The catalogue parameters the phone doesn't offer the model yet. A test fails on a
catalogue parameter of an offered tool that is neither offered
(`CompactNetworkLookups.Offered`) nor listed here.

| Tool | Parameters | Why |
| --- | --- | --- |
| `find_device` | `offset`, `limit` | The phone fetches 20 rows and counts the rest in the heading. A further page wouldn't fit the reply. |

## Batch 5 package provenance

The reviewed network operations are published as `DashyNMS.Agent.Shared`
`2.0.0-ci.43670427340.1` by DashyNMS/mcp's Shared package workflow, run
43670427340, from protected `main` commit
`3e9367b415e99fe3c50f2d21c8834b54bb09539a`, after PRs #135 and #148. The package SHA-256 is
`8bc57bca89670f7006d8288439e93cd2809d232703def9ef1e59a430843292f7`.
Publication verified the feed's bytes, and the provenance attestation verifies
against that source commit and workflow. Mobile's verification script pins the
same version, hash and commit. The Core pin remains `2.0.0-ci.64`, covered by
upstream's package compatibility job. Mobile Release CI separately checks trim
and AOT compatibility.

## Built tools

What each shared tool on the phone does beyond the pattern above.

### How find_device is built

`find_device` is the first migration and the pattern for the rest.

**Package.** `DashyNMS.Agent.Shared` `2.0.0-ci.43670427340.1`, from DashyNMS/mcp's
`shared-package.yml` run 43670427340 at commit
`3e9367b415e99fe3c50f2d21c8834b54bb09539a` (DashyNMS/mcp#148), SHA-256
`8bc57bca89670f7006d8288439e93cd2809d232703def9ef1e59a430843292f7`. It was
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

**Maintenance.** The shared search checks each returned row sequentially, at most
the phone's 20-row page; it does not check the entire match set. The typed shared
state supplies the row and evidence state. Active maintenance takes precedence;
a failed check keeps the device-list state, labels maintenance unknown and never
claims it is off. The evidence source distinguishes a completed check from an
unavailable one. Maintenance is read again on the next lookup even while the
device list remains cached. Cancellation, the deadline and retirement cover the
extra reads. The pinned request-count fixtures and HTTP active/inactive/refused/
missing cases check both hosts, with separate page-bound and late-result tests.

**Allowlist and lifetime.** Device search permits `IDevicesApi.ListAsync`,
`IArpApi.FindByMacAsync`, `IDeviceGroupsApi.ListAsync` and
`IDeviceGroupsApi.GetMemberDeviceIdsAsync` and `IDevicesApi.IsUnderMaintenanceAsync`
for positive IDs on the returned page. Location filtering reads the device
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
assessment uses the same Core evaluator as the Health page. Each model attempt
captures the person's saved dBm, signal, temperature and fan thresholds and the
sensor-limit override in `AgentOptions`. A later question opens a fresh set and
reads changed settings without restarting the chat. Sensor definitions still
supply state-sensor severity. Sensor-reported limits remain visible; when saved
thresholds override them, the text labels those limits as overridden.
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

### How graph is built

`HostGraphLookup` owns aliases, device/port/sensor selection, listings, spans,
image decoding and Core's image limit. Mobile offers every catalogue argument.
A dedicated profile allows device, sensor, port and graph-listing reads plus
sensor fallback. Its raw transport permits only GET image envelopes for device,
port and sensor paths, the fixed 800×250 query Shared builds, and the SVG
permission probe. Port names are canonical escaped path components; traversal,
extra query parameters, bodies, writes and raw text reads are refused.

Incomplete listings lead the reply, including a successful image with partial
inventory and an unknown negative result. Metadata retains type, local custom
window/preset, selected port/sensor and alternative graph types as whole rows.
Only shown device rows supply evidence. Pixels remain unredacted, as in MCP.

Lookup images reach the answer through a typed callback, independently of the
native model's text tool bridge. Core's type/size limits and the eight-image
answer cap apply. Model text completion preserves lookup attachments. Context
retry discards the abandoned attempt's images, and the existing attempt and
connection guards reject late callbacks. The saved-chat path already serialises
these validated attachments. The ninth graph is refused before transport, so a
lookup cannot promise an image hidden by the answer cap.

On iOS 27, the bridge checks the active model's `.vision` capability and returns
Foundation Models Prompt tool output containing the correlated image. C# passes
NSData alongside text, never base64 in the text or an ambient shared image buffer.
The conversation reserves 1,536 extra tokens per result when graph is offered
on an image-capable route. Native counts the image as an Attachment before
submitting it. Invalid decoding, encoded data over Core's 2 MiB limit, decoded
dimensions over 2048×1024, token-count failure or cost above that reserve uses
the metadata-only text. That fallback keeps exactly the same rows and evidence,
and stays within 900 characters. Native reports submission by an attempt-local
image ID. The conversation maps each ID to the displayed attachment, so mixed
success identifies the exact Graph image number that remained metadata-only.
The notice is part of the saved transcript. iOS 26 keeps the fallback.

Apple documents [multimodal prompts](https://developer.apple.com/documentation/foundationmodels/analyzing-images-with-multimodal-prompting),
[PromptRepresentable tool output](https://developer.apple.com/documentation/foundationmodels/tool)
and [vision capability checks](https://developer.apple.com/documentation/foundationmodels/languagemodelcapabilities).
Its image guide also covers Private Cloud Compute; no PCC route is enabled here,
and its consent and configuration-text notice remain #575.

Copied Graphs fixtures include fixed MCP projections and PNG/SVG envelopes.
HTTP cases compare device/CPU/port/sensor metadata, pixels and request sets,
including partial listings, denial, oversized images, custom spans, legend,
cancellation, deadline and a connection switch. Route tests cover canonical
names and refused paths. An end-to-end conversation test verifies that the
text-only model gets no base64 while the answer and reopened saved-chat snapshot
keep the graph and metadata-only notice. Capability-enabled cases exercise typed
pixel delivery, rejected-image notices and the 8,192-token budget; both tool-call
paths retain the same lookup limit. Mixed-delivery regressions reject either of
two images and check the numbered notice after saved-chat restoration. Field accounting compares device, CPU,
port and sensor Data with the fixed MCP projections, plus current typed results
for custom windows, partial/negative listings and the type-list cap. Types and
selected entities are checked in their own rows. No Data field is deliberately
excluded. Native compilation and Release checks run in GitHub Actions. Real
model/image-token measurements and iPhone acceptance remain unverified while
this PR is draft.

### How Unimus config tools are built

`config_backup`, `config_diff` and `recent_config_changes` use
`HostConfigLookups` with separate captured Unimus clients and all catalogue
arguments. They are offered only when Unimus is configured. The saved
`IntelligenceConfigText` choice is captured with each attempt: Off refuses before
any LibreNMS or Unimus read, Summary returns metadata/counts, and Full permits
shared redacted text. A privacy or integration change discards in-flight results
and evidence. No backup trigger, reconfiguration or connection test is exposed.

The dedicated LibreNMS profile allows only the device list and group catalogue/
membership needed by shared config matching and scopes. Shared owns matching,
backup selection, revision lookup, paging, diff rendering, windows and fleet
filtering. Mobile formats local times, config lines, backup IDs and cursors,
whole-row omissions and verified device evidence. Unreadable-device counts lead
recent-change replies, so failures cannot look like a complete empty result.
Backup warnings remain from Shared, with their dates localised. Server error
bodies never enter replies.

The copied upstream ConfigLookups fixture uses synthetic bearer authentication
and real Core HTTP clients. CI compares request sets and known backup/diff/change
facts with the shared host; further cases cover Full/Summary/Off, line paging,
redaction, unavailable tools, scoped partial failures, cancellation, privacy
changes, token retirement and the read-only profile. Field accounting compares
all populated MCP Data fields for backup Full/Summary and a one-line page,
diff Full/Summary and its source cap, plus recent changes, first backups and
unreadable devices. Each revision is checked by backup ID; config text is checked
line by line, and a missing whole row needs an omission count. The source cap's
omitted-line count is stated separately from the phone's budget omissions.
Unreadable-source messages use the host's fixed safe category. No Data field is
deliberately excluded. iPhone acceptance remains pending while this PR is draft.

Settings → Intelligence has an automatic-save Configuration text picker when
Unimus is configured. It writes Core's existing Summary/Full/Off key and restores
the previous value if persistence fails. Changing it stops an active model attempt,
including one that has already received lookup text, while keeping earlier chat
text visible. The page prepares existing secure setup before deciding visibility.
CI covers persistence/reload, rollback, visibility and interruption. The caption
refers to the current on-device model. The separate Private Cloud Compute work
in #575 must explain that Full config text goes to Apple before it is enabled.

### How search_logs is built

The tool is offered only after Graylog setup and a successful per-attempt streams
read. The probe uses a separate captured client, the saved credentials/TLS
choices, caller cancellation and a 15-second deadline. A failed or refused probe
leaves the tool out; it sends no question or device data. WhatChanged remains
available for its independent partial-source result. The operation itself uses
`HostLogSearch` with captured options and clients, never environment settings.

All catalogue arguments are offered. A dedicated LibreNMS profile allows only
the device list and positive-device interface addresses for configured address
matching. Other reads and actions are refused. Failed interface coverage stops
the search rather than silently narrowing it. Shared logic owns query/stream/
level resolution, relative count buckets, past windows, sampling and redaction.

Replies retain criteria, exact count/window, top-value sample provenance,
newest/oldest caveats, cleaned lines and count buckets as whole rows within 900
characters. Sample sources never create device links; only a shared device filter
can supply one. Upstream-unreturned messages and omitted rows are counted, using
64-bit counts. Integration state is checked before and after the read.

Copied Graylog fixtures run through real Core clients with synthetic Basic auth.
HTTP tests compare current/past counts, samples and query sets against the shared
host, accounting separately for the readiness probe. Further checks cover absent/
refused setup, denied searches, marker-shaped log text, redaction, missing address
coverage, caller cancellation, the profile and large omission counts. Field
accounting compares every populated MCP Data leaf for current and past windows,
oldest-page fallback, empty results, a verified device filter and populated log
attributes. Log lines, top-value records and buckets are checked in their own
rows; missing whole rows need an omission count. No Data field is deliberately
excluded. iPhone acceptance remains pending while this PR is draft.

### Captured integration clients

Before opening a model attempt, the conversation awaits the phone's existing
Graylog/Unimus setup and secure-storage preparation, with caller cancellation.
Setup publishes immutable connection snapshots only after configuration. Each
attempt creates separate Core clients from those snapshots, including the saved
TLS and credential choices; operations cannot reconfigure them, test a connection
or trigger a backup. These clients use no request logger and close with the
attempt. Setup generations reject further reads after an integration is changed
or forgotten. Every saved-settings change also retires the attempt, including
changes that later restore the original value. The main LibreNMS generation
still governs the whole lookup set.

The captured options include server timestamps, Graylog query field/address
matching, the saved Unimus discovery domain and the phone's config-text choice
(Summary by default). Core applies its usual domain candidates for backups,
diffs, scoped changes and WhatChanged. The history
factory now receives these clients; its LibreNMS-only tools do not call them.
WhatChanged uses them; the remaining integration tool adapters follow. CI tests use two synthetic
HTTP services to check addresses, credentials, capture against settings changes,
retirement and refused configuration/write methods.

### How what_changed is built

The adapter calls the shared `ReadChangesAsync` with captured read-only optional
clients. The heading states whether config changed, is unknown without a
baseline, is disabled by the saved choice, is unavailable or has no matching
device. Graylog is independently unconfigured, counted, unavailable or skipped
for a past window. Failed interface-address coverage prevents a narrowed log
search. Config Summary shows counts without text; Full shows shared-redacted
diff rows; Off makes no Unimus request. Internal MCP environment-variable wording
is replaced with the phone's saved preference wording.

Source failures and window coverage precede detail rows. Config version and
backup times, change counts, timeline events, optional log samples and diff rows
then share the whole-row budget. Omitted rows and upstream caps are counted;
only a shown verified device gets a chip. The adapter checks integration state
before and after the shared read. Integration changes, forgotten credentials or
changed config-text/query preferences reject late results and evidence.

HTTP tests cover configured services, text modes and redaction, a missing config
baseline, disabled config reads, missing address coverage and a privacy change
during an in-flight read. Unconfigured services retain the LibreNMS timeline.
The history deadline/retirement test also covers this adapter. Current MCP
field accounting covers its unconfigured-service projection and shared timeline;
configured-service facts have independent HTTP assertions. iPhone acceptance
remains pending while this PR is draft.

### How event and alert history is built

Device/fleet events and historical active_alerts use `HostEventLookups` with a
dedicated captured LibreNMS client. It allows device and group lists/membership,
rule names, open alerts, positive-device outages, and device/global event and
alert logs at the shared 200/1,000/2,000 depths. Other reads, maintenance, actions
and writes are refused. These three operations do not use Graylog or Unimus;
WhatChanged uses a separate profile that additionally permits per-device IPs
for configured Graylog address coverage.

All catalogue history parameters are offered. active_alerts uses historical
occurrences when hours/since/until is supplied and current groups otherwise;
both modes now accept offset/limit. Historical rows have no live alert ID, so
they link only the verified device. Rows retain raised/recovered times, unknown
recovery/open state, severities, fault counts, event source/kind/text/user and
outage duration. Window bounds, completeness/unknown coverage, source notices
and page metadata lead the reply. Omitted whole rows and unfetched page rows
are counted, and only shown rows create evidence.

Copied event-history fixtures compare independent outage, port-event and raised/
recovered facts with the shared host and its request set. Failure tests preserve
unknown coverage and severity/open state; a busy log reaches the shared cap
without becoming complete. Deadline, caller cancellation and retirement are
covered during pending history reads. Field accounting compares populated MCP
Data leaves on fixed explicit windows, one-row event/alert pages and the shared
WhatChanged timeline. Time/source pairs identify timeline rows without inventing
entity IDs; omitted rows pass only when counted. iPhone acceptance remains pending.

#### History facts left out

| Tool | Left out | Why |
| --- | --- | --- |
| `device_events` | `device.sys_name`, `device.os`, `device.hardware`, `device.last_polled` | Budget. History names the device with IP, location and state; device_health supplies inventory and last poll. |
| `what_changed` | `device.sys_name`, `device.os`, `device.hardware`, `device.last_polled` | The same budget choice for change context. |


### How fleet rankings and health_overview are built

`HostFleetRankings` owns traffic/error direction, ranking, scoped filters and
counts. Its dedicated client allows only the device list, fleet port-status
snapshot and group catalogue/membership. `HostHealthOverview` has a separate
profile for device/scope lists, sensors and the restricted sensor fallback
transport. Other reads, maintenance, single-device reads and writes are refused.

All catalogue parameters are offered. Rankings keep the shared order, candidate
and checked-port totals, directions, rates, problem counts and traffic double-
counting notice. Health overview captures saved thresholds and the override
choice per attempt using the same options as device_sensors. The shared operation
owns type/problem filtering, the bounded scope fallback, denied-device count and
paging. An unscoped denied sensor fleet read is refused; denied devices in a
permitted fallback lead the reply and are excluded from sensor counts.

Whole rows outside 900 characters and candidates outside the requested page are
counted as omitted. Only displayed rows supply device and sensor chips. Health
paging metadata precedes readings, and type counts are separate rows. HTTP tests
compare the pinned MCP fixture and independent rank/count facts with the shared
host, including request sets, groups, directions, limit, denied reads, scoped
fallback, captured thresholds and fresh choices on the next question. Deadline,
cancellation and retirement checks cover both profiles. IntelligenceFleetFactsTests
compares the populated fields in MCP's direct Data projections with one-row
traffic/error/problem pages and a health page, including type totals and next
offset. Device acceptance remains pending while this PR is draft.

#### Fleet facts left out

| Tool | Left out | Why |
| --- | --- | --- |
| `health_overview` | `readings.reading.rank` | Sorting metadata. The displayed readings already follow the shared order. |


### How device_details is built

`HostDeviceDetails` owns the default/selected sections, paging, sorting, sensor
fallbacks and partial failures. The adapter offers every catalogue parameter:
`device`, `sections`, `offset` and `limit`. Its compact default page is 20 rows.
Ask for one or a few sections when their detail matters; the shared default
section selection still applies if none are supplied.

A dedicated captured client allows device inventory, wireless and availability,
per-device ports, IPs, ARP and FDB, fleet VLANs and sensors, and per-device BGP,
OSPF, OSPFv3 and VRFs. Sensor health fallback uses the existing restricted sensor
transport. IDs must be positive; alert reads, memberships, single-device reads,
maintenance, unrelated APIs, raw sends and writes are refused before transport.

Source failures lead the reply; section totals distinguish known empty sections
from unreadable ones. Every section is formatted from its typed result. The
900-character budget retains whole rows, counts both unfetched page rows and
rows that do not fit, and offers only the displayed device chip. Pages retain
their offset, limit and next offset. The common deadline and retirement logic
covers the operation. Copied upstream HTTP fixtures test every section, request
sets, inventory/VLAN/PoE/routing/availability/address facts, failed VLAN reads,
page counts, invalid arguments and the client restrictions. Typed-boundary facts
also cover true, false and missing replaceability/BGP admin-down flags; explicit
false is rendered as no, while a missing value remains unknown. Device-detail field
accounting compares every populated MCP Data leaf in each section fixture and
an inventory page with the phone's text. Additional HTTP cases cover populated
wireless data and page metadata before budget omissions. iPhone acceptance
remains pending while this PR is draft.

### How port, neighbour and trace tools are built

`HostPortLookups` owns all port resolution, topology ordering, reverse-neighbour
observations and endpoint classification. Port and neighbour device arguments
use the common shared device resolver first. Their dedicated client permits the
device list, per-device ports and links, fleet links and VLANs, a MAC-specific ARP
read and per-device FDB reads. The extra transport permits only canonical IPv4
ARP collection paths and 12-digit lowercase MAC FDB collection paths, with the
right model type and collection key. Query strings, path escapes, raw sends,
maintenance, other resources and writes are refused before transport.

Port rows include state, speed, duplex, MTU, VLANs, traffic, errors and neighbour
observations. Neighbour rows retain reported/reverse direction and protocol.
Known positive device IDs supply chips; an unmonitored neighbour stays text.
The app has no port marker, so port IDs are plain facts beside a device chip.

Trace replies explicitly describe stored observations, preserve ambiguity,
source failures, candidate ports and bounded omissions, and never claim address
ownership. The shared caps on switches, MACs and observations apply before the
900-character whole-row budget. Device evidence comes only from displayed rows.
The host owns caches, and the common deadline and retirement checks cover all
three tools. HTTP tests compare the pinned upstream envelopes and independent
port, neighbour and edge facts with the package operation, including request
sets, reverse links, optional failures and invalid input. Current MCP projections
serialise these operations' Data directly; IntelligenceTopologyFactsTests compares
every populated leaf on the fixture with the displayed text or the explicit table
below. iPhone example-question acceptance remains pending while this PR is draft.

#### Topology facts left out

| Tool | Left out | Why |
| --- | --- | --- |
| `port_status` | `device.sys_name`, `device.os`, `device.hardware`, `device.last_polled` | Budget. The device row gives its names, IP, location and state; device_health supplies inventory and last poll. |


### How alert_details and alert_rules are built

Both adapters use `HostAlertRules` in a dedicated captured client. It permits
only the device, rule, template, group and location lists, system version, open
alerts, a positive alert/fault ID in all states, open siblings for a positive
rule ID, and alert history. History is bounded by the shared operation: 200 for
one device, or 200/1,000/2,000 globally. Other queries, event logs, single device
reads, memberships, maintenance and writes are refused before transport.

All rule catalogue arguments are offered unchanged, including sorts, paging,
severity, enabled/disabled choice and verbose notes. Rule and target IDs are
plain facts; the app has no rule route and cleaned target names are not exact
navigation filters. Alert details link the source-qualified alert or fault and
its device only when the shared result supplies that device. A follow-up carries
the source previously observed in this lookup set; a different source or an
ambiguous identity is refused. Unknown or failed history stays unknown.

Source notices precede detail rows. Rule conditions, procedures, notes, trigger
fields, history, counts, scope targets and compatibility advice use bounded
labels and whole rows within 900 characters. Omitted rows and pages are counted
and create no evidence. The shared summary retains incomplete-history notices.
The copied AlertRules fixtures are checked byte for byte in CI. HTTP tests check
shared detail facts, legacy/fault navigation, source mismatch, optional failures,
rule filters, sorts and paging, required failures and refused APIs. `IntelligenceAlertFactsTests` checks every non-null leaf of MCP's current
projection on the shared fixtures, including the disabled rule's removed-column
advice. This projection is constructed from the shared typed results and the
reviewed `RuleTools` shape, rather than a frozen recording or MCP runtime on the
phone. Independent HTTP assertions still cover the known trigger/history facts.
On-device follow-up acceptance remains pending while this PR is draft.

### How list_groups_and_locations is built

`HostGroupsAndLocations` owns the device and group catalogue reads, location
aggregation, ordering, redaction and partial failures. Its dedicated captured
client permits only those two lists. Membership, maintenance, other resources
and writes are refused before transport. Devices are required; an unavailable
group list is labelled in the heading while location results remain usable.

Rows show group names, types and descriptions and location device/down counts.
Only positive supplied IDs receive markers. Exact server filter names stay in
evidence, separate from cleaned model text; no location ID is invented from a
name or aggregate count. Whole rows outside the 900-character budget are counted
as omitted and create no evidence. The common cancellation, deadline and session
retirement checks apply to pending catalogue reads too.

HTTP tests compare the same pinned device/group envelopes with the shared host
operation and assert independent counts, failure handling, exact filter names,
redaction and evidence. Further tests cover bounded output, refused client APIs,
late results, cancellation and the deadline. Model selection is covered for a
group/location question; on-device acceptance remains pending.

### How device_health is built

The complete `HostDeviceHealth` operation replaces the narrow summary adapter
under #634. The tool keeps the catalogue's name, description and `device`
argument, resolved by the existing shared resolver. There is no phone-only
`device_summary` tool or descriptor.

Each attempt owns a health-profile `ConnectionBoundClient` and operation beside
the resolver. Its reviewed reads are the device list, one device by positive
numeric ID, that device's maintenance, processors, memory, storage and ports,
and the open-alert query with Core's source probe. Other reads, all writes and
session controls are refused before transport. Disposal and connection changes
retire it. The common caller cancellation, 15-second deadline, late-result and
evidence checks remain around the operation.

The reply puts unavailable source names before data rows, then the device's OS,
hardware, names, IP, location and last poll, uptime, maintenance, resource maxima,
open group/fault counts, port counts, fullest storage, grouped alerts and problem
ports. A failed source never becomes zero or an empty healthy section. Missing
or mismatched fresh devices use only the listed device and identify the fresh
read as unavailable. Location evidence keeps the exact filter name; alert evidence
retains its fault or legacy-alert source; port evidence includes its owning device.
Only rows shown create evidence. Whole rows outside 900 characters, including
upstream-capped problem ports, are counted as omitted.

`Fixtures/DeviceHealth` retains the upstream device-field bytes from MCP's
`DeviceSummary` fixture at the pinned Batch 5 commit. The old mobile summary
fixture path and adapter tests are removed. Independent HTTP tests add all health
sources, check the values against the package result and displayed text, and fail
each source separately. The shared device-field cases cover ignored and unknown
states, location records, redaction, missing fresh devices and uptime. Profile
tests reject other reads and writes; the existing lookup tests cover switching,
retirement, caller cancellation and the deadline. On-device model selection for
OS, summary and hardware questions remains a device acceptance check; CI alone
cannot establish it.

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

### Tool choice and budgets (#637)

Each question gets a fresh model session, so `IntelligenceToolChoice` chooses its
tools per question:

1. The tools its words point to (alerts, sensors, a device's health, finding
   devices, the overview), and the device tools when it names a device by an
   address or a name with a digit in it.
2. The device tools and `find_device` when the chat has a Spotlight (#231).
3. What the last two earlier questions pointed to, at half weight, so "and its
   sensors?" keeps its subject.
4. The overview and active alerts when nothing else matched.
5. Then the tools those tools' descriptions name, while they fit.

Costs are the model's own token counts (`tokenCount(for:)`, iOS 26.4 and later):
the instructions, the prompt, each tool's definition (its name, description and
parameter schema) and a full 900-character result. Definitions and the result
are counted once per context size and kept. Before iOS 26.4, which can't count,
the phone estimates two and a half characters a token, about as densely as
lookup text full of markers, ids and addresses packs, so it offers less rather
than overflowing. That figure is checked against device counts once measured.

Within 80% of the reported context, after the instructions, prompt and the
answer's room (150, 300 or 400 tokens by detail level), tools are added in order
while two full results still fit; the first is always offered. The lookup limit
is two at 4,096 tokens; above that it's the room left divided by one result, at
least two and at most desktop's 48. The prompt's own share is what's left after
the instructions, the answer, two results and the cheapest tool. Once the tools
are chosen and the instructions name the limit and the tools left out, the total
is counted again. If the final instructions cost more, tool selection repeats
with that cost before dropping history or rejecting the question. Fewer related
tools can leave room for two results even when the first selection did not.
Earlier turns give way if that still does not fit, as on a context limit. A
retry after a context limit starts its lookup count again.

Tools an offered description names that the question doesn't get, and tools not
on the phone yet, are listed in the instructions, so the answer says what it
couldn't check. When the lookups run out, the lookup says so and the turn says
some lookups failed, as desktop's "lookups used up" note does.

Device measurements of these costs on iOS 26.4 and iOS 27 are recorded here once
taken on devices; they are never inferred from CI.

### Instructions (#638)

`IntelligenceInstructions` lays the instructions out as Core's
`Instructions/*.md`: the base block, the detail level's block, and Spotlight
when the chat has one (#231). Web search stays desktop's. On the device each
block is a short form of Core's, embedded from
`src/DashyNMS.Mobile.Shared/Intelligence/Instructions/*.md`, with `{max_calls}`
set to the question's lookup limit (#637) and a closing line naming the tools
it doesn't get. A context of 32,000 tokens or more (Private Cloud Compute, #575)
gets Core's own blocks word for word, read from the pinned Core package, with
web search marked off.

`Fixtures/Intelligence/core-instruction-rules.json` maps every line of Core's
base, Brief, Balanced, Detailed and Spotlight blocks to its short form on the
phone, which must appear in the phone's block, or to a reviewed exclusion with
its reason. `IntelligenceInstructionsTests` reads Core's blocks from the pinned
package and fails when one has a line the map doesn't, so a rule desktop adds
shows up here. Full report is desktop's alone and isn't mapped.

Device checks (a request to change the network declined, an ambiguous name
answered with a question, a status answer with coloured bullets) are recorded
here once run on an eligible iPhone.

## Batch 3 shared data (core#90)

Mobile pins Shared `2.0.0-ci.44719120361.1` from DashyNMS/mcp#162, commit
`bcdbd8f9300ec3fccfe66d76cc553a9d6f528bce`, and Core `2.0.0-ci.76`, the Core it
was built and compatibility-tested against. The protected-main publish run
44719120361 passed its package, binary compatibility and Core compatibility
checks and verified the published feed bytes. The package SHA-256 is
`eeb47740dc19ea6cc3c42bccb6a6a6781a9e98154389c2ea725aa9e1adc57c67`; its
GitHub-hosted provenance attestation was verified against that commit and
workflow before pinning. It brings LibreNMS 26.11 VLAN numbers in device details
and endpoint tracing (DashyNMS/mcp#156) and device status words that follow the
reported state, with unknown and ignored devices counted apart in the overview
and per location (DashyNMS/mcp#121).

## Batch 6 Spotlight (#231)

Mobile pins Shared `2.0.0-ci.43744058288.1` from MCP #154, commit
`8f32942c194132fa7152f1e0f8e8448be7a3ac8e`. The protected-main publish run
43744058288 passed its package, binary compatibility and Core compatibility
checks and verified the published feed bytes. The package SHA-256 is
`1bccd1ecd09899a52e66ce26caa9bf8c52f883dcdaaf61a21e6085402b6dc530`;
its GitHub-hosted provenance attestation was verified before pinning.

Spotlight holds up to 32 Core object references with the encrypted chat. A
searchable picker offers devices, groups and locations. Reply objects offer
Open, Add to Spotlight and Spotlight only this, including VoiceOver actions.
Each change checks the captured connection, chat revision and source evidence,
revalidates the exact identity through Shared's HostObjectResolver, then checks
ownership again. Duplicates, missing objects and failures keep the old scope.
Changes are locked while answering. Only typed scope IDs, parent IDs and optional rule IDs feed both
the measured instruction budget and tool selection; a long scope can require a
shorter question or fewer targets. The model passes scope to tools that accept
it, as on desktop; Spotlight does not claim to impose an API access restriction.
Device acceptance for the picker, native actions and composer at large text,
with VoiceOver, in both themes and with the keyboard remains pending.

### Ask Intelligence (#232)

Rows use `IntelligenceObjectTargets` for typed identity, shared by swipe,
long-press and accessibility actions. Section rows retain sensor/port or
scope identity explicitly; rankings keep ports and map pins carry a verified
LibreNMS location ID separately from their map-local index. Without that ID a
map location is not offered as an object. Log and Graylog rows use their
resolved device, and neighbour links use the local device.

The entry captures the row's connection generation. `AskIntelligenceService`
revalidates the exact object before starting a new chat. Saved mode uses the
existing save-before-leaving flow. Single-chat mode retains preceding chats
and drafts in memory only; Switch to previous chat cycles back without writing
them, and a connection change clears them. Delete saved chats marks the current
and retained transcripts memory-only, preventing a later switch from recreating
deleted history while keeping drafts available until a new chat. Turning the setting off retains access in memory, with each retained private transcript permanently excluded from autosave. Transcript replacement suppresses captures until turns, draft, detail, Spotlight and privacy all belong to the incoming chat; collection observers cannot save a mixture during a retained-chat switch. Actions are unavailable while
Intelligence is off, the model is unavailable or an answer is running.
The Android accessibility adapter adds named native TalkBack actions alongside
iOS VoiceOver actions. Native swipe, screen-reader and entry-path acceptance
remain pending; CI cannot establish native gesture behaviour.

### Conversation export (#233)

The chat header offers Export conversation as Markdown or PDF, including the
single-chat conversation. A snapshot contains the title, visible questions,
readable answers, incomplete-answer notes and validated graph images. It
excludes the unsent draft, raw lookup records, hidden reasoning and diagnostics.
The Markdown formatter and file naming were adapted from desktop's
`IntelligenceChatExport.cs`; headings and incomplete-answer notes keep its
format. With graphs, the share is a ZIP containing the Markdown and its sibling
images folder, so relative graph links survive the share sheet. Without graphs
it is a plain `.md` file.

PDF uses iOS's native paginated print renderer over escaped, locally generated
HTML, with headings, tables, code, safe source links and a native-drawn graph appendix labelled by answer.
There is no new rendering dependency, script or externally loaded image. Both
formats recheck chat/connection ownership before sharing and clear the export
cache after the share completes. Golden tests compare desktop's Markdown
format and the PDF print-layout input; native PDF rendering, pagination,
selectable text, source-link annotations and graph inclusion still require
installed-device acceptance. A print-input golden is not a rendered-PDF test.

### Starter questions and activity (#640)

The twelve-question catalogue and four-at-a-time rotation are desktop's, with device
and alert questions filtered by the current connection's cached device/open
alert context. A question is also filtered until its required tools are offered.
New/restored chats rotate; ordinary activation only updates changed context.
Tool selection recognises the capacity and fan-problem starters explicitly.
Lookup rows use desktop's action wording and the collapsed summary reads
"Looked at …", including how many lookups could not be read. Running, failed,
stopped and interrupted states retain their existing explicit status text.

### Detail indicator (#656)

The native picker supplies the only dropdown indicator. The overlaid chevron,
150pt width cap and trailing indicator margin are removed. Brief, Balanced and
Detailed continue to use the existing selected-index/detail mapping and native
accessibility. The chip is at least 48pt high and allows native text scaling.
Native appearance at maximum Dynamic Type and VoiceOver remains unverified.

The CI-only `iOS rendered PDF golden` job now also compiles the production
renderer into a simulator harness and prints the same recorded chat with a
pinned, hash-verified desktop renderer. Both actual PDFs must preserve ninety
paragraphs over multiple pages, selectable table/code text, a clickable source
link, an embedded image and visible graph pixels against a checked-in golden.
It retains PDFs and page rasters as evidence. Runs 43758300189 and 43758304873
passed at `7da8e19`. The desktop PDF has six pages and the native PDF seven;
both preserve all ninety paragraphs, one embedded graph and the clickable
source link. The native raster contains 14,940 red graph pixels. This establishes
the recorded PDF regression, not installed-device chat UI acceptance.

The first native artifact exposed two PDF-specific differences: ligatures use
Unicode presentation forms in extracted text, so the semantic golden applies
NFKC normalisation; UIKit's HTML formatter omitted data-URI images, so the
production renderer now draws decoded graphs directly in an appendix labelled
by answer and graph number. The image-object and coloured-pixel assertions stay
required and passed in the native simulator golden.

CodeRabbit corrections keep Spotlight display names in UI/saved data only,
never in the system instruction block. The model resolves typed IDs through
normal read-only lookups. The uptime-ranking starter remains in the desktop
catalogue but is not offered on the phone: none of its reviewed operations
ranks the whole fleet by uptime. Open-alert starter eligibility also follows
successful generation-bound background monitor polls.
