# DashyNMS Mobile

.NET MAUI (Android + iOS) app that reuses DashyNMS desktop's Core library.

- `external/desktop` is a git submodule of DashyNMS/desktop. Run `git submodule update --init` if it's empty. Never edit files under it here - upstream fixes go to the desktop repo.
- `src/DesktopNMS.Core.Portable` compiles the desktop Core sources for net10.0; `src/DashyNMS.Mobile.Shared` holds view models and services (plain net10.0, unit tested); `src/DashyNMS.Mobile` is the MAUI head (pages + platform adapters only).
- Tests need no MAUI workloads: `dotnet test tests/DashyNMS.Mobile.Shared.Tests` and `dotnet test tests/DesktopNMS.Core.Tests`.
- Building the head needs the `maui-android` / `maui-ios` workloads plus the Android SDK or Xcode; CI covers both.
- Match desktop's style: British spelling in user-facing text, XML doc comments explaining *why*, sealed classes.
