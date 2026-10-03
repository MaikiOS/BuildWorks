# BuildWorks mesh corners and view depth game checks

**English** | [Русский](current-pass_RU.md)

Test **0.19.51**, only in **TerrainRamp-1.0-Test**.
Reply only to **1–6 below**. The .50 series is [archived](archive/pass-0.19.50.md).
The owner accepted sizing, selected pivot artwork, transforms, optional B and
localization in .50. This pass rechecks mesh corners, magnetic capture and depth.
Automated checks do not constitute Valheim acceptance.

## Preparation

Hammer → BuildWorks → Blueprints → edit a copy of the test blueprint.
Add two walls, two chairs and a slanted beam via Tab. Before each check, Esc cancels
a pending operation; select the specified part in the tree and press Ctrl+2.
Keep the pointer over the viewport, not a text field. Enable **Snap: native + ours**;
leave **Geometry points: off (experiment)** in Attachment. Keep **Size: in 3D**.

## Reply checks

1. **Furniture corners without experiment.**
   Select a chair → **All points**. Orbit and zoom around legs, seat and back.
   **Expected:** ivory helpers lie on actual component mesh corners, not corners
   of an empty enclosing box. No fabricated edge middles. Corners of overlapping
   components can be inside the model. Report empty-space helpers or missing
   important leg corners with a screenshot and the part name.

2. **Slanted beam and experiment.**
   Select the beam → All points; inspect both ends. In Attachment enable Geometry
   points; inspect the beam and chair again, then disable it.
   **Expected:** ordinary and detailed helpers share one ivory sign, all at real
   mesh corners. The experiment adds shallower edge turns, not middles or new
   coordinates; simple meshes may have the same set. Native amber sockets retain
   their prefab coordinates, not mesh-derived positions. Restore Near cursor.

3. **Aimed helper capture and adjacent switching.**
   Select chair one → click an ivory leg point (A) → G. Approach chair two and
   aim directly at one displayed target, then an adjacent one. Enter; one Ctrl+Z.
   Repeat and cancel with Esc.
   **Expected:** A exactly joins the aimed target; the next target takes over
   without old capture trapping it. Targets reveal within 2 m of source grips.
   Direct pointer aim can capture inside that radius; unaimed proximity remains
   0.55 m. The target never replaces pivot A. Cancel leaves no transform.

4. **Native-only filter.**
   Wall → G → **Snap: native**. Aim at an amber target on wall two, then an ivory
   helper. Esc. Restore Native+ours and repeat G.
   **Expected:** native-only allows amber targets, not ivory; Native+ours allows
   both. Furniture without native sockets does not acquire invented native ones.
   An intentionally selected helper A can remain the grip, but target filtering
   still applies.

5. **New depth handle with view axes.**
   Select wall → Ctrl+2 → header Axes button → **Axes: view**. Find the small **Z**
   depth handle near the gizmo centre. Hold LMB and drag up, then down; release.
   One Ctrl+Z.
   **Expected:** up moves away from the camera, down towards it, without screen
   sideways motion. The footer explains the gesture. Release commits one action;
   one Undo restores it. Local/world axes hide this special handle.
   View X/Y still run along the screen.

6. **Exact depth and state cleanup.**
   View axes → G → Z → move mouse up → type **1** → Enter; one Ctrl+Z.
   Repeat G → Z → mouse motion → Esc. Then local axes → R → X → 30 → Enter;
   one Ctrl+Z. Save a blueprint copy, exit and reopen.
   **Expected:** 1 is exactly one metre from the operation start away from camera,
   not added to the mouse preview. Esc fully cancels. Ordinary rotation works after
   switching; depth mode does not leak. Saved parts and poses remain intact.

## Reply format

Example: “1 OK; 2 stray leg point, screenshot; 3 cannot switch adjacent target;
4–6 OK.” For failures include part name, source filter, experiment on/off, axes
and exact input sequence.
