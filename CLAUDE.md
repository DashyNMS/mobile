# DashyNMS Mobile

.NET MAUI (Android + iOS) app that reuses DashyNMS desktop's Core library.

- Core is consumed as the pinned `DashyNMS.Core` NuGet package in `src/DashyNMS.Mobile.Shared`; no desktop checkout is needed.
- `src/DashyNMS.Mobile.Shared` holds view models and services (plain net10.0, unit tested); `src/DashyNMS.Mobile` is the MAUI head (pages + platform adapters only).
- Tests need no MAUI workloads: `dotnet test tests/DashyNMS.Mobile.Shared.Tests`. Restore requires `DASHYNMS_PACKAGES_TOKEN` with `read:packages`.
- Building the head needs the `maui-android` / `maui-ios` workloads plus the Android SDK or Xcode; CI covers both.
- Match desktop's style: British spelling in user-facing text, XML doc comments explaining *why*, sealed classes.
- PR titles, commits and issue titles: `type(scope): what changes, in plain words`. One issue per PR, squash-merged, `Closes #NN` in the description, issue titles worded as the result the fix delivers. Full standard and scopes: docs/DEVELOPING.md, "Commits, pull requests and issues".
- Commits, PRs and release notes carry no AI attribution or Co-Authored-By lines.
- Never put real device names, hostnames, addresses or other identifiable network data in issues, PRs, commits, screenshots or test fixtures.
