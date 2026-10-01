# Editor visibility and compact controls proposal

**English** | [Русский](editor-visibility-proposal_RU.md)

Draft for owner approval, 2026-10-01. Ostrix accepts the focused 0.19.41 game
series by report: stationary contour, cleanup, readable translated hints,
see-through, camera and tree. Remaining usability requests are collapsible hints,
less crowded manipulators, attachment context while seeing through, and compact
viewer settings. This document proposes changes; none are implemented or installed.

## Why the selected part disappears under controls

The current editor enables Move, Rotate, plane and scale handles together.
It also defaults to all anchors: 21 bounds points plus exterior native snap points.
Native points have a 36-pixel hit radius at default editor size, versus 12 for
bounds points; most handles lose click priority to an anchor. Reducing opacity
alone would not fix input interception.

Evidence: `BlueprintEditorController.cs` (`showAllAnchors`, `TryBeginGizmoDrag`);
`BlueprintEditorScene.cs` (`ShowGizmo`, `TryGetGizmoAnchors`);
`TransformGizmoView.cs` (`Show`, `AnchorHitRadius`, hit tests).
Geometry, world anchor, group pivots and snap calculations must stay unchanged.

## Three alternatives

| Proposal | Interaction | Benefit | Tradeoff |
| --- | --- | --- | --- |
| 1 Lighten the combined manipulator | Keep all transform handles; show anchors near the cursor, plus selected/pinned points; reduce ordinary glyphs and matching hit areas | Smallest change and least relearning | Nearby points still crowd a small part; Move and Rotate remain superimposed |
| 2 Explicit active control family | Within Transform choose Move, Rotate or Points; Scale remains an explicit control and numeric field | Predictable picking and a visible part; no invisible input arbitration | One extra switch when changing operation |
| 3 Automatic cursor focus | Keep the combined manipulator; highlight one candidate, dim neighbors and retain only essential marks during drag | Fewer switches for experienced players | Ambiguous overlaps require stable arbitration and a way to reach alternatives; greatest complexity |

Recommendation after rechecking small parts and dense groups: **2**.
Alternative 1 mostly reduces drawing; alternative 3 moves complexity into hidden
behavior. An explicit family makes the available action visible before the click.

## Proposed manipulator behavior

Keep Q/G/A/C mode switching and existing gestures; choose the Transform family
with a small visible control. Do not assign conflicting new shortcuts. Move shows
arrows/planes, Rotate shows rings, Points shows attachment/pivot controls.
Scale stays explicitly accessible. Active, pinned and current target marks remain
distinct; ordinary points use small marks instead of large permanent diamonds.
Point detail choices belong in the manipulator panel, not on top of the mesh.

Changing displayed controls must not change the selection, pinned point, document
or Undo history. Drawing and hit testing use the same family state: an invisible
handle cannot intercept a click. Existing world/F9 defaults are unaffected.
Array, Contour and placement keep the controls needed for their own workflow.
No new manipulator framework or replacement snap mathematics is needed.

## Proposed see through context

Keep the accepted F7 behavior. When an active snap target is known, show only
that target object's restrained shape outline and its active attachment mark,
even if the solid drawing is temporarily hidden. Do not draw every hidden
object's wireframe. This is a proposal for spatial context, not an exact mesh
intersection calculation. Toggle F7 off and orbit to inspect the real joint.

There is a separate current limitation: `IsEditorPointVisible` still includes
temporarily hidden blockers, and target search uses that visibility filter.
Therefore F7 does not guarantee access to every interior snap point. Do not silently
change candidate eligibility to implement a visual outline. Interior-target access
needs a separate interaction decision and regression test.

Do not add transparent-material rewriting, a screen-space cutaway shader or
mesh-intersection machinery in this pass.

## Proposed compact viewer and hints

Use three small labeled dropdowns in a viewport header:
**Camera**, **Display**, **Manipulator**. Camera contains FOV, game-value reset,
projection and framing. Display contains grid and F7. Manipulator contains active
family, existing sizes and point-detail options. Reuse current controls/handlers;
open by click, close with Escape or an outside click, and consume that click.
Expose current state in the header. Icons use original BuildWorks styling and tooltips.

A single Hints collapse button reduces the six-group footer to a narrow status
and current confirm/cancel strip. Actually return its height to the viewport;
do not merely hide text. Expanding restores the existing groups. Keep errors and
modal instructions readable. Do not automatically reopen all hints on every tool change.
Editor UI state must not alter blueprint saves or the world camera.

The design reference is Blender's separation of tool/object gizmos and detailed
dropdowns, plus Houdini's separation of control visibility from geometry visibility.
These are principles, not copied assets or a claim that this is identical to either app.
See [Blender Gizmos](https://docs.blender.org/manual/en/latest/editors/3dview/display/gizmo.html),
[Blender View settings](https://docs.blender.org/manual/en/latest/editors/3dview/sidebar.html),
and [Houdini control selection sets](https://www.sidefx.com/docs/houdini/character/kinefx/selectionsets.html).

## Implementation and approval gates

Approve the recommended family behavior and compact header before code/icons.
Implement in bounded steps: footer collapse; explicit manipulator families with
matched hit tests; compact panels; then target context outline. Preview each visible
step in the existing Unity Workbench before guarded installation.

Check a small wall, dense groups, coincident points, pin/mesh snap, adding,
Array/Contour and cancellation; EN/RU and supported UI scales. Invisible controls
must not capture input, panels must not click through, collapsed hints must recover
viewport space, and camera/document/world F9 behavior must remain unchanged.
Then run an explicit owner game series only in TerrainRamp-1.0-Test.
No version bump, deployment or release is part of this proposal.
