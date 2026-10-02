# BuildWorks snap point assets

**English** | [Русский](snap-point-assets_RU.md)

Six original raster PNGs generated through imagegen for .46 from the
[approved concept](../images/snap-point-style-proposal-v1.png).
Not vectors or game screenshots. Each is 1254 × 1254 with transparent background.
Alpha, decode, actual sprite registration and picking are checked in Unity.
Gameplay-distance readability still requires Ostrix's game feedback.

Directory: `src/BuildWorks/Assets/BlueprintEditorIcons/`.
Gold snap-native = native clasp; blue snap-corner = bounds corner;
green snap-midpoint = edge midpoint; pink snap-centre = centre;
purple snap-pin = Shift pivot; white snap-active = active source.
Coordinates stay exact. Native rim and helper centre have distinct hit areas;
signs are neither hidden nor separated on screen.

## Individual motif prompts

The following English prompts are retained from each individual generation.
Shared direction: original Nordic forged joinery, broad readable silhouette,
open attachment centre, restrained engraving and transparent background.
No game or other mod assets were copied.

### snap-native

`snap-native.png`

A gold interlocking timber-joint clasp: two opposed curved carved-metal jaw plates surrounding an OPEN transparent centre. Bronze-gold silhouette with ONE thick engraved interlace on each jaw. No disk fill.

### snap-corner

`snap-corner.png`

A BLUE notched forged right-angle carpentry bracket, like an L-shaped joint with a single broad engraved interlace and two large notches. Its inner hollow corner is the exact canvas centre. Keep most centre area open.

### snap-midpoint

`snap-midpoint.png`

A GREEN opposed pair of iron joint clamps, one above and one below the EXACT centre. Broad simple bevel and ONE engraved knot each. Open transparent central joint gap.

### snap-centre

`snap-centre.png`

A PINK carpenter compass rosette: four short forged pointed arms and a narrow engraved circular rim surrounding an open centre. Nordic joinery craft, not a generic cross.

### snap-pin

`snap-pin.png`

A PURPLE fixed pivot stake: narrow long forged Scandinavian pin with locking notch and a small open ring centred EXACTLY on canvas centre. Amethyst rim, sharp lower stake, two broad decorative grooves. Distinct silhouette.

### snap-active

`snap-active.png`

A WHITE luminous compact source hook: two opposing curved ivory-metal carved hooks around open exact centre. One broad Nordic interlace groove, bright pale bevel. Active attachment point, not arrow.

## Pin registration

The final snap-pin has an open outer locking ring around a filled amethyst
centre, with a stake notch and no full background disk. Its ring is not at the geometric PNG centre:
sprite pivot `(0.496, 0.634)` registers it exactly on the model; others use
`(0.5, 0.5)`. DrawArtwork honours the pivot without moving the actual point.
Further art edits require alpha, picking and small-size checks again.
