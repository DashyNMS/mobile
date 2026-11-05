# #442 delivery status

Reconciled 5 November 2026 against main `503393f101ba480b2f12f175ab84d8eb64bcba1c`
(merged #549). Refs [#442](https://github.com/DashyNMS/mobile/issues/442).
All 40 original child scopes have assessment records, processed sequentially by
the parent. This is the complete delivery handback index, **not epic acceptance**.
Read the linked assessments for full modes, routes, persistence/security contracts,
C/F/U findings and exact native matrices; historical pending prose is dated evidence.

Evidence: live #442 and all 40 child bodies, states and comments; merged PR metadata;
CLAUDE.md, DEVELOPING.md, DESIGN.md and existing reviews. Current issue state is
preserved. Epic ticks, closure alone, merged source and CI do not prove acceptance.
Explicit acceptance below means a live comment records the user's decision; its
scope is only the reported result (not a fresh exhaustive hardware pass).

Live totals: 19 closed and 21 open. Fourteen children have explicit acceptance
comments, including build 231 acceptance for #454, #458 and #462 in addition
to the initial eleven. Five closed children (#443, #446, #450, #452, #453)
have no explicit acceptance in the fetched comments; preserve their closures.

## Complete child ledger

Each delivery cell links the latest scoped PR and its full merge SHA. Earlier
implementation is retained in the assessment/history. All native results for a
**new run at this baseline are U (UNVERIFIED)**: no installed build or frames.
“Closed; acceptance U” preserves closure where live comments do not establish
acceptance, including older negative feedback; it does not request reopening.

| Child / live state | Assessment and complete matrix | Latest merged delivery / SHA | Compact source outcome | Acceptance evidence |
| --- | --- | --- | --- | --- |
| [#443](https://github.com/DashyNMS/mobile/issues/443) CLOSED | [443-health](443-health.md) | [#519](https://github.com/DashyNMS/mobile/pull/519) `56ccb450d3b82b74e72d2187c196c282c1347acc` | retained controls/fade | Closed; acceptance **U** (preserve closure) |
| [#444](https://github.com/DashyNMS/mobile/issues/444) CLOSED | [444-groups-locations](444-groups-locations.md) | [#490](https://github.com/DashyNMS/mobile/pull/490) `83e9a3983b53f3a93931b46347ae8325baa8076b` | groups/locations and retained navigation | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/444#issuecomment-6618547247) |
| [#445](https://github.com/DashyNMS/mobile/issues/445) CLOSED | [445-geographic-map](445-geographic-map.md) | [#494](https://github.com/DashyNMS/mobile/pull/494) `ad42d7180b24cc4712fa403d79ecd3b85084e17b` | map startup correction | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/445#issuecomment-6618548115) |
| [#446](https://github.com/DashyNMS/mobile/issues/446) CLOSED | [446-alert-rules](446-alert-rules.md) | [#520](https://github.com/DashyNMS/mobile/pull/520) `d271e9c1d76a92625499d72ab41798116ba25077` | retained controls/camera clip | Closed; acceptance **U** (preserve closure) |
| [#447](https://github.com/DashyNMS/mobile/issues/447) CLOSED | [447-alert-rule-details](447-alert-rule-details.md) | [#486](https://github.com/DashyNMS/mobile/pull/486) `881830b72aa091a5745f421f7b6dc6d74e815223` | complete rule facts | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/447#issuecomment-6618770032) |
| [#448](https://github.com/DashyNMS/mobile/issues/448) CLOSED | [448-alert-template-details](448-alert-template-details.md) | [#488](https://github.com/DashyNMS/mobile/pull/488) `38fa645bb4b97b83221c5437f545585f32089a48` | complete template facts | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/448#issuecomment-6618770735) |
| [#449](https://github.com/DashyNMS/mobile/issues/449) CLOSED | [449-alert-details](449-alert-details.md) | [#490](https://github.com/DashyNMS/mobile/pull/490) `83e9a3983b53f3a93931b46347ae8325baa8076b` | fault scope/navigation | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/449#issuecomment-6618771493) |
| [#450](https://github.com/DashyNMS/mobile/issues/450) CLOSED | [450-neighbourhoods](450-neighbourhoods.md) | [#521](https://github.com/DashyNMS/mobile/pull/521) `c3b584e7b21b7cd9dd8c578b482bb8fd1423d1bd` | fitted saved-list header | Closed; acceptance **U** (preserve closure) |
| [#451](https://github.com/DashyNMS/mobile/issues/451) CLOSED | [451-neighbourhood-editor](451-neighbourhood-editor.md) | [#492](https://github.com/DashyNMS/mobile/pull/492) `95d50a6709b6caf1122412cae5d09c31be223aca` | editor fields/Save | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/451#issuecomment-6618662335) |
| [#452](https://github.com/DashyNMS/mobile/issues/452) CLOSED | [452-neighbour-link-details](452-neighbour-link-details.md) | [#522](https://github.com/DashyNMS/mobile/pull/522) `765b02c27e8b77e4b79c59d644334d719c81de64` | list controls/detail clearance | Closed; acceptance **U** (preserve closure) |
| [#453](https://github.com/DashyNMS/mobile/issues/453) CLOSED | [453-more-navigation](453-more-navigation.md) | [#523](https://github.com/DashyNMS/mobile/pull/523) `cf11a4171371febe229ef8ebd47f41f3a411174a` | approved lightweight catalogue | Closed; acceptance **U** (preserve closure) |
| [#454](https://github.com/DashyNMS/mobile/issues/454) CLOSED | [454-settings-about](454-settings-about.md) | [#524](https://github.com/DashyNMS/mobile/pull/524) `9e616fd7e86c23651b1b4c139134e31ba5f7007f` | approved identity/grouping | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/454#issuecomment-6629906810) |
| [#455](https://github.com/DashyNMS/mobile/issues/455) CLOSED | [455-sign-in](455-sign-in.md) | [#497](https://github.com/DashyNMS/mobile/pull/497) `5c989a8e5a4c2b3e01ebbd4e44477cb50e97d746` | credentials/submit | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/455#issuecomment-6618938158) |
| [#456](https://github.com/DashyNMS/mobile/issues/456) CLOSED | [456-web-sign-in](456-web-sign-in.md) | [#498](https://github.com/DashyNMS/mobile/pull/498) `bcd8ea88f426b4544429ad452f223bbe9b402c33` | web recovery | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/456#issuecomment-6618939147) |
| [#457](https://github.com/DashyNMS/mobile/issues/457) CLOSED | [457-diagnostics-consent](457-diagnostics-consent.md) | [#499](https://github.com/DashyNMS/mobile/pull/499) `3ac10df0e7f4d1e994a2973dd4deda9922c3256d` | consent/completion | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/457#issuecomment-6618870876) |
| [#458](https://github.com/DashyNMS/mobile/issues/458) CLOSED | [458-whats-new](458-whats-new.md) | [#525](https://github.com/DashyNMS/mobile/pull/525) `63466c80fdab8a1300d61ce4d6f7d52ead85bcf3` | single heading/fixed Got it | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/458#issuecomment-6629907781) |
| [#459](https://github.com/DashyNMS/mobile/issues/459) CLOSED | [459-licences](459-licences.md) | [#501](https://github.com/DashyNMS/mobile/pull/501) `a52994ca0af20ac7b0764d02d08c477347242dee` | licence reading | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/459#issuecomment-6618921043) |
| [#460](https://github.com/DashyNMS/mobile/issues/460) CLOSED | [460-log-entry-details](460-log-entry-details.md) | [#502](https://github.com/DashyNMS/mobile/pull/502) `c25b71cf34fad3286ddebb6e93fa2a4b5e712865` | complete entry facts | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/460#issuecomment-6618968741) |
| [#461](https://github.com/DashyNMS/mobile/issues/461) OPEN | [461-graylog-message-details](461-graylog-message-details.md) | [#526](https://github.com/DashyNMS/mobile/pull/526) `e67448928e7aaa37d0b81b474ebc6c24f5c26a3d` | pane clearance/actions; symptom U | **U**; no explicit device acceptance |
| [#462](https://github.com/DashyNMS/mobile/issues/462) CLOSED | [462-backup-history](462-backup-history.md) | [#527](https://github.com/DashyNMS/mobile/pull/527) `0c55d06496a70f55bc887bb35fbd5e1f35eb808c` | history/comparison glass/base | [Explicit user acceptance](https://github.com/DashyNMS/mobile/issues/462#issuecomment-6629908503) |
| [#463](https://github.com/DashyNMS/mobile/issues/463) OPEN | [463-backup-contents](463-backup-contents.md) | [#528](https://github.com/DashyNMS/mobile/pull/528) `d78b33592812076cb8e8f6678a6acaff57776727` | contents Search/Share | **U**; no explicit device acceptance |
| [#464](https://github.com/DashyNMS/mobile/issues/464) OPEN | [464-backup-diff](464-backup-diff.md) | [#529](https://github.com/DashyNMS/mobile/pull/529) `f7c71306b40fd33f6ad86e68bfb8c2e1d161967e` | diff Share/base | **U**; no explicit device acceptance |
| [#469](https://github.com/DashyNMS/mobile/issues/469) OPEN | [469-device-availability](469-device-availability.md) | [#530](https://github.com/DashyNMS/mobile/pull/530) `74c1891514d6dcf9eef2fd3d388b3a8203ff454c` | shared measured section base | **U**; no explicit device acceptance |
| [#470](https://github.com/DashyNMS/mobile/issues/470) OPEN | [470-device-sensors](470-device-sensors.md) | [#531](https://github.com/DashyNMS/mobile/pull/531) `cdf3b0d32f6ce1fdf1978ac2c58068101b1ab1a8` | neutral unassessed section sensors; #532 remains | **U**; no explicit device acceptance |
| [#471](https://github.com/DashyNMS/mobile/issues/471) OPEN | [471-device-resources](471-device-resources.md) | [#533](https://github.com/DashyNMS/mobile/pull/533) `2010d7b44cc73027d0ffbea72a2a686c4c859918` | near-full text and source failures | **U**; no explicit device acceptance |
| [#472](https://github.com/DashyNMS/mobile/issues/472) OPEN | [472-device-ports](472-device-ports.md) | [#534](https://github.com/DashyNMS/mobile/pull/534) `476ba157faf577ac2d7e791079cf3749f66f3e05` | approved adaptive port cards | **U**; no explicit device acceptance |
| [#473](https://github.com/DashyNMS/mobile/issues/473) OPEN | [473-device-neighbours](473-device-neighbours.md) | [#535](https://github.com/DashyNMS/mobile/pull/535) `7d2c5a943ad40c676ecf5d3afbd1e33a62916351` | graph role/selection | **U**; no explicit device acceptance |
| [#474](https://github.com/DashyNMS/mobile/issues/474) OPEN | [474-device-vlans](474-device-vlans.md) | [#536](https://github.com/DashyNMS/mobile/pull/536) `868e526c4b0f31c8fa11a22c55ea869c901cae1a` | membership-denied records retained | **U**; no explicit device acceptance |
| [#475](https://github.com/DashyNMS/mobile/issues/475) OPEN | [475-device-fdb](475-device-fdb.md) | [#537](https://github.com/DashyNMS/mobile/pull/537) `f08148946ae6cbb962efde48bf4de413bfeb0b72` | actual VLAN tags/records | **U**; no explicit device acceptance |
| [#476](https://github.com/DashyNMS/mobile/issues/476) OPEN | [476-device-arp](476-device-arp.md) | [#538](https://github.com/DashyNMS/mobile/pull/538) `e3d9f3f0fadc0c7a1916f30227d921d9b8bf3da7` | ARP source failures | **U**; no explicit device acceptance |
| [#477](https://github.com/DashyNMS/mobile/issues/477) OPEN | [477-device-routing](477-device-routing.md) | [#539](https://github.com/DashyNMS/mobile/pull/539) `17f747981176242b8ee3c2af5eaf6fb9c16bf651` | supported routing protocols | **U**; no explicit device acceptance |
| [#478](https://github.com/DashyNMS/mobile/issues/478) OPEN | [478-device-wireless](478-device-wireless.md) | [#540](https://github.com/DashyNMS/mobile/pull/540) `6ad258e7e866f7a228821c2567a592eddcb1268a` | wireless unknown health | **U**; no explicit device acceptance |
| [#479](https://github.com/DashyNMS/mobile/issues/479) OPEN | [479-device-inventory](479-device-inventory.md) | [#541](https://github.com/DashyNMS/mobile/pull/541) `26ee61babe42a406cdd057c81b06d52dec7b567b` | approved Tree options/raw serial | **U**; no explicit device acceptance |
| [#480](https://github.com/DashyNMS/mobile/issues/480) OPEN | [480-device-graphs](480-device-graphs.md) | [#542](https://github.com/DashyNMS/mobile/pull/542) `d1c6e86c88e33b83e864ac331f8ff8312d0d1d84` | approved legends/lifecycle + Core ci.56 | **U**; no explicit device acceptance |
| [#481](https://github.com/DashyNMS/mobile/issues/481) OPEN | [481-interface-graphs](481-interface-graphs.md) | [#543](https://github.com/DashyNMS/mobile/pull/543) `ccfe4b0f593e13c38212ef73a4094cda59463a77` | live ports/unknown-name fallback | **U**; no explicit device acceptance |
| [#482](https://github.com/DashyNMS/mobile/issues/482) OPEN | [482-device-event-logs](482-device-event-logs.md) | [#546](https://github.com/DashyNMS/mobile/pull/546) `256307a98aeb8f459df8dd508df9535431e6a23c` | complete event facts + transparent row overlay | **U**; no explicit device acceptance |
| [#483](https://github.com/DashyNMS/mobile/issues/483) OPEN | [483-device-graylog](483-device-graylog.md) | [#545](https://github.com/DashyNMS/mobile/pull/545) `56d55fef095f6b2c62cefc2007772584540ea971` | approved compact facts + request isolation | **U**; no explicit device acceptance |
| [#484](https://github.com/DashyNMS/mobile/issues/484) OPEN | [484-device-maintenance](484-device-maintenance.md) | [#547](https://github.com/DashyNMS/mobile/pull/547) `357937869cfb8854c23be45ed957dc662e5b51f1` | fixed Schedule/error reveal | **U**; no explicit device acceptance |
| [#516](https://github.com/DashyNMS/mobile/issues/516) OPEN | [516-network-log-camera](516-network-log-camera.md) | [#548](https://github.com/DashyNMS/mobile/pull/548) `612b4a89c66e73dee58a1037fe37f535907a4356` | horizontal outer clip/internal gutters | **U**; no explicit device acceptance |
| [#517](https://github.com/DashyNMS/mobile/issues/517) OPEN | [517-network-graylog-camera](517-network-graylog-camera.md) | [#549](https://github.com/DashyNMS/mobile/pull/549) `503393f101ba480b2f12f175ab84d8eb64bcba1c` | accepted wrapping/sideways deferral; no camera fix | Explicit deferral in [#549](https://github.com/DashyNMS/mobile/pull/549); native **U** |

## Delivery dependencies and live inventory

All 30 PRs in the #519–#549 range are merged; #532 is an **issue**. #544
supplies Event log facts; #546 follows with transparent disabled/pressed/focus
states. #515 (`a9773b68f8e70229fe074a0c95d877a247638dde`) records the fixed-action
alignment rule, not an implementation fix. #530's shared base correction is
consumed by all eleven section assessments #469–#479; #541 also audits the
Ports panel sibling. The rows identify final scoped deliveries, not every
historical PR. No open mobile PRs were returned by the live inventory before
this docs PR was created. This new reconciliation PR is the sole intended
addition; no feature PR is pending from this run.

Core [#80](https://github.com/DashyNMS/core/issues/80) remains administratively
OPEN, but its confirmed legend source defects are fixed by merged
[Core #81](https://github.com/DashyNMS/core/pull/81),
`8774898a8a3ba3e487fc076722f020ffd19e6981`. Successful
[publication run 56](https://github.com/DashyNMS/core/actions/runs/41949476685)
packed/pushed `2.0.0-ci.56` with that RepositoryCommit; merged mobile #542
consumes it. Passing consumer CI restore/tests are recorded in #542's final
review and [graph assessment](480-device-graphs.md). Direct authenticated nupkg
retrieval returned HTTP 403: embedded nuspec metadata was not independently
inspected. That limitation does not negate the verified CI restore or establish
native correctness. Later publication run 57 does not change the pinned ci.56.

## Outstanding source work and bounded unknowns

- **Confirmed, existing follow-up [#532](https://github.com/DashyNMS/mobile/issues/532),
  OPEN, outside the 40 children:** Device Sensors/overview neutralise custom
  no-limit readings, but `DeviceSectionLoader.ReadSensor` still supplies OK to
  Dashboard, SensorPicker and WidgetSnapshot. Audit all consumers under #532;
  this is not merely a device check or accepted deferral.
- **Maintenance #484:** `SetupPageLayout.ShowBack` and header Back remain
  available while `MaintenanceViewModel` awaits scheduling/confirmation. Success
  then navigates `..` unconditionally, including bulk success. Back during that
  await can pop the form before completion pops its caller. This is a substantiated
  source race, not proof it occurred on hardware. Parent judgement owns a scoped
  follow-up under #484; this docs PR introduces no fix or new issue.
- **Shared keyboard lifetime:** `ManualKeyboardScroll.Owners` is a static strong
  reference set; release depends on Disappearing. A retained owner without that
  event can remain registered and keep automatic translation disconnected.
  `SetupPageLayout.Activate` can reclaim ownership, but 13 setup consumers lack
  `IActivePage`: AddCard, GraphPicker, SensorPicker, SpotlightPicker,
  MapLocationPicker, TopCardSetUp, NotifyRulePicker, GraylogDevicePicker,
  GraylogSettings, UnimusSettings, SensorDefinitionsSettings, Thresholds and
  SettingsSection pages. Maintenance and NeighbourGroupEditor implement it.
  The lifecycle dependency is source-confirmed; whether a native transition
  omits Disappearing/Appearing and exposes it is U. Parent coordinates shared
  ownership with accepted #405; no setup acceptance is revoked.
- **Preserved rounding limitations:** #471's near-full/100 correction is delivered.
  Other boundary text can still show 94.96 as 95% while Warning, 79.96 as 80%
  while OK, and Availability 99.86 as 99.9% while Warning or 98.96 as 99% while
  Critical. Core tiny-positive rounding is also preserved. Record raw value,
  text and status; no wider rounding redesign or accepted deferral is inferred.
- **Native hypotheses, not invented source blockers:** #461's reported
  three-quarter-width/short base has no proven root cause despite #526's source
  corrections. Graylog long fixed errors may reduce the viewport; floating
  keyboard geometry, recycling/semantic focus and graph payload/SVG identity
  beyond count matching remain U where their assessments say so.

The user's exact #517 decision is **“Keep wrapping; defer sideways underlap.”**
[#549](https://github.com/DashyNMS/mobile/pull/549) and
[the #517 assessment](517-network-graylog-camera.md) own that accepted deferral.
Keep #545's compact wrapping header. Vertical movement cannot create a horizontal
filter lane. No camera-layering fix or device acceptance is claimed; reopen the
visual decision only at the user's request. #288 global work and accepted #405
setup ownership remain separate and intact.

## Ordered remaining device acceptance

For every new result record full installed SHA/build, OS/device, entry/mode,
theme/text, actual versus expected and evidence. All items below currently **U**.
Historical explicit approvals and closed states above remain preserved.

1. **Duo outer, inner portrait and inner landscape geometry first.** Capture
   same-coordinate host height H, pill bottom B, viewport bottom V and V−B,
   separately for side, bottom and top-wide pills, plus camera, native outer clip
   and clipping ancestors. Then independently prove complete final-row/action
   reach clear of glass; footer reach does not prove viewport-base alignment.
   Prioritise #469–#479 shared geometry, #463/#464 backups, #516 Logs camera
   travel, and #517's retained wrapping vertical path within its deferral.
2. **Graylog #461:** measure pushed and embedded detail width, pane/host bounds,
   viewport/base and content clearance on every entry, including Dashboard direct
   messages and narrow/wide changes. Reproduce the 3/4-width/short-base symptom
   before attributing a cause; coordinate #483/#517 without redesigning filters.
3. **Ports #472 and all eleven section modes:** measured filter panel/wheel
   height through open/close animation, maximum text, VO/TB focus and activation,
   recycled cards, search/sort/grouping, graph entry and complete final facts.
   Include neighbour graph sibling bounds and Inventory Tree options independently.
4. **Graphs #480/#481:** all device/interface kinds and ranges, missing/partial/
   zero/invalid legends, wrapping, one toggle per activation, Only/Show all and
   long-press isolation without also toggling. Check final legend and controls.
5. **Lifecycle and input:** fold/rotate during load/search, keyboard dismissal,
   retained tab/child return and resume without Refresh; one glass owner and no
   placement loops. Exercise setup keyboard handover and retained activation
   dependency above, preserving #405 approval. Repeat shared geometry on each
   section; include ordinary iPhone when available, both themes, maximum text,
   VO/TB and Reduce Motion. Use each linked matrix for remaining route/state cases.
6. **Maintenance #484:** now/later, validation/error reveal, single/bulk success,
   partial/all failure, busy confirmation, Back during an in-flight operation and
   actual-caller return using **mock writes only; no real server writes**. Measure
   fixed Schedule and final field with keyboard open/closed. Source-race disposition
   requires parent judgement before claiming completion.

## Handback boundary

This change edits this ledger only. Full intended/staged diff and whitespace
review are the local checks; no local builds, tests, restores, validation scripts
or agents. Parent owns fresh independent Opus full-diff review, current-head CI
judgement and final handback. Code review, CI and device acceptance are separate.
No issue posts/closures, epic ticks, merge, watch, release or app edits are part of
this delivery. Parent and protected #475 checkouts remain untouched.
