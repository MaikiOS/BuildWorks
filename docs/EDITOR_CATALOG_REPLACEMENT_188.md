<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .188 — explicit catalogue replacement of loaded parts

Native .187 QA passed 96 Forest Hut import/edit/Undo/Redo/Save As/reopen steps. Placement stopped before its ghost: four `wood_wall_roof` records resolve visually but are absent from all 609 current Hammer entries. `wood_wall_roof_a` exists separately; no verified automatic migration was found. No construction or debit occurred; Wood64 remained.

One selected, effectively visible, unlocked part can now use catalogue Replace or Align. Replace removes only that target; Align retains it. Existing multi-selection missing-target copy behavior is unchanged. Preview, cancellation, stale-selection checks, one Undo/Redo and Save remain on the existing document path. Known loaded bounds use actual visible geometry. Invalid selection retains selection-help instructions without opening replacement.

There is no automatic prefab substitution and no change to native eligibility, costs, stations or permissions. Manual replacement is an explicit blueprint edit, not proof of old-format compatibility.

Normal/QA builds, Geometry, Localization/Store tests, EditorBridge/WorldLayout, localization1100 and HostContract188 passed. Final Unity Workbench passed 612 screenshots and loaded replacement/alignment/invalid-selection checks. Independent reviews: PASSED and Ship. Formatter exit2:135 existing whitespace diagnostics, none on added/changed lines against .187 baseline. Guarded .188 deployment verified both DLL hashes and 107 unchanged existing QA config files after closed-process/WhatIf checks. .188 native construction/reload remain unverified.

Continue with the existing own `QA187 Forest Hut` card, category `QA187`, in the isolated QA world through Thunderstore Modded. Explicitly select each legacy wall, Replace → Open catalogue → choose `wood_wall_roof_a` for testing → inspect preview → cancel once, then confirm/Undo/Redo. Save/reopen all 28 parts and the 15-degree door before trying the world ghost. Stop on any unresolved ID or wrong geometry before payment. Full paid construction and actual reload are separate outstanding checks.

[Detailed Russian evidence and steps](EDITOR_CATALOG_REPLACEMENT_188_RU.md).
