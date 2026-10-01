# BuildWorks current development handoff

**English** | [Русский](HANDOFF_RU.md)

## Verified candidate 0.19.44 — 2026-10-01

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
