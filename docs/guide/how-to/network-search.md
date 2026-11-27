# Find where something is connected

**Network Search** finds an address, a network, a MAC address or a name and shows where it plugs in: the switch port, the routers that know it, and the network around it. It works like the Network Search page in LibreNMS 26.11.

Open **More → Network Search**, or tap the compass on the **Dashboard**. On an iPad or Mac keyboard, press **⌘F** on the Dashboard.

## Search

1. Type or paste an IP address, a network such as `192.0.2.0/24`, a MAC address in any format, or a device or host name.
2. Choose **Search** on the keyboard.

Paste a list of addresses or MACs to look them all up at once. Your last ten searches appear under the box; tap one to run it again.

## Read one result

When the search finds a monitored device, its summary uses the device-name field chosen in Settings. Its details include the IP address.

When the search finds one thing, its page leads with where it's connected:

- **Connected to** is the switch port it most likely plugs into. **Direct** means it's the only device on that port; **Shared** means it's one of several, such as behind a desk phone. For a device LibreNMS monitors, this lists discovered neighbours from either end of the connection, including the switch port for an access point or antenna whose switch reports the link. The same connection appears on Device Overview and in Neighbours.
- **Conflict** appears when one address answers from several MAC addresses, or one MAC is on several access ports at once.
- **Seen on** lists every switch port that learned it, closest first, with uplinks marked. Each dot shows the port's operational state, matching the state text below it.
- **IP addresses** and **MAC addresses** can each be copied or searched for.
- **Same subnet** lists the gateway and other monitored interfaces on its network.
- **Neighbour of** lists the routers whose ARP tables have it.

Tap a switch, router or device to open it, or the graph button for a port's traffic.

## Read a list

When several things match, or you search a network, they're listed. Tap a source chip, such as **ARP** or **MAC tables**, to hide or show what it found, and **Sort** to order them by address, name or MAC address. **+N more** shows the next page. Tap a row to see where it's connected.

Rows show MAC addresses and router names when the search finds them.

## When something's missing

The summary above the results shows what the search checked. Open **Show sources** to see each source's outcome when several are listed. A warning identifies sources that were unavailable, denied, failed or only partly checked. When that happens, a match may be missing.

## Share the results

Tap the share button by the tab bar to send the results as a CSV file. Choose how MAC addresses are written in **Settings → Devices → MAC addresses**.
