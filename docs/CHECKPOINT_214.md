# .214 — selection rectangle, import focus and v11 state experiment

2026-10-18 · [Русский](CHECKPOINT_214_RU.md) · [Current evidence](CURRENT_STATUS.md)

Installed in TerrainRamp-1.0-Test: **0.19.214.0**, SHA `A57FB813C53C20C2C271765C8AD40A9F7BF3841578BBEFC2D1FEE1233156E2D4`. All143 QA data files are unchanged. Valheim was not launched; .214 has no game or owner acceptance yet. No public binary was released.

## Installed changes

- The screen selection rectangle converts mouse coordinates into its Canvas parent's local coordinates, including camera, scale and rectangle origin. Physical piece selection is unchanged.
- Import navigation in the **editor** selects the actual newly created card button and its page. Identity includes ID and source, so another source with the same ID cannot take focus. Ordinary filtering does not request this one-shot focus change.
- Installed fog behavior is unchanged; Building does not activate the new solver path.

## Verification

Actual HUD/catalog methods passed Unity Play Mode checks:48 rectangle cases across Overlay/Camera, three scales, normal/offset parents and four drag directions;3 catalog cases for duplicate IDs across sources, pagination with80 fillers and absent-source rejection preserving selection. This is Unity with host substitutes, not Valheim or visual acceptance of every window.

Normal/QA builds:0 warnings/errors;131 geometry groups, world selection, host/control/isolation and1104 localization keys passed. Independent correctness and simplicity reviews: PASSED/Ship. `verify` is unavailable; direct review and available checks ran. The host gate initially expected .213 and localization misidentified shader filenames as UI keys; the version expectation and exact filename exceptions were corrected and both checks passed again.

Private receipts, excluded from GitHub: `artifacts/release-214` and `artifacts/fog-idle-velocity`.

## v11 result, separate from deployment

The private experiment saves four RGBAHalf fields, three ledger buffers and five CPU integration values. It checks two wall poses including90° rotation, translation and body movement outside the source, with checkpoints before and during contact. All4 checkpoint restores and both full-replay controls produced **zero field, ledger and mesh/volume image differences**. The sweep contains14 rows;1646 corrupt-report cases and32 metadata checks passed their rejection/coverage gates.

`checkpointFieldLedgerBytes=19668996` counts only one checkpoint's field/ledger payload. It excludes five CPU values, object/container overhead, staging and renderer. Synchronized upload measured16.7–25.1ms per state: wall-clock, not GPU timing or FPS. Twenty-eight such dense payloads alone require about525MiB. Synchronous whole-house transfers are not a production solution.

Eight seconds of replay still fail v11 fidelity: one scenario has local error0.056640625 against a limit below0.03. Limits were not relaxed. Next: bounded state preservation/restoration without losing accepted history, followed by physical domain, spatial ownership and actual house/FPS. `runtimePolicyImplemented=false`, `wholeHouseSupported=false`, `gameHostTested=false`.

## New owner checks, when convenient

Launch Thunderstore → **TerrainRamp-1.0-Test → Modded**, existing QA world/character only. No stand construction or resource gathering is required.

1. With the hammer, open the catalog → «ВЫБРАТЬ В МИРЕ» → switch to «Режим: экранная рамка». Drag in all four diagonal directions. The start stays at the initial click and the opposite corner follows the pointer. Cancel without saving a blueprint. Optionally repeat with another resolution/UI scale. Failure: offset, growing pointer gap or wrong corner. Volume/height editing is not accepted by this check.
2. From the editor blueprint catalog, note the card count. «Импорт файла» → choose `01-floor.blueprint` below → use category `QA214 FOCUS` → «Подтвердить импорт» → «В каталог». Expected: category/page shows the result, **BW183 Floor** is selected with its own preview, exactly one explicit copy was added. Failure: wrong card/source or focus on the first unrelated card. No repeat of accepted house cost/reload checks is required.

Import folder:

```text
C:\Users\nfspr\AppData\Roaming\Thunderstore Mod Manager\DataFolder\Valheim\profiles\Default\BuildWorks\artifacts\Blueprints-for-import\BuildWorks-tests\Batch-183
```

Files: `01-floor.blueprint`, `02-walls.blueprint`, `03-two-documents.zip`, `04-deliberately-invalid.blueprint`. The last is intentionally invalid and must not add a card. Remaining batch checks are in [the .183 instructions](MANUAL_QA_183.md). Fog/house/board are not ready for another owner acceptance run.

## Next work

Run three bounded tracks in parallel: v11 runtime state preservation; editable volume/current membership; specific reproducible import/item defects. A separate authoritative project journal has been scoped for stable projectId/part index, confirmed payment/creation/refunds and crash recovery. This is design only. Storage WAL and renderer are not the construction journal. All14 main goal blocks remain visible in the live checklist.
