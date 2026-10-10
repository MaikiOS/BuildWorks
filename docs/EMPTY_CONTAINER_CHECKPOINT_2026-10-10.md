# QA0.19.142 — ordinary empty-container construction

Fixed a reproduced whole-blueprint rejection: an empty or explicitly cleared `valheim-container-v1` archive incorrectly entered mounted-item validation. Validation, supply selection and native-placement completion now share the same supply requirement.

Only an exact native root Container, fully validated archive and an explicitly empty default-loot table pass. Nonempty uncleared archives, derived/nested components and default-loot prefabs remain refused. Native Awake adds default loot during instantiation; supporting those prefabs needs a scoped creation policy, not clearing a live inventory afterward.

Source archives remain unchanged. This fix performs no inventory load, clear or save. All-storage contents restoration and placement choices I03/I05 remain incomplete.

The regression failed before the fix. Normal/QA builds: zero warnings/errors. HostContract Steam25185596, localization961, isolation, compiled QA control, protocol28, EditorBridge and Unity Workbench492 passed. Independent reviews: PASSED and Ship. Source checkpoint: `5fa37eef8f91bd90315aa6d1cb843161f6c2f1db`.

Guarded deployment to TerrainRamp-1.0-Test passed; both DLL hashes match.137 existing config/import/save files retain hashes, sizes and exact UTC timestamps. Receipts: artifacts/checkpoint-0.19.142/{verification,installation}.json.

Valheim was not launched. Native placement, costs and reload remain unverified. The .141 QA channel remains included; its first host smoke is documented in [QA_CONTROL.md](QA_CONTROL.md).
