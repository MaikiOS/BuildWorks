# Ordinary world snapping (.193)

## Native Codex run — 2026-10-15

Thunderstore Modded, TerrainRamp-1.0-Test; isolation and BW_QA_43d3719a character/world verified. Three wood_beam targets were free QA setup, separate from paid proof. Real vanilla woodwall, F9 closed, NoCostCheat=false. Identity/Y180/X180 targets × headings−5/0 (tilt−27): all6 native/chosen instance pairs equal, live gap0, player yaw45 retained. Camera can still select a neighboring native point.

Two real mouse clicks each produced one successful TryPlacePiece, PlacePiece(cheated=false), and ConsumeResources without exceptions; Wood60→58→56. First cursor positioning moved the camera, so its earlier ghost snapshot is not matching-pose proof. Second stationary-cursor click matched recorded ghost position/quaternion. Real logout/enter retained both TRS and all16 inventory records, slots, attributes and durability; Wood56. Quaternion q/−q treated equivalently. Native ZDO.Load assigns SetID(++m_loadID); SetID uses UnknownFormerUserKey, so cross-load ZDOID equality is invalid.

Preserved failed diagnostics: teleport cooldown, absent ZDO.GetUID, overstrict cross-load ID assertion. Correct semantic assertions are separate. Evidence under artifacts/checkpoint-0.19.193: native-snap-matrix-v2[-assertions], native-paid[-assertions], native-paid2, native-exact-before/after-reload, native-reload-semantic-assertions, native-inventory-reload and current native-zdo/zdoid audit. Three setup targets and two paid walls remain at x2566…2574,z138; two additional beams discovered before paid testing were left untouched. No grants/repairs. God/ghost restored false, normal logout/quit, no Valheim process, own temporary localhost GABS stopped. Catalog not populated; DLL unchanged.

This covers6 auto cases and2 ordinary paid placements/reload, not Q/E, explicit F9 XYZ, every prefab/camera, remote networking or owner acceptance. Next: native signs and automatically migrated Forest Hut. Full goal ACTIVE.

2026-10-15. Following the [.192 audit](WORLD_SNAP_AUDIT_192.md), Ostrix delegated the stable behaviour decision.

Ordinary Hammer pieces now retain Valheim's chosen pair and player rotation. They do not re-rank only against the aimed owner or inherit its hidden root rotation. Native translation/validation continues unchanged. Intentional XYZ alignment remains in the existing explicit F9 precision tool; five compiled precision methods match .192. Composite blueprint aimed-point selection, editor, midpoints, manual/alternative placement, costs and persistence are preserved. Native equal-distance ties remain enumeration-based; this does not promise a unique answer in ambiguous geometry.

Verification: actual-method red→green95 CPU assertions; extracted-method Unity6000.0.61f1 real TRS244 assertions with inert host wrappers; normal/QA0 warnings/errors; Geometry/Store exe; Host25909357 and compiled no-rotation-write guard; QA control/isolation; localization1103; final Workbench624; independent correctness PASSED and Ponytail Ship. First Workbench failed licensing in sandbox (198); approved repeat passed. Initial Unity fixture incorrectly placed a test component under Editor; corrected fixture passed. Logs preserved, temporary test code removed. No native gameplay proof is inferred.

Guarded deployment installed0.19.193.0 only in TerrainRamp-1.0-Test, matching both source hashes and retaining SHA/mtime of104 protected existing files. Catalog not populated. QA .192 backup retained. BuildWorks SHA256 `39A9AE03C233EF5DBE1E0CDC9427FA37457852F97BD04C60FD8FC8A5692ADEE5`; Geometry `74E2F1CBBC3D85AE0B33C9D12F1A6C6B102889CA9AE928278308AF59E54F90FB`.

Evidence: local `artifacts/checkpoint-0.19.193`. Native .193 acceptance remains open at installation. Ostrix subsequently authorized launch/testing while away; use Thunderstore Modded and verify the isolated QA identity before any world operation. Full goal ACTIVE/incomplete. [Russian native scenario](MANUAL_QA_193_RU.md).
