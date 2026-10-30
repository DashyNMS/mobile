# 2.0.0

## New
- **Alert groups.** On LibreNMS 26.10, faults are grouped by device and rule, with each fault's details and history. {alerts}
- **Event log and Graylog cards.** The newest entries on the dashboard; tap one to open it. {dashboard}
- **State sensor names.** Fans and power supplies read "Ready" or "Fan failed", from the sensor definitions catalogue. {health}
- **Auto-refresh.** Logs and Graylog can refresh themselves while they're open. {logs}
- **Config backups.** With Unimus set up in Settings, each device lists its backups. Read or search one, compare any two, and share either. {devices}

## Improved
- **Logs.** Entries sit under a heading for each day, with the time at the right. {logs}
- **Edit dashboard.** Tick several cards to add them together, add one below another, or duplicate a card with its set-up. Suggested picks out cards that fit your set-up. {dashboard}
- **Notifications.** One per alert group, and a tap opens the alert or group it was for. {alerts}
- **Slow sensor lists.** The last readings stay on screen, with their time, while LibreNMS answers. {health}
- **Graylog.** Messages from a short host name link to their device. {graylog}
- **About.** Lists the app, Core and sensor definition versions, ready to copy into a bug report. {settings}

## Fixed
- **Devices with no ports.** Show the devices that see them, rather than an error. {neighbours}
- **Missing readings.** A sensor with no reading shows a dash and no colour. {health}
