# DashyNMS Mobile

.NET MAUI app for Android and iOS, using DashyNMS Core.

- Core is the pinned `DashyNMS.Core` NuGet package in `src/DashyNMS.Mobile.Shared`; no desktop checkout is needed.
- `src/DashyNMS.Mobile.Shared` holds view models and services in plain .NET; `src/DashyNMS.Mobile` is the MAUI head with pages and platform adapters only.
- Use sealed classes and XML doc comments explaining why.
- Before changing Intelligence tools, lookup results, model instructions or chat features, read [Intelligence](docs/INTELLIGENCE.md). The 2.0.0 target is desktop parity on Apple Intelligence, with the allowed differences recorded there. Build order is the Order section of #236, the only 2.0.0 Intelligence epic: take its first unchecked item.
- Before creating, changing or reviewing a mobile page, read [Page design](docs/DESIGN.md), including layout, navigation, forms, pickers, accessibility and motion.

## Repository conventions

- Before changing build, test or publication workflows, read [the developer guide](docs/DEVELOPMENT.md). Before changing release notes, read its [Release notes](docs/DEVELOPMENT.md#release-notes) section.
- Before changing shared conventions or security checks, read [Repository standards](docs/REPOSITORY-STANDARDS.md); update the canonical source and every affected consumer.
- Create or find an issue describing the bug or intended change before opening any PR, including dependency, documentation and CI changes. Every PR must reference its issue.
- When filing an issue, set its organisation type, relevant labels and milestone. Follow [issue categorisation](https://github.com/DashyNMS/.github/blob/main/docs/REPOSITORY-STANDARDS.md#issue-categorisation) for milestone choices and API verification.
- Work on a branch; use one issue per PR into `main`, then squash-merge. The PR title becomes the commit subject on `main`.
- PR titles, branch commits and issue titles use `type(scope): what changes, as the user would notice it`. Read [Commits, pull requests and issues](docs/DEVELOPMENT.md#commits-pull-requests-and-issues) for this repository's types, scopes and examples.
- Start the description in lower case, omit the final full stop and aim for a complete subject under 72 characters. Separate two scopes with a comma; omit the scope when there is no single area. Document a new scope in the developer guide in the same PR.
- Word issue titles as the result the fixing PR delivers. Epics end with `(epic)`. Keep issue numbers out of titles; start the PR body with `Closes #NN` when resolving an issue, then explain what changes and how it was checked.
- Mark breaking changes with `!` and explain the migration and version implications for affected consumers.
- Dependabot uses `build(deps): ...` for every ecosystem. Authenticated Dependabot may use generated capitalisation and exceed 72 characters.
- Use British spelling in user-facing text. Keep real device names, hostnames, addresses, credentials and other identifiable network data out of issues, PRs, commits, logs, screenshots and fixtures. Use `example.net` and documentation address ranges.
- Commits, PRs and release notes carry no AI attribution or `Co-Authored-By` lines.
- Read `LICENSE` before changing or redistributing repository material. Preserve third-party licences, copyright notices and provenance.

## Validation and releases

- Shared tests need no MAUI workloads: `dotnet test tests/DashyNMS.Mobile.Shared.Tests`. Restore requires `DASHYNMS_PACKAGES_TOKEN` with `read:packages`.
- Building the head needs the `maui-android` or `maui-ios` workload plus the Android SDK or Xcode; CI covers both.
- Changes to app behaviour also update `docs/guide` in the same PR. Before editing the guide or wiki publishing, read [User guide and wiki](docs/DEVELOPMENT.md#user-guide-and-wiki).
- A change users would notice adds or updates its line in `docs/releases/<version>.md`, using the version in `Directory.Build.props`, in the same PR. GitHub release notes come from that file.
- `src/DashyNMS.Mobile.Shared/WhatsNew.md` is a separate, curated set of user highlights with its own formatting. Update it when a change merits highlighting; it does not duplicate every release-note entry.
