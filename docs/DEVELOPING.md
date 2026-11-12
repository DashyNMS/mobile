# Developing DashyNMS Mobile

The technical guide to the app: what each part does, how it shares code with
desktop, and how to build it. For what the app is and how to get it, see the
[README](../README.md).

A cross-platform (Android and iOS) companion to [DashyNMS desktop](https://github.com/DashyNMS/desktop)
for keeping an eye on your [LibreNMS](https://www.librenms.org/) network from
your phone. It's built with .NET MAUI and runs on **the same Core library as
the desktop app**, so API handling, models, alert logic and fixes are shared
rather than rewritten.

> **Status: 1.0.** Everything below is in the first App Store release. See the
> [roadmap](#roadmap) for what's next.

## What it does today

- **Sign in** with your server address and API token, the same fields as desktop,
  including the optional backup address and self-signed-certificate switch.
  The token is kept in the platform keychain/keystore, and you're signed
  straight back in next launch.
- **Dashboard**: desktop's widgets as a column of cards you choose and
  order (Dashboard → Customise), kept in desktop's own dashboard setting:
  - alert group counts with separate fault totals, labelled as visible to this account, with desktop's gauge as a bar
  - device counts
  - the five alerts most needing attention
  - pinned and recently viewed devices
  - sensors you pick, coloured against their thresholds
  - one device graph you pick, with its time range
  - wireless controllers' access points and clients, found as desktop finds
    them (one device per OS first)
  - the newest event log entries or Graylog messages across the network,
    with desktop's type, stream, range and count; a tap opens the entry with
    its device, and See all opens Logs or Graylog on the card's filter

  Each card only fetches what it needs. Pull to refresh.
- **Devices**: every device, with desktop's Up / Down / Maintenance / Disabled
  chips (with counts), search across name, IP, OS, hardware, location, type,
  contact, serial and id, Type / Location / Group filters, and sorting by
  name, status, IP, uptime, location, OS or hardware. Swipe to pin a device
  to the top; recently viewed devices show in a strip above the list. Pins
  and recents are desktop's own settings. Maintenance windows are checked per
  device after the list loads, as on desktop.
- **Device detail**, laid out as desktop's Device View overview: the name,
  state and hardware/OS/IP; **Rediscover**, **Schedule maintenance** (now or
  later, a duration, and desktop's skip/mute/run-alerts choice), pin, and
  **SSH** and **Telnet** (in whichever app on the phone handles those links);
  desktop's stat tiles (30-day availability, uptime, ports up, active
  alerts); the Device card (names, IP, OS, hardware, serial, location,
  contact, groups, dependencies, added, last discovered, object ID,
  description); then desktop's overview cards - ping response, active
  alerts, availability, resources, sensors, connected to, busiest ports -
  and the rest of its sections as cards, one column on a phone and a grid on
  a tablet or in landscape. Sections the device has nothing in drop out, as
  on desktop. Each opens its own searchable page:
  availability and outages, sensors (coloured against desktop's thresholds or
  the sensor's own limits), graphs, CPU/memory/storage, ports (tap one for
  its traffic, packet and error graphs), neighbours
  (tap through to the neighbour; a device with no ports, such as an access
  point, still lists the devices that see it), VLANs, FDB, ARP, routing (BGP, OSPF,
  VRFs), wireless, inventory and the event log.
- **Alerts**: on LibreNMS 26.10, faults appear in expandable groups by device
  and rule, with fault totals, worst severity, first and last seen times, and
  partial acknowledgement. Expanded children name their entities. Groups stay
  visible while any child matches the filters, including a new fault alongside
  acknowledged siblings. Children enter the virtualised list only when expanded.
  Open alerts are most severe first, with desktop's Critical /
  Warning / OK / Acknowledged filter chips (with counts) and search across device,
  rule, note and alert id. Filters are remembered between launches. Swipe to
  acknowledge (with an optional note) or unacknowledge. On 26.10 these actions
  explicitly apply to the device/rule group. Core reads the group back after a
  write; the result reports verified changes, partial failures and missing faults.
  Detail offers a single-fault action only when the current rule and open count
  confirm per-entity scope, and Core checks that scope again before writing.
  A refusal on one resource leaves other groups available. **Export** shares the
  filtered list as CSV, in desktop's columns.
- **Logs** (More, or Alerts → Logs): LibreNMS's event log and alert log for the whole
  network, newest first, with each entry's device and a tap through to it.
  Loads a page at a time; search covers what's loaded. Logs and Graylog have
  desktop's Auto-refresh chip and interval, off until switched on. It runs only
  while the page shows and the app is in the foreground, never overlaps a
  request, and brings new entries in at the top without moving a list
  scrolled down. A failed refresh keeps the rows and filters.
- **Alert detail**: tap any alert (in the list, on the dashboard, in Device
  View or in a notification) for its own page: why it fired - desktop's
  fault details, showing the columns the rule tests, with the rest behind
  "Show all fields" - the rule's
  condition, notes and procedure, and acknowledge / unacknowledge. A 26.10
  fault uses its own current payload and matching fault_id history, including
  while acknowledged. Its recurrence summary counts device/rule occurrences
  across all sibling faults. Truncated or uncertain history is marked incomplete;
  an unreadable log is shown as unavailable.
- **Alert notifications**: new, reopened and (optionally) recovered or
  acknowledged alerts, with desktop's rules - see [below](#alert-notifications).
  Tapping one opens the alert, or the alert list for a summary.
- **App icon badge** (iPhone): the open alert-group count on the icon, counted as
  desktop's Alerts tab badge and using its settings (on/off, and whether
  acknowledged alerts count). It updates with every alert check, so it's
  live in the background too. Android launchers show their own dot or
  count from DashyNMS's notifications instead, as Android gives apps no
  way to set a number.
- **Map** (More): desktop's geographical map. One pin per LibreNMS
  location, showing its device count, red if any device there is down. It
  uses OpenStreetMap tiles, or the tile server in desktop's own map setting,
  drawn with Leaflet bundled inside the app. Tap a pin for that location's
  devices. Devices without usable coordinates are counted, not dropped.
- **Neighbours** (More): every CDP/LLDP link across the network,
  one row per cable. Links with a device or port down come first, with a
  "down only" filter and search. Tap to open either end.
- **Groups & locations** (More): desktop's Groups and
  Locations tabs as one page. Every device group or location with its device
  count, how many are down or disabled, and coordinates where LibreNMS has
  them. Anything with devices down comes first. Tap one for the Devices list
  filtered to it.
- **Graylog** (More, and a Graylog section in Device
  View): messages from your Graylog server, found as LibreNMS finds them -
  by time range, level, stream and message text, newest first, and for a
  device by its names and addresses (optionally every interface address).
  Known sources are shown by device name, matched as desktop matches them:
  IP, hostname or sysName in any case, then a short name such as `core-sw1`
  when only one device has it. Tap a message for every field and a way to
  its device. Set up in Settings → Graylog with desktop's Graylog
  settings; the password is kept in the platform keychain/keystore. Like
  desktop, it's independent of the LibreNMS sign-in.
- **Health**: desktop's Health tab. Every sensor across the network in its
  four categories (dBm, signal, temperature, fan speed), coloured against
  the same thresholds as Device View, problems first, with Critical /
  Warning / OK chips and search. Tap a sensor for its device's sensors.
- **Home-screen widgets**, drawn from the snapshot each alert check saves,
  so they stay current in the background and never hold the API token:
  The set in the design mock-ups:
  - *Alerts*: the worst alert with its severity, device and rule (small), or
    the critical and warning counts beside the three worst (medium).
  - *Alert pie chart* (small): every device once, by its worst alert -
    critical, warning, acknowledged or OK - with the count in the middle.
  - *Overview* (large): a devices bar, up/down/disabled, alert counts and the
    top alerts.
  - *Lock screen* (iPhone): circular and rectangular. Counts only
    unless Settings → Lock screen widgets → Hide alert details is turned off.

  Alert widgets count and list each device/rule group once. Acknowledgement
  and severity settings apply to qualifying children; fault totals stay separate.
  Totals describe what is visible to the account.
  Tap an alert or device to open it, anywhere else for the alert list.
  Android's are normal app widgets that show more as they're resized.
  iPhone's are a Swift WidgetKit extension (`ios-widget/`), which uses an
  App Group, set up for TestFlight builds (see [Releasing](#releasing)).
- **More** (the last tab): every page, under Monitor, Network, Logs and App
  headings. Pin up to three to the tab bar beside Dashboard and More, which
  always stay. Devices, Alerts and Health are pinned until you choose; a page
  that isn't pinned opens from More, and links to it (a dashboard count, a
  notification) open it there.
- **Settings** (More → Settings): a row per section, each summarising its
  values and opening its own page - the server (address, version, the
  *Server stores timestamps in UTC* option), appearance (the phone's
  light/dark, or fixed), devices (desktop's name choice, recently viewed),
  alert checks (how often while the app is open - desktop's poll interval,
  30 seconds to 15 minutes - and the app icon badge), notifications (per
  severity, recovery, acknowledgement, quiet hours), desktop's Health
  thresholds, lock screen widgets, Graylog and sensor definitions - then
  About and Diagnostics, then sign-out. About lists the app version and
  build, Core, and the sensor definitions and their state, and reopens
  What's new. What's new appears once after an update, never on a fresh
  install, from the embedded `WhatsNew.md`. The diagnostics header reuses
  that list beside the phone and its system, never the server, names, token
  or a path.
- **Sensor definitions** (Settings): the DashyNMS/sensor-definitions
  catalogue that names state sensors, on by default as desktop's. You can
  use your own public GitHub repository instead. Core's
  `SensorDefinitionStore` keeps it in Core's data folder: sign-out keeps it,
  "Sign out and forget everything" deletes it, and so does turning it off.
  The app asks for a newer catalogue at launch and when it comes back to the
  foreground. Core fetches at most daily and works offline from the cached
  copy. Nothing is fetched from a background alert check.
  With them on, a state sensor shows its name ("Ready", "Fan failed") with
  the number beneath, in Device View, the dashboard's Sensors cards and the
  sensor picker. It's coloured by the definition only when the source says
  how healthy the state is; an unresolved or label-only state is never OK.
  `CorePolling` applies them to each sensor list in the foreground only, so a
  background check never asks GitHub for an OS's definitions.

Read-only alert rule details show per-entity notification settings and the maximum
entity limit (including inherited limits). Conditions using device columns removed
in LibreNMS 26.10 include compatibility advice, including before a server upgrade.

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
  The versioned state distinguishes legacy alerts from faults. An older saved
  state or a server switching source always takes a quiet baseline and withdraws
  old notifications, even when first-poll suppression is off.
- Background checks fetch open alerts only, whatever the recovery settings:
  recoveries come from looking up the alerts that left the open list. Some
  LibreNMS 26.10 servers return HTTP 500 when recovered faults are listed, and
  that failed every check, so notifications and widgets stopped.
- Fault changes notify once per device and rule. Additional faults and partial
  recovery update the same notification silently. Recovery requires the last
  outstanding fault to be confirmed recovered. Missing rows are looked up with
  at most four requests at once, regardless of display and notification filters.
  Failed, denied or missing lookups retain the last known state.
- Notifications reach the platform before the check saves its new alert state.
  A cancelled fetch or failed delivery leaves the previous state for the next check.
- Widget alert data updates before device and sensor enrichment. Enrichment has
  a five-second budget; a failure keeps the previous readings and their timestamp.
  Sensors are fetched only when a sensor card needs them. Notification names use
  the session's cached devices, or the alert's hostname until devices are loaded.
- Your own acknowledge/unacknowledge from the Alerts tab doesn't notify you.
- A later notification for the same alert replaces the earlier one, and
  acknowledging or recovering an alert removes its notification.
- More changes than desktop's per-poll limit (5) become one summary
  notification ("3 new critical alerts").
- Sounds and how notifications appear are set by the phone. On Android there
  are three channels (critical, warnings, updates) you can tune under the
  app's notification settings.
- Signing out stops the checks and forgets the saved state.

Notification and widget taps carry a qualified alert key (`fault:12` or
`alert:12`) or a device/rule group (`device:7/rule:3`). Group targets open the
Alerts list with that group expanded, including its acknowledged faults;
single-fault targets open only that fault. Detail loading checks the server's
alert source before reading the id. Old numeric targets open Alerts with a
moved-on message, so a server upgrade cannot make an old notification open an
unrelated fault. Widget snapshot version 2 includes both keys; the app and the
Swift extension ignore older snapshots until the next alert check replaces them.

## How the code is shared with desktop

Mobile consumes the pinned `DashyNMS.Core` package from
[DashyNMS/core](https://github.com/DashyNMS/core), targeting net10.0.
Core's own repository runs its tests. The pinned 2.0.0-ci.49 package includes
LibreNMS 26.10 fault identities, grouping, recovery state, scope-aware actions,
fault history and per-resource permissions. Nullable reported device values keep
omitted flags and uptime unknown. It also corrects single-fault action
results so untouched siblings do not appear as failures. It reads and writes the
sign-in link desktop shows as a QR code (#171). Mobile's versioned
notification baseline must accompany these APIs before a build ships.
Update the package version in
`src/DashyNMS.Mobile.Shared/DashyNMS.Mobile.Shared.csproj` to take a Core fix.
Mobile's app version stays independent of Core and desktop; the next mobile
release remains 2.0.0.

CorePolling connects the mobile pages to Core's AlertMonitor, DeviceMonitor,
SensorMonitor, DeviceCache and AlertRuleCache. Worker events complete awaited
requests; page continuations run on the UI context. Session changes discard
cached names and readings. Device and sensor polling slows while the app is
in the background, using Core's activity policy. The platform coordinator
still schedules background wakes and notifications and preserves mobile's
server-specific notification baseline. Widgets use the same device cache
and sensor monitor as the pages.

Health, the dashboard's Sensors cards and Device View share one in-flight
sensor list request, waited for outside the alert and device polls. A large
LibreNMS can take most of a minute to send it, and Core allows two. Pages keep
Core's last good readings on screen with "Updating…" while it loads. After a
failure they say how old the readings are, in desktop's words.

Sensor values use Core's formatting, including API units for unknown classes.
Missing or non-finite readings show a dash with no severity or widget fill.
Graphs retain LibreNMS's legend when API entries cannot be matched to drawn
series; the graph picker uses the device's advertised graph lists.
Core's DashboardLayoutService persists card
changes and Undo; DashboardLayout retains only the phone's ordering and
supported-card projection of the shared settings.

The shared mobile project owns session handling and CSV export (originally
adapted from desktop), view models and services. The MAUI head supplies pages
and platform adapters. Session and CSV source provenance is recorded in
`src/DashyNMS.Mobile.Shared/Desktop/README.md`.

Denied reads stay distinct from empty results. Device section cards remain visible
with a permission message, and denied graphs show as denied rather than broken.
Missing device state flags and uptime display as unknown, including in widgets.
A resource-specific write refusal leaves other devices available and does not stop
a bulk action; a token-wide refusal still stops the batch.

## Building

Use the .NET SDK in `global.json`. No submodules are needed.
GitHub Packages requires authentication even for public packages. Set
`DASHYNMS_PACKAGES_TOKEN` to a token with `read:packages`; never commit it.
The package source and mapping are in `nuget.config`. Actions uses its
`GITHUB_TOKEN` with `packages: read`; the Core package must grant this
repository Actions read access.

Shared code and tests (no MAUI workloads):

```sh
dotnet test tests/DashyNMS.Mobile.Shared.Tests
```

The app itself:

```sh
# Android (Windows, macOS or Linux; needs the Android SDK + JDK 17+)
dotnet workload install maui-android
dotnet build src/DashyNMS.Mobile -f net10.0-android -t:Run

# iOS (macOS with a matching Xcode, and xcodegen for the Swift bridge)
dotnet workload install maui-ios --version 10.0.402.1
(cd ios-intelligence && xcodegen generate)
DEVELOPER_DIR=/Applications/Xcode_27.1.app/Contents/Developer dotnet build src/DashyNMS.Mobile -p:MobilePlatform=ios -t:Run
```

Or open `DashyNMS.Mobile.slnx` in Visual Studio or Rider.

iOS builds use Xcode 27.1 and workload set `10.0.402.1`, matching the
[.NET for iOS 27.1 release](https://github.com/dotnet/macios/releases/tag/dotnet-10.0.1xx-xcode27.1-10018). Both CI and TestFlight
select this toolchain explicitly. The iOS 27.1 SDK enables the Duo's full
display and vertical system bars; changing page padding cannot remove the
compatibility strip produced by older SDKs. The minimum supported iOS
version remains 15.0.

The iOS build includes a small Swift framework, `ios-intelligence/`, which
wraps Apple's Swift-only Foundation Models framework in an Objective-C API
(#570). `src/DashyNMS.Mobile.Intelligence.iOS` binds it for .NET by hand in
`ApiDefinition.cs` and builds it through an `XcodeProject` item, so the only
extra step is `xcodegen generate`, which writes the Xcode project that isn't
checked in. Each selector in `ApiDefinition.cs` is spelled out in an
`@objc(...)` in `Intelligence.swift`; change both together. The framework
weak-links Foundation Models so the app still launches before iOS 26, and CI
fails if that link turns strong.

Before shipping, check a Duo on both displays: launch closed and open,
fold and unfold while on Dashboard and a list/detail page, rotate, and
check the header, system tab bar, alert dot, scrolling and keyboard insets.
Confirm the same pinned tabs and devices remain selected after resizing.
In particular, force-quit and launch directly on the inner display with a
saved session, then repeat from the outer display and unfold. Both paths
must show the pinned tab bar without needing another launch. The iOS
`AppShellRenderer` uses the compact compatibility layout on phones and
tablets. The native-trait experiment in #381 was reverted after device
reports showed changed portrait tabs and no improvement to landscape launch.
#372 (cold launch with five native items but no visible pill until
rotation) is not yet accepted on a device. The renderer now settles a wide
tab bar: `TabSettlePolicy` checks for UIKit's pill over about a second and
nudges harder each time (refresh the items, hide and show the bar, flip the
horizontal size class for one pass), then gives up until the size changes.
MAUI is pinned to 10.0.120 in the app's csproj, newer than the 10.0.110 the
workload set ships: 10.0.110's iOS `ShellItemRenderer` never passes
`traitCollectionDidChange:` to UIKit's tab controller, the likely cause of
#372 (dotnet/maui#39263, fixed by #39264). If a Duo cold launch shows the
pill without rotating and also no longer logs "Initial tabs: nudged",
`TabSettlePolicy` can be removed. The missing log alone proves nothing: the
settle check may not have run, or may have been waiting for controllers.
Narrow and portrait views are never nudged. The "Initial tabs" diagnostics
say which nudge, if any, produced the pill. Keep that failure distinct from
portrait presentation and verify both paths on the Duo. Simulator CI does
not reproduce this timing.
On the inner display in portrait, every page's top line (the Dashboard's
logo and Customise, other pages' titles or "‹ Back") should sit level with
the clock, its right-hand buttons stopping short of the status bar and
camera. `TopLineSpace` places it from UIKit's occlusion reserved regions
(iOS 27.1) and puts it back below the status bar wherever they leave too
little room, so check the outer display and a conventional iPhone are
unchanged, and that pushed pages, list/detail panes and scrolled pages
look right as the phone opens and closes.
Also check a conventional iPhone and iPad. A successful simulator build
alone does not verify these layouts.

Every page's title sits on the Dashboard's top line (#253). App.xaml's
`TopBarPadding` and `TopBarHeight` place it; `PageHeader` and `BackBar` take
their position from them, so a page adds no top padding or margin above its
header. A tab's own page puts its title on the line; a pushed page puts its
"‹ Back" row there, with the title beneath.

Dashboard editing, Add card, Choose sensors, Choose a graph and Top card
set-up put Back in the glass action area on iOS 26 and later (#343), with
no Back button in the header. Graph and Top card set-up keep their explicit
Save buttons. Android and older iOS keep the header Back control.
Check each route from Edit dashboard, and Add card from the empty dashboard:
Back must return to the page it came from. Adding a single Sensors or Graph
card opens its set-up after closing Add card; check Back there too. Repeat
with bottom and side tab bars, after rotation and folding, and with the
keyboard open. The last list item, Add button and Undo toast must stay clear
of the glass. Dashboard and Sensor changes save immediately; Graph and Top
card changes need Save, and ticking cards in Add card needs Add. Back must
preserve these rules, and returning to an editor must restore its glass
action without leaving another page's actions visible.

A device's page and each of its section pages (Ports, Sensors, Inventory
and the rest) also put Back in the glass on iOS 26 and later (#340, #344).
A section page sets `PageHeader.BackInGlass`, so its title takes the top
line with no "‹ Back" row. Both pages keep only the top safe area and pad
their content from `CoveredEdges.ForScrolling`, so on iPhone Duo's
landscape screen the rows reach from the camera to the side tab bar's glass
rather than sitting in a narrow column in the middle. Section rows go two or
three across when there's room, except Inventory, whose nesting reads down
one column, and Ports, whose compact cards go at most two across (#472). Inventory has expandable entries, Expand all / Collapse all,
and a Show ports toggle with ports hidden initially (#351). Search reveals
matching entries with their ancestors without changing saved expansion
state. Check serial copying, firmware details, the type summary and refresh
with a collapsed branch. Every section searches from the glass magnifier (#349), its
field sliding down from beneath the title; Android and older iOS open it
from the header's icon. Ports also opens Sort & filter from a glass button
on iOS 26 and later, or a header icon elsewhere (#350). The bottom card
contains the saved sort and grouping choices. Search and the card open one
at a time; after they close, a line explains the active search, sort and
grouping. Check a short section and a long one, Ports with
its filter card, and Inventory, with bottom and side tab bars,
after rotation, and on a small phone: the header card and first heading
must start clear of the status bar, and the last row must scroll clear of
the glass.

Device section searches keep the field and keyboard open while filtering
or scrolling the rows. The keyboard Search key or a tap outside the field
puts search away, retaining the filter. Check typing several letters into
both short and long lists, including a search with no matches, then tapping
the list and completing with the keyboard Search key on Android and iOS.

The device's Neighbours section also shows a scrollable graph (#352).
On phones it sits above the cards; at 800pt of usable width it sits beside
them. A node highlights and scrolls to its card. Both use the same filtered
rows. Links use the local port's state, or inactive/unknown when readings
are unavailable. Cards include speed, traffic in/out and discovery direction.
Check an unmonitored neighbour, a reverse-only link and a busy switch, with
search, rotation, dark mode and VoiceOver/TalkBack.

Device and port Graphs pages use the same edge clearances and glass Back
as the device page (#353). At 800pt of usable width, named series sit in a
300pt panel beside the graph; on phones they follow it. Graphs without a
named series list keep the full width. Check rotation, side and bottom tab
bars, graph/range selection, series toggles, Show all and long-press isolation.
The Show picker switches between Device graphs and ports with an interface
name (#354), retaining the chosen time range. A direct port link selects
that port, graph and range. Check switching between two ports and back to
the device, and a failed port listing: existing device graphs and a directly
opened port must still work, with the port-list failure shown separately.

Config backups, a single backup and Changes use `PushedListLayout` (#355)
for glass Back and manual side/bottom clearances. Backup and diff lines
scroll clear of the bottom glass, and Compare stays above it. Back returns
to the page that opened the current page: Compare opens Changes directly
from Config backups. Check the full return path to a device opened from
an alert, plus comparison selection, sharing, backup search and diff folding.
Android and older iOS retain the header Back.

Logs and standalone log entries put Back in the glass on iOS 26 and later
(#356), returning to their actual opener. Logs opens its themed search from
the glass magnifier, or a header icon on Android and older iOS. Search closes
on the keyboard Search key, lost focus or scrolling while typing, retaining
the term. Check the device filter, Event/Alert log switch, split detail pane,
auto-refresh, keyboard dismissal and the entry/device/alert return path.

Graylog and standalone messages also use glass Back (#357), with no Back
on a root Graylog tab. The glass magnifier or header icon opens themed
search. The keyboard Search key submits the server query; dismissing the
field keeps the typed term. Check a device-scoped feed, root tab, unconfigured
Graylog, split detail, message return path and query submission.

Schedule maintenance uses the full usable width with glass Back on iOS 26
and later (#358). Its four form sections use two columns at 800pt and one
below that, with 12pt gaps. Schedule occupies the shared fixed action slot
and Back discards the draft. The shared setup layout owns keyboard and
measured glass clearance. Check one device and multiple selected devices,
Start now and a later date, duration presets and custom values, denied scheduling,
keyboard
entry, rotation, and side/bottom glass.

Glass actions avoid the whole system-reserved region, including active Live
Activities (#371). If expanded actions cannot fit, Search and all non-Back
actions move into a native ellipsis menu. Back is explicitly marked as
navigation and stays outside the menu. Compact placement checks the tab bar,
screen bounds and occlusions again. Glass is allowed in the side inset that
page content avoids. Overflow stays in the tab column or bottom action row;
it never relocates into the content area. A blocked lane cannot be rescued
by moving its actions to the centre of the screen. An 8pt expansion margin prevents toggling at a boundary.
Reserved regions are read in UIKit's constraint-update callback so changes
trigger placement again. Check Live Activity appearance, expansion and removal,
rotation, root/pushed pages, filled action icons, menu callbacks, VoiceOver,
and Reduce Motion. On systems without the reserved-region API, safe-area
bounds remain the available fallback.

Event log and Graylog use `TabAlignedListLayout` (#375, #376) to match the Alerts/Devices
card base to the native tab pill. The scrolling footer keeps the final row
and Load more clear of a bottom bar, and split detail keeps its own clearances.
Check short/long logs, both bar orientations, rotation, keyboard dismissal,
split detail, unconfigured Graylog and the final row on iOS. Android keeps its existing layout.

Widget setup uses themed rounded search fields instead of native SearchBars
for sensors, graph devices and Spotlight (#386). Card titles and graph
choices keep visible labels, and ranked-card pickers avoid fixed minimum
widths that crowd small phones. Filtering and immediate saving remain unchanged;
the keyboard Search key dismisses typing while retaining the filter. Check
both themes, clearing the filter and sensor selection on a small screen.

Dashboard graph, ranked-card and Spotlight setup pages put their explicit
Save action below the form as a full-width primary button (#384). Headers
carry navigation only. Sensor selection retains its immediate-save behaviour.
Check existing/new widgets, disabled Save states, small-screen scrolling,
keyboard dismissal and bottom glass clearance.

The ellipsis menu button itself has a capsule configuration and clipped,
rounded bounds (#382), so UIKit's pressed/menu preview has the same shape
as its surrounding glass. Check opening, selecting and cancelling the menu,
including a Back + ellipsis group, both themes and VoiceOver on a device.

The device-detail logo uses a 48pt rounded tile with 7pt padding (#388).
Its light neutral background stays readable in both themes, with the dark
colour matching desktop's logo tile. No tile is shown without a loaded logo.
Check transparent SVG/PNG logos, proportions and long device titles.

Dashboard setup/picker children keep native page safe areas
and add only the extra glass-row clearance (#391). Their scrolling controls
disable duplicate native content insets; the editor footer no longer adds
an unrelated 96pt gap. Check top labels and the last item/action in every
orientation, both displays, keyboard use, reordering and return from setup.
Edit dashboard uses a scrolling viewport beneath the bottom bar, with measured
side clearance and footer space for the glass controls. Its cards use the same
340pt minimum column width, 12pt gap and three-column limit as the dashboard:
two columns on a wide Duo display, one on a narrow display. Order is read across
then down. Card titles and subtitles wrap, with Set up below and Remove in More.
More also offers Move earlier/later for reordering without dragging. Changes still
save immediately, and removal still offers Undo. The shared dashboard toast
slides and fades, with Reduce Motion respected. Check drag reordering across
columns, folding, rotation, large text, the final Add action and the full
Dashboard → Edit → Add/Set up → Back chain on both platforms.

DeviceSectionPage's rows have no enclosing list card, so its viewport runs to
the base of the body host, beneath the pill and glass, as on the Dashboard; only
a docked keyboard stops it short (#469–#479, correcting the pill-base viewport
from #530). `SectionListPlacement` (shared, unit tested) puts the whole measured
obstruction plus a 24pt gap, and any visible filter panel's measured height, in
the virtualised footer. Missing measurements fall back to 12pt plus the covered
bottom, in the footer. This shared path covers Availability, Sensors, Resources,
Ports, Neighbours, VLANs, FDB, ARP, Routing, Wireless and Inventory. Neighbours'
wide graph pane has no scrolling footer, so it keeps clearing the bottom bar and
glass in its own margin. Graphs (a full-height ScrollView), logs and Graylog
(list cards), maintenance (setup form with a fixed action) and backups (list
cards) have independent page layouts. Verify each shared section on inner/outer
displays in portrait/landscape, including short and long lists, Ports filters,
Inventory expansion and Neighbours' graph.

CI (`.github/workflows/ci.yml`) runs the mobile test suite, builds an Android APK
(uploaded as an artifact) and builds for the iOS simulator.

To ship an iOS build, see [Releasing](#releasing) below. The manually
triggered **TestFlight** workflow signs the app and uploads it to App Store
Connect, with no Mac needed.

## Commits, pull requests and issues

Every change goes through a branch and a pull request into `main`, one issue per PR, and is squash-merged, so the PR title becomes the commit on `main`. PR titles, commit messages and issue titles follow [Conventional Commits](https://www.conventionalcommits.org/) in plain words, the same standard as every DashyNMS repository:

```
type(scope): what changes, as the user would notice it
```

- **type:** `feat` (new), `fix` (was wrong, now works), `perf`, `refactor` (no behaviour change), `docs`, `test`, `build` (projects, packages, versions), `ci`, `chore`, `revert`.
- **scope:** the area, in lower case: `alerts`, `devices`, `device-details`, `dashboard`, `widgets`, `notifications`, `background`, `logs`, `graylog`, `maps`, `health`, `neighbours`, `ports`, `graphs`, `rules`, `settings`, `diagnostics`, `sign-in`, `intelligence`, `demo`, `core`, `ios`, `android`, `deps`. Separate two with a comma, or leave it out when there's no single area. A new area can add a scope; add it here in the same PR.
- **description:** says what changes, not how. Lower-case start, no full stop, the whole title under about 72 characters.
- **Issue numbers:** never in the title. `Closes #NN` goes in the PR description, which then says briefly what changed and how it was checked.
- **Breaking changes:** a `!` after the scope, and a sentence saying what breaks.
- **Issue titles:** the same form, worded as the result the fixing PR will deliver, so the PR can reuse the title. An epic ends with `(epic)`. Labels carry the rest.
- **Attribution:** commits, PRs and release notes carry no AI attribution or `Co-Authored-By` lines.

Examples:

```
fix(alerts): acknowledging no longer jumps back to the top
feat(widgets): an alert count widget for the lock screen
build(ios): build number from the run number + 100
docs: release steps for TestFlight
```
## Releasing

The one-time Apple setup (App IDs, the App Group, certificates and profiles)
is done; the old step-by-step guide for it is in git history as
`docs/RELEASING-IOS.md` if it's ever needed again.

**Branches:** `main` is what's in the App Store (or with Apple). Each
version is built on its own branch, `release/<version>` (for example
`release/1.1.0`), made from `main`:

- Work for the version is committed straight to its release branch, each
  commit naming the issues it deals with (CI and CodeQL run on every push).
- When it's ready, one PR takes the release branch into `main`; tag
  `v<version>` there, and start the next release branch from the new `main`.
  Keep the old release branch - it has the batch-by-batch history.
- A fix for what's live goes to `main` (bumping the patch version, `1.0.1`),
  then `main` is merged into the release branch so the fix carries on.

**Each release:**

1. Bump `<Version>` in `Directory.Build.props` on the new release branch
   (the display version, for example `1.1.0`). Build numbers are set
   automatically, so repeat uploads of one version are fine. Update
   `src/DashyNMS.Mobile.Shared/WhatsNew.md` to match: its `# 2.0.0` heading
   must be the new version, or What's new won't show. Keep it short, with a
   `{icon}` from the tab glyphs on each line.
2. Actions → **TestFlight** → *Run workflow* on the release branch (or
   `main` for a fix), and approve the run. Leave *Build number* blank unless
   you need a particular one.
3. The build appears in App Store Connect → **TestFlight** once Apple has
   processed it (typically 5-30 minutes). Test it, then choose it on the
   version page and submit.

**App Review notes.** From 2.0.0, keep this in the version's App Review
Information notes, because guidelines 1.2 and 4.7.1 ask where generated
content can be reported:

> Intelligence answers can be reported from the chat. Open More ›
> Intelligence (or the Intelligence tab, if pinned) and ask a question. Then
> press and hold the answer and choose Report this answer. With VoiceOver,
> use the answer's Report this answer action from the Actions rotor. Choose
> Offensive, Wrong or misleading, or Other. A preview shows the question,
> answer and reason, which can be edited to remove names and addresses. It
> then opens a draft email to dashynms@pckp.net, the address on the support
> page. Nothing is sent until the person sends that email.

**The workflow's secrets** live in the `iOS-Build` environment (only `main`
and `release/*` can use them, and a maintainer approves each run), not in
repository secrets:

| Secret | What it is |
| --- | --- |
| `IOS_DIST_CERT_P12_BASE64` / `IOS_DIST_CERT_PASSWORD` | The Apple Distribution certificate, base64, and its password |
| `IOS_PROVISIONING_PROFILE_BASE64` | The app's App Store profile (with the App Group) |
| `IOS_WIDGET_PROVISIONING_PROFILE_BASE64` | The widget's profile; without it builds ship the app alone |
| `APPSTORE_API_KEY_ID` / `APPSTORE_API_ISSUER_ID` / `APPSTORE_API_PRIVATE_KEY` | The App Store Connect API key the upload uses (the `.p8`'s whole contents) |

**Opt-in error reports and usage (#108)** go to the `dashynms-mobile` Sentry
project, separate from desktop's. Both `iOS-Build` and `Android-Build` have:

| Name | Kind | What it is |
| --- | --- | --- |
| `SENTRY_DSN` | variable | The project's DSN, built into store builds as `DASHYNMS_SENTRY_DSN`. A build without one never asks about sharing and sends nothing; CI and simulator builds never have it. |
| `SENTRY_AUTH_TOKEN` | secret | An organisation token that records each uploaded build as a Sentry release, `<version>+<build number>` - the name the app reports under |

`SENTRY_ORG` (`pckp`) and `SENTRY_PROJECT` are repository variables. The
privacy policy must say what's shared (#220) before a build with the DSN
reaches testers.

**Worth knowing:**

- The distribution certificate expires after a year, and the profiles with
  it: renew them and update the signing secrets.
- A build number App Store Connect has already seen is rejected. The
  workflow uses its run number + 100; if that collides, raise the
  `IOS_BUILD_NUMBER_OFFSET` repository variable or type a number in.
- `ITSAppUsesNonExemptEncryption` is `false` (standard HTTPS only), which
  skips the export-compliance question on each build. Revisit it if the app
  ever adds its own cryptography.

## Feature parity

What desktop has (from its code, README and the website), and where mobile
stands. ✅ done, 🟡 partly, ⬜ not yet, ➖ not planned for a phone.

| Area | Desktop | Mobile |
| --- | --- | --- |
| Sign-in, saved session, backup address, self-signed certs | ✅ | ✅ Also by scanning desktop's sign-in QR code with the camera, which always asks first |
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
| Open in browser / SSH / Telnet | ✅ | SSH and Telnet, in whichever app on the phone handles the link; no Open in browser |
| Health (sensors across all devices vs limits) | ✅ | ✅ Desktop's four categories, problems first |
| Groups and locations | ✅ | ✅ Counts and devices down; opens the Devices list filtered (editing stays on desktop) |
| Neighbours views | ✅ | 🟡 Every CDP/LLDP link, down ends first (desktop's own custom views stay on desktop) |
| Maps: network, geographical, custom | ✅ | 🟡 Geographical, on OpenStreetMap or desktop's tile server (network and custom maps stay on desktop) |
| Logs: event log, Graylog | ✅ | 🟡 Network-wide event log and alert log (Alerts → Logs); Graylog to come |
| Unimus config backups and diffs | ✅ | ✅ Device View's Config backups: read and search a backup, compare any two, share either through the share sheet (no Backup now) |
| Settings: poll interval, thresholds, device name style, theme/accent, server logo | ✅ | ✅ Poll interval, thresholds, device names, light/dark, timestamps, notifications (accent colour and server logo stay desktop's) |
| Update checks | ✅ | ➖ The stores handle it |

The website describes desktop only, and leaves out several things desktop
already has: Neighbours, Maps, Logs/Graylog, Unimus, the backup server
address and in-app updates. Nor does it mention mobile yet.

## Roadmap

Ordered by how much each adds on a phone, not by desktop's order.

What's left is the less everyday:

1. **Rules and templates**, read-only.
2. **Instant alerts**: a push from the server side, so alerts don't wait for
   the next background check.
3. **Store builds**: Play internal testing (TestFlight is in place, see
   above).

## Intelligence on the phone

Intelligence is split across two releases (re-planned on 2026-11-10, after
#297's groundwork check on 2026-10-31 against Core 2.0.0-ci.49 and
DashyNMS/mcp at `61161c4`).

**2.0.0: Apple's models on iPhone and iPad (#236).** Intelligence uses
Apple's Foundation Models framework. It runs on the device and, from iOS 27,
can optionally use Apple's Private Cloud Compute. This route was chosen
because:

- it needs no account, sign-in or API key, and costs the person nothing;
- on the device, nothing leaves the phone, so there's no third-party AI
  sharing to ask permission for (App Store guideline 5.1.2(i));
- it avoids the ChatGPT route's open questions: OpenAI's terms for a store
  app, and the in-app purchase rules;
- it doesn't wait on the DashyNMS/mcp changes below.

The constraints that shape it:

- Foundation Models is Swift-only. A small Swift framework wraps it, bound to
  .NET with Native Library Interop, built with xcodegen as `ios-widget/` is,
  and weak-linked so the app still runs on iOS 15.
- It needs iOS 26 or later, a device that supports Apple Intelligence, and
  Apple Intelligence turned on. Elsewhere Intelligence is hidden or says why.
- The on-device context is small: 8,192 tokens measured on iOS 27.2, and
  4,096 by Apple's notes for iOS 26.4. It covers instructions, tools,
  question, lookups and answer together. The current 24 MCP tools and GPT
  prompts don't fit, so the phone has five compact lookups over Core's
  clients that return short text, and its own short instructions. #570
  measured the budget on a device; see below.
- The model sits behind an `IIntelligenceModel` interface in Mobile.Shared.
  The pages, saved chats, Spotlight, reporting and export are shared, so later
  models need no new pages.

### What the on-device model can do (#570)

**Result: the overview question completed on the tested device.** Measured on 2026-11-10 with
the former Settings › Diagnostics › Intelligence check, on an iPhone Duo running iOS
27.2, a TestFlight Release build of `3d36e85`, against a LibreNMS server
with 946 devices and 208 open alerts. One question, "How is the network
doing?", asked once, so the times are a single reading and not an average.

| What | Measured |
| --- | --- |
| Context size (`contextSize`) | 8,192 tokens |
| Whole question, by the model's own count | 559 tokens in, 239 out |
| Instructions (285 characters) | about 60 tokens |
| One lookup's definition (137 characters, one argument) | about 30 tokens |
| The overview lookup's result (809 characters) | about 310 tokens |
| The answer (634 characters) | 239 tokens |
| LibreNMS's part of the lookup | 2.0 s |
| First words | 3.5 s after asking, lookup included |
| Whole answer | 6.1 s after asking |
| App memory | 248.8 MB before, 246.6 MB after |
| App size | 42.9 MB to 43.1 MB between the uploads either side |

How to read these:

- **The diagnostic counts suggest about 47 tokens of framing**
  when counting text as instructions: the 25-character question counted as
  53. The per-piece rows above have that taken off. The model's own count
  (`Usage` on the response, iOS 27) supplies the total for this reading. About 150 of the
  559 input tokens are framing, the lookup's schema and the call itself.
- **Network text costs about 2.5 characters a token**, not the four of
  ordinary prose, because host names, addresses and rule names split badly.
  Budget lookups in characters at that rate.
- **App memory did not rise in this reading.** The app grew
  by about 0.2 MB, which is the Swift bridge (the upload sizes also include a
  few unrelated changes).
- **The answer repeated the supplied counts.** Every count matched the
  lookup, and it named the same devices and alerts in the same order. It
  restated more than it summarised, and it dropped a leading number from
  several device names. So the lookups have to do the judging, and names
  must come from lookup data, not from the model's wording.

**The budget for the rest of #236.** Sized for the 4,096-token context of
iOS 26.4, which wasn't measured and is assumed to count tokens the same
way; iOS 27 then has room to spare.

| Part | Budget |
| --- | --- |
| Instructions | 150 tokens (about 400 characters) |
| Lookups | 5, each defined in 80 tokens or fewer: a name, one sentence and one argument |
| A lookup's result | 900 characters (about 360 tokens), whatever the network's size |
| Lookups per question | 2 at most |
| An answer | about 300 tokens; the detail dial moves it between 150 and 400 |
| Turns the model sees | The newest that fit in 80% of `contextSize`: about 3 on 4,096 and 7 on 8,192 |

That is 550 tokens fixed for instructions and lookups, and about 750 for
each turn with one lookup. Older turns stay on screen but are dropped from
what the model sees (#573). The 2.5-characters-per-token rate is an estimate
from this single lookup, not a safe upper bound. Other text and languages,
including Japanese, Chinese and Korean, may use more tokens. Read
`contextSize` at run time and never assume either figure.

For #573, if the model reports a context-limit error, discard the failed
session and retry the current question once in a fresh session without
older turns. Keep the earlier conversation visible and explain that it was
left out. If the question still exceeds the limit without older turns,
ask the person to shorten it or narrow its scope. Do not retry indefinitely
or mark the partial answer complete.

What this means for the lookups (#572) and the conversation (#573):

- A lookup puts what needs attention first: unacknowledged alerts before
  acknowledged ones, then by severity. The model repeats the order it's
  given.
- The instructions tell the model to copy names exactly. Links to devices,
  alerts and sensors (#228) come from the lookup's own data, matched to the
  answer, never from the model's spelling.
- Show that a lookup is running. LibreNMS took a third of the wait.

The check supports keeping the on-device model as the default for 2.0.0.
Private Cloud Compute (32,000 tokens, #575) stays what it was: an optional
way to get deeper answers on iOS 27, not a replacement.

Still to check on a device, under #571: the 4,096-token context on iOS
26.4, a launch before iOS 26 (CI proves Foundation Models is weak-linked,
not that the app starts), and Stop partway through an answer.

**2.1.0: ChatGPT, and Android (#577).** The person's own ChatGPT plan, as on
desktop, becomes a second model on iPhone and the first on Android (#576),
with desktop's full tool set. Two things gate it:

- **OpenAI's terms.** OpenAI documents plan usage for "open-source and
  locally hosted apps" and offers other apps a commercial partner programme.
  Project-owned app materials are now MIT licensed (see LICENSE and NOTICES.md).
  Confirm that the integration meets the applicable service terms before any
  ChatGPT build reaches testers; changing the licence alone does not verify that.
- **The DashyNMS/mcp changes.** Core already has the ChatGPT engine: the
  sign-in (`ChatGptSignIn`, `IdTokenValidator`), the Responses client, the
  conversation loop and its instructions (`IntelligenceConversation`,
  `IntelligenceInstructions`), consent, the detail dial, and the allowlist of
  24 tool names in current Core source (`IntelligenceToolAllowlist`), which
  must also be verified against the consumed package. The network tools are the
  missing piece. A phone can't start desktop's `dashynms-mcp` process, so the
  tools have to run in the app, from DashyNMS/mcp's `DashyNMS.Agent`. Against
  #221's four upstream needs:

| Need | Where it stands |
| --- | --- |
| A published package | Not yet: the mcp repository's CI doesn't pack or push it, and there's no `DashyNMS.Agent` on GitHub Packages |
| The host's own clients | Not yet: `AddDashyNmsAgent` registers its own `LibreNmsClient`, `UnimusApi` and `GraylogApi` with `AddSingleton`, not `TryAdd`, so certificate trust and the backup address wouldn't follow the app's |
| Plain options | Done: `AgentOptions` is plain; only the `DashyNMS.Mcp` host reads environment variables |
| A tool table safe for AOT | Not yet: the 24 attributed tool methods are found by reflecting over `[McpServerToolType]` classes, which iOS's AOT and trimming may strip |

If the package still can't run in-process once those land, the ChatGPT
route uses 2.0's compact lookups, extended.

### Intelligence settings (#571)

Settings › Intelligence is a pushed setup page that saves changes
immediately. Back returns to Settings and keeps saved choices. The entry is
hidden on Android and before iOS 26. Other unavailable states explain the
reason; when Apple Intelligence is off, Open Settings uses the public
app-settings URL and tells the person where to go.

Mobile consumers must use `IntelligencePreferences.Enabled`, together with
model availability, rather than Core's `IntelligenceEnabled` alone. Core's
desktop default is on. Mobile requires its own versioned on-device notice
marker before that flag can enable anything. This marker is separate from
Core's third-party consent fields. Cancelling the first notice leaves the
mobile gate off. Turning the feature off retains the notice acceptance.

Detail, colour indicators and single-chat mode use Core's setting keys.
The on-device detail choices are Brief, Balanced and Detailed. Legacy
styles are normalised, and Full reads as Detailed without changing the
stored value until the person chooses. Single-chat mode and Delete saved
chats are described under "Single-chat mode (#230)". The chat entry follows in #227.
The diagnostic check was removed under #595. The enablement control shows
the saved choice; there is no separate Ready or enabled status row. Availability
problems remain visible whether Intelligence is on or off, alongside save errors.
Installed-build acceptance for enabled, disabled and unavailable settings remains
pending under #595; keep the issue open until the user accepts the presentation.

Persistence requires Core's `ICheckedSettingsStore.TrySave()` acknowledgement
from package `2.0.0-ci.64` or later. A best-effort-only store cannot report a
saved change. Tests force a real temporary-file write failure and reload the
previous file for both first enablement and disabling after notice acceptance.

The page uses `SetupLayout` and `SetupPageLayout` for inset and Back ownership,
with no Save action. It refreshes availability on appearing and activation.
Shared tests cover each availability state, the opt-in notice, persistence,
legacy detail values, failed saves and the Settings handoff. Platform
compilation is checked in CI. Device checks remain outstanding: supported
and unsupported devices, Apple Intelligence turned off, resume, light/dark,
large text and VoiceOver, plus glass clearance on both Duo displays.

### Conversation engine (#573)

`IntelligenceConversationViewModel` keeps questions, partial answers and
lookup evidence in memory. It runs one question at a time. Stop cancels the
current attempt, and late callbacks cannot rewrite a finished answer.
Retry appends a new attempt with the original detail level. A per-question
detail choice does not change the saved default. Edit returns the original question while the
old partial answer stays visible. A lookup failure leaves a completed answer
with a warning. Refusals, context limits and unavailable models have fixed
messages, without native exception details.

Each question uses a fresh model session with explicit recent completed
turns. The input budget reserves instructions, five lookup definitions,
two 900-character results and output space within 80% of runtime context.
Older turns remain visible but may be omitted from the prompt. A context
failure retries once in a fresh session without history; another failure
asks for a shorter or narrower question. Character estimates remain only
estimates, so the native failure path is required.

Mobile supplies a main-thread dispatcher for streaming, lookup progress
and terminal state. Every question requires the mobile opt-in, an available
model and a signed-in connection. Connection events advance a generation
so an old connection reference cannot revive a run after a switch. Every
connection event interrupts the run and clears retained turns, including
sign-out, sign-in and reconnect. An immediate ownership gate blocks old-turn
copy, retry and edit while UI cleanup is queued. Only the latest queued
reset can authorise a new chat. Opt-out also interrupts the current answer.
Each question permits at most two actual lookup calls, including a
context-recovery attempt.

The engine never logs or uploads questions, answers or lookup evidence.
It keeps the composer's draft and the chosen detail (`Draft`,
`SelectedDetail`) so a saved chat can restore them, and `Restore` shows a
saved chat as a new revision. On app suspension, the host requests a short
iOS background reservation, awaits `InterruptAsync` for up to four seconds,
then saves the chat (below) for up to four more. Both expiration and
completion end the reservation. Native suspension and model-failure cases
still need device verification.

### Saved-chat storage (#222)

`IntelligenceChatStorage` wraps Core's `FileIntelligenceChatStore` (per-chat
files, written to `.tmp` and renamed, a damaged file renamed `.chat.bad`
while the rest load) in `FileSystem.AppDataDirectory/chats`. Each file is
AES-256-GCM under one random key in secure storage (`intelligence-chats-key-v1`;
iOS keychain "this device only", Android Keystore), as desktop's keyring
protector does. A key is only created when secure storage says there is none.
If it can't be read or stored, or the folder can't be excluded from backups,
the state is `Unavailable` with a reason for the user: nothing is written,
nothing plain, and existing files are left for when the key returns. A
missing key means a new one, and old files are set aside as they fail to
decrypt. iOS marks the folder with `NSURLIsExcludedFromBackupKey` every time it
opens; Android relies on the manifest's `allowBackup="false"`.

`OpenAsync` returns a store bound to the current generation, or null when
unavailable. Its calls block on file I/O, so run them off the UI thread. Either
sign-out choice awaits `WipeAsync`. It moves the generation on under the store's
lock before anything else, so a late save from an older store throws
`ChatStorageUnavailableException` and writes nothing. Then it removes the key and
the folder. Opening and wiping take turns over the key, so concurrent opens make
one key, and a key being created can't land after a wipe. Opens queued before
or started during cleanup are refused. A false key-removal result is checked
against secure storage.

Cleanup has to hold across a restart, so `WipeAsync` first writes a fixed-content
note, `chats.cleanup-pending`, beside the folder (flushed to disk), and deletes
it only after the key and the folder are both gone. While the note exists, from
a refused removal or a wipe cut off by a crash, `OpenAsync` retries the removal
before reading any key. It stays `Unavailable` until both removals and the note's
deletion succeed, then starts afresh under a new key. If the note's existence
can't be checked, it opens nothing and deletes nothing. `WipeAsync` returns
`Removed`, `Retained` (note saved: locked across restarts) or
`RetainedUntilRestart` (the note couldn't be written either, so the lock lasts
only for this run; later opens try to write it again). Sign-out shows the
matching warning with Retry cleanup and Continue signing out. The note sits
outside the folder, preferences and secure storage, so neither the wipe nor
"forget everything" (`ILocalDataWipe`) removes it first. `IChatCleanupFiles`
wraps the note and folder operations so tests can make each one fail. Logs name only the
step that failed. Chat text, scopes, keys and exception messages are never
logged. The chat list, autosave and restore are below (#229). Single-chat privacy mode
is #230. Unreadable files without the key and absence from device backups still
need checking on an iPhone and an Android phone.

Android chat-key access uses the same AndroidX encrypted preferences and Keystore
alias as pinned MAUI, with checked synchronous `Commit` for writes and removal.
Reading an existing key recommits it before accepting it, since a failed commit
may leave a value visible in memory. Chat-key reads propagate storage errors
without MAUI's credential-clearing recovery. Other credentials keep MAUI's
existing behaviour. Native persistence and failure handling still need device
acceptance; shared failure tests establish the storage's response to adapter errors.

### Saved chats (#229)

`IntelligenceChatListViewModel` keeps the conversation as saved chats in the
storage above. Chats are scoped per LibreNMS server
(`IntelligenceSavedChats.Scope`: `on-device`, a newline and the web root, which
Core hashes into the folder name), so another server never lists them; 2.1.0's
ChatGPT accounts (#577) add the account as desktop does. The list loads once per
connection, off the UI thread and after writes already queued, and reopens the
chat that was open into an empty transcript. It is grouped by Core's
`IntelligenceChatActivityGroups`, pinned first and then by activity. Activity is
a new question or a changed answer state; drafts, renames and pins don't
reorder the list.

`IntelligenceSavedChats` maps each turn to desktop's two messages. The answer
message keeps detail, outcome, message text, lookup activity, Core's typed
references, images and, in an `Exact filters` status card, the exact group and
location names Devices filters by. An answer saved mid-stream (`Generating`, or
`IsIncomplete`) reopens as Interrupted with its partial text. Restored
references pass the live checks again (a valid typed object whose key matches,
filters only for groups and locations); anything else is dropped and stays
plain text. Images pass the size and type limits again. Spotlight, looked-at
context, history and outfit in a file are kept as they were; there is no
Spotlight in phone chats yet (#231).

A saved chat is bound to the conversation's `ChatRevision`. New chat, reopening,
connection changes and sign-out move the revision before the transcript changes,
so a cleared or foreign transcript is never written into the chat. Captures also
need a current transcript, the scope the list was loaded for and the same scope
generation. Capturing runs on the UI thread and builds fresh lists.
`IntelligenceChatWriter`, a port of desktop's writer (desktop #321), serialises,
encrypts and writes off it, in order. A newer save of a chat replaces one still
waiting, so at most one save and one delete per chat wait. A delete drops waiting
saves of that chat and refuses later ones, so a late debounce or suspension save
can't bring it back. Sign-out drops everything waiting (`Invalidate`), and the
storage's generation refuses a write already under way. A server switch lets
the previous server's waiting saves finish in that server's scope. Draft and
detail changes save two seconds after they settle; streaming saves at most every
two seconds; a new question and a finished, stopped or failed answer are queued
at once; an unchanged chat isn't rewritten. Anything typed or answered while
the list is still loading is saved as soon as loading finishes.

Every save and delete returns what became of it (`ChatWriteResult`). A failed
or refused save leaves the chat marked for retry, so the next change and
suspension write it again even if nothing changed. A
delete clears the open chat at once, but the chat leaves the list, and VoiceOver
says it was deleted, only once its file is gone. If the delete fails, or a
sign-out, server switch or single-chat mode drops it from the queue, the chat
stays (or comes back with the list) and can be deleted again; a failed or
dropped delete stops refusing saves of that chat.

New chat and opening another saved chat share one leave decision
(`NewChatAsync`, `OpenAsync`), so the header, glass, sheet rows, long-press
menu, swipes and VoiceOver actions can't skip it. It queues the latest and
waits at most `LeaveWait` (two seconds) for the save that carries the chat's
current text, draft or detail (`RequiredSave`). Only a chat that is empty, or
whose save is confirmed written, is left without a question. A loaded chat
nobody changed counts as written; saves that only move the reopen marker, pin
or rename don't need confirming. A save that failed asks "couldn't be saved";
one still going when the wait ends asks "hasn't finished saving". Both offer
Try again, Discard chat or Cancel. A chat that isn't being saved at all
(single-chat mode, a transcript used in it, unavailable storage) gets the red
discard confirmation. The outcome is read from the save itself as well as from
the posted acknowledgement, so a save that has just finished counts and one
still waiting never does. After each question, the scope generation,
transcript revision and target are checked again, so a sign-out, server
switch, single-chat change or reload during the question cancels rather than
opening a chat from the old scope.

A damaged file is set aside by Core while the rest load, and the list says how
many couldn't be read. When the storage is unavailable the list's status is the
storage's reason and nothing is written; if reading fails, the list is read-only
so an empty list never replaces the files. Saved chats are read from the phone,
so they open offline; answer chips route to pages that show their own offline or
cached state.

Leaving after a failed or pending save offers Try again, Leave anyway or Cancel.
It explicitly warns that earlier saved history may remain and a pending write
may finish; this does not delete saved history. Private memory-only text keeps
the explicit discard confirmation. Edits to draft, detail or answer content
while waiting invalidate the leave decision, so an older snapshot cannot
justify clearing newer text.

Single-chat mode is described in its own section below (#230).

The chat list is `IntelligenceChatsPage`, a modal sheet opened from the header
or glass list button. On iOS 26+, the modal uses normal tab-bar glass
for Back and New chat on phone-sized and landscape windows. In a large portrait
window, New chat moves alongside the title using the same `ChatActionPlacement`
rule and `HeaderGlassGroup` as the main chat experiment. Back stays in normal
glass. Older iOS and Android retain labelled header buttons.

The opaque modal uses OverFullScreen on iOS to keep the presenting tab bar
attached for measurement through folding and rotation. `TabBarButtons.Show`
accepts the modal as its presentation page, hosting the normal controls above
the modal content. Resume activates the top modal first. The underlying chat
refuses to reclaim glass while a modal is present; closing Chats explicitly
reactivates the conversation. The list runs to the screen base, with native
scroll inset adjustment disabled, measured glass clearance inside the footer,
and a 20pt themed fade and matching spacer beneath the fixed header.
Back dismisses the modal; New chat uses the existing save/discard leave decision.
Rows have themed separators and use reveal-only swipes with a 48pt threshold,
so a shorter partial swipe can settle open. Rebinding a recycled row closes
its previous swipe. Installed-build acceptance remains pending under #593,
including swipe stability, large text and VoiceOver.

Tests in `IntelligenceSavedChatTests` cover scoping,
activity grouping, rename, pin and confirmed delete, restart restore of draft,
detail, model, evidence filters and images, streaming and Interrupted restore,
suspension, coalescing, delete, sign-out and wipe ordering, damaged files, an
unreadable key store and single-chat mode, plus changes made while loading,
failed and dropped deletes, failed saves retried unchanged, a reconnect in
single-chat mode, and the leave decision: a save held past the wait, a save
failing after it, declined discards, opening history after single-chat mode,
an answer arriving after the scope moved on, and a chat that follows the
Settings detail until it picks its own. Force-quit mid-answer, performance
with long chats, and the sheet's layout and accessibility still need
installed-device acceptance.

### Single-chat mode (#230)

Settings › Intelligence › Single-chat mode (Core's `IntelligenceSingleChatMode`)
keeps one conversation in memory: questions, answers, the draft, lookup
evidence, images and detail. Nothing from it is written, so a crash or
force-quit leaves nothing to restore. The saved-chat list is hidden and settings
are kept.

When the setting turns on, the list drops the writer's queue, moves the scope
generation on and records the entry synchronously, before its posted clean-up
runs. Every capture, and loading, checks the setting and that record, so a
debounce, streaming save or suspension save that runs in between writes nothing.
The record matters when the mode is turned on and off before the clean-up runs:
by then the setting already reads off, but the clean-up still marks the
transcript private before it handles the turn off. The chat that was showing is unbound, so text added in the mode
can't reach the file it came from. A write the writer had already started holds
only what was captured before the mode went on; it may finish, and that is the
only thing that can. A transcript that had text in the mode stays unsaved after
leaving it, including one started by a reconnect in the mode, until New chat.
Leaving it by New chat or by opening a saved chat asks before discarding it.

Turning the mode on doesn't delete saved chats. Settings › Intelligence ›
Delete saved chats is the separate choice. It is offered whether or not the mode
is on, confirms with the standard destructive wording, and goes through
`IntelligenceChatListViewModel.DeleteSavedChatsAsync` (`ISavedChatsDeletion`),
which owns the writer. Before anything is awaited, it drops waiting saves, moves
the scope generation on and empties the list. It then calls the storage's
sign-out wipe (`WipeAsync`), so the cleanup note, key removal and generation
refusal of a write already under way all apply. The chat on screen stays, but
it is unbound and marked unsaved, so none of the deleted history comes back as a
new chat. Settings says "Saved chats were deleted." only after a complete
removal. A partial one shows what remains locked and offers Try again, and the
list shows the storage's reason. Sign-out still wipes on its own.

In this mode the chat is also kept out of the app switcher
(`IntelligenceSnapshotShield`). It decides, and the head draws. Pages that show
the conversation implement `IPrivateChatSurface`: the chat, the saved-chat sheet,
the report preview and the enlarged graph. On iOS, `App` raises a cover window
(`IosSnapshotCover`) above the app's own window on `Window.Deactivated`, and
again on `Stopped` as a fallback, before the snapshot. The shield counts the
cover as up only when `Show` reports it installed; with no native window or
scene yet, or an exception, it stays uncovered and `Stopped` tries again. The
cover shows the app name on the system background and says the chat is hidden.
It is never key and takes no touches. It comes down on `Activated` or
`Resumed`, even if the mode or page changed meanwhile.

The keyboard lives in a system window that no app window can sit above, and its
predictive bar can show words from the private draft. So before the cover goes
up, every window of the scene ends editing without animation, taking the
keyboard down. It isn't raised again on return. The draft is kept and the user
taps the composer to carry on: bringing the keyboard back automatically could
raise it over a page the user has since left. The composer's glass (#227)
settles at rest, as for any dismissal. This applies only in single-chat mode
with the chat on screen, so it includes Control Centre and notification pulls. On Android the activity window has `FLAG_SECURE` while
the chat is on screen in the mode, re-evaluated on Shell navigation, modal pushes
and pops, and setting changes. Intelligence is hidden on Android for 2.0, and
MAUI's modal dialogs have their own windows, which the flag doesn't reach yet.
Other pages aren't hidden. `PrivateChatSurfaces` lists what each window shows,
its page and any sheets on the window's root or the current Shell section, and
`PrivateScreen` decides. Only content pages known not to be the chat leave it
unhidden. No windows, a window with no page, a missing Shell page, or a page or
container it doesn't recognise all count as the chat. Navigation, tabbed and
flyout wrappers count by the page they show.

`IntelligenceSavedChatTests` uses a store spy with UI work held back to show
zero store writes across streaming, a lookup, an image, draft and detail changes,
debounce and suspension, before and after the setting's clean-up runs. It also
shows that old chats survive the mode and are listed again after it, without the
private text. Two more turn the mode on and off before the list handles
either, with private text, an image and a draft (and in one, a server switch)
in between: the store gets no writes and the old chat is unchanged. Further
tests show that Delete saved chats removes every server's
chats, the key and the note, drops a waiting save and refuses a running one, and
never saves the open chat again. A removal that can't finish says so, and
retrying finishes it. `IntelligenceSettingsTests` covers the confirmation, its
decline, success only after complete removal, and Try again and Not now.
`IntelligenceSnapshotShieldTests` covers when the cover goes up and comes down,
a failed or throwing install that is tried again, when the window is secure,
and `PrivateScreen` treating anything unknown as the chat. The native cover,
keyboard dismissal and flag are built in CI only. Installed-device checks
remain: force-quit and relaunch in the mode restores nothing; the app-switcher
snapshot is covered and shows no keyboard or predictive suggestions; the cover
clears on return, leaving the keyboard down, the draft intact and the glass at
rest. The keychain and backup checks from #222 also remain.

### Chat page (#227)

More opens Intelligence as a relative route; pinning selects its retained tab.
Both the catalogue and the tab use the mobile opt-in and model-availability
gate. A hidden saved pin is retained until the next explicit pin edit, which
saves the visible selection. Availability is checked again on return and resume.
Direct routes show an explanation and refuse work when the gate is closed.

The page uses the setup layout for one top inset owner, a finite transcript
viewport and a measured composer overlay above the docked keyboard and glass.
The transcript footer reserves the overlay height plus 16pt, including a stable 44pt latest-message arrow slot. The arrow aligns with the
860pt capped chat column; hiding it does not change the footer height. SetupPageLayout still owns
the keyboard/glass gap.
New chat and Saved chats use glass on iOS 26+, with header actions elsewhere.
A chat revision also clears the composer draft on connection changes, including
when a retained page next activates. Ordinary page departure and return keep
the chat and draft. The transcript and draft are hidden while a connection
reset is queued. Back is present only for the pushed route. Command-N asks
the chat page to start a new chat, through the saved-chat leave decision below.

Ask clears an accepted question from the composer. Stop retains partial text;
Retry appends an attempt and Edit copies the original question into the field.
The detail picker applies to the next question without changing Settings. Until
the chat has its own pick it follows the Settings default: each activation
shows the conversation's detail without recording it as a pick, and Ask uses
the conversation's detail rather than reading the picker.
Copy exports only the chosen answer through the system clipboard. Starter
questions rotate on new chat and restore, hide while the composer has text,
and immediately submit through the normal ask guards. A stale suggestion or
non-empty draft refuses submission. Ordinary activation keeps the suggestions. Labels display plain text and
streaming updates are coalesced at 100ms intervals. Scrolling follows while
within 64pt of the end, otherwise Jump to latest restores following.

The presentation follows desktop's Sparkle navigation identity and separates
right-aligned user bubbles from left-aligned assistant answer bubbles. Both
transcript and composer use the measured viewport width, capped at 860pt; user bubbles
cap at 560pt and 85% of the column. A static themed accent wash provides an
ambient backdrop without animation. The page background owns the wash,
including behind safe-area gutters, header, composer and glass; the transcript
has no separate background plate or opaque viewport-top fade. The composer is
one rounded surface with a 48 to 140pt auto-growing input, compact detail choice and an accessible
Send control that becomes Stop while running. Lookup activity folds behind
an expandable summary. Successful answers omit the redundant Completed
line, while warnings and interrupted outcomes stay explicit. Copy is a rounded clipboard icon at the answer foot; the question owns an
Edit icon which refuses to overwrite a draft. The view model permits editing owned,
settled questions only while the normal ask gate is open. Assistant headings and repeated
reference chip rows are omitted; verified inline references keep their guards. A collapsed Open referenced item
chooser supplies accessible 44pt navigation targets without persistent pill rows. Installed-device visual acceptance remains pending under #227.

The initial page permits the existing client's cached lookup results; it does
not claim live authentication or fresh readings. A visible note says lookups
may be cached and asks readers to check readings and their age in LibreNMS.
Chats are saved locally using the encrypted store described under #222 and
#229; single-chat mode keeps its transcript in memory. Conversation data is
excluded from diagnostics. Saved-chat sheets are described under #229;
Spotlight remains under #231. Formatted answers and answer
reports are described under #228 and #574 below. Device acceptance for #227 and #573
remains open: test long streaming answers, Stop and suspension, both entry
routes and resume, a small/large phone and iPad, large text, VoiceOver, themes,
keyboard clearance and following while reading older text. No installed-device
result is inferred from shared tests or platform CI builds.

For the reuse decision, the Agent catalogue, the rules every shared tool keeps
and what counts as a finished migration, see
[Shared Intelligence tools](INTELLIGENCE-TOOL-REUSE.md). Device search
(`find_device`) runs DashyNMS/mcp's shared `DashyNMS.Agent.Search` package; the
other four lookups are still mobile queries, each with its own issue listed
there.

### Compact network lookups (#572)

`IIntelligenceLookups.Create()` supplies five read-only lookups over the
session's client: overview, device search, device summary, active alerts and
device sensors. Each reply is at most 900 characters. Rows lead with the
LibreNMS name in the person's device-name choice, followed by Core's marker,
for example `core-sw1 [[device:708]]`, so the model names devices rather than
writing "device 708". A device without a usable name reads
"Device 708 (name unavailable)". Alert rows keep their source-qualified
`alert:` or `fault:` key beside the `[[alert:ID]]` marker. Alert and overview
lookups name every alert's device from one device-list request, never one
request per alert; if that list can't be read, rows fall back to the alert's
own hostname and carry no device link. Omitted rows are counted. Device search
ranks exact, then starting, then containing matches over hostname, sysName,
display name and IP, ties by hostname, as the MCP agent does; its heading gives
the exact total and unfetched matches count as omitted. It searches by name or
IP only: a MAC address is refused before any request. Alerts put unacknowledged
items first, then severity. Sensor values are raw readings, not a health
assessment. Network labels cannot form a marker of their own.

`Create(evidence)` also reports the `IntelligenceEvidence` behind each accepted
reply, only for rows that fitted in the reply and only while the connection is
unchanged. Each item pairs Core's `IntelligenceInlineReference`, whose name is the
cleaned display label (at most 80 characters, no control characters or marker
brackets), with `Filter`: a group or location name exactly as LibreNMS returned
it. Devices filters by that exact name, so a chip never filters by its shortened
label. The record is plain data so saved chats (#229) can keep it.

Create a fresh lookup set for each new model session. A set refuses calls
after the connection object changes and discards replies arriving from the
previous connection. Requests have a 15-second deadline and fixed failure
text; cancellation propagates to the caller. No questions, replies or
exception details are logged. The former diagnostic and its separate overview
lookup were removed under #595; the historical measurements above are unchanged.
The conversation uses this interface.

### Formatted answers (#228)

Answers render as Markdown: headings, paragraphs, bullet and numbered lists,
tables that scroll sideways, fenced and inline code, quotes, rules, bold,
italic and links. `IntelligenceMarkdown` in Mobile.Shared parses the text into
blocks, following desktop's `MarkdownView`; `IntelligenceMarkdownView` in the
head draws them as native labels and spans. No HTML is interpreted, nothing is
executed, and Markdown image syntax shows only its description, so no image is
fetched. No renderer library was added, so the Licences page is unchanged.

Core's inline markers resolve through `IntelligenceInlineMarkup` against the
turn's lookup evidence (and earlier turns of the same chat), so a chip shows the
LibreNMS name and, where known, its status in words. A well-formed object marker
whose id no lookup returned reads as Core's fallback ("Device 12") and links
nowhere. A malformed marker stays literal text. Status colour follows
Settings › Intelligence › Colour status indicators and always accompanies the words.

Verified device, alert or fault, sensor, group and location references remain
inline without duplicate chip rows beneath the answer. A collapsed
Open referenced item chooser provides accessible navigation with 44pt targets. `IntelligenceAnswerActions` opens it through the
existing pages: Device View, alert detail with the qualified key, the device's
Sensors section, or Devices filtered by the evidence's exact group or location
name, never the chip's display text. A group or location without that name stays
plain text. It refuses a reference not in the turn's evidence, a mismatched or
invalid target, and any turn from a previous connection. Destination pages handle a target that has
since been deleted. Links open in the system browser only if Core's
`WebSearchCitations.TryGetLink` accepts them (http or https, no user
information) and the answer still contains them.

Graph images and web citations arrive only with the ChatGPT route (#577), so
their display is tested with recorded answers. `IntelligenceAnswer.Images`
carries Core's `IntelligenceImage`; PNG and JPEG within Core's 2 MB limit, at
most eight, show inline and enlarge in a modal view. An enlarged preview
clears its image when its turn loses connection ownership and rechecks on
return, so the modal cannot keep the previous network’s evidence visible. A model
that cites sources hands its finished output items over unformatted in
`IntelligenceAnswer.Output`, in the Responses `message` shape Core's own
conversation reads. When the turn completes, `IntelligenceAnswer.ToMarkdown`
joins their text as Core does, with Core's `WebSearchCitations.FormatMessage`
turning each accepted citation into a visible Markdown link; the turn shows,
copies and vets that Markdown. Answers without message output, including every
on-device answer, keep `Text` unchanged. A recorded citation is tested through
this model, conversation and renderer path. Saving images
with a chat follows under #222 and #229. Copy answer copies the original
Markdown, markers included.

The on-device instructions now ask for Markdown and markers within the
400-character budget. Golden tests cover a table with markers, malformed
markers, safe and unsafe links, nested lists, quotes, code and a recorded
citation. Device acceptance remains pending for layout, large text, VoiceOver,
themes, sideways table scrolling, chip navigation and the image view.

### Answer reports (#574)

Each answer offers Report this answer, as App Store guidelines 1.2 and 4.7.1
ask of generated content, and as Google Play's AI-generated content policy
will ask once Android has Intelligence. Press and hold the answer bubble for a
menu with Copy answer and Report this answer. VoiceOver offers Report this
answer as a custom action on every part of the answer. The reasons are
Offensive, Wrong or misleading, and Other. Only a finished, stopped or failed
answer with text can be reported, never one still streaming.

`IntelligenceAnswerReporter` (Mobile.Shared) captures the chosen turn, its exact
question and answer and the chat revision before the reason sheet opens. It
checks them again when a reason is chosen. A sign-out, connection change, new
chat or changed text in the meantime discards the report and says that
nothing was sent. `IntelligenceReportViewModel` drives the modal preview and
keeps watching the chat while it is open. If ownership is lost, it clears the
question and answer, disables sending and shows that the report was discarded.
It checks once more just before handing anything over.

The preview shows the recipient, the subject, the reason, and the question
and answer in editable fields, with a reminder to remove device names and
addresses. `IntelligenceReportFormat` builds both the preview and the email,
so the email holds exactly this:

```
Reason: <reason>

Question:
<question as shown>

Answer:
<answer as shown, Markdown and markers included>
```

Nothing else goes in: no lookup results or evidence, other turns, server
address, token, app or device identifiers, images or attachments. Line endings
are normalised and control characters other than new lines and tabs are
dropped. A body over 8,000 characters is never cut off. The person is asked to
remove text instead.

Open in email app hands the draft to `IEmailComposer`. In the head this is
MAUI's email API: Mail's compose sheet on iOS, or a mailto intent on Android.
The person reviews and sends the email there, so the app sends nothing itself
and has no reporting backend. The address is `AppLinks.SupportEmail`
(dashynms@pckp.net), the one already published on the support page and in the
privacy policy. `mailto` is declared in `LSApplicationQueriesSchemes` and the
Android `queries` element so availability can be checked. Without an email app
the preview says so and offers Copy report, which copies the same recipient,
subject and body. A composer failure says it couldn't open, without native
details. The App Review notes under Releasing say where reporting is.

Shared tests cover the exact body and draft, exclusion of evidence and other
turns, redaction, cancelling the reason sheet, connection and new-chat changes
while choosing and while previewing, running, empty and other-chat turns,
stopped partial answers, unavailable and failing composers, the length bound
and control-character cleaning. Device acceptance is pending. Check the long
press on touch, the VoiceOver action, the preview at large text in both themes
and with the keyboard, Mail's compose sheet, and the no-mail-account path.

## Upstream notes

Things found while porting that would be better fixed in the desktop repo.
Everything raised up to v1.1.0 was fixed in DashyNMS/desktop#209, and mobile
now uses Core's own code for it: settings saving with no listeners, the data
folder off Windows, Windows-safe Unimus file names, Core on plain net9.0,
every device's event and alert logs, a device's last poll, the alert
notification rules, and certificate trust (DashyNMS/desktop#189).

The app asks before trusting a certificate it can't verify, in Core's words
(`CertificateTrust.DescribeForPrompt`), at sign-in, when the saved session
is restored, and when testing Graylog or Unimus. A background alert check can't ask,
so it just fails until the app is opened.

Nothing is outstanding. Add new findings here, and raise them on
DashyNMS/desktop.
