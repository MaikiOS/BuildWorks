# BuildWorks current development handoff

**English** | [Русский](HANDOFF_RU.md)

Updated 2026-09-30. Active stage: **BW-00**. Source baseline 0.19.38, Store v9,
Valheim 1.0.16. Current authorized development/test profile: TerrainRamp-1.0-Test
only. Earlier integration-profile permission is superseded.

The [roadmap](docs/ROADMAP.md) and [owner test series](specs/roadmap/current-pass.md)
are the work order. Automated build/geometry/store/localization/bridge/world-layout/
host/deployment-guard checks pass. Formatting verification exits successfully with
workspace-loading warnings. No new Workbench/game/reload/multiplayer acceptance
is claimed by this checkpoint. No new binary release is published.

0.19.38 is installed in the dedicated profile; BW-00 is **awaiting owner**.
The previous pair was backed up. Installed SHA-256:
- BuildWorks.dll: `6FA1CE3C239232B6CCB67756BBDDF8602A5D88AE3D849C107A33134D032D9E06`
- BuildWorks.Geometry.dll: `29746E620709B0964A6345613A8AA75C261B35D9C22887F5F67A0AF0877AAB82`

Independent correctness review passed without blockers. Complexity review:
Lean already. Ship. Other profiles/mods/saves were not modified.

Next: owner game series, then bounded fixes if needed; approve visible UI/icons
before BW-01 redesign. Discuss guides/curves again before Line A/B implementation.
After each verified step update EN/RU docs, checkpoint commit and normal GitHub push.
Keep game binaries, saves and personal logs out of the repository.
