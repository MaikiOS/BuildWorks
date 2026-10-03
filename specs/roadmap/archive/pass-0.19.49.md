# BuildWorks point and gizmo game tests

**English** | [Русский](current-pass_RU.md)

Test **0.19.49**, only in **TerrainRamp-1.0-Test**. Reply to the numbered
tests below, not the [archived .48 list](archive/pass-0.19.48.md).
Workbench checks pass; actual Valheim readability and physical input remain unverified.
First inspect **1–3**. If unreadable, send a screenshot before continuing.

## Preparation

Open a copy of a disposable blueprint: Hammer → BuildWorks → Blueprints → Edit.
Include two ordinary wooden walls, a sloped beam and a chair; add through Tab.
Before each editor test: Esc to cancel pending actions, select the named part
in the tree, Ctrl+2 for the gizmo, pointer over the viewport, no focused text field.
A is a temporary transform pivot, not the saved world/group anchor.
B defines the direction A→B for working axis X; this is not a curve tool or
edge-to-edge alignment. Both size modes change controls, never the piece itself.

## Tests to answer

1. **Fixed screen size.**
   Start in the editor with one wall selected. Above the viewport open
   Manipulator → select **Size: on screen**. Close the menu; zoom in and out
   using Ctrl+wheel over the viewport.
   **Expected:** arrows, rings and point badges retain approximately the same
   pixel size. The wall changes apparent size but its scale value stays unchanged.
   Click a visible point: the clickable area agrees with its drawing.

2. **Size in 3D.**
   Same wall → Manipulator → select **Size: in 3D**. Close the menu; zoom in/out.
   In Camera switch perspective/orthographic and change orthographic zoom.
   Return to perspective and change FOV; reset it to the game value.
   **Expected:** controls have a fixed model-relative size in 3D, appear larger
   nearby and smaller farther away. No camera-dependent inflation. Picking follows
   the drawn size. Adjust point/handle size in Manipulator if desired; the piece's
   own scale never changes from these settings. Switch back to screen mode and
   verify test 1's behavior returns.

3. **Two point types and reveal.**
   Use **Native + ours** and **Near cursor** above the viewport. Select a wall,
   then a sloped beam. Move the pointer toward/away from their point locations.
   Toggle **Near cursor** to **All points**, then toggle **Native + ours** to
   **Native only**. Return to Native + ours / Near cursor.
   **Expected:** gold = native sockets; blue = all generated helpers, no separate
   corner/midpoint/centre colours. Default points appear near the pointer.
   All points shows the selected model's full set; Native only restricts it.
   Occluded unselected points are dimmer. Signs stay at true coordinates.
   At exact overlap the helper is inside the native rim, not displaced.

4. **Choose A and rotate from the original pose.**
   Wall → click a point with LMB. Move the pointer away; R, move the mouse,
   then X, move again, type 30, Enter. Press Ctrl+Z once.
   **Expected:** the chosen point stays visibly selected with its type preserved;
   the gizmo moves to A. R rotates around A. Choosing X discards the earlier free
   rotation; 30 replaces the mouse preview with exactly 30° from the starting
   pose. A stays fixed; one Undo restores the whole action. No wall tint.

5. **Scale around A.**
   Select a wall, choose an off-centre point A → S; move the mouse, type 1.5,
   Enter; Ctrl+Z once. Repeat S, move, then Esc.
   **Expected:** 1.5 means 150% of the action-start scale, not an additional
   multiplier on the mouse result. A stays fixed. One Undo restores it; Esc
   cancels without recording an edit. Try the visible engraved scale badge:
   its filled centre is clickable, not only its border.

6. **Optional A→B working frame.**
   Wall → click point A → **Choose B** above the viewport → click a different
   point B. Press R → X; move the mouse → Esc. Repeat R → X → X → X → Esc.
   Then click **Clear A → B**. Use the Axes header button to cycle Local/World/View.
   **Expected:** line A→B is stable; A remains pivot, X follows A→B.
   Repeated X switches Edge → World → Edge with matching header and motion.
   Esc restores the prior frame. Clear A → B removes its line and returns Local.
   Choosing A again as B is rejected. View uses camera directions; this does not
   automatically rotate or connect edges of two models.

7. **Move point to point.**
   Wall → click its native gold point A → G. Approach another wall within about
   2 m; aim near a native target, then Enter. Undo once. Repeat G and cancel Esc.
   **Expected:** nearby target points appear before magnetic capture. A is the
   source grip and lands on the chosen target; target does not replace pivot A.
   The piece follows scene surfaces like the placement ghost. Enter records one
   edit; Esc restores the starting pose. Check both size modes and note the mode
   if a target is unclear or difficult to pick.

8. **Auto and source filters.**
   Select the sloped beam (selection replacement clears A) → G → Attachment →
   Auto; keep Additional points off. Stop the mouse and press Q/E.
   Enable Additional points, then select Native only in the header; try Q/E again.
   Esc. Select a chair → G with Native + ours; open Attachment.
   **Expected:** default cycle is Auto/native points only and updates immediately
   at a stationary cursor. Native only also removes helpers from source/target
   choices; a deliberately selected helper A remains visible and usable.
   Furniture without native sockets offers helpers in Native + ours; Native only
   intentionally does not invent sockets. Menu clicks never confirm a transform.

9. **Blueprint resource cards in the world.**
   Save a separate blueprint of exactly two ordinary walls, exit the editor.
   With Hammer choose an ordinary wall, then the blueprint without placing it.
   Switch English/Russian and reselect. If available, also inspect a blueprint
   using several resource types; scroll its long card list with the Hammer menu open.
   **Expected:** thumbnail/name/short description above native resource icons,
   summed quantities and deduplicated station icons. Typically Wood 4 for two
   walls costing 2 each. No text/card overlap; translated labels refresh.
   Your own blueprint name is not translated. Report long-list coverage separately.

10. **Cleanup and language.**
    After blueprint preview select a wall, Repair, unequip/re-equip Hammer.
    Reopen the editor, switch English/Russian, choose A/B, start G and Esc;
    select a different part and switch Ctrl+1 / Ctrl+2 / Ctrl+3.
    **Expected:** ordinary HUD restored; no extra cards/scrollbar. New header,
    hints and menus translate. Selection replacement clears A/B and its guide.
    Tool switching cancels pending transforms. No hanging operation, unintended
    deletion or confirmation.

## Reply format

One result per number: `1 — works`, `2 — problem: …`, through `10`.
Include the number, screen/3D mode, active tool, exact gesture and screenshot
for visual defects. Use “not tested” where needed. No lettered extra series.

This is editor/input/display acceptance, not resource consumption/refunds,
world save/reload or multiplayer proof. No other profiles or curve tests.
