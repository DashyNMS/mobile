# Device search fixtures

A synthetic LibreNMS `/api/v0/devices` response (`devices.json`) and what
`find_device` must make of it (`cases.json`), for the shared device search
behind Intelligence's `find_device` (#596). Every name and address is invented:
`example.net` and the documentation ranges 192.0.2.0/24 and 198.51.100.0/24.
No real network data.

Both files are byte-identical copies of DashyNMS/mcp's
`tests/DashyNMS.Agent.Tests/Fixtures/DeviceSearch`, where the MCP `find_device`
tool and `HostDeviceSearch` run against them. `scripts/check-search-fixture.sh`
fails CI if these differ from the commit it pins. Change them upstream first,
then copy them here and move the pin.

`cases.json` holds:

- `requests`: the only endpoints a text search may read.
- `cases`: device ids in ranked order and the exact total for each query
  (exact, then starts with, then contains, over hostname, sysName, display name
  and IP; ties by hostname, case-insensitive).
- `states`: the shared summary's status word and the state LibreNMS reported.
  Device 11 reports no state: the summary says `down`, mobile shows `Unknown`.
- `failures`: the HTTP status of a failed device list and the
  `DeviceSearchFailure` it maps to. Mobile says "refused" for `Refused` and its
  usual failure line for the rest.

Devices 1 to 3 repeat the ranking case in DashyNMS/mcp's `DeviceSearchTests`.
