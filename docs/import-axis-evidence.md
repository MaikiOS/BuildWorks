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

Implemented regression, passed2026-10-08T16:19:41Z in Unity Workbench252 and
independent correctness/ponytail review: first part Rx90/Ry45, second identity
at(2,0,0). Both source row orders preserve actual editor positions and rotations.
Without saved support the shared placement frame remains identity. With support
Ry30 the shared Hammer rebase/root plan gives inverse30 and delta(sqrt3,0,1),
checked against independent constants without changing stored part poses.
ActualDividerAcceptance additionally pins the original CoffeeTable source SHA
and checks both floor rotations against yaw-33.75 and their literal displacement.

Boundary: shared frame/rebase/root-plan helpers and actual editor visuals executed;
native registry/CreatePrefab, FramePartIndex and Session/world placement did not.
The test keeps cloned child order fixed, so the reversed source rows prove importer/
editor order independence only. No production change or automatic straightening.
Evidence: artifacts/ui-runtime/verification-status.json, actual-divider-check.txt
and artifacts/ui-axes-110-current.log. Actual Valheim placement remains open.

Global header rotations are not executed by the current parser; neither checked
CoffeeTable nor Forest Hut sample has such a header. Support must follow an actual
format specification and sample, not an inferred axis swap.
