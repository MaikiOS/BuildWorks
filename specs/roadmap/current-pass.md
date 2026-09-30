# Step by step BuildWorks game checks

**English** | [Русский](current-pass_RU.md)

Updated 2026-09-30 from Ostrix's feedback. **TerrainRamp-1.0-Test only**,
BuildWorks **0.19.41 candidate**, Valheim 1.0.16. Do not test the old DLL as this candidate.
Install only with Valheim closed; Valheim is not launched automatically.
Start with the short series below. Run the rest in separate sessions, not all at once.
Use a disposable world and blueprint copy, not an important build.

## Results already reported

- Passed by owner report: held-RMB menu, furniture Q/E, nearby snap markers,
  below-grid placement, group pivots, Array, two-part save/reopen, world reload,
  camera through ground, and editor English. Do not repeat these wholesale.
- 0.19.39 owner report accepts menu hover, native tab language refresh and F framing.
  Selection/box behavior is provisionally accepted; report exact steps if it recurs.
- Owner accepts contour hover/cleanup and shortcut availability. The approved
  0.19.41 editor improvements below need game acceptance. Category chooser,
  resource panel and rebinding still require their separate UI gates.
- Esc removes the partial build and restores its ghost, but refunds drop on the
  ground. Count-based accounting and multiplayer remain pending.
- One-piece save is currently disallowed; two pieces save correctly by report.

## Next focused checks after installing 0.19.41

1. **Hover without copies:** edit a disposable blueprint containing three touching
   wooden walls and a separate pole. Stop adding, select the pole and press C over
   the viewport. Hover near a wall's top edge without clicking: a blue guide should
   appear, but no copies. Move onto empty space: the hover guide should disappear.
   Move along the same edge first: the guide must stay on full edge endpoints,
   not slide with the cursor. Different edges can deliberately select a different chain.
2. **Clear the contour:** click that edge to preview copies, then click `CANCEL`.
   The guide and copies must disappear. Press G and select another part; no old
   line should return. Repeat starting with C and a click, but cancel with Esc,
   or switch directly with G/Q, or delete the selected source with Delete.
3. **Readable keys:** select a part. At the bottom of the viewport, find six blocks:
   Modes, Current tool, Selection/groups, History/save, Visibility and Camera.
   Within those blocks, find mode keys
   Q/G/A/C, Ctrl+G, Ctrl+D, history, visibility and camera controls. Switch through
   G, A and C: tool actions must change without losing the common mode keys.
   Open the catalog with Tab and type: hints must describe catalog/text input,
   not commands that would interfere with typing. Check Russian and English.

4. **See-through:** place a wall in front of another part. Select the rear part
   in the Outliner, press F7 or click `See-through F7` at the viewport's top right.
   The front wall disappears completely, the selected part stays solid and can
   be picked through it. Orbit around it: parts no longer blocking the selection
   return. Toggle off: all temporary hiding ends. Clear selection or select the
   front wall in the tree: it must return. Manually hidden parts stay hidden. Repeat F7
   outside the editor only to verify BuildWorks does not handle it there; another
   installed mod may own that key in the world.

5. **Camera and tree:** open Settings in the editor header. Change Field of view
   with its slider and degree input; As in game must restore the game camera's
   value. Projection toggle at the viewport disables these controls in orthographic
   mode. Close the editor: the world camera must be unchanged. Reopen a disposable
   blueprint with more parts than its tree can fit: scroll by wheel and drag the
   visible right scrollbar. Rows must stay below Search, names and anchor badges
   must not cover the eye/lock columns, and scrolling must not change selection.

Double-click framing and shortcut rebinding are not shipped.

The full scenarios below are a reference, not a request to repeat passed checks.

## Entering each mode

**Our Hammer:** equip the Hammer in the world and press RMB to open the catalog.
The indexed BuildWorks catalog should open. If the native catalog appears,
click its `BUILDWORKS` button. If that button is missing, stop and report it.

**Blueprint Editor:** in our Hammer open `BLUEPRINTS`, single-click a disposable
blueprint card, then click `EDIT` on the right. You should see the grid, a tool
rail on the left, and the Outliner on the right.

**Adding in the editor:** move the pointer over the grid and press Tab.
Choose a piece in the catalog. Return the pointer to the grid: a placement ghost
should follow it. Press Q/E at this point in the following checks, not with
the catalog open or RMB held. Esc cancels adding.

**Ordinary world placement:** choose a piece in the Hammer and close the catalog.
A ghost moves in front of the character. F9 is off.

**World F9:** first aim the ordinary ghost at its intended location, then press
F9. The precision panel and arrows/rings appear. This is a separate mode:
an ordinary arrow moves along one axis at the configured step. That behavior
alone does not demonstrate broken snapping.

## Earlier reproduction scenarios

### 1b Blueprint card menu

Where: our Hammer → `BLUEPRINTS`. Not the editor Outliner.

1. Point at the disposable blueprint's image, not its `⋯` button.
2. Press and hold RMB. A menu of actions for that blueprint should appear.
3. Keep RMB held, move onto `RESOURCES`, then release RMB.
4. Repeat on the same, already selected card.

Expected: pressing does not close the catalog; moving does not dismiss the menu;
releasing performs the selected action. If it disappears, report whether it was
step 2 or 3 and whether the card was already selected.

**Do not repeat as a test:** missing MMB favorites on blueprint cards is confirmed
by the owner report and code. The favorites handler currently covers ordinary
pieces only. This is a UI gap, not an accepted blueprint feature.
The `⋯` button currently selects the card and displays its actions on the right;
it does not open a popup. The mismatch with the expected behavior is recorded.

### 3b Furniture attachment in the editor

Where: Blueprint Editor → adding a piece. Not world F9.

1. Open the disposable blueprint in the editor. Press Tab and choose an ordinary
   wooden chair. If unavailable, choose a table and report its exact name.
2. Aim the ghost at empty grid space without placing it. Release Shift, Ctrl and RMB.
3. Press E once, then several more times with pauses. After each press watch
   the bottom label and the point by which the pointer holds the furniture.
4. Press Q: the previous point should return. Capture a screenshot if nothing responds.
5. Press Esc. Choose a wooden wall in the same way and press E for comparison.

Expected: the editor provides auxiliary bounds-based points for furniture without
native snap points. Both the label and furniture position relative to the pointer
change. This does not promise native furniture snap points in the vanilla world.
Report the furniture name, whether the label changes, whether the held point
changes, and whether the wall comparison works. These observations replace a
single ambiguous “does not work.”

### 4a Target preview and snapping in the editor

Where: editor → adding a piece. **Do not press F9 for this check.**

1. Press Tab, choose a wooden wall, and place one on the grid with LMB.
2. Press Esc to stop adding that wall. Press Tab and choose the wall again.
3. Aim the second ghost near the first without placing it. Release Shift and RMB.
4. Slowly approach the placed wall's edge. Available target points should become
   visible first; approaching further should produce a connection.
5. Press Q/E several times and watch the bottom label change. Check whether
   the source point attaching the second ghost to the first changes too.
6. Move the ghost away and press Esc.

Expected: target markers appear before magnetic capture, not only after attachment.
The configured preview radius is 2 m and capture radius is 0.55 m. Do not estimate
centimeters by eye: report whether points appear in advance, attachment occurs,
and the Q/E label changes. Visible markers without attachment are a separate result.

## Next session after the short series is reviewed

### 2b Text entry without accidental commands

Where: editor → Outliner. RMB on parts/groups is already checked.

1. Select one piece. Hold RMB on its row, move onto `RENAME`, and release.
2. Enter `QEGAC test` in the name field. Confirm using the dialog's button.

Expected: typing does not activate tools, the catalog, or camera movement;
the piece stays still. Only its name changes.

### 4b Placement below another piece

Where: editor → adding, with transformation used to prepare the target.

1. Place a wooden floor. Press Esc, select it in the Outliner, and press G.
2. Raise it approximately one meter with the green arrow. Release the mouse.
3. Press Tab and choose a wooden pole. Orbit with MMB until you can see the
   raised floor's underside, then release MMB.
4. Approach a bottom target of the floor with the pole ghost. Use Q/E to select
   the pole's top. Try attaching its top beneath the floor.
5. If it attaches, place with LMB and compare with the ghost before the click.

Expected: the lower target is not rejected simply because of the grid; placement
matches the ghost. Separately report if the grid itself follows the raised floor:
that is a working-plane issue, not proof that snapping succeeded.

### 5 Blueprint anchor and two group pivots

Where: editor, then ordinary blueprint placement in the world.

1. Place four walls in a disposable blueprint: two left and two right.
2. Select the two left rows with Ctrl+LMB in the Outliner and press Ctrl+G.
   This is the first group. Group the two right walls in the same way.
3. Expand both groups. Hold RMB on the first left wall and choose `BLUEPRINT FRAME`.
   Through the same menu set it as the first group's `GROUP PIVOT`.
4. Set one right wall as the second group's `GROUP PIVOT` only.
5. Select both groups with Ctrl+LMB in the Outliner and note the gizmo location.
   Then select all pieces with a drag rectangle in the viewport and compare.
6. Save. Return to the world, select that card in `BLUEPRINTS`, and click `PLACE`
   on the right. Without F9, approach an existing world wall with the left anchor wall.

Expected: equivalent full selections do not move the gizmo onto the second group's
pivot; the designated blueprint anchor determines world attachment.

### 6a Array in the editor

Where: editor. This is not the world F9 Array test.

1. Place one wooden wall, press Esc, and select it in the Outliner.
2. Press A over the viewport. Array parameters should appear on the right.
3. Set `IN ROW = 2`, `ROWS = 1`, `PACK`, `NO GAP`; leave rise, turn, pitch,
   roll and scale step at 0. Do not enable symmetry.
4. Pull one golden arrow horizontally outward and release LMB. Keep the pointer
   over the viewport and, without Ctrl, scroll one step forward and one backward.
   Watch the row before applying.
5. Click `APPLY`. Compare the result with the last preview.
6. Press Ctrl+Z once, then Ctrl+Y once.

Expected: the packed row extends the original wall; scrolling changes the count
rather than filling a stretched interval using a different distribution.
Apply matches preview; one Undo removes the entire Array and Redo restores it.

### 6b Contour along a simple chain

Where: editor, after Array works. Not the future curve tool.

1. Use Array to make and apply a straight row of three identical walls without gaps.
2. Add a separate wooden pole. Press Esc and select only that pole.
3. Press C. Aim at the top edge of one of the three walls and click LMB.
   Keep the walls unselected: they define the chain; the pole repeats along it.
4. If a preview appears, click `APPLY`, then Ctrl+Z. If a message appears instead,
   copy its exact wording.

Expected: a chain of at least two supports is found and copies follow its points;
Apply matches preview and one Undo cancels it. A complex closed contour does not
have to be constructed manually for this check.

### 7 Save and reopen the editor

Where: the disposable blueprint editor from check 5.

1. Select a wall, press G, move it slightly with an arrow and rotate it with a ring.
   Record the displayed position/rotation/scale and capture the Outliner.
2. Click `SAVE`, then `EXIT`. Reopen the same blueprint through the Hammer's `EDIT` button.
3. Select the same wall and compare values, group membership and anchor markers.

Expected: nothing disappears or shifts on reopening.

### 8 Esc during composite placement

Where: ordinary world placement, in empty space in a disposable world only.

1. Choose a disposable blueprint large enough that placement does not complete
   instantly. Note resource counts beforehand if costs are enabled.
2. Our Hammer → `BLUEPRINTS` → card → `PLACE`.
3. Click LMB to place. Press Esc while pieces are still appearing.
4. Close the game menu and inspect the site. Compare resources and the active ghost.

Expected: cancellation leaves no partial build; resources are neither lost nor
refunded twice; the same blueprint ghost returns. If placement completed before
Esc, report “could not catch cancellation,” not FAIL.

### 9 World save after precision F9 placement

Where: world. F9 is needed here; this checks transforms and persistence, not
the target-preview radius from 4a.

1. Choose a wall and approach an existing wall using ordinary placement.
2. Press F9. Move slightly with one arrow and rotate with one ring.
3. Click `PLACE` on the BuildWorks panel. Inspect the placed wall and capture it.
4. Save and exit the world normally. Reenter the same disposable world with the
   same character and compare the wall with the screenshot.

Expected: position and rotation persist. Axis-constrained arrow movement is normal
here; this check does not promise vanilla-style free pointer movement in F9.

### 10 Camera near the ground

Where: world, all catalogs and the editor closed, Hammer equipped.

1. Press B for the installed camera mod. Test mouse look and W.
2. Fly low, aim slightly downward, and hold Shift+W.
3. Repeat looking higher. Press B to exit.
4. Open the Blueprint Editor and exit; check normal character camera controls again.

Expected: no stuck input or unexplained abrupt stop as the ground approaches;
camera control returns after the editor. If movement stops, report whether there
was visible ground contact and whether raising the view changes the result.

### 11 Russian and English UI

Where: Valheim's normal language settings, then our Hammer and editor.

1. In Russian, open `BLUEPRINTS`, then the editor. Check cards, the right panel,
   Outliner and footer while adding a piece and pressing Q/E.
2. Save the disposable blueprint and exit the editor. Change the game language
   to English normally and reopen the same windows.

Expected: labels are translated; no `[buildworks_...]` tokens or clipped commands.
Owner-created blueprint/group names do not have to translate automatically.

### 12 Multiplayer in a separate session

**Do not run now without a second participant and separate agreement on the series.**
Both clients need matching mod versions in the dedicated test profile.

1. The host opens a disposable world and the second player connects.
2. The host places a test blueprint. Both compare piece count, position and rotation.
3. The second player reconnects; compare again.
4. The host saves and restarts the test server/world; both compare again.
5. Check removal of one disposable piece with authorized permissions and resource
   costs for placement. Do not bypass ownership restrictions.

Expected: both see the same construction before/after reconnect/restart; operations
respect resources and permissions. Without a second client, record “not tested.”

## Reporting results

For now report **1–5 from the short 0.19.41 series above**, not the historical
scenarios. For example: `1 — guide stays fixed; 2 — cancellation clears it;
3 — hints readable; 4 — occluder disappears; 5 — FOV and tree scrolling work`.
If a step is impossible, give its number and what appears instead; do not guess
where the tool is hidden. Do not repeat the remaining scenarios yet.
Read profile logs locally; do not publish personal data. BW-00 stays open;
findings cannot be closed by a build alone. Curves still require discussion.
