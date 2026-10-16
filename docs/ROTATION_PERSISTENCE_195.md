# Precise rotation for new blueprint pieces — .195

Current Valheim stores Euler angles in ZDO and truncates disk rotations to half-degree bins. The paid .194 Forest Hut lost 0.5 degrees on 10/28 pieces after reload; positions, scale and inventory remained equal. Full reload therefore failed.

New successful blueprint placements save exact rotation plus a witness produced by the actual host disk codec as two typed Quaternion ZDO extras. Reads require finite unit quaternions and a matching native exact/witness value. A genuine SetRotation invalidates both extras. Old objects are not rewritten; WorldPrecision is unchanged.

Independent review caught a resource-loss path in the first implementation: native ConsumeResources precedes PlacePiece. The writer now runs after object/payment enrollment in successful TryPlacePiece completion, before series completion. Fresh creation is checked before native debit. A write failure uses existing cancellation: finalized debit snapshot, confirmed native/network removal, then one-time refund of the actual supplied resources. No new compensation system was added.

A foreign hook that substitutes source/ghost network identity blocks automatic removal/refund and logs an error. Removing that borrowed identity or proving compensation is unsafe; this exceptional debit remains unresolved.

Checks: Unity118 with actual writer/reader/Harmony patches and inert storage; normal/QA builds 0/0; current Build25909357 HostContract; Geometry including delayed payment finalization; Store; localization1104; QA control/isolation. Workbench624 preceded the final nonvisual writer relocation; affected build/HostContract/Geometry checks were rerun. Correctness PASSED and Ponytail Ship; actual .195 result follows below.

Installed: .195, SHA E19249E42CE92BE9D2B2B5403D88117948C7C4A731C8015B3975B71ECF8875EA; Geometry74E2F1CB…54F90FB. Guarded deployment retained127 protected files. Correctness PASSED and Ponytail Ship after the literal source/ghost alias fix; actual-source identity regression8 is part of HostContract. Local receipts: artifacts/rotation-persistence-195.

## Actual Valheim — 2026-10-16

Thunderstore Modded; fresh .195 loader/isolation, exact BW_QA_43d3719a identity. Existing own QA194 Forest Hut reused, no new catalog cards. Diagnostic yaw225 prepared via native m_placeRotation=10; mouse wheel not tested. One explicit QA Wood58 grant and one real LMB. NoCostCheat=false, m_noPlacementCost=false. 28 TryPlacePiece /28 PlacePiece /28 ConsumeResources, zero exceptions; Wood58→0, Hammer46→18.9. Granted resources carry native cheat metadata; this does not establish cheated=false for every object.

All28 types/poses/scales match the saved blueprint and recorded ghost: max position0.000274m (rounded logged pivot), quaternion component3.47e-8, scale4.8e-7. Normal logout/enter retained28 unique objects; exact position/quaternion/scale differences all0. Entire inventory identical, Wood0, no repeated grant. native-reload-assertions.json fullReloadPassed=true.

One post-enter preparation teleport was refused by the native teleport cooldown. Entry/inventory succeeded; the original failed receipt remains. No mutating script repeated; subsequent lookup used read-only calls because the player was already at the test location.

God/ghost false, normal game exit, own temporary GABS46880 stopped. Old .194 house untouched. This migrated imported-house case now passes paid construction and exact reload. Other formats, WorldPrecision, actual native write-failure compensation, and remote clients remain open. Next main step: populated storage/mounts, starting with the prepared .187 scenario.
