# Interface graphs review, #481

Refs #481 and #442. Source assessment on 5 November 2026 at main
`d1c6e86c88e33b83e864ac331f8ff8312d0d1d84`. No additional confirmed
interface-specific defect was found in this pass. This is an assessment-only
change, not device acceptance or a claim that the remaining checks passed.

## Baseline and ownership

HEAD, local main and freshly fetched origin/main match the SHA above. The
isolated topic checkout was clean before this file was added. Live issue bodies,
all comments and states were read for #481, #480, #472 and #442. All are open;
#481, #480 and #442 have no comments. #472's rejection and later approved
implementation are preserved, not treated as interface-graph acceptance.

Live open-PR listing returned none. All-state PR body search for 481 returned
merged #510 and #542. Direct PR lookup confirms #542 MERGED, with head
`b051c2e764f6d02a02b9463b6463af3913ac8691` and successful unit, Android,
iOS simulator and CodeQL checks. Its merged result is the baseline above.
The remaining remote graph branch points to that merged head, not unfinished
work to recreate. #534 is also merged and records the approved Ports cards.

Read CLAUDE.md, DEVELOPING.md, DESIGN.md, DESIGN-REFERENCES.md, the full
#480 assessment and `/tmp/mobile-442/reconciled-assessment.md` central/shared
rules, recurring matrix M and #481 checkpoint. Read the exact approved compact
legend proposal at `/tmp/mobile-442/legend-proposal.html`. Historical assessments
describe their own heads; current source takes precedence.

This checkout is the sole writer for this assessment. No production file,
shared helper, dependency, or other checkout is edited. In particular the
protected dirty #475 checkout is untouched. Shared ownership remains:

| Source | Consumers and boundary |
| --- | --- |
| DeviceGraphsPage XAML/code and DeviceGraphsViewModel | #480 device mode and #481 every interface mode, serial ownership; no separate interface page |
| PortGraphs | DeviceGraphs and GraphPicker; picker descriptions remain naturally ordered, interface sources retain IfIndex order and saved graph identity |
| LongPress | Graph legend native toggle plus Alerts/Devices row selection; Button-specific native hold handling remains unchanged |
| ViewModelBase | Shared read failures, diagnostics and busy handling; retained redraw ownership from #542 remains unchanged |
| CoveredEdges, TabBarButtons, PageHeader, AppShell/IActivePage | Shared placement, caller Back and retained activation; no per-page offsets or lifecycle dispatcher added |
| Ports DeviceSectionPage/view model | #472 owns cards, filter/query/order and entry; graph Back must retain that existing instance |
| Core GraphLegend, GraphSeriesNames, Port, graph API | Pinned package owns source DTOs, units, naming and endpoint semantics; mobile does not mirror them |

## Modes and data contract

`Pages/DeviceGraphsPage.xaml.cs:ApplyQueryAttributes` calls LoadPortAsync with
the actual IfName. `DeviceSections/DeviceSectionViewModel:OpenLinkAsync` passes
DeviceId, LinkPortIfName, row title and device name through a relative route.
Device detail's Ports section and overview highlights reach that Ports instance.
`ViewModels/DashboardViewModel:OpenGraphAsync` passes saved GraphPortIfName,
GraphName and GraphTimeRangePreset. Device detail's Graphs section and graph
links enter device mode, from which Show can choose any listed port.
AppShell registers one transient pushed page. There is no pinned root or
embedded interface graph page; embedded device detail still pushes this page.

Show includes Device graphs and deduplicated nonblank IfNames, sorted by display
name using NaturalOrder. A direct link missing from a successful list, or an
API-failed list, adds its own target without guessing a replacement interface.
DisplayName prefers IfDescr, then IfName; labels never replace the API key.
Renamed/deleted interfaces can fail at the server and require real-account
verification. Non-API failures can stop context initialisation; offline recovery
is not certified by the direct-link API-exception fallback.

Every interface offers the following sources. Names come from Core's committed
`GraphSeriesNames.Ports.cs`, not the English graph description.

| Graph identity | Display description | Defined legend order | Current values supported by ForPort |
| --- | --- | --- | --- |
| port_bits | Traffic | In, Out | IfInOctetsRate / IfOutOctetsRate, bytes/s multiplied by 8 into decimal bit-rate units |
| port_upkts | Unicast packets | In, Out | IfInUcastPktsRate / IfOutUcastPktsRate, per second |
| port_nupkts | Broadcast and multicast packets | Broadcast in, Broadcast out, Multicast in, Multicast out | None; all values stay null |
| port_errors | Errors | Errors in, Errors out, Discards in, Discards out | IfInErrorsRate / IfOutErrorsRate per second; discard values stay null |

Descriptions are naturally ordered in the graph picker, so Broadcast and
multicast packets appears before Errors, Traffic and Unicast packets. Initial
interface selection is still **Traffic** by identity; an explicit compatible
graph deep link wins. Unsupported graph identity falls back to the offered
list. Show switches to device/another port retaining the selected range; it
does not promise to retain the previous graph kind across targets.

Each graph supports Hour, Day, Week, Month and Year. Initial range is Day unless
the caller supplies a recognised preset. Core builds the escaped endpoint
`devices/{deviceId}/ports/{escaped IfName}/{escaped graphType}` with from,
optional to, width, height and legend query parameters. Slashes belong to the
IfName, not to a display-description path. The SVG owns historical samples,
axes and times; current API values in the legend are not historical samples.

Pinned Core `2.0.0-ci.56` source was inspected through GitHub at publication
commit `8774898a8a3ba3e487fc076722f020ffd19e6981`, including GraphLegend,
GraphSeriesNames.Ports and Port. #480 records successful publication run
41949476685, explicit version/RepositoryCommit and consumer CI. Its direct
nupkg download returned 403; this review does not claim package-byte inspection.

Supported missing readings produce a dash. Negative/non-finite readings and
traffic multiplication overflow produce a dash through Core's formatter.
Measured zero, including signed zero, stays zero with b/s or /s. Unsupported
non-unicast and discard readings are null, so the native row has no fabricated
numeric value; names, Shown/Hidden and controls remain. Port.AdditionalData
does not establish a supported typed reading contract. No new DTO field or
inferred sum is introduced. Typed payload completeness remains UNVERIFIED.

Core resolves unique recognised legend colours first; exact full definition
counts permit positional fallback. Unexpected count/colour combinations can
remain unnamed. Mobile preserves the server legend in that case; a lone
unresolved series can use the graph description without a current value.
Equal counts alone do not prove the actual server's series identity. Real
payload/SVG identity beyond these rules remains UNVERIFIED, including server
customisation, percentile entries and incomplete series.

## Concern ledger

C means inspected source contract, U means unverified outcome, N/A means absent
capability. There is no newly confirmed correction in this PR. A C entry does
not turn its accompanying native check into a pass.

| Concern | Source evidence and disposition |
| --- | --- |
| Selection/range/target handoff | C: LoadContextAsync, DisplayTargetAsync and PortGraphs implement the modes above; GraphTargetTests cover range/cache separation and missing-list direct links. U real rapid graph/range/target/device changes, delayed lists and renamed/missing IfName. |
| Chart and legend | C: RawAsync retains legend=true SVG; NameSeriesAsync uses Core resolution/ForPort and preserves unnamed source legend. U actual full series identity and server content completeness. |
| Current readings | C: pinned ForPort distinguishes supported missing/invalid from zero and unsupported null as above. U actual payload field availability, localisation and native blank-value speech. |
| Cache/offline | C: raw key includes DeviceId, graph identity, actual IfName and range; switching devices clears retained cache. Hidden key is device:graph:IfName, with desktop-compatible persistence. Theme redraw and port series changes use retained raw SVG. U uncached offline entry, permission changes after cache and complete recovery flow; no offline guarantee inferred. |
| Series state | C: SetHidden rejects all-hidden and empty entries, saves per graph/port, and ShowAll CanExecute follows SomeHidden. LoadGraph clears raw, entries, hidden and series before await, invalidates render generation and suppresses stale load failures. U delayed real handoffs and native rapid activation. |
| Approved compact legend | C: one native transparent Toggle over excluded/input-transparent wrapping facts; separate Only sibling, 44pt minima, explicit Shown/Hidden and filled/outlined marker. LongPress cancels native toggle release on iOS and consumes Android Button LongClick; no enclosing tap. U native hold fires only Only, keyboard focus/activation, maximum-text wrapping and actual hit bounds. Exact approved policy is unchanged. |
| Action audit | C: iOS26 Back and conditional eye Show all use glass; active/current placement and command guard protect callbacks. Android/older iOS use Header Back and the eye IconButton. Toggle/Only remain contextual to a series. U native More overflow if glass compacts, picker dismissal and parent return. N/A page Share, Export, Search, Refresh, Compare, Save or fixed primary action. No invented action is required. |
| Retained lifecycle | C: IActivePage.Activate reapplies header, disposed edge watch, idempotent Moved handler, theme subscription and glass without fetching/resetting selection. AppShell dispatches current-page activation after navigation/resume. Disappearance removes subscriptions and owner-safe glass. U retained return without Appearing, picker/child return, tab switch, app resume and theme while hidden/loading. |
| Usable base and insets | C source ownership: native iOS page top only, GraphScroll finite star row with Never adjustment, full background, content-only side/bottom padding from CoveredEdges.ForScrolling plus normal gutters. Android retains page native container handling. U actual host/viewport/pill coordinates and usable-base acceptance; no measurements or guessed offset added. |
| Camera and adaptive width | C: no chip scroller/filter overlay; header/content use covered edges. Wide graph and 300pt legend split at 800pt available width, otherwise stack. U actual camera layer compared with Devices/Alerts, text fit in 300pt pane, native background/base extent and fold/rotation. |
| Fade and final content | C: placement reserves 20pt top content padding for the input-transparent 20pt fade; footer padding includes bottom clearance. U initial text readability, chart clipping, final series/Only reach and final scroll position separately from viewport fit. |
| Loading/empty/error/denied | C: busy indicator, TargetsError, ErrorMessage, no-graphs state and GraphImages.ThrowIfDenied; direct API-list failure retains target. U real cold/empty/failed/retry, denied SVG, disconnected entry and cached transitions for every kind/range. Retry through another selection/re-entry must be exercised; no new retry UI approved. |
| Long text/accessibility | C: persistent picker semantic names; range selection names; wrapping legend facts and actual Hide/Show names include available detail/value/state; native Only alternative. U VoiceOver/TalkBack speech, chart information beyond native current readings, reading order, maximum text, keyboard traversal and both-theme contrast. |
| Search/filter/comparison | N/A on graph page: query, no-match state, drag-to-dismiss search, filter panel and comparison. C Ports keeps its own query/sort/group instance through relative navigation. U real Back preserving Ports query, choices and position. |
| Motion | C: graph page adds no custom animation; redraw retains source and uses current palette. N/A custom sheets/transition controls. U Reduce Motion and repeated native navigation/picker transitions. |
| Security/source/persistence | C: GraphHtml encodes SVG as data-image with restrictive CSP; WebView rejects external navigation. Routes, credentials, permissions, settings and server raw SVG semantics unchanged. GraphPicker retains source order/selection/save fields. U malicious/real account end-to-end behaviour; no new security acceptance claimed. |

## Exact remaining verification

Every item here is **UNVERIFIED**. Record mobile SHA, app build, OS, device,
display/orientation and result for each case. Historical #542 CI and proposal
approval do not satisfy these checks.

1. Run every kind above at all five ranges from Ports, Dashboard saved port
   graph/range and device-mode Show. Use synthetic interfaces with distinct
   IfName/IfDescr/alias, slash-containing names, long names, missing/renamed
   interfaces and failed/denied lists. Inspect the requested actual IfName,
   current selected identity/range and matching SVG, not just chart presence.
2. For every kind exercise cold load, slow response, source-empty where the
   endpoint allows it, denial, timeout, retry by selection/re-entry, cached and
   uncached offline. Distinguish unsupported null, absent supported reading,
   measured zero, invalid/negative/non-finite/overflow and valid units. Check
   custom/missing/extra legend entries against the actual SVG series identity.
3. Toggle each series, attempt hiding the last visible one, Only by button and
   long press, then Show all by glass/header. Require exactly one activation,
   persistent per-device/per-port/per-graph hidden keys, no stale controls while
   loading, and no extra graph fetch for theme/port visibility changes. Switch
   graph/range quickly, change target during delayed work when available, then
   fail an older request. Verify current graph, error, busy and saved state.
4. On Duo outside, inside portrait and inside landscape, then ordinary iPhone,
   record native page, body host, GraphScroll viewport and content bounds in
   the same coordinates as pill/glass/camera. Record H, pill bottom B, viewport
   bottom V and V minus B for side, bottom and top-wide pill separately.
   Compare with Devices/Alerts. Check full background extent and initial fade
   readability; separately scroll the last legend facts and Only above glass.
   No numerical result can be inferred from content padding.
5. Repeat narrow/800pt split/short landscape with both themes and maximum text,
   many/long/no legend entries and all states above. Verify complete picker
   selection, wrapped facts/Only, control hit areas and chart bounds. Exercise
   VoiceOver/TalkBack, hardware keyboard traversal and selection, native picker
   open/dismiss, keyboard shown then dismissed where the platform offers one,
   Reduce Motion and repeated transitions. Search drag checks are N/A here.
6. Fold/rotate without reopening, switch tabs and return without Appearing,
   background/resume, change theme while hidden/loading and dismiss native
   pickers. Require one glass owner and caller Back, current actions and no
   reset/fetch solely for activation. Pop to Ports with query, sort/group,
   filter choices and position retained; repeat dashboard and nested
   alert/device callers. Inspect Tab bar buttons/Top line diagnostics for loops.
   Verify Android/older-iOS header fallback as well as iOS26 glass.

## Review and CI disposition

Inspected existing GraphTargetTests, GraphLegendTests and the shared CI workflow.
They provide relevant source regressions, not a completed interface device
matrix. This PR changes documentation only, so no mirrored layout tests or new
behaviour tests were added. No local build, tests, restore or validation script
ran. Whitespace and full intended/staged diff inspection precede commit/push;
new-head CI results must be read separately from baseline #542's green checks.

No new DESIGN lesson is added because no new defect/fix establishes one. The
existing approved legend and shared placement guidance already cover this scope.
Parent must obtain fresh Opus 5.5 Medium full-diff review and judge CI before
any authorised merge. This implementation child starts no agents and performs
no watch, merge, issue closure, epic tick or release. #481 remains open pending
the unverified evidence and explicit user acceptance.
