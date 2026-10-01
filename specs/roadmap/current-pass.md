# BuildWorks current game tests

**English** | [Русский](current-pass_RU.md)

Test **0.19.45**, only in **TerrainRamp-1.0-Test**. This is the single current
answer list: report results for **1–12 below**. Old scenarios are
[archived separately](testing-history.md); do not answer their numbers.

Automated Unity checks do not replace these Valheim checks. Installation requires
Valheim closed; the game is not launched automatically.

## Prepare the test

Open a disposable world. Equip the Hammer → open BuildWorks → Blueprints →
select a copy of a blueprint containing at least two adjacent wooden walls →
Edit. If needed, add walls through Tab and LMB. Do not use an important blueprint.
For keyboard commands, place the pointer over the viewport and leave text fields.
Before each new test, finish/cancel the previous action and select one wall again.

## Tests to answer

1. **Recognizable selection.** Press Ctrl+1 for Select. Click one wall row in the
   object tree, then move the pointer away from the model.
   **Expected:** every wall keeps its original material, including the selected
   wall. Its tree row is highlighted and all gizmo handles appear immediately,
   without G. Click a different wall: the tree highlight and gizmo move to it.

2. **Vanilla versus geometry points.** With a wooden wall selected, press Ctrl+2
   to reset to the combined gizmo; set Points to All in Tool parameters.
   Inspect the points while moving the camera closer/further.
   **Expected:** gold = vanilla sockets (plain large diamonds); blue = bounds
   corners (small circles); green = edge midpoints (small crosses); pink = bounds
   centre; purple = pinned point; white = active helper. No point is hidden or
   moved to avoid overlap. Coincident helpers fit inside the gold diamond.
   The bottom line explains every colour. Arrows/scale retain their engraving.

3. **Hover feedback and scale grip.** Keep the wall selected. Without pressing
   a mouse button, approach an arrow, then a rotation ring, then the filled scale
   badge outside the rings. Finally drag the badge with LMB and press Esc.
   **Expected:** approaching brightens each control; hovering clearly identifies
   the winning handle. Hover alone never transforms the wall. Dragging the badge
   changes scale; Esc restores the original scale. No size comparison is needed.

4. **Exact rotation from the start.** Select one wall. Press R and move the
   mouse until it turns visibly. Press X without moving the mouse.
   **Expected now:** the wall returns to its pose before R. Move the mouse again,
   then type 30 and press Enter.
   **Expected:** rotation is exactly 30° from its pose before R, not 30° added
   to the mouse preview. Ctrl+Z restores the original pose; Ctrl+Y restores the
   30° result. Press Ctrl+Z once more before the next test.

5. **Exact axis movement.** Select the same wall. Press X, move the mouse, type
   -2.5, press Enter.
   **Expected:** movement is -2.5 m along the displayed X axis from the starting
   pose. Ctrl+Z restores it exactly. The numeric position fields agree with the
   movement; typing digits does not switch tools.

6. **Exact scale and invalid input.** Select the wall. Press S, move the mouse,
   type 1.5, press Enter; then Ctrl+Z. Start S again, type 0 and press Enter.
   **Expected:** the first result is original scale ×1.5, undone in one step.
   Zero is visibly invalid and does not commit. Esc cancels it; the original
   wall and its selected gizmo remain.

7. **Immediate Q/E and native snapping.** In Tool parameters set Magnet to Game.
   Select a wall, press G and aim near a gold socket on the other wall. Keep the
   mouse stationary and press Q, then E. Turn the wheel; press Esc. Repeat with
   Magnet set to Mesh, then confirm with LMB and undo with Ctrl+Z.
   **Expected:** Q/E changes the source point and repositions the preview in that
   same frame, without moving the mouse. Game targets native sockets, including
   sockets inside their own mesh; Mesh also offers generated helpers. Nearby
   targets appear; wheel changes yaw. Esc/Undo restore the original, no copy.

8. **MMB click removes only a placed part.** Press Ctrl+1. Click/release MMB on
   a placed wall; Ctrl+Z. Then Tab → choose a wall → return to the viewport
   without placing the ghost. Click/release MMB on a placed wall again; Ctrl+Z.
   **Expected:** each click removes only its placed target. Undo restores it.
   The unplaced catalog ghost remains during the second deletion. Esc ends adding.

9. **MMB camera does not remove.** Press Ctrl+1. Start an MMB drag on a placed
   wall, move the pointer noticeably away and back before releasing.
   Then try Shift+MMB.
   **Expected:** orbit/pan only; returning to the starting point cannot turn a
   drag into deletion. No part disappears and no transform is confirmed.

10. **Tool switching cancels the previous action.** Select a wall, press S and
    type 1.5, but do not confirm. Press Ctrl+3 for Array; then Ctrl+4 for Contour,
    Ctrl+1 for Select, Ctrl+2 for Gizmo.
    **Expected:** the unconfirmed scale is discarded. Each tool opens its own
    parameters; no abandoned copies, contour lines or old transforms remain.
    Selection/gizmo stay usable without closing the editor.

11. **Fields and menus own their input.** Open Space → Tools; type in its search,
    then close it. Click a numeric position field and type a number, without
    pressing Apply.
    **Expected:** typing does not start Rotate/Scale/Move, delete parts or change
    tools. The field changes only its text until Apply; leaving it does not start
    an old mouse gesture. Close the editor without saving this test change.

12. **English world description, resources and hints.** Save a test blueprint
    containing exactly two wooden walls. Check an ordinary wall's resource cost
    in the Hammer, then select the blueprint for world placement, without placing
    it. Switch the game language to English and select that same blueprint again.
    **Expected:** the bottom native description is English and lists the summed
    cost of both walls (e.g. 4 Wood if one wall costs 2), plus required stations.
    Your own saved blueprint name is not translated. Open Edit → select a wall:
    the colour legend is English; collapse Hints and reopen it. The action line
    remains while collapsed. Report a screenshot if description/hints are clipped.

## How to reply

Reply with one result per number: `1 — works`, `2 — problem: …`, through
`12`. For a problem, give the step number, keys/mouse gesture, active tool and
what happened instead; attach a screenshot if visual. Write “not tested” if you
did not run that step. No additional lettered series is requested.

Do not run multiplayer, other-profile or curve tests now. Curves still require
a separate discussion. Actual resource consumption, save/reload and network
behaviour are not proven by this series; step 12 checks display only.
