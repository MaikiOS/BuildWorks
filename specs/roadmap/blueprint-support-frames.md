# Free blueprint supports — researched proposal, not implemented

**English** | [Русский](blueprint-support-frames_RU.md)

2026-10-06. Ostrix clarified that the desired feature is a freely authored blueprint
support with its own transform, not another snap point owned by a nearby piece.
Main source inspection plus two read-only agents examined UX and coordinate/storage
contracts. This proposal needs approval before new entity/UI/world persistence work.

## Keep the meanings distinct

- Native/derived/custom snap points: places to connect; current custom points contain
  only root-local XYZ, no name/orientation/identity.
- Primary part/group and existing group pivot: current geometry-bound frame choices.
- A/B: temporary operation pivot and edge direction, not saved independent entities.
- New support: named position + orientation, independent of the part geometry.
  Selecting one must not silently change placement origin or create a snap socket.

## Recommended first workflow

```text
Objects
  Groups and pieces                         (unchanged)
Supports
  Entrance · Placement origin
  Roof centre
  + Create support
```

Initially free supports belong to the blueprint root, not a part/group. Deleting or
reparenting nearby geometry cannot move/delete them. Group-owned free supports are
not needed for the first workflow; existing group pivots remain available.

Create at the current explicit A pivot, otherwise the document's current gizmo
pivot, then enter support-edit scope. No silent creation merely by clicking a point.
Select its tree row or marker: the inspector says `Support: <name>`; G/R and numeric
input edit its pose only, Enter commits one Undo, Esc restores the preview. Clicking
a part leaves support-edit scope. S is disabled here with an explanation: a position
and orientation have no size. Existing part click/delete/camera shortcuts are unchanged.

Two explicit assignments reuse the same support:
- `Use as transform centre`: selected parts then move/rotate/scale around this pose.
  Editing the support itself never moves those parts.
- `Use for placement`: one saved default for the whole blueprint. The old primary
  part/group remains stored as a fallback, not overwritten.

The selected support shows one clear centre and short oriented axes; unselected
supports use quiet markers and hover names. Do not introduce another always-visible
point cloud. A distinct support symbol and explicit inspector scope differentiate
it from snap candidates. Names/long rows must fit at all supported screen sizes.

## Data and maths

Add an independent support list with stable ID, bounded name, root-local position
and normalized quaternion, plus a saved placement-support reference. No scale field.
Store v11 / public portable v2 are required because current strict readers reject
unknown fields. Keep v10 custom points and old primary/group references compatible.
Undo, detached copying and import/export preserve poses and remap IDs atomically.
Duplicating a blueprint copies supports; duplicating a group does not steal a root
support. Removing the chosen support visibly restores the legacy placement fallback.

For local support `(p,q)`, target world pose `(A,R)`, uniform blueprint scale `S`:
`Q = R * inverse(q)`, `T = A - Q * (S*p)`.
World parts remain `T + Q*(S*partPosition)` and `Q*partRotation`. The same formula must
drive ghost preview and actual native placement. Assigning a support does not recentre
stored parts or change their poses. Changing frame orientation cannot be treated as
a position-only marker offset.

## World persistence is unresolved, not silently downgraded

Current placement produces independent native pieces without a durable blueprint
instance. File persistence and placement-preview support do not preserve a free
support after world reload. A server-authoritative instance manifest/registry and
host-removal, reconstruction, revision and remote-client rules must be chosen first.
Do not bind it semantically to an arbitrary part or add a hidden carrier as a shortcut.
No new world/save format or carrier has been implemented by this research.

## Acceptance gates

1. Create/name/G/R/number/Enter/Esc/Undo/Redo affects only the support.
2. Explicit transform-centre use rotates/scales chosen parts without scope leakage.
3. Save/open/export/import/duplicate retain pose and identity; old files still work.
4. Nonzero support offset + rotation + blueprint scale align preview and placement.
5. Part/group deletion preserves root supports; deleting the default gives visible fallback.
6. Tree and markers stay readable at supported resolutions; A/B remains temporary.
7. World save/reload/reconnect/host-part removal/remote client only after a persistence
   contract exists; no claim of world durability from editor tests alone.
