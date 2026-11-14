# Shared device detail fixture

Synthetic LibreNMS routes derived from the existing MCP estate fixture, with
example.net and documentation addresses. `cases.json` records independent
expected facts. Unlisted endpoints return 404. Tests serve the bundle over
loopback HTTP to separate MCP and host connections and compare fields and
request sets. Covered sections are inventory, VLANs, FDB, ARP, PoE, wireless,
routing, availability and addresses, plus paging and refused inventory.
Existing MCP tests retain independent field expectations and section aliases.
A successful empty wireless list is distinct from an unavailable section.
Device runtime, presentation budgets and evidence remain Mobile checks.
