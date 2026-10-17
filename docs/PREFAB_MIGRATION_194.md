<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .194 — roof migration and native result

2026-10-16. QA 0.19.194.0 installed only in TerrainRamp-1.0-Test; full goal ACTIVE and incomplete.

Real .193 rejected the old/current roof pair after GetSnapPoints added two owned MidSnap nodes to the current cached prefab (10 vs12 children). .194 omits only owned direct empty Transform leaves. Unowned names, children/components and real geometry/socket/collider differences still reject migration.

Red reproduced the defect; green15 exercises actual Append and matcher. Clean native assets, normal/QA builds0/0, Geometry/Store, Host25909357, localization1103, control/isolation/snap95 passed. Independent correctness PASSED and separate Ponytail Ship. No verify helper; direct checks used. Full Workbench624 was not repeated because visual code did not change. Guarded deployment retained hash/mtime of123 QA data files. Installed DLL SHA256 `27E50A87D03728A5EC3D305973DDC82E59BCC568C1C27C957A832C00C4340550`.

Native Thunderstore Modded, exact isolated BW_QA_43d3719a: review showed4 wood_wall_roof → wood_wall_roof_a replacements. Editor/Save As produced QA194 Forest Hut, category QA194, ID645bf7b76d9d419b8c21c5dad4fd31ab; all28 parts and4 original roof IDs retained. Source untouched.

One real click completed28 successful TryPlacePiece/PlacePiece/ConsumeResources calls without exceptions. Separate QA grant Wood52→58, actual payment58→0, NoCostCheat=false, Hammer32.6→5.4. Granted materials carry native cheat markers, so some PlacePiece calls have cheated=true from ItemCheated; this did not disable resource payment. Do not claim all28 calls used cheated=false.

All28 prefabs/TRS matched document and recorded ghost within0.000214 m position,2.93e-8 quaternion,4.8e-7 scale. Placement pivot (2,-.2,2) comes from actual placement log and registry source; pivot log is rounded.

**Full reload failed:** all28 objects, positions/scales and full inventory retained, but10 rotations shifted about0.5 degrees. Current native ZDO.Save calls ZPackage.WriteSmallRotation, which multiplies Euler by2 and truncates to integer; readback multiplies by.5. Exact rotation persistence is the next required blocker. Network IDs were not compared across load.

Separate native .193 sign case passed editor/ordinary ghost, two paid placements Wood56→52/Coal1→0, and real reload retaining Cyrillic text, both TRS and inventory. Batch513/UGC denial/network remain open.

Local receipts: artifacts/prefab-migration-194 and artifacts/checkpoint-0.19.193/native-main-path. No native assets/DLLs/saves/raw receipts are published. Protections restored, normal logout/Exit completed. Remote networking, full .194 Workbench and all format cases remain unverified.
