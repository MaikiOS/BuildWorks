# BuildWorks exact chair corner game checks

**English** | [Русский](current-pass_RU.md)

Test **0.19.53**, only in **TerrainRamp-1.0-Test**. Reply only to **1–2 below**.
The accepted .52 checks 1–2 are not repeated; [the old list](archive/pass-0.19.52.md)
is an archive. This revision uses exact mesh geometry in the editor by disabling
decorative native Piece vertex noise on preview materials only. Textures remain;
world pieces and their original materials are unchanged.

## Preparation

Edit a test blueprint copy. Add the wooden reclining chair **piece_chair02**
(the chair with the slanted wooden back from your screenshot) and a wall via Tab.
Select the chair → Ctrl+2. Enable **Snap: native + ours**, **All points** and
**Geometry points: on (experiment)** in Attachment. Keep **Size: in 3D**.

## Reply checks

1. **Dots coincide with visible chair corners.**
   Zoom into the top of the back, the seat and the feet; orbit the chair.
   **Expected:** the centre of each small yellow dot stays on a real sharp corner,
   not beside it. A translucent dot extends around its centre; judge the centre,
   not its outer radius. Large ivory knots are ordinary bounds helpers and may
   remain in empty space. They are not the dots being tested.
   **Report:** good, or a screenshot of an offset yellow centre.

2. **The same corner is the pivot and snap grip.**
   Click a small yellow seat/foot dot.
   **Expected:** the selected collar and gizmo pivot centre on that dot.
   Press R, choose an axis and move the mouse; Esc restores the pose.
   Press G and aim at an ordinary ivory point on the neighbouring wall.
   **Expected:** the selected corner joins that point; Esc restores the pose.
   **Report:** good, or name the failed action and include a screenshot.

## Reply format

“1 — good; 2 — good” or “1 — back corner offset, screenshot; 2 — good”.
No other answer list applies to this pass. Valheim appearance and interaction
still need this owner check; Workbench verification is separate.
