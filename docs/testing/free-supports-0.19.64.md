<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](../CURRENT_STATUS.md).

# Free support acceptance — 0.19.64

**English** | [Русский](free-supports-0.19.64_RU.md)

For the next authorized TerrainRamp-1.0-Test deployment, not the unchanged installed .56.
No Valheim was launched for this checkpoint. Use a disposable two-piece blueprint.
Report exactly one result per number: `1 — works` or `1 — problem: action, expected,
actual, screenshot`. There are six cases and no supplementary lettered cases.

1. **Create/select.** Select two pieces and Create support in the Supports branch.
   Its row, inspector and gizmo appear without moving pieces. Explicit A wins when
   creating; F frames the support. Clicking a piece restores piece-edit scope.
   Long names elide with full tooltip; lower inspector actions remain scrollable.
2. **Move.** Drag/release an arrow; then G, X, mouse motion, type 1, Enter.
   Only the support moves. The number replaces mouse displacement from the start.
   One Ctrl+Z undoes the last move. G/Esc commits nothing. Handles remain readable
   in both model-space and constant screen-space sizing.
3. **Rotate/no scale.** R, Y, mouse motion, type 90; press S before Enter.
   S explains no scale without cancelling rotation; result is exactly 90 degrees
   from the start. Pieces remain unchanged. Inspector Apply commits name and pose
   together, with one Undo. A 513-m coordinate is rejected without mutation.
4. **Piece transform centre.** Retain selected pieces, choose a support and Use as
   transform centre; R/S changes those pieces around the support, not the support.
   The tree marks the role. Reset transform centre clears transient assignment;
   choosing another A restores the ordinary point-based workflow.
5. **Placement/save.** Assign Use for placement, save, close and reopen the test.
   Support name, XYZ, rotation and Placement role persist; piece poses are unchanged.
   Support edit focus and transient centre do not reopen. Clear placement support
   restores legacy fallback without deleting the support.
6. **Delete/isolation.** Support Delete removes only the support and clears its
   placement role. Ctrl+Z restores both. Closing and opening a new document must not
   retain the old support, G/R preview or temporary centre.

Persistent world support instances, world reload/network acceptance and quiet unselected
viewport markers remain separate work. Current support selection is through the tree.
