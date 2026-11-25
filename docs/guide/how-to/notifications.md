# Choose which alerts notify you

DashyNMS checks your server for new alerts. It isn't told by the server, so a notification can arrive some time after the alert.

| | While the app is open | In the background |
| --- | --- | --- |
| iPhone and iPad | At your chosen interval | When the system allows, which can be hours apart. Opening the app often helps. |
| Android | At your chosen interval | About every 15 minutes, or later with battery saver on |

If you need to know the moment something breaks, also set up a LibreNMS alert transport on your server.

## Turn notifications on

1. Open **Settings → Notifications** and turn on **Alert notifications**.
2. Allow notifications when your phone asks. If you said no before, allow them for DashyNMS in the phone's own settings.
3. Choose **Critical alerts** and **Warnings**, and whether to hear **When an alert recovers** or **When someone acknowledges an alert**.

Alerts that were already open when you signed in don't notify you. Your own acknowledgements don't either.

## Choose the rules

Under **Which alerts notify you**, choose:

- **Every alert**;
- **Every alert except…**, to leave rules out;
- **Only these rules**, to pick the rules that matter.

Choose **+ Add a rule** to build the list. You can also use **Change** on an alert's own page. Each list is kept when you switch between them.

## Set quiet hours

1. Turn on **Quiet hours** and choose when they start and end.
2. Turn on **Still notify critical alerts** if those should come through.

## What a notification shows

Faults from one rule on one device share one notification, which updates as more appear and goes away once the alert clears. Many changes at once become one summary. Tap a notification to open its alert or group.

**Hide details on the lock screen** shows how serious an alert is but not which device. On iPhone, also check **Show Previews** in the phone's notification settings.

Sounds are set in the phone's notification settings for DashyNMS.

## Count alerts on the app icon

On iPhone and iPad, **Settings → Alert checks → Alert count on the app icon** shows how many alerts are open. Choose which severities count, and whether acknowledged ones do.
