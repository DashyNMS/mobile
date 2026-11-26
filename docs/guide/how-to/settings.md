# Change your settings

Open **More → Settings**. Each row shows its current choice.

## Server

- See the address and LibreNMS version, and whether the connection uses HTTPS.
- See whether the app is on the backup address, and switch back to the main one. You set a **Backup address** when you sign in; to change it, sign out and sign in again.
- Turn on **Server stores timestamps in UTC** only if alert times are out by your time zone's offset.

To sign in to a different server, sign out first.

## Tabs and appearance

- Choose the tabs in **More**. See [Find your way round](../getting-started.md#find-your-way-round).
- **Appearance** follows the phone, or stays light or dark.
- **Devices** chooses which name devices go by, how many recently viewed ones to keep, and how [Network Search](network-search.md) writes MAC addresses.

## Alert checks and notifications

- **Alert checks** sets how often DashyNMS checks while it's open, from 30 seconds to 15 minutes (1 minute to begin with), and the count on the app icon.
- **Notifications** decides [which alerts notify you](notifications.md).
- **Lock screen widgets** decides [what a locked phone shows](widgets.md#hide-details-on-the-lock-screen).

## Health thresholds

Set the warning and critical limits for optical power, signal, temperature and fan speed. A sensor's own limits in LibreNMS win for the bounds they set, unless **Always use these thresholds** is on. Choose **Save** to apply them.

## Sensor definitions

These give state sensors names and colours, such as **Ready** or **Fan failed**. They come from a public catalogue on GitHub, checked at most once a day, and keep working offline. Turn them off, or choose **Use my own repository**.

## Help

**Help** is this guide, kept in the app so it works with no signal. The line at the foot of the contents says which version it describes. Tap the magnifier to search it. Other pages link to the part that explains them, such as **Help with alerts** on **Alerts**.

## Diagnostics

- **Share diagnostics** makes a log to attach to a bug report. Turn on **Hide names** first to replace names and addresses with placeholders. It never includes your token or passwords.
- **Error reports and usage** is off unless you choose to share. See [Privacy and your data](../reference/your-data.md#error-reports-and-usage).

## Sign out

Choose **Sign out**, then one of:

- **Sign out** stops notifications and removes your sign-in, pins, recently viewed devices and saved chats. Your settings, dashboard and server address stay.
- **Sign out and forget everything** also removes your settings, dashboard, servers, and Graylog and Unimus connections. Only the diagnostics log is kept, for bug reports.
