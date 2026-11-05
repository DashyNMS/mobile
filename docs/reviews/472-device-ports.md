# Device Ports review, #472

## Approved compact cards on main 3bbac99, 5 November 2026

This section supersedes the 4 November revision below, including its two
portrait columns, many landscape columns at an estimated portrait width, and
every raw fact on the card. Those sections remain history for their own heads.
The baseline is origin/main `3bbac9978cda9f48020c530ce179c46657ba6b1c`, in a
new isolated worktree; no earlier work is replayed.

**User feedback and approval.** The user reported that the cards were far too
long. Each card should show only the port name and description, the operational
state, speed, traffic in/out, errors in/out and VLANs. Normal displays show one
card per row and larger displays two at most. The user then explicitly approved
the parent's rendered mock-up as proposed: 14pt padding, a 4pt left status edge,
the standard radius, a semibold 17pt name over a muted 14pt description, a
status row with `● Up` at the start and speed at the end, a three-column table
(Traffic/Errors, IN ↓, OUT ↑) and a VLANs footer. Errors cards say `Up · errors`
in red, with positive error values in red. This is source approval only, not
device acceptance.

**Lesson recorded in DESIGN.md.** A port card is for scanning a list, not for
auditing one port. The audit facts doubled card height without helping anyone
compare ports. They remain model data for search, sort, grouping and status.

### Implementation and preserved contracts

| Concern | Source disposition |
| --- | --- |
| Columns | `PortColumns.Count` reuses `DashboardColumns.Count` with a 340pt minimum, the list's 8pt gap and at most two. Non-finite widths give one. The Duo's outer display gives one and its inner display two where the pane is at least 688pt. The portrait estimate, landscape floor-fit and trailing remainder are removed; cards fill their track. Other sections keep 340pt/three columns. Corrected; physical widths U |
| Card hierarchy | `PortReadout` puts a 4pt `BoxView` edge inside the ordinary 1pt Card border, which clips it to the 16pt radius. The page's Ports border trigger now only clears padding, so it no longer sets a 2pt stroke or status stroke. Name, description, status/speed row, divider, Auto/star/star table, divider and VLANs footer, with no fixed heights or line caps. Corrected |
| Status palette | Unchanged classification. Green Up, red `Up · errors` (the label was previously `Errors · link up`), amber Down, the same gray Disabled for admin shutdown and disabled monitoring. Unknown and `Up · readings unavailable` stay neutral, using theme stroke and text. The edge and label use one trigger each, as before. Positive error cells use live theme Critical text. Corrected |
| Readings | Missing or invalid traffic says Unavailable; non-up traffic says Not shown; missing speed says Speed unavailable; errors keep full precision as `n/s`, so 0.001 never reads 0/s. Overflow and NaN guards unchanged. Corrected |
| VLANs | Verified in Core: `/devices/{id}/ports?with=vlans` fills typed `Port.Vlans` (802.1Q numbers, `Untagged`). The card lists positive memberships, unique and ascending. If the device reports no memberships at all, a direct ifVlan is labelled Access/native VLAN. Otherwise a port without memberships says Not reported, following Core's `VlanMembership.HasMembershipData` rule (a trunk can report ifVlan 1). No extra request, fleet permission, dependency, internal ID or invented tag. Corrected |
| Removed from the card | ifName repeat, raw operational/admin states, the monitoring flag, the extra heading, the `Open interface graphs ›` line and the visible missing-ifName explanation. The matching readout still says graphs are unavailable. Deliberate per approval |
| Speech | `PortActionDescription` speaks name, description, status label, speed, traffic in/out, errors in/out and VLANs in visual order, then the action or the unavailability reason. All visible labels stay decorative. Corrected; native order U |
| Action | Unchanged: one transparent native button of at least 44pt, spanning both columns, with an explicit empty style and transparent Normal/Disabled and tinted Pressed/Focused states. It routes the actual ifName with the same DeviceId. Without an ifName there is no button. `OnRowBindingContextChanged` is unchanged. Compliant |
| Search, sort and group | `Value`, `Detail`, `Subtitle`, `PortStatusText`, `LinkPortIfName` and `PortFacts`, including the VLAN fallback/trunk, are unchanged, so matching, the seven sorts, four groupings, saved preferences and the legacy rank/traffic/error totals are unchanged. Compliant |
| Overview | `DeviceDetailView` keeps its layout. `PortOverviewIdentity` is now set by the loader, with the same name/ifName/alias lines. Traffic lines are unchanged for the positive-traffic rows it selects. It shares the status-label wording. Compliant |
| Other modes | Availability, Sensors, Resources, Neighbours, VLANs, FDB, ARP, Routing, Wireless, Inventory and EventLog templates, flags and policies are untouched. The window-size subscription stays as a harmless placement refresh. Compliant |

**PR #551 coordination.** #551 is open, unmerged and owns the shared section
base (`SectionListPlacement`, the viewport/footer code in `PlaceControls` and
two XAML comments). This change touches only the later Ports span/margin block
and the row Border's Ports trigger. It does not replay, revert or conflict with
the #551 geometry. Whichever merges second must keep both; #551 is not merged here.

### Validation and pending native evidence

The full intended and staged diffs were reviewed against DESIGN.md and
`git diff --check`. No local restore, build, tests, scripts or agents were run.
New CI-only tests cover the column breakpoint (one, two, maximum two, non-finite),
compact speech order and contents, explicit missing speed and non-up traffic,
`Up · errors` with per-side positive errors, typed VLAN ordering/uniqueness,
the direct-only label, the device-wide membership rule through the loader, and
the existing palette, overflow and tiny-rate cases. Shared-test, Android and
iOS CI, the parent's review and every device result are **U**.

| Check | Required evidence, all U |
| --- | --- |
| Duo outer, inner portrait and landscape; ordinary iPhone; fold and rotate | Column count and card widths: one on outer and ordinary phones, two on the inner display only when each card is at least 340pt |
| Compact layout | Card height, aligned table columns, no clipping at maximum text, status edge clipped by the radius, long description and VLAN wrapping, light and dark |
| States | Up, `Up · errors` with a positive side in red, Down, both Disabled causes, Unknown, incomplete Up, missing speed, invalid traffic, partial errors |
| VLANs | Payload memberships compared with the card; device without memberships shows Access/native VLAN; a port without memberships on a device with them shows Not reported |
| VoiceOver/TalkBack | One readout per card in visual order with the action; no button on a card without an ifName; 44pt target; recycling across mixed states |
| Behaviour | Search, every sort and grouping, relaunch persistence, graph entry with the actual ifName and return, refresh/failure, final card clear of the pill with #551 |

Use synthetic example.net names and device IDs 7 and 8 only. Refs #472 and
#442; the parent owns review, CI, merge and acceptance. #472 stays open.

## Approved revision on main 2010d7b, 4 November 2026

This section supersedes historical presentation and geometry-compliant findings
below. Historical provenance remains evidence for its original heads only.
Live origin/main was `2010d7b44cc73027d0ffbea72a2a686c4c859918`; the task
worktree was clean and no open PR existed. PR #510 is merged, head
`52f88f5e3938e22977476fd2dd69444c24bf8707`, squash
`f220f15f7e09fda091f870a55faffbb9488b939e`. No old work is replayed.
The separate dirty #475 worktree and unrelated branches are untouched.

The user approved `/tmp/mobile-442/ports-proposal.html` and its revised Markdown
contract with the explicit correction, "Admin shutdown and monitoring disabled
should be the same otherwise approved". Both now use the same gray Disabled
border/label. Raw states and the monitoring flag remain independent facts.
The user also authorised a responsive portrait-width estimate from live logical
window dimensions and page share. The portrait estimate subtracts base 16pt
gutters; measured camera/pill gutters constrain current landscape space only.
There is no stored orientation width, hard-coded display size or card height.
Split views and landscape side-pill changes can alter the estimate. Native
width preservation is approximate and UNVERIFIED.

### Consumers and boundaries

`DeviceSectionLoader.PortRow` supplies `SectionRow` to full
`DeviceSectionPage` and `DeviceDetailView` via `DeviceSectionCard.Ports`.
The overview still selects five positive-traffic rows and preserves totals,
up/down/unknown counts and Ports tile semantics. `PortArrangement` consumes
PortFacts, not display fields. Existing rank/state, natural name, index, alias,
VLAN fallback/trunk, traffic bytes/s and speed sort/group facts are unchanged.
New PortDisplayFacts are separate from searchable Fields, preserving Matches.
PortSummary/PortDescription remain compatibility properties for existing tests;
new visible/spoken projections use PortFactsDescription/PortActionDescription.
`OnRowBindingContextChanged` restores the existing outer link gesture for every
non-Port row and removes it for Ports, including recycled cards. PortReadout
supplies the one native button with no enclosing gesture.

Shared host owners remain Availability #469, Sensors #470, Resources #471,
Ports #472, Neighbours #473, VLANs #474, FDB #475, ARP #476, Routing #477,
Wireless #478 and Inventory #479, plus EventLog compatibility. Their layout
flags, loader ordering, source facts, native viewport/inset policy and lifecycle
remain intact. The 340pt/three-column policy is unchanged for other sections.
Neighbour graph/split/reverse links, VLAN/FDB/ARP facts, routing states,
wireless formatting, inventory expansion/ports/copy and resource/sensor/availability
projections retain their contracts. Graphs #480/#481, Graylog and backups use
specialised routes. EventLog normally uses Logs. Setup #405 acceptance is unchanged.

The available Core Port model confirms nullable raw states, byte/s octet rates,
errors/s rates, nullable long bit/s speed, nullable direct IfVlan and bool Disabled.
The pin remains 2.0.0-ci.52. Available sibling source is corroboration, not proof
of the pinned binary. No local restore or pin change was made; CI must confirm
the exact package API. A missing boolean payload cannot be recovered from this
typed bool; "Monitoring disabled: No" describes the model flag, not proof of
payload completeness. No AdditionalData inference or extra fetch was added.

### Current whole-scope dispositions

Compliant/corrected below means source inspection. Every device result is UNVERIFIED.

| Concern and actual trigger/mode | Location, observed/expected behaviour and disposition |
| --- | --- |
| Full Ports, long names/aliases and max text | Page previously used one coloured summary. PortReadout now stacks wrapping title, label and all approved facts. Corrected; no height/line cap. Two-column physical feasibility remains unverified |
| Full page portrait/landscape, cold landscape/fold/resize | PlaceControls and PortColumns use live window shorter dimension scaled by page share, less base 32pt gutters; current landscape clearance is applied only to available width. Portrait two tracks, landscape floor-fit with trailing remainder and no three-column cap. Corrected Ports-only; estimate limits above apply |
| Up/errors/down/disabled/unknown and contradictions | PortRow retains IsUp-first PortState/RowStatus/rank. Display-only palette maps positive existing errors red before green; both disabling causes gray only when non-up; down amber; missing/testing/unrecognised neutral. Raw source tokens remain visible. Corrected |
| Traffic, speed, errors and VLAN | PortDisplayFacts uses existing bit formatter after finite/nonnegative and multiplication-overflow guards. Positive speed only; errors show each valid zero or partial side. Non-up traffic says not shown; direct VLAN absence says not reported. Complete traffic/error validity gates green. Corrected, no utilisation/discards/membership inference |
| Graph action, no ifName | PortReadout native transparent Button fills the card content; Ports border padding moves into decorative facts. One joined description includes identity, states, all facts and action. Missing ifName is a plain joined readout without button/chevron. Corrected; native traits/activation unverified |
| Overview/tile | DeviceDetailView uses wrapping identity/alias, display status and directional traffic, with matching compact speech and parent Ports action. Existing selected highlights/counts/tile unchanged. Corrected hierarchy; no nested graph command |
| Sort/group/search/preferences | Seven sort and four group choices, defaults, enum keys, null-picker handling and immediate writes unchanged. Search domain unchanged. Done only closes. Compliant |
| Open Sort & filter with wheel/keyboard, short landscape/max text | Old unbounded stack replaced by Auto header/Done and star ScrollView body. MaximumHeightRequest uses SectionBody height less measured overlay clearance and top margin. Focused picker scrolls into view after focus/frame change. Footer retains actual panel height through exit. Corrected source; native picker/Done bounds unverified |
| Loading/empty/error/offline/cache/refresh | Existing BusyCard, refresh, Retry, errors suppressing empty, source-empty versus unmatched preserved. Ports retains last-loaded rows through refresh/failure without timestamp; no freshness claim. Overview can retain prior card while initial device request runs/fails; fresh cards load after success. Compliant with recorded limitations |
| Device handoff/source IDs | Existing generation/serial pending guard clears on ID/mode change and rejects old results/errors. API still selected device ports excluding Deleted with existing permissions/token. Waiting for old request can delay new one. Compliant; no mutation/new storage |
| Header/search/glass/lifecycle | Central IActivePage, idempotent watchers/observers, Back/search/filter ownership, iOS26 glass and older/Android header unchanged. ButtonSearch opens only from controls/Command-F; native drag/completion/unfocus retain query, replacements keep focus. Compliant in source; all native returns/resume unverified |
| Viewport/insets/final card | Inherited #530 SectionBody-native pill/occupied measurements, top-only iOS safe area, UIScrollView Never, finite Refresh and one footer owner retained. Ports stage margin narrows only horizontal tracks. No extra bottom translation/insets. Compliant source dependency, rejected native base extent not accepted |
| Keyboard already open on activation | Inherited activation may lack keyboard frame until another notification. Not proven fixed by a scrolling panel. UNVERIFIED dependency; measure before acceptance |
| Themes/contrast/speech/motion | Known status borders use StatusColor; neutral uses theme-aware normal card stroke. Labels use readable StatusText and facts stay neutral. Linked cards have one native button description; nonlinks have a separate readout description. Native action >=44pt, ReduceMotion retained. Contrast, speech order, recycling, wrapping and gesture/drag behaviour require native evidence |
| Editors/server writes/export/graph redesign | N/A. No Ports editor, Save draft, extra endpoint, export or graph presentation change approved |

### Review of 66b0ffa and corrections

Claude Opus 5.5 Medium found no merge blockers and four medium findings.
The implicit Button Disabled fill was substantiated from the app style: fixed
with an explicit empty style and local transparent Normal/Disabled states;
Pressed/Focused use the existing translucent tint. Neutral borders previously
used the text-colour fallback: fixed with a Ports-only theme-aware card-stroke
trigger. The landscape calculation incorrectly subtracted current camera
insets from a portrait estimate: fixed by subtracting base 32pt gutters only.
The 393x852 example with 75pt current side clearance now fits three 176.5pt
cards, covered by a CI-only column test. The accessibility recycling concern
was plausible rather than proven: removed the parent Grid description entirely;
only the linked native button or the separate nonlink label carries speech.
VoiceOver/TalkBack recycling and traits remain UNVERIFIED.

Lower findings: overview speech now matches visible compact facts; window size
subscriptions are paired across activation/departure; filter-scroll resize
re-reveals its focused picker. Moved durable Ports guidance under adaptive
accessibility. Full-precision error values remain deliberate to preserve the
reported rate and avoid zeroing tiny positives; verify long values wrap in the
two-column cards. The trailing strip outside the scrolling stage remains the
approved stable-width remainder, with headings/empty text centred in that stage;
verify dragging there and report usability problems. Repeated alias is retained
as an explicitly labelled source fact. These choices reject optional presentation
changes, not demonstrated defects. Short landscape panel space and Done/picker
bounds still need physical measurement. Fresh full-diff review and final CI are
pending; no device result is claimed.

Self-review also found that summing two finite positive error rates could
overflow and incorrectly choose green. Display-only error detection now checks
each source independently, with a CI regression; legacy ranking stays unchanged.

### Validation and pending native evidence

Opus 5.5 Medium re-reviewed the full diff at db78cbe. No proven blocker;
one medium trigger-precedence risk remained: both Ports triggers set Stroke.
Rather than rely on unverified same-priority trigger restoration, the colour
now has one owner: only the known-status MultiTrigger sets Stroke. Thickness
and padding stay in the Ports trigger; neutral uses the Card style's live theme
stroke. PortHasStatusBorder drives the condition. The full and overview neutral
status labels likewise use live theme text by default, with a single known-state
colour trigger. Native mixed-status recycling and live theme switching remain
required verification steps. A tiny portrait estimate could create impractically
many columns in an unusually small pane; no arbitrary minimum width or column
cap was introduced against the approved adaptive policy. Record actual pane and
card widths during split/fold tests. Final full-diff review and CI remain pending.

Code self-review covers the complete intended main-to-head diff against DESIGN,
the explicit staged file set and whitespace. No local builds, tests, scripts or
agents run. New CI-only tests cover visual states and contradictions, both
Disabled causes, partial errors, zero, invalid/overflowing rates, real ifName,
unchanged search domain and adaptive columns. Existing command routing and
PortArrangement/preferences tests remain relevant. Parent Opus 5.5 Medium
independent full-diff judgement is PENDING. Final-head CI is PENDING.
No merge/watch/release/issue closure/epic tick. #472 remains open under #442.

For every check below record final integrated SHA, CI/TestFlight build number,
device and OS, theme, text size and assistive settings. Final review SHA is the
PR head, not baseline 2010d7b. Build/OS/results are currently unavailable.

| User trigger/configuration | Required evidence, all UNVERIFIED |
| --- | --- |
| Ordinary iPhone and Duo outer/inner, portrait/landscape, cold landscape then fold/rotate/resize | Window/page logical bounds, actual gutters, column count, first/final card widths before/after; portrait two columns; landscape roughly stable cards constrained to pane; report split/side-pill estimate delta |
| Short/long lists, max text, both themes, VO/TB, Reduce Motion | Full facts without clipping/line caps; one joined readout/action, >=44pt target, no missing-name button/chevron; overview parent action and compact hierarchy. Report physical two-column failure rather than changing policy |
| Every pane/pill mode, first/final cards, tab/child return and resume | Native SectionBody height H, pill bottom B, Refresh/List viewport bottom V, V-B delta, first heading and complete final-card bounds; final card clears glass and measured panel |
| Search header/glass/Command-F, filtering/programmatic scroll vs actual drag, no matches/clear | Query/order retained; filtering keeps focus, real drag/completion/unfocus dismiss; no swipe reveal or duplicate glass/header commands |
| Sort/Group pickers and Done at max text, wheel/keyboard show/hide, rotate/fold while focused, resume with keyboard already active | Panel/header/body/Done and actual keyboard/wheel bounds, focused picker reachable, Done fixed/reachable, footer reserves actual visible panel through exit once; viewport restores without double translation |
| Every sort/group and saved relaunch, graph and caller return | Exact seven/four choices/order/defaults/auto-save, natural/index/VLAN/type/alias facts; actual ifName route and same DeviceId, query retained after graph Back, Devices narrow/split/Dashboard/alert actual caller |
| Cold/cache/offline/denied/404-empty/genuine empty, refresh failure/success/Retry, slow device7 then8 | Honest errors and retained last-loaded Ports facts, no stale rows under new ID, permissions/credentials/read-only behaviour preserved; distinguish 404 compatibility-empty from source-empty |
| Raw source values and contradictions | Compare payload bytes/s with displayed bits/s, zero/partial/missing/nonfinite/overflow traffic/errors, speed/VLAN missing, oper-up/admin-down/monitoring-disabled, both non-up disabling causes same gray label. No invented complete VLAN membership |

Use synthetic example.net and edge.example.net, device IDs 7/8 only. Visual
approval is not device acceptance. Parent owns sequential CI/review/merge and
later delivery; this implementation does not close #472.

Refs #472 and epic #442. Baseline main `2c7f32f`. Read live issues,
CLAUDE.md, full DEVELOPING.md, DESIGN.md and DESIGN-REFERENCES.md before
assessment. Routine guide choices were authorised. This is a prepared PR,
without merge, release, watch, issue closure or epic ticks.

## Assessment before implementation

| Concern | Baseline finding | Disposition |
| --- | --- | --- |
| Modes | Ports list, search, sort/group panel, embedded busiest-port highlights and ports-up tile | All assessed; graph presentation stays with #481 |
| Long content | Fix required: title tail truncation and 180pt/two-line traffic cap squeeze names and rates | Port-only wrapping presentation, including overview highlights |
| Activation | Fix required: gesture-only row lacks a native accessible action | One full-width wrapping button names facts and graph action; missing ifName is a plain readout with unavailable explanation |
| State | Fix required: every non-up, non-admin-down state reported Down | Only explicit down is Down; missing/testing/unrecognised states are neutral Unknown, searchable and grouped separately |
| Traffic/errors | Fix required: absent errors imply OK; invalid rates can look like valid traffic | Missing/nonfinite/negative traffic says Unavailable; incomplete readings are neutral; positive finite errors retain Warning |
| Units/thresholds | Compliant: octets/s multiplied by eight through Units.Bits, speed via Units.Speed, positive errors warn | Preserved, no new threshold or derived server reading |
| Ordering/persistence | Compliant: PortArrangement, ports.sort and ports.grouping, VLAN/trunk/type/name/index/traffic/speed/description ordering | No persisted names, default, schema or ranking calculation changed; Unknown adds rank 3 after existing ranks |
| Filters/keyboard | Fix required: fixed 80pt label column crowds largest text; panel can open over keyboard | Stack labels/pickers; ports-only panel opening dismisses search and keyboard, retaining query |
| Search | Existing title/alias/value/detail local matching compliant; state and real ifName absent | Add port-only status and ifName matching; #507 owns ButtonSearch/native drag policy |
| Loading/empty/error | Compliant: first-load BusyCard, error suppresses empty, source empty differs from unmatched query, pull refresh | Preserve; #507 owns Retry and race guard |
| Cached/offline | Retained rows during refresh/failure are last loaded facts without a timestamp | Existing behaviour preserved, not live or cached-freshness evidence; hardware failure sequence unverified |
| Insets/header/final row | Existing manual iOS list inset, measured side clearance, finite viewport above glass plus gap and 24pt footer compliant in source | #507 owns list fade/spacer and lifecycle correction; no duplicate inset owner |
| Themes/motion/accessibility | Unverified on hardware | Described card action and explicit text supplied; no new motion; device matrix below remains required |

DeviceSectionPage and code-behind consume DeviceSectionViewModel, loader groups
and SectionRow. DeviceDetailView embeds DeviceSectionCard's five busiest rows;
its summary counts and positive-traffic selection remain unchanged. The tile
now stays neutral when unknown ports exist and no explicit down takes priority.
No graph selection or range editor is added here.

## Callers, returns and contracts

DeviceDetailViewModel opens the relative DeviceSection route with selected
device ID, Ports enum and display name. Detail is reachable through Devices
narrow/split views, Dashboard, alerts and other device links. Tile and card
open the same ports list. Back retains the actual caller. Row activation opens
relative DeviceGraphs with the same DeviceId, actual SNMP ifName, display name
and device name. A missing ifName never fabricates an ID from the description.
Graph Back returns to Ports with its search and arrangement; #481 owns graph
presentation and graph descriptions' order. Shell registrations are unchanged.

Deleted ports remain excluded. API permission failures remain errors; no new
endpoint, credentials, permission assumption, package, persistence or schema is
introduced. API aliases and names are preserved. Sort values and PortFacts
ranking stay in their existing byte units, independent of displayed bit units.
Other SectionRow consumers have PortLayout=false and retain existing matching.

## Exact queued integration after #507, #508 and #509

Read their availability, sensors and resources review documents and relevant
diffs in the supplied checkouts read-only. No commits were imported and no
other branch was changed. Parent updates this branch only at its serial
review/merge turn after #509, with mandatory Claude Opus 5.5 Medium review
after quota resets. No Claude reviewer or delegate was started.

1. DeviceSectionPage.xaml: combine specialised availability, sensor, resource
   and port presentations under one wrapper. Retain all four legacy-grid hide
   triggers. Keep the card border gesture as the single port activation and
   the legacy grid gesture for other rows. Preserve inventory child commands.
2. DeviceDetailView.xaml: retain all specialised highlights and legacy-grid
   hide triggers. PortSummary intentionally omits a graph action because the
   overview card opens Ports, not the individual graph. Do not lose the
   resource bar or availability/sensor joined readouts.
3. SectionRow: combine independent flags and Matches clauses. Only rows with
   Port facts use these new summaries; do not apply them to wireless or other
   sections. Loader and tests must retain all sibling reading fixes.
4. Keep #507's captured section/device, serial handoff, generation guard,
   availability clearing, #509's resource clearing and #508's sensor Updating
   and cache freshness. Preserve #507's IActivePage, disposable watcher,
   idempotent glass, ButtonSearch, Retry, OnScroll=False and list-column fade
   with 20pt spacer. Add this ports-only keyboard close inside the existing
   panel-change callback; do not restore old shared search code.
5. The stacked sort/group panel remains in the existing measured glass-safe
   slot. Verify largest text and short landscape height after integration.

Consumers #469–#479 retain their contracts: availability windows/outages,
sensor classes/limits/definitions, resource units/bars, port arrangement and
graph identity, neighbour graph/reverse links/split pane, VLAN/FDB/ARP fields
and order, routing protocol states, wireless endpoint/units, and inventory
tree/ports toggle/serial copy. Graphs, logs and backups keep specialised routes.
Setup #405 acceptance is unchanged.

## Validation and exact remaining device checklist

Full intended and staged diffs self-reviewed and git diff --check only.
No local builds, tests or scripts run. Added synthetic regression cases for
unknown status, invalid traffic, warning text and actual graph identity.
Existing arrangement, persistence and route tests remain relevant. Shared
tests, Android and iOS CI are pending. Independent parent review and every
hardware result below are unverified, not inferred from source inspection.

Use example.net and edge.example.net, IDs 7/8, long synthetic interface names
and aliases; record final integrated SHA and CI build with each result.

| Check | Required steps | Result |
| --- | --- | --- |
| States/readings | Up/down/admin-down/disabled/missing/testing; zero/positive/missing/NaN/infinite/negative rates and errors; verify explicit text, bits versus byte sorting and speed/VLAN | Unverified |
| Arrangement | Every sort and grouping, natural numbered names, duplicate indexes, VLAN/trunk/no VLAN; relaunch and confirm persisted sort/group; search then reset query | Unverified |
| Loading/failure | Cold/empty/unmatched/offline/denied; refresh success/failure/Retry and slow device switch after #507; distinguish retained last-loaded data | Unverified |
| Keyboard/filters | Header/glass/Command-F, typing replacements retain focus, native drag/completion/unfocus close and retain query; open filters while typing, choose sort/group and Done | Unverified |
| Graph route/return | Tile/card to Ports, named row to actual ifName graph, missing name unavailable; graph Back retains term/order; Devices narrow/split, Dashboard and alert callers return correctly | Unverified |
| Themes/largest text | Light/dark, longest name/alias/value/filter choice, short/long list; initial heading clear of fade, final row reachable above glass and keyboard | Unverified |
| VoiceOver/TalkBack | One complete button readout, accurate action, one activation, 44pt target; nonlink explanation and overview card action; picker labels and Done reachable | Unverified |
| Duo | Outer, inner portrait, inner landscape; fold/unfold and rotate during search/filter/refresh; no clipping, stale glass owner or final-row obstruction | Unverified |
| Resume/iPhone | Background/resume at Ports and graph then Back; ordinary iPhone portrait/landscape, older header and iOS 26 glass where available, Reduce Motion and largest text | Unverified |

User acceptance is required before closing #472 or ticking #442. Parent owns
merges and any later TestFlight delivery.

## Sequential integration

Updated against main 5661748 after merged #507–#509. Port actions and filters
coexist with the Resource, Sensor and Availability templates; each hides the
legacy row independently. Both sides' regression tests are retained. Shared
activation, ButtonSearch, Retry, fade and generation-guarded refresh are unchanged.
Full-diff Claude review and CI must verify the combined XAML and contracts.
No hardware outcome is claimed.

## Independent review remediation

Claude Opus 5.5 Medium reviewed 071ded2 against main 5661748. Known
lowerLayerDown/notPresent states remain Down; traffic ranking and error totals
sum only finite nonnegative inputs. Repeated non-up state text is omitted.
Ports use a start-aligned wrapping readout rather than a multiline Button;
card-wide activation is restored for ports and other links without a duplicate
inner gesture. Native action traits still require device verification.
Regressions cover known-down states and invalid-rate ranking/error totals.
Replacement CI and fresh review are pending. No local builds/tests ran;
all hardware outcomes remain unverified, including missing error-rate behaviour.

Claude re-reviewed d6c4e5d with no blocking or medium findings. Linked rows now
show a decorative chevron; non-link and overview labels retain status colours.
Valid traffic with missing error counters says Error readings unavailable.
Unknown counts appear in the overview summary and tests cover neutral Unknown
and Down taking priority. Hardware must verify card-action activation and traits.
Replacement CI is pending; no local builds/tests ran.

CI at f335cad found the new overview assertion addressed section metadata rather
than the displayed card. It now uses SectionCards and its SummaryText. Replacement
CI remains pending; no local tests ran.

Android CI at 18b286e exposed a missing closing wrapper in the merged port/reading
template. The nesting is corrected; generated-control errors were consequences
of that parse failure. Replacement platform CI remains pending.

## Status colour correction

Device review of #553 found every Up port green and the other states not
reliably coloured. Core's device-ports request carries ifInErrors_delta and
ifOutErrors_delta, not the error rates, so no live port could show as errored,
and the edge/label triggers bound a converter only for non-neutral states.
`PortStatus` now decides one class per port, first match wins: admin down is
grey Disabled; Disabled or Ignore is grey Not monitored; a positive error rate
or, without one, delta is red; Up with zero errors both sides is green; enabled
but down is amber; anything else is grey Unknown. Up without error readings is
grey `Up · errors unavailable`. The Status sort and grouping lead with errored,
then down. Card and overview labels use one fixed-colour trigger per class.
No local builds/tests ran; all device outcomes are UNVERIFIED.
