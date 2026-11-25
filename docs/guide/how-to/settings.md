# Change your settings

Open **More → Settings**. Each row shows its current choice.

## Server

- See the address and LibreNMS version, and whether the connection uses HTTPS.
- Add or change a **Backup address**, another route to the same server.
- Turn on **Server stores timestamps in UTC** only if alert times are out by your time zone's offset.

To sign in to a different server, sign out first.

## Tabs and appearance

- Choose the tabs in **More**. See [Find your way round](../getting-started.md#find-your-way-round).
- **Appearance** follows the phone, or stays light or dark.
- **Devices** chooses which name devices go by and how many recently viewed ones to keep.

## Alert checks and notifications

- **Alert checks** sets how often DashyNMS checks while it's open, from 30 seconds to 15 minutes, and the count on the app icon.
- **Notifications** decides [which alerts notify you](notifications.md).
- **Lock screen widgets** decides [what a locked phone shows](widgets.md#hide-details-on-the-lock-screen).

## Health thresholds

Set the warning and critical limits for optical power, signal, temperature and fan speed. A sensor's own limits in LibreNMS win for the bounds they set, unless **Always use these thresholds** is on. Choose **Save** to apply them.

## Sensor definitions

These give state sensors names and colours, such as **Ready** or **Fan failed**. They come from a public catalogue on GitHub, checked at most once a day, and keep working offline. Turn them off, or choose **Use my own repository**.

## Diagnostics

- **Share diagnostics** makes a log to attach to a bug report. Turn on **Hide names** first to replace names and addresses with placeholders. It never includes your token or passwords.
- **Error reports and usage** is off unless you choose to share. See [Privacy and your data](../reference/your-data.md#error-reports-and-usage).

## Sign out

- **Sign out** stops notifications and removes the sign-in and saved chats.
- **Sign out and forget everything** also removes your settings, dashboard and everything else DashyNMS saved on this phone.
