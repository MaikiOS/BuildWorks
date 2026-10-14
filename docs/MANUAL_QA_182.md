# Manual QA .182 — portable file and Share

**English** | [Русский](MANUAL_QA_182_RU.md)

QA .182 is installed in TerrainRamp-1.0-Test. Use the authorized QA world through Thunderstore → Modded. The catalog is intentionally empty; only import files you choose. Previously accepted scale, Save As, camera and snapping checks need no repeat for this file-sharing stage.

## 1. Automatic file for an owned blueprint

Start with a small blueprint you created or imported and saved in the editor. Optional fixture: `16-supported-snap.blueprint` under `artifacts/Blueprints-for-import/BuildWorks-tests`; all source libraries remain outside the game catalog.

1. Edit one part and select Save (Russian: «Сохранить»), confirming the name/category.
2. Open the editor blueprint catalog, hover the saved card, and select Share («Поделиться») at the bottom.
3. Select Open folder («Открыть папку»). The dialog gives the exact `<ID>.buildworks.json` filename; record it for the remaining checks. Copy path («Скопировать путь») provides the complete file path.
4. Edit/save again and open Share again.

Expected: `TerrainRamp-1.0-Test/BepInEx/config/BuildWorks/share/<ID>.buildworks.json` updates under the same name. The directory appears on first save/share. Sharing does not add a card; Save As creates a separate owned blueprint/file. Deleting an owned blueprint removes only its managed share file, preserving source files and user exports.

Failure: a new card per Share click, stale data after a successful write, no file without an error, or changed external source bytes.

## 2. Saved version with an open draft

Start with the exact file/name from check 1 and the saved blueprint open in the editor.

1. Move one part without saving.
2. Open the catalog, hover the saved card and select Share.
3. Close the dialog/catalog; check the unsaved movement and Undo.

Expected: the dialog explicitly says it shares the saved version. The file excludes the unsaved movement; the draft, dirty state and history remain intact. No extra card appears.

Failure: replaced/cleaned draft, lost Undo, or unsaved changes silently included in the file.

## 3. Hammer and importing the shared file

Start with the file from check 1. No resources or construction are required.

1. Open Hammer → Blueprints, select the same saved card, then Share.
2. Compare the full path with check 1 and close the dialog.
3. Use Import file («Импорт файла») to select the exact `<ID>.buildworks.json` recorded earlier; explicitly confirm policy/category.
4. Open the imported blueprint in the editor and compare parts, scale, poses, text/supports if present in the chosen source.

Expected: Hammer gives the same file. Only an explicit import adds a card; supported construction data and provenance survive. Another player can import a copy of the file. In-game player transfers and the shared server library are not implemented yet.

Failure: sharing places/publishes objects, implicitly creates copies, produces a file our importer rejects, or loses supported data.

Write conflicts and file-only retry after committed Save As are automated checks. Do not corrupt your library or block folders for manual QA. In-game acceptance and network transfer remain separate evidence.
