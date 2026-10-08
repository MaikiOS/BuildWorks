# Imported axes — source evidence, not native acceptance

Read-only research2026-10-08. No automatic reorientation is justified yet.

The local BiomeBlueprints-1.0.3 smoke file
artifacts/external-blueprints/BiomeBlueprints-1.0.3/smoke/hs_ashlands_madzobuild_coffeetable.blueprint
already has floor quaternion(0,.2902847,0,-.9569404), equivalent yaw-33.75°.
Floor position delta(.5555696,0,-.83145138) agrees with this orientation.
ExternalBlueprintLibrary preserves xyz/xyzw and only normalizes quaternion length.
The importer must not silently straighten the original author's geometry.

No saved placement support: BlueprintPlacementFrame.TryResolve returns identity;
registry rebasing changes origin without rotation. World plans use rootRotation×raw
partRotation. FramePartIndex choosing a carrier does not itself rotate the blueprint.
An explicit saved support intentionally contributes its inverse orientation.

The editor defaults to local axes: its active part can therefore tilt the gizmo.
World axes intentionally ignore the active part rotation. This is separate from
blueprint geometry and from the transform centre or placement support.

Next regression, not yet implemented: first part Rx90 with non-axis yaw, second
identity at(2,0,0), no support. Editor/registry/world root identity and delta(2,0,0)
must survive exchanging row order. Add explicit support Ry30 and prove inverse30
compensation without changing stored part poses. Keep an independent CoffeeTable
raw quaternion oracle. Actual Valheim placement remains a separate observation.

Global header rotations are not executed by the current parser; neither checked
CoffeeTable nor Forest Hut sample has such a header. Support must follow an actual
format specification and sample, not an inferred axis swap.
