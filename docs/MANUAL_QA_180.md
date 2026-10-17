<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Manual .180 queue — chosen imports into a clean catalog

When convenient, launch through Thunderstore → TerrainRamp-1.0-Test → Modded.
Use .180 after its installation is confirmed in the live checklist. Do not use
the main world or saves.

All prepared sources are in artifacts/Blueprints-for-import under the owner's
BuildWorks source tree. The game does not scan this folder. It contains576
blueprint files and28 previous ZIP imports, including repeated models. Attribution
and provenance remain beside them. A file is not proof of format/mod support.

Ostrix chooses files and may add other libraries. Do not import the entire old
collection again. Optional known small example:
`BiomeBlueprints-1.0.3/blueprints/hs_ashlands_madzobuild_coffeetable.blueprint`.
The previous house refusal uses
`BiomeBlueprints-1.0.3/blueprints/hs_blackforest_hut.blueprint`.

## 1. Empty catalog and one chosen import

Initial state: Blueprint catalog has no cards after the reset. Select Import file,
Edit path, the chosen source directory, Open, the file, then Import file. Read
warnings and independent contents settings, then Confirm import.

Expected: phase name, phase bar and elapsed seconds. The bar is not a time estimate.
Success closes the exchange and selects the new card/page. A multi-document ZIP
states which first document will be selected; all documents remain in the catalog.
Failure signs: unexplained hang, wrong card/page, hidden result, raw translation
key or success reported after rejection.

## 2. Import from the editor catalog

Initial state: a dirty draft with an edited part. Open its catalog, set a search
that would exclude the incoming blueprint, and import a chosen file.
Expected: search clears and the new card/page is selected. The dirty draft,
selection, transforms and Undo history remain. Failure: a lost change/draft,
replacement with a different document, or duplicates from reopening the catalog.

## 3. Close and failure

Before confirmation, Back to file settings or Close creates no record. After
confirmation, Background closes the window while the started write continues.
The completed result can be found in the catalog. A pre-commit failure reports
rejection. A post-commit catalogue failure offers Refresh catalogue, which only
reads and never makes another copy. Do not damage files or lock directories to
force this test; automated tests already cover that failure.

## Not yet available

Pre-import category choice, automatic Share file, multi-file selection, batch
export and player-to-player transfer are not implemented. Several documents inside
one supported ZIP do not mean the interface supports selecting several files.
Accepted scale/Save As/camera tests need no repeat. If freshly imported Forest Hut
is unavailable, Check blueprint and the filename are sufficient; do not farm
resources for diagnosis.
