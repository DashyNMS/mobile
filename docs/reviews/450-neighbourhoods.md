# Neighbourhoods assessment — #450

Refs #450, epic #442. Baseline main 38fa645. Read live #450/#442/#451/#452,
CLAUDE.md, DEVELOPING.md, full DESIGN.md and DESIGN-REFERENCES.md, and PR #490
read-only. FULLDESIGN.md is absent. No queued branch is edited.

## Before fixes

| Concern | Baseline assessment | Resolution |
| --- | --- | --- |
| Names/summaries | Fix required: summary truncates; counts lack units | Wrap all text; explicit unavailable/count text |
| Entry/actions | Fix required: 36pt arrows, small chevron, no list delete entry | Named 44pt Edit and More actions; existing commands |
| Ordering | Compliant: saved order, bounded adjacent swaps | Preserve, including imported IDs/definitions |
| Empty | Compliant: empty saved definitions distinct from network data | Keep saved-source empty text |
| Loading/error | Fix required: count fetch failures invisible | Shared busy helper, error and retry; definitions remain usable |
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
CI: shared tests, Android and iOS builds pending. Parent reviews sequentially with
Claude Opus 5.5 Medium after quota reset and after #489 then #490; no review here.
Parent updates only this PR when its queue turn arrives. No merge or release.

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

No closure or epic ticks. TestFlight only after #450–#452; acceptance remains pending.
