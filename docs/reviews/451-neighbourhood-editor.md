# Neighbourhood editor assessment

Refs #451, epic #442. Baseline main da65c6b. Read live #442/#450/#451/#452,
CLAUDE.md, docs/DEVELOPING.md, full DESIGN.md and DESIGN-REFERENCES.md,
#490 diff/assessment and #491 diff/assessment read-only.

## Scope and contracts

Neighbours opens saved neighbourhoods; Add opens the editor without an ID;
Edit passes GroupIdParameter. New defaults to one blank rule. Edit clones the
saved definition. Name, all/any, rule field/operator/value, add/remove and regex
validation are draft only. Relative Back discards without confirmation. Save
requires a nonblank name and valid regex, trims supported values, drops blank
supported rules, retains unsupported rules and extension fields via Clone,
preserves ID and position, persists then pops. Unknown IDs retain the existing
new-definition behaviour. Zero rules and duplicate names remain permitted.
There is no seed picker, search, device selection or network write in this form.
Counts use the cached NeighbourDirectory snapshot and Core matching through
NeighboursViewModel.InGroup, not health or availability. Counts remain absent
until known; no unknown value becomes zero. Existing count semantics are retained.
Delete acts immediately on the saved ID after the existing destructive
confirmation, independent of draft edits. List reorder/delete remain #450.
No credentials, permissions, schema, matching implementation or server data change.

## Findings and disposition

| Concern | Baseline and result |
| --- | --- |
| New/edit fields and name | Fix required: ReadableColumn capped width. SetupLayout now fills width with shared 16pt/measured gutters. SetupSection and Field reuse shared presentation; nonblank accessible descriptions supplied. |
| Match and field/operator selection | Fix required: side-by-side chips clipped long text. Stacked native pickers expose the existing ordered labels and indexes; all/any semantics preserved. Native large-text picker rendering remains unverified. |
| Rule text and removal | Fix required: 32pt Remove and horizontal layout. Wrapping status, stacked controls, 44pt secondary Remove with rule-specific description; Add secondary, 44pt. Unsupported rule remains read-only/removable. |
| Validation | Fix required: fixed Save now scrolls to the named error and announces the captured message; name/regex refusal and inline regex error retained, no new minimum-rule/name uniqueness constraint. |
| Save and failure | Fix required: duplicate header/scrolling Save. One implicit-primary Save outside finite viewport; async command disables concurrent Save. Persistence failure restores in-memory definition and retains draft for retry, with non-sensitive error. IO/access failures covered; underlying store atomicity remains its existing contract. |
| Delete | Compliant: immediate saved-definition confirmation retained; DangerButton remains subordinate in a labelled saved-neighbourhood section with an immediate-action caption. No new Undo or delayed persistence. |
| Back/header | Fix required: scrolling Back. Shared PageHeader gives visible Back on shared top line on Android/older iOS and one glass Back on modern iOS, labelled from actual caller. #490 supplies root-title fallback. |
| Insets/keyboard | Fix required: adopt SetupPageLayout in one step; no old safe-area/helper remains on this page. Native iOS top only, shared sides/bottom, native scroll adjustment disabled; measured pill/keyboard Save placement and focused-field scrolling reused. Android native safe areas/resize retained. Hardware results unverified. |
| Lifetime/reentry | Fix required: retained More stack must reclaim controls. IActivePage restores the helper through AppShell’s central navigation/resume hook without reloading the draft. Helper activation is idempotent; subscriptions/tokens dispose on disappearance. Dynamic form size changes rescan focus handlers without duplication. |
| Search/drag | Compliant/not applicable: editor has no search/list; no SearchReveal added. Setup pickers elsewhere remain unchanged. |
| Themes/accessibility | Primary uses shared themed disabled style; secondary actions use SecondaryButton. Wrapping text and named native inputs/actions supplied. Both themes, screen readers, largest text and floating keyboard remain unverified. |

## Dependencies and shared ownership

Queue #489 -> #490 -> #491 -> this PR. No other branches changed. #491 owns
NeighbourGroupsPage and list VM refresh; editor changes only its portion of
NeighbourGroupsViewModel.cs. Separate failure test file avoids its test hunks.
Parent updates ONLY this branch when its turn follows merged predecessors.
#452 owns link detail. TestFlight follows #450-#452, not this implementation.

SetupPageLayout adds explicit retained activation, and refreshes focus handlers
when scrolling form content resizes. Existing consumers keep their persistence
and action contracts: Add card, Graph, Sensors, Ranked cards, Spotlight, Graylog,
Unimus, sensor definitions, thresholds, settings sections and immediate pickers.
Their layout/lifetime regression checks remain required; this is not new device
acceptance for #405. No shared PageHeader, BackBar, glass or route policy change.

Pinned Core 2.0.0-ci.52 is unchanged. No new Core API is introduced: Clone,
NeighbourViews, rule enums/labels and InGroup are existing compiled-source usages
and regression fixtures. Package binaries/source are unavailable in this checkout;
CI must verify compatibility. No package restore or local script was run.

## Review and validation evidence

Full intended diff self-inspected against the scope and DESIGN; whitespace
checks passed. Claude Opus 5.5 Medium reviewed head 1466017 against main da65c6b:
Save rollback, IDs and imported rules were confirmed; two medium and three low
findings were addressed. Failed Save now scrolls and announces errors, activation
uses IActivePage, Delete is labelled as immediate, and regression assertions
verify the original saved name and rules remain untouched.

Unit tests passed in both CI runs at the reviewed head; one iOS simulator run
passed while remaining platform/security jobs were pending at review time.
Replacement CI for these fixes remains pending. No local builds, tests or scripts
ran. Existing tests cover regex refusal, imported-rule retention and matching;
failed-save tests cover draft, rollback, identity, retry and Back behaviour.

## Device evidence required

All results below remain unverified. Record commit/build, OS, device, keyboard,
text size, theme and diagnostics for each run; code inspection and CI do not
establish physical results. Use synthetic Lab/AP names and example.net only.

Run each new/edit scenario on Duo outer, inner portrait, inner landscape and an
ordinary iPhone, light/dark, default/largest text, TalkBack/VoiceOver, with
fold/unfold, rotation in both directions, background/resume and keyboard dismissal.

1. Neighbours -> neighbourhood list -> Add: name, all/any, each field/test,
   long value, invalid/valid regex, add several rules, remove first/final rule.
   Back cancels without changing saved definitions; reopen starts clean.
2. Edit saved/imported definition: change name/rules, Back cancels; Save persists
   same ID/order and list refresh shows changes. Return to Neighbours retains
   selection/match semantics. Remove unsupported rule only on Save.
3. Inject persistence failure for new and edit: no navigation or in-memory
   change, draft remains, error scrolls into view and is announced even from the final rule, retry Save succeeds once. Rapid Save
   taps do not duplicate. Confirm/cancel immediate Delete separately.
4. Docked keyboard on name and final newly added value: final field and fixed
   Save reachable; dismiss restores vertical-pill bottom alignment. Bottom bar
   clears Save, iPad top pill is not treated as bottom. Floating keyboard must
   not strand focused input. Repeat after rotation/fold/resume with keyboard up.
5. More -> Neighbours -> list -> editor, switch to pinned Devices and back to
   More: draft survives, actual-caller Back and single glass owner restored
   without refresh. Repeat repeated push/pop and resume; no stale handlers.
6. Check camera/native safe areas, full-width gutters, long labels/validation,
   final Add/Delete reachability, 44pt whole actions, accessible selection and
   removal announcements, primary disabled/busy contrast and no duplicate Save.
7. Smoke-check shared setup consumers after dynamic focus subscription change,
   including adding fields, child return, immediate chooser pop and repeated
   appearance/disappearance. Watch for duplicate observers or placement loops.

No merge, release, issue closure or epic ticks. User acceptance remains pending.
