# DashyNMS Mobile

A cross-platform (Android and iOS) companion to [DashyNMS desktop](https://github.com/DashyNMS/desktop)
for keeping an eye on your [LibreNMS](https://www.librenms.org/) network from
your phone. It's built with .NET MAUI and runs on **the same Core library as
the desktop app**, so API handling, models, alert logic and fixes are shared
rather than rewritten.

> **Status: early.** Sign-in, a dashboard, devices, device detail, open alerts
> (with acknowledge/unacknowledge) and settings are in place. See the
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
- **Settings**: the connected server and version, the *Server stores
  timestamps in UTC* option (the same setting as on desktop), and sign-out.

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
  head only supplies adapters for navigation, dialogs, the browser and
  secure storage.

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
dotnet build src/DashyNMS.Mobile -p:TargetFrameworks=net10.0-ios -t:Run
```

Or open `DashyNMS.Mobile.slnx` in Visual Studio or Rider.

CI (`.github/workflows/ci.yml`) runs both test suites, builds an Android APK
(uploaded as an artifact) and builds for the iOS simulator.

To ship an iOS build to testers, see [docs/RELEASING-IOS.md](docs/RELEASING-IOS.md).
The manually triggered **TestFlight** workflow signs the app and uploads it to
App Store Connect, with no Mac needed.

## Roadmap

Roughly in order, following what desktop already has:

1. **Alert notifications.** Background polling (Android WorkManager / iOS
   background fetch) using desktop's `AlertChangeDetector` and
   `NotificationStateStore`, with per-severity settings and quiet hours.
2. **Health**: sensors (dBm, temperature, fans) coloured against their limits.
3. **Graphs**: device and port graphs via Core's `IGraphsApi`.
4. **Device detail, fuller**: ports and neighbours, event log, maintenance
   windows, rediscover.
5. **Groups and locations**, plus pinned and recently viewed devices.
6. **Integrations**: Unimus config backups and Graylog messages (Core already
   has both clients; they need keychain-backed secret stores like the
   LibreNMS token's).
7. **Store builds**: Play internal testing (TestFlight is in place, see above).

## Upstream notes

Things found while porting that would be better fixed in the desktop repo:

- `UnimusExport.FileNameFor` relies on `Path.GetInvalidFileNameChars()`,
  which only contains `/` and `\0` off Windows. That means `:`, `*` and `?`
  survive into file names exported from a phone. A fixed, Windows-safe set
  would make the names portable.
- Core targets `net9.0-windows` only because of its DPAPI secret stores.
  Moving those (and `AddDesktopNmsCore`'s registration of them) into the WPF
  project would let Core target plain `net9.0`/`net10.0`. Mobile could then
  use a normal `ProjectReference` instead of compiling the sources.
