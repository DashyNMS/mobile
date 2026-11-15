# Shared fleet ranking and health fixture

Synthetic routes copied from the existing MCP estate, with example.net and
documentation addresses. `cases.json` records independent rankings and counts.
Unlisted routes return 404. Over loopback HTTP, MCP and host adapters use
separate connections and compare fields and request sets. Coverage includes
traffic, errors, device problems and directions, group filtering, a zero fan,
an unplugged optic, unknown health, and Core's default assessment. Existing
MCP tests retain independent values and permission/fallback cases. Shared-only
host tests cover customised thresholds for temperature, fan, signal and dBm,
sensor limit overrides, and capture against later mutation.
