#!/usr/bin/env bash
# Checks the device search fixture (#596) is byte-identical to DashyNMS/mcp's, which
# runs its MCP find_device tool against the same files. Both adapters are then held to
# one set of expectations from one response. Move the pin when upstream changes them.
#
# Needs: gh (authenticated; DashyNMS/mcp is public).
set -euo pipefail

# DashyNMS/mcp main commit holding the fixture: the squash merge of DashyNMS/mcp#115.
FIXTURE_COMMIT="9051820a5136cbbda22bb7921ac570adc99a0a00"
UPSTREAM="tests/DashyNMS.Agent.Tests/Fixtures/DeviceSearch"

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
LOCAL="$ROOT/tests/DashyNMS.Mobile.Shared.Tests/Fixtures/DeviceSearch"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

for file in devices.json cases.json; do
  gh api -H "Accept: application/vnd.github.raw" "repos/DashyNMS/mcp/contents/$UPSTREAM/$file?ref=$FIXTURE_COMMIT" > "$TMP/$file"
  if ! cmp -s "$TMP/$file" "$LOCAL/$file"; then
    echo "::error::Fixtures/DeviceSearch/$file differs from DashyNMS/mcp@$FIXTURE_COMMIT"
    diff "$TMP/$file" "$LOCAL/$file" || true
    exit 1
  fi
done
echo "Device search fixture matches DashyNMS/mcp@$FIXTURE_COMMIT."
