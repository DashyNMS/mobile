# Groups and locations

`devices.json` and `groups.json` are byte-identical copies of DashyNMS/mcp's
`DeviceSearch` envelopes at `a9032220aa18c3234d920f0503eba6de0348d6ba`.
`scripts/check-shared-fixtures.sh` checks their provenance in CI.

SharedGroupsAndLocationsHttpTests runs both hosts against these envelopes and
independently checks three groups, three locations and Lab annexe's four devices
and two down devices. Synthetic inline routes add location IDs and failure cases.
