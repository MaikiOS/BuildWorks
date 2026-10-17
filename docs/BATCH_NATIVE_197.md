<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Limited native batch exchange check, QA .197

2026-10-16. Actual Valheim, Thunderstore TerrainRamp-1.0-Test → Modded, existing BW_QA_43d3719a character/world. Installed QA0.19.197.0 remains unchanged; SHA32780B01…6882F3. No gameplay source or DLL changes.

Batch-183 review produced four documents and an explicit Russian warning for the invalid file. Cancellation kept6 catalog entries. One confirmed import produced10:1/2/1/2 parts, a global category and a separate Walls category. The catalog selected the first imported entry. Export with ZIP Walls deliberately deselected produced exactly three documents and kept10 cards. Reimport produced13 and selected BW197 ROUNDTRIP. Normal logout/world reentry retained13:3 personal and10 external; no catalog error. Actual editor displayed Floor and both Walls; Walls had2 document parts and2 visual nodes after reload.

File comparisons prove all fields of Floor/Walls/ZIP Floor preserved except fresh unique document/part IDs and the requested category. Original source files, prior import archives and the retained storage WAL have unchanged SHA. The initial strict equality failure is retained; all differences are enumerated and individually checked. These fixtures contain no groups/supports/contents, so their behavior is not proven here.

After the13-entry reload check, an explicit external Walls placement selection created one personal copy cc552d6270924447b3ebf51e66e09a5b, bringing the final catalog to14. It is retained, with its normal share auto-file and external placementImportId. No construction was confirmed, items were not granted/spent. The visible14-item inventory/Wood1 remained equal; full binary inventory equality was not tested.

Most actions used existing GABS runtime handlers, some selection collections directly; this is not complete pointer/keyboard coverage. Real sky clicks deselected ZIP Walls and applied the picker; screenshots were inspected by the main agent in the conversation, not independently reviewed in the file-only evidence pass. The reviewer independently confirmed file comparisons including missing-key versus null distinctions. Receipts/code/data: artifacts/native-batch-197. [Detailed Russian evidence and limitations](BATCH_NATIVE_197_RU.md).

No complete process restart, unsaved draft/Undo, all original editor cards, other resolutions, heavy workload, network or storage acceptance. TMP font creation warnings remain tracked despite readable observed text. Normal exit, protections restored, own helper stopped, endpoint absent. Full goal ACTIVE/incomplete. Next development remains durable paired-transfer completion and crash/replay/return checks before production filled placement.
