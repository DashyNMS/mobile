# Device search fixture shared with MCP

A synthetic LibreNMS `/api/v0/devices` response (`devices.json`) and what
`find_device` must make of it (`cases.json`), for the shared device search
behind Intelligence's `find_device` (#596). Both files are byte-identical copies
of DashyNMS/mcp's `tests/DashyNMS.Agent.Tests/Fixtures/DeviceSearch`, where the
MCP `find_device` tool and `HostDeviceSearch` run against them.
`scripts/check-search-fixture.sh` fails CI if these differ from the DashyNMS/mcp
commit it pins. Change them upstream first, then copy them here and move the pin.

Every name and address is invented: `example.net` and the documentation ranges
192.0.2.0/24 and 198.51.100.0/24. Never put real network data here.

`cases.json` holds:

- `requests`: the only endpoints a text search may read.
- `cases`: device ids in ranked order and the exact total for each query
  (exact, then starts with, then contains, over hostname, sysName, display name
  and IP; ties by hostname, case-insensitive).
- `states`: the shared status word for chosen devices, and the state LibreNMS
  actually reported. Device 11 reports no state: the shared summary says `down`,
  where mobile shows `Unknown`.
- `failures`: the HTTP status of a failed device list and the
  `DeviceSearchFailure` the host search maps it to. The category is the host's
  contract: the MCP tool returns its own sentence per status instead, so the
  shared part is that both fail without data after reading only `requests`.
  Mobile says "refused" for `Refused` and its usual failure line for the rest.

Devices 1 to 3 repeat the ranking case in DashyNMS/mcp's `DeviceSearchTests`.
The rest add sysName prefixes, devices added by IP, duplicate display names,
disabled, down and unreported devices, a secret-shaped display name and
marker-shaped text. Every device has a location for the scope cases; location
doesn't affect text ranking.

The other files are also copies of DashyNMS/mcp's:

- `groups.json` and `group-1.json`: the device groups and one group's members.
  Group 3 is empty, and LibreNMS answers it with a 404.
- `scopes.json`: routes and cases for group, location and status scopes, an
  empty group, ambiguous and missing names and an invalid status, each with the
  requests it may read.
- `transport.json`: failed device lists served over HTTP: 401, 403, 404, 500,
  502 and 503 with error envelopes or HTML, a 200 login page, a 200 error
  envelope, a 200 whose `devices` isn't a list, a request that's never answered
  (5-second timeout) and an address with nothing listening. Request counts
  include Core's retries (502 and 503 are tried four times).

`SharedDeviceSearchTests` serves `devices.json` through Core's real device API
over a fake transport to both the package's own executor and mobile's
`find_device`. It checks each case, status word, request and failure category
against `cases.json`.

`SharedDeviceSearchHttpTests` serves the files over loopback HTTP, so Core's
transport does the parsing, retrying and timing out itself. It covers:

- **Text cases and transport failures:** mobile's `find_device`, through the
  app's per-chat transports, and the package's `HostDeviceSearch`, over Core's
  real client, return the same ids and totals, and each server must log the
  same requests. Only `HostDeviceSearch` is held to the exact failure category.
  Mobile says "refused" for `Refused` and its usual failure line for the rest,
  with no evidence and no server text.
- **Scope cases:** these go through the package's executor on mobile's Core pin
  only. Mobile's `find_device` takes search text alone. A separate test checks
  that text naming a group or a location reads only the device list.

DashyNMS/mcp's `SharedSearchHttpFixtureTests` serves the same files to the MCP
server's own services and `find_device` tool. There too the category is checked
on `HostDeviceSearch` only; the MCP reply is checked for an error with no data
and nothing secret-shaped.

## What this fixture does not prove

These are not compared across adapters here, and parity on them should not be
claimed from it:

- Scopes in mobile itself. Mobile has no group, location or status search yet
  (#608). The scope cases only show that the package agrees with MCP on mobile's
  Core pin.
- MAC search over ARP, paging past the first page and the verbose record.
  Mobile refuses MAC search before any request (#608).
- Admin-only refusals, TLS and certificates, backup-address failover,
  cancellation and a retired connection. Mobile's own tests cover its side of
  these against other inputs.
- Faults (alerts) and other tools. `find_device` reads no alerts, and mobile's
  alert lookups are still its own implementation (#601), so nothing here
  compares fault identities, severity or grouping.
