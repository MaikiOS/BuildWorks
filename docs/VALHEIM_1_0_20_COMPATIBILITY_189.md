# .189 — Valheim 1.0.20 compatibility

Steam changed the host from1.0.17/build25730771 to1.0.20/build25909357 after .188 verification/deployment. The old QA fingerprint refused activation before root changes or Harmony patches; BuildWorks disabled itself, exposing the ordinary save lists. No saves were restored and no automated game input was sent. The owner entered TerrainRamp_Lab and then exited.

Current native IL was independently reviewed for storage initialization, root overrides, cloud suppression and local world/profile/backup paths before updating QA fingerprints. Readers retain old formats and add Inventory110, ItemStand110, profile47, Player34 and world42. Exact bytes/source item versions remain intact; future111/48/35/43 are refused. The native ItemData.Save and ZDO.Save layouts are unchanged; profile SavePlayerToDisk differs only by its version literal. Player readback checks the reviewed prefix and exact fresh blob, with an opaque tail.

DB2/completion/metadata/mapping must agree on supported41/42. Immutable referenced chunks independently support41/42, matching native incremental reuse and chunk-local load version. This is bounded structural evidence, not complete DB2/load or settlement authority. Native109 ignores the cheated flag while110 applies it; normal BuildWorks placement uses the player's provided item and does not restore archived item properties.

Normal/QA builds0/0; Geometry, Localization/1100, Store, EditorBridge, WorldLayout, Host1.0.20, full QA-isolation/18 receipts and ContentReservation suites passed. Positive current formats, truncation/future versions, byte-preserving110 conversion, mismatched generation headers and world42 with retained chunk41 are covered. Unity Workbench612 and independent PASSED/Ship passed. Formatter135 findings are confined to unchanged baseline lines.

Guarded QA.189 installation verified both DLL hashes and107 unchanged existing QA data files after CIM/WhatIf. Catalogues, identity tokens and receipts were retained. Live .189 isolation, Inventory110 transfer, mixed world save/load and the full paid house/reload remain unverified.

Next: Thunderstore → TerrainRamp-1.0-Test → Modded, stop at the main menu. Verify fresh .189/isolation-active logs before selecting the existing BW_QA world/player. Continue the existing QA187 Forest Hut copy through explicit legacy-part replacement, ghost, normal payment and actual reload. Do not select ordinary saves on a refusal. [Russian evidence and exact steps](VALHEIM_1_0_20_COMPATIBILITY_189_RU.md).
