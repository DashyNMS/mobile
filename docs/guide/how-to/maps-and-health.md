# Use maps, neighbours and health readings

## See your sites on a map

Open **More → Map**. Each LibreNMS location with coordinates has a pin with its device count, red if any device there is down. Tap a pin, then **Show in Devices**. Devices without coordinates are counted rather than left out.

The map's background comes from OpenStreetMap, so it needs an internet connection.

## See what's connected

Open **More → Network map** for a diagram of devices and the links between them:

- Pinch to zoom, and drag to move round.
- Use **Find a device** to jump to one. Tap it for its details, **Open** or **Neighbours**.
- Drag a device to move it. **Reset layout** puts everything back.
- **Unlinked** lists devices with no known links.

## Check links between devices

Open **More → Neighbours** for every link LibreNMS discovered with CDP or LLDP. Links that are down, or whose device is down, come first. Search by name, port or platform, and tap a link for both ends.

**Neighbourhoods** group neighbours by the name, description, port or protocol they announce, or the switch port they use. Use **Add** to make one.

## Check sensor readings

Open **Health** for optical power, wireless signal, temperature and fan speed across the network, problems first. Use **Critical**, **Warning** and **OK**, or search. Tap a sensor to open its device's sensors.

Colours follow **Settings → Health thresholds** and the limits set in LibreNMS. A sensor with no limits has no colour, and one with no reading shows a dash.

State sensors, such as fans and power supplies, show a name like **Ready** or **Fan failed** when [sensor definitions](settings.md#sensor-definitions) know it.
