# Editor visibility and compact controls proposal

**English** | [Русский](editor-visibility-proposal_RU.md)

Draft for owner approval, 2026-10-01. Ostrix accepts the focused 0.19.41 game
series by report: stationary contour, cleanup, readable translated hints,
see-through, camera and tree. Remaining usability requests are collapsible hints,
less crowded manipulators, attachment context while seeing through, and compact
viewer settings. This document proposes changes; none are implemented or installed.
Ostrix's follow-up makes alternative 2 conditional on fast action/axis switching,
moving an existing editor part like a placement ghost, prominent next-step guidance
and original game-styled handles. The keyboard proposal now keeps Blender-style
G/R/S actions, restores direct persistent-tool switching, and adds one keyboard
tool menu for future growth. It replaces the rejected Q/W/E/R/T and Space-start
draft; the revised interaction still needs approval.

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

These are proposed bindings, not current behavior. Keep immediate actions separate
from persistent tools: G/R/S begins a temporary preview directly; selecting a tool
changes its idle controls without modifying geometry. Neither path requires a
preparatory toolbar click or Space to start a transform.

| Input with editable selection | Proposed next action |
| --- | --- |
| G / R / S while idle | Start Move / Rotate / Uniform Scale preview immediately |
| Top-row 1 / 2 / 3 / 4 while idle | Choose Select / Gizmo / Array / Contour; Gizmo remembers its last control family |
| Space while idle | Open Tools under the cursor; select by a shown key or click |
| F3 while idle | Open the same Tools popup with search focused; type a name, arrows and Enter to choose |
| X/Y/Z during Move or Rotate | Choose an axis; repeat that axis to switch current/alternate world-local orientation, showing both axis and frame |
| Shift+X/Y/Z during Move | Move in the plane excluding that axis |
| Viewport LMB or Enter during a keyboard-started preview | Confirm once; a starting UI click cannot also confirm |
| Esc | Restore the exact starting state, keeping selection |

Examples: select wall → G → point at neighbor → LMB. Precise move:
G → X → move mouse → Enter. Rotate: R → X → move mouse → Enter.
Ordinary handle dragging still confirms on releasing LMB; it does not require Enter.
Uniform scale does not offer S → X/Y/Z: nonuniform scaling is outside this proposal.
Unconstrained G depth behavior and R rotation must be decided before promising
Blender's third repeated-axis press to clear the constraint. Valheim uses Y-up;
Blender uses Z-up, so screen-facing rotation cannot silently mean world Y.
F9 can remain a compatibility entry to Gizmo; omit duplicates from the main hints.
Tab remains the catalog key, with existing Shift+A as a compatibility shortcut.

The 1–4 map is our proposal, not Blender's tool map; numpad keys are not implicitly
included. A becomes Select All, Array moves to 3; C may remain a Contour compatibility
alias, not a second advertised primary. Do not auto-renumber tools or reserve 5–0
for imaginary tools. New tools get a stable command identity and appear in Tools;
an optional direct binding can be assigned without displacing existing/custom keys.
Reuse the current tool enumeration and planned binding settings, not a new framework.

Space → shown key is sequential, not a held chord. It leaves text letters available
outside the popup. Search takes keyboard focus only on explicit field activation
or F3; the same letter cannot simultaneously select a tool and enter search text.
Both paths call the same existing tool handlers. Blender provides this pattern as
the configurable Spacebar Action **Tools**, not the default Play behavior:
[Blender Keymap](https://docs.blender.org/manual/vi/5.2/editors/preferences/keymap.html).
Its [F3 Search](https://docs.blender.org/manual/en/3.6/interface/controls/templates/operator_search.html)
supports name lookup. Axis orientation follows the
[Blender Axis Locking principle](https://docs.blender.org/manual/en/5.2/scene_layout/object/editing/transform/control/axis_locking.html),
subject to the unconstrained-action gate above.

Input ownership is explicit: focused UI/text/dialog/catalog first, then camera
navigation over the viewport, then pending preview, then idle tools. During Surface
Move/new placement Q/E/wheel retain their construction meaning, not tool switching.
Return from navigation/UI must re-arm movement without
a jump; tool input must not leak into text fields or the world session.
During a pending operation,
save/history/catalog/selection-changing commands must not commit or undo some other
edit; show Confirm or cancel first. This includes tool changes, Space/F3 and a new
G/R/S action, except contextual Q/E source selection. Digits during a modal transform
are reserved for numeric entry, not 1–4 tool switching; this is a future input
requirement, not shipped numeric-entry support. Escape or loss of application focus cancels.
Navigation freezes the preview; resume without a jump. Changing axis/frame or source
point rebases the interaction to the current preview instead of snapping unexpectedly.

## Remaining keyboard and mouse commands

Keep familiar commands instead of relocating every key merely to make the cluster smaller:
A all (Ctrl+A compatibility), Ctrl+D copy, Ctrl+G group, Ctrl+S save, Ctrl+Z undo, Ctrl+Y or
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
Space/F3 shares one tool list; do not add a second command palette, layout presets,
keyboard framework or world remapping in this pass.

## Move existing parts like the placement ghost

Surface following best matches construction. A screen-parallel plane is steadier
but hides depth control. The proposed pair is Surface with an explicit Plane
fallback, not automatic switching; the initial G method and depth interaction
remain an approval gate. Axis/plane constraints use the chosen world/local frame.

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
| Selected | Wall selected · G Move · R Rotate · S Scale · Space Tools |
| Persistent tool choice | 1 Select · 2 Gizmo · 3 Array · 4 Contour; highlight the active tool |
| Surface Move | Point at a surface · X/Y/Z axis · Q/E source point |
| Axis Move | Move along X · World · X switch to Local · Enter confirm · Esc restore |
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

## Viewport space and tooltip fit

Borrow Blender's resizeable area boundaries, collapsible side regions and reversible
viewport maximization, not its whole window/docking system. Start with the existing
tree/inspector split and minimum readable widths; compact icon toolbar, dropdowns
and a Focus button must restore the previous layout. Keep the next-action strip
visible while focused. Available space belongs to the model, not more permanent
buttons. [Blender Areas](https://docs.blender.org/manual/en/5.2/interface/window_system/areas.html)
documents resizing and maximization; these are proposed adaptations, not shipped controls.
Do not copy Blender N for a sidebar: N already owns the composite-pivot override.

Static audit: runtime `BlueprintEditorView` creates a fixed 250×58 tooltip with
10/4-pixel insets and places that box inside `safeRoot`. `ShowTooltipNow` does not
measure text or resize for content. This confirms a fit risk, not a reproduction
of the owner's exact clipped string. The synthetic Workbench uses character-count
width estimates; those are not actual glyph measurements. The inspected runtime
acceptance checks buttons and status hints, but has no dedicated tooltip-fit gate.

Fix shared tooltip sizing: measure localized text at a bounded width, wrap and
derive height including padding, then clamp the measured rectangle to the safe
viewport. Keep readable font size and non-intercepting tooltip input. If content
cannot fit even then, shorten the tooltip and put detail in explicit help; do not
silently clip or shrink it to illegibility. Validate real runtime tooltip text,
not only the synthetic UI, in EN/RU at supported UI scales, narrow windows and
screen corners, including long object names and effective custom bindings.

## Implementation and approval gates

Approve G/R/S actions, 1–4 persistent tools and one Space/F3 tool popup,
unconstrained Move/Rotate and depth behavior, editor MMB navigation-only,
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
Check tool keys versus modal numeric input, popup letter/search focus, repeat-axis
orientation, camera flight priority, effective bindings and measured tooltip fit.
Then run an explicit owner game series only in TerrainRamp-1.0-Test.
No version bump, deployment or release is part of this proposal.
