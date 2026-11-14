# Shared alert details and rules fixture

Synthetic routes copied from the existing MCP estate fixture. Names and
addresses use example.net and documentation ranges. `cases.json` records
independent expected facts. Serve routes not listed as HTTP 404, including
Core's faults probe so this fixture uses legacy alerts. Each host uses its
own connection, caches and request log. The fixed time is 2026-10-17 10:00 UTC.

Coverage includes trigger fields, previous firings, rule scopes, sorted rule
names and counts. Tests compare MCP to the host over loopback HTTP and reject
an expected source that differs from the returned alert. Existing MCP tests
retain independent expected fields for faults, histories, removed columns,
permission failures and compatibility. Runtime iOS acceptance is not proved.
