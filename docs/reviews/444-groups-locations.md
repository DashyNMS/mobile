# Groups and locations review, #444

## Baseline and contracts

Inspected live #444 and parent #442, CLAUDE.md, DEVELOPING.md, full
DESIGN.md and DESIGN-REFERENCES.md. Base is main at
70feeb10d3a36013e2156672b2c596c4603fb37f, after #466.
No open PRs were listed at assessment. The current guide has no separately
named severity/fidelity policy; this pass applies its text-and-colour,
unknown-state and complete-content rules without changing source semantics.

GroupsLocationsPage is a searchable virtualised list. AppShell.PageType and
AppPages expose it as a pinned tab or a pushed page from More. Header Back
returns relatively to the actual caller. There is no device-detail mode.
Groups is initially selected; Locations uses the same query. Mode/query survive
while this page instance lives, not as persisted settings. Opening a row calls
Routes.Devices with either GroupParameter or LocationParameter and the exact
key. ShellNavigationService/AppPages.Resolve retain the existing distinction:
pinned Devices switches tabs; unpinned Devices pushes its page. A switched tab
does not promise a Back stack to Groups. A pushed child returns to its caller.
No selection, filter persistence, credentials, permissions or API writes change.

GroupsLocationsViewModel builds rows from devices, groups, membership and
best-effort locations. SectionRow.Matches searches title, subtitle, count and
detail. Counts include disabled/ignored and unknown devices with their existing
labels. Down groups come first, then the existing case-insensitive name order
and ungrouped tie-break. Empty groups and coordinate-only locations remain.
The location-list API failure fallback is unchanged. No global helper or other
accepted page is edited. Shared-project changes affect only this view model;
SectionRow and other consumers are unchanged.

## Scope assessment and findings

| Concern | Source finding and disposition |
| --- | --- |
| Groups/Locations modes | Compliant commands/query retention; fixed accessible selected-state descriptions that previously depended on colour. |
| Search/filter controls | Compliant SearchField appearance and always-visible behaviour on both platforms retained. Added ISearchHost, Search-key and scroll keyboard dismissal; no SearchReveal migration. |
| Counts/status | Compliant existing totals and down/disabled/unknown text retained. No fabricated OK text for empty/unknown rows. |
| Ordering | Compliant preservation of existing list order; this is not a picker-order migration. |
| Row activation/Devices | Existing whole-card command and exact route keys retained; combined accessible facts and action hint added. Native activation remains unverified. |
| Long names/descriptions | Fix required: TailTruncation and competing count columns hid source text. Wrapping name, subtitle, count and status now share one flexible column. |
| Empty groups | Compliant zero-device rows remain actionable with their original key. |
| Loading | Fix required: no explicit loading text. Shared BusyCard now occupies its own fixed status row; pull refresh remains available. |
| Errors/empty lists | Fix required: IsEmpty was not notified after IsBusy cleared. Added notifications and regression coverage. Error suppresses empty claim; pull refresh retries, glass also offers refresh. Last good rows remain on failure. |
| Insets/footer | Fix required: legacy UseSafeArea plus container safe area, no glass footer/native collection policy. Removed legacy flag; iOS page owns top only, supporting layouts/RefreshView opt out, native collection adjustment is Never on handler/load. CoveredEdges owns sides and footer bottom; Android retains page container handling. |
| Fade | Fix required by scrolling pattern: 20pt input-transparent fade within list viewport and matching header space. Empty text has 32pt margin. |
| Glass/lifecycle | Fix required: Show/Hide paired, Moved handler paired, retained edge watcher disposed on exit. Pushed Back only, Header.BackInGlass only when iOS glass available, matching Logs/Graylog. Pinned root has no Back. |
| Resize/resume/themes/large text | Unverified on devices. CoveredEdges watcher recomputes placement; page lifecycle restores glass ownership. No new window subscription or native observer token. |

No confirmed finding is deferred. Native layout and accessible activation are
implementation risks until device checks establish outcomes.

## Validation evidence

Source full-diff inspection completed. git diff --check passed before commit.
Regression tests added for empty-state notification after loading and mode
selection/query retention. Existing tests cover count/order, empty groups,
coordinates, search and exact Devices route parameters.
No local build or test was executed, as instructed. CI shared tests,
Android/iOS compilation and security checks are pending PR execution.
Mandatory independent Claude review is pending parent coordination.
CI and review are not device outcomes. All device outcomes below are unverified.

## Exact device acceptance steps

Use synthetic groups such as Branch 2 and a long Branch 10 description, a
zero-device group, ungrouped devices, locations with/without coordinates, and
up/down/disabled/unknown members.

1. Pin Groups, launch cold, switch tabs and resume. Confirm no Back on its
   root and one glass owner. Unpin it, open More > Groups & locations, check
   glass Back on iOS 26+ and header Back on Android/older iOS. Back returns More.
2. In each mode check totals/status against the source, down-first order,
   empty group and coordinate-only location. Activate rows including ungrouped.
   Check Devices' exact filter with Devices pinned and unpinned; return using
   the applicable tab or Back and confirm mode/query retained.
3. Search by name, description, count and status. Switch modes with a query.
   Test no matches, Search return, scrolling, content touch and Command-F;
   confirm query survives keyboard dismissal and search stays visible.
4. Test first loading, empty sources, failed initial fetch and failed refresh.
   Confirm no misleading empty claim while busy/error, last good rows survive,
   and pull refresh retries. Check glass refresh on iOS 26+.
5. In light/dark and largest practical text, inspect all long names,
   descriptions, counts and state text. Scroll to the final row and activate
   it above bottom/side glass. Confirm first row/empty text clear the fade.
6. Repeat on Duo outer, inner portrait and inner landscape; fold/rotate with
   keyboard open, then dismiss it and resume. Check camera/side-pill clearance.
   Repeat on an ordinary iPhone when available, including older iOS if available.
7. With VoiceOver/TalkBack, confirm one announcement per row includes its name,
   details/counts/status, each mode announces selection, and each activation
   opens once. Check Reduce Motion and repeated navigation. Inspect diagnostics
   for overlap and tab-button placement-loop warnings.

Record tested commit/build and each configuration separately before user
acceptance. Keep #444 and epic #442 open until that acceptance.
