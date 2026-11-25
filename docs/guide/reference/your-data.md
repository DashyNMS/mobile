# Privacy and your data

DashyNMS Mobile talks to your LibreNMS server, and to Graylog and Unimus if you set them up. There's no DashyNMS account, no advertising and no tracking. This page is a summary; the [privacy policy](https://dashynms.pckp.net/mobile/privacy/) is the full version.

## What's kept on your phone

- Your API token, and your Graylog and Unimus passwords, kept securely by the phone. Turn off **Remember token on this device** when you sign in if you don't want the API token kept.
- Your settings, dashboard and pins.
- What the app last saw, for using it offline and for widgets. Widgets never hold the token.
- Saved Intelligence chats, encrypted.
- Sensor definitions, if you use them.

## What leaves your phone

- Requests to your own servers.
- Map areas you look at, to OpenStreetMap.
- A request to GitHub for sensor definitions, at most once a day. Nothing about you or your network is in it.
- A request to GitHub for this guide's public pages, when you open **Help**. It asks for files by name, and nothing about you or your network is in it. Once the app has a copy, it doesn't look again for 30 days. Searching help stays on your phone.
- Error reports and usage, only if you choose.

## Error reports and usage

After you sign in, DashyNMS asks once whether to share error reports, and separately usage and performance. Both start off. Change them later in **Settings → Error reports and usage**.

What you share goes to DashyNMS through Sentry. It never includes your server, device names, addresses, tokens, readings, logs or anything you type. Sentry sees your phone's IP address when something is sent, so reports aren't anonymous, and keeps them for at most 90 days. Turn either off and anything not yet sent is deleted.

## Intelligence

Answers are worked out on the device by Apple Intelligence. Nothing is sent to DashyNMS or to an AI service. Reporting an answer opens an email for you to check and send yourself.

## Remove your data

**Sign out and forget everything** in **Settings** removes what DashyNMS saved on this phone, except its diagnostics log. Deleting the app removes that too. To cancel the API token, revoke it in LibreNMS. If you signed in with a computer's code, that computer uses the same token.
