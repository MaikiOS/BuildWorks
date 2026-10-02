# BuildWorks development roadmap

**English** | [Русский](ROADMAP_RU.md)

Updated 2026-10-02. Ostrix approved this order after the development review.
The goal is predictable building with real Valheim pieces, then useful group
operations and editable generators. Each stage delivers a complete workflow,
not just a button or a mathematical helper.

## Baseline and evidence

Current source candidate: **0.19.46**, Store v9, Valheim 1.0.16.
0.19.39 owner feedback accepts menu hover, language refresh and camera framing;
Transform selection is provisionally accepted. The owner accepts contour cleanup;
the focused 0.19.41 game series is accepted by owner report on 2026-10-01.
New requests concern collapsible hints, manipulator clutter, hidden attachment
context and compact viewer settings. Keyboard/tool access is approved in principle;
Ostrix approved the Scandinavian manipulator form study on 2026-10-01.
The full editor implementation is authorized. Owner feedback supersedes the .43
first-G gate: .45 keeps the original model material and shows the combined gizmo immediately.
G starts surface Move; R/S/axes retain exact operation-start numbers. Editor
handles are 25% larger. Vanilla points are brighter closed gold diamonds;
generated helpers, including midpoints, are smaller open coloured marks.
Current game tests have one answer list; older scenarios are archived.
Ctrl+1–4 selects tools; MMB click deletes in Select/adding while
MMB drag orbits. Six original engraved SVG/texture assets, filled scale and
screen-distance hover feedback extend the accepted Scandinavian forms.
Collapsible hints, compact dropdowns and active hidden-target context remain.
Ostrix's .45 report on 2026-10-02 accepts tests 1, 3–6 and 8–11, plus language
refresh in 12. Ostrix approved the [design](../specs/roadmap/snap-art-and-hud-proposal.md)
on 2026-10-02. Candidate .46 implements six thematic PNG point signs, Auto
surface movement with native-only Q/E/opt-in helpers and an Attachment chooser,
plus native resource/station icon cards with bounded overflow. Automated checks
and independent review pass; BW-01 awaits the focused owner series 1–8.
Earlier .45 plain shapes above are historical, not the .46 artwork.
All development deployments, upgrades and game tests use **only
`TerrainRamp-1.0-Test`**. `Default/BuildWorks` is the source/artifact tree,
not an installed mod. Do not modify other profiles, mods or saves as a side effect.

Existing capabilities: indexed Hammer/native favorites; isolated editor;
numeric transforms, uniform scale 1–400%, nested groups, independent group/world
anchors; Array/Contour, Undo/Redo and contextual hints; Q/E source selection,
2 m target preview and 0.55 m magnetic capture; native world-series placement and
cancellation. Missing-prefab visuals already exist.

Release, Geometry, Store, EditorBridge, WorldLayout, localization and HostContract
passed on 2026-09-29. This is not current game acceptance. Earlier English and
slanted-piece Q/E acceptance remain historical. The short 0.19.38 smoke in another
profile does not close the dedicated-profile gate. Current checkpoint evidence
is recorded in HANDOFF and the [active pass](../specs/roadmap/current-pass.md).

## Status rules

Planned → In progress → Automatically verified → Awaiting owner → Accepted.
A regression reopens the affected gate. Record date, commit, game version,
profile, DLL hashes and untested scenarios. A build or screenshot never closes
Russian UI, save/reload or multiplayer checks. One bounded stage is active;
dependent changes wait for its gate, unrelated preparation may continue.

## Ordered stages

| ID | Deliverable | Dependency | Status |
| --- | --- | --- | --- |
| BW-00 | Dedicated-profile baseline and reliable handoff | Current core | Focused 0.19.41 owner series accepted; cost/refund accounting and networking pending |
| BW-01 | Consistent input, clear pivots/snap/working plane, compact F9 | BW-00 | .45 input accepted; .46 artwork/Auto/cards approved and automatically verified; awaiting owner series 1–8 |
| BW-02 | Group layout operations without resizing pieces | BW-01 | Planned; focused operation UI approval required |
| BW-03 | Persistent Array and minimal recipe persistence | BW-02 | Planned; safe Store migration and editing/Bake gate |
| BW-04 | Guides and curves in accepted slices | BW-03 + renewed owner discussion | Tool-semantics discussion required before coding |
| BW-05 | Safe exchange and dependency recovery | Stable Store; BW-03/04 before exchanging recipes/guides | Planned; minimal readiness checks start in BW-00 |
| BW-06 | Linked instances, original adaptive pieces, explicit terrain compatibility | Proven generators, persistence and relevant network gates | Deferred; separate contracts |

No promised dates or release numbers before scope and evidence are known.

## BW-00 Establish the current baseline

Enforce a single deployment target; test forbidden targets. Use the dedicated
profile's BepInEx/camera for host checks. Synchronize the existing source-only
GitHub checkout. Build, verify and install the DLL pair only while Valheim is closed.

Owner series: Hammer/favorites; editor menus/input; wall, sloped beam, roof and
furniture contact; Q/E above/below grid; fresh Array/Contour; independent anchors;
Esc/resource rollback; editor save/reopen and world reload; camera near-ground
movement/teardown; English/Russian. Multiplayer is a separate host/remote series
before new world/network behavior, not a blocker for preparing an isolated UI proposal.
Inspect missing/unsupported parts and explain blocking errors; preserve placeholder
data. Full dependency manifests belong to BW-05.

## BW-01 Make existing controls understandable

Latest owner feedback confirms furniture Q/E, nearby targets, below-grid placement,
group pivots, Array, two-part save/reopen, world reload and near-ground camera.
The partial-build Esc cleanup restores the same ghost, but returned resources
drop on the ground; exact cost/refund accounting still needs a count-based check.
One-part saves remain deliberately blocked by the current two-part blueprint contract.

The next UI approval covers an explicit category chooser (cycling a single category
looks inert), aggregate resources and required stations in the native piece panel
instead of a Resources popup, a per-mode shortcut settings page with rebinding,
and double-click Outliner framing while retaining F. Do not auto-jump the camera
after every placement. 0.19.41 draws the contour guide at full edge endpoints,
not the cursor's relative point. The approved F7/button hides blocking editor
visuals entirely; selection stays solid, temporary occluders do not intercept
selection, and document visibility/materials are unchanged. Sampled mesh rays
are still a heuristic, not pixel-perfect occlusion. Six labeled hint groups,
editor-only FOV slider/input/game reset and compact tree rows with anchor badges,
eye/lock columns and an overflow scrollbar are automatically verified. Their
design is accepted by owner report; FOV is disabled in orthographic view.
The [next editor proposal](../specs/roadmap/editor-visibility-proposal.md)
compares three ways to reduce manipulator clutter and recommends explicit active
control families with matched hit tests, collapsible hints and compact viewer
dropdowns. A target-only outline could retain attachment context under F7;
interior snap eligibility is a separate decision. These editor changes are implemented
in .42 after owner approval; they await the focused game series, not another design vote.
The [approved input revision](../specs/roadmap/editor-input-revision.md) replaces
.42 direct-action/plain-digit/MMB-navigation-only controls. In .44 selection shows
Combined immediately; G starts Surface Move, R/S start actions, Ctrl+1–4 selects
tools and MMB click deletes in Select/adding. One Space
popup with F3 search remains. Future tools must not renumber existing bindings;
modal digits belong to numeric entry. Typed values replace mouse deltas from the
original operation snapshot, with exact cancel/one Undo and next-step guidance.
G follows real surfaces excluding the selection; a ray miss
cannot confirm or silently use distant ground. P explicitly chooses the screen
plane; R uses the camera-facing plane until constrained, S is uniform. XYZ chooses
an axis, repeated XYZ switches World/Local, Shift+axis excludes that axis.
Q/E and wheel retain source-point/yaw behavior in surface placement. Independent
Move/Rotate/Points/Scale filters have matching visibility and hit tests; selection
shows Combined by default and Array retains its controls. Tooltips use actual glyph measurements and bounded
wrapping; EN/RU fit and supported scales remain part of owner UI acceptance.
Community translation overrides
need a documented file format and fallback/reload tests before shipping.

Approve a small wireframe before visible UI/icon changes. Distinguish placement
anchor, transform pivot, local/world axes and source/target snap points. Select
with editable objects keeps numeric fields; multi-selection explicitly labels
deltas. Bottom hints show the action/state and keys available now.

Decide explicitly between fixed Y=0 and a chosen working plane; auto-grounding
must not look like changing document coordinates. Always show the active snap
target, including crowded scenes with over 24 candidates. Keep 2 m preview separate
from 0.55 m capture; do not blindly change prefab origins.

Acceptance: no UI click-through construction; text owns typing; context menus
survive the intended gesture; gizmo/numeric changes agree; below-grid objects
remain visible/reachable; EN/RU panels contain all controls at supported UI scales.
Reuse existing views/math. Compact F9 comes here, not at the end of the roadmap.

## BW-02 Edit group layouts without changing size

Distinct operations: equal local per-element translation, fixed radial offset,
and position-only layout scaling. Target each piece or each immediate child
assembly as a whole. Expand selection by prefab/children/group and inversion;
then Align min/center/max, Distribute and duplicate Mirror.

Acceptance: four beams move inward without changing geometry size; intended
rotations and child-assembly distances survive; only intended nodes change;
preview/cancel/apply and one Undo. No general recipe framework or Store bump
is needed for these direct commands.

## BW-03 Save and re-edit an Array

Reuse existing Array math. Add only the first generator's recipe data: canonical
source subtree, parameters, deterministic logical IDs, anchor mapping and explicit
Bake. The [recipe contract](../specs/spec-pattern-recipes.md) defines safety,
not an obligation to ship every possible operation immediately.

Acceptance: count/spacing editable after reopen; no drift/lost hierarchy;
preview=commit=Bake; appropriate Undo/order handling; safe v9 migration;
unknown future recipes are preserved or blocked, never silently discarded.

## BW-04 Discuss guides before implementation

First agree with Ostrix: magnetic frames for manual construction, repeated-group
generation, or both; control-point editing, spacing/count, orientation/roll,
source/pivot, open/closed paths and persistence. **This includes Line A/B.**

After approval: Line A/B → polyline → arc → Bezier, each saved, editable and
cancellable. Existing curve tests do not approve the tool. Ordinary pieces stay
rigid. Adaptive original geometry needs a later mesh/UV/collider/snap/network
contract. Move Contour to a common evaluator only after that evaluator is proven.

## BW-05 Exchange without data loss

Versioned import/export, dependencies, ready/blocked reasons, identity collisions,
missing-piece recovery and lossless transforms/groups/anchors/metadata. Unknown
input must not overwrite valid documents. No automatic mod installation or
third-party mesh/texture extraction.

## BW-06 Expand from demonstrated needs

Linked instances with explicit Make Unique; original adaptive parts/materials;
then cooperation with a separately maintained terrain mod, without hidden earth
changes. No near-term node graph, expressions, full Fields/dynamics, arbitrary
external references or third-party mesh deformation.

## Checkpoints and GitHub synchronization

After each verified step: review the diff, run affected checks, obtain independent
correctness review for non-trivial changes, update EN/RU status, commit a working
checkpoint and push normally to [MaikiOS/BuildWorks](https://github.com/MaikiOS/BuildWorks).
Ostrix authorized this on 2026-09-30. Preserve unrelated changes. Publish only
source and safe docs: no force-push, saves, game DLLs, personal logs/paths or
automatic binary releases. On sync failure keep the local commit and report
the actual error, never claim success.

Pause for owner UI/icon approval, the prepared game-test series or the renewed
curve discussion. Resume from the recorded checkpoint, not archived STOP text.
Report new access/safety blockers truthfully. Deployment does not authorize
launching the game or changing saves.
