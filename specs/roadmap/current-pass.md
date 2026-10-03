# BuildWorks ordinary helpers and experimental corner game checks

**English** | [Русский](current-pass_RU.md)

Test **0.19.52**, only in **TerrainRamp-1.0-Test**.
Reply only to **1–3 below**. The .51 checks 3–6 are accepted: do not repeat the
full magnet/native-filter/view-depth series. [The .51 list](archive/pass-0.19.51.md)
is an archive, not additional questions. Automated tests do not prove real prefab
behavior in Valheim.

## Preparation

Hammer → BuildWorks → Blueprints → edit a test blueprint copy.
Add a chair, wall and slanted beam via Tab. Esc cancels any pending operation.
Select the specified part in the tree → Ctrl+2. Keep the pointer over the viewport.
Enable **Snap: native + ours**, **All points**; in Attachment start with
**Geometry points: off (experiment)**. Keep **Size: in 3D**.

## Reply checks

1. **Ordinary helpers without experiment.**
   Select wall, beam, then chair; orbit each.
   **Expected:** amber marks are native sockets; carved ivory marks are the previous
   bounds corners, edge middles and centre. They are not replaced by all mesh vertices.
   Chair bounds corners can be in empty space: these are ordinary bounds helpers,
   not experimental leg corners. Report missing or duplicated ordinary helpers.

2. **Small experimental mesh corners.**
   Enable Geometry points for the chair. Zoom around legs, seat and back, then
   inspect the beam. Toggle the experiment off and on.
   **Expected:** ordinary carved helpers stay unchanged. Extra real corners are
   small translucent warm dots without a large pattern; hover strengthens them,
   depth/occlusion weakens idle dots. Bends less than 45° from a straight line,
   corners buried in closed mesh components and overlaps within 5 mm of ordinary/
   native points get no extra mark. Off removes only the dots.
   Open/non-manifold components are filtered conservatively; report a buried
   corner with a screenshot and part name. Complex visuals have a 128-corner limit.

3. **Dot picking and target identity.**
   With experiment enabled click a small leg dot.
   **Expected:** a distinct carved selection collar appears; the gizmo moves to
   that pivot. Press G and aim this grip at an ordinary ivory target on another part.
   **Expected:** the grip exactly joins it, without turning the ordinary target
   into an experimental dot. Esc restores the pose. Turn experiment off, choose an
   ordinary helper and cancel G again. Restore **Near cursor**: idle points should
   not fill the viewport without pointer proximity.

## Reply format

Example: “1 — good; 2 — buried seat corner, screenshot; 3 — good.”
For a failure include part name, experiment on/off, point/snap mode and exact input.
There are no other answer lists in this pass.
