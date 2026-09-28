# DashyNMS Mobile

A cross-platform (Android and iOS) companion to [DashyNMS desktop](https://github.com/DashyNMS/desktop)
for keeping an eye on your [LibreNMS](https://www.librenms.org/) network from
your phone. It's built with .NET MAUI and runs on **the same Core library as
the desktop app**, so API handling, models, alert logic and fixes are shared
rather than rewritten.

> **Status: early.** Sign-in, a dashboard, devices and Device View, alerts
> (with detail, acknowledge/unacknowledge and export), alert notifications, an
> app icon badge, home-screen and lock-screen widgets and settings are in place. See the
> [roadmap](#roadmap) for what's next.

## What it does today

- **Sign in** with your server address and API token, the same fields as desktop,
  including the optional backup address and self-signed-certificate switch.
  The token is kept in the platform keychain/keystore, and you're signed
  straight back in next launch.
- **Dashboard**: desktop's widgets as a column of cards you choose and
  order (Dashboard → Customise), kept in desktop's own dashboard setting:
  - alert counts, with desktop's gauge as a bar
  - device counts
  - the five alerts most needing attention
  - pinned and recently viewed devices
  - sensors you pick, coloured against their thresholds
  - one device graph you pick, with its time range
  - wireless controllers' access points and clients, found as desktop finds
    them (one device per OS first)

  Each card only fetches what it needs. Pull to refresh.
- **Devices**: every device, with desktop's Up / Down / Maintenance / Disabled
  chips (with counts), search across name, IP, OS, hardware, location, type,
  contact, serial and id, Type / Location / Group filters, and sorting by
  name, status, IP, uptime, location, OS or hardware. Swipe to pin a device
  to the top; recently viewed devices show in a strip above the list. Pins
  and recents are desktop's own settings. Maintenance windows are checked per
  device after the list loads, as on desktop.
- **Device detail**: state, identity, uptime and the device's own open alerts,
  **Rediscover** and **Schedule maintenance** (now or later, a duration, and
  desktop's skip/mute/run-alerts choice), pin, "Open in browser", **SSH** and
  **Telnet** (in whichever app on the phone handles those links), and
  desktop's Device View sections, each on its own searchable page:
  availability and outages, sensors (coloured against desktop's thresholds or
  the sensor's own limits), graphs, CPU/memory/storage, ports (tap one for
  its traffic, packet and error graphs), neighbours
  (tap through to the neighbour), VLANs, FDB, ARP, routing (BGP, OSPF,
  VRFs), wireless, inventory and the event log.
- **Alerts**: open alerts, most severe first, with desktop's Critical /
  Warning / Acknowledged filter chips (with counts) and search across device,
  rule, note and alert id. Filters are remembered between launches. Swipe to
  acknowledge (with an optional note) or unacknowledge. **Export** shares the
  filtered list as CSV, in desktop's columns.
- **Logs** (Alerts → Logs): LibreNMS's event log and alert log for the whole
  network, newest first, with each entry's device and a tap through to it.
  Loads a page at a time; search covers what's loaded.
- **Alert detail**: tap any alert (in the list, on the dashboard, in Device
  View or in a notification) for its own page: why it fired - desktop's
  fault details, with the columns the rule tests first - the rule's
  condition, notes and procedure, the rule's recent history on that device,
  and acknowledge / unacknowledge.
- **Alert notifications**: new, reopened and (optionally) recovered or
  acknowledged alerts, with desktop's rules - see [below](#alert-notifications).
  Tapping one opens the alert, or the alert list for a summary.
- **App icon badge** (iPhone): the open-alert count on the icon, counted as
  desktop's Alerts tab badge and using its settings (on/off, and whether
  acknowledged alerts count). It updates with every alert check, so it's
  live in the background too. Android launchers show their own dot or
  count from DashyNMS's notifications instead, as Android gives apps no
  way to set a number.
- **Map** (Devices → More): desktop's geographical map. One pin per LibreNMS
  location, showing its device count, red if any device there is down. It
  uses OpenStreetMap tiles, or the tile server in desktop's own map setting,
  drawn with Leaflet bundled inside the app. Tap a pin for that location's
  devices. Devices without usable coordinates are counted, not dropped.
- **Neighbours** (Devices → More): every CDP/LLDP link across the network,
  one row per cable. Links with a device or port down come first, with a
  "down only" filter and search. Tap to open either end.
- **Groups & locations** (Devices → More): desktop's Groups and
  Locations tabs as one page. Every device group or location with its device
  count, how many are down or disabled, and coordinates where LibreNMS has
  them. Anything with devices down comes first. Tap one for the Devices list
  filtered to it.
- **Graylog** (Alerts → Logs → Graylog, and a Graylog section in Device
  View): messages from your Graylog server, found as LibreNMS finds them -
  by time range, level, stream and message text, newest first, and for a
  device by its names and addresses (optionally every interface address).
  Known sources are shown by device name; tap a message for every field and
  a way to its device. Set up in Settings → Graylog with desktop's Graylog
  settings; the password is kept in the platform keychain/keystore. Like
  desktop, it's independent of the LibreNMS sign-in.
- **Health**: desktop's Health tab. Every sensor across the network in its
  four categories (dBm, signal, temperature, fan speed), coloured against
  the same thresholds as Device View, problems first, with Critical /
  Warning / OK chips and search. Tap a sensor for its device's sensors.
- **Home-screen widgets**, drawn from the snapshot each alert check saves,
  so they stay current in the background and never hold the API token:
  - *Alerts*: the worst alert in full (small), three with counts (medium),
    or six with their rules wrapped, acknowledged ones last (large).
  - *Alert pie chart*: every device once, by its worst alert - critical,
    warning, acknowledged or OK - with the devices behind the red and amber
    on the large size.
  - *Overview* (large): a devices bar, up/down/disabled, alert counts and the
    top alerts.
  - *Pinned devices* and *Sensors* (medium, large): the app's pinned devices,
    and the dashboard Sensors card's sensors against their limits. Sensors
    are only read while some are picked, at most every 15 minutes.
  - *Lock screen* (iPhone): inline, circular and rectangular. Counts only
    unless Settings → Lock screen widgets → Hide alert details is turned off.

  Tap an alert or device to open it, anywhere else for the alert list.
  Android's are normal app widgets that show more as they're resized.
  iPhone's are a Swift WidgetKit extension (`ios-widget/`), which needs a
  one-off App Group setup before TestFlight builds include it; see
  [docs/RELEASING-IOS.md](docs/RELEASING-IOS.md#home-screen-widget).
- **Settings**: the connected server and version, light/dark (the phone's
  own, or fixed), how often to check alerts while the app is open (desktop's
  poll interval, 30 seconds to 15 minutes), desktop's Health thresholds
  (dBm, signal, temperature, fan speed, and whether they override a sensor's
  own limits, checked before saving), the app icon badge, notification options
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
| Dashboard | Drag-and-resize widget grid: alerts, gauge, device status, pinned sensors, graph, recently viewed, pinned devices, wireless | ✅ The same widgets as cards you show, hide and order (one column, so no resizing; one of each) |
| Alert notifications | Toasts, per-severity persistence/sound, quiet hours, start-up suppression | ✅ Same rules; sound and persistence set by the phone |
| Worst severity at a glance | Tray icon with open-alert count | ✅ Home-screen widget; app icon badge (iPhone; Android's launcher dot comes from notifications) |
| Alerts list | Severity/state filters, search, CSV export | ✅ Same filters (remembered between launches), search, CSV export through the share sheet |
| Alert detail: fault details, rule, history | ✅ | ✅ On its own page, from any alert or notification |
| Acknowledge / unacknowledge | ✅ | ✅ |
| Alert rules and templates | Full editor | ⬜ View only, maybe; editing is a desktop job |
| Devices list | Sortable grid, column picker, state chips, Type/Location/Group filters, search | ✅ State chips (incl. maintenance), Type/Location/Group filters (one choice each), search, seven sorts |
| Pinned and recently viewed devices | ✅ | ✅ On Devices and the dashboard (pin by swiping or from Device View) |
| Bulk actions, add device | ✅ | ➖ Maybe add device later |
| Device View: overview, active alerts | ✅ | ✅ |
| Device View: availability/outages, sensors, resources (CPU/memory/disk), ports, neighbours, VLANs, FDB/ARP, routing, wireless, inventory, graphs, event log | ✅ | ✅ Each section on its own page, searchable; neighbours link to their devices |
| Device actions: rediscover, maintenance window, edit, delete | ✅ | 🟡 Rediscover and maintenance windows; edit/delete stay on desktop |
| Open in browser / SSH / Telnet | ✅ | ✅ SSH and Telnet open in whichever app on the phone handles the link |
| Health (sensors across all devices vs limits) | ✅ | ✅ Desktop's four categories, problems first |
| Groups and locations | ✅ | ✅ Counts and devices down; opens the Devices list filtered (editing stays on desktop) |
| Neighbours views | ✅ | 🟡 Every CDP/LLDP link, down ends first (desktop's own custom views stay on desktop) |
| Maps: network, geographical, custom | ✅ | 🟡 Geographical, on OpenStreetMap or desktop's tile server (network and custom maps stay on desktop) |
| Logs: event log, Graylog | ✅ | 🟡 Network-wide event log and alert log (Alerts → Logs); Graylog to come |
| Unimus config backups and diffs | ✅ | ⬜ |
| Settings: poll interval, thresholds, device name style, theme/accent, server logo | ✅ | ✅ Poll interval, thresholds, device names, light/dark, timestamps, notifications (accent colour and server logo stay desktop's) |
| Update checks | ✅ | ➖ The stores handle it |

The website describes desktop only, and leaves out several things desktop
already has: Neighbours, Maps, Logs/Graylog, Unimus, the backup server
address and in-app updates. Nor does it mention mobile yet.

## Roadmap

Ordered by how much each adds on a phone, not by desktop's order.

What's left is the less everyday:

1. **Unimus**: config backups, including Device View's Unimus section. Core
   already has the client; it needs a keychain-backed token store like
   Graylog's.
2. **Rules and templates**, read-only.
3. **Instant alerts**: a push from the server side, so alerts don't wait for
   the next background check.
4. **Store builds**: Play internal testing (TestFlight is in place, see
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
- `ILogsApi` only takes one device. LibreNMS's `logs/eventlog` and
  `logs/alertlog` routes treat the device as optional and then list every
  device's entries, which mobile's `NetworkLogs` uses through Core's transport.
  An overload without the device in Core would let both apps share it.
- `GraylogApi` (and `UnimusApi`) turn "Allow untrusted certificate" into a
  callback that accepts every certificate, as `LibreNmsTransport` does (#2).
  Their logins are safe from redirects, though: .NET drops the
  `Authorization` header on any redirect it follows, which
  `GraylogTransportTests` checks.
- The rules for which alert changes deserve a notification (`ShouldNotify`,
  the titles, and the summary cap in `AlertNotificationService`) live in the WPF
  app, so mobile's `AlertNotificationPlanner` has to mirror them. Moving
  them into Core would give both apps one copy.
