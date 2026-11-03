# Neighbour link details, #452

Refs #452 and #442. Baseline main 95d50a6709b6caf1122412cae5d09c31be223aca.
Read live #452/#442/#450/#451/#288, CLAUDE.md, DEVELOPING.md, complete
DESIGN.md and DESIGN-REFERENCES.md. Inspected
#490 design/navigation lessons and #491/#492 scope and remote diffs read-only.
No queued branch was edited. Repo-workflow applied with the user's explicit
no-local-build/test/script and non-closing-reference overrides.

## Baseline modes, routes and contracts

NeighboursViewModel.Build constructs deduplicated links from server devices,
ports and NetworkLink data; NeighbourDirectory supplies the list snapshot.
Neighbours.Open passes that same NeighbourLink object on relative neighbourlink.
ShellNavigationService offers it to the current IDetailHost first. NeighboursPage
embeds NeighbourLinkView only while ListDetail.IsSplit; narrow screens push
NeighbourLinkPage. ListDetail retains the detail when narrowed and restores it
when widened. Replacing a selection creates a new view; it has no model event
subscriptions. Device-scoped neighbour lists are #473, not a second caller here.

Pushed Back pops the actual previous page; each endpoint opens relative
devicedetail with the existing integer DeviceIdParameter. Returning restores
the link snapshot. Embedded Back is hidden, and the host retains glass ownership;
an endpoint still pushes device detail and returns to the host. A retained More
stack must reclaim pushed Back after another tab and resume without refresh.

Modes include both monitored ends, unmonitored remote, unresolved local/remote
port, known device/port down, active and inactive discovery, no payload, and
missing announcement fields. There is no detail fetch, refresh, timestamp,
loading/error/stale detection, permission editor, Save, Undo or toast. The
snapshot may age; adding freshness or a fabricated retry/error is unsupported.
List fetch states stay with the source and are not reimplemented here.

Contracts preserved: Core Neighbours.Matches and FromLinks, deduplication,
filters/counts, server IDs, device navigation, endpoint entity kinds and
permission behaviour, saved definitions/order and all persistence. No server
writes, capability additions, credential access or new fallback identities.
Core is pinned to 2.0.0-ci.52 in the shared project. Existing NetworkLink,
Neighbour and endpoint consumers were checked in source; the package is not
cached locally, so package-level type verification remains for CI. No new Core
API is introduced.

## Concern assessment and fixes

| Concern | Baseline finding | Outcome |
| --- | --- | --- |
| Identity, long names | Endpoint labels and navigation rows truncate; two endpoint columns squeeze narrow panes | Fix: complete character wrapping, stacked endpoint cards at every width; no multiline Button measurement |
| Metadata | Fixed label/value columns crowd large text; Field trims source | Fix: label above wrapping value, preserve exact meaningful field source including whitespace/newlines; omit whitespace-only fields |
| Missing data | Empty endpoint state and question-mark ports; no-payload page blank | Fix: explicit State unknown / Port unknown and no-link return guidance; missing metadata explains absence |
| Status | Up could imply unobserved remote health | Fix: Active describes discovery and uses neutral colour unless both endpoints report up; Down and Not active retain precedence; endpoint state remains source-based, unknown is explicit |
| Endpoint actions | Tap rows lack joined descriptions, names truncate | Fix: whole 44pt minimum wrapping action, joined side/name/port/state/discovery description, child label excluded; command availability notified on snapshot replacement |
| Pushed navigation | No glass owner; legacy full safe-area flag | Fix: relative Back and actual caller label, iOS glass with header fallback, top-only native page owner; old flag removed |
| Embedded navigation | Host already hides Back | Compliant: host ownership preserved, no embedded glass actions |
| Insets/fade | Fixed 24pt foot, no glass/column-boundary clearance or fade | Fix: host-boundary side and bottom clearance, final-content bottom clearance, child None and native scroll Never on handler/load; matching Background RGB fade within viewport below visible BackBar, reserved 20pt |
| Lifecycle | No pushed retained-stack restoration | Fix: central IActivePage activation, no local Window/Shell subscriptions or duplicate glass-move placement; view edge watcher disposed on unload/replacement |
| Loading/error/stale/toast | No such detail capabilities | Compliant scope: preserve snapshot contract; freshness cannot be independently verified |
| Native layout and assistive activation | No hardware evidence | Unverified; exact matrix below |

The permanently stacked layout is the conservative guide-compliant choice for
narrow panes and large text; it does not introduce a new multi-column breakpoint.
No shared helper/global edit or accepted-consumer migration is included.

## Dependencies and queue

Queue #489 -> #490 -> #491 (#450) -> #492 (#451) -> this PR. Parent updates
ONLY this branch when its turn arrives, after predecessors land. #490 owns the
central AppShell activation and BackBar root-title fallback. This PR does not
copy or edit that central fix; pushed activation now implements its IActivePage
hook. #491 owns the saved list and #492 the editor and shared setup changes.
No root-list/search expansion.
#288 still owns Neighbours mock-ups and global light-theme acceptance.

## Validation and remaining evidence

Claude Opus 5.5 Medium reviewed the full diff at 551d647 against main 95d50a6.
One medium, three low and two nit findings were addressed: central activation,
host-relative bottom clearance, honest status colours, omission of blank-only
fields, prose word wrapping and removal of duplicate glass-move placement.
Regression assertions cover neutral unknown state, known-up state and blank-only
fields while retaining exact meaningful field text and endpoint navigation.
Full intended diff and whitespace were inspected. Unit tests passed in both CI
runs at the reviewed head, confirming pinned Core compatibility. Android, iOS
and Swift analysis were pending at review time; replacement CI remains pending.
No local builds or tests ran. All device results remain unverified.
Issues remain open and epic boxes untouched.

Run every row below in light and dark themes, default and large text. Record
commit/build, OS/device, actual outcome and diagnostics separately; all are
currently unverified.

| Scenario | Required device evidence |
| --- | --- |
| Missing ends/data | Monitored pair; unmonitored remote; missing local/remote port; no metadata; no payload: explicit unknown/absence, no false zero/OK or unrunnable action |
| Long names | Long local/remote hostname and interface ID, multiline description and MAC/protocol: full content, wrapping and final field reachable |
| Active/inactive | Active with unknown remote health, inactive, local device/port down and remote device/port down: text independent of colour and correct action description |
| Endpoint return | Narrow Neighbours -> link -> each device -> Back -> link -> Back actual caller; wide embedded -> each device -> Back host; repeat from pinned and unpinned More stacks |
| Narrow/split | Resize across split threshold with selection retained, replace detail, reopen; only host-reaching insets, no duplicate Back or glass |
| Duo | Outer portrait, inner portrait and inner landscape; fold/unfold, rotate and background/resume in pushed and embedded modes, final item above glass |
| Retained tab | More link -> Devices -> More and resume without Refresh: one correct glass owner and caller label |
| VoiceOver/TalkBack | Whole action focus/name/state and one activation per tap, full long labels, unavailable action absent; Back meaningful |
| iPhone | Ordinary iPhone portrait/landscape, header fallback where applicable and iOS glass: top safe area once, full-width matched fade, scroll Never and last-item clearance |

Section TestFlight is after #450–#452 land under the parent; obtain separate user
acceptance for #452 before closure. No hardware outcome is inferred from CI.
