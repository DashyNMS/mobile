# Neighbourhoods assessment — #450

## Current corrective pass, 4 November 2026

Baseline `d271e9c`, current origin/main including Health #519 and Alert rules
#520. Live #450/#442/#451 and cached full #450 comments were read alongside
the reconciled assessment. Earlier review/CI evidence below is historical and
does not validate this changed head. New CI and the parent's independent
Claude Opus 5.5 Medium review are pending separately. No local builds, tests,
scripts, agents or device checks were run. Full diff against origin/main and
DESIGN.md and whitespace are inspected before commit.

The user comment on #450 reports top text clipping. It explicitly leaves bottom
extent UNVERIFIED. The tested New neighbourhood presentation is accepted under
#451; this pass changes neither editor mode nor Save/persistence. Earlier
accepted pages retain their acceptance.

| Concern / trigger / location | Current assessment and correction |
| --- | --- |
| Open saved list on iOS 26+, top text at Body/title | Corrected source gap. Hiding BackBar left the row-1 PageTitle outside the visible fitted line. Reuse PageHeader, whose TopBar.Fit registration owns the visible title line. Actual reported clipping and largest-text fit remain UNVERIFIED on hardware. |
| Native top ownership | Source compliant with existing header policy. iOS page keeps native top only; Body has no safe edges. TopLineSpace may release top on iOS 27.1+ only when measured reserved regions permit fitting beside the status bar. On iOS 26 it retains native top plus the standard header padding. No guessed top offset or second top inset. |
| Retained More return/resume, Activate and queued Moved | Corrected departure gap. Active/current-page guard rejects queued placement after departure. Existing IActivePage central owner remains; watch uses ??=, Moved removes before adding, disappearance disposes/removes and owner-safe Hide remains. No Shell/Window hooks added. Native return behaviour UNVERIFIED. |
| Back/Add, glass and fallback | Source compliant. PageHeader keeps the visible fitted title, glass gets Add and route-sensitive caller Back, older iOS/Android retain header Add and caller Back. Current list is pushed from Neighbours; pinned Neighbours and More-pushed Neighbours are caller modes, not a newly supported list tab. |
| Full names/summaries/counts | Source compliant. WordWrap, full single-rule description, multi-rule all/any summary, singular/plural neighbour counts and explicit unavailable text remain. Full unknown imported rule handling and match semantics unchanged. Speech, contrast and maximum-text fit UNVERIFIED. |
| Add/Edit/return | Source compliant. Relative editor route; Edit passes exact saved string ID. OnAppearing reloads saved definitions and cached counts; activation alone restores controls without refreshing data. Back discards editor clone, Save returns to list. No migration. |
| Order/delete | Source compliant. Saved order, bounded menu moves and immediate persistence remain; commands re-resolve IDs after menus/confirmation. Delete retains destructive confirmation and irreversible warning. Selection fallback to All remains Neighbours' responsibility. No Undo added. |
| Empty/loading/error/refresh | Source compliant. Empty means no saved definitions, independently of network counts. BusyCard covers count requests; retry/pull forces reload. Failed counts retain last good rows; unchanged failed cache keeps warning, newer successful cache clears it. Zero is a real match count, unknown stays unavailable. Native cold/offline/denied/retry behaviour UNVERIFIED. |
| Usable base and final-row reach | UNVERIFIED, no bottom defect asserted and no adjustment made. See bounds trace below. |
| Search/keyboard, camera scroller, fixed Save/compare/toast | N/A on this list. No search Entry, chip scroller, draft form, comparison or toast. Camera clearance for title/rows remains required. Editor keyboard/Save remains the accepted separate #451 contract. |

### Bounds trace and remaining measurements

Native page hosts Body, whose final star row hosts the finite Grid, RefreshView
and CollectionView. iOS native page bottom is released; Body and RefreshView
have SafeAreaEdges=None. Native CollectionView scroll adjustment is Never.
Body applies side gutters once via ForScrolling; footer alone reserves
12 + ForScrolling.Bottom inside scrolling content. It does not shrink the
viewport. The 20pt matching-background fade has a 20pt content header and passes
input. There is no fixed bottom action or card surface to align here.

Devices/Alerts measure native pill.Bottom to place a viewport/card bottom;
Health now applies that policy to ListCard.Margin with a separate content
spacer. Copying those margins to this full-height scrolling surface would
change its viewport without evidence. Preserve the current footer pending
measurement. In one native page coordinate system record page/host/Body/row/
RefreshView/CollectionView frames, content extent/offset, safe insets, pill and
glass-action bounds. Compare viewport and final-content bottom with H and B,
including side-pill usable base B and delta H-B. Distinguish full background
reach, actual viewport reach and last-row clearance. Check wide top pills
separately from bottom pills; missing geometry must use the existing fallback.
No physical coordinates are available in this pass.

### Device evidence required for this head

Every result remains UNVERIFIED. Bind each result to the final PR head SHA,
installed build number, OS version, device/display, theme and text size. Do not
reuse historical CI or acceptance as device evidence. Parent records final SHA
and build/OS binding after CI and installation; no build is installed here.

1. On Duo outer, inner portrait, inner landscape and ordinary iPhone, run both
   themes and largest text. Open from pinned Neighbours and More-pushed
   Neighbours. Measure title glyph bounds and fitted header against native top,
   camera and fade. Verify complete title, introduction, errors and empty text.
2. Measure the complete bounds trace above for side, bottom and wide top pill,
   and missing geometry fallback. Scroll to the final long wrapped row; prove
   usable-base reach as well as unobscured Edit/More. Collect Top line and glass
   diagnostics, checking no placement loop, partial dark bar or duplicate inset.
3. Fold/rotate while open, switch tabs away/back to retained More, background/
   resume, enter editor and return by Back/Save/delete. Check one glass owner,
   actual caller Back, one Add action and fallback on older iOS/Android.
4. Use synthetic long names and single/multiple/unsupported rules, zero/one/many
   counts, empty definitions, cold/slow/offline/denied loads, retry/pull and
   failed refresh with retained data. Return after editor loads links to verify
   count/error recovery. Confirm unknown never becomes zero or healthy.
5. Add/cancel/save, edit exact imported ID/name/rules, reorder first/middle/last,
   restart, cancel/confirm delete including selected definition. Check full
   saved definitions, all/any/regex matching, order, selection and viewport.
6. VoiceOver/TalkBack read complete names, summaries, counts and target-specific
   actions; activate once and measure 44pt targets. Check readable colours in
   both themes, Reduce Motion and repeated transitions. List keyboard dismissal
   is N/A; child keyboard return must leave list geometry and controls correct.

No merge, watch, release, issue closure or epic checkbox change is authorised
in this pass. Refs #450 and #442 remain non-closing.

Refs #450, epic #442. Integration baseline main 8f6dc2a (included in
43c22f9e00187d091997e901e65da67f5e0dba05). Read live #450/#442/#451/#452,
CLAUDE.md, DEVELOPING.md, full DESIGN.md and DESIGN-REFERENCES.md, and PR #490
read-only. No queued branch is edited.

## Before fixes

| Concern | Baseline assessment | Resolution |
| --- | --- | --- |
| Names/summaries | Fix required: summary truncates; counts lack units | Wrap all text; explicit unavailable/count text |
| Entry/actions | Fix required: 36pt arrows, small chevron, no list delete entry | Named 44pt Edit and More actions; existing commands |
| Ordering | Compliant: saved order, bounded adjacent swaps | Preserve, including imported IDs/definitions |
| Empty | Compliant: empty saved definitions distinct from network data | Keep saved-source empty text |
| Loading/error | Fix required: count fetch failures invisible | One busy card for every count request, error and explicit fresh retry; definitions remain usable |
| Layout | Fix required: unbounded BindableLayout in ScrollView | Finite virtualised CollectionView, reserved matching fade/footer |
| Navigation/glass | Fix required: BackBar only, no glass ownership | Restore on appearing, retained navigation and resume |
| Native/accessibility | Unverified on device | Explicit matrix below; no acceptance inferred |

## Modes, routes and contracts

Neighbours (pinned root or More-pushed) calls EditGroupsCommand -> relative
Routes.NeighbourGroups (`neighbourgroups`); Add -> Routes.NeighbourGroupEditor
(`neighbourgroup`); Edit passes the exact saved string ID under
Routes.GroupIdParameter (`group`). Editor Save
and confirmed Delete pop Routes.Back; Back discards its clone. List OnAppearing
refreshes definitions and counts after return. Actual caller labels use BackBar;
#490 owns its improved root fallback. List is pushed, not itself a pinned tab.
No search exists here and none is added; Neighbours search is outside this issue.
#451 owns editor UI, #452 link detail, #288 Map/Neighbours mock-ups remain open.

AppSettings.NeighbourViews remains local settings with the desktop-compatible
schema, not a LibreNMS write or a credential store. Reorder/delete save immediately;
Add/Edit draft saves only in editor. Existing destructive confirmation and
CannotBeUndone text remain; no Undo/toast existed or is introduced. Counts use
NeighbourDirectory's last cached snapshot, not a new freshness claim. Core matching,
all/any, regex and unknown imported rules/extensions remain untouched. Neighbours
retains selected ID unless deleted, then falls back to All. No schema migration.

## Evidence and pending device matrix

Code: full-diff self-inspection and whitespace checks at handoff. Regression tests
added but not run. No local builds/tests/scripts, external review or delegation.
Claude review of 43c22f9 confirmed M1–M3 and L1–L4: stale cache errors/no-op
retry, collection resets, duplicate activation ownership, count wording, retry
progress, boundary menu actions and outdated evidence. This revision clears
errors on valid-cache reuse, explicitly reloads on Retry/pull, retains last-good
rows on failure, emits a single Move or Remove without replacing unchanged
rows, delegates activation to #490's
IActivePage owner, uses singular/plural counts, displays one busy overlay for
all requests and omits boundary moves. Commands re-resolve saved IDs after sheets.
Regression coverage added for cache recovery, forced reload, busy state, retained
rows and collection events; not executed locally.

At the 43c22f9 review, unit/iOS/CodeQL actions and C# passed; Android and Swift
were pending. At this revision's preparation, Android also passed and Swift
remained pending. New pushed-head CI is unknown until CI runs. Parent owns the
fresh full-diff Claude 5.5 Medium review and CI/merge handling. No merge or release.
Editor #451/future PR #492 retains the Clone ID and Save/Delete Routes.Back
dependency; this revision does not edit the shared editor.

Device evidence: ALL UNVERIFIED. For each case run Duo outer, inner portrait,
inner landscape and ordinary iPhone, both themes, largest text, TalkBack/VoiceOver,
fold/rotate and resume. Record exact build/head, OS, result and diagnostics:

1. Empty saved source; Add; cancel draft; Save; return refreshed list. Reopen Edit
   with exact imported ID, rename/change rules, Save/Back; selection preserved.
2. Long multiword names and long rule summaries, unknown imported rule; all text
   wraps. Edit/More each announced with target and activated once; all actions 44pt.
3. Move first/last and middle rows; saved order and definitions survive restart.
   Delete cancel leaves order/selection; confirm retains existing irreversible
   warning, removes only target, returns All if selected definition was removed.
4. Slow/offline/denied count fetch, retry, zero matches and cached counts: definitions
   stay usable; no false zero/OK, no empty-source claim from a network failure.
5. Neighbours -> list -> editor -> Back; list -> Neighbours; pinned and More paths,
   switch to another pinned tab and back to retained More stack, background/resume.
   One glass owner; relative Back/actual caller, no refresh needed to restore glass.
6. Scroll first/final row below matching 20pt fade; camera, side and bottom pill
   clearance, no dark partial bar/double inset/placement loop. No toast to measure.
7. Far down a long list, move and confirmed delete preserve viewport and focus;
   no empty flash. Retained More tab return restores glass Back and Add.
8. Offline count failure -> editor successfully loads links -> Back clears the
   old error. Retry/pull with cached links fetches again; failure retains counts.

No closure or epic ticks. TestFlight only after #450–#452; acceptance remains pending.


## Follow-up review at f7fa227

Claude confirmed the count reload and central activation fixes. Refresh now
replaces only rows whose definition instance or count changed; Move emits one
Move event without a follow-up Refresh, and an unchanged Refresh emits no events.
A failed refresh retains its cache reference: later visits clear the error only
when the directory has a newer successful snapshot, preserving offline warnings
for unchanged cached counts. Collection-event and recovery regressions cover
these cases. At f7fa227 unit tests, CodeQL actions/C#, and one Android/iOS run had
passed; other platform runs and Swift were pending at review. Replacement CI is
pending, with no local builds/tests and no device acceptance. Refs #450 remains
intentional under the user's device-acceptance gate.
