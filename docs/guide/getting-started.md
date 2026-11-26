# Getting started

## What you need

- An iPhone or iPad on iOS or iPadOS 15 or later, or an Android phone or tablet on Android 7 or later. See the install pages for [iPhone and iPad](installation/install-iphone-ipad.md) and [Android](installation/install-android.md).
- A LibreNMS server your phone can reach, over your network or a VPN.
- An account on that server that's allowed to use its API.

## Try the demo

No server yet, or want to look round first? Choose **Try the demo** on the sign-in page. DashyNMS opens on an example network, with devices, alerts, graphs, maps and Network Search to explore, and a **Demo** chip next to its name.

Intelligence in the demo is an example conversation. Its suggested questions have answers ready, worked out from the demo network, so they work on any phone. Other questions need a phone with Apple Intelligence.

The demo keeps its own settings, sends no notifications and saves nothing. Your sign-in details stay as they were. To leave, tap **Demo** or choose **Settings → Leave the demo**: you're back on the sign-in page and nothing from the demo is kept.

## Sign in with LibreNMS

1. Open DashyNMS.
2. Type your **Server address**, for example `https://librenms.example.net`.
3. Choose **Sign in with LibreNMS**.
4. Sign in on your server's own page, the way you do in a browser.

DashyNMS then creates an API token for this phone. This needs an `https` address, LibreNMS 26.4 or later and an account with API access. Otherwise, use a token.

## Sign in with an API token

1. Choose **Use an API token instead**.
2. Choose **Open the API Tokens page** to make a token on your server, then copy it. If you can't create one, ask your LibreNMS administrator.
3. Paste it under **API token**, or choose **Use the token you copied**.
4. Choose **Sign in**.

If your server uses a self-signed certificate that you trust, turn on **Allow untrusted certificate**. If it doesn't use HTTPS, DashyNMS warns you first: the token and everything the app loads would travel unencrypted.

**Remember token on this device** keeps you signed in. You can also enter a **Backup address**, another route to the same server. DashyNMS uses it when the main address doesn't answer.

## Sign in from DashyNMS on your computer

1. On the computer, open **Settings → Server** and choose **Show** under **Sign in DashyNMS Mobile**.
2. Scan the code with your phone's Camera app.
3. Open the link in DashyNMS and confirm.

The code contains the computer's API token, so keep it out of photos and shared screens. The phone and the computer then share that token, and revoking it signs both out. For one you can revoke on its own, make a separate token for the phone.

## Find your way round

- **Dashboard** is the first tab: the cards you choose, such as alerts, device status and pinned devices.
- **Devices**, **Alerts** and **Health** are pinned beside it to begin with.
- **More** is the last tab and lists every page: Monitor, Network, Logs and App.

To change the tabs, open **More** and tap the pin beside a page. Up to three pages can be pinned between Dashboard and More. Unpin one to make room. The change is saved straight away, and pages that aren't pinned still open from More.

On an iPad, in landscape and on a wide screen, lists and details sit side by side. Choose a row to see it beside the list.

Pull down on a page to refresh it. With no signal, or your VPN off, DashyNMS opens to what it last saw and marks it, for example **Offline: last updated 07:42**. It catches up once your server answers.

## Try your first everyday tasks

1. [Open an alert](how-to/alerts.md) and see why it fired.
2. [Pin a device](how-to/devices.md#pin-a-device) you check often.
3. [Choose your notifications](how-to/notifications.md) before you rely on them.
4. [Arrange your dashboard](how-to/dashboard.md).

If something doesn't work, start with [Troubleshooting](reference/troubleshooting.md).
