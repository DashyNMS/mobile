# Shared Intelligence tools: inventory and proof design

Design for [mobile #596](https://github.com/DashyNMS/mobile/issues/596), reviewed
2026-11-11, with the first migration recorded on 2026-11-12 under
[First migration: find_device](#first-migration-find_device). The sections after
that one are the original inventory and plan; where they say no package exists,
the first-migration section supersedes them.

## First migration: find_device

Intelligence's `find_device` now runs DashyNMS/mcp's shared device search. The
other four lookups and Diagnostics' `NetworkOverviewLookup` are unchanged.

**Package.** `DashyNMS.Agent.Search` `0.1.0-ci.43135054163.1`, published by
DashyNMS/mcp's Search package workflow run 43135054163 from commit
`0bdf50e9249eafd880f1a5ee3bcd66e0d81549fe` to GitHub Packages, SHA-256
`731965d4bc971f62ff6ee49f47254335ee3295deea50184b9a49b2cdd7fcab1d`, with a
build provenance attestation. It is MIT licensed, targets net10.0, references
Core (minimum `2.0.0-ci.34`; mobile's direct `2.0.0-ci.64` pin wins) and has no
`ModelContextProtocol` reference. Upstream CI ran its tests against the exact
packed binary with Core ci.34 and ci.64. `scripts/verify-search-package.sh`
runs straight after restore and before anything builds, tests or publishes
against the package, in CI and in the Play and TestFlight workflows. It checks
the file's SHA-256, its attestation (signed by DashyNMS/mcp's
`search-package.yml` at that commit on `refs/heads/main`, GitHub-hosted runner),
the nuspec's repository and commit, that the extracted `lib/` files and LICENSE
match the verified file, and that the package has no build-time assets. There is
no NuGet lock file yet (#609).

**What mobile now does.** `CompactNetworkLookups` creates one
`HostDeviceSearch` per lookup set, so per model session and captured connection,
and calls the typed `FindDevicesAsync` with `Limit = 20`. Ranking (exact, starts
with, contains over hostname, sysName, display and IP; ties by hostname),
trimming, paging and redaction are upstream. Mobile's substring filter and
device-id ordering are deleted. Mobile only maps the typed result to the compact
profile: rows in the person's device-name style with `[[device:ID]]`, the exact
total in the heading, unfetched and over-budget matches counted as omitted, and
`IntelligenceEvidence` only for rows shown.

**Host ownership and isolation.** No Agent registration runs; mobile passes its
own clock, the server timestamp setting and `ConnectionBoundDeviceList`. That
client never touches the session's shared client. On the first search it asks
the app's `IntelligenceDeviceTransports` for a Core `LibreNmsTransport` of its
own, pinned to the captured connection's address, backup and token, and builds
Core's device API on it. A switch can't redirect its requests to another server.
The host keeps that transport in step with the session without changing its
server: the current request timeout and saved trusted certificates when it opens
and whenever settings change, the session's address (main or backup) at open and
whenever the session fails over or is switched back, and the session's request
timing. It is published before it is configured, so no change is missed, and
configuring and every later update run one at a time per transport, each
reading the latest settings and address: a change that lands mid-configure is
applied after it, never overwritten by the older state. Failover is the session's: when a chat transport can't reach the server
it asks the session to move (`ISessionService.SwitchToBackup`), so the whole app
moves together, and a chat for an earlier connection can't move a later one.
Each chat transport still has its own `ServerFailover`, because Core's
`Configure` resets the one it is given. A certificate it refuses goes to the
same `CertificatePrompt` as the session's, tagged with the session generation
the transport was opened for. A refusal from an earlier connection isn't asked
about, and accepting a prompt after a switch trusts nothing; otherwise the
certificate is trusted on that connection's chat transports straight away.
The session client's own refusals name only a host and a certificate, and can
come from the previous connection's request (the same host on another port, or
a certificate since rotated), so the prompt never asks about them as reported:
it probes the connection in use with a fresh request on a transport bound to its
generation and asks about what that meets, or nothing if it gets through.
Chats and lookups are bound by `SessionScope` (the session's connection
generation plus server, backup and token), not the connection object, which
Core replaces whenever a certificate is trusted or the timeout changes; those
keep the chat, while sign-in, a switch or sign-out ends it.
Of `IDevicesApi` only `ListAsync` is allowed; the other device endpoints, every
other API and all session controls throw before transport. Each model attempt
opens its lookups as an owned `IntelligenceLookupSet` and disposes it however
the attempt ends (answer, context retry, failure, Stop), which closes the
transport. A switch or sign-out during a search does the same at once, which
cancels the wait and refuses later calls, cached ones included. Late replies and
their evidence are discarded and the lookup says the connection changed. Tests
over loopback HTTP and TLS cover the switch-before-send case, the certificate
prompt and live trust, timeout changes, failover in both directions, stale
certificate prompts and isolation. Without a
transport factory, `find_device` fails closed rather than use the shared client.

**Cancellation, deadline, failures.** Caller cancellation propagates. Mobile's
linked 15-second deadline and `WaitAsync` bound remain around the call, and the
package adds its own `WaitAsync` and late-fault observation. Failures arrive as
fixed upstream categories with no server text or exception; mobile maps refused
to its own refusal line and everything else to the existing failure line. An
empty result is distinct from a failure. Nothing is logged.

**Profile.** Model-facing name, purpose and single `query` argument are
unchanged. Text search only: MAC-shaped queries are refused before any request,
and ARP and group endpoints are blocked by the wrapper (#608).

**States.** The shared summary reports only up, down and disabled, and reads an
omitted status as down. Mobile doesn't use that word: each row's state comes
from Core's `ReportedState` on the raw record from the same device list, so an
unreported state shows as Unknown and ignored devices as Ignored. Upstream's
summary itself is #604.

**Fixtures.** `tests/.../Fixtures/DeviceSearch` holds synthetic LibreNMS
responses (documentation addresses, `example.net`) and their expectations:

- ranked ids and totals per query, the only endpoints a text search reads,
  status words and reported states, and the failure category for each HTTP
  status (`cases.json`);
- group, location and status scope cases, with the requests each may read
  (`scopes.json`, `groups.json`, `group-1.json`);
- device-list failures served over HTTP: error envelopes, HTML, a non-list
  body, retried 502 and 503, an unanswered request and an address with nothing
  listening (`transport.json`).

The files are byte-identical to DashyNMS/mcp's. `scripts/check-search-fixture.sh`
fails CI if mobile's copies differ from the pinned upstream commit. Upstream,
the MCP `find_device` tool and `HostDeviceSearch` run against them: over a fake
transport, and over loopback HTTP through the MCP server's own services and
Core's real transport.

Mobile runs them two ways:

- through Core's real device API over a fake transport, to both the package's
  executor and mobile `find_device`;
- over loopback HTTP, to mobile `find_device` through the app's per-chat
  transports and to `HostDeviceSearch` over Core's real client.

Both must give the same ids, order, totals and per-server request counts,
including Core's retries. Only `HostDeviceSearch` is held to the exact failure
category. Mobile `find_device` must give its refusal line for `Refused` and its
failure line for every other category, with no rows, evidence or server text.
Upstream, the MCP tool words failures its own way and reports no category, so
its reply is checked only for an error with no data and nothing secret-shaped.
No test shows the MCP tool and mobile agree on a category.

Mobile's `find_device` takes search text only. The scope cases therefore run
through the package on mobile's Core pin, which shows the package agrees with
MCP there but doesn't show mobile offers scopes (#608). A separate test checks
that text naming a group reads only the device list.

Not compared across adapters: MAC search, paging, a combined group and location
scope, TLS and failover, cancellation, and faults. `find_device` reads no
alerts, and mobile's alert lookups remain its own implementation (#601), so
fault identities, severity and grouping have no cross-host comparison yet. See
the fixture README. A fixture package (#606) would replace the copy.

**CI proof added.** The Android Release build fails on any trim, AOT or NuGet
warning naming the package and reports the APK size and effective trimming/AOT
properties. A new `iOS Release (device, unsigned)` job builds Release
`ios-arm64` with the shipping defaults (no `PublishAot`), fails on such
warnings, requires the assembly in the `.app` and its Mono AOT output, records
evaluated properties and sizes, and keeps the Foundation Models weak-link check.
These prove compilation through the device toolchains, not execution.

**Not done, and where it is tracked.** No trimmed Android runtime run, iOS
simulator smoke harness or installed-device check; no measured token use; no
size delta against a matched baseline (#607). Remaining migrations: overview
(#600), alerts (#601), device summary (#602), sensors (#603); further catalogue
tools (#610).

**Measurements.** See [Measurements](#measurements-for-the-first-migration).

## Evidence and reproducibility

The review used these exact source revisions:

- Mobile `938c53492b48d0b7b8bfa1806a2f7295c46f2cec`, particularly
  [CompactNetworkLookups](../src/DashyNMS.Mobile.Shared/Intelligence/CompactNetworkLookups.cs),
  [NetworkOverviewLookup](../src/DashyNMS.Mobile.Shared/Intelligence/NetworkOverviewLookup.cs),
  the shared project's package reference, conversation view model, evidence and answer actions.
- [MCP/Agent `b0a808c1375041c93244403ca37c375cb73ed7d0`](https://github.com/DashyNMS/mcp/tree/b0a808c1375041c93244403ca37c375cb73ed7d0).
  Paths below starting with `Agent/` mean `src/DashyNMS.Agent/` at that revision.
- [Core `4dce2a7ceded871b0f0e7e6800223ecf830691cb`](https://github.com/DashyNMS/core/blob/4dce2a7ceded871b0f0e7e6800223ecf830691cb/src/DesktopNMS.Core/Intelligence/IntelligenceTools.cs),
  fetched from upstream main, rather than assuming the adjacent Core checkout was current.
  This is the current source allowlist, not proof of the contents of every published package.

To refresh the inventory, inspect every `McpServerTool(Name = ...)` attribute
under `Agent/Tools`, compare those names with Core's
`IntelligenceToolAllowlist.Names`, and inspect signatures and bodies. Count
names, not classes or README claims. Check publication independently of source.

## Decision and package blockers

Extract typed execution/results **inside DashyNMS/mcp's Agent library**, and
make MCP and mobile thin adapters over those services. Retain the existing
query implementation and move its result shaping into explicit DTOs; do not
copy queries into mobile or parse the public tools' JSON. Core continues to
own transport, LibreNMS models and alert grouping. A new Core API is justified
only for behaviour that belongs to all Core consumers.

Direct calls are possible in ordinary .NET source, but are unsuitable for this
migration as currently exposed. Of 24 public attributed tool methods, 23
return `Task<string>` through `ToolRunner.RunAsync`; `graph` returns
`Task<CallToolResult>` with MCP content. Query bodies such as `FindAsync`,
`OverviewAsync`, `DeviceAsync`, `OpenAsync` and `DetailsAsync` are private and
mostly produce `JsonObject`/anonymous data. Public summary records and
`ToolResult` helpers do not constitute a typed query contract.

`Agent/DashyNMS.Agent.csproj` targets `net10.0` and references
`ModelContextProtocol.Core` 2.2.0. MCP's `Directory.Build.props` pins Core
`2.0.0-ci.34`; mobile pins `2.0.0-ci.64`. These are different dependency
baselines, not evidence of binary incompatibility. Reconcile them upstream,
then verify API, alert semantics and runtime compatibility in CI. Do not
silently downgrade mobile or rely on NuGet's dependency unification as proof.

Publication checks on the review date:

- GitHub's authenticated organisation NuGet package listing returned only
  `DashyNMS.Core`; its version list included `2.0.0-ci.34`, `2.0.0-ci.64` and the newer
  `2.0.0-ci.65` (rechecked through the authenticated package API). Mobile
  still pins ci.64; ci.65 is available, not an automatic upgrade recommendation.
  Listing verifies publication metadata, not restored package contents or
  compatibility. No restore or package binary inspection was performed.
- NuGet.org's flat-container endpoint for `dashynms.agent/index.json` returned 404.
- MCP workflows build/test and publish a container; no Agent NuGet pack/push
  workflow was present. The project is a library, but that does not make it a
  published, consumable package.

Thus no published Agent package was discoverable in the intended feeds with
this access. This is not a claim about inaccessible/private alternative feeds.
Upstream must establish an immutable, versioned package, dependency policy,
package-source mapping, release provenance and mobile repository read access.
Mobile's `nuget.config` already routes `DashyNMS.*` to GitHub Packages; verify
Agent restore with the CI token before adding a pinned reference. No project
reference to a developer's MCP checkout should become the shipping dependency.

## Current catalogue

All 24 names below are in the current Core source allowlist and have explicit
read-only, non-destructive attributes in Agent. None of those attributes proves
mobile support. Only the five chat lookups and diagnostic described below are
currently supplied by mobile. Additional tools remain deferred until their
own session, profile and proof work is complete.

| Agent source under `Tools/` | Exact tool name | Capability and mobile restriction |
| --- | --- | --- |
| AlertTools.cs | `active_alerts` | Current grouped device/rule alerts or raised occurrences in a time window; separate fault totals, paging and filters. Existing mobile name has different semantics. |
| RuleTools.cs | `alert_details` | Fault/rule detail and history. Preserve source and scope; a representative fault ID is not a group identity. Deferred. |
| RuleTools.cs | `alert_rules` | Rule search, severity, disabled rules, paging and verbose definitions. Permission dependent; deferred. |
| ConfigTools.cs | `config_backup` | Unimus backup text or summary under ConfigText policy. Requires configured integration; deferred. |
| ConfigTools.cs | `config_diff` | Unimus configuration comparison. Requires integration and redaction; deferred. |
| ConfigTools.cs | `recent_config_changes` | Unimus recent changes. Requires integration; deferred. |
| DetailTools.cs | `device_details` | Inventory, VLANs, FDB, ARP, PoE, wireless, BGP/OSPF/VRFs, availability and addresses. FDB/ARP are not default sections; endpoint permissions and section failures apply. Deferred. |
| DeviceTools.cs | `device_health` | State, uptime, maintenance, grouped alerts, CPU/memory/storage and problem ports. Closest composite to summary, not equivalent. |
| DetailTools.cs | `device_neighbours` | CDP/LLDP links, including reports from the other end. Requires fleet link data; deferred. |
| HealthTools.cs | `device_sensors` | Threshold-assessed, problem-first readings, type aliases, limits and omissions. Existing mobile returns raw readings. |
| DeviceTools.cs | `find_device` | Ranked name/IP search, MAC via ARP, status and scope filters, paging. Proposed first migration. |
| GraphTools.cs | `graph` | Graph discovery/fetch and PNG MCP content, not a text-only tool. Graph transport/authentication and image policy need separate proof; deferred on-device. |
| HealthTools.cs | `health_overview` | Scoped sensor assessment; permissions may require bounded per-device fallback. Deferred. |
| ScopeTools.cs | `list_groups_and_locations` | Group/location catalogue and counts. Exact filter names and IDs must remain distinct from labels. Deferred. |
| NetworkTools.cs | `network_events` | Network event window, filters and paging. Permission and timestamp semantics apply; deferred. |
| NetworkTools.cs | `network_overview` | Visible-account device counts, grouped alert/fault totals, ports, system version, partial failures. Existing mobile is narrower. |
| PortTools.cs | `port_status` | One port's state, counters/rates and connected context. Do not equate rates with historical traffic; deferred. |
| LogTools.cs | `search_logs` | Graylog messages, source matching, streams/windows. Requires configured Graylog client; deferred. |
| NetworkTools.cs | `top_devices` | Ranked device resource metrics with scope filters; deferred. |
| NetworkTools.cs | `top_errors` | Ranked interface error rates with scope filters; deferred. |
| NetworkTools.cs | `top_interfaces` | Ranked interface traffic rates with scope filters; deferred. |
| TraceTools.cs | `trace_endpoint` | IP/MAC tracing through stored ARP/FDB/topology, not active traceroute or packet capture; incomplete topology remains uncertain. Deferred. |
| EventTools.cs | `device_events` | Device event/alert history in a bounded window; deferred. |
| EventTools.cs | `what_changed` | Composite history with optional configuration/log integration data. Missing integrations must be explicit; deferred. |

There is no separate Agent `device_summary` tool. A current 24-name allowlist
corrects the former 22-name planning note in DEVELOPING.md; it does not certify
these tools for Foundation Models. No write, acknowledgement, rediscovery,
SSH execution or new remote MCP service is proposed here.

## Every mobile lookup and semantic differences

The chat obtains five definitions through `IIntelligenceLookups.Create`.
`NetworkOverviewLookup.AsLookup` is a sixth implementation used by Diagnostics,
not another chat catalogue entry. Registration is in Shared's
`ServiceCollectionExtensions`; Diagnostics consumes it in
`IntelligenceCheckViewModel`. Retain the diagnostic baseline until explicitly
replaced so its measurements remain comparable.

| Mobile implementation | Shared target | Differences requiring an explicit decision |
| --- | --- | --- |
| Compact `network_overview` | NetworkTools overview typed service | Mobile counts raw open rows and lists alert/down-device evidence; Agent counts device/rule groups and faults separately, includes ports/system and down locations, and tolerates some failed sources. Agent counts alone cannot supply the current per-row evidence. Add shared typed bounded detail or deliberately change the profile; never invent references from aggregate counts. |
| Compact `find_device` | DeviceTools find typed service | Mobile substring matches hostname/sysName/display/IP, sorts by numeric ID, refuses blank input, has no paging. Agent ranks exact/prefix/contains then hostname, trims input, supports MAC, status/group/location, default 10/max 50 and continuation. Adopt shared ranking intentionally, expose true omissions and keep the initial mobile profile to text search. |
| Compact `device_summary` | No exact equivalent | Agent device_health is a much broader fan-out; verbose find results include OS/hardware, while device_details covers other sections. Add a small shared device-summary operation over existing resolver/device data upstream or agree a reviewed health profile. Do not recreate a composite query in mobile. Preserve location ID plus exact filter name and device-name settings. |
| Compact `active_alerts` | AlertTools current-open typed service | Mobile accepts positive device ID or empty, sorts unacknowledged before severity then ID, and emits raw alert/fault rows. Agent groups by device/rule, orders worst first, has severity/scope/time-window filters, occurrence history and paging. Group totals, partial acknowledgement, representative fault and child evidence require a new explicit profile; name equality is not parity. |
| Compact `device_sensors` | HealthTools device typed service | Mobile reads fleet sensors, filters by positive device ID, sorts by sensor ID and labels readings as raw with unknown health. Agent resolves names/IDs, uses SensorSource permission handling, assesses limits/defaults, orders problems first, supports type and caps at 100. Preserve missing-reading vs zero and avoid claiming an unassessed raw value is healthy. |
| Diagnostic `NetworkOverviewLookup` | NetworkTools overview, later | Names capped at eight/40 characters; severity before acknowledgement; includes disabled/ignored/unknown device counts. No typed evidence. Keep separate from chat's 900-character results during the first proof. |

Mobile currently emits device-name-style labels, qualified `alert:`/`fault:`
identity alongside `[[alert:ID]]`, and typed evidence with device/rule ownership.
Groups must use a distinct device/rule key; a fault and legacy alert with the
same number cannot share ownership. Preserve exact group/location filter text
separately from cleaned 80-character labels. Reference navigation must still
validate evidence, target type and connection ownership.

## Required upstream contract and host ownership

The following APIs are proposed, not present or tested:

1. Extract `FindDevicesAsync(FindDevicesRequest, CancellationToken)` first,
   returning typed matches, stable IDs, total/next offset, source age and
   unavailable/error metadata. Both DeviceTools and mobile call this same
   executor. Retain ranking/filtering/paging upstream.
2. Define typed results for each later operation, including grouped alerts,
   qualified fault identities, partial failures, scope and truncation. MCP
   serialises them through its existing result policy. A mobile formatter
   changes presentation only. A caller must not bypass redaction by taking the
   typed path; make the public result safe before either formatter sees it.
3. Supply registration that accepts host-owned `ILibreNmsClient`, integration
   clients, clock and options without registering transports or credentials.
   Reject missing required clients. Do not call current `AddDashyNmsAgent`
   inside mobile: it unconditionally registers singleton transports, failover,
   LibreNMS, Graylog and Unimus clients and could supersede host registrations.
4. Create an Agent executor and all its directories/caches per connection
   generation. DeviceDirectory/FleetCache cache for 60 seconds and have no
   connection key. Reusing them with mobile's mutable session client can expose
   the old network's data. Bind execution to a captured session or guarded
   transport, cancel on switch/sign-out, drop caches, refuse new old-generation
   calls and discard late results/evidence. A final reference-equality check
   alone cannot stop an old operation sending a request on a new session.
5. Use the host's certificate prompts/trust, backup transport, credentials,
   caching and timestamp setting. Integrations use their own captured host
   configuration and ownership. Disable their tools when absent; never read
   server environment variables as a mobile session configuration source.
6. Propagate caller cancellation through every request and semaphore. Retain
   mobile's linked 15-second deadline and WaitAsync bound for uncooperative
   transports. Distinguish cancellation, timeout, refusal, partial and empty
   results. Agent ToolRunner propagates caller cancellation but supplies no
   equivalent whole-lookup deadline. Its exception logging needs a mobile
   policy that excludes payloads/questions/credentials and exception detail.

`ToolResult.Render` currently redacts string values, caps UTF-8 JSON at a default
16 KiB and marks truncation/omitted entries. Redactor recognises secret patterns;
it does not promise to remove arbitrary personal data or every possible secret.
Mobile's Clean function prevents control/marker injection, but is not secret
redaction. Keep both protections, configuration Off/Summary/Full policy and the
untrusted-data boundary. Never turn device/log/config text into trusted model
instructions. Do not lower the JSON byte cap to 900 and parse the remainder:
its generic shrinking can remove nested rows and shorten text before evidence
is derived. Compact evidence must come from the exact accepted typed rows.

The current failure path requires an explicit adapter policy. At the pinned
revision, `Agent/ToolRunner.cs` returns `ToolException.Details` and
`ToolErrors.Describe` can return server text under `details.server_message`.
It also calls `LogWarning(ex, ...)` and `LogError(ex, ...)`. Pattern redaction
at `ToolResult.Render` does not make arbitrary server bodies or exception logs
safe. The mobile adapter must omit server_message, raw bodies, URLs, exception
messages/objects and stack traces from model text, evidence and diagnostics.
Map failures to reviewed status codes and fixed safe messages; log only the
allowlisted tool name, status, duration and generation identifier through a
host privacy logger. Apply this to partial component failures too. Test server
text with embedded secrets and prompt instructions, including errors that
bypass successful-result formatting. MCP may retain its own reviewed server
policy; mobile must not inherit it implicitly.

Host mapping must be explicit in the proposed registration:

| Agent dependency/option | Mobile mapping |
| --- | --- |
| `ILogger<T>` / `ILoggerFactory` | Host-owned factory with the privacy filter above; do not pass exceptions to the general diagnostics logger. |
| `TimeProvider` | Host-owned clock, replaceable in fixture tests. |
| `ServerTimestampsAreUtc` | Current captured server setting, renewed on session/settings change. |
| `GraylogQueryField`, `GraylogMatchAnyAddress` | Captured host Graylog settings and client; disabled until that integration is supported. |
| `ConfigText` | Explicit mobile consent/policy, default Off for the proof, never Agent's current Full default. |
| `MaxResultBytes` | Explicit upstream/MCP safety cap, default 16 KiB; separate mobile 900-character projection, never JSON slicing. |

The per-generation requirement follows existing mobile behaviour, not a new
claim that Core already isolates Agent. See
[SessionService](../src/DashyNMS.Mobile.Shared/Desktop/SessionService.cs)
for reconnect/sign-out and `StateChanged`,
[CorePolling](../src/DashyNMS.Mobile.Shared/Services/CorePolling.cs) for
`OnSessionChanged`, generation increments, worker disposal and `PollSession`,
and [CompactNetworkLookups](../src/DashyNMS.Mobile.Shared/Intelligence/CompactNetworkLookups.cs)
for the captured connection check. At the pinned MCP revision,
[DeviceDirectory](https://github.com/DashyNMS/mcp/blob/b0a808c1375041c93244403ca37c375cb73ed7d0/src/DashyNMS.Agent/Devices/DeviceDirectory.cs)
and [FleetCache](https://github.com/DashyNMS/mcp/blob/b0a808c1375041c93244403ca37c375cb73ed7d0/src/DashyNMS.Agent/Devices/FleetCache.cs)
key freshness only by fetch time, with a 60-second lifetime and no server key.
That combination justifies new generation-owned caches and transport guards;
it does not prove an exploit or a completed isolation fix.

## Compact schema and task selection

The first migration targets on-device Intelligence only; it does not implement or change the deferred ChatGPT route. Before any later cloud use, define consent, the detail dial and a separate data-minimisation profile for tool output leaving the device.


Proposed initial profile: keep the five existing model-facing names and single
`query` string argument, positive IDs for dependent tools, no arbitrary JSON
execution. For first `find_device` proof expose text search only; MAC and scope
search stay unadvertised until reviewed. A static adapter maps the single
argument to a typed request and never allows model-selected method names.

Use the intersection of Core's reviewed allowlist, implemented static registry,
host integration availability and task profile. Check it at invocation as well
as schema listing. Unknown/disallowed tool names must fail before transport.
`device_summary` needs an explicit reviewed alias/contract rather than slipping
past that intersection. Future task profiles can select alerts/rules, health,
ports/topology or history, with a small search/overview base. Do not expose all
24 schemas to the on-device context or infer tools from untrusted network text.

Keep at most 900 characters per reply, definitions at most 80 tokens each,
an instruction budget of 150 tokens and the existing conversation reservation for
two results and output inside 80% of runtime context. These are existing design
limits, not new measurements. DEVELOPING.md records 8,192 tokens measured on
an iOS 27.2 device, 809 overview characters at about 310 tokens, and a conservative
4,096-token iOS 26.4 assumption that was not measured. The #570 instructions measured 285 characters, about 60 tokens. The evidence is the dated #570 diagnostic reading in
[DEVELOPING.md](DEVELOPING.md#what-the-on-device-model-can-do-570), from
TestFlight `3d36e85`: 559 input/239 output tokens for one question, not a
benchmark of the replacement or the full catalogue.
[Conversation budgeting](../src/DashyNMS.Mobile.Shared/Intelligence/IntelligenceConversationViewModel.cs)
reserves 550 fixed tokens, 720 for two results and 400 for output before
applying the 2.5 character estimate within 80% of reported context.
Character-to-token ratios are language dependent. Measure the replacement's actual definitions/results
through the bridge, including long names and non-Latin text.

A compact typed projection carries status, visible-account scope, freshness,
shown rows, exact total where known, unavailable sources, upstream omissions,
profile omissions and continuation where supported. Render whole rows with
names and intact IDs, reserving space for omissions and failures. Do not sum
nested fault omissions as if they were omitted groups. If the heading itself
exceeds budget, use a bounded fallback without evidence. Only accepted rows
produce `IntelligenceEvidence`; an aggregate count produces no entity link.

## CI and identical-fixture proof plan

Existing `.github/workflows/ci.yml` runs shared Debug tests, Android Release
compilation and an iOS Debug simulator build with bridge weak-link inspection.
That iOS job does not prove Release device trimming/AOT. TestFlight publishes
Release `ios-arm64`, but a signed distribution workflow is not a substitute for
a required PR proof job. No passing CI run for an Agent migration is claimed.

Upstream and mobile CI must run these gates after implementation:

| Gate | Required evidence |
| --- | --- |
| Package/dependencies | Restore immutable Agent and reconciled Core pins using mobile CI permissions; record transitive MCP dependencies and package provenance. |
| Static catalogue/schema | Generate or explicitly register reviewed DTO serializers and invocation delegates. Compare exact names and schemas with upstream catalogue/Core allowlist, reject unknown calls. Current MCP ToolRegistration uses GetTypes, attributes, MethodInfo, ActivatorUtilities and runtime schema construction; do not ship this discovery path on mobile. |
| iOS Release publish/warnings | Add a PR `ios-arm64` Release publish with shipping trimming/AOT settings, unsigned where supported by the head and widget setup. Capture effective properties and fail on new relevant linker/AOT warnings with reviewed baselines. This produces a device artefact; it cannot run on a simulator. Keep Foundation Models weak-link inspection. |
| iOS simulator execution | Build a separate simulator-compatible smoke harness and execute generated serializers, static dispatch and every advertised tool with fixtures. Validate schemas. This does not execute the ios-arm64 artefact or prove device AOT behaviour. |
| Installed device | Execute the Release device artefact through every advertised adapter and serializer, then perform the native acceptance below. Record separately from publish and simulator results. |
| Android | Retain Release compilation. Run the static dispatch and generated serializers with the same fixtures in a trimmed Release build on an emulator or instrumented device; host CLR tests do not establish Android runtime or trimming behaviour. |
| Session isolation | Switch A to B during directory fetch, cached result, integration call and an uncooperative late request. Assert no A rows/evidence/navigation on B, no cross-generation request or cache reuse, and no result after cancellation. |
| Privacy/read-only | Transport spies allow only reviewed read endpoints. Assert token/config secrets and injected marker/control/prompt text do not leak into trusted output, evidence or logs. Unknown tools and absent integrations make zero requests. |

The head and TestFlight command do not explicitly set `PublishAot`,
`UseInterpreter`, `MtouchInterpreter`, `TrimMode` or `MtouchLink`. iOS Release
normally uses Mono AOT and partial trimming; `PublishAot=true` selects Native
AOT and must not be silently introduced as though it were the shipping default.
MAUI enables the interpreter for iOS Debug, which changes trimming defaults.
See the SDK's [Apple build properties](https://github.com/dotnet/macios/blob/main/docs/building-apps/build-properties.md).
Record the actual workload version and evaluated properties in future CI,
including `PublishTrimmed`; SDK defaults are not an evaluated-build report.
Android Release normally enables `RunAOTCompilation` and profiled AOT on Mono,
which is not iOS full AOT or proof of reflection safety; see
[Android build properties](https://github.com/dotnet/android/blob/main/Documentation/docs-mobile/building-apps/build-properties.md).
No property evaluation, publish or execution was run for this inventory.

Isolate the reusable typed executor/DTO/static registration from MCP attributes,
`CallToolResult` and schema discovery in an upstream assembly with no
`ModelContextProtocol.Core` reference. Keep the existing MCP facade separate.
If upstream instead proposes retaining that dependency, review its transitive
closure and trimming impact before accepting it. CI must retain the resolved
package graph and compare matched baseline/candidate Release IPA/APK sizes,
uncompressed managed/native assembly sizes and cold-start/memory readings on
the same SDK, RID, signing and packaging settings. Record absolute bytes and
deltas; no MCP size cost or successful isolation is measured here.

Use one immutable set of synthetic **transport responses**, not independent
expected strings. Agent has Core-derived fixtures under
`tests/DashyNMS.Agent.Tests/Fixtures/LibreNMS/{26.9.1.1,26.10.0}` and compatibility,
permission, grouping, scope and catalogue tests. Reuse their provenance and pin
fixture hashes/revision in a shared fixture bundle. Before publication or copying into mobile, scan and manually review every fixture for identifiable hostnames, IP and MAC addresses, serials, SNMP locations and contacts, and rack or site names; replace any such data with synthetic values. This audit is a release gate and has not been performed. Proposed concrete mechanism:
upstream publishes a test-only `DashyNMS.Agent.TestFixtures` NuGet package with
JSON, licence/provenance and a SHA-256 manifest. Both test projects pin the exact
same immutable version and verify the manifest; the production app must not
reference or embed it. This package does not exist yet. Existing upstream
fixtures are copied files, not an already shared package. Both the MCP adapter and
mobile adapter must execute the same typed services against those responses.
Compare canonical semantic DTOs before presentation, then independently assert
MCP JSON and compact text/evidence. Never implement a mobile oracle by repeating
the query algorithm, and do not treat the existence of upstream tests as a
passing migration result.

Existing files, verified in the pinned tree, include `faults-eight-cores.json`,
`faults-grouped.json`, `alertlog-eight-cores.json`, `devicegroup-empty.json`,
`devicegroup-members.json`, `forbidden-alert.json` and `forbidden-group.json`,
plus versioned devices/rules/alerts/log responses. Their README identifies Core
#80 as provenance and instructs manual recopying. Existing test classes include
`LibreNmsCompatibilityTests`, `AlertGroupToolsTests`, `PermissionToolsTests`,
`ScopeIdentityTests` and `ToolCatalogueTests`. Those files do not establish the
whole matrix below. Ranked-search transport bundles, cross-adapter canonical
DTO comparisons, generation-race/late-cancellation fixtures, error-log leakage,
non-Latin budget and compact evidence omission assertions are new work. Add
or extend synthetic inputs for every matrix case not demonstrably covered;
record a case-to-file/test manifest in the implementation PR.

The required matrix, including existing material to reuse and new cases, is:

- Legacy alert and fault with colliding numeric IDs, eight faults on one rule,
  partial acknowledgement, sibling recovery and incomplete history. Compare
  group/fault totals, severity order, source-qualified keys and device/rule scope.
- Duplicate/missing device names, ambiguous resolution, exact/prefix/substring
  ranking, empty scopes and group/location boundary membership. Compare paging
  without skips, including upstream truncation followed by compact omissions.
- Missing vs zero sensor readings, threshold/default assessment and denied fleet
  endpoint with bounded per-device fallback; unknown remains unknown.
- 401/403/admin-only/404, failed device-name lookup, one failed component, timeout,
  caller cancellation and transport ignoring cancellation. Verify unavailable
  versus empty and stable safe messages without raw HTTP bodies.
- Exact location filter text with long/control/marker-like labels, redaction
  markers and configuration Off/Summary/Full. Check evidence only for shown rows.
- Fresh and 60-second-old cache states, same IDs on different networks and
  integration configuration changes. Record request count and source age.

For the first migration, CI runs ranked search fixtures through MCP FindDevice
and mobile `find_device`, verifies the same match membership/order/IDs and
truthful omissions, and exercises its static schema through the bridge. Run the same fixtures on the iOS simulator and the Android trimmed Release runtime; execute them on an installed iOS Release device as a separate acceptance check.
Remove mobile's substring selection and ID ordering only in that implementation
PR after the equivalence tests exist. Compare presentation by its specified
profile, not byte equality with verbose MCP JSON.

Native-device acceptance remains separate: Foundation Models availability,
actual tool choice, small-context recovery, stop/suspend/resume, connection
switches, evidence navigation, VoiceOver and memory behaviour on supported
phones/tablets. Simulator CI cannot prove model quality or installed-device AOT
execution. Latency, allocations, package/IPA/APK size delta, cold start, cache
request counts and real token usage must be recorded before expanding tools.
All replacement measurements remain unverified in this design.

## Follow-up issues

Opened with the first migration (2026-11-12):

- #600 overview, #601 grouped alerts, #602 device summary, #603 sensor assessment:
  each an upstream typed operation plus a mobile migration.
- #604 truthful device states in the shared summary.
- #605 per-connection client so a retired search can't send (done in mobile by
  a per-chat Core transport; a Core factory would only tidy it).
- #606 one fixture bundle run through both adapters.
- #607 runtime, installed-device, token and size measurements.
- #608 MAC, group and location search in chat.
- #609 NuGet lock files.
- #610 further catalogue tools (epic).

## Staged work and acceptance status

1. Open an MCP implementation issue/PR for typed find execution, host-owned
   registration, safe results, static mobile registry/serialisation and immutable
   Agent publication. Align Core dependency there; make a Core issue only for
   missing transport/model contracts. Validate MCP output compatibility upstream.
2. Open a mobile proof PR pinning that package and migrating only `find_device`.
   Add identical-fixture comparisons, isolation/privacy gates and Release iOS
   AOT proof plus Android CI. Record measurements and device checks separately.
   Preserve the other four lookups and Diagnostics during this proof.
3. Plan separate upstream/mobile PR pairs for overview/grouped alerts and fault
   detail/rules; summary/health/sensors; groups/locations and ports/topology;
   history/logs/configuration; finally graph/image handling. Each pair defines
   its compact profile, missing-data policy, typed evidence and fixtures before
   advertising capability. Integration-dependent tools stay disabled until ready.
4. Retire each duplicated mobile query only when its replacement passes its
   gates. Update the catalogue and DEVELOPING.md as definitions change. Reassess
   task selection and context/size cost before each addition.

This document supplies a source inventory, mapping, blocker decision and proof
plan. It does **not** complete host isolation/cancellation/redaction proof,
Release AOT or Android migration proof, identical-fixture comparisons, measured
compact profile, first lookup migration or performance/device acceptance.
Upstream implementation is required before those #596 criteria can be met.

## Measurements for the first migration

Recorded from CI for the PR that made the migration. Anything not listed is
unverified.

- Package: nupkg and assembly sizes are written to the `Unit tests` job summary.
- Android Release APK size and iOS Release `.app` size are written to their job
  summaries. No matched baseline build was compared, so no delta is claimed.
- Tokens: the tool definition text is unchanged; results stay within 900
  characters. No bridge measurement was taken (#607).
