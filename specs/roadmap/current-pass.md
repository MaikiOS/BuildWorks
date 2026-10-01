# BuildWorks current game tests

**English** | [Русский](current-pass_RU.md)

Test **0.19.44**, only in **TerrainRamp-1.0-Test**. This is the single current
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
   **Expected:** that wall stays gold-tinted; the others retain their original
   appearance. All default gizmo handles appear immediately, without G. Click a
   different wall in the viewport: its highlight and gizmo replace the first.

2. **Vanilla versus geometry points.** With a wooden wall selected, press Ctrl+2
   to reset to the combined gizmo; set Points to All in Tool parameters.
   Inspect the points while moving the camera closer/further.
   **Expected:** vanilla points are larger, brighter, closed gold diamonds with
   engraving. Geometry/bounds/midpoint helpers are smaller open coloured marks.
   A pinned point remains purple. The bottom action line explains both kinds.

3. **Readable larger gizmo.** Keep the same wall selected. Move the pointer away
   from every handle, approach an arrow/ring, then hover the scale badge.
   **Expected:** the gizmo is larger than .43; idle handles remain readable,
   proximity gradually brightens them, and only the winning control is strongly
   highlighted. Scale can be dragged from its filled centre. Esc cancels a drag.

4. **Exact rotation from the start.** Select one wall. Press R, then X, move the
   mouse to turn it, type 30, press Enter.
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

7. **Move an existing part like a ghost.** Select one wall and press G once.
   Move the pointer onto another wall, press Q/E and turn the wheel; press Esc.
   **Expected:** G starts surface-move preview immediately. Q/E changes the source
   point, wheel changes yaw, nearby target points are shown. Esc restores the
   original pose with no extra piece. Repeat G and confirm with LMB; Ctrl+Z
   restores the original in one step.

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

12. **English and compact hints.** Switch the game language to English normally.
    Reopen the test blueprint → Edit → select a wall. Open Manipulator and inspect
    the bottom hints; collapse Hints and reopen it.
    **Expected:** labels and the vanilla/geometry legend are English, with no raw
    localization keys. The action line remains when hints are collapsed. At the
    UI scale you normally use, labels/tooltips fit their panels.

## How to reply

Reply with one result per number: `1 — works`, `2 — problem: …`, through
`12`. For a problem, give the step number, keys/mouse gesture, active tool and
what happened instead; attach a screenshot if visual. Write “not tested” if you
did not run that step. No additional lettered series is requested.

Do not run multiplayer, other-profile or curve tests now. Curves still require
a separate discussion. Save/reload, resources and network proof are not implied
by passing this editor series.
