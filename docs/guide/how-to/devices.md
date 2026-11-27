# Find and investigate a device

## Find a device

1. Open **Devices**.
2. Search by name, address, operating system, hardware, location, type, contact, serial number or device ID.
3. Tap **Up**, **Down**, **Maintenance** or **Disabled** to narrow the list (or open **Status** at larger text sizes or on short screens), or open **Sort & filter** for type, location, group and sort order.

Devices you opened lately are in a strip above the list.

**More → Groups & locations** lists each group and location with how many devices are down. Tap one to see its devices.

## Pin a device

Tap the pin at the right of a device's row, or pin it from its own page. Pinned devices stay at the top of Devices and on the dashboard's **Pinned devices** card.

## Work on several devices

Press and hold a device, then tick the others. Use **Pin**, **Unpin**, **Rediscover** or **Maintenance** in the bar.

## Look into a device

Tap a device to open it. Its page starts with:

- its name, state, hardware, system and address;
- 30-day availability, uptime, ports up and active alerts;
- **Rediscover**, **Maintenance**, **SSH** and **Telnet**. SSH and Telnet open in whichever app on your phone handles those links.

The cards below cover ping response, alerts, availability, resources, sensors, connections and the busiest ports. Tap a card for its full page. Depending on the device, there are also pages for:

- [ports and graphs](ports-and-graphs.md);
- neighbours, as a list and a diagram;
- VLANs, MAC addresses (FDB) and ARP;
- routing (BGP, OSPF and VRFs), wireless and inventory;
- the event log, [Graylog](integrations.md) and [config backups](integrations.md#read-and-compare-config-backups).

Pages a device has nothing for aren't listed. If your account isn't allowed to read one, its card says so rather than showing it as empty.

## Search within a page

Use the magnifier on a device's section, for example to find one port or sensor. On iOS 26 and later the buttons are in a row at the bottom or side of the screen. On earlier versions and on Android they're at the top. The search is kept after you close the keyboard, and a line says what's filtered.

Inventory opens folded. Use **Expand all** and **Collapse all**, or **Show ports** to include them.

## Change which name is shown

**Settings → Devices → Device names** chooses the hostname, the system name or another name LibreNMS holds.
