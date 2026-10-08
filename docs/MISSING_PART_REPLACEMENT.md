# Explicit missing-part replacement — .115

Use existing placement and gizmo tools to prepare your own replacement. Select
exactly one missing row directly and the available replacement parts/group, then
Tools → REPLACE MISSING PART. Confirmation identifies the missing prefab and
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
prefab availability. Cancel or stale review changes nothing. Native19/portable9
remain unchanged. Save/reopen uses the existing atomic store path.

Workbench verifies actual controls and disk reopen. Native construction, world
reload and multiplayer still require game acceptance. See QA_0.19.115_RU.md.
