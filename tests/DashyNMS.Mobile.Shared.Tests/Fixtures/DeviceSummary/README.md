# Shared device summary fixture

Synthetic LibreNMS answers for one device's summary, shared by DashyNMS/mcp and
DashyNMS/mobile. Both serve these files over loopback HTTP through Core's real
client: MCP to `device_health` and `HostDeviceSummary`, mobile to Intelligence's
`device_summary`. Every name and address is invented (`example.net`, 198.51.100.0/24).

- `devices.json`: the device list `device_health` resolves from. Device 6 is listed
  but `devices/6` returns no device.
- `device-1.json` to `device-6.json`: `devices/{id}` answers.
  - 1: up, its location sent as the whole JSON record (`{"id": 3, "location": ...}`),
    as `devices/{id}` can send it. Only the name is shown.
  - 2: down, a display name that repeats the hostname (dropped) and zero uptime
    (reported as unknown, not as just rebooted).
  - 3: no `ignore` flag, so its state is Unknown although MCP's status word says up;
    no location.
  - 4: ignored, a secret in the hardware text (redacted) and a location name with
    spaces around it: the shown name is trimmed, the filter name is exact.
  - 5: disabled, its location id only inside the location record.
  - 6: an empty device list: not found.
- `cases.json`: each case's device, the requests the host summary should log and
  the values it and `device_health` should give, or the host's failure category and
  whether `device_health` answers from the list (`"mcp": "list"`) or fails. Device 42
  is on neither the list nor the server.
- `transport.json`: failures on `devices/1` while the device list answers. Request
  counts include Core's retries on 502 and 503. `device_health` falls back to the list
  row on every one; the host summary fails with the category given.

The location's id, shown name and filter name are on the host result only;
`device_health`'s JSON doesn't carry them.

These files are byte-identical copies of DashyNMS/mcp's
`tests/DashyNMS.Agent.Tests/Fixtures/DeviceSummary`. `scripts/check-shared-fixtures.sh`
fails CI if they differ from the commit it pins. Change them upstream first,
then copy them here and move the pin. This README is mobile's own.
