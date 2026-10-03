# BuildWorks current development handoff

**English** | [Русский](HANDOFF_RU.md)

## Verified candidate 0.19.49

Ostrix approved the point-purpose workflow and two sizing modes on 2026-10-03.
Editor points now have only native gold and generated blue provenance. Default
near-cursor reveal, persistent selected A, manual All/Native-only and depth dimming
replace the idle multi-colour cloud. Existing thematic PNGs are reused, not replaced
by a concept. Explicit helper A remains visible/usable under Native-only; hidden
helpers are excluded from Auto/Q/E and target scans.

LMB selects A without starting a drag. The gizmo moves to A; G uses A as source
grip, R/S use it as pivot. Target capture never replaces A. Optional B defines
working X along A→B; coincident points are rejected and antiparallel direction
uses deterministic original-up roll. This is not a guide/curve or automatic edge
alignment. Local/World/View/Edge frames and their visible hints agree; repeat axis
and Esc restore the original working frame. Operation-start numbers, atomic Undo,
MMB behavior, Array/Contour and document/store format remain unchanged.

Manipulator → Size switches fixed screen pixels or model-relative 3D dimensions.
Model sizing uses rotated-local renderer dimensions and actual part scale, not
camera distance/FOV/projection. Reference freezes during previews; independent
size sliders still work. Drawing and projected point picking share the same size.
World/F9 retains its old sizing, colours and occlusion contract.

Release: zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks and HostContract pass; 672 matching localization
keys and 643 static references, embedded resources checked. Format exits 0 with
the existing workspace-loading warning. Unity 6000.0.61f1 passed 81 UI captures at
2026-10-03T13:18:22.5621315Z, actual native-model near/far renders, A/B/axis/reset,
visible frame text, Native-only choices, fixed model metres/FOV/projection,
projected hit areas and appended helper-A provenance. Independent correctness
and Ponytail reviews pass; the final text blocker was corrected and re-tested.

- BuildWorks SHA256: 317B3D15F5B0AFF315D152C03D928BCEEFE9017550093B1EA2609F0FA40EECBC
- Geometry SHA256: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Installed with Valheim closed only in TerrainRamp-1.0-Test through the guarded
script; hashes match. Previous .48 DLL pair: artifacts/checkpoint-0.19.49/preinstall.
No game launch, save change, other-profile mutation or binary release. Workbench
substitutes external host/physical input; actual Valheim readability, costs/refunds,
reload and networking remain owner gates. BW-01 awaits the single numbered
[current series 1–10](specs/roadmap/current-pass.md), starting with readability 1–3.
The .48 list is archived, not a second answer list. Stop at owner feedback.

## Verified candidate 0.19.48

Ostrix approved trying compact nested point signs on 2026-10-03. Six individually
generated transparent PNGs replace .47 artwork: native clasp, corner, midpoint,
centre, pin and active brackets. Native/helper screen radii are 16/9 px; exact
coordinates stay unchanged. A pin is appended state, not a new native socket.
Pin/active overlays preserve original typed points; idle hover names the point.
Scale and Alt-copy priority also gate that caption, with a controller regression.

Snap RGB no longer exceeds white. Embedded icons use mipmaps/trilinear filtering;
Workbench imports keep the same 1254 px dimensions without compression or NPOT
resizing. The blue inside elbow is registered at (0.43,0.42); other pivots (0.5,0.5).
[Assets, exact prompts and concept-to-runtime explanation](docs/artwork/snap-point-assets.md).
These are raster sprites, not vectors. World F9, snap coordinates, input bindings,
costs, Store v9, Array/Contour and the accepted .47 HUD layout are unchanged.

Release: zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks, HostContract and built localization pass
(654 keys, 631 static references). Format exits 0 with the existing workspace
loading warning. Unity 6000.0.61f1 passed 81 UI captures at
2026-10-03T10:35:17.6722987Z, plus actual wood overlap/pin captures.
Native contrast and helper colours remain visible at coincident coordinates;
the pin render has 10 contrast pixels under the neutral-colour palette test.
Independent correctness and Ponytail review pass after the hover-priority fix.

- BuildWorks SHA256: 1C4D56285740E5CDCEA301698B55BD19810A32FF9468C3CEBDB7BE263335B91F
- Geometry SHA256: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Installed with Valheim closed only in TerrainRamp-1.0-Test via the guarded script;
hashes match. Previous .47 DLL pair and artwork are backed up locally at
artifacts/checkpoint-0.19.48/preinstall and artwork-before. No Valheim launch,
save change or binary release. Workbench substitutes the external host/input;
game readability, physical input, refunds, reload and networking remain unproven.
BW-01 awaits the owner: reply only to point 1 of the
[current pass](specs/roadmap/current-pass.md), then resume 2–8 after visual approval.

## Verified candidate 0.19.47

Owner screenshots of .46 reopened two visual defects. Editor snap signs now
have larger screen radii (native 21.25 px, helper 11.25 px at default settings)
and a higher idle opacity floor (0.96/0.88); combined manipulator idle opacity
is 0.64. Artwork, pivots, source coordinates and selected materials are unchanged.
Hover stays stronger; dragging dims unused handles. World F9 is unchanged.

The blueprint HUD measures wrapped title/description height and places native
resource/station cards below that header with a 16 px gap. Native thumbnail size,
labels, costs and availability logic are retained. Parent, sibling, geometry and
text wrapping restore on ordinary selection/teardown. A regression covers the
owner's two-resource/one-station panel and a nested native header.

Release: zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks, HostContract and the built localization catalog
pass (647 keys, 624 static references). Format exits 0 with a workspace-loading
warning. Unity 6000.0.61f1 passed 81 UI captures at
2026-10-02T20:14:19.1510813Z plus focused idle-art/HUD-layout captures.
Independent correctness and Ponytail review found no blockers.
Host/physical input are substituted on the Workbench; actual Valheim readability,
native hierarchy, costs/refunds, reload and networking are not proven by it.

- BuildWorks SHA256: 7D3355E79D700719359FEB85B5E7C4C98BC50179BE0260A372159D43E5807303
- Geometry SHA256: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Installed with the game closed only in TerrainRamp-1.0-Test through the guarded
script; hashes match. Previous .46 is backed up at
artifacts/checkpoint-0.19.47/preinstall. First inspect readability and HUD spacing
in [the same numbered series](specs/roadmap/current-pass.md), before continuing
the full 1–8 run. No Valheim launch or binary release. BW-01 stays awaiting owner;
later stages and curves remain gated.

## Verified candidate 0.19.46

Ostrix approved artwork and controls on 2026-10-02. Six individual transparent
PNGs are implemented: native clasp, corner, midpoint, centre, Shift pin and
active source. Coordinates are unchanged; a coincident helper is picked in the
centre, its native socket on the outer rim. Selected model materials stay untinted.
[Assets and generation prompts](docs/artwork/snap-point-assets.md).

G follows scene surfaces/ground like the placement ghost. Auto is default;
Q/E cycles native-only, with opt-in helpers and explicit furniture fallback.
Attachment previews/chooses real points without confirmation; an explicit
source survives G → R → G. Native pair priority, 2 m previews, 0.55 m capture
and 0.7 m winner retention are implemented. Accepted R/S/axes, Esc and one Undo remain.

Blueprint HUD uses native resource cells and station icons. Resources aggregate;
stations deduplicate; missing availability accounts for paid requirements.
Cards widen/wrap; long lists have screen-bounded scrolling, including an active
Canvas resize. Ordinary selection/errors/teardown restore original cell parents
and geometry. Display-only: marker m_resources stays empty; costs are not changed.

Release has zero warnings/errors. Geometry, Store, Localization, EditorBridge,
WorldLayout, DeploymentChecks and HostContract pass. 647 matching keys and
624 static references; embedded resources verified. Format exits 0 with a
workspace-loading warning. Fresh Unity 6000.0.61f1: 81 captures and actual
Controller/EventSystem, imported game meshes and HUD adapter checks pass at
2026-10-02T19:44:17.4464874Z. Independent correctness/complexity review has no
blockers. External host/physical input is substituted; native HUD hierarchy,
in-game readability, costs/refunds, world reload and networking remain unproven.

Artifacts:

- BuildWorks SHA256: 5EABBC77C5D0ED0D0F7DC9A8F9CA56C5541F3293E723108F72F42368FA8AE9F6
- Geometry SHA256: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Installed only in TerrainRamp-1.0-Test with the game closed, through the
guarded script; hashes match. Previous .45: artifacts/checkpoint-0.19.46/preinstall.
Next: one [owner series 1–8](specs/roadmap/current-pass.md).
Accepted .45 evidence is preserved below and archived. BW-01 awaits game
acceptance; later stages and curves stay gated. No binary release is published.

## Verified candidate 0.19.45 — 2026-10-01

Active gate: BW-01 owner Valheim acceptance in TerrainRamp-1.0-Test.
Owner accepted .44 exact move/scale, MMB click versus drag, tool cleanup and UI
input ownership. This revision addresses the remaining visual, rotation and snap
feedback, not a new tool workflow. Selection keeps original materials and still
shows the combined gizmo immediately.

Plain gold native diamonds, compact blue corner circles and green midpoint crosses
retain their true coordinates. No coincidence suppression; native pin keeps its
gold rim with a purple accent inside. Pink centre, purple pin and white active
helper are explained alongside gold/blue/green in English and Russian.
Arrow/ring/scale artwork stays engraved. World/F9 sizing and Store v9 unchanged.

Choosing an axis resets mouse preview to the immutable action-start pose; typed
values replace mouse deltas. Q/E reevaluates attachment immediately at a stationary
cursor. Game targets native sockets, including ones inside their own mesh; Mesh
also offers generated helpers. Other parts still occlude native sockets.

World blueprint HUD description refreshes with language and shows aggregate
resources plus deduplicated stations. It reuses registry resource aggregation;
marker requirements remain empty, so no second aggregate cost is charged.
This text-only integration has compiled host-contract proof, not game HUD layout
or actual resource-consumption proof.

Release: zero warnings/errors. Geometry, Store, Localization (639 matching keys
and built resources), HostContract, EditorBridge, WorldLayout and profile guards
pass. Format exits 0 with a workspace-loading warning. Fresh Unity 6000.0.61f1:
81 captures and actual Controller/EventSystem checks pass at
2026-10-01T19:42:07.8611315Z, including coincident glyphs, blue/green shapes,
axis reset, same-frame Q/E, native interior socket and Game/Mesh midpoint checks.
Independent correctness and complexity reviews pass. Workbench physical input
is substituted; game acceptance, save/reload and multiplayer are not claimed.

Installed only in TerrainRamp-1.0-Test with Valheim closed through the guarded
script. Hashes match the tested build:
- BuildWorks: F1C0F506D4A2897A6435877E20175870FB6B8489486B11DDA73C54585AC93F3F
- Geometry: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Previous .44 pair: artifacts/checkpoint-0.19.45/preinstall.
Next: only steps 1–12 in [the current owner test series](specs/roadmap/current-pass.md).
Step 12 checks world language/resource display without placing a blueprint.
No game launch or binary release; source-only GitHub synchronization.

## Historical candidate 0.19.44 — 2026-10-01

Active gate: BW-01 owner Valheim acceptance in TerrainRamp-1.0-Test.
Selection now keeps a clear gold tint and immediately shows Combined handles,
without a first-G gate. G starts Surface Move; R/S/XYZ remain exact actions from
the original operation pose. Select keeps MMB click deletion and drag navigation;
additive selection takes priority over handles on an unselected part.

Editor gizmo is 25% larger. Native snap points are larger, brighter closed gold
diamonds with existing engraving; generated bounds/midpoints are smaller open
colored markers. Classification uses captured prefab provenance, not the name
or position of a point. The 128-part budget reserves all 512 native exterior
points before generated helpers. World/F9 sizing and document/store format are
unchanged. The purple pinned point stays visible above its engraving.

Release build: zero warnings/errors. Geometry, Store, EditorBridge, WorldLayout,
Localization (637 keys and built resources), HostContract and forbidden-profile
deployment checks pass. Format verification exits 0 with a workspace-loading
warning. Unity 6000.0.61f1: 81 captures; actual installed asset rendering,
Controller.Update/LateUpdate and EventSystem checks pass, completed
2026-10-01T16:57:24.9419102Z. Independent correctness review PASS; complexity
review found no actionable cuts. Workbench substitutes physical input:
game-host/save/reload/resource/network acceptance is not claimed.

Installed only in TerrainRamp-1.0-Test with Valheim closed through the guarded
script. Installed hashes match the checked build:
- BuildWorks: 9EAC47B84796A25C345FEA3D31737209B0B8E4E4B0B5F9832825D493578729AE
- Geometry: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Previous .43 pair: artifacts/checkpoint-0.19.44/preinstall.
Next: only steps 1–12 in [the current owner test series](specs/roadmap/current-pass.md).
Older test lists are frozen separately in testing-history.md; they are not an
additional answer series. No game launch or automatic binary release.

## Historical candidate 0.19.43 — 2026-10-01

Active gate: BW-01 owner Valheim acceptance. .43 supersedes .42 editor input,
not world/F9 controls. Select hides handles; G opens Combined, repeated G starts
Surface Move. R/S/XYZ edit with numbers relative to the original operation pose.
Ctrl+1–4 selects tools and cancels pending previews. MMB click removes a placed
part in Select/adding; drag, modifiers, other buttons or UI takeover never remove.
The adding ghost remains. Esc rolls back; ready-gizmo Esc hides retaining selection.

Six original editable Nordic SVGs and transparent exported textures add engraving,
metal gradients and rivets. Idle controls dim; screen proximity brightens; winning
hover/active control is emphasized. Filled scale has whole-silhouette picking.
Pinned point retains its purple meaning. Existing icon export/loader is reused.

Release zero warnings/errors; Geometry, Store, EditorBridge, WorldLayout,
Localization (637 matching keys/built resources), HostContract and forbidden-profile
deployment checks pass. Format verification exits 0 with workspace-loading warnings.
Unity 6000.0.61f1: 81 fresh UI captures, real installed asset rendering and actual
Controller.Update/LateUpdate plus EventSystem checks pass, completed
2026-10-01T15:54:50.0544740Z. Independent read-only review and final MMB follow-up
PASS; ponytail review finds no worthwhile new abstraction to remove.
Workbench uses substituted physical input; this is not Valheim OS-key interception,
save/reload, resource accounting or multiplayer proof.

Installed only in TerrainRamp-1.0-Test with Valheim closed through the guarded
deployment script. Installed hashes match the build:
- BuildWorks: C85E2247EA8167BA636A2154387496B4C92AB4B6C2DC028B4E2FA849DA6DE167
- Geometry: 3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16

Previous .42 pair: artifacts/checkpoint-0.19.43/preinstall.
Next: six focused owner steps in specs/roadmap/current-pass.md (Russian duplicate).
Source-only synchronization targets MaikiOS/BuildWorks main; no binary release,
Valheim launch, save/other-profile/other-mod mutation is part of this checkpoint.


## Historical handoff before .43

Updated 2026-10-01. Active work: **BW-01 owner game acceptance**; BW-00 accounting/network
gates remain open. Source candidate 0.19.42, Store v9,
Valheim 1.0.16. Current authorized development/test profile: TerrainRamp-1.0-Test
only. Earlier integration-profile permission is superseded.

The [roadmap](docs/ROADMAP.md) and [owner test series](specs/roadmap/current-pass.md)
are the work order. Automated build/geometry/store/localization/bridge/world-layout/
host/deployment-guard checks pass. Formatting verification exits successfully with
workspace-loading warnings. Unity Workbench passes 81 captures and actual input
regressions for stationary contour guides, cancel/tool switching/deletion, F7/button,
occluder drawing restoration and picking, camera FOV and outliner overflow.
English/Russian catalogs contain 633 matching keys. The focused 0.19.41 game series
is accepted by Ostrix's report on 2026-10-01, not independently observed.
Cost/refund accounting and multiplayer remain untested. No new binary release is published.

0.19.42 is installed in TerrainRamp-1.0-Test through the guarded script with
Valheim closed; its game series is pending. Previous pair: artifacts/checkpoint-0.19.42/preinstall.
Installed SHA-256:
- BuildWorks.dll: `C9DFFEF5A9E989AA00A084906CF6BCCB0617FDA15EB964C4DFC883DB092570D0`
- BuildWorks.Geometry.dll: `3EF58DB30AE547E99293DD2B90E97BF8FEE005534C055D23656714FA09D9EA16`

Independent correctness re-review passed after anchor-tooltip and Russian handoff fixes. Complexity review:
Lean already. Ship. Other profiles/mods/saves were not modified.

Latest owner report: held-RMB menu, furniture Q/E, nearby targets, below-grid
placement, group pivots, Array, two-part save/reopen, world reload and camera pass.
0.19.39 owner feedback accepts native tab language refresh, menu hover and F framing;
selection/box is provisionally accepted; .40 contour cleanup is accepted.
0.19.41 implements the approved editor changes: stationary full-edge contour guides,
fully hidden occluders (off by default; F7/viewport button), six grouped shortcut hints,
camera FOV 10–120 with a game-setting reset (disabled in orthographic view), and
compact 30-unit outliner rows with separate anchor badges, striped rows and a native
scrollbar. Wheel scrolling is 1.5 times faster. See-through uses seven sampled mesh
rays per candidate, restores original drawing flags, permits picking through temporary
occluders, and does not change materials, manual visibility, blueprint data or the world.
Resources/stations in the native panel, category chooser, shortcut rebinding,
double-click framing and external translations await their
UI/format gates. One-piece save remains blocked by the two-part contract.
Esc removes partial placement and restores its ghost; dropped-resource amounts
still need accounting. Multiplayer is deferred; no other profile transfer authorized.
The five focused checks remain a regression reference, not a request to repeat them.

The approved editor design is implemented in .42: explicit Move/Rotate/Points/Scale
families and Nordic forms, temporary G/R/S with exact cancel/one Undo, 1–4 tools,
Space/F3 search, compact dropdowns, collapsing hints with next-action strip, Focus,
resizable tree split, measured tooltips and active hidden-target outline.
MMB is editor camera-only, Delete removes; world/F9 and Store remain unchanged.
Unity: 81 fresh captures, current-frame Enter validation, surface/source/camera
rebasing, return-to-origin, hidden hit-test and EN/RU tooltip scale regressions pass;
completed 2026-10-01T13:48:43.1597938Z. Release zero warnings/errors, final format
exit 0 and two independent read-only reviews PASS. No independently observed
Valheim, networking or full narrow-window tooltip proof. Next: six focused .42
owner checks. Interior snap access remains separate. Discuss guides/curves again
before Line A/B implementation.
After each verified step update EN/RU docs, checkpoint commit and normal GitHub push.
Keep game binaries, saves and personal logs out of the repository.
