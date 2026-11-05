# BuildWorks roadmap

[Русский](ROADMAP_RU.md) · 2 November 2026

## Current work and scoped results

**Installable alpha: 0.19.329.** [Download and release notes](https://github.com/MaikiOS/BuildWorks/releases/tag/v0.19.329-alpha). The original 2 November ZIP is unchanged; its internal “private test candidate” wording describes its manual-test origin. This is an alpha prerelease; beta follows completion of the full agreed goal. No Thunderstore upload is included.

Accepted development source: `32f70d3293f9ccfebcedbf2f60083e23fdd92b84`; documentation checkpoint: `6569cfff05c0ba8a7f4d288b41b9ff58bfb5c041`. These later changes are not in the installable archive. Durable mounted-item return cleanup, saved absence, terminal state and recovery have SOURCE/CPU acceptance only. Production controller/admission remains closed; paid gameplay/restart/network validation is still required. The full scope remains 137 stable IDs / 35 scoped accepted / 102 open. 2.16 and 2.19–2.23 remain open; all 14 sections and future milestones remain required.

The .329 archive has bounded acceptance for one wall-overlap chest costing 10 Wood and a wall cooking station rotated 90 degrees costing 2 Wood, followed by a new-process reload; the chest opens. Poses were set with virtual transform fields and confirmed through native placement. Physical mouse/gizmo input was not re-proved. The container-only NoContact negative had 2 Wood; a sufficiently funded negative and cooking/fire/food/fuel remain untested. Old 329h preservation UNKNOWN/HOLD and planner strictPlayerStateSame=false are retained. Full module and host+remote acceptance remain open.

## Product direction

BuildWorks provides free and precise construction in Valheim, from one piece to a complex custom building. Players will build manually, edit and reuse assemblies, transfer buildings through blueprints, gradually construct them with resources, and share them. Further development brings editable repetitions, construction guides, and original adaptive pieces for more ambitious architecture.

The foundation remains part of the game: real Valheim objects, understandable controls, construction costs, durable results, and cooperative play with server-authorized permissions.

## Development milestones

| Milestone | Player outcome | State |
| --- | --- | --- |
| 1. Precision construction and editor | Work freely with pieces and assemblies | Foundation available in alpha; complex cases still need verification |
| 2. Buildings as blueprints | Take an existing building or save your own, edit it, and build it again | Partially implemented; completing the main journey |
| 3. Construction projects | Leave a future house in the world and gradually supply its materials | Partial evidence; complete paid lifecycle remains required |
| 4. Complete and large buildings | Transfer large buildings, furnishings, and contents without loss | Foundations exist; substantial work remains |
| 5. Shared libraries and cooperative construction | Share blueprints and use a server library | File sharing and server-library foundation exist; network journey incomplete |
| 6. Editable assemblies and repetitions | Revise layout and repetition parameters after creation | Later milestone; ordinary Array and Contour already exist |
| 7. Construction along guides | Place pieces and assemblies along lines and curves | Planned direction; tool behavior still needs agreement |
| 8. Original adaptive pieces | Create shapes beyond rigid standard pieces | Longer-term direction; separate geometry and material development |
| 9. Cooperation with EarthWorks | Connect site preparation and construction | Separate later milestone after construction projects are ready |

Numbers show the direction of development. Some preparation can overlap. No entire milestone is declared complete; future release dates and versions are not assigned.

## Milestone scope

### 1. Precision construction and editor

<a id="f9-planned-20261105"></a>
**F9 feedback accepted for planning — 5 November 2026.** These requirements are not confirmed fixes and are not included as new behaviour in alpha 0.19.329. The reported version/profile and causes remain unverified; no implementation or release date is assigned.

- **Selected transformation point:** investigate the reported rotation/scale regression where F9 uses the object centre instead of the selected point. Keep that point fixed during rotation and applicable scaling; use the normal centre when no point is selected.
- **Ordinary objects as supports:** allow furniture, chests and crafting objects to support other objects through real contact and a valid support chain—for example, a scaled-down chest on a stool. Object type alone must not exclude a support. This planned policy replaces the earlier blanket exclusion of containers; it does not change the released alpha. Walls, beams and roofs retain their strength rules. Floating self-supporting cycles and a global support override are excluded; removal, alternative contacts and reload still need validation.
- **World grid:** stable world origin/axes and adjustable spacing, with independent display and snapping switches. Snap the selected point; the grid must not follow the camera or object. Keep existing connection-point snapping; no universal vanilla grid has been established.
- **Consistent controls:** match the editor's G/R/S tools, axes/axis pairs, selected point, mouse/numeric input, confirmation and cancellation. Reuse the editor's transformation behaviour and mathematics.
- **Explicit F9 scale:** provide S, axis controls and numeric scaling around the selected point within the existing positive 1–1000% range per component, including proportional scaling. Account for the original prefab scale and keep colliders, preview, snapping, payment, interaction and saved/reloaded results consistent. Reject invalid or unrepresentable values.

Pivot regression and support behaviour have priority within this feedback. Reproduction and game validation remain required; no fix or release version is claimed. The existing order of blueprint work, the complete agreed goal and all future milestones remain. Beta still requires completion of the full agreed goal.


Translate, rotate, and scale on selected axes; reliably snap rotated pieces; use groups, independent anchors, connection points, Undo/Redo, and an interior camera. **Outcome:** predictable assembly, placement matching the preview, and the same shape after reload. Core tools exist; complex snapping and remaining control issues still need verification.

Ordinary world auto-join, including F9, preserves the chosen player rotation. Adopting additional target tilt is a separate held mode, without hidden turns caused by equivalent axes of symmetric beams. **Acceptance:** unchanged inputs give an unchanged result across camera sides, vertical/horizontal/diagonal prefabs, actual root XYZ changes, manual rotation and mode transitions. Prefab mesh tilt and root rotation are distinct cases.

### 2. Buildings as blueprints

External library or world capture → editing → construction. A personal library with categories, understandable files, Save As, deletion, and batch import/export. Foreign formats declare supported data and losses. Locate a missing part by its saved position, replace it with your pieces, or exclude it in a separate copy. **Outcome:** usable construction blueprints with limitations explained beforehand. Selected houses passed this journey; the full declared corpus remains incomplete.

### 3. Construction projects

Insufficient materials leave a recognizable, pass-through ghost based on v11: surface flow, held holes and recovery without separate white volume haze. A stake and board let players equip a hammer, inspect shortages, supply materials, continue, or cancel. Finished parts become real objects; projects survive world visits; cancellation refunds actual spending without repeated payouts. **Outcome:** unfinished houses are convenient to return to. Connect the durable project/board to confirmed payment and actual construction, then unfinished-part fog and whole-house acceptance; laboratory approval alone does not establish readiness of the mechanic. The returning surface must restore its correct form independently of prefab orientation and nearby damaged parts.

Cancellation requires durable deletion, physical refund issuance and collection records; an ambiguous crash outcome must never automatically replay delivery. [Technical plan](PROJECT_CANCELLATION_PROTOCOL.md).

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

The current queue is 2.19–2.23, then later sections; 2.16 remains required before release. The complete paid board/v11 project and remaining milestones2–5 stay mandatory. Later tools in milestones 6–9 do not replace this work. Regressions in the working core take priority over feature expansion.

Controls, Russian UI, performance, data preservation, and network authority apply at every milestone. Guides need saved assemblies; adaptive pieces need proven geometry and snapping; EarthWorks cooperation needs working construction projects. Public-release readiness requires game evidence and separate owner authorization to release.

[Current evidence and limits](CURRENT_STATUS.md) · Active work and manual checks (evidence retained locally; not published) · [Development journal](DEVELOPMENT_JOURNAL.md) · [Current module contract](../PROJECT_CONTRACT.md)

## All 137 requirement IDs — navigation to the existing goal

Statuses mirror LIVE; no new checkmarks. English entries are editorial summaries, not replacements for the complete canonical requirements (evidence retained locally; not published). This is not a new goal or acceptance.

| ID | Scope status | Requirement summary |
| --- | --- | --- |
| 1.1 | Scoped accepted | Evidence and installed-build reconciliation |
| 1.2 | Scoped accepted | Separate requirements, evidence, history and future plans |
| 1.3 | Scoped accepted | Inventory all 137 scoped requirements and corpus evidence |
| 1.4 | Scoped accepted | Verify memory and code retrieval without blind reindexing |
| 1.5 | Scoped accepted | Preserve accepted owner cases; rerun only when affected |
| 2.1 | Scoped accepted | Format/capture fidelity; scoped V1 six-piece correction |
| 2.2 | Scoped accepted | Immutable originals, provenance and capture metadata |
| 2.3 | Scoped accepted | Warn before optional loss; reject unknown mandatory geometry |
| 2.4 | Scoped accepted | Verified third-party format/version register |
| 2.5 | Scoped accepted | Authentic producer corpus and honest stage results |
| 2.6 | Scoped accepted | Nested frames, prefab scale, named centre and independent transforms |
| 2.7 | Scoped accepted | Supported materials/signs through paid placement and cold reload |
| 2.8 | Scoped accepted | Only proven legacy prefab migration with warnings |
| 2.9 | Scoped accepted | Tomrer9/10/14-field layouts; bounded file/native evidence |
| 2.10 | Scoped accepted | Homestead descriptive/terrain-contact records stay passive |
| 2.11 | Scoped accepted | Owner-approved all-format first-beta scope; implementation separate |
| 2.12 | Scoped accepted | OpenKeep/DevBridge bounded JSON profile and paired export |
| 2.13 | Scoped accepted | KG YAML import/export and original consumer; bounded native proof |
| 2.14 | Scoped accepted | KG oprint binary profile and original reader; bounded proof |
| 2.15 | Scoped accepted | BetterBuilding BBP1-4 archive/export; state application remains separate |
| 2.16 | Open | Legacy BetterBuilding original-v1 consumer, capture and strict-state gaps |
| 2.17 | Scoped accepted | Planner JSON codec and pinned profile-data fidelity, including layers, source preservation, original consumer and four direct producer checks, accepted in internal 0.19.329. The issued 0.19.328 is not fixed; full gameplay sections 3–13 and networking remain mandatory. |
| 2.18 | Scoped accepted | Innangard writer/schema/authentic capture data; older preservation UNKNOWN retained, gameplay3–13 still mandatory |
| 2.19 | Open | Unbound authentic external contract and building/state metadata |
| 2.20 | Open | ValheimStudio authentic writer/reader and schema fidelity |
| 2.21 | Open | ValheimRAFT linked moving structures, frames and state |
| 2.22 | Open | WorldEditor scene/world bridge contracts; not generic blueprints |
| 2.23 | Open | Valheim Tools authentic EWP output, producer and reader |
| 3.1 | Open | Single/batch format, filename and folder from both catalogues |
| 3.2 | Open | Declared target-format fields, loss review and full native companion |
| 3.3 | Open | Batch selection/category/options/review and complete result reporting |
| 3.4 | Open | Cancellation/failure preserves originals and competing writes |
| 3.5 | Open | Focus/select exact imported card; broader UI/ordinary input open |
| 3.6 | Open | Preserve unsaved editor/history during import; full regressions open |
| 3.7 | Open | Original target reader/version proves interoperability, not self-roundtrip |
| 4.1 | Open | Explain missing IDs/mods and unknown origins in both catalogues |
| 4.2 | Scoped accepted | Bounded owner-approved incomplete blueprint and missing transform |
| 4.3 | Open | Accurate known bounds or an honest uncertain marker |
| 4.4 | Open | Fresh prefab registry provenance; missing versus unsupported/unavailable |
| 4.5 | Open | Explicit incomplete versus omit-missing copy; never empty silently |
| 4.6 | Open | Align copies and explicit final replacement preserve source pieces |
| 4.7 | Open | Catalogue insertion respects bounds/prefab/group frames and axes |
| 4.8 | Open | Full alignment/replacement preview, cancel, Undo, save/reopen acceptance |
| 5.1 | Open | Preserve existing editor/world F9 tools and ordinary-piece support |
| 5.2 | Open | Independent saved anchors; separate pivot and placement anchor |
| 5.3 | Open | Tree and tools expose created anchors and objects |
| 5.4 | Open | Saved user snaps and editor target rotation without hidden changes |
| 5.5 | Open | Ordinary world auto-snap retains chosen player rotation |
| 5.6 | Open | Separate held target-tilt mode without hidden inheritance |
| 5.7 | Open | Optional assumed-ground support estimate; full native/visual/corpus open |
| 5.8 | Open | Alt+Move-axis/plane drag-copy; full groups, metadata, save/focus open |
| 6.1 | Open | Complete storage, mount, item, armour-stand lifecycle; bounded owner cases |
| 6.2 | Open | Independent storage-content and mounted-item inclusion controls |
| 6.3 | Open | Use real supplied items without repair, upgrade or old privileges |
| 6.4 | Open | Preserve supported item properties/provenance through cost and refunds |
| 6.5 | Open | Separate explicitly authorized administrative reconstruction mode |
| 6.6 | Open | Server-authorized privileges; local client cannot grant itself rights |
| 6.7 | Open | Separate construction/sign data from wear, fuel, inventory and identity |
| 6.8 | Open | Natural objects require admin rights or assigned server price |
| 6.9 | Open | Saved displays/signs scale and orient correctly across all views |
| 6.10 | Open | Understandable supported mount item editing and unsupported transforms |
| 6.11 | Open | Optional AzuCraftyBoxes/external-source compatibility with real access rules |
| 7.1 | Open | Area then top/bottom volume selection; full interaction remains open |
| 7.2 | Open | Resize corners preserving opposite corner, height and exclusions |
| 7.3 | Open | Recompute loaded selection without duplicates; retain manual edits |
| 7.4 | Open | Explicit boundary intersection and loaded-zone limits |
| 7.5 | Open | Selection framing at different resolutions, UI scales and directions |
| 7.6 | Open | Accept readable non-blinding highlight from actual game examples |
| 8.1 | Open | Durable project/board and v11 only on unfinished pieces |
| 8.2 | Open | Future parts remain non-colliding and unusable as storage/stations |
| 8.3 | Open | Accessible hammer/board interaction with genuine range/obstruction checks |
| 8.4 | Open | Continue playing while gathering materials; no blocking preview pause |
| 8.5 | Open | Journal intent, actual cost, result and persistence before finalizing parts |
| 8.6 | Open | Paid cancellation returns actual contribution without native duplicate drops |
| 8.7 | Open | Exact-property refund recovery after failures, retries and old saves |
| 8.8 | Open | Full local paid board/cancel/claim/restart workflow; broader cases open |
| 8.9 | Open | No-contribution cancellation has no resource effect; appearance/network open |
| 8.10 | Open | Recover projects/contributions/parts after unload, reconnect/server restart |
| 8.11 | Open | Stable project/part/operation identity and exact recovery matching |
| 8.12 | Open | Measure threshold for direct small builds versus planned large builds |
| 8.13 | Open | Mixed inventory/chest payment, contents, cancel/refund, reload and host+remote |
| 9.1 | Scoped accepted | Owner-approved laboratory v11 visual basis; not whole-house acceptance |
| 9.2 | Open | Retain visible surface/scatter/holes; separate white volume haze removed |
| 9.3 | Open | Smooth recovery after leaving; visual acceptance still needed |
| 9.4 | Open | Four-section wall entry/pause/exit/re-entry from different sides |
| 9.5 | Open | Correct visual domains for rotated/scaled prefabs and buildings |
| 9.6 | Open | Visual state follows part/project lifecycle and reconnect |
| 9.7 | Open | No expensive volume pass; sustained performance/memory still open |
| 9.8 | Open | Visual failure cannot corrupt accounting, permissions or project access |
| 9.9 | Open | Do not redistribute extracted game resources or lab archives |
| 9.10 | Open | Preserve approved reference/handoff/control clips before visual edits |
| 10.1 | Scoped accepted | Owner-tested catalogue-opening delay fixed in that scoped case |
| 10.2 | Open | Measure import, catalogue, editor, ghost, construction/save and cancellation |
| 10.3 | Open | Isolated QA limit bypass keeps safety quotas and no ordinary-client privilege |
| 10.4 | Scoped accepted | Blueprint module excludes arbitrary OBJ/FBX/glTF and terrain transfer |
| 10.5 | Scoped accepted | BuildWorks remains independent of EarthWorks/site preparation |
| 10.6 | Scoped accepted | Confirmed future one-board/store boundary; building cancellation leaves terrain |
| 10.7 | Open | Agree joint board, access, reserves, refunds and API before integration |
| 11.1 | Open | Understandable localized import/export/errors in both entry points |
| 11.2 | Open | UI scale, resolution, thumbnail and tooltip edge cases |
| 11.3 | Open | Ready owner scenarios with files, materials, steps and failure criteria |
| 11.4 | Open | Build-camera mod compatibility without range, cost, station or rights bypass |
| 11.5 | Scoped accepted | Bounded isolated QA279 deployment/normal exits; not current version claim |
| 11.6 | Open | Authorized isolated automated QA and cleanup; no owner interruption |
| 11.7 | Open | Separate command, state, physical input and rendered proof; retain failures |
| 11.8 | Open | Isolated corpus register with names, provenance and per-stage results |
| 12.1 | Scoped accepted | Owner-approved Save As separate card and reopening |
| 12.2 | Open | Save/copy/import/update semantics with cancellation and data safety |
| 12.3 | Scoped accepted | Owner-approved own deletion/external hide and persistence; not world deletion |
| 12.4 | Scoped accepted | Owner-approved independent component scale in bounded case |
| 12.5 | Open | Positive finite1-1000percent scale, numeric controls and group guards |
| 12.6 | Open | Prefab basis matches preview, collision, world result and reload |
| 12.7 | Open | G/R/S axis/pair frames, highlight, mouse and numeric control |
| 12.8 | Open | Pointer wrapping and focus/window/input transitions; remaining regressions open |
| 12.9 | Open | Filters find nested objects without hierarchy/document mutation |
| 12.10 | Scoped accepted | Owner-approved basic interior camera; focus regressions remain separate |
| 12.11 | Open | Compact readable filters/tools preserving accepted Space menu |
| 12.12 | Open | Honest missing/incomplete thumbnail rather than a blank field |
| 12.13 | Open | Camera placement preserves ordinary permissions, cost and validation |
| 12.14 | Open | Prepared scale/snap tests with actual support and no owner setup burden |
| 13.1 | Open | Personal catalogue contains explicit imports/creations and permitted server entries |
| 13.2 | Open | Clear import category/path and managed portable sharing files |
| 13.3 | Open | Share folder is not auto-scanned; export is a separate conversion |
| 13.4 | Scoped accepted | Source folders separated from catalogue; file presence is not import proof |
| 13.5 | Open | Administrator-published server library with enforced visibility |
| 13.6 | Open | Permitted personal copies preserve provenance, not shared edit/admin authority |
| 13.7 | Open | Recipient/consent/idempotent transfer protocol; runtime endpoint remains open |
| 13.8 | Open | Separate host+remote permissions, retries, reconnect and server restart |
| 14.1 | Open | Coordinator integration and independent review before source/docs publication |
| 14.2 | Open | Full external-file/editor/ghost/paid-build/reload corpus and networking |
| 14.3 | Open | Full own-capture/edit/ghost/build/reload fidelity journey |
| 14.4 | Open | Continue all-source/container lifecycle work and all agreed conditions |
| 14.5 | Open | Close every remaining requirement2-13 with its own acceptance |
| 14.6 | Open | Update live requirements, evidence, history, build hashes and source checkpoint |
| 14.7 | Scoped accepted | Preserve assemblies, guides, adaptive pieces and EarthWorks future directions |
| 14.8 | Open | Full completion audit before goal completion; docs do not change goal state |
| 14.9 | Open | Bilingual docs/wiki and truthful media; Thunderstore upload still withheld |
