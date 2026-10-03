# BuildWorks current game tests

**English** | [Русский](current-pass_RU.md)

Test **0.19.48**, only in **TerrainRamp-1.0-Test**. For now reply only to **1**.
The [.45 series](archive/pass-0.19.45.md) is historical, not a request to repeat it.
Unity passed; actual Valheim acceptance is still required. This iteration changes
only point artwork/feedback. Inspect 1 at normal distance and closer; if unclear,
send a screenshot before running 2–8. Those numbers and controls are unchanged.

## Preparation

Use a disposable world and a copy of a blueprint. Hammer → BuildWorks →
Blueprints → Edit. Include two adjacent wooden walls; add them with Tab if needed.
Keep the pointer over the viewport and leave text fields before using shortcuts.
Cancel the previous action with Esc before each test. **Tests 5–8 start in the
world HUD; test 7 also opens the editor chooser. Do not place the blueprint.**

## Tests to answer

1. **Point signs.** Select a wall → Ctrl+2 → Tool parameters → Points: All.
   Inspect gold native clasps, blue corner brackets, green midpoint clamps and
   pink centres. Shift+LMB pins a point: purple stake. Ctrl+LMB a helper selects
   it for attachment: white brackets added to its coloured sign. Hover a marker:
   the highlighted bottom line names its type and the Shift/Ctrl actions.
   **Expected:** no wall tint or displaced/hidden point. At coincident positions,
   the helper is inside the native rim: centre and rim can be picked separately.
   The pin does not invent a native socket; original signs remain visible.
   Report whether types, exact centre and overlap are readable at normal viewing
   distance. Compare a wall and a sloped beam. Reply to this number only for now.

2. **G and short Q/E.** Select a sloped beam → G. Move over empty grid, then
   near another wall. Stop the mouse; press E until Auto returns, then Q.
   Turn the wheel; Esc.
   **Expected:** surface movement like a placement ghost; status identifies Auto
   or the native source. Default Q/E cycles only Auto/native points and updates
   immediately without moving the mouse. Wheel changes yaw; Esc restores the pose.

3. **Attachment chooser.** Select a wall → G → Attachment above the viewport.
   Hover, then choose a Native row. Enable Additional points and choose a Helper.
   Close by clicking outside; press R, then G. Move the pointer until the pose
   visibly changes; Enter; Ctrl+Z.
   **Expected:** hover identifies the real point; menu actions never confirm.
   Helpers are opt-in; the explicit source survives G → R → G. Enter commits once;
   one Undo restores the pose. UI clicks never start another transform.

4. **Auto and furniture.** Wall → G → Attachment → Auto; disable Additional
   points. Approach another wall within about 2 m, then near a gold socket; Esc.
   Add a chair through Tab, place it in the editor, select it → G → Attachment.
   **Expected:** nearby targets appear before capture; Auto prefers native pairs
   and holds through small movements. Furniture without native sockets offers
   labelled helpers; furniture with native sockets uses them. Esc restores.
   Name any furniture that offers no usable targets.

5. **Native cards.** Save exactly two ordinary wooden walls as a test blueprint.
   Close the editor. In the world Hammer, inspect one ordinary wall, then choose
   the blueprint without placing it.
   **Expected:** thumbnail/name/short description above resource icon/name/amount
   cards; summed Wood (normally 4 if each wall costs 2); workbench as a station
   icon. Not the old textual resource list.

6. **Totals, availability and overflow.** Use a separate test blueprint with
   different resources/stations; save and choose without placing. Compare totals
   with its component costs. For several rows, open the Hammer menu to free the
   cursor and scroll over the resource panel.
   **Expected:** resources summed, stations deduplicated, missing paid requirements
   indicated, availability updated. Cards widen/wrap inside the screen; overlong
   lists scroll. If types are insufficient, reply “long list not tested”; a huge
   blueprint is not required solely for this check.

7. **Language.** Switch to English; select an ordinary piece, then the blueprint.
   Edit → select a wall → G → Attachment. Return to Russian and repeat selection.
   **Expected:** cards/description/chooser refresh without restart. Your own
   blueprint name is not translated. Screenshot untranslated/clipped labels.

8. **Ordinary HUD restored.** After the blueprint preview choose a wall, then
   Repair. Unequip/re-equip Hammer and choose a wall. Reopen Edit; G; Esc.
   **Expected:** native panel/cards restored, no extra cells or hanging scrollbar;
   editor cancellation leaves no abandoned points/copies or confirmation.

## Reply format

One result per number: `1 — works`, `2 — problem: …`, through `8`.
For failures give the number, active tool, exact keys/mouse gesture and actual
result; screenshot visual issues. Write “not tested” where needed.
No lettered supplements or repetition of accepted .45 transform/camera tests.

This is display/input acceptance, not proof of resource consumption/refunds,
world save/reload or multiplayer. No other-profile or curve tests; curves require
a separate discussion.
