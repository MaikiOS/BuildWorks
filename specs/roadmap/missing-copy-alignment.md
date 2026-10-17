<!-- docs-role: design record; reviewed 2026-10-17 -->
> Design/specification record: requirements below are not blanket implementation or acceptance claims. [Current state and queue](../../docs/CURRENT_STATUS.md).

# Missing alignment: confirmed copy semantics

Confirmed by Ostrix on2026-10-13: selected source walls remain at their original positions; new copies align near the missing marker. Implemented/tested and deployed in .178; game acceptance pending. See docs/MISSING_COPY_178_RU.md.

Reuse detached selected-subtree insertion, not live DuplicateSelection followed by another transform (two history commits). Preserve selected part data, source provenance, nested group/pivot relationships with fresh IDs. Align keeps missing; align-and-replace removes only missing and retargets its references. Original IDs/poses/groups remain intact. One confirmed operation makes one Undo; preview/cancel create no live records. Reject copy budget overflow atomically.

Independent document supports/custom snaps stay once at their original positions for selected-part copies; they are not owned by that selection. Whole catalogue insertion retains its existing support/snap-copy behavior. Explicit primary anchor is required for alignment only, including catalogue data without primary or with primary after the first row. Local gizmo must use the copied anchor rotation; no new gizmo mechanism.

Expected three-record fixture: align becomes five records (original two, copied two, missing); replace becomes four (original two, copied two). Validate source/marker immutability, rotated target, nested pivots, one Undo/Redo, exact saved data and reopen. Controller should show detached duplicate preview, not move original visuals. Existing source/cost/network/scale rules remain in force.
