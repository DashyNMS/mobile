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

# iOS (macOS with a matching Xcode)
dotnet workload install maui-ios --version 10.0.402.1
DEVELOPER_DIR=/Applications/Xcode_27.1.app/Contents/Developer dotnet build src/DashyNMS.Mobile -p:MobilePlatform=ios -t:Run
```

Or open `DashyNMS.Mobile.slnx` in Visual Studio or Rider.

iOS builds use Xcode 27.1 and workload set `10.0.402.1`, matching the
[.NET for iOS 27.1 release](https://github.com/dotnet/macios/releases/tag/dotnet-10.0.1xx-xcode27.1-10018). Both CI and TestFlight
select this toolchain explicitly. The iOS 27.1 SDK enables the Duo's full
display and vertical system bars; changing page padding cannot remove the
compatibility strip produced by older SDKs. The minimum supported iOS
version remains 15.0.

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
The earlier attached-layout/native-item refresh remains, but #372 is still
unresolved: cold launch can have five native items with no visible pill
until rotation. Keep that failure distinct from portrait presentation and
verify both paths on the Duo. Simulator CI does not reproduce this timing.
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
one column. Inventory has expandable entries, Expand all / Collapse all,
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
below that, with 12pt gaps. Schedule and Cancel scroll clear of the bottom
glass. Check one device and multiple selected devices, Start now and a
later date, duration presets and custom values, denied scheduling, keyboard
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

Edit dashboard and its setup/picker children keep native page safe areas
and add only the extra glass-row clearance (#391). Their scrolling controls
disable duplicate native content insets; the editor footer no longer adds
an unrelated 96pt gap. Check top labels and the last item/action in every
orientation, both displays, keyboard use, reordering and return from setup.
This change is scoped to dashboard editing, not other tab systems.

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
- **scope:** the area, in lower case: `alerts`, `devices`, `device-details`, `dashboard`, `widgets`, `notifications`, `background`, `logs`, `graylog`, `maps`, `health`, `neighbours`, `ports`, `graphs`, `rules`, `settings`, `diagnostics`, `sign-in`, `demo`, `core`, `ios`, `android`, `deps`. Separate two with a comma, or leave it out when there's no single area. A new area can add a scope; add it here in the same PR.
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

Where Batch 7 (Intelligence, #236) stands, checked for #297 on 2026-10-31
against Core 2.0.0-ci.49 and DashyNMS/mcp at `61161c4`.

**Core has the engine.** The pinned package has everything except the network
tools: the ChatGPT sign-in (`ChatGptSignIn`, `IdTokenValidator`), the
Responses client, the conversation loop and its instructions
(`IntelligenceConversation`, `IntelligenceInstructions`), saved chats
(`FileIntelligenceChatStore`, `SavedIntelligenceChat`), consent, the detail
dial, and the allowlist of 22 tool names (`IntelligenceToolAllowlist`). So
#222 to #234 can be built in Mobile.Shared on Core alone, with desktop's view
models as the spec.

**The network tools can't be used yet.** A phone can't start desktop's
`dashynms-mcp` process, so the tools have to run in the app, from
DashyNMS/mcp's `DashyNMS.Agent`. Against #221's four upstream needs:

| Need | Where it stands |
| --- | --- |
| A published package | Not yet: the mcp repository's CI doesn't pack or push it, and there's no `DashyNMS.Agent` on GitHub Packages |
| The host's own clients | Not yet: `AddDashyNmsAgent` registers its own `LibreNmsClient`, `UnimusApi` and `GraylogApi` with `AddSingleton`, not `TryAdd`, so certificate trust and the backup address wouldn't follow the app's |
| Plain options | Done: `AgentOptions` is plain; only the `DashyNMS.Mcp` host reads environment variables |
| A tool table safe for AOT | Not yet: the 39 tool methods are found by reflecting over `[McpServerToolType]` classes, which iOS's AOT and trimming may strip |

Until those land, #221's on-device check (a Release build on an iPhone and
an Android phone, the four tools' results, size, first-question cost and
memory) can't be run.

**Recommendation.** Move Batch 7 to 2.1.0 rather than hold 2.0.0 for it.
First make the three remaining changes in DashyNMS/mcp (publish the package,
`TryAdd` the clients or take them from the host, and give the tools a
generated or hand-written table), then run #221 on devices. The fallback, if
the package still can't run in-process, is re-implementing the commonest
tools over Core's clients in Mobile.Shared, which is the most work.

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
