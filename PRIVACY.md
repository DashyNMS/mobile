# Privacy

DashyNMS Mobile talks to the LibreNMS server you sign in to, and to Graylog and Unimus if you set them up. There's no account, no advertising and no tracking, and nothing about your network is ever sent to DashyNMS.

A few things leave your phone only in the ways described here:

- **Error reports and usage, only if you choose.** After you sign in, the app asks once whether to share error reports, and separately usage and performance. Both start off. If you turn one on, it's sent to DashyNMS through [Sentry](https://sentry.io):
  - error reports: the app version, the phone's operating system and its version, the processor type, and where in the app's own code an error happened
  - usage and performance: how often the app starts, which features are used, how long start-up, pages and requests take, how responsive it is, how much memory it uses, and - if you share both - how each time it was open ended, under a random ID that's new each time
  - never your server's address, device or other names, IP addresses on your network, tokens or passwords, readings, logs, or anything you type, and no user or install ID
  - Sentry sees your phone's IP address when something is sent, as any website would, so reports aren't called anonymous. Sentry keeps them for at most 90 days.
  - turn either off in Settings › Diagnostics › Error reports and usage, and anything not yet sent is deleted
- **Sensor definitions,** if you use them, are downloaded from GitHub (the official repository, or one you choose). Nothing about you or your network is sent.
- **Map backgrounds** come from OpenStreetMap's tile servers, which see which parts of the map are loaded, as for any map website.

The full privacy policy is on the website: **<https://dashynms.pckp.net/mobile/privacy/>**. It's also linked in the app under Settings → About.

The website is the one official copy; this file is a summary of it.
