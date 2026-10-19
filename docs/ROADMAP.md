# BuildWorks roadmap

[Русский](ROADMAP_RU.md) · Updated 19 October 2026

## Product direction

BuildWorks provides free and precise construction in Valheim, from one piece to a complex custom building. Players will build manually, edit and reuse assemblies, transfer buildings through blueprints, gradually construct them with resources, and share them. Further development brings editable repetitions, construction guides, and original adaptive pieces for more ambitious architecture.

The foundation remains part of the game: real Valheim objects, understandable controls, construction costs, durable results, and cooperative play with server-authorized permissions.

## Development milestones

| Milestone | Player outcome | State |
| --- | --- | --- |
| 1. Precision construction and editor | Work freely with pieces and assemblies | Foundation available in alpha; complex cases still need verification |
| 2. Buildings as blueprints | Take an existing building or save your own, edit it, and build it again | Partially implemented; completing the main journey |
| 3. Construction projects | Leave a future house in the world and gradually supply its materials | **Current priority: paid continuation through a durable project/board with v11 fog** |
| 4. Complete and large buildings | Transfer large buildings, furnishings, and contents without loss | Foundations exist; substantial work remains |
| 5. Shared libraries and cooperative construction | Share blueprints and use a server library | File sharing and server-library foundation exist; network journey incomplete |
| 6. Editable assemblies and repetitions | Revise layout and repetition parameters after creation | Later milestone; ordinary Array and Contour already exist |
| 7. Construction along guides | Place pieces and assemblies along lines and curves | Planned direction; tool behavior still needs agreement |
| 8. Original adaptive pieces | Create shapes beyond rigid standard pieces | Longer-term direction; separate geometry and material development |
| 9. Cooperation with EarthWorks | Connect site preparation and construction | Separate later milestone after construction projects are ready |

Numbers show the direction of development. Some preparation can overlap. No entire milestone is declared complete; future release dates and versions are not assigned.

## Milestone scope

### 1. Precision construction and editor

Translate, rotate, and scale on selected axes; reliably snap rotated pieces; use groups, independent anchors, connection points, Undo/Redo, and an interior camera. **Outcome:** predictable assembly, placement matching the preview, and the same shape after reload. Core tools exist; complex snapping and remaining control issues still need verification.

Ordinary world auto-join, including F9, preserves the chosen player rotation. Adopting additional target tilt is a separate held mode, without hidden turns caused by equivalent axes of symmetric beams. **Acceptance:** unchanged inputs give an unchanged result across camera sides, vertical/horizontal/diagonal prefabs, actual root XYZ changes, manual rotation and mode transitions. Prefab mesh tilt and root rotation are distinct cases.

### 2. Buildings as blueprints

External library or world capture → editing → construction. A personal library with categories, understandable files, Save As, deletion, and batch import/export. Foreign formats declare supported data and losses. Locate a missing part by its saved position, replace it with your pieces, or exclude it in a separate copy. **Outcome:** usable construction blueprints with limitations explained beforehand. Selected houses passed this journey; the full declared corpus remains incomplete.

### 3. Construction projects

Insufficient materials leave a recognizable, pass-through v11 fog ghost. A stake and board let players equip a hammer, inspect shortages, supply materials, continue, or cancel. Finished parts become real objects; projects survive world visits; cancellation refunds actual spending without repeated payouts. **Outcome:** unfinished houses are convenient to return to. Connect the durable project/board to confirmed payment and actual construction, then unfinished-part fog and whole-house acceptance; laboratory approval alone does not establish readiness of the mechanic. Returned fog must restore the correct surface independently of prefab orientation and nearby damaged parts.

### 4. Complete and large buildings

Capture an area with editable upper/lower bounds, manually include/exclude parts, and understand loaded-world limits. Handle signs, furnishings, storage, mounts, items, and armor stands. Independent content choices; survival uses supplied items with their own properties, while full administrative restoration requires authority. Trees and suitable objects without recipes are administrator-only or explicitly priced. **Outcome:** complete buildings pass the full journey without loss, duplication, or unacceptable delays. Load measurements will establish the boundary between small direct placement and large planned construction.

### 5. Shared libraries and cooperative construction

Share blueprints between players and use a server library published by its administrator. Separate player/administrator access, permitted personal copies, and consistent construction across participants. **Outcome:** libraries and projects work through reconnects and server restarts without permission bypasses or repeated charges/refunds. File sharing exists; direct transfer and complete cooperative behavior remain ahead.

### 6. Editable assemblies and repetitions

Align, distribute, mirror copies, and change spacing between pieces or assemblies without resizing their geometry. Saved Arrays retain their source assembly and allow later count/spacing edits. Linked instances with an explicit Make Unique action follow. **Outcome:** revise a fence, row of supports, or repeated assembly through parameters instead of rebuilding manually. Existing copy tools are a foundation; persistent editable operations are not ready.

### 7. Construction along guides

Straight A/B → polyline → arc → Bezier curve. Guides supply positions and orientations for pieces or repeated assemblies; ordinary pieces remain rigid. **Outcome:** edit a path, predictably construct along it, save, and resume. Before implementation, including A/B, agree on manual placement versus repetition, control points, spacing, and orientation.

### 8. Original adaptive pieces

Original BuildWorks pieces and materials change shape through agreed parameters or guides. Appearance, textures, collisions, and snap points match the resulting form. **Outcome:** richer architecture that reconstructs consistently after reload and for server participants. This is a later direction, not an available feature or a promise to deform arbitrary prefabs.

### 9. Cooperation with EarthWorks

A later stage. Current work is independent house construction without site preparation; terrain work comes when EarthWorks is connected.

Explicit cooperation between a separate terrain-preparation project and the building project: site → materials → gradual construction. **Outcome:** a coherent workflow between two independent mods. The unfinished building, v11 ghost and stake/board form the cooperation point. EarthWorks alone owns terrain; cancelling the building never changes or restores it. Preparation starts from the selected house/blueprint, with one board and one material store for the joint project. Presentation/actions, access and accounting remain to be agreed. The [architecture proposal](EARTHWORKS_BOUNDARY.md) is not approved; implementation follows independent building projects and the required EarthWorks tools. Terrain transfer and arbitrary OBJ/FBX/glTF are not added to the current blueprint module.

## Priorities and progression

The next outcome is **a durable construction project with paid board continuation and v11 on unfinished parts of the house**. Then continue completing the blueprint and full construction journey in milestones 2–5. Later tools in milestones 6–9 do not replace this work. Regressions in the working core take priority over feature expansion.

Controls, Russian UI, performance, data preservation, and network authority apply at every milestone. Guides need saved assemblies; adaptive pieces need proven geometry and snapping; EarthWorks cooperation needs working construction projects. Public-release readiness requires game evidence and separate owner authorization to release.

[Current evidence and limits](CURRENT_STATUS.md) · [Active work and manual checks](LIVE_CHECKLIST_RU.md) · [Development journal](DEVELOPMENT_JOURNAL.md) · [Current module contract](../PROJECT_CONTRACT.md)
