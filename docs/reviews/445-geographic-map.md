# Geographic map review — #445

Baseline: main at 185216035c96733009e5aae6f7c3770907b47479 (#467).
Read live #445, #442 and #288, CLAUDE.md, developing/design guides and
DESIGN-REFERENCES.md; inspected NetworkMapPage before selecting this approach.

## Scope assessment recorded before implementation

| Concern / mode | Assessment | Resolution |
| --- | --- | --- |
| Pinned Map tab; unpinned More → Map → Back | Fix required | One glass owner, pushed-only Back; retain header Back elsewhere |
| Devices routes | Compliant in inspected code | No current direct Map caller found; Map → Show in Devices passes exact location; row opens device ID; retain navigation service routing |
| Canvas / zoom / pan / selection | Fix required | Keep WebView geometry stable beneath overlays; relocate Leaflet controls and attribution into unobscured area |
| Single pin / merged cluster / selected devices | Fix required | Wrapping heading/actions and device text, bounded scrolling card; preserve single-location-only Devices action |
| Narrow / large text / folding | Fix required | Recompute card height and control clearance on size/safe-area/glass changes |
| Accessible alternative to pin tapping | Fix required | Searchable finite location picker uses stable pin objects; whole device rows expose full names, details and state |
| Counts / unlocated devices | Fix required | Preserve counts; include unknown devices in pin total and explicit grey ring segment; explicit no-coordinate state |
| Initial loading / refresh / error / empty | Fix required | One overlay BusyCard, visible Retry on error, no viewport row insertion |
| Tile settings / attribution / security | Compliant; geometry needs fix | Preserve bundled Leaflet, nonce CSP, JSON escaping, bridge allowlist, external browser, UA/Referer and custom tile setting |
| Persistence / permissions | Compliant | Read-only map; no new storage, permission or credential flow; refresh retains selection by location name |
| Search / keyboard | Compliant | No Map search or input exists; no new search contract |
| Motion / themes / screen reader / camera / resume | Unverified on device | Follow shared geometry; exact checks below |

#288 has no comments or newer acceptance evidence on the live issue. Its
mock-up and global light-theme work remains open; this page implementation
uses the guide-authorised NetworkMap reference, and does not complete #288.

## Evidence

Code: independent Claude Opus 5.5 Medium full-diff reviews completed through
50f55c7. That review confirmed prior fixes and found four low cleanups, addressed
below. CI at 50f55c7 passed unit tests (including the Node camera bridge), Android,
iOS simulator and CodeQL actions/C#; Swift remained pending at review. Final CI
after the cleanup is pending. No local builds/tests executed.
Device: all outcomes unverified; no screenshots/build acceptance claimed.

## Remaining device steps

On Duo outer, inner portrait and inner landscape, then an ordinary iPhone,
Android and older iOS: test light/dark and largest text; open pinned and
More-pushed Map, return from device detail and Show in Devices, switch tabs,
resume, rotate/fold repeatedly. Confirm exactly one glass owner and pushed-only
Back, header Back fallback, no placement-loop diagnostics, camera clearance,
reachable zoom and attribution links above the selected sheet and bottom/side
pill. Pan/pinch, then select a pin or chooser location and verify it centres above the card; closing must not flash. Check
merged pins, identical coordinates, long location/device names, last device row,
VoiceOver/TalkBack chooser and device activation. Exercise empty server, all
unlocated, mixed unlocated, failed initial load and failed refresh then Refresh;
old map must survive failure. Check default/custom HTTP(S) tile settings,
external attribution navigation and Reduce Motion. Searchable chooser operation with a large catalogue
and very large text on short landscape displays require device verification.

## Implemented contracts and limits

MapPage and its location picker consume the changed MapHtml/MapViewModel APIs. DeviceItem gains
additive details/announcement properties; existing consumers keep their bindings.
No shared control or accepted page was edited. NetworkMapPage is unchanged. Cards
use natural row height up to a 200pt target below 740pt canvas height, otherwise
300pt, further limited by available space. A measured Close row plus separate 44pt title and 44pt list
reserves take precedence over that cap on very small displays. The list remains
finite and the title can scroll independently. Entrance and exit use named motion and Reduce Motion;
Close hides the card before resetting transforms and clearing selection. Switching
selection keeps a stationary card in place.

The bridge retains selected IDs through card exit and dims nonselected marker
groups over 250ms (immediate with Reduce Motion). Redrawn clusters use those IDs.
Initial bounds use header/status/glass padding; selected locations use an offset
camera centre above the card. Clearances are reapplied after document navigation
and resize. Leaflet retains geographic clustering, tiles and attribution.

The chooser uses natural location order and device-count units, and is disabled
in the header or omitted from glass while busy or without pins. Glass is rebuilt
only when chooser availability changes. It pushes a SearchField/SetupLayout page
with a finite CollectionView. Long names wrap; choices use pin object identity,
not label lookup. Cancelling returns without changing selection. Large-catalogue
search, keyboard and accessibility remain device checks. Header/status use page-background surfaces and
a 20pt fade. Zoom targets are 44px, correcting Leaflet's 30px touch default.

## Review fixes after 83e8ed4

All six medium and six low findings were confirmed and addressed. There is no
accepted deferral of selected-pin dimming. M1/M2 cover the bridge selection and
camera changes; M3 adds an explicit unknown ring share; M4/L1 fix card switching
and exit; M5 makes each wrapping device row a single accessible tap target; M6
adds opaque header/status surfaces. L2 caps the card; L3 orders and gates choices;
L4 adds conditional captions and Retry while retaining stale data; L5 adds
behavioural tests for unknown membership/ring data, empty/unlocated/loading/error
states, retained locations and property notifications; L6 corrects route and
counts evidence and enlarges zoom targets.

Tests were added but not run locally, per instruction. This implementation pass
inspected the full PR diff and whitespace; it does not replace the parent's final
Claude Opus 5.5 Medium full-diff review. CI and all device outcomes remain pending.
In particular, verify Navigated-to-clearance ordering after refresh, native glass
versus Leaflet controls, huge text on short landscape screens, chooser long names
and large catalogues, Duo orientations/folding/rotation/resume and both themes.

## Follow-up review after 11b5667

The fresh Claude full-diff review confirmed M1–M6, then identified one medium
and seven low findings. This implementation addresses all eight:

- Camera centring occurs only for an explicit selection notification. Resize
  invalidates Leaflet geometry without centring; changed clearances pan only by
  the change in uncovered centre, preserving the user's pan. Close queues empty
  IDs before the smaller clearance. Bridge calls are serialised.
- Each selection has a version, including repeated selection of the same pin.
  A new selection aborts/reverses the named transition from its current position;
  stale close completion cannot clear it. Entrance travel uses measured height
  with a fallback and starts after dispatch, following NetworkMap's pattern.
- The card measures row-template content only until its available list cap is
  filled. The CollectionView keeps a finite HeightRequest; it is never put in
  a ScrollView. Small selections use their natural content height. Close remains
  outside the separately bounded scrolling heading and list; huge text cannot
  reduce the card to zero. Native measurements remain device-unverified.
- Focus waits until Leaflet has a view; a timed fallback supplies an initial fit
  if native clearance is lost. Initialisation happens once, including an empty
  coordinate source, and later native clearance does not reset the view.
- This pass moved the chooser behind IDialogService; the final pass below
  supersedes that action sheet with the guide's searchable finite picker.
  No action-sheet deviation or misleading server-ID label remains.
- Empty-state text suppresses the redundant all-unlocated caption. DeviceItem
  supplies HasDetails and a description joining nonblank fields; the row hides
  an empty details caption and has no trailing spoken comma.

Added behavioural tests cover stale-close versions, natural chooser order,
unique duplicate-name labels selecting/opening the correct device, empty device
speech/details, and mutually exclusive unlocated/empty status. Existing ring,
membership and security tests remain. No local builds or tests were run.

Prior CI at 11b5667: unit/platform and CodeQL actions/C# passed, Swift pending,
as reported in the dispatch. That evidence does not validate these follow-up
changes. New CI and the parent's fresh Claude review are pending. No device
acceptance is claimed. Check merged-pin +2 zoom through centring, redraw opacity
flashes, whole-row VoiceOver/TalkBack, long-name/large choosers, Leaflet/glass/side
pill clearance including 12pt card margins, refresh ordering, huge text on short
landscape, Duo fold/rotate/resume, themes and Reduce Motion.

## Final review fixes after f1b40a4

The fresh Claude review confirmed seven of eight earlier findings. This pass
fixes the remaining merged-focus race and the five low findings:

- Marker clicks store a keyed target zoom and start no camera animation.
  Focus exclusively applies that zoom synchronously with the uncovered-centre
  offset. Single-pin and picker choices retain zoom; subsequent rapid taps
  cannot leave a competing animation to overwrite the offset.
- The 250ms fallback records that its fit lacked native clearance. First native
  clearance repairs that fit if the user has not moved the map. Programmatic
  camera calls are guarded separately from user movement. A focused selection
  keeps its zoom and receives the safe offset instead of another full-map fit.
  Actual user movement prevents the repair. Later clearance changes pan only
  by a delta while selection is dimming; without selection the view deliberately
  stays where the user left it. Resizes never repeatedly fit or centre the map.
- The whole-card minimum is measured Close + 44pt title + 44pt list + padding.
  This separately guarantees a nonzero device viewport; natural row measurement
  can shrink small selections while the CollectionView remains finitely bounded.
- Map → Choose a location pushes a registered, transient picker with the calling
  map as its owner. SetupPageLayout supplies pushed Back/glass, safe clearances,
  rotation/resume placement and paired lifecycle cleanup. SearchField handles
  the search keyboard; disappearing explicitly closes it. The fixed search/status
  rows sit above a finite CollectionView with a 20pt fade/spacer. All locations
  remain searchable and naturally ordered, including hundreds of rows. Names
  remain intact; units/coordinates are details and duplicate names with identical coordinates
  gain a neutral choice ordinal. Row objects carry identity independently of text.
  Choosing selects on the original map and pops one page. MapPage defers the
  camera/selection update until Appearing restores its glass and usable WebView.
  Document replacement replays selection after Navigated, so refresh cannot lose
  a focus request sent to the previous document.
  Back selects nothing.
  A refreshed snapshot is rejected with a reopen route rather than selecting
  a reassigned pin index. DI, route registrations and constructor consumers match.

Regression coverage: shared tests exercise picker owner/return routes, duplicate
names choosing the right device, 300 locations, natural ordering, search reset,
no matches and refreshed/stale object identity. The Unit tests CI job now also
executes the actual embedded camera bridge using Node's built-in test runner
and a small Leaflet camera model. It checks merged/single/native choices, rapid
taps, the absence of competing animation, safe offsets, fallback repair versus
user movement, pending focus, empty sources and close/resize pan preservation.
These tests were authored and inspected but were not run locally.

At f1b40a4, unit/platform and CodeQL actions/C# passed; Swift was pending at
review, as reported in the dispatch. New CI and the parent's fresh Claude
full-diff re-review are pending. Full implementation diff and whitespace were
inspected. All device outcomes remain unverified: real handlers for row measure
and first-frame travel, TalkBack/VoiceOver whole-row activation, side-pill margins
and glass/44px Leaflet controls, new large-chooser search/keyboard/accessibility,
Duo folds/rotation/resume, themes/Reduce Motion and refresh camera fit while
selected. No chooser deferral, device acceptance, merge or issue closure is claimed.


## Cleanup after review at 50f55c7

Claude confirmed every earlier finding fixed with no medium-or-higher findings.
The four low cleanups now use shortest round-trip coordinate text, constrain fit
padding to leave at least a quarter of each canvas axis usable, remove repeated
stale-picker instructions and correct this evidence. Added regressions cover
51.1 coordinate formatting and oversized clearance on a short canvas. Final CI
is pending; all device checks above remain unverified. The review cannot settle
real Leaflet user-zoom animation timing, handler measurement, accessibility or
Duo geometry. No device acceptance is claimed.
