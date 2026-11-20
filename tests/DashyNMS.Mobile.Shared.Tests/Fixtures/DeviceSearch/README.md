# Device search fixture shared with mobile

A synthetic LibreNMS `/api/v0/devices` response (`devices.json`) and what
`find_device` must make of it (`cases.json`). DashyNMS/mobile keeps
byte-identical copies in `tests/DashyNMS.Mobile.Shared.Tests/Fixtures/DeviceSearch`
and runs its Intelligence `find_device` against them; its CI fails if its copies
differ from these at the commit it pins. Change both together.

Every name and address is invented: `example.net` and the documentation ranges
192.0.2.0/24 and 198.51.100.0/24. Never put real network data here.

`cases.json` holds:

- `requests`: the only endpoints a text search may read.
- `cases`: device ids in ranked order and the exact total for each query
  (exact, then starts with, then contains, over hostname, sysName, display name
  and IP; ties by hostname, case-insensitive).
- `states`: the shared status word for chosen devices (MCP's `status`) and the
  state LibreNMS actually reported (the summary's typed `State`, as Core's display
  name). Both come from the reported state (MCP #121): device 9 is ignored
  (`ignored`, `Ignored`), device 10's response leaves out `disabled` and device 11
  reports no flags at all (both `unknown`, `Unknown`). Neither is called up or
  down, so the `down` status scope finds device 8 alone.
- `failures`: the HTTP status of a failed device list and the
  `DeviceSearchFailure` the host search maps it to. The category is the host's
  contract: the MCP tool returns its own sentence per status instead, so the
  shared part is that both fail without data after reading only `requests`.

Devices 1 to 3 repeat the ranking case in `DeviceSearchTests`. The rest add
sysName prefixes, devices added by IP, duplicate display names, disabled, down,
ignored, partly reported and unreported devices, a secret-shaped display name and
marker-shaped text.
Every device has a location (`Core site`, `Lab` or `Lab annexe`) for the
scope cases; location doesn't affect text ranking.

`groups.json` (`/api/v0/devicegroups`) and `group-1.json`
(`/api/v0/devicegroups/1`) answer the group scope. Group 3 is empty: LibreNMS
answers it with a 404, which Core reads as no members.

`scopes.json` holds the routes a scoped search may read and its cases:

- group, location and status scopes with the ids, total and requests each one
  gives;
- an empty group;
- an ambiguous and a missing group name, an ambiguous location and an invalid
  status. `HostDeviceSearch` fails each as `InvalidRequest`; the MCP tool's
  reply is checked only for an error with no data. Both read only the listed
  requests, and an invalid status reads nothing.

`transport.json` holds failed device lists served over HTTP: 401, 403, 404,
500, 502 and 503 with LibreNMS error envelopes or an HTML page, a 200 that is a
login page, a 200 error envelope, a 200 whose `devices` isn't a list, a request
that is never answered (`hang`, at the 5-second minimum timeout) and an address
with nothing listening. `requests` counts what reaches the server, including
Core's retries: 502 and 503 are tried four times, and nothing reaches the
unreachable address. Bodies carry secret-shaped text that no adapter may repeat.
Each case's `code` is the `DeviceSearchFailure` the host search must give. The
MCP tool words failures its own way and reports no category, so its reply is
checked only for an error with no data and no secret-shaped text.

`SharedSearchFixtureTests` runs every case and status word through both the MCP
`find_device` tool and `HostDeviceSearch` over a fake transport, and every
`failures` entry through both. `SharedSearchHttpFixtureTests` serves the same
files over loopback HTTP to the MCP server's own services (`AddDashyNmsAgent`,
Core's real client and transport, `find_device` as the SDK activates it) and to
`HostDeviceSearch`. It runs the text cases, every scope case and every transport
failure, and checks each server's request log.

DashyNMS/mobile serves the same files over HTTP to its Intelligence
`find_device` through its own per-chat transports. Mobile's `find_device` takes
only search text, so mobile runs the scope cases through the package's
executor over its own Core client and checks that `find_device` never reads a
group. That shows the package and mobile's Core pin agree on scopes. It doesn't
show that mobile offers them.

## What this fixture does not prove

These are not compared across adapters here, and parity on them should not be
claimed from it:

- MAC search over ARP, paging past the first page and the verbose record.
- A group and a location together, and the `locations` endpoint.
- Admin-only refusals, TLS and certificate failures, backup-address failover,
  cancellation and a retired connection. `HostDeviceSearchTests` covers the
  host side of these; the MCP side has its own tests against different inputs.
- Faults (alerts) and other tools: `find_device` reads no alerts, so this
  fixture says nothing about fault identities, severity or grouping.

Maintenance adds one `devices/{id}/maintenance` read for each returned row,
never for matches outside the page. Each basic case now lists its complete
`requests`; top-level `requests` remains the device-list-only failure path.
`scopes.json` includes each successful page's maintenance requests.
`maintenance.json` covers active, inactive, refused and missing maintenance
reads over HTTP. Active maintenance changes the typed state and MCP status to
maintenance. A failed check leaves the reported state and omits the nullable
maintenance flag, so it never asserts that maintenance is off.
