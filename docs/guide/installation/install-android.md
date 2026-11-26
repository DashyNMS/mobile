# Install on Android

DashyNMS Mobile runs on Android 7 or later. It isn't on Google Play yet.

## Install a test build

Test builds change often and may not work. To try one:

1. Open the [releases page](https://github.com/DashyNMS/mobile/releases) and choose a nightly build.
2. Download its APK file on your phone and open it. Android may ask you to allow installs from your browser.
3. Open DashyNMS and follow [Getting started](../getting-started.md) to sign in.

Each test build updates the one before it. An app installed from Google Play later is signed differently, so you'll need to remove the test build first.

## What's different on Android

- Back, search and filter buttons are at the top of the page instead of in a row at the bottom.
- The number of open alerts comes from Android's own notification dot or count. Your launcher decides how it looks.
- Sounds and pop-ups for critical alerts, warnings and updates are three separate channels in the phone's notification settings for DashyNMS.
- [Intelligence](../how-to/intelligence.md) isn't available on Android in 2.0.

## Remove

In **Settings**, choose **Sign out**, then **Sign out and forget everything**, then uninstall the app. To cancel the token itself, revoke it in LibreNMS.
