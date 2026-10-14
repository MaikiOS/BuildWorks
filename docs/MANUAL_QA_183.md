# Manual QA .183 — batch import and export

**English** | [Русский](MANUAL_QA_183_RU.md)

Launch through Thunderstore → TerrainRamp-1.0-Test → Modded, using the existing QA world. No materials or construction stand are needed. Previously accepted scale, camera, Save As and snap cases need no repetition.

## Source files

`artifacts/Blueprints-for-import/BuildWorks-tests/Batch-183` is separate from the game catalog:

- `01-floor.blueprint`: BW183 Floor, 1 part, BW183 FLOORS.
- `02-walls.blueprint`: BW183 Walls, 2 parts, BW183 WALLS.
- `03-two-documents.zip`: BW183 ZIP Floor and BW183 ZIP Walls, 1/2 parts.
- `04-deliberately-invalid.blueprint`: intentional invalid input; expect an explicit skip warning.

These are original BuildWorks technical fixtures. Other local collections are under `artifacts/Blueprints-for-import` with provenance/conditions retained; file availability does not prove support for every object.

## 1. Select and cancel

Start with N catalog cards. Hammer → Blueprints → Import file, or the editor blueprint catalog. Change path to Batch-183, Open, Choose group, mark all four files, Import file. Wait for four documents and the invalid-file warning. Documents and categories must show four individual records, including both ZIP documents. Close before Confirm import.

Expected: N cards and unchanged source files. Failure: silent skip, stuck modal or cards before confirmation.

## 2. Categories and import

Repeat the selection. Set common category `BW183 GROUP`. Documents and categories → set BW183 Walls to `BW183 CUSTOM`; leave other fields empty → Apply selection. Confirm import, wait for the receipt, Open catalog.

Expected: N+4 cards; Walls in CUSTOM and the other three in GROUP; first new card selected. Open each in the editor and compare 1/2/1/2 parts. Invalid input has no card. A category is a label, not a storage directory. Scroll the report when needed.

Failure: missing documents, wrong overrides, empty/extra parts or duplicates from merely reopening the catalog.

## 3. Export a selected subset and reimport

Start with the four imported cards. Select one → Export file → Choose group. Mark only these four. Group format is full BuildWorks ZIP. Choose directory/name `QA183-roundtrip` (use a fresh name on repeats; no overwrite).

Export file → Documents and categories → uncheck BW183 ZIP Walls → Apply selection → Confirm export. Copy the full result path. Export creates `QA183-roundtrip.zip` without new catalog cards. Close, import that exact ZIP, setting common category `BW183 ROUNDTRIP`.

Expected: three new cards Floor/Walls/ZIP Floor with 1/2/1 parts and matching editor appearance. Original cards and files remain. Excluded document is absent. Failure: export creates cards, mutates originals, includes excluded data or produces unreadable/lossy output. Compatibility with foreign mods is not claimed.

## 4. Draft and restart

Open an owned saved blueprint, move a part without saving, open batch import from its catalog and close at review. Return: the draft and Undo must remain. Finish/cancel that draft normally, exit cleanly and reload the same QA world through Modded. Imported cards, categories and part counts must remain; merely opening the catalog adds no duplicates.

## Storage and limits

Profile paths are under `TerrainRamp-1.0-Test/BepInEx/config/BuildWorks`: imports holds one committed group archive, exports is the default export directory, share holds automatic full owned files. External cards reach share when explicitly saved/shared. Source collections are separate.

Automated checks cover cancellation during writing, cleanup, filename conflict, source change, shared limits and independent storage/mount choices. Do not corrupt the library manually. World construction, items, heavy buildings and multiplayer remain separate open acceptance work.
