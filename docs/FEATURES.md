# BuildWorks features and boundaries

[Русский](FEATURES_RU.md) · [Evidence by level](CURRENT_STATUS.md) · [Controls](CONTROLS.md)

**Alpha 0.19.329 limits:** paid placement and cold reload are confirmed for one wall-overlap chest and one rotated wall cookingstation; the chest opens. Cooking with food/fire/fuel and the sufficient-resources negative case remain untested. Placement does not waive operating requirements, resources, permissions or station checks. A chest cannot provide attachment support for another chest. Producer-name corrections are included; review missing parts and test your own mod stack. Unbound export UI is later source work and is not included in this issued archive.


Guide for alpha 0.19.329; each feature has its own acceptance scope. [Version/limits](ALPHA_0.19.329.md).

## Furniture and crafting placement

Eligible furniture, chests and crafting pieces may rotate and overlap in BuildWorks F9/blueprint placement when they have real contact with an eligible support. A typed Container never supplies attachment support; another chest may overlap only when a separate eligible surface supports it. Other WearNTear donors retain their native support flag and valid-view requirement; static surfaces without WearNTear may qualify. Structural walls, beams and roofs retain load-bearing strength rules. Ordinary placement outside BuildWorks keeps native restrictions. No free building, global lifetime exemption or admin air placement is included. Fire, fuel, roof and other operating requirements are separate from placement.

## Hammer and catalog

Categories/materials/search/recent/favorites expose available build pieces and blueprint cards. Import/export are reachable from Hammer and editor catalogs. Cards show count/category/preview and availability warnings. Owned entries can be renamed, categorized, deleted, saved as new copies or shared. External sources are hidden/restored without deleting originals. Opening/importing a source does not make every object buildable.

## Blueprint editor

The isolated viewport has grid, camera/light settings, catalog, object tree, groups, lock/visibility, filters and Undo/Redo. Save updates an owned entry; Save As creates a new ID/name/category. The interior camera helps inspect and place items inside complex geometry. Temporary editor objects do not become game-world objects.

Move/rotate use XYZ frames; scale supports independent positive components1–1000%, proportional mode, numeric input and axis/pair constraints. Reaching a supported limit clamps the preview. Pointer wrapping and fine input exist but remaining edge cases are not declared perfectly accepted. One completed operation is one Undo step; cancellation restores the prior document.

Array repeats a piece/group in line/plane with Pack/Fit/exact step, rise, heading/pitch/roll, scale and symmetry. Contour repeats along a connected support chain. Arbitrary editable spline guides/adaptive original pieces remain later work.


Alt+drag a Move arrow/plane moves a copy; Esc cancels, release is one Undo. Display provides manual groundY and an optional structural estimate (off). Grey is Unknown; red can meet the minimum; this is not real-site validation. Full5.7/5.8 remain open.

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



## Development after the archive

Accepted development source: `32f70d3293f9ccfebcedbf2f60083e23fdd92b84`; documentation checkpoint: `6569cfff05c0ba8a7f4d288b41b9ff58bfb5c041`. These later changes are not in the installable archive. Durable mounted-item return cleanup, saved absence, terminal state and recovery have SOURCE/CPU acceptance only. Production controller/admission remains closed; paid gameplay/restart/network validation is still required. The full scope remains 137 stable IDs / 35 scoped accepted / 102 open. 2.16 and 2.19–2.23 remain open; all 14 sections and future milestones remain required.
