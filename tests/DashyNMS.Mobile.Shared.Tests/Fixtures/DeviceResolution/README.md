# Shared device resolution fixture

Synthetic LibreNMS answers for resolving the device a model names, shared by
DashyNMS/mcp and DashyNMS/mobile. Both serve these files over loopback HTTP
through Core's real client: MCP to its own `DeviceResolver` wiring and
`HostDeviceResolver`, mobile to Intelligence's device tools. Every name and
address is invented (`example.net`, 198.51.100.0/24, 2001:db8::/32).

- `devices.json`: the device list resolution reads, and the only request it makes.
  - 1 and 7 share the display name "Core switch 1".
  - 3 has an IPv6 address, matched however it's written.
  - 4 has no sysName, so `dist-sw4` matches on the short hostname; it is ignored.
  - 5 is named by its address and has no `ignore` flag (state Unknown).
  - 6's hostname starts with `42`, so "42", which no device has as its id,
    falls through to the short-hostname tier; it is disabled.
  - 8 has a secret in its display name, redacted in candidates.
  - 9 and 10 share the short hostname `edge-sw9` under different domains.
- `cases.json`: one case per tier (id, exact hostname, display name, sysName and
  address, short hostname, part of a name), three ambiguous names (an exact display
  name, a short hostname and part of a name), more matches than candidates listed,
  an unknown name, an id no device has, and an empty phrase.
  Each gives the outcome (`match`), the device id and reported state when found,
  the redacted candidates and omitted count when ambiguous, MCP's message for
  anything but a match, and the requests made. `code` is the host's failure
  category for a phrase that can't be resolved at all.
- `transport.json`: failures on the device list, including a 404 and an error
  envelope sent with 200. Request counts include Core's retries on 502. A refused
  list is `Refused`, never "no match".

Mobile keeps a copy and checks it against this one by commit. Change both
together.
