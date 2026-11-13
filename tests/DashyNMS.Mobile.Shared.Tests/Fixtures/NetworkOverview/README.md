# Shared network overview fixtures

Synthetic transport responses used by MCP and mobile. Three processor faults on
one device/rule are one alert group. Devices: four total, three up, one down;
the down device is 2 at Core site. One live port is down while enabled and counts
errors; the disabled port is excluded. Version: 26.10.0.

Run both the complete case and a refused ports source. The latter retains device,
alert and version data, with null port counts and a fixed Refused category. Copies
in mobile must be byte-identical and checked against the package source commit.
