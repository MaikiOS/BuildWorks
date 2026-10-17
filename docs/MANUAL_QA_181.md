<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .181 — category before import

When convenient, test after .181 installation through TerrainRamp-1.0-Test →
Modded. The catalog is not automatically populated. Choose your own source files
from artifacts/Blueprints-for-import. Optional small two-document example:
`BuildWorks-tests/34-two-categories.zip` in that folder.

1. Initial state: file selected in Import file; first Import file action opened
   review. An empty Category field means preserve source categories. Confirming
   example34 produces QA181 Category A/B in QA181 A/B. Failure: forced OTHER
   category or selection of an unrelated result.
2. Before confirming another chosen import, type a category such as My tests.
   The review states its normalized name and document count; a ZIP applies it to
   all documents. Confirmed cards appear in MY TESTS; the first result is selected
   on its actual page. Source ZIP stays unchanged. Failure: mixed categories,
   extra card or category lost after game restart.
3. In another review, Choose opens existing categories with search and paging.
   Select a row. Cancel returns without changing the choice; Original resets the
   override. Back to file settings writes nothing and a new review preserves
   source categories by default. Failure: stuck dialog, cancelled choice applied,
   or game input receiving typed text.
4. Invalid input such as `<bad>` explains the rejection and disables confirmation.
   Fix it or select Original. Long warnings remain readable through the visible
   scrollbar or mouse wheel. Failure: raw exception, inaccessible warnings,
   confirmation still disabled after correction or unresponsive UI.

No resources, recipe unlocking, construction or world setup are needed. Do not
import the entire collection or repeat accepted scale/camera/Save As tests.
Per-document categories during multi-file selection, multi-file selection itself,
automatic Share files and player-to-player transfer are still pending.
