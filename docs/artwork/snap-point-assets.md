# BuildWorks snap point assets

**English** | [Русский](snap-point-assets_RU.md)

## Current point workflow

.50 keeps amber `snap-native`; all ordinary generated helpers use the new ivory
`snap-helper` joining knot. Selected A/source adds `snap-selected`, a carved
locking collar/rune, without replacing provenance. Opt-in geometry helpers use
a smaller amber `snap-geometry` knot, not the native circular clasp. No corner/
midpoint/centre colour classes remain in the editor. World/F9 art is unchanged.

Three original transparent 1254 × 1254 raster PNGs were generated individually
through imagegen. Geometry is an edit of the helper, not a new native sign.
All three register at (0.5,0.5). Neutral shader colour, mipmaps, trilinear filtering
and uncompressed Workbench import match the embedded decoder.
Model-relative 3D sizing is default; screen mode retains native/helper/geometry
quad radii of 16/9/5.5 pixels. Selected overlay radius is 1.3× native or 2.1× helper.
Near-cursor/depth feedback and the same projected hit geometry apply in both modes.
Rendered acceptance is separate from Valheim approval; no concept is runtime proof.

## Exact .50 generation prompts

The following are the actual generation/edit prompts, not reconstructed briefs.
### snap-helper

Use case: stylized-concept. Asset type: one production raster snap-point icon for BuildWorks Nordic construction editor, transparent square canvas, NOT a mockup or concept board. Create a compact pale IVORY forged carpenter's joining knot: four short chunky interwoven hooked lobes, softly rounded angular Viking carving, one broad dark incised groove. All lobes balanced symmetrically around an OPEN small transparent centre exactly at canvas centre. Recognizable as ONE generic attachment point, NOT an L-shaped corner, axis, compass or star. Slender charcoal outside rim, warm ivory enamel face, restrained shallow bevel. Very readable at only 18-28 pixels, strong silhouette and generous negative space. Motif occupies 85% canvas. No background, letters, numbers, scene, other icons, photoreal texture, fine ornament, rivets, blue/red/green/purple/pink, glitter, drop shadow, glow or specular white highlights. This is a standalone final sprite; geometry centre is its attachment centre.

### snap-selected

Use case: stylized-concept. Asset type: one production raster selected-pivot OVERLAY for BuildWorks Nordic construction editor, transparent square canvas, NOT a mockup. Draw a warm IVORY carved Nordic locking collar: an OPEN thin forged outer circular band with four broad incised chevron notches and a small clearly recognizable symmetrical locking rune at exact centre, with two short horizontal prongs. Mostly transparent interior, so underlying amber or ivory attachment point remains identifiable. Centre rune occupies no more than 22% motif width, band radius 43% canvas width, balanced centre exactly50%50%. Short chunky restrained bevel, dark charcoal outline and incision, warm ivory face. Readable at24-36 pixels. No filled medallion, background, letters, numbers, extra icons, scene, fine decorative loops, blue/red/green/purple/pink, jewels, drop shadow, rays, sparkle, bloom, metallic white highlights. Overlay shape must read unmistakably as SELECTED AND LOCKED pivot and be visibly different from an unselected simple point.

### snap-geometry

Edit target: attached production snap helper knot sprite. Change ONLY ivory enamel faces to muted warm amber-brass. Preserve exact hooked knot silhouette, dark charcoal outline, symmetric central registration, alpha transparency, canvas and margins, carved groove and bevel. No additions. This is a smaller experimental geometry-point sprite, NOT native circular socket; MUST keep the same four-hook helper knot shape so it remains distinguishable from native snap sign. No extra icons, text or background.

## Historical .48/.49 assets

The following colours, registration and overlay sizes are historical, not the
current editor legend. The old files remain for reproducible history.

Six replacement raster PNGs generated individually through imagegen for .48.
The [earlier concept](../images/snap-point-style-proposal-v1.png) is historical.
Not vectors or game screenshots. Each is 1254 × 1254 with transparent background.
Alpha, decode, actual sprite registration and picking are checked in Unity.
Gameplay-distance readability still requires Ostrix's game feedback.

The owner approved a compact, nested sign system after .47 game screenshots.
Native signs have 16 px screen radius; helpers 9 px, at default settings.
Pin and active states are independent overlays; neither replaces a helper's type.
Actual coordinates are unchanged. Game readability still requires owner approval.

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

Muted amber-brass native socket: TWO thin opposed C-shaped forged jaw segments forming a slim outer circular clasp. Large EMPTY central aperture, at least 65% of full motif width, for a blue corner icon to fit INSIDE without overlap. Each jaw has one broad notch. It must read as a connection socket, NOT a wreath, wings, laurel, jewellery, or filled medallion. Uniform dark outside and inside rim. Warm gold face, not dazzling white.

### snap-corner

`snap-corner.png`

Bright azure-blue carpentry corner: a compact forged L-shaped right-angle bracket with two short equally thick arms, one broad angular Nordic notch in each. The EMPTY INSIDE elbow is exactly at canvas centre (50%,50%); bracket lies mainly BELOW and LEFT of that centre; both arm ends balanced around it. Do NOT place the elbow at the bottom-left of the canvas. Compact silhouette that can fit in an outer gold ring. No long arms or detailed knots.

### snap-midpoint

`snap-midpoint.png`

Fresh jade-green midpoint clamp: TWO small opposed forged wedge jaws directly ABOVE and BELOW the exact canvas centre. Equal chunky short bars with inward triangular notches; open central gap, no other arms. Each has a single broad incised line. Symmetric, compact, clear green face and dark outline, can fit inside a gold outer socket.

### snap-centre

`snap-centre.png`

Soft rose-pink Nordic carpenter compass: FOUR short tapered forged points around a SMALL hollow central diamond precisely at canvas centre. Broad dark edge, pink enamel face and one simple incised groove per point. Compact balanced star, no surrounding circle, no long needles, no glitter. Recognisable at 20 pixels, not ornate rosette.

### snap-pin

`snap-pin.png`

Amethyst-purple fixed pivot OVERLAY: a narrow locking pin with a hollow small diamond head exactly at canvas centre, two short prongs extending vertically upward and downward. Mostly transparent canvas, narrow silhouette. Purple colour and dark outline, one strong notch. Designed as a small centre overlay so the gold/blue/green BASE POINT remains visible around it. No large background disk, no long spear, no gemstone covering the centre.

### snap-active

`snap-active.png`

Ivory-white active-source OVERLAY: FOUR SHORT inward-pointing forged corner ticks around an EMPTY transparent central opening exactly at canvas centre. Compact square aperture and one broad Nordic cut notch, graphite outer edge. Thin open shape that fits inside a larger snap icon, no filled disk, no rays, no sparkle, no runic letters.

## Registration and minification

The blue inside elbow uses sprite pivot `(0.43, 0.42)`; other new PNGs use
`(0.5, 0.5)`. DrawArtwork registers the motif, not the transparent rectangle.
The appended pin is state only: original typed markers at that coordinate remain.
Pin radius is 10.5 px; white active brackets 6.5 px. These are quad radii, not
opaque silhouettes. Transparent margins also consume apparent size.

## Why the generated preview differs from the game

A 1254 px image becomes a 32 px native quad or an 18 px helper quad.
Fine engraving cannot remain equally detailed at that size. An off-centre
generated motif looks displaced even when the model coordinate is correct.
The .47 shader multiplied snap RGB by 1.8/1.65/1.8, changing colour and contrast;
.48 uses neutral white. The previous Workbench importer resized NPOT textures
and compressed them, unlike the embedded decoder. Both paths now retain 1254 px,
mipmaps and trilinear minification without compression in the capture.
Mipmaps reduce unstable tiny strokes; they do not restore lost drawing detail.

Review the real render on wood and grid, including coincident types, active
state and pin, not another enlarged concept. Unity tests require native and
helper colour at one shared coordinate. External host/input are substituted on
the Workbench; Valheim camera, display scale and physical controls remain an owner gate.

## Shared generation brief

Use case: stylized-concept. Asset: one production raster snap-point icon for a Nordic Valheim-style construction editor, NOT a concept board. Transparent background, square canvas. Orthographic front-facing painted forged-metal UI emblem, no perspective. Readable when reduced to 20-28 pixels: broad simple silhouette, dark charcoal outer outline, strongly colored enamel face, one LARGE shallow engraved notch/interlace only. No photoreal fine texture, cracks, tiny rivets, bloom, drop shadow, text, letters, scene, labels, borders, or additional icons. All components centred precisely on the geometric canvas centre; centre of attachment is OPEN transparent and obvious. Occupy 80% of canvas, balanced 10% margins. Keep colour stable without white specular highlights. Restrained game-like bevel.
