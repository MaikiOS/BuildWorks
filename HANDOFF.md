# BuildWorks current development handoff

**English** | [Русский](HANDOFF_RU.md)

Updated 2026-10-01. Active work: **BW-01 design approval**; BW-00 accounting/network
gates remain open. Source candidate 0.19.41, Store v9,
Valheim 1.0.16. Current authorized development/test profile: TerrainRamp-1.0-Test
only. Earlier integration-profile permission is superseded.

The [roadmap](docs/ROADMAP.md) and [owner test series](specs/roadmap/current-pass.md)
are the work order. Automated build/geometry/store/localization/bridge/world-layout/
host/deployment-guard checks pass. Formatting verification exits successfully with
workspace-loading warnings. Unity Workbench passes 81 captures and actual input
regressions for stationary contour guides, cancel/tool switching/deletion, F7/button,
occluder drawing restoration and picking, camera FOV and outliner overflow.
English/Russian catalogs contain 615 matching keys. The focused 0.19.41 game series
is accepted by Ostrix's report on 2026-10-01, not independently observed.
Cost/refund accounting and multiplayer remain untested. No new binary release is published.

0.19.41 is installed in TerrainRamp-1.0-Test through the guarded script with
Valheim closed; its focused owner series is accepted. The previous .40 DLL pair
was backed up. Installed SHA-256:
- BuildWorks.dll: `A1D1CAF28246E6E4D2A8E9908B06BDD56AE8C6492FBD1CB2EBD7D7DE5DE0F10C`
- BuildWorks.Geometry.dll: `B449F58CE647F6FB92127015710B9EC9657F5E7F816DFCEA1ACE04C26151FCED`

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

Next: approve the [editor visibility proposal](specs/roadmap/editor-visibility-proposal.md).
Three alternatives were assessed; explicit Move/Rotate/Points control families
are recommended with collapsible hints and compact viewer dropdowns. Target-only
outlines are proposed for F7 attachment context; interior snap access is a separate
decision. No proposal is implemented or installed. Discuss guides/curves again
before Line A/B implementation.
After each verified step update EN/RU docs, checkpoint commit and normal GitHub push.
Keep game binaries, saves and personal logs out of the repository.
