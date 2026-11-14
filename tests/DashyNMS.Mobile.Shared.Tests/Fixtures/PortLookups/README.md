# Shared port and topology fixture

Synthetic LibreNMS routes, using example.net and documentation addresses.
`cases.json` records independent expectations. Unlisted routes return 404.
Each adapter gets its own connection and request log. Tests compare complete
port, neighbour and trace fields over loopback HTTP, and compare request sets.
The fixture covers a down edge port, an uplink, an unmonitored camera, reverse
neighbours and stored ARP/FDB observations. It never proves address ownership.
Existing MCP fixtures retain ambiguity, multiple MACs, busy uplinks, caps and
invalid-address coverage. Host tests also cover failure categories and retirement.
