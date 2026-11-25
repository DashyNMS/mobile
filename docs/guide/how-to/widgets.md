# Add home screen and lock screen widgets

Widgets show what the app's last alert check found, so you can see problems without opening it. They hold a copy of that result, never your API token.

## Add one

1. Press and hold an empty part of your home screen and choose to add a widget. On iPhone it's **Edit → Add Widget**.
2. Search for **DashyNMS**.
3. Choose a widget and a size.

| Widget | What it shows |
| --- | --- |
| **Alerts** | The worst alert, or the critical and warning counts beside the three worst |
| **Alert pie chart** | Every device once, by its worst alert |
| **Overview** | Devices up, down and disabled, alert counts and the top alerts |
| Lock screen (iPhone) | Alert counts, in a circle or a rectangle |

Tap an alert or device on a widget to open it. Tap anywhere else for the alert list. On Android, a widget shows more as you make it bigger.

## Hide details on the lock screen

**Settings → Lock screen widgets → Hide alert details** is on to begin with, so a locked iPhone shows only counts. Turn it off to show device and rule names.

## When a widget looks out of date

A widget changes when DashyNMS checks for alerts. In the background, your phone decides how often that is. Open the app to check straight away. If a widget stays empty, see [Troubleshooting](../reference/troubleshooting.md#a-widget-is-empty-or-old).
