# 2.0.0

## New
- **Chat Spotlight.** Keep Intelligence on chosen devices or objects, and start a new chat from a device, alert, sensor or port. {intelligence}
- **Conversation export.** Share a chat as Markdown or PDF, with graph images and clear notes on unfinished answers. {intelligence}
- **Offline.** With no signal or the VPN down, the app opens to what it last saw, marked "Offline: last updated 07:42", and catches up as soon as the server answers. {dashboard}
- **Sign in from desktop.** Scan the QR code DashyNMS shows on your computer with the camera. The app asks first, then signs in as that computer does. {settings}
- **Alert groups.** On LibreNMS 26.10, faults are grouped by device and rule, with each fault's details and history. {alerts}
- **Event log and Graylog cards.** The newest entries on the dashboard; tap one to open it. {dashboard}
- **Spotlight.** Keep an Event log or Graylog card to some devices, a group or a location, as on desktop. {dashboard}
- **State sensor names.** Fans and power supplies read "Ready" or "Fan failed", from the sensor definitions catalogue. {health}
- **Auto-refresh.** Logs and Graylog can refresh themselves while they're open. {logs}
- **Config backups.** With Unimus set up in Settings, each device lists its backups. Read or search one, compare any two, and share either. {devices}
- **Help.** The user guide is in Settings, with search, and works with no signal. Alerts, Intelligence, sign-in and Settings link to the page that explains them. {settings}
- **Network Search.** Find where an address, network, MAC address or name is connected, from More or the Dashboard's compass, as in LibreNMS 26.11: the switch port it plugs into, the routers that know it, conflicts, and which sources couldn't be checked. Results can be shared as CSV. {devices}
- **Demo.** Try the app on an example network with no server: Try the demo on the sign-in page. It keeps nothing, sends no notifications, and leaves your sign-in as it was. Intelligence has an example conversation there too, with suggested questions that answer on any phone. {dashboard}

## Improved
- **Intelligence.** Four starter questions at a time and clearer lookup activity, matching desktop. {intelligence}
- **Network overview.** Intelligence counts alert groups and faults separately, includes port totals and the LibreNMS version, and keeps available data when a source cannot be read. {alerts}
- **Logs.** Entries sit under a heading for each day, with the time at the right. {logs}
- **Edit dashboard.** Cards follow the dashboard's columns on wider displays, with more room for titles and Move earlier/later in the card menu. Tick several cards to add them together, add one after another, or duplicate a card with its set-up. Suggested picks out cards that fit your set-up. {dashboard}
- **Notifications.** One per alert group, and a tap opens the alert or group it was for. {alerts}
- **Slow sensor lists.** The last readings stay on screen, with their time, while LibreNMS answers. {health}
- **Graylog.** Messages from a short host name link to their device. {graylog}
- **About.** Lists the app, Core and sensor definition versions. Share diagnostics carries them for a bug report. {settings}
- **iPhone layout.** Lists, device details and settings scroll full height behind the Liquid Glass tab bar and fixed actions, with consistent button placement across portrait, landscape and foldable displays, clearer contrast and VoiceOver labels, and row access to Ask Intelligence via swipe, touch-and-hold and VoiceOver. {dashboard}

## Fixed
- **MAC addresses on LibreNMS 26.11.** A device's MAC table shows each address's VLAN number again, for newer servers and older ones. {devices}
- **Sensors with no limits.** A custom sensor with no limits shows its reading without a colour on the dashboard, in the sensor picker and on the widget, rather than green for OK. {dashboard}
- **Device states in Intelligence.** A device LibreNMS didn't report a state for, or one it ignores, is no longer called up or down, and network counts list them separately. {intelligence}
- **Alert rules on LibreNMS 26.11.** Rules that test a field 26.11 removed carry a warning saying what to use instead. {alerts}
- **Edit dashboard.** Set up opens Spotlight for log cards. The introduction stays clear of the heading fade, and Add a card scrolls above Undo. {dashboard}
- **Devices with no ports.** Show the devices that see them, rather than an error. {neighbours}
- **Missing readings.** A sensor with no reading shows a dash and no colour. {health}
- **Health totals and maps.** Health preserves dBm casing and lists sensors without limits separately in its summary; the map opens in the active theme with zoom controls clear of the header. {health}
- **Device states in widgets and the network map.** Pinned-device widgets and the network map details panel now use the same state names as the device list, including Maintenance and Unknown. {devices}
