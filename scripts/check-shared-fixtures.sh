#!/usr/bin/env bash
# Checks the shared fixtures are byte-identical to DashyNMS/mcp's, which runs its MCP
# tools against the same files: device search (#596) for find_device, open alerts
# (#601) for active_alerts, the network overview (#600) for network_overview, the device
# summary (#602) for device_summary, device sensors (#603) for device_sensors and device
# resolution (DashyNMS/mcp#131) for every tool's device argument (#635). Both
# adapters are then held to one set of expectations from
# one response. Move a pin when upstream changes that fixture.
#
# Needs: gh (authenticated; DashyNMS/mcp is public).
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# check <fixture directory> <DashyNMS/mcp commit> <files...>
check() {
  local name="$1" commit="$2"
  shift 2
  local upstream="tests/DashyNMS.Agent.Tests/Fixtures/$name"
  local local_dir="$ROOT/tests/DashyNMS.Mobile.Shared.Tests/Fixtures/$name"
  for file in "$@"; do
    gh api -H "Accept: application/vnd.github.raw" "repos/DashyNMS/mcp/contents/$upstream/$file?ref=$commit" > "$TMP/$file"
    if ! cmp -s "$TMP/$file" "$local_dir/$file"; then
      echo "::error::Fixtures/$name/$file differs from DashyNMS/mcp@$commit"
      diff "$TMP/$file" "$local_dir/$file" || true
      exit 1
    fi
  done
  echo "$name fixture matches DashyNMS/mcp@$commit."
}

# DashyNMS/mcp#119's squash merge on main.
check DeviceSearch 57e09ca472783055c7909612ffbc1e99af5d18ab \
  devices.json cases.json groups.json group-1.json scopes.json transport.json

# DashyNMS/mcp#124's squash merge on main.
check OpenAlerts c463e92a2ec7794a6f231bf6ccfd0ed9f41f50c3 \
  devices.json faults.json alerts.json cases.json transport.json

# DashyNMS/mcp#125's squash merge on main.
check NetworkOverview 7a84405254c57a2be7e35e1f7a296f163a5a7ed0 \
  devices.json faults.json ports.json system.json

# DashyNMS/mcp#128's squash merge on main.
check DeviceSummary 59cf2b623583366abcc2785685ba362cc1b1da4b \
  devices.json device-1.json device-2.json device-3.json device-4.json device-5.json \
  device-6.json cases.json transport.json

# DashyNMS/mcp#129's squash merge on main.
check DeviceSensors 812266dd63c8201406c47282a8c45899d94abe4e \
  README.md devices.json sensors.json health.json cases.json transport.json

# DashyNMS/mcp#134's rebase merge on main (DashyNMS/mcp#131).
check DeviceResolution ce4ec6219c1212e2584524ed7ac3643720ab5635 \
  README.md devices.json cases.json transport.json
