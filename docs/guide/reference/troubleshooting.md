# Troubleshooting

Start with the section closest to what you see. Keep the error message and the time it happened.

## Can't connect to the server

1. Open the same address in your phone's browser. If that fails too, check Wi-Fi, mobile data, your VPN and the server.
2. Check the whole address, including `https://` and any path.
3. Check that your account may use the API and that the token hasn't been revoked.
4. If **Sign in with LibreNMS** can't finish, use an [API token](../getting-started.md#sign-in-with-an-api-token).
5. For a self-signed certificate you trust, turn on **Allow untrusted certificate**.

If the page says **On backup address**, the main address didn't answer.

## It says Offline

DashyNMS is showing what it last saw. Check your connection or VPN, then pull down to refresh.

## Devices, alerts or sections are missing

1. Clear the search and filters.
2. Look at the same item in LibreNMS with the same account.
3. If a card says you don't have permission, ask your LibreNMS administrator.
4. A device without any data for a section doesn't list that section.

## Notifications don't arrive

1. Check **Settings → Notifications** for the severities, the rules and quiet hours.
2. Check that your phone allows notifications for DashyNMS and isn't in a Focus or Do Not Disturb.
3. On iPhone, check that **Background App Refresh** is on for DashyNMS. On Android, check battery optimisation.
4. Remember that [background checks](../how-to/notifications.md) can be a long time apart.

## A widget is empty or old

1. Open DashyNMS and sign in, then wait for it to check for alerts.
2. Remove the widget and add it again if it stays empty.
3. Check **Background App Refresh** (iPhone) or battery optimisation (Android).

## Times are wrong by whole hours

Change **Server stores timestamps in UTC** in **Settings → Server**. Graylog has its own time-zone setting.

## Intelligence is missing or says it's unavailable

Open **Settings → Intelligence**, which says why. It needs a device that supports Apple Intelligence, iOS 26 or later and the model downloaded. Right after turning Apple Intelligence on, the download can take some time.

## Report a problem

1. Open **Settings** and turn on **Hide names**.
2. Choose **Share diagnostics** and save the log.
3. [Open an issue](https://github.com/DashyNMS/mobile/issues) with what you did, what you expected and what happened. The phone and app versions are in the log.

Read the log before posting it, and never post your token or a sign-in code.
