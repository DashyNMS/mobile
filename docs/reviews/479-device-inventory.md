# Device Inventory review, 4 November 2026

Refs #479 and #442. Baseline main 6ad258e7e866f7a228821c2567a592eddcb1268a.
The prepared isolated checkout was clean at that commit. Live issue bodies and
all comments were fetched: both issues OPEN, no comments, no open PRs. No remote
Inventory branch or existing partial PR was found. Earlier green merged work
is the baseline, not device acceptance. Read CLAUDE, DEVELOPING, DESIGN,
DESIGN-REFERENCES, the reconciled assessment and sibling assessments.

## Ownership recorded before editing

The parent authorises this sole writer for shared DeviceSectionPage work.
The shared page and DeviceDetailView templates consume SectionRow. Inventory
alone opts into the new InventoryLayout property through InventoryIndex.
No Core package/API, source request, navigation, storage or permission changes.

| Shared consumer | Contract retained |
| --- | --- |
| Availability #469/#530 | Measured body base, independent footer, unknown percentages and outages |
| Sensors #470/#531 | Definitions, finite readings, neutral unassessed states |
| Resources #471/#533 | Own units/bars, source-specific unsupported handling |
| Ports #472/#534 | Approved grid/window policy, saved filters and actual ifName graph action |
| Neighbours #473/#535 | Graph/readout selection and theme |
| VLANs #474/#536 | Membership and source-error distinctions |
| FDB #475/#537 | Actual VLAN tags and optional metadata permission handling |
| ARP #476/#538 | Raw MAC and primary source failures |
| Routing #477/#539 | All protocol facts and source-local failures |
| Wireless #478/#540 | Reused SensorLayout, units and explicit unknown health |
| Inventory #479 | One-column tree, expansion, port hiding, search and exact serial copy |

No other checkout was changed, including the dirty #475 checkout. Shared
geometry, activation, observers, non-Port gestures and all sibling presentation
flags remain unchanged. Parent owns independent full review and CI judgement.

## Complete scoped assessment

C means source-compliant, correction identifies a confirmed source defect,
N/A means inapplicable, and U means unverified. Native acceptance is U for
all rows regardless of source disposition.

| Concern | Evidence and disposition |
| --- | --- |
| Pattern and modes | C searchable grouped pushed detail; Inventory always one column, unlike other sections. Full list has nested chassis/module/optic/port entries, summary and tree controls. Overview has count and three highlights, opens full Inventory. Empty success hides overview card. |
| Long content | Correction: DeviceSectionPage generic grid truncates title, caps Detail at three lines and constrains Class to 180pt/two lines. Trigger is long name, firmware/serial or unknown class at narrow width/large text. Inventory-specific triggers wrap title/detail and span facts over value columns, moving Class into the content stack. Overview replaces truncated title/value row with complete wrapping facts. Expected complete existing source text without horizontal competition. |
| Header controls | CORRECTED IN SOURCE under the user’s 5 November “Approve as proposed”: Back, Search and one Tree options glass action (three total); header icon fallback opens the same measured panel. Fixed heading/Done precede finite scrolling tree controls. InventorySummary stays in the list header. Native acceptance remains UNVERIFIED. Per-row expansion and Copy serial stay contextual. |
| Tree ownership | C loader calls Core InventoryTree.Build and depth-first DescendantsAndSelf. Mobile does not reconstruct containment. Full details below. |
| Expansion/search | C InventoryRows.Visible preserves matching descendants and ancestors through collapsed branches while search is active; clearing restores collapsed set. Hidden ports and their subtrees remain excluded from search until toggled on. Expandability counts visible children. Same-device refresh preserves collapsed indices and port switch. Changing device/section clears both. No durable expansion setting is added. |
| Selection/restoration | N/A selectable rows, editor, comparison and draft Save. CollectionView SelectionMode None. Expansion is session-local state, not a selected device. Existing search/query lifetime remains unchanged. |
| Source failures | C one primary request. Loader maps 404 to empty; 401/403, timeout and 500 propagate to RunAsync/ErrorLine/Retry. Failed refresh retains prior Inventory rows with error; no freshness claim. No enrichment or stronger permission dependency is introduced. Mixed independent reads N/A. |
| Load states | C BusyCard first load, explicit error/Retry, suppressed false empty during load/error, successful empty and unmatched search distinguished by EmptyText. U cold/refresh timing, offline and native retry focus. No durable Inventory cache exists. |
| Routes | C DeviceDetailViewModel.OpenSectionAsync passes DeviceId, section and device name to relative registered DeviceSection route. Devices pinned/More, narrow/split detail, Dashboard, alert/device and neighbour links can reach the owner detail. Back returns to actual caller. Inventory rows have no child destination. U each native route and retained stack. |
| Search/keyboard | C SearchField/ButtonSearch, header/glass/Command-F entry, actual drag/completion/unfocus dismissal and query retention; replacement/programmatic scrolling must retain focus. Matches searches existing title/subtitle/class/detail, including serial and firmware. No search/export semantics change. U native keyboard and focus. |
| Copy/export/security | C OnCopySerial keeps explicit tap, nonblank guard, raw Serial CommandParameter and Clipboard.SetTextAsync without trimming or formatting. Clipboard OS restrictions/failure handling remain existing platform behaviour. No export exists in this mode; no new export or action permission is inferred. Credentials, server text and storage unchanged. |
| Geometry/fades | C finite SectionBody/list, existing measured PlaceControls base, footer obstruction and 20pt header fade reservation. Native scroll adjustment Never and single inset ownership remain. No arbitrary geometry offsets added. U H/B/V, deep indentation, camera clearance and final row separately. |
| Lifecycle | C activation restores glass/watchers; disappearance removes subscriptions. One view-model lifetime self-subscription updates command availability on busy transitions; no new native observers. U tab return, child return to owner detail, fold/rotation and resume without Refresh, one glass owner and no placement loop. |
| Accessibility/themes | C themed labels, explicit class text, descriptive switch and serial action, existing labelled expansion controls. Overview joined speech excludes child duplicates; full section leaves facts and buttons separately accessible. U target sizes, button roles, contrast, focus order, speech, maximum text and long unbroken strings. |
| Motion | C existing ReducedMotion search policy, no new animation. U repeated transitions and reduced-motion on/off. |
| Other DESIGN rules | N/A fixed commit placement, immediate-save settings, chooser selection, form validation, numeric units, graph series, selected sheets and destructive confirmation. Inventory is read-only except explicit clipboard writes and local tree controls. Existing navigation and state contracts are preserved. |

## Core source contract and limits

Pin remains DashyNMS.Core 2.0.0-ci.52. Confirmed provenance is Core CI run 52,
commit 5f7bde53ac05ea75f7cb6dd06a8fae62606f0769. Inspected that revision's
Models/InventoryEntry.cs, Topology/InventoryTree.cs and Api/ResourceApis.cs.
GetInventoryAsync calls inventory/{deviceId}/all, collection inventory, once.
No port/metadata endpoint is needed to classify or display the authorised rows.

ContainedIn is entPhysicalContainedIn and names entPhysicalIndex, never
entPhysical_id. Zero, self-parent and missing-parent entries become roots.
Duplicate indices keep the first entry. Siblings sort by ParentRelPos, negative
positions last, then Index. A placed-index set breaks cycles; unplaced cycle
members are promoted to roots without dropping distinct indices. Mobile retains
Core depth-first ordering, including its policy for malformed hierarchies.
Index identifies expansion state; it is not a human module/slot number.

DisplayName uses trimmed Name, then trimmed Description, then Entity {Index}.
The existing mobile projection joins distinct nonblank Description, Model and
Manufacturer; Detail labels raw Serial and FirmwareRev. Serial itself retains
exact source bytes. Class is raw server text, including unfamiliar values;
IsPort is only case-insensitive class port. Classes have no numeric units or
health judgement. Blank class is not invented as a known type. Summary groups
existing Class values, falling back to Unknown for null, blank or whitespace.

The DTO also exposes HardwareRev, SoftwareRev, Alias, AssetId, IsFru, VendorType,
ManufactureDate, IfIndex and AdditionalData. This pass does not reinterpret or
add those undisplayed fields. Missing typed IDs/defaults, complete real payloads
and vendor-specific metadata remain U. Typed synthetic tests establish only the
projection contract, not live payload completeness. No internal ID is relabelled
as a human field. Core parser and hierarchy are not copied into mobile.

## Review, CI and native evidence

Full intended diff is reviewed against current main and DESIGN, including all
shared consumers; staged whitespace check is required before commit. New CI-only
regression covers different database/physical indices, raw serial whitespace,
firmware and unknown class. Existing InventoryRows tests cover collapsed search,
port hiding and expandability. No local build, test, restore or validation script
was run. Shared tests, Android/iOS and security CI on the final head are pending.
Independent parent review is pending. DESIGN already covers these fixes; no new
reviewed durable lesson beyond #515/#530/#536/#537 is added.

Every native check below is separately UNVERIFIED. Record final SHA/build/OS,
using synthetic example.net identities and documentation IP ranges only.

| Matrix | Required verification |
| --- | --- |
| Devices | Duo outer/inner, portrait/landscape; ordinary iPhone portrait/landscape; light/dark and maximum text in every combination |
| Geometry | Measure host H, pill bottom B, viewport bottom V and V minus B in common coordinates, separately for side/bottom/top glass. Independently reach complete last row; check first heading/fade, camera, short/long/deep trees and empty/error content |
| Tree | Multiple chassis, nested modules/optics, port-only children, missing parents/cycles/duplicates, blank/unknown class and long names/serial/firmware; expand/collapse all and individual branches, refresh and search then clear restore expansion |
| Sources | Empty, loading, slow, 404, 401/403, 500, offline, failed/successful refresh and Retry; retained rows remain accompanied by failure; slow old-device request cannot overwrite new device |
| Keyboard | Header/glass/Command-F, serial/class/firmware search, no matches, clear; typing replacement/programmatic scroll retains focus; actual drag/completion/unfocus dismiss with query retained; keyboard-open last row and restored base |
| Actions/accessibility | VO/TB complete facts, stateful expansion, named switch and copy actions, exactly one activation, raw copied bytes and platform clipboard failure; no false row navigation; overview opens Inventory and Back returns to caller |
| Lifecycle | Every route above, tab/child return and background/resume without Refresh, fold/rotation during load/search/refresh, repeated motion/reduced-motion transitions, one glass owner |
| Shared smoke | All eleven consumers above, especially Ports graph/filter return, neighbour graph/theme, VLAN/FDB permission/source errors, ARP, Routing and Wireless |

Check Tab bar buttons and Top line diagnostics for loops or overlap. CI cannot
establish native acceptance. No merge, watch, issue closure, epic tick or release
is performed by this implementation sub-agent.

## Independent review and approved proposal

Opus 5.5 Medium reviewed e2d5756. Its medium glass-action finding is accepted:
page-wide tree controls are not exempt from the pushed-page glass rule.
The proposed single Tree options action opens a compact measured panel with
Expand all, Collapse all, Show ports and Done, retaining current commands and
session state. Android/older iOS use a header Tree options icon and the same
panel. The user approved this exact proposal on 5 November 2026 (“Approve as proposed”). It is now implemented in source; approval does not establish device acceptance.
Availability must follow loaded source rows and actual command availability,
including hidden ports and empty search results, with commands disabled during loading. Valid retained rows after a failed refresh remain actionable once not busy, with the error retained and no freshness claim. Device measurements and final independent review are pending.

Low source findings are corrected: blank class no longer reserves a caption in
the full list, overview speech follows visible Title/Class/Subtitle/Detail order,
and whitespace class summaries group under Unknown. Exact copied serial bytes
remain untouched. These corrections remain preserved alongside the approved Tree options implementation.

## 5 November implementation disposition

The sole-writer checkout remains t3-cc2626a6 on the existing PR branch.
Before editing, inspected PlaceControls and all eleven consumers listed above:
CoveredEdges owns camera/system clearance; SetupActionPlacement and measured
pill/glass/keyboard bounds own overlay clearance; Refresh owns viewport base;
ListFoot owns scrolling clearance, including FilterCard’s actual visible height
through exit. FilterCard remains the single overlay, with its existing finite
ScrollView, focus reveal, Done and keyboard policy. Ports picker bindings,
saved sort/group preferences and the approved #472 grid policy are preserved.
The shared glass action adds optional disabled state, defaulting to enabled for
all existing callers. Inventory’s callback rechecks active owner and live source/
busy state. Activation remains idempotent with no additional native observers.

Availability uses loaded source rows, never filtered row count. Hidden ports and
no matches retain Tree options. Source-empty hides it; busy closes the panel and
disables commands. Expand/Collapse inspect actual visible expandable branches;
search-forced expansion cannot be collapsed until search clears. Done only
closes. Per-row expansion, session state and raw Serial clipboard bytes remain
unchanged. CI-only shared regression exercises these transitions, including
retained rows after failure and later successful empty refresh.

All exact native H/B/V and V−B measurements, panel bounds and final-row reach
with keyboard open/closed remain UNVERIFIED. All Duo outside/inside portrait/
landscape and ordinary iPhone orientations, themes, largest text, VO/TB,
fold/rotation, tab resume, child return and exact clipboard checks in the full
matrix above remain UNVERIFIED. No local build, test, restore or validation
script was run. Final-head shared, Android/iOS/security CI and fresh independent
Opus 5.5 Medium full review are pending and owned by the parent. No merge,
watch, issue closure, epic tick or release is authorised by this implementation.

CI at 86cb1d8 found CS0266 in the new disabled glass-action alpha assignment:
the conditional used double literals, while UIKit requires NFloat. Changed
only those literals to float, preserving the intended opacity. Replacement CI
is pending; no local build or test ran. Native disabled-state appearance remains
unverified.

Fresh Opus review at 86cb1d8 found seven issues. The CI alpha compile error was
already fixed at 80463a5. The header icon now uses OpenTreeOptionsCommand with
CanExecute notifications, letting IconButton own its enabled look and state.
Expand/Collapse use a page-local disabled-opacity style, not a shared LinkButton
change. Disabled overflow UIActions carry the native Disabled attribute as well
as the tap guard. Busy is included in the glass cache only when Tree options is
present, so other sections no longer rebuild glass on every refresh. Per-row
expansion is again allowed during refresh; only page-wide options disable, as
approved. Corrected the stale Unknown-summary sentence. Shared tests cover
command availability and row expansion during a pending refresh. New panel
stack layouts explicitly opt out of inherited safe areas. Fresh full review
and replacement CI remain pending; every native result remains unverified.

User device feedback, 5 November 2026: all good apart from the content not
reaching the base of the screen. The shared base correction and its cause are
recorded in [the #469 assessment](469-device-availability.md#base-correction-for-469479-5-november-2026).
It changes only DeviceSectionPage's viewport and footer ownership; the approved
Tree options, expansion, Show ports, serial copy and summary are unchanged.
Native base and final-row checks remain U.
