# Device search fixtures

Synthetic LibreNMS `/api/v0/devices` response and ranked search cases for the
shared device search behind Intelligence's `find_device` (#596). Every name and
address is invented: `example.net` and the documentation ranges 192.0.2.0/24 and
198.51.100.0/24. No real network data.

Devices 1 to 3 repeat the ranking case in DashyNMS/mcp's
`tests/DashyNMS.Agent.Tests/DeviceSearchTests.cs` at commit
`0bdf50e9249eafd880f1a5ee3bcd66e0d81549fe` (exact `sw`, prefix `sw-prefix`,
substring `a-sw`; expected order 3, 2, 1). The rest add sysName prefixes,
devices added by IP, duplicate display names, disabled and down devices, a
secret-shaped display name and marker-shaped text.

`cases.json` gives the expected order of device ids and the exact total for
each query, written by hand from the upstream ranking rules (exact, then
starts with, then contains, over hostname, sysName, display name and IP; ties
by hostname, case-insensitive). The tests check both the mobile adapter and
the package's typed executor against them, so a ranking change upstream fails
here rather than silently changing answers.

These are a copy, not a shared bundle: no published fixture package exists yet.
Keep any change in step with upstream until one does.
