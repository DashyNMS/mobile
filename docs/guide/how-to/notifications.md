# Choose which alerts notify you

DashyNMS checks your server for new alerts. It isn't told by the server, so a notification can arrive some time after the alert.

| | While the app is open | In the background |
| --- | --- | --- |
| iPhone and iPad | At your chosen interval | When the system allows, which can be hours apart. Opening the app often helps. |
| Android | At your chosen interval | About every 15 minutes, or later with battery saver on |

If you need to know the moment something breaks, also set up a LibreNMS alert transport on your server.

## Turn notifications on

DashyNMS asks for permission after your first sign-in. Choose **Allow**. If you said no, allow notifications for DashyNMS in the phone's own settings.

Then open **Settings → Notifications**:

- **Alert notifications** turns them all on or off. They're on to begin with.
- **Critical alerts**, **Warnings** and **When an alert recovers** are on to begin with.
- **When someone acknowledges an alert** is off.

Alerts that were already open when you signed in don't notify you. Your own acknowledgements don't either.

## Choose the rules

Under **Which alerts notify you**, choose:

- **Every alert**;
- **Every alert except…**, to leave rules out;
- **Only these rules**, to pick the rules that matter.

Choose **+ Add a rule** to build the list. You can also use **Change** on an alert's own page. Each list is kept when you switch between them.

## Set quiet hours

1. Turn on **Quiet hours** and choose when they start and end. They run from 22:00 to 07:00 unless you change them.
2. Leave **Still notify critical alerts** on if those should come through.

## What a notification shows

Faults from one rule on one device share one notification, which updates as more appear and goes away once the alert clears. Many changes at once become one summary. Tap a notification to open its alert or group.

On Android, **Hide details on the lock screen** shows how serious an alert is but not which device. On iPhone, set **Show Previews** to **When Unlocked** or **Never** in the phone's notification settings for DashyNMS.

Sounds are set in the phone's notification settings for DashyNMS.

## Count alerts on the app icon

On iPhone and iPad, **Settings → Alert checks → Alert count on the app icon** shows how many alerts are open. Choose which severities count, and whether acknowledged ones do.
