# Health scope assessment (#443)

Baseline: main `268cfb3`. Live #443 and #442 read on 3 November 2026.
References: DESIGN.md, DESIGN-REFERENCES.md, Devices/Alerts placement,
SearchReveal/SearchField, SectionRow and HealthViewModel. Related merged
work: #257 (unknown), #258 (cached readings), #295 (offline), #437
(threshold form). No open Health implementation PR was found.

## Assessment before implementation

| Concern / trigger | Status | Evidence and action |
| --- | --- | --- |
| Four categories; severity filters; counts; search | Compliant | HealthViewModel counts the entire category before filtering; ChipMemory remembers severity only; query and category remain page-local. |
| Unknown and non-finite readings | Fix required | Core-derived status/counts already exclude unknown from OK and keep it visible with all chips off; HealthPage shows only a dash and colour. Add explicit text and row description. |
| Long names, units, narrow/large text | Fix required | HealthPage fixes four segments to equal widths, uses a non-wrapping chip strip, truncates device names and reserves an Auto reading column. Allow category/chip wrapping and stack full row text. |
| Whole-row activation and announcements | Fix required | Tap covers the grid, but there is no combined accessible description. Preserve navigation; combine name, device, value, limits and severity. Describe selected categories and filters. |
| Clear target | Fix required | HealthPage explicitly sets 30pt. Use minimum 44pt. |
| Search, Command-F, keyboard | Fix required | Shared SearchField and Command-F fallback already work. Add ISearchHost for consistency and glass search while retaining SearchReveal: always visible on Android, scroll-revealed on iOS. |
| Finite viewport | Compliant | CollectionView is in a star Grid row, outside any ScrollView. |
| Final row and glass | Fix required | Native safe area alone does not account for extra glass actions. Reuse CoveredEdges and Devices pill/footer placement. |
| First load, refresh, empty source, no matches, failure, cached/stale/offline | Compliant | Existing busy/error/empty gates and SensorRefreshStatus retain last readings and timestamp; keep them. Visual operation unverified. |
| Threshold return | Compliant | Only Settings opens Thresholds; Save validates and persists, Back discards; settings Changed rebuilds loaded Health rows immediately without refetch. No new entry route needed. |
| Entry/return routes | Compliant | Default pinned Health tab, repinned tab and unpinned More page use AppShell page factory; the unpinned route now supplies glass Back and hides header Back on iOS 26+; sensor pushes DeviceSection.Sensors with ID/name, Back returns to Health. Switching tabs and returning calls RefreshIfStaleAsync (one minute). |
| Persistence/security | Compliant | Read-only sensor/device requests use existing session and offline stores; no credential, permissions, cache format or settings schema changes. No Health write except existing severity preferences. |
| Runtime fit, accessibility, insets, motion, navigation | Unverified | Requires devices; source inspection establishes no device outcome. |

All visual changes follow existing guide rules; no new visual policy proposed.

## Evidence

Implementation self-inspection: complete intended diff inspected; git diff --check passed.
CI: initial unit-test job caught an incorrectly typed event raise in the new
regression test; corrected to EventHandler<AppSettings>. CI at 2e913e2 passed shared tests,
Android, iOS simulator and CodeQL actions/csharp; Swift was still pending at review.
Replacement CI after review fixes is pending;
no builds or tests run on this code machine. Regression tests
are added for severity text, unknown visibility, counts and threshold changes
without refetch (the page return itself remains a device check).
Claude Opus 5.5 medium reviewed 2e913e2. Addressed missing glass Back,
explicit child safe-area ownership, equal-fill wrapping segments, pill radii,
missing-value speech, search alignment and assessment inaccuracies. Restored
SearchReveal to preserve existing Android visibility and iOS scroll reveal;
Command-F previously worked through its fallback. Claude re-reviewed the full diff at 4ffd249 with no blocking findings.
Removed the remaining legacy iOS safe-area flag as requested and added the
optional accessibility property summaries. Final CI is pending.
Segments retain equal growth without a zero basis so they can wrap. Severity
chips retain pill geometry with a 22pt radius at the accessible 44pt minimum.
Every device outcome remains unverified.

## Remaining device steps

1. On the PR build, use synthetic sensors with long names/device names,
   long units/limits, critical/warning/OK and absent/non-finite readings.
   Check light/dark and largest text on Duo outer, inner portrait and inner
   landscape, folding/rotation without reopening, plus ordinary iPhone and Android.
2. Open pinned Health, unpin and open from More, repin, switch tabs and resume;
   verify glass Back returns to More on iOS 26+, one glass owner, header actions on older iOS/Android, and final row
   clears bottom/side/top pills without doubled side or bottom insets. Inspect placement diagnostics for overlap/loops.
3. Toggle all severities off: unknown remains visible and says unavailable;
   check category totals, search/no matches, Clear and relaunch filter memory.
4. Open search from glass/header and Command-F after scrolling; type, refocus,
   Search and scroll to dismiss the keyboard; query must remain. Android search
   stays visible; iOS search reveals on upward scroll and remains while in use.
   Repeat rapidly with Reduce Motion enabled and disabled.
5. VoiceOver/TalkBack: hear complete row name/device/reading/limits/severity
   once, activate the whole row once, confirm device Sensors and Back; hear
   selected category/filter state, and operate Clear at large text.
6. Open Settings → Health thresholds; change a negative/decimal threshold,
   Save and return to Health: severity/counts change. Repeat with Back to
   discard. Verify no forced fetch within the fresh interval.
7. Exercise first load, pull refresh, slow refresh with kept readings, empty
   source, failure with/without cache and offline timestamp; no false empty/OK.
   Obtain explicit user acceptance before closing #443 or ticking #442.
