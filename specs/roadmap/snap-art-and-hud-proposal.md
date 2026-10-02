# BuildWorks snap point artwork and native resource cards

**English** | [Русский](snap-art-and-hud-proposal_RU.md)

Design proposal, 2026-10-02. BuildWorks 0.19.45 remains installed. Ostrix accepted
tests 1, 3–6 and 8–11; test 12 confirms language refresh, not the resource-card
layout. Test 2 requests thematic artwork and test 7 reopens cursor snapping.
No new controls or assets in this proposal are implemented or approved yet.

## Point artwork

[Generated concept](../../docs/images/snap-point-style-proposal-v1.png) is a
style study, not a game screenshot or final runtime asset. Six related, original
Nordic motifs distinguish function by shape as well as colour:

- Gold native socket: interlocking timber-joint clasp.
- Blue corner: a notched forged angle bracket.
- Green midpoint: opposing joint clamps.
- Pink centre: a carpenter compass.
- Purple Shift pin: a fixed stake with a locking notch.
- White active source: an illuminated compact source marker.

Keep exact attachment coordinates, original model materials and existing
proximity feedback. Coincident markers share one centre; the smaller helper
fits inside the native rim. Do not hide points or separate their screen centres.
After approval, author individual assets and inspect them at real gameplay
sizes; the board's enlarged previews do not prove small-size readability.
Reduce fine engraving if it becomes noise, without reverting to generic glyphs.

## Source selection during cursor movement

Current G movement cycles every gizmo anchor, including bounds and generated
midpoints. Catalog placement already has an Auto entry; G does not share that
source-selection workflow. This causes long Q/E cycles and inconsistent behaviour.

Recommended proposal:

- G moves the existing selection over scene surfaces like the placement preview.
  Keep its rotation/scale and exact Esc/Undo guarantees.
- Default source set is Native. Q/E cycles Auto followed only by native sockets.
  Generated points must not lengthen that cycle.
- Auto chooses a nearby native source/target pair first; outside capture distance,
  movement remains surface-based rather than locking to an axis or grid.
  Use stable winner retention to prevent flicker between equivalent pairs.
- Additional points are opt-in. With no native sockets (e.g. furniture), clearly
  show the helper fallback instead of offering an empty source set.
- A compact attachment chooser offers Auto and labelled native points; hovering
  highlights the real point on the preview. Additional points form a separate
  section. Opening/choosing/closing it must never place or confirm the part.
  Do not add a mandatory radial menu or change Space/F3/Tab bindings.
- An explicitly picked source stays explicit; changing modes must not silently
  discard it. Existing Shift-pin manipulation retains its separate pivot role.

Reuse the scene's existing source provenance and placement contact logic.
Do not replace the accepted R/S/axis workflow or rewrite Array/Contour.
The native-first policy and chooser require owner approval before implementation.

## Native blueprint resource cards

Ostrix's screenshots define the requested layout: blueprint thumbnail/name and
short localized description, then the same kind of icon/name/amount cells as an
ordinary piece, with required-station icons and availability. Text-only resource
lines from .45 do not satisfy this request.

Local host inspection confirms Hud.SetupPieceInfo uses m_requirementItems,
InventoryGui.SetupRequirement and CraftingStation icons. Reuse those native
templates; aggregate resources by item identity, deduplicate stations, and keep
the panel in its native position. Widen it when needed, then wrap extra cells
within the screen. Restore its original layout on ordinary-piece selection,
HUD rebuild and teardown. All labels/tooltips must refresh with game language.

This must remain display-only. Never put aggregate costs into the blueprint
marker's m_resources: native child placement already consumes each child's
requirements. Show missing resources/stations accurately without implying that
the whole blueprint is valid merely because its first part is valid.

## Implementation gate and checks

Artwork and source-selection approval are the next gate. No new binary release
or deployment in this documentation checkpoint. After approval, checks must cover
same-position glyph picking, native-only versus helper cycles, stationary Q/E,
Auto winner stability, furniture fallback, chooser input ownership, exact cancel
and one Undo. Resource cards require exact totals, multiple station types,
insufficient inventory, language refresh, overflow and ordinary-HUD restoration.

Prepare one new numbered owner series only after implementation and automated
checks. Do not ask the owner to rerun the accepted .45 points now.
