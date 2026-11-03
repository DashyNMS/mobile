# Page contracts and acceptance evidence

Read this reference when changing an existing setup page or dashboard editor,
or checking which device acceptance supports a reference implementation.
[DESIGN.md](DESIGN.md) owns the general rules for every page. The contracts
below preserve page-specific behaviour during migration. Acceptance records
establish only the evidence stated, not every configuration in the checklist.

## Setup page contracts

### Graph

`GraphPickerPage` is the stepped setup reference. Headers are "Choose a device"
and "Choose a graph". The selected Device section has Change device, then the
optional card title is the first draft field. Device and device-wide graph
choices use natural order. Ports retain interface order; their graph
descriptions still use static order, a known gap against DESIGN.md. An empty
graph source explains that the device has no graphs. Save and stored fields
retain their existing draft contract.

### Sensors

`SensorPickerPage` uses a scrolling card-title header, finite sensor list and
fixed Save. The header starts below the fade. Each accessible row includes
sensor name, device, reading and selected state, with its checkbox excluded
from duplicate announcements. Reopening retries a failed load; another Save
retries a failed save and retains the draft. See the sensor persistence
contract below.

### Ranked cards

`TopCardSetUpPage` has no choose step. Its heading is the card kind's title.
Card title comes first, then Rows, Rank by and, for Top errors, Quiet ports.
Each field uses `SetupSection`; the switch keeps its own accessible description.
Its draft, Save and stored fields are unchanged.

### Spotlight

`SpotlightPickerPage` opens from a log card's Dashboard Spotlight line or its
Set up action in Edit dashboard. Both pass the selected widget ID.
Its header is "Spotlight". Whole network is a separate immediate-action
section with "Every device. Applies straight away." and Use whole network.
Below it, "Or keep it to" contains Devices/Group/Location, search, selected
count and prompt, status, then a finite list. Devices allow several selections;
a group or location is a single choice. Missing saved choices remain at the
top until unticked. Fixed Save waits for all sources to load. Whole network
saves and returns immediately; other choices wait for Save.

### Setup batch, #407 to #414

These contracts were accepted with the batch recorded below. Each page uses
`SetupLayout` and follows DESIGN.md's action hierarchy.

- **Add card #407:** search uses `SearchField`; the catalogue uses the choice
  grid breakpoint. Category counts, unavailable reasons, multi-selection,
  add-below position and automatic setup of a single Sensors or Graph card are
  unchanged. Add is the fixed primary action and names what it adds.
- **Graylog #408 and Unimus #409:** one connection form shape: enablement,
  server, login or token, then options. Save is fixed and drafts stay as now.
  Test connection checks the draft, saves nothing and may add a trusted
  certificate to the draft only. Forget saved password or token removes the
  stored secret immediately after confirmation and stays subordinate.
- **Sensor definitions #410:** the switch and source are the draft; installed
  version, last check and status are read-only. Update now acts on the saved
  source and explains when an unsaved draft disables it.
- **Thresholds #411:** value rows stack when crowded. Reset to desktop's
  defaults changes the draft only; Save validates and applies; Back discards.
- **Settings sections #412:** all seven sections share one layout and save
  automatically. Dependent visibility, permissions, privacy text and the
  notification rule list remain unchanged.
- **Rule picker #413 and Graylog device picker #414:** immediate choosers
  with picker statuses. Already-added rules state why they cannot be added.
  Any device is the scope choice above the results. Suggestions, group
  headings, the result cap and natural order are retained.

## Dashboard editor contract

`CustomiseDashboardPage` uses the dashboard's adaptive columns and saves
immediately. More offers Duplicate where supported, Add a card after,
Move earlier/later and Remove. Removal retains Undo. Sensors and Graph open
their pickers, Top cards open ranked-card setup, and Event log/Graylog open
Spotlight. Add card automatically opens setup only for a single Sensors or
Graph card. Returning from any child refreshes the saved cards.

The six-dot grab handle uses `Aspect="None"` and is hidden from screen readers.
`Aspect="Uniform"` previously stretched its geometry into clipped bars.
The shared `DashboardToastView` supplies the Undo transition. Its custom
property binding uses an explicit `Binding` object.

## Sensor persistence contract

Sensors drafts its title and selections, commits them together on Save and
discards changes on Back. Opening without a card ID creates a card only on
Save; an explicitly added card remains when its later setup is cancelled.
Empty selections are supported. A failed save keeps the draft. Saved sensors
absent from the server response are retained. This contract was accepted
under #395.

## Acceptance evidence

The first five records are from 2 November 2026; the setup batch was accepted
on 3 November 2026. Build numbers refer to TestFlight 2.0.0. Full PRs and issues
retain implementation and review details.

| Scope | Accepted implementation | Evidence and limits |
| --- | --- | --- |
| Shared setup #406 | PR #418, `b4fd1983c99cb06c25f8afe9d5a1d234a2262890`, following #417 | User confirmed manual checks on build 212 and authorised closure and the #405 checkbox. Individual device, OS and accessibility results were not supplied. |
| Graph #396 | PR #421, `8259758dc1385d8258adac49097d1ff656d4092e` | User accepted after PR review; shared tests and Android/iOS simulator CI passed. Device and screen-reader configurations were not recorded. Review left port graph descriptions in static order and device-row accessible operation unverified. |
| Ranked cards #397 | PR #425, `7f0e1f2217fa628209a63c89c2871e69108c9fcd` | User approved closure and #405 completion after device testing build 216. Shared tests and Android/iOS simulator CI passed. Individual configurations and screen-reader results were not recorded. |
| Spotlight #398 | PR #428, `89eac7478bb70267e10d15b925fd7658193a6196` | User approved closure and #405 completion after device testing build 217. Shared tests and Android/iOS simulator CI passed. Individual configurations and screen-reader results were not recorded. |
| Dashboard editor #399 and #430 | PR #431, `6d37a55c8dd729f5b1363c11508cb3770ffd618e`, following #401, #404 and #427 | User said the result looks good and authorised both issue closures and their #405 checkboxes. Shared tests, Android/iOS simulator and CodeQL passed. TestFlight run 41519437048 successfully built and uploaded build 219. Individual device configurations and screen-reader results were not supplied. |
| Setup batch #407 to #414 | Add card #440, Graylog #435, Unimus #434, sensor definitions #436, thresholds #437, settings sections #441, rule picker #439 and Graylog device picker #438 | User approved all eight issue closures and completion of epic #405 on 3 November 2026. Each PR completed Claude Opus 5.5 review and required CI checks. [TestFlight build 220](https://github.com/DashyNMS/mobile/actions/runs/41553258282) built and uploaded successfully. Individual device configurations, diagnostics and screen-reader results were not supplied. |

Editor review #430 confirmed three gaps: log-card Set up reached a form that
could not load it, the introduction started under the heading fade, and the
footer omitted the visible Undo toast's height. PR #431 fixed all three.
These findings inform the general navigation, scrolling and overlay rules in
DESIGN.md. Acceptance of one page does not complete other page migrations in
#405 or establish their device results.
