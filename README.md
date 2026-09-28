# DashyNMS Mobile

A cross-platform (Android and iOS) companion to [DashyNMS desktop](https://github.com/DashyNMS/desktop)
for keeping an eye on your [LibreNMS](https://www.librenms.org/) network from
your phone. It's built with .NET MAUI and runs on **the same Core library as
the desktop app**, so API handling, models, alert logic and fixes are shared
rather than rewritten.

> **Status: early.** Sign-in, a dashboard, devices, device detail, open alerts
> (with acknowledge/unacknowledge), alert notifications and settings are in place. See the
> [roadmap](#roadmap) for what's next.

## What it does today

- **Sign in** with your server address and API token, the same fields as desktop,
  including the optional backup address and self-signed-certificate switch.
  The token is kept in the platform keychain/keystore, and you're signed
  straight back in next launch.
- **Dashboard**: open alerts by severity, devices up/down/inactive, and the
  five alerts that most need attention. Pull to refresh.
- **Devices**: every device, down devices first, searchable by name, IP, OS,
  hardware or location, and filterable by state.
- **Device detail**: state, identity, uptime and the device's own open alerts,
  plus an "Open in browser" link to the device in LibreNMS.
- **Alerts**: open alerts, most severe first. Swipe to acknowledge (with an
  optional note) or unacknowledge.
- **Alert notifications**: new, reopened and (optionally) recovered or
  acknowledged alerts, with desktop's rules - see [below](#alert-notifications).
  Tapping one opens the device, or the alert list for a summary.
- **Settings**: the connected server and version, notification options
  (per severity, recovery, acknowledgement, quiet hours, a test
  notification), the *Server stores timestamps in UTC* option (the same
  setting as on desktop), and sign-out.

## Alert notifications

The app checks LibreNMS for alert changes and notifies you the same way
desktop does. It uses Core's `AlertChangeDetector` and `AlertSummaryText`,
and desktop's `NotificationSettings`. Desktop's rules for which changes to
notify about live in its WPF app, so `AlertNotificationPlanner` mirrors them.

**When it checks:**

| | While the app is open | In the background |
| --- | --- | --- |
| Android | Every poll interval (60 s by default, 30 s at most often) | About every 15 minutes (WorkManager's minimum), later under Doze or battery saver |
| iOS | Every poll interval | When iOS allows (background app refresh): often every few hours or less, more often if you open the app a lot |

Because it polls rather than receiving a push from the server, a background
notification can lag the alert by minutes on Android and longer on iOS.
Getting alerts on the phone the moment they fire needs a push from the
server side, for example a LibreNMS alert transport. That's a separate piece of
work from this app.

**Details worth knowing:**

- The first check after signing in (or after switching server) records what's
  already open without notifying, like desktop's "don't notify on first poll".
  Later checks, including background wakes in a fresh process, compare
  against that saved state (`alert-watch.json` in the app's data folder).
- Your own acknowledge/unacknowledge from the Alerts tab doesn't notify you.
- A later notification for the same alert replaces the earlier one, and
  acknowledging or recovering an alert removes its notification.
- More changes than desktop's per-poll limit (5) become one summary
  notification ("3 new critical alerts").
- Sounds and how notifications appear are set by the phone. On Android there
  are three channels (critical, warnings, updates) you can tune under the
  app's notification settings.
- Signing out stops the checks and forgets the saved state.

## How the code is shared with desktop

```
external/desktop/                  git submodule: DashyNMS/desktop
src/
  DesktopNMS.Core.Portable/        desktop's Core, compiled for net10.0
  DashyNMS.Mobile.Shared/          view models, session, secret storage (net10.0)
  DashyNMS.Mobile/                 MAUI app head: pages + platform adapters
tests/
  DesktopNMS.Core.Tests/           desktop's own Core tests, run against the portable build
  DashyNMS.Mobile.Shared.Tests/    tests for the shared mobile layer
```

- **No copied code.** `DesktopNMS.Core.Portable` compiles
  `external/desktop/src/DesktopNMS.Core/**/*.cs` directly. Desktop's
  `SessionService` depends only on Core, so it's linked in the same way.
  To pick up desktop changes, bump the submodule:
  `git submodule update --remote external/desktop`.
- **What's different on mobile.** Desktop keeps secrets with Windows DPAPI.
  Mobile implements Core's `ITokenProtector` over MAUI `SecureStorage`
  instead (`SecureTokenProtector` + `SecretCache`), and registers its own
  services rather than calling `AddDesktopNmsCore()`.
- **Portability check.** CI runs the desktop Core test suite on Linux against
  the portable build, so anything upstream that only works on Windows shows
  up here. One test is filtered out today for a known upstream gap
  (see `tests/DesktopNMS.Core.Tests/DesktopNMS.Core.Tests.csproj`).
- **Keep the head thin.** Anything that isn't a page or a platform API belongs
  in `DashyNMS.Mobile.Shared`, where it's plain .NET and unit tested. The
  head only supplies adapters for navigation, dialogs, the browser, secure
  storage, notifications and background scheduling.

## Building

You need the .NET 10 SDK (see `global.json`) and a clone that includes the
submodule:

```sh
git clone --recurse-submodules https://github.com/DashyNMS/mobile
# or, in an existing clone:
git submodule update --init
```

Shared code and tests (any OS, no MAUI workloads needed):

```sh
dotnet test tests/DashyNMS.Mobile.Shared.Tests
dotnet test tests/DesktopNMS.Core.Tests
```

The app itself:

```sh
# Android (Windows, macOS or Linux; needs the Android SDK + JDK 17+)
dotnet workload install maui-android
dotnet build src/DashyNMS.Mobile -f net10.0-android -t:Run

# iOS (macOS with a matching Xcode)
dotnet workload install maui-ios
dotnet build src/DashyNMS.Mobile -p:MobilePlatform=ios -t:Run
```

Or open `DashyNMS.Mobile.slnx` in Visual Studio or Rider.

CI (`.github/workflows/ci.yml`) runs both test suites, builds an Android APK
(uploaded as an artifact) and builds for the iOS simulator.

To ship an iOS build to testers, see [docs/RELEASING-IOS.md](docs/RELEASING-IOS.md).
The manually triggered **TestFlight** workflow signs the app and uploads it to
App Store Connect, with no Mac needed.

## Feature parity

What desktop has (from its code, README and the website), and where mobile
stands. ✅ done, 🟡 partly, ⬜ not yet, ➖ not planned for a phone.

| Area | Desktop | Mobile |
| --- | --- | --- |
| Sign-in, saved session, backup address, self-signed certs | ✅ | ✅ |
| Dashboard | Drag-and-resize widget grid: alerts, gauge, device status, pinned sensors, graph, recently viewed, pinned devices, wireless | 🟡 Fixed layout: alert and device counts, top five alerts |
| Alert notifications | Toasts, per-severity persistence/sound, quiet hours, start-up suppression | ✅ Same rules; sound and persistence set by the phone |
| Worst severity at a glance | Tray icon with open-alert count | ⬜ App icon badge, home-screen widget |
| Alerts list | Severity/state filters, search, CSV export | 🟡 Hide acknowledged only |
| Acknowledge / unacknowledge | ✅ | ✅ |
| Alert rules and templates | Full editor | ⬜ View only, maybe; editing is a desktop job |
| Devices list | Sortable grid, column picker, filters | 🟡 Search and up/down filter |
| Pinned and recently viewed devices | ✅ | ⬜ |
| Bulk actions, add device | ✅ | ➖ Maybe add device later |
| Device View: overview, active alerts | ✅ | ✅ |
| Device View: availability/outages, sensors, resources (CPU/memory/disk), ports, neighbours, VLANs, FDB/ARP, routing, wireless, inventory, graphs, event log | ✅ | ⬜ |
| Device actions: rediscover, maintenance window, edit, delete | ✅ | ⬜ Rediscover and maintenance first |
| Open in browser / SSH / Telnet | ✅ | 🟡 Browser only |
| Health (sensors across all devices vs limits) | ✅ | ⬜ |
| Groups and locations | ✅ | ⬜ |
| Neighbours views | ✅ | ⬜ |
| Maps: network, geographical, custom | ✅ | ⬜ Geographical suits a phone best |
| Logs: event log, Graylog | ✅ | ⬜ |
| Unimus config backups and diffs | ✅ | ⬜ |
| Settings: poll interval, thresholds, device name style, theme/accent, server logo | ✅ | 🟡 Timestamps and notifications only |
| Update checks | ✅ | ➖ The stores handle it |

The website describes desktop only, and leaves out several things desktop
already has: Neighbours, Maps, Logs/Graylog, Unimus, the backup server
address and in-app updates. Nor does it mention mobile yet.

## Roadmap

Ordered by how much each adds on a phone, not by desktop's order.

**Next: the things you open the app for**

1. **Widgets**: a home-screen widget with open alerts by severity and devices
   down, tapping through to the app. Android widgets can be written in C#
   here. iOS widgets have to be a small Swift WidgetKit extension, built
   alongside the MAUI app and reading a snapshot the app shares through an
   App Group. That needs the App Group capability on the App ID and a
   regenerated provisioning profile.
2. **App icon badge**: the open-alert count on the icon, the phone's version
   of desktop's tray icon.
3. **Alerts list parity**: severity and state filters and search, plus an
   alert detail view with the rule, note and fault details.
4. **Pinned and recently viewed devices**, on the dashboard and at the top of
   Devices.
5. **Device View, core tabs**: availability and outages, sensors, ports,
   event log. Plus the two actions you want in a hurry: rediscover and
   schedule maintenance.

**Then: the rest of the everyday views**

6. **Health**: sensors across all devices, coloured against their limits.
7. **Graphs**: device and port graphs via Core's `IGraphsApi`.
8. **Groups and locations**, with the same click-through to Device View.
9. **Device View, remaining tabs**: resources, neighbours, VLANs, FDB/ARP,
   routing, wireless, inventory.
10. **More settings**: poll interval, health thresholds, device name style,
    theme.

**Later**

11. **Geographical map** of devices and their state.
12. **Integrations**: Graylog messages and Unimus config backups. Core
    already has both clients; they need keychain-backed secret stores like
    the LibreNMS token's.
13. **Rules and templates**, read-only.
14. **Instant alerts**: a push from the server side, so alerts don't wait for
    the next background check.
15. **Store builds**: Play internal testing (TestFlight is in place, see
    above).

## Upstream notes

Things found while porting that would be better fixed in the desktop repo:

- `SettingsStore.Save()` is `Changed?.Invoke(this, Write())`. The
  null-conditional skips `Write()` as well when nothing subscribes to
  `Changed`, so settings are silently never saved. Desktop always has a
  subscriber, so it doesn't notice. Mobile works around it with
  `MobileSettingsStore`. The fix is to call `Write()` first, then raise the
  event.
- `AppPaths` uses `Environment.GetFolderPath(SpecialFolder.ApplicationData)`,
  which returns an empty string off Windows when the folder doesn't exist
  yet, which is always the case on a fresh phone install. Everything then
  lands in a relative `DashyNMS` folder. Mobile creates the folder first
  (`MobileStorage.EnsureDataFolder`). Passing `SpecialFolderOption.Create`
  in `AppPaths` would fix it at the source.
- `UnimusExport.FileNameFor` relies on `Path.GetInvalidFileNameChars()`,
  which only contains `/` and `\0` off Windows. That means `:`, `*` and `?`
  survive into file names exported from a phone. A fixed, Windows-safe set
  would make the names portable.
- Core targets `net9.0-windows` only because of its DPAPI secret stores.
  Moving those (and `AddDesktopNmsCore`'s registration of them) into the WPF
  project would let Core target plain `net9.0`/`net10.0`. Mobile could then
  use a normal `ProjectReference` instead of compiling the sources.
- The rules for which alert changes deserve a notification (`ShouldNotify`,
  the titles, and the summary cap in `AlertNotificationService`) live in the WPF
  app, so mobile's `AlertNotificationPlanner` has to mirror them. Moving
  them into Core would give both apps one copy.
