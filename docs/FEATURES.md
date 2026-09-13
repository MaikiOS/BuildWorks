# BuildWorks features and interactions

**English** | [Русский](FEATURES_RU.md)

Statuses in this document apply to alpha 0.19.33:

- **implemented** — the feature is present in the build;
- **automatically verified** — deterministic, Unity, or host checks pass;
- **in-game validation pending** — a manual Valheim test is still required.

## 1. Indexed hammer

BuildWorks opens its own index over the construction menu. Pieces can be browsed by category, material, recency, and favorites. Search, counters, and a separate entry into the blueprint library are available. DeepNorth pieces that belong to construction are merged into the construction category instead of appearing in a separate technical tab.

The middle mouse button uses Valheim's native favorites storage, and the star updates in the current interface. Selecting a card closes the menu and returns the regular placement ghost; the piece must not be built until the player clicks in the world.

**Status:** implemented; the basic in-game flow was accepted. A short recheck is required after Valheim updates.

## 2. Blueprint library

The library displays saved blueprints as cards with a preview, name, category, and piece count. Actions include place, edit, rename, change category, refresh preview, inspect resources, and delete.

A double left-click starts placement. Holding the right mouse button opens a contextual menu at the cursor; the hovered command runs when the button is released. `Select in world` enters selection mode for existing pieces without accidentally placing the previously selected ghost.

**Status:** implemented; primary in-game interactions were accepted.

## 3. Creating a blueprint

Two workflows are available:

1. Create an empty blueprint and add pieces from the editor catalog.
2. Select existing pieces in the world, save the selection, and open it in the editor.

World selection highlights pieces and preserves their real prefab identities and relative transforms. The structure is not converted into one model.

**Status:** implemented; world selection and transition into the editor were accepted.

## 4. Dedicated editor

![Selection mode](images/editor-select.png)

The editor is isolated from the game world. It provides a viewport, grid, controllable camera, lighting, catalog, object tree, active-tool parameters, and a continuously updated footer showing available actions.

UI scale from 60–140%, projection, lighting, grid, and visual handle size are configurable. Temporary editor objects are destroyed on exit and never become world objects.

**Status:** implemented and automatically verified at 1920×1080, 2560×1440, and 3440×1440.

## 5. Editor catalog and piece placement

![Blueprint catalog](images/editor-catalog-blueprints.png)

`Tab` opens the catalog of pieces and saved blueprints. Search covers display name, prefab, and source; filters and pages are available. Vanilla building pieces are listed first.

During placement, the mouse wheel rotates the piece, Q/E changes the source snap point, the middle mouse button deletes a hovered piece that was already placed, and the left mouse button commits the preview. For furniture or decorations without native snap points, the editor creates temporary points only: bottom center, four bottom corners, center, and top center.

**Status:** implemented and automatically verified; the new 0.19.33 surface-contact and Q/E behavior still requires the in-game smoke test.

## 6. Selection and precise transform

![Precise transform](images/editor-transform.png)

The user can select one piece, multiple pieces, a group, or a subtree. Available operations include:

- translation on X/Y/Z;
- rotation around X/Y/Z;
- uniform scaling from 1–400%;
- numeric entry and drag/scrub values;
- local or world axes;
- free movement or a single-axis constraint;
- distance and angle steps;
- native mesh snapping and BuildWorks snap points;
- pinning the selected point;
- Alt-drag duplication;
- one Undo step per completed operation.

Holding `N` temporarily displays the combined frame of the current selection instead of the assigned anchor without modifying the saved document.

**Status:** implemented. Numeric transform, frame override, duplication, and Undo are automatically verified; the main in-game workflows were accepted.

## 7. Object tree and groups

![Object-tree context](images/editor-outliner.png)

The tree displays pieces, groups, and nesting. It supports range selection, search, filters, drag/drop, group creation and deletion, reparenting, group/ungroup, rename, duplicate, visibility, and locking.

The tree context menu contains structure operations only. Hold the right mouse button to open it at the cursor and release over the required command.

### Anchors

- A **group anchor** defines the pivot for local transformations of the selected group; all descendants move with it.
- A **blueprint anchor** can be any single piece in the document and defines the world frame and snap points used when placing the complete blueprint.
- When an anchor is not assigned, the corresponding group or complete blueprint uses its combined bounds center.

Delete, move, duplicate, and Undo correctly clear or remap anchor references.

**Status:** implemented. Store v9, hierarchy/remap, and pointer regressions are automatically verified. The full multiplayer/reload gate remains open.

## 8. Array

![Array tool](images/editor-array.png)

Array repeats a selected piece or group in a line or plane. Golden arrows define the directions. Parameters include:

- number of items and number of rows;
- `PACK / NO GAP` for a natural constant step;
- `FIT LENGTH` to distribute copies between fixed endpoints;
- `EXACT STEP`;
- rise;
- heading, pitch, and roll for each next copy;
- scale step;
- symmetry.

The preview changes during numeric scrubbing, while the document changes only once when Apply is pressed. The complete operation is reversed by one Ctrl+Z.

**Status:** mathematics and Unity regressions pass. Fresh Array reset and Pack/Fit behavior in 0.19.33 still require a manual smoke test.

## 9. Contour

![Contour tool](images/editor-contour.png)

Contour takes the selected piece or group as its source and repeats it along a visible connected chain of support pieces. The user selects the target chain in the viewport, adjusts spacing, and confirms one atomic commit.

**Status:** implemented and automatically verified. Extended curves and editable guide nodes are not part of this tool yet.

## 10. Precision world placement — F9

F9 enables the continuous precision layer for a regular piece or the entire selected blueprint. F10 locks the current ghost for precise editing. Left Alt releases the cursor for dragging handles. Translation, three-axis rotation, local/world axes, steps, snap points, Array, and Contour are available.

When a blueprint has a world anchor, the gizmo and world snapping use it. Holding `N` temporarily displays the complete blueprint frame without changing the anchor.

**Status:** precision world placement, frame handling, and F9 Array/Contour are implemented. Separate save/reload and multiplayer gates remain open. In-game screenshots will be added after the 0.19.33 smoke test is accepted.

## 11. Composite blueprint construction

One preview represents the complete composition, but confirmation constructs real pieces sequentially through Valheim's native flow. Requirements, permissions, stamina, tool state, clipping, and per-piece restrictions are checked. If stamina runs out or the hammer breaks, the series can continue after recovery. A world plan is limited to 512 pieces.

Pressing Esc during an incomplete series cancels it and destroys pieces created by that unfinished attempt. Returning to construction restores the original blueprint ghost.

**Status:** automated lifecycle regressions pass; the complete survival/multiplayer smoke test remains mandatory.

## 12. Storage and compatibility

Blueprints preserve prefab identity, transforms, groups, local anchors, and the world anchor. Older formats are read compatibly without recalculating coordinates. BuildWorks does not extract meshes, textures, or materials from third-party mods.

**Not implemented yet:** reliable file transfer between different mod sets, dependency manifests, recovery of unknown prefabs, and a public import/export workflow.

## 13. Runtime localization

BuildWorks currently asks Valheim to localize native piece names, but the mod's own catalog, editor, HUD, errors, tool parameters, category names, and adaptive-piece metadata are embedded in Russian. There is no runtime language selector, English catalog, fallback layer, or automated locale test in 0.19.33.

**Status:** not implemented. English-first runtime localization with a complete Russian catalog is a required foundation item in the [roadmap](ROADMAP.md). The English GitHub documentation does not imply that the current in-game UI is English.
