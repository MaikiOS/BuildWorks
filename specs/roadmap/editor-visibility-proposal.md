# Editor visibility and compact controls proposal

**English** | [Русский](editor-visibility-proposal_RU.md)

Draft for owner approval, 2026-10-01. Ostrix accepts the focused 0.19.41 game
series by report: stationary contour, cleanup, readable translated hints,
see-through, camera and tree. Remaining usability requests are collapsible hints,
less crowded manipulators, attachment context while seeing through, and compact
viewer settings. This document proposes changes; none are implemented or installed.
Ostrix's follow-up makes alternative 2 conditional on fast action/axis switching,
moving an existing editor part like a placement ghost, prominent next-step guidance
and original game-styled handles. The refined interaction below still needs approval.

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

Keep Q/G/A/C mode switching and existing gestures. A visible family selector chooses
idle handles; direct action keys start a temporary operation without finding a menu.
Move shows
arrows/planes, Rotate shows rings, Points shows attachment/pivot controls.
Scale stays explicitly accessible. Active, pinned and current target marks remain
distinct; ordinary points use small marks instead of large permanent diamonds.
Point detail choices belong in the manipulator panel, not on top of the mesh.

Changing displayed controls must not change the selection, pinned point, document
or Undo history. Drawing and hit testing use the same family state: an invisible
handle cannot intercept a click. Existing world/F9 defaults are unaffected.
Array, Contour and placement keep the controls needed for their own workflow.
No new manipulator framework or replacement snap mathematics is needed.

## Proposed action and axis workflow

These assignments are proposals, not current bindings. G/F9 still opens Transform
handles; Q/A/C keep Select/Array/Contour. R currently aliases Transform; making it
start rotation is an explicit interaction change for owner approval.

| Input with editable selection | Proposed next action |
| --- | --- |
| M or Take and move button | Pick up existing selection for cursor-following preview |
| R or Rotate button | Start rotation preview, initially around the current frame's Y axis |
| S or Scale button | Start uniform scale preview; do not imply per-axis resizing |
| X/Y/Z during Move | Constrain movement to an axis; repeat the same axis to clear |
| X/Y/Z during Rotate | Choose the rotation axis; repeating it keeps that axis |
| Shift+X/Y/Z during Move | Move in the plane excluding that axis |
| Space during Move or Rotate | Switch world/local frame, with an explicit label |
| Viewport LMB or Enter | Confirm once; the starting UI click cannot also confirm |
| Esc | Restore the exact starting state, keeping selection |

Examples: select wall → M → point at neighbor → LMB. Precise move: M → X →
move mouse → Enter. Rotate: R → Y → move mouse → Enter.
This uses Blender's action-then-axis principle, not identical bindings or repeated-axis
semantics: [Blender Axis Locking](https://docs.blender.org/manual/en/5.2/scene_layout/object/editing/transform/control/axis_locking.html).

Ctrl+G grouping, Ctrl+S saving and Shift+S step input keep priority while idle.
Camera flight with RMB and text entry own their inputs. During a pending operation,
save/history/catalog/selection-changing commands must not commit or undo some other
edit; show Confirm or cancel first. Escape or loss of application focus cancels.
Navigation freezes the preview; resume without a jump. Changing axis/frame or source
point rebases the interaction to the current preview instead of snapping unexpectedly.

## Move existing parts like the placement ghost

Surface following best matches construction. A screen-parallel plane is steadier
but hides depth control; offer it as an explicit fallback rather than automatically
switching movement methods. Axis/plane constraints use the chosen world/local frame.

Start from the existing selected point, otherwise the current selection pivot;
keep initial pose until deliberate mouse movement. Exclude all moving parts from
surface/target hits to avoid self-snapping. Reuse existing nearby/capture distances
and source-point rules; Q/E chooses source points and the wheel rotates around that
point using the placement yaw step. These gestures apply to surface Move, not
Rotate/Scale or camera flight. No automatic normal alignment.

Only in Surface Move, a missing surface hit retains the last preview but disallows
confirmation and says No surface. Let the user choose the explicit plane fallback.
Axis Move and the explicitly chosen plane can confirm a valid result without a
surface under the cursor. Never jump to far-away ground. Nearly parallel plane/axis
calculations must likewise fail safely.
Axis/plane limits outrank magnetic snapping: a candidate off the constraint is not
captured. This priority is a proposed interaction rule, not a cosmetic change.

Capture editable stable IDs, original transforms, temporary pivot/pin/constraint
state once. Preview with existing Scene.PreviewTransform; confirm through the
document's ApplyTransformDelta once. Restore from the document on cancellation.
Do not route confirmation through HandlePartPlacement/AddPart/InsertBlueprint.
No duplicate IDs, delete-and-recreate, anchor reassignment or Store change.
Keep relative group transforms, zero-delta commits do not dirty the document or
create Undo, and panels pause movement without confirming or leaking clicks.
This proposal affects existing parts in the isolated editor, not built world pieces.

## Prominent next step guidance

Keep one high-contrast gold-accent action strip visible even when reference hints
are collapsed. Use a bold action verb and framed key labels, not blinking text.
Show only meaningful continuations for the current state:

| State | Example guidance |
| --- | --- |
| Selected | Wall selected · M move · R rotate · S uniform scale |
| Surface Move | Point at a surface · X/Y/Z axis · Q/E source point |
| Axis Move | Move along X · World · X release axis · Enter confirm · Esc restore |
| Valid target | Target: wall · Point 3/8 · LMB/Enter confirm · Esc restore |
| Missing target | No surface · Point elsewhere or choose Plane · Esc restore |
| Confirmed | Moved · Ctrl+Z undo, then return to selection guidance |

Axes remain available as clickable controls, not keyboard-only actions. Proposed
footer strings must be localized; these examples are not new runtime keys.
Do not color every command equally. Errors have text/icon meaning as well as color.
Expanded six-group hints remain a reference, not the main instruction.

## Original game styled manipulators

Use restrained metal arrowheads, thin rotation arcs and small point marks with an
open center over the part. Preserve distinguishable XYZ colors and letter labels.
Dark edging should retain contrast against both wood and the grid; bronze/gold
belongs to hover, selection and peripheral UI, not elaborate ornaments on the mesh.
Inactive lines are subdued; hover brightens/thickens the approached handle;
drag shows the active control, source/pin/target and necessary constraints.

Hover does not enlarge the hit area or change which handle wins. Hidden families
cannot receive hits; retained pin marks are informative unless the active family
allows manipulating them. Exact color/opacity/icon shapes need an owner-approved
visual mockup before implementation. No Blender/Houdini/other-mod assets are copied.

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
and prominent next-step strip including current confirm/cancel. Return its height to the viewport;
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

Approve M/R/S, surface-following fallback, contextual Q/E/wheel, constraint priority,
next-step strip and visual mockup before code/icons.
Implement in bounded steps: footer collapse/guidance; explicit manipulator families
with matched hit tests; existing-part cursor preview with atomic confirmation;
compact panels; then target context outline. Preview each visible
step in the existing Unity Workbench before guarded installation.

Check a small wall, dense groups, coincident points, pin/mesh snap, adding,
Array/Contour and cancellation; EN/RU and supported UI scales. Invisible controls
must not capture input, panels must not click through, collapsed hints must recover
viewport space, and camera/document/world F9 behavior must remain unchanged.
Test exact cancellation/one Undo, zero-delta commit, group IDs/anchors, self-hit
exclusion, ray misses, axis/frame/source changes, camera navigation/focus loss,
UI-start click guarding and modifier/text priority.
Then run an explicit owner game series only in TerrainRamp-1.0-Test.
No version bump, deployment or release is part of this proposal.
