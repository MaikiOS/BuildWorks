# BuildWorks current development handoff

**English** | [Русский](HANDOFF_RU.md)

Updated 2026-09-30. Active stage: **BW-00**. Source candidate 0.19.40, Store v9,
Valheim 1.0.16. Current authorized development/test profile: TerrainRamp-1.0-Test
only. Earlier integration-profile permission is superseded.

The [roadmap](docs/ROADMAP.md) and [owner test series](specs/roadmap/current-pass.md)
are the work order. Automated build/geometry/store/localization/bridge/world-layout/
host/deployment-guard checks pass. Formatting verification exits successfully with
workspace-loading warnings. Unity Workbench passes 81 captures and actual input
regressions for contour hover/cancel/tool switching/deletion, F7/button and
occluder material restoration after camera/selection changes. No candidate Valheim or multiplayer
acceptance is claimed. No new binary release is published.

0.19.40 is installed in TerrainRamp-1.0-Test through the guarded script with
Valheim closed; BW-00 awaits the focused owner series. The previous .39 DLL pair
was backed up. Installed SHA-256:
- BuildWorks.dll: `ED3BE3EB60B9A03A986013BCD60460926C0BBC6048EBAF69455AE00429E70858`
- BuildWorks.Geometry.dll: `22906B26CA2C7CD3D5887F819282AF30BC935C00AE10EC34449503CC8FFDBCE8`

Independent correctness review passed without blockers. Complexity review:
Lean already. Ship. Other profiles/mods/saves were not modified.

Latest owner report: held-RMB menu, furniture Q/E, nearby targets, below-grid
placement, group pivots, Array, two-part save/reopen, world reload and camera pass.
0.19.39 owner feedback accepts native tab language refresh, menu hover and F framing;
selection/box is provisionally accepted. 0.19.40 adds hover-only contour guides,
shared guide cleanup, a readable contextual footer and approved editor-only
see-through (off by default; F7/viewport button). It uses sampled mesh rays and
tinted silhouettes, preserves hidden flags/materials/data, and does not alter picking.
Resources/stations in the native panel, category chooser, shortcut rebinding,
double-click framing and external translations await their
UI/format gates. One-piece save remains blocked by the two-part contract.
Esc removes partial placement and restores its ghost; dropped-resource amounts
still need accounting. Multiplayer is deferred; no other profile transfer authorized.
Use the revised four checks, not the entire previously accepted series.

Next: owner game series, then bounded fixes if needed; approve visible UI/icons
before BW-01 redesign. Discuss guides/curves again before Line A/B implementation.
After each verified step update EN/RU docs, checkpoint commit and normal GitHub push.
Keep game binaries, saves and personal logs out of the repository.
