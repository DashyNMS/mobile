# MCP's JSON for the shared fixtures

What DashyNMS/mcp's own tools return for the shared synthetic fixtures, recorded
from MCP at `ce4ec6219c1212e2584524ed7ac3643720ab5635` by a temporary test that
ran each tool through the MCP server's own wiring over loopback HTTP. Every name
and address is the fixtures' invented ones.

| File | Tool and arguments | Fixture |
| --- | --- | --- |
| `find_device.json` | `find_device` `query: "sw"` | `DeviceSearch`, `scopes.json` routes |
| `find_device-verbose.json` | the same with `verbose: true` | the same |
| `active_alerts.json` | `active_alerts`, no arguments | `OpenAlerts`, the 26.10 server |
| `active_alerts-verbose.json` | the same with `verbose: true` | the same |
| `network_overview.json` | `network_overview` | `NetworkOverview` |
| `device_health.json` | `device_health` `device: "1"` | `DeviceSummary` |
| `device_sensors.json` | `device_sensors` `device: "1"` | `DeviceSensors`, the fleet server |

`IntelligenceFactsTests` runs mobile's lookup over the same fixture with the
same arguments and fails on any field here that its text neither shows nor
lists under "Facts left out" in docs/INTELLIGENCE.md (#636). `age_minutes` was
measured when recorded, so it is compared as a written length, not a value.

Record again when MCP's output for these tools changes.
