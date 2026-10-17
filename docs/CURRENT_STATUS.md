# Current BuildWorks status

**2026-10-17 · [Русский](CURRENT_STATUS_RU.md) · [Roadmap](ROADMAP.md) · [Documentation](README.md)**

Source and installed isolated QA build: **0.19.206**. Tested host: Valheim 1.0.20, Steam build25909357. Latest published binary is the historical [0.19.37 alpha prerelease](https://github.com/MaikiOS/BuildWorks/releases/tag/v0.19.37-alpha); its ZIP does not contain .206. There is no stable release. Source synchronization is not binary publication.

| Area | Implemented / automated evidence | Valheim or user evidence | Still open |
| --- | --- | --- | --- |
| Precision placement and editor | XYZ, paired axes, 1–1000% component scale, groups, anchors, Undo/Redo, Save As, filters and interior camera | Owner accepted specific scale/save/camera/catalog cases; ordinary snap checked in .193 | Broader rotated/modded contacts, pointer wrapping edge cases and full visual acceptance |
| Imported construction | Explicit prefab migration, supported text/material paths, real native pieces | Forest Hut (28) paid pieces and exact transform/inventory reload in [.195](ROTATION_PERSISTENCE_195.md) | All formats/prefabs, constructive state, large buildings and networking |
| Formats and missing parts | Native store24/portable13; supported .blueprint/.vbuild import, reviewed export, missing records/bounds/copy alignment | Limited import/editor cases accepted | Unknown models/behavior are not restored by archiving; wider native corpus acceptance |
| Catalog and file exchange | Categories, Save As, delete/hide, automatic portable file, Share, batch import/export | Limited batch .197 import/export/reentry proved | Full batch UI/dirty draft/restart; some owner checks remain |
| Server library and sharing | Administrator publication; players/admin visibility, verified personal copies, provenance; in-process checks | Host+remote not accepted | Live access/reconnect/restart; direct player-to-player transfer |
| Items and storage | Separate storage/mount inclusion; supplied-item preservation and journal foundations | Limited chest/inventory comparison only | Durable settlement, cancellation/refund/crash/replay, all holders/armor/admin restore and network authority |
| v11 fog | [.206](FOG_ATLAS_206.md): shared rendering of3 independent sources, GPU packing/order/depth/translation/recovery regressions | Last native visual [.203](FOG_VOLUME_203.md):2 walls | Whole house, large-part/region seams, total memory/FPS, multiple players and owner acceptance |
| Unfinished construction | Existing transient pause/refund foundations | No persistent project acceptance | Durable project + stake/board + material continuation + confirmed cancel-refund + reload/server |
| World selection and heavy chain | Capture/volume foundations and targeted checks | Basic capture accepted; later coordinate/height UX issues reported | Editable volume/gizmos, boundary rules, full loaded-area and performance acceptance |
| World objects and EarthWorks | No terrain integration dependency | None claimed | Admin-priced nonrecipe objects; EarthWorks is a later separate integration |

Evidence levels are distinct: source → automated checks → deployment → native Valheim → owner acceptance → host+remote. A later build number does not upgrade older evidence. [.193](WORLD_SNAP_193_RU.md), [.195](ROTATION_PERSISTENCE_195.md), [.197](BATCH_NATIVE_197.md) and [.206](FOG_ATLAS_206.md) document their exact scope.

QA.206 installed DLL SHA `B1691BB1DAE7668AEBC924A185329F4AE552492B4A57457D6A798A0DF68042FC`;143 protected files retained their hashes/timestamps. Normal/QA builds: 0warnings/0errors, host/control/isolation/localization 1104 and independent reviews passed. .206 has not run in Valheim. Local receipts are under `artifacts/fog-atlas-206` and are not shipped in GitHub.

Next: approved v11 on existing Forest Hut, then persistent unfinished project and board. [Full scope](../PROJECT_CONTRACT.md), [ordered milestones](ROADMAP.md), [owner live list](LIVE_CHECKLIST_RU.md). Accepted cases need no repeat without a regression reason.
