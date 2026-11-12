# Shared open alerts fixture

Synthetic LibreNMS answers for the alerts open now, shared by DashyNMS/mcp and
DashyNMS/mobile. Both serve these files over loopback HTTP through Core's real
client: MCP to `active_alerts` and `HostOpenAlerts`, mobile to Intelligence's
`active_alerts`. Every name and address is invented (`example.net`, 198.51.100.0/24).

- `devices.json`: four devices at two locations.
- `faults.json`: a LibreNMS 26.10 server.
  - Eight processor faults on one rule, two of them acknowledged: one partly
    acknowledged group of eight faults.
  - Fault 12, a device-down fault. A legacy alert 12 in `alerts.json` is a
    different thing, so the two must keep different identities (`fault:12`,
    `alert:12`).
  - Three port faults, one recovered (state 0). The fixture server ignores
    Core's `state` filter, so the operation's own filter must drop it: the group
    has two faults, not three.
  - A fully acknowledged single fault, and a group whose only fault has
    recovered, which must not appear.
- `alerts.json`: an older server (no `/faults` route) with alerts 12 and 13.
- `cases.json`: each case's server, arguments and expected groups in order,
  representative identities, fault and acknowledged counts, each group's fault
  ids in order, states, totals and
  the requests each server should log. `requests` gives the default per server;
  a case may override it.
- `transport.json`: failures on the alert routes while the device list answers.
  Request counts include Core's `/faults` probe, its fallback to `/alerts` on a
  404 and its retries on 502 and 503.

These files are byte-identical copies of DashyNMS/mcp's
`tests/DashyNMS.Agent.Tests/Fixtures/OpenAlerts`. `scripts/check-shared-fixtures.sh`
fails CI if they differ from the commit it pins. Change them upstream first,
then copy them here and move the pin. This README is mobile's own.
