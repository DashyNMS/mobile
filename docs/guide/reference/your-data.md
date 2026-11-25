# Privacy and your data

DashyNMS Mobile talks to your LibreNMS server, and to Graylog and Unimus if you set them up. There's no DashyNMS account, no advertising and no tracking. This page is a summary; the [privacy policy](https://dashynms.pckp.net/mobile/privacy/) is the full version.

## What's kept on your phone

- Your API token, and your Graylog and Unimus passwords, in the phone's keychain or keystore.
- Your settings, dashboard and pins.
- What the app last saw, for using it offline and for widgets. Widgets never hold the token.
- Saved Intelligence chats, encrypted.
- Sensor definitions, if you use them.

## What leaves your phone

- Requests to your own servers.
- Map areas you look at, to OpenStreetMap or the tile server in your settings.
- A request to GitHub for sensor definitions, at most once a day. Nothing about you or your network is in it.
- Error reports and usage, only if you choose.

## Error reports and usage

Both are off until you turn them on in **Settings → Error reports and usage**. They go to DashyNMS through Sentry and never include your server, device names, addresses, tokens, readings, logs or anything you type. Turn either off and anything not yet sent is deleted.

## Intelligence

Answers are worked out on the device by Apple Intelligence. Nothing is sent to DashyNMS or to an AI service. Reporting an answer opens an email for you to check and send yourself.

## Remove your data

**Sign out and forget everything** in **Settings** removes what DashyNMS saved on this phone. To cancel the API token, revoke it in LibreNMS. If you signed in with a computer's code, that computer uses the same token.
