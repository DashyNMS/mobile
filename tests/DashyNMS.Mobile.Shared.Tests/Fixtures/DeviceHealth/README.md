# Device health fields

The JSON files are copied byte for byte from DashyNMS/mcp's DeviceSummary fixture
at the Batch 5 package commit. They independently specify the shared device and
location fields. The mobile fixture path now names the complete device_health tool.

SharedDeviceHealthHttpTests supplies additional synthetic health routes. It checks
complete resource values, each failed source, and listed-device fallback. The old
summary case for a listed device missing from its fresh response now expects an
explicit unavailable notice and the listed device, rather than a whole-read failure.
The upstream fixture's original summary-specific error metadata stays unchanged.
