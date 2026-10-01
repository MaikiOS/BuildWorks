# BuildWorks editor input revision and acceptance tests

**English** | [Русский](editor-input-revision_RU.md)

Approved by Ostrix, 2026-10-01; .45 removes selection tint, retaining .44 immediate handles.
Selection preserves the original material; world/F9 controls are unchanged. Automated
evidence is recorded in HANDOFF; owner Valheim acceptance remains pending.

## Findings in .42 before this revision

- `SelectionGizmoTool` maps Select to Transform, so selection already shows handles.
- G currently calls `BeginKeyboardPreview(Move)` rather than opening all handles.
- Plain 1–4 select persistent tools; the tools popup separately consumes digits.
- `IBlueprintEditorInput` has no text stream for modal numeric entry. Inspector
  TMP fields are a different input path and do not implement this request.
- `RebaseKeyboardPreview` copies the current delta into a mouse baseline. Exact
  typed values must instead use an immutable operation-start snapshot.
- MMB currently starts camera tracking, with an existing six-pixel movement
  threshold. Deletion on mouse-down would conflict with orbit over geometry.
- The editor scale glyph is stroke-only and hittable within six pixels of its
  lines. Enlarging only its invisible hit region risks stealing nearby handles.
- Gizmo art consists of generated meshes/lines and colors, not the concept's
  engraved interior artwork or texture assets. Existing toolbar SVG export is
  available; no new vector rendering dependency is required.

## Approved controls

| State | Input | Result |
| --- | --- | --- |
| Select with an editable selection | Select part | Original material, selected tree row and immediate combined gizmo; no edit |
| Select or combined gizmo ready | G | Start cursor Surface Move of existing selection |
| Gizmo ready | X, Y or Z | Start axis Move without a second G |
| Gizmo ready | R or S | Start Rotate or uniform Scale |
| Pending Move or Rotate | X, Y or Z | Reset mouse preview to original pose and constrain; repeat toggles World/Local |
| Pending Move | Shift plus axis | Plane excluding that axis |
| Pending Surface Move | Q/E, wheel | Source point updates attachment immediately, yaw; P chooses screen plane |
| Pending transform | Typed number | Replace mouse result using the original action snapshot |
| Pending transform | Enter or viewport LMB | Validate and confirm exactly one edit |
| Pending transform | Esc | Exact rollback; return to combined gizmo |
| Gizmo ready | Esc | Return to Select with its gizmo; Esc in Select clears selection |
| Any viewport tool | Ctrl+1/2/3/4 | Select/Gizmo/Array/Contour; cancel pending preview first |
| Ready Select or adding | MMB click on a placed part | Remove that one eligible part on release |
| Viewport | MMB drag, Shift+MMB | Orbit, pan; never remove |

G/R/S/axis operations are available directly from Select; no first-G gate remains.
Ctrl+2 selects the explicit Gizmo tool. Combined
includes translation, planes, rotation, uniform scale and source/pin controls.
Keep optional manual display filters; a transform action opens the combined default. Array and
Contour keep their own parameters and geometry, not shared transform state.

## Numeric entry and operation ownership

Capture selection IDs, original transforms, pivot, pin and axis frame at action
start. Camera/mouse rebasing changes pointer mapping, never this reference.
`R → X → 30` means 30 degrees from that action's original pose, not 30 more than
the mouse preview. Axis Move `2` means two metres from its start. Scale `1.5`
means multiply the initial sizes by 1.5, preserving group layout and bounded
1–400% part scale; Inspector percentages retain their existing separate meaning.

Typing owns the preview until cleared. Mouse movement cannot add to a typed
value. Support main-row/keypad digits, minus, period/comma, Backspace and Delete
editing. Empty or incomplete input such as `-` or `1,` cannot silently confirm.
Use bounded finite numeric values; no expressions in this slice. Unconstrained
Move must request an axis before accepting one scalar. Invalid typed scale must
show an error, not clamp or commit. Clearing the buffer returns to mouse control
without a jump. One-axis S is not offered because current scale is uniform.

Changing R/S/Move during a preview rolls back the old preview and starts the new
action from the unchanged document. A tool chord does the same before switching.
Ctrl+Z during a preview cancels it without also undoing the previous document edit;
press again for history. Ctrl+S/duplicate/group cannot mutate a pending preview.
MMB and RMB camera navigation pause it; resuming does not change the reference.
Focus loss cancels. Only one transform, Array, Contour, placement or camera gesture
owns input at a time. A held/released mouse button after cancel cannot commit a
later action. Escape handles the innermost text/menu/action first, not several.

## MMB deletion and input priority

Capture eligible hovered part ID and start position on press, not release hover.
Delete only on release over the viewport when the gesture remained a click,
started on that part, and no UI/control/drag/modifier owned the gesture. Crossing
the camera threshold even once permanently suppresses deletion for that gesture.
An empty-start drag never becomes deletion. Shift/Ctrl/Alt+MMB are navigation or
reserved gestures, not deletion. During an active transform MMB is navigation
only. During adding, remove a placed part but never the uncommitted ghost.
Locked, manually hidden, missing or removed targets are not eligible.

Priority: focused dialog/text field → popup → active gesture → tool commands →
selection/navigation. Ctrl+1–4 must precede the old generic modifier return;
plain digits must no longer switch tools. Tool search consumes text and popup
digits without passing them into transforms. RMB flight owns WASD/Q/E, not S
scale or Q/E snapping. UI presses cannot become viewport confirmation.

## Artwork and scale handle

Implemented sources: `assets/blueprint-editor/icons/source/gizmo-*.svg`, six
original editable drawings with metal gradients, engraving and rivet details.
`scripts/Build-BlueprintEditorIcons.ps1` exports transparent 128×128 PNGs into
`src/BuildWorks/Assets/BlueprintEditorIcons` and one complete review sheet.
The existing icon loader embeds these assets; no vector runtime dependency,
third-party artwork or raster image disguised as a vector is used.

Keep accepted Nordic silhouettes; add independently editable sources for move
tip, plane badge, rotation endpoint, scale badge and source/target marks. Use SVG
for contours/engraving and separate generated transparent texture assets for
metal grain where useful. A generated PNG is not a vector. Embed exported assets;
do not load concepts from external paths at runtime. Draw UV/materials on existing
owned geometry and dispose them on teardown. World/F9 art is out of this revision.

Default handles are dim, smoothly brighten by screen-space distance to their
actual shape, then strongly emphasize the winning hovered/dragged control.
No glow over every control and no dependence on world distance. Axis identity
remains readable. Replace scale's tiny open strokes with a filled, engraved badge
about 35 logical pixels across, offset away from crowded rings. Its entire visible
silhouette is clickable with small padding; overlap resolution is deterministic.
Check engraving at actual small size, not just a large concept image.

Snap points use plain shapes: gold native diamond, smaller blue corner circle and
green midpoint cross, pink centre, purple pin accent, white active helper. A native
pin retains its gold rim with a compact purple accent inside. Coordinates remain
unchanged; no decluttering hides or offsets points. The bottom legend names all
colours. Game mode targets native sockets even inside their own mesh; other pieces
still occlude. Furniture without natives retains helper fallback.

World blueprint description refreshes with language and displays summed resources
and deduplicated required stations. HUD text only: marker requirements stay empty,
preserving native per-child checks and consumption. Layout and actual costs still
require owner game acceptance.

## Required regressions

Reuse actual Controller.Update/LateUpdate and native EventSystem Workbench checks;
do not test a disconnected imitation of the proposed state machine.

| Case | Gesture | Required result |
| --- | --- | --- |
| Visibility | Select in tree/viewport; G; Esc | Original material and immediate handles; G previews; Esc cancels and retains selection |
| Precision baseline | R X; mouse turn; type 30; Enter | Exactly 30 degrees from original; one Undo restores exact pose |
| Move and scale | Axis Move; mouse; type -2,5; S then 1.5 | Original reference, culture/keypad parity, uniform bounded scale |
| Input parser | Empty, minus, comma, backspace, overflow, NaN-like text | No accidental confirm, NaN, deletion or document mutation |
| Constraint | Repeated XYZ, excluded plane, axes near camera-parallel | Visible frame, deterministic baseline, invalid ray cannot confirm |
| Ownership | R to S to Move; Ctrl+1–4 during previews | Exact rollback of abandoned action; no old mode continues |
| History | Esc, Ctrl+Z, redo, zero-delta confirm | Cancel no history; one Undo per edit; zero delta no edit |
| Camera | MMB/RMB flight and wheel during mouse/typed preview | Pause/resume no jump; typing reference retained; no deletion |
| MMB click | Click eligible part in Select/adding, then Undo | One part removed once; ghost retained; one Undo |
| MMB drag | Start on part/empty/UI; exceed threshold and return | Camera only, never deletion; UI does not click through |
| UI and focus | Fields/search/menu, outside release, focus loss, reopen | Correct input owner; no stale gesture/value/preview |
| Groups and locks | Nested selection, primary/group anchors, locked part | Correct pivot, stable IDs, no duplicate transform or lock bypass |
| Visual picking | Combined controls overlapping, filled scale, zoom and UI scale | Winning hover matches click; reachable scale; no invisible interception |
| Renderer | Idle/near/hover/drag/occluded, EN/RU, 100/120/140% | Smooth dimming, readable detail/text, correct cleanup |
| Modifier tools | Array/Contour apply/cancel then switch, including numeric input | Existing math unchanged, no orphan guides/copies |
| Host boundary | Reopen editor; ordinary world/F9 input and save/reload | No world-input or Store migration regression |

The matrix is a regression contract, not a claim that every variation is tested.
ControllerInputAcceptance drives actual Update/LateUpdate for combined visibility,
numeric baselines, invalid input, axis switching, rollback, tool switching,
history, camera pause and MMB click/drag. RuntimeEditorAcceptance and
EditorInteractionAcceptance check texture presence, proximity/hover and filled
scale picking with installed native assets. See the focused owner game series
in current-pass.md for host-specific behavior and visual acceptance.
