#!/usr/bin/env bash
# Checks the device search fixture (#596) is byte-identical to DashyNMS/mcp's, which
# runs its MCP find_device tool against the same files. Both adapters are then held to
# one set of expectations from one response. Move the pin when upstream changes them.
#
# Needs: gh (authenticated; DashyNMS/mcp is public).
set -euo pipefail

# DashyNMS/mcp commit holding the fixture: DashyNMS/mcp#117's head. Move this to that
# PR's squash merge on main once it lands.
FIXTURE_COMMIT="d7d0ca8c44b1010d4ba34e0f45fe2bf103cffd34"
UPSTREAM="tests/DashyNMS.Agent.Tests/Fixtures/DeviceSearch"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOCAL="$ROOT/tests/DashyNMS.Mobile.Shared.Tests/Fixtures/DeviceSearch"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

for file in devices.json cases.json groups.json group-1.json scopes.json transport.json; do
  gh api -H "Accept: application/vnd.github.raw" "repos/DashyNMS/mcp/contents/$UPSTREAM/$file?ref=$FIXTURE_COMMIT" > "$TMP/$file"
  if ! cmp -s "$TMP/$file" "$LOCAL/$file"; then
    echo "::error::Fixtures/DeviceSearch/$file differs from DashyNMS/mcp@$FIXTURE_COMMIT"
    diff "$TMP/$file" "$LOCAL/$file" || true
    exit 1
  fi
done
echo "Device search fixture matches DashyNMS/mcp@$FIXTURE_COMMIT."
