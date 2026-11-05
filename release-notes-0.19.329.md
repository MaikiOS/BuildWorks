# BuildWorks 0.19.329 alpha

Manual-install **alpha prerelease**, not beta or stable. This is the unchanged 2 November manual-test archive; its internal “private test candidate” labels describe its origin. No Thunderstore upload.

## Package and source

- `BuildWorks-0.19.329-alpha-test.zip` — SHA256 `3C91113CB4D601A843317DA251BB60BF191107EC5D7E15D74983279373934E41`.
- Original source checkpoint `5a095eb81967e074c29c8588161e0495325c4b6d`; this release tag contains the matching source snapshot and package-specific guides.
- Later accepted development changes, including the new mounted-item return/recovery path, are **not in this ZIP**.

## Install, update or remove

Use an existing Valheim profile with BepInEx 5. Verified host: Steam build26249040, QA BepInEx5.4.23.6 / Unity6000.0.75f1. Other host versions/mod stacks need separate testing. Valheim/BepInEx are not bundled; Jotunn/EarthWorks/TerrainRamp are not runtime dependencies.

Close Valheim. Back up plugin/configuration/library/world/character data outside plugins. Verify the ZIP checksum. Merge its `BepInEx` folder into the selected profile and replace all four supplied files together under `BepInEx/plugins/Ostrix-BuildWorks`: `BuildWorks.dll`, `BuildWorks.Geometry.dll`, `YamlDotNet.dll`, `THIRD_PARTY_NOTICES.md`. Keep one active copy. Preserve existing data; do not copy QA profiles/bridge/tokens. Start the profile and check that BepInEx loads BuildWorks0.19.329 once. Updating uses the same process. To remove, close the game and remove only that plugin directory; world pieces/data are not automatically rolled back. Downgrading may require a matching pre-update library backup.

[Complete tagged instructions](https://github.com/MaikiOS/BuildWorks/blob/v0.19.329-alpha/docs/INSTALL.md).

## What was tested and what remains

F9/blueprint placement allows eligible furniture/storage/crafting rotation and overlap with real support, with native payment/access/operating requirements retained. A typed Container is never an attachment donor. Bounded native QA confirmed one wall-overlap chest for10Wood and one rotated90-degree wall cooking station for2Wood, then new-process reload; both retained pose/scale/health and the chest opened. Transform fields were set virtually; this does not re-prove physical mouse/gizmo input. The container-only NoContact negative used2Wood, below chest cost; a funded negative, cooking/fire/food/fuel, full mod stacks and networking remain untested. No new gameplay screenshots/GIFs.

The full project remains alpha:137IDs/35scoped accepted/102open. 2.16 HOLD, 2.19 and the remaining requirements remain required. Old329h preservation UNKNOWN/HOLD and planner strict-player-state gaps remain. Test on backed-up data. Beta only follows completion of the full agreed goal.
