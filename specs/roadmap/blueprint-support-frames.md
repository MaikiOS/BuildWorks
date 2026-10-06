# Free blueprint supports — approved, document foundation implemented

**English** | [Русский](blueprint-support-frames_RU.md)

## Checkpoint 0.19.63 — placement frame, not UI

Saved placement support now rebases Hammer visuals, collider bounds and native sockets
with the same inverse pose used to recover the geometric root before placement plans.
Nested/mirrored XYZ-scale Unity visuals agree with root math; 50/100/200% support-offset
compensation, actual Session composition/commit/history and legacy fallback pass.
Placement origin remains separate from world-F9 bounds pivot and geometry/socket filters.
Release, 108 Geometry, six Bridge, 12 WorldLayout, Store/Localization/HostContract/
Deployment/format checks and fresh 144-state Unity PASS (2026-10-06T10:17:52Z).
Native Quaternion.Inverse is checked inside Unity, not simulated outside the engine.
Independent correctness and Ponytail reviews PASS. Tree/G/R/explicit transform-centre and world durability
remain; installed .56 and saves unchanged. No Valheim placement/reload/network proof.

## Checkpoint 0.19.62 — storage and exchange

Store v11 and portable v2 preserve support ID/name/root-local position/unit rotation
and nullable placementSupportId. Old v10/v1 documents migrate to empty supports without
rewriting the source on read. Version-mislabeled/unknown/duplicate fields block writes;
support IDs share the part/group namespace, maximum 64. Legacy update overloads retain
payload; explicit support-aware updates can clear the default/list. Atomic write failure
restores support refs, geometry, points and metadata. Detached external copies remap all
support IDs/defaults without moving parts; editor save/open bridges preserve poses.

Automated Store tests include actual 353-file archive (109 format-compatible, 244 blocked),
v10/v1 migration, malformed payloads, max/overflow, clone and rollback. Actual EditorBridge
adds support disk roundtrip; Unity's real Controller.TrySave/OpenExisting also checks a
rotated support/default. Release, required target checks and independent correctness/
Ponytail pass; verify helper absent. Fresh 144-state Unity proof is recorded under
artifacts/support-storage-0.19.62 (gameHostTested=false).

Storage is complete for this slice, not the player workflow: tree/G/R/transform-centre,
placement-frame composition, world reload/network durability are still pending. New data
must not be advertised as a working world placement frame before the pose resolver is
integrated. Interactive budget 512 and installed .56 remain unchanged; no game/save writes.

## Checkpoint 0.19.61 — foundation only

Ostrix approved the workflow on 2026-10-06. BlueprintEditorDocument now holds
independent root-owned Supports (maximum 64), separate ActiveSupportId and optional
PlacementSupportId. Add/name/pose/default/delete share validated atomic Undo/Redo
and rollback. Object selection is retained when selecting a support; object clicks
leave support scope. Geometry duplicate/delete/reparent do not own these supports.
Insertion remaps IDs and composes root pose without replacing destination default.
Controller.CopyDocument now deep-copies supports and custom snap points.

Release has zero warnings/errors. 108 Geometry checks, five actual EditorBridge
checks, Store/Localization/HostContract/WorldLayout/DeploymentChecks pass.
144 exact-source Unity UI states pass at 2026-10-06T09:35:25Z, gameHostTested=false.
Independent correctness and Ponytail review pass; verify skill is absent.

This does NOT implement the Supports branch, G/R UI routing, disk schema or world
persistence yet. Store remains v10 and portable remains v1; no support payload is
accepted or written there. The new model is not exposed to players. Next complete
slice must connect v11/v2, tree/inspector/gizmo and all copy/save paths together.
Free frame math must stay separate from HasExplicitFrame/IsFramePart, whose
existing role also filters geometry/native sockets. Rotated placement/default and
history require the same pose resolver before world use. No limit increase,
deployment, game launch or save mutation.


2026-10-06. Ostrix clarified that the desired feature is a freely authored blueprint
support with its own transform, not another snap point owned by a nearby piece.
Main source inspection plus two read-only agents examined UX and coordinate/storage
contracts. The first workflow is now approved; world persistence still needs its
server-authoritative contract and in-game acceptance.

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
