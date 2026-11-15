# Shared graph fixture

Synthetic device, port, sensor and graph listings use example.net and
documentation addresses. The fixture includes a small PNG and SVG responses
for the existing permission probe. HTTP tests compare MCP and host metadata,
image bytes and request sets. A separate denied SVG tests HTTP 200 permission
errors; an oversized encoded image tests Core's 2 MiB image boundary before
decoding. Existing MCP tests retain aliases, spans, ambiguity, listing caps
and per-sensor selection expectations. Pixels remain unredacted.

`expected-mcp.json` contains independent, fixed text payload expectations for
device traffic, explicit CPU, port errors and a temperature sensor. These
protect the MCP contract even when both adapters use the same operation.
