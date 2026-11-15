# Shared Unimus config fixture

Synthetic Unimus API v2 envelopes use documentation addresses and example.net.
Tests require a synthetic bearer token and use Core's real Unimus client over
loopback HTTP. Each adapter has separate clients, caches and query logs.
The fixture covers newest-backup content fallback, config redaction, a diff,
full/summary/off modes and recent changes. Existing MCP tests retain bounded
backup lookup, older revisions, stale backups, first backups, scope and cap
expectations. No fixture contains production credentials or network data.
