# Explicit missing-part replacement — current source

Updated 2026-10-09. The new entry is not deployed to QA .116.
Incomplete documents keep REPLACE MISSING PART visible above the inspector.
Without a ready selection it explains the steps and opens the real catalogue,
without deleting anything. Select the missing row and own parts together again
after placing your replacement.

Use existing placement and gizmo tools to prepare your own replacement. Select
exactly one missing row directly and the available replacement parts/group, then
the inspector button or Tools → REPLACE MISSING PART. Ctrl-click adds tree rows.
Confirmation identifies the missing prefab and
replacement count. Already placed selected parts are used; no extra copies,
automatic alignment, resizing or name matching are performed.

One operation removes the missing working record and groups the chosen parts under
its former parent. Their positions, rotations, scales and payloads stay unchanged.
Primary-part and pivot links targeting the removed record move to the first chosen
part in document order. If moving parts empties the primary group, its reference
moves to the replacement group. Independent supports, custom snaps and preserved
source documents are unchanged. One Undo restores the full prior state.

Hidden/locked parts, two missing rows, overlapping parent/child selection and group
capacity are refused. Confirmation rechecks document revision, selection and live
prefab availability. Cancel or stale review changes nothing. This step does not
change current native21/portable10. Save/reopen uses the existing atomic store path.

Workbench verifies actual controls and disk reopen. Native construction, world
reload and multiplayer still require game acceptance. Current scenario:
[visible replacement](MISSING_REPLACEMENT_ENTRY_CHECKPOINT_2026-10-09.md).
