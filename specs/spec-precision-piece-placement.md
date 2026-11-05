# Precisely Place One Building Piece

## Problem

Valheim lets the player choose and snap individual parts, but severely limits
their translation and rotation after snapping. Complex roofs and diagonal
structures therefore fail because the required placement cannot be expressed.

## Users

Ostrix is the first builder and interaction approver. Later the same behavior
serves players on a compatible modded server.

## Proposal

Extend the existing placement ghost instead of replacing it. The player aims
and snaps one selected piece normally, freezes that attachment, then manipulates
the same ghost with visible XYZ arrows and rotation rings. Confirming returns to
Valheim's native placement path so the original piece, costs, restrictions,
support, persistence, and multiplayer identity remain intact.

## User Stories

- As a builder, I want to adjust one snapped piece on every axis so that I can
  create roof and frame angles absent from the vanilla catalogue.
- As a builder, I want mouse-driven handles and visible numeric deltas so that
  precision does not require cycling through a long keyboard parameter list.
- As a builder, I want to retain native snap points so that precise placement
  still connects naturally to existing blocks.
- As a builder, I want to drag one corner while its diagonal opposite remains
  fixed so that beams, walls, and roof pieces can be aimed from an exact anchor.

## Acceptance Criteria

- Given an active vanilla placement ghost, when precision mode starts, then its
  current snapped position and rotation become the resettable base transform.
- Given move mode, when an XYZ arrow is dragged, then only that displayed axis
  changes and the result is quantized to the selected 1/5/10/50 cm step.
- Given rotate mode, when an XYZ ring is dragged, then rotation changes around
  that displayed axis using the selected 0.1/0.5/1/5/15 degree step.
- Given local or world mode, when a handle is dragged, then its visible axes and
  applied transform use that same coordinate space.
- Given anchor mode, when one visible corner handle is dragged, then the diagonal
  opposite corner remains fixed in world space while the rigid piece rotates.
- Given edit mode, when it opens, then Valheim stops receiving character and
  camera input while the cursor operates the HUD and gizmo. Middle-mouse orbit,
  Shift+middle-mouse pan, and wheel zoom provide an independent view of the piece.
- Given an ordinary buildable object (including furniture, storage, stations and extensions),
  when F9 placement is validated, then a fresh complete contact at its final pose
  provides real support. The original aim hit cannot validate an object floating elsewhere.
  Native surface, station, location, access, player, biome, clipping and payment checks
  remain final. Native `WearNTear` evaluates the actual placed structure afterward.
- Given an active supported building camera, when F9 releases its own view to confirm,
  then it restores permitted camera policy before native validation. A rejected placement
  reacquires the camera before reopening F9; cancel releases owned input and view state.
- Given edit mode, when the player clicks a gizmo or HUD control, then no piece
  is placed and no resources, stamina, or durability are consumed.
- Given edit mode, when the orbit camera no longer points at the original surface,
  then the captured ghost remains visible and keeps its approved transform.
- Given a valid edited transform, when the player clicks `ГОТОВО`, then Valheim
  validates and holds that ghost until the normal placement click creates exactly
  one instance through the complete native cost and tool-use transaction.
- Given cancel or reset, when invoked, then the ghost, camera, controls, and cursor
  return to their prior safe state without leaving hidden renderers or helpers.

## Original first-slice exclusions — historical

The current module follows `docs/GOAL_FULL_RU.md`. Ostrix's29Oct clarification
extends ordinary F9 objects and final-pose contact as specified above; the old
structural-only exclusions below are not current furniture/station admission rules.
They record the original first slice, not a reduction of the complete module goal.

Original exclusions: scaling, mesh deformation, alternative production pieces,
automatic arrays, construction guides, special ground/water/station pieces,
full transformed surface re-raycasting, final art, multiplayer editing and public release.

## Dependencies

BepInEx, the current Valheim/Unity host assemblies, and the guarded
`TerrainRamp-1.0-Test` deployment path.

## Success Metric

Ostrix places and reloads one ordinary piece at a non-vanilla three-axis angle
with 1 cm / 0.1 degree precision, attached from a normal existing snap point.

## Estimated Effort

Large: runtime gizmo, host validation integration, and in-game interaction QA.

## Priority

P1 — replacement first slice for BuildWorks.
