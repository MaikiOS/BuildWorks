# BuildWorks features and boundaries

[Русский](FEATURES_RU.md) · [Evidence by level](CURRENT_STATUS.md) · [Controls](CONTROLS.md)

This guide describes source/QA .206, not the historical .37 download. Each implemented feature still has its own acceptance scope.

## Hammer and catalog

Categories/materials/search/recent/favorites expose available build pieces and blueprint cards. Import/export are reachable from Hammer and editor catalogs. Cards show count/category/preview and availability warnings. Owned entries can be renamed, categorized, deleted, saved as new copies or shared. External sources are hidden/restored without deleting originals. Opening/importing a source does not make every object buildable.

## Blueprint editor

The isolated viewport has grid, camera/light settings, catalog, object tree, groups, lock/visibility, filters and Undo/Redo. Save updates an owned entry; Save As creates a new ID/name/category. The interior camera helps inspect and place items inside complex geometry. Temporary editor objects do not become game-world objects.

Move/rotate use XYZ frames; scale supports independent positive components1–1000%, proportional mode, numeric input and axis/pair constraints. Reaching a supported limit clamps the preview. Pointer wrapping and fine input exist but remaining edge cases are not declared perfectly accepted. One completed operation is one Undo step; cancellation restores the prior document.

Array repeats a piece/group in line/plane with Pack/Fit/exact step, rise, heading/pitch/roll, scale and symmetry. Contour repeats along a connected support chain. Arbitrary editable spline guides/adaptive original pieces remain later work.

## Anchors and snapping

Pieces/groups and independently stored free anchors are represented separately. Transform pivot and placement anchor are different choices; temporary A/B points are not saved anchors. Native piece snaps, declared user snaps and editor helper points respect their own frames. Ordinary world auto-snap retains Valheim's chosen pair/player rotation; F9 provides explicit XYZ precision. Composite/editor snapping is separate. [.193](WORLD_SNAP_193_RU.md) gives limited native comparison evidence.

## Import, export and missing parts

Supported `.blueprint`/`.vbuild` dialects and internal files/ZIP are reviewed before conversion. Source data and provenance remain; unknown archived fields do not imply restored behavior. Export warns about unsupported fields and writes only the chosen format's confirmed structure. Batch export is full BuildWorks ZIP. See [format contract](../specs/roadmap/blueprint-format.md).

Missing records retain their original transform and known metadata. Known local bounds give a transformed box; absent dimensions give an uncertain marker. Align uses copies of selected own pieces and retains originals/missing record; Replace additionally removes the selected missing record. A reviewed copy may exclude missing parts, but an all-missing result is refused. [Alignment guide](MISSING_PART_REPLACEMENT.md).

## Files, server catalog and construction

The primary `blueprints.json` library has managed portable `share/<id>.buildworks.json` files after save/share. Categories are metadata, not arbitrary filesystem moves. Group import/export, progress and selected results exist. Server publication is administrator-only with players/admin visibility and reviewed personal copies. Direct player-to-player transfer and full live server acceptance remain open.

World construction creates native pieces and uses native validation/resources. [.195](ROTATION_PERSISTENCE_195.md) proves one28-part imported house and exact reload. Storage and mounted items have independent inclusion choices; durable accounting/admin restoration and complete runtime support are not finished. Nonrecipe world objects cannot become free survival pieces.

## Unfinished projects, v11 and limits

Approved v11 is being integrated: native two walls/.203, Unity common three-source volume/.206. Persistent house/stake/board/material continuation/cancel/refund/reload/server are not connected yet. Existing pause is transient, not a durable project.

Production interactive/editor limit512; QA/Unity editor50000; document part limit50000; owned library32 entries/12 categories/100000 aggregate parts. File guards and memory limits remain. This is not a performance promise for any maximum-size building.

English fallback and complete Russian catalogs are implemented and checked. [Roadmap](ROADMAP.md) tracks remaining selection/heavy/world-object/admin/network work. EarthWorks/terrain and OBJ/FBX/glTF are outside this module.
