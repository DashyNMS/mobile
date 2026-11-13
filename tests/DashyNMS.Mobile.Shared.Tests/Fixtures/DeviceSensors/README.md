# Shared device sensors fixture

Synthetic LibreNMS answers for one device's sensor readings, shared by DashyNMS/mcp
and DashyNMS/mobile. Both serve these files over loopback HTTP through Core's real
client and transport: MCP to `device_sensors` and `HostDeviceSensors`, mobile to
Intelligence's `device_sensors`. Every name and address is invented (`example.net`,
198.51.100.0/24).

- `devices.json`: three devices. Device 3 has no sensors.
- `sensors.json`: the network-wide list (`resources/sensors`).
  - A fan reading of 0 beside a fan with no reading (`null`): critical against its
    own low limit, and unknown with no value. Zero is a reading; missing is not.
  - Temperatures judged by Core's default thresholds, one with its own high limit
    winning.
  - A dBm reading judged by the default threshold, and one in the "nothing plugged
    in" band, which stays unknown.
  - A voltage judged by its own limits, a voltage with no limits and a state sensor
    with no limits or installed definition: both unknown, never ok.
  - Another device's sensor, which must not appear.
- `health.json`: the device's own health routes, read only after the network-wide
  list is refused (403). They list processor and storage types too, which are never
  read, and give the same nine readings.
- `cases.json`: each case's server, device, optional type, expected readings in
  order, states, what each was judged against, values, totals, types, whether the
  device-by-device route was used, and the requests each server should log. The
  device-by-device route reads concurrently, so request lists are compared as sets.
- `transport.json`: failures on the network-wide list while the device list
  answers, and on the device's health route after the list is refused. Request
  counts include Core's retries on 502 and 503.

Installed state definitions aren't covered here: they need a catalogue on disk, and
are tested in Core.

Mobile keeps a copy and checks it against this one by commit. Change both together.
