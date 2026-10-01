# Editor visibility and compact controls proposal

**English** | [Русский](editor-visibility-proposal_RU.md)

Draft for owner approval, 2026-10-01. Ostrix accepts the focused 0.19.41 game
series by report: stationary contour, cleanup, readable translated hints,
see-through, camera and tree. Remaining usability requests are collapsible hints,
less crowded manipulators, attachment context while seeing through, and compact
viewer settings. This document proposes changes; none are implemented or installed.
Ostrix's follow-up makes alternative 2 conditional on fast action/axis switching,
moving an existing editor part like a placement ghost, prominent next-step guidance
and original game-styled handles. The subsequent keyboard review replaces the
M/R/S draft with a compact tool cluster and a separate action start. It needs approval.

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

Keep existing world gestures; reorganize isolated-editor shortcuts as proposed below.
A visible family selector and matching keys choose idle handles without moving the part.
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

These assignments replace the earlier M/R/S draft; they are not current bindings.
Prefer Q/W/E/R: frequent tool choices are adjacent under the left hand. A tool
choice changes visible handles, not geometry. Space starts its cursor-driven action;
dragging a visible handle starts directly. This costs one extra key versus direct
G/R/S actions, but clearly separates choosing a tool from starting an edit.
There are no double-tap/hold-to-switch gestures or automatic tool switches.

| Input with editable selection | Proposed next action |
| --- | --- |
| Q / W / E / R while idle | Choose Select / Move / Rotate / Uniform Scale; keep the part stationary |
| T while idle | Choose Points handles; retain explicit pin/attachment gestures |
| Space or Start button in Move / Rotate / Scale | Start cursor-following Move, Y-axis rotation or uniform scale preview respectively |
| Space in Select / Points | No transform; show the applicable tool choices or point gestures |
| X/Y/Z during Move | Constrain movement to an axis; repeat the same axis to clear |
| X/Y/Z during Rotate | Choose the rotation axis; repeating it keeps that axis |
| Shift+X/Y/Z during Move | Move in the plane excluding that axis |
| V in transform tools or their preview | Switch world/local frame, with an explicit label; replaces the old Space assignment |
| Viewport LMB or Enter during a Space-started preview | Confirm once; the starting UI click cannot also confirm |
| Esc | Restore the exact starting state, keeping selection |

Examples: select wall → W → Space → point at neighbor → LMB. Precise move:
W → Space → X → move mouse → Enter. Rotate: E → Space → X → move mouse → Enter.
Ordinary handle dragging still confirms on releasing LMB; it does not require Enter.
For an already active Move tool, Space is the one-key pickup. G/F9 remain compatibility
entries to the last transform family (Move initially), not new direct pickup aliases;
omit those duplicates from the compact main hint strip. A/C keep Array/Contour;
Tab is the primary catalog key. Existing Shift+A remains a secondary compatibility shortcut.
The adjacent tool cluster is also used by [Maya's Tool Box](https://help.autodesk.com/cloudhelp/2024/ENU/Maya-Basics/files/GUID-B345E162-0149-4E09-AC98-48DCFC227F33.htm).
This uses Blender's action-then-axis principle, not identical bindings or repeated-axis
semantics: [Blender Axis Locking](https://docs.blender.org/manual/en/5.2/scene_layout/object/editing/transform/control/axis_locking.html).

Input ownership is explicit: focused UI/text/dialog/catalog first, then camera
navigation over the viewport, then pending preview, then idle tools. E chooses
Rotate only while idle; in Surface Move it advances the source point. Q likewise
selects only while idle. During new placement Q/E/wheel retain their construction
meaning, not tool switching. Return from navigation/UI must re-arm movement without
a jump; tool input must not leak into text fields or the world session.
During a pending operation,
save/history/catalog/selection-changing commands must not commit or undo some other
edit; show Confirm or cancel first. This includes Q/W/E/R/T and A/C, except contextual
Q/E source selection. Space must not restart a pending operation. Escape or loss of application focus cancels.
Navigation freezes the preview; resume without a jump. Changing axis/frame or source
point rebases the interaction to the current preview instead of snapping unexpectedly.

## Remaining keyboard and mouse commands

Keep familiar commands instead of relocating every key merely to make the cluster smaller:
Ctrl+A all, Ctrl+D copy, Ctrl+G group, Ctrl+S save, Ctrl+Z undo, Ctrl+Y or
Ctrl+Shift+Z redo. Modified chords have priority over single keys. F frames selection,
Home frames all. H/Shift+H/Ctrl+H/Alt+H keep hide/isolate/show-except/show-all;
F7 keeps see-through. N remains the temporary composite pivot override, Shift+S
focuses the step field. No numeric keys replace geometric X/Y/Z labels.

The mouse proposal deliberately separates navigation from deletion: MMB orbit,
Shift+MMB pan, RMB+WASD/QE fly, wheel zoom, Shift while flying faster. Delete is
the sole viewport deletion key in the editor; remove tap-MMB deletion there, including
new-part placement, so a failed orbit never deletes a part. World MMB removal is
unchanged. This editor/world difference needs explicit owner approval.
Ctrl+wheel always zooms the editor camera; ordinary wheel rotates in Surface Move/new
placement, changes count in Array, and zooms otherwise. During pending previews,
navigation pauses/rebases the preview; it cannot confirm, delete or open the catalog.
Short RMB over the idle viewport keeps catalog access; RMB in the tree keeps its
context menu. Panels own wheel scrolling and their mouse gestures.

LMB selection, Ctrl/Shift+LMB additive selection, empty-space box selection, Alt+arrow
copy, Shift+point pin and Ctrl+point magnetic drag remain contextual gestures.
Do not turn Ctrl into a global magnet toggle: it already owns text/selection commands.
Array/Contour still use Enter to apply and Esc to cancel; Space does not start a second
operation. New-part placement still adds repeatedly by LMB; confirming an existing-part
move finishes that one operation. Esc while idle keeps current clear-selection/exit behavior.

Expose one per-context shortcut list in Settings with reassignment and reset, as already
planned in BW-01. Conflicts in overlapping contexts must be rejected or explicitly
resolved; resetting must not silently discard saved custom bindings. Labels and hints
must use the effective binding, not fixed text. Display Latin key identities in EN/RU;
test actual Unity input on Russian and English keyboard layouts rather than assuming
physical-key behavior. This is a future settings requirement, not an implemented service.
No extra layout presets, command palette, keyboard framework or world remapping in this pass.

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
| Selected | Wall selected · W Move · E Rotate · R Scale · T Points |
| Move tool idle | Move selected · Space pick up · or drag an arrow |
| Surface Move | Point at a surface · X/Y/Z axis · Q/E source point |
| Axis Move | Move along X · World · X release axis · Enter confirm · Esc restore |
| Valid target | Target: wall · Point 3/8 · LMB/Enter confirm · Esc restore |
| Missing target | No surface · Point elsewhere or choose Plane · Esc restore |
| Confirmed | Moved · Ctrl+Z undo, then return to selection guidance |

Axes remain available as clickable controls, not keyboard-only actions. Proposed
footer strings must be localized; these examples are not new runtime keys.
Do not color every command equally. Errors have text/icon meaning as well as color.
Expanded six-group hints remain a reference, not the main instruction. The compact
footer groups tool choice, current action and confirm/cancel into distinct blocks;
show only the applicable block plus the next step, not every keyboard command.

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

Approve Q/W/E/R/T tools, Space start/V axes, editor MMB navigation-only,
surface-following fallback, contextual Q/E/wheel, constraint priority,
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
