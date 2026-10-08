# BuildWorks development roadmap

## .110 — pinned import scale: verified code, deployment pending

Native17/portable7 pins absolute source size independently of canonical/Hammer bases.
Editor/thumbnail/ghost/save/reopen and rotated nonuniform snaps passed; editor tiny
geometry is separate from native world range. Parent scale/shear/mirror capture is
rejected. Normal/QA0/0, Geometry/Store/host/localization857/QA isolation/format,
Workbench252 and independent correctness/ponytail PASSED. Installed QA remains .109.
No Valheim execution; native build/reload/network and axis-offset reproduction open.
Legacy v16 copies remain; one new .110 placement copy may coexist, then deduplicate.
See checkpoint-0.19.110.md and QA_0.19.110_RU.md.

QA .109 installed; hashes match,48 QA files and library unchanged. Checkpoint:3683382. No game execution.

## .109 — Lava Lantern visual clone scale

Installed real asset reproduced double child scale20.7203922. Shared BakeMesh fixed;
0.82m shell bounds match independent static high mesh within0.05m; thumbnail cachev3.
Normal/QA0/0, Geometry/Store/host/localization856/QA isolation/format, Workbench252
and independent correctness/ponytail review passed. No Valheim execution.
Canonical/Hammer import bases and native place/reload remain open.
See docs/checkpoint-0.19.109.md and docs/QA_0.19.109_RU.md.

QA .108 installed by the guarded script; DLL hashes match,48 QA files and the library unchanged. No game execution. Checkpoint:e51c840.

## .108 — heavy editor: automated checks passed

Staged load/progress/cancel and virtual outliner passed1025-record actual-controller
checks including missing/Undo/Redo/save/reopen. Normal512, QA50000 with loaded-session
guard; capture512 and heavy finalization remain open. Workbench252 screenshots,
normal/QA0/0, Geometry/Store/host/localization856/QA isolation/format and independent
review passed. No Valheim execution; installed build remains .107.

See docs/checkpoint-0.19.108.md and docs/WORK_STATUS_RU.md.

QA .107 was installed by the guarded script; hashes match,48 QA save/receipt files and the library are unchanged. The game was not launched.

## .107 — payment/refund: automated checks passed, game acceptance open

Actual paid stacks replace recipe refunds. Native drop suppression, original network ID
and a live native batch flush gate settlement. Last completion waits for resource spending.
Normal/QA0/0, GeometryTests, HostContract, localization854, QA separation, format and two
independent read-only reviews passed. Native cancellation/reload/network remain unaccepted.

See [checkpoint .107](checkpoint-0.19.107.md) and [full requirement ledger](WORK_STATUS_RU.md).


## .106 — catalog feedback; verified code step, game verification pending

Ostrix accepted the built CoffeeTable shape. Exact resource delta and world reload
are not separately accepted; do not repeat the shape check without a relevant change.
Native .105 prepared the temporary 64x64 m yard and built four parts. The larger yard remains open.

.106 reuses an untouched imported placement copy, prepares the library in a worker,
retains conversion cache across native edits, reserves the thumbnail layer, prevents
actions overlapping the preview, and adds cached world-unavailable card markers.
Native v16 reads previous versions; portable v6 is unchanged. Normal/QA builds0/0,
store regressions, host contract, localization853 and format passed.
QA .106 is installed in TerrainRamp-1.0-Test by the guarded script; hashes match.
All48 QA save/receipt files and the library stayed unchanged during deployment. Game acceptance is pending.

188/353 external documents still exceed the editor's512-piece cap. Giant lamps,
staged heavy-editor loading, persistent stake jobs and admin world objects remain open.
[Evidence, dependencies and narrow manual checks](feedback-2026-10-08.md).

## QA .105 — temporary 64 m stand for the import/build/reload test

Native .104 found no free 192x192 m candidate with the required height/location
constraints; nothing was changed. .105 uses a temporary 64x64 m stand and retains
all safety checks. The larger yard remains open. An unused plan is recomputed only
without a yard receipt or backup; the operation stamp includes the smaller size.
Native placement and world reload remain unverified.


## QA .104 — location-aware site selection; native verification pending

In game, .103 passed ground preflight and refused construction/storage before any
backup, receipt or terrain mutation. .104 excludes exact native location positions,
including locations not yet spawned, using both native radii plus the existing 4 m
object margin. Loaded object/terrain checks remain mandatory. An unused old plan is
reselected only when no yard receipt or backup exists. Supplies are not reissued.
Construction and save/reload acceptance remain open.
Both builds 0/0, QA 93 cases, host contract, 853 localization keys and format passed.


## QA .103 — native terrain-edit provenance

.102 verified the native base getter, then refused a height difference beforemutations.
.103 preserves finite/current/base15.5m bounds and rejects nonempty savedTerrainComp data
and active legacy player TerrainModifiers. Generated natural flattening is not itself a
playeredit. Bothbuilds0/0, QA82, newcompiledguards, host, localization853 andformat passed.
Nativeyard/construction/reload remainopen.


## QA .102 — native blended base-height preflight

.101 teleported successfully, then refused ground preflight beforebackup/mutations.
.102 compares untouched ground with native Heightmap.GetWorldBaseHeight including biome blending.
Loaded/protected/height bounds remain enforced; refusal diagnostics identify the point/reason.
Bothbuilds0/0, QA82, compilednativecontract, host, localization853 andformat passed. Nativeyard/build/reload remainopen.


## QA .101 — native spawn teleport cooldown

.100 selected(-256,43.4301529,-768), then nativeTeleportTo refused beforebackup/terrain.
.101 waits2.1s and rechecks the original player/session before teleporting.
Bothbuilds0/0, QA82, cooldowncompiledguard, host, localization853 andformat passed.
Native .101 yard/construction/reload acceptance remainsopen.


## QA .100 — automated checks passed; native acceptance remains open

Native .99 refused before plan/terrain: bounded4096 search found minimum dry span18.5402069m.
.100 changes both native terrain bounds only for explicitly marked ZDO terrain in the
original isolated QA world: +/-16m during preparation and reload. Original token,
world name/UID, Local closed server and no peers are required. Normal build omits the
patch; unmarked QA terrain retains +/-8m. The requested192x192m footprint is unchanged.
Both builds0/0, QA82, host and localization853 passed; old99 fails the new regression guard.
This QA terrain requires the QA binary after reload; it is not a product terrain feature.
Native backup precedes terrain changes; supplies and previous receipts are never reset.
CoffeeTable construction and world-reload acceptance remain open.

## QA .99 search follow-up

Native .98 enabled noon but found no qualifying192m yard within +/-1024m; it refused
before writing a yard plan/receipt or changing terrain. .99 expands only the bounded
search/plan extent to +/-4096m (4225 candidate centres); size, dry Meadows and native
height limits remain. Both builds0/0, QA80, host, localization853 and affected format
passed. Native .99 and imported construction/reload remain open. Earlier statuses are history.

## Current evidence 2026-10-08 — QA yard .98 source verified; native creation pending

.97 native QA confirmed stations, no supply reissue, CoffeeTable import/edit/save/reopen
and cancellation. Construction refused in spawn protection; after moving out, the
workbench was out of range. No imported construction/reload success is claimed.

.98 adds an explicitly requested 192x192m distant yard and midday only to the isolated
QA build. Back up native world files before terrain changes, recheck session/ground/
objects after waits, protect crossing objects, and use separate one-shot yard/station
receipts. Same world/token/supplies retained. Both builds0/0, QA80 plus compiled guards,
HostContract, Localization853, formatting and independent/Ponytail review passed.
verify unavailable. .98 installed with matching DLL hashes; all32 existing QA files unchanged. Native yard acceptance pending.
Details and exact test sequence: docs/testing/qa-yard-0.19.98.md (Russian duplicate exists).
Earlier checkpoint/deployment paragraphs below are historical.

**English** | [Русский](ROADMAP_RU.md)

Updated 2026-10-08. Ostrix approved this order after the development review.
The goal is predictable building with real Valheim pieces, then useful group
operations and editable generators. Each stage delivers a complete workflow,
not just a button or a mathematical helper.

## Baseline and evidence

Active source and installed QA **0.19.97**. Native supplies saved complete; station preflight refused before placement.
.97 canonical station resolution: QA72/builds/host/localization/formats/independent review PASS; guarded installation passed; all22 QA-root hashes unchanged; native retry pending.
Keep the same QA saves/complete supply receipt; do not reissue. Stations/build/reload remain open.

Previous source and installed QA **0.19.96** at that checkpoint: [QA startup and first native series](testing/qa-stand-0.19.93.md).
.95 replay falsely expected slot0 after native save rotated to slot1; no grants executed.
.96 persisted primary/native identity replay and async-save boundary: QA72/builds/API/localization/format PASS.
Installed after confirmed exit; same QA saves verified unchanged. Native stand/build/reload pending. Details: current HANDOFF.

Previous source and installed QA **0.19.95** at that checkpoint:
.94 menu/isolated identity creation passed; world entry refused preparation before grants/receipts.
.95 waits for native respawn/nonzero ID, preserving strict identity matching. Regression/QA64/builds/API/
localization/format/review PASS; guarded installation passed after exit; QA files unchanged. Native stand/build/reload remain pending.

Previous source and installed QA **0.19.94** at that checkpoint:
Authorized .93 launch reproduced an early platform getter NRE before identities/grants/stations.
.94 lifecycle guard, failing-before/passing-after regression, QA64/deployment12, both builds/API/localization/
format and independent review PASS; installed hash verified. Retry ONLY manager Modded; native build/reload pending.

Previous source checkpoint **0.19.93**, source-only evidence at that checkpoint: [QA stations/package](testing/qa-stand-0.19.93.md).
Scaled visual/collider sites, no terrain changes, one-shot native stations/knowledge/world-save acknowledgement;
explicit guarded QA deployment with local pinned files and marker last. QA64 BCL/deploy12 replica/forbidden4 PASS,
both Release0/0, HostContract/Localization853/format and independent review PASS. Installed .82 unchanged.
Native activation/grants/stations/world save/build/reload/network unexecuted; first launch requires owner authorization.

Previous source checkpoint **0.19.92**, not deployed: [one-shot QA supplies](testing/qa-save-isolation-0.19.90.md).
Actual native resource plan for7 pinned fixtures, Hammer/materials/AddKnownItem, stack/count/weight guards,
started-before-grant and profile-save-before-complete. 25identity+11once+13stack BCL PASS; native compiled only.
No in-game grants executed; external staging/physical stations/chest/ready stand remain open.

Previous source checkpoint **0.19.91**, not deployed: [QA isolation and loaded gate](testing/qa-save-isolation-0.19.90.md).
Rechecks Local world UID/profile ID/ZDO player ID, closed server/no peers/no cheats before future provisioning;
25 BCL receipt/identity cases and compiled guard/coroutine wiring PASS. Runtime unproven; no grants yet.

Previous source checkpoint **0.19.90**, not deployed: [QA save isolation](testing/qa-save-isolation-0.19.90.md),
separate opt-in DLL, normal build excludes QA. Cold-storage/root/cloud guards and fresh Local identities with receipt;
source/path/compiled proof only, not native runtime. Owner-authorized QA bootstrap save creation disabled; preflight PASS.
Resources/stations/ready survival stand remain open; full goal incomplete. Installed .82.

Previous source checkpoint **0.19.89**, not deployed: [incomplete catalogue insertion](testing/missing-catalogue-0.19.89.md)
warns about missing models while retaining every record; unknown costs/stations never appear free.
Actual import/card/click/controller/Undo/Redo/Cancel/save/disk and required checks PASS. Native Session producer
is pinned by HostContract, not a game run. Installed .82; full import/build/reload and safe survival stand open.
Early native save boundary researched; overall isolation remains unproven.

Previous source checkpoint **0.19.88**, not deployed: [incomplete foreign blueprints](testing/missing-model-0.19.88.md)
retain missing records/absolute scale basis; actual editor open/edit/save preserves them without invented
dimensions. Late model resolution divides once on reopen. Store15/JSON6; world pending-basis rejection is atomic.
Release/required checks/Unity252 and independent reviews passed. Missing-mod cards/metadata/1→N, heavy editor
and full import/build/reload remain open. Installed .82.

Previous source checkpoint **0.19.87**, not deployed: ordinary/native-holder/passive/heavy world mounts show
the supplied item's own quality/variant. Confirmation binds own reference/native packet; disappearance or mutation,
including native recipe consumption, pauses attachment without fallback. Archive/editor/thumbnails unchanged.
See [current audit](continuation-audit-2026-10-07.md) and HANDOFF for evidence and limitations. Installed remains .82;
full imported build/reload and prepared survival world are still open.

Previous source checkpoint **0.19.86**, not deployed: survival ItemStand accepts the supplied same-prefab
item's own properties; exact-one native spending and exact supplied-package confirmation remain.
Archive untouched; no admin restoration. New regression reproduced on .85 and passed with28 adapter
cases/Unity252; required checks and both reviews PASS. Installed remains .82; no game/network proof or push.
[Audited matrix and dependencies](continuation-audit-2026-10-07.md),
[additional209-source corpus/format boundaries](../specs/roadmap/external-corpus-2026-10-07.md).
Full imported build/reload and prepared survival world remain open. Goal incomplete.

Previous source checkpoint **0.19.85**, not deployed: bounded typed foreign-state conversion,
full legacy mount snapshots and ZDO sign text, exact source binding and ordinary survival policy.
Original admin/ownership/damage settings stay archived, never executed. Release/required checks,
actual 353-file/133,738-packet audit, WorldLayout14 and Unity252 PASS; both reviews passed.
113 plans have no foreign-state blockers before host dependencies/scale; this is not world proof.
Appearance, headers, legacy inventories, armour/free items and placement choices remain open.
TerrainRamp-1.0-Test stays .82; see HANDOFF. Full goal is not complete.

Previous source checkpoint **0.19.84**, not deployed: full shared-template world ghosts up to 50,000 parts,
intrinsic prefab-scale parity and faster composite socket clustering. Editor remains limited to 512.
Release/required checks, WorldLayout14 and Unity252 PASS; independent correctness and ponytail reviews passed.
No native world/FPS proof. Unsupported item effects fail closed; foreign ZDO/containers/armour/contents choices
and the live-host lamp remain open. TerrainRamp-1.0-Test remains .82. Details: HANDOFF; goal not complete.

Previous source checkpoint **0.19.83**, not deployed: native world custom-point persistence and known
foreign ItemStand recipe conversion. Release/required checks and Unity252 PASS; WorldLayout14
includes repeated custom-point ownership. Independent reviews passed. Real Valheim persistence
and the remaining heavy/foreign-state/contents/lamp goals are not claimed complete. See HANDOFF.

Previous checkpoint **0.19.82**, installed only in TerrainRamp-1.0-Test: TMP control-escape parsing
disabled for literal Windows paths; compact folder display and expanded Edit path field replace clipping.
The owner's .81 screenshot exposed a missed regression: \\nfspr and \\test-fixtures became LF/TAB.
Release/HostContract/Localization846/world-layout/deployment guards and Unity252 PASS; Russian collapsed/
expanded screenshots inspected; independent correctness and ponytail reviews passed. No game/save/push.
[Short Russian acceptance steps and file explanations](testing/player-exchange-0.19.82_RU.md).
Remaining import/export/world goals below are unchanged; real Valheim acceptance is pending.

Previous source checkpoint **0.19.81**, installed only in TerrainRamp-1.0-Test: editor opening no longer applies
world-only payload/socket gates; its single-line path claim was disproved in game and corrected in .82. Close is at upper-right.
The Hammer read-only blueprint report exposes editor/world reasons and actual prefab scales. Six named test files
are installed with SHA256 checks. Release/HostContract/Localization843/Geometry/Store/bridge/layout/deployment guards
and Unity228 PASS; independent correctness/pony review found no blockers. No game/save/push.
[Exact prepared player actions](testing/player-exchange-0.19.81_RU.md). Huge lamp cause, >512 scenes, foreign decoding,
world contents/sockets and placement-time contents choices remain unfinished. See HANDOFF for verified hashes.

Previous source checkpoint **0.19.80**: native ItemStand world placement consumes a real matching inventory item only after
native construction costs, confirms exact saved contents and preserves later exact-item reservations. Owner/access,
queue/orientation and uncertain rollback guards are implemented. Release/required checks, Unity228 and 26 adapter cases PASS;
native world/drop/save/client acceptance remains required. Native14/portable5 unchanged. On 2026-10-07, .80 was installed
only in TerrainRamp-1.0-Test; version 0.19.80.0 and both SHA256 hashes verified. No game/save/push.
[Combined owner acceptance: 22 cases](testing/full-acceptance-0.19.80.md). Older entries retain historical deployment status.
Foreign state, containers, armour/free items, editor scenes above 512 and durable world sockets remain unfinished.

Previous source checkpoint **0.19.79**: ItemStand capture rejects mid-read ZDO revision changes and invalid/rebound views,
including legacy and empty-to-populated transitions; bounded raw bytes are privately copied. Release/required checks and
228 isolated Unity scenarios PASS. World restoration is not implemented; native queue/cost/confirmation boundaries are
recorded in HANDOFF. Native14/portable5 and installed .67 unchanged; no deploy or game/save changes.

Previous source checkpoint **0.19.78**: native ItemStand v109 is fully inspected by the shared immutable item reader.
Capture/clear validate the complete packet and prefab/quality/variant/one-item metadata; malformed archival bytes remain
retainable, not clearable/executable. Storage/mount preflight stays independent, including unreadable mounted evidence.
Geometry113, Store, Release/API and 228 isolated Unity layouts/input checks PASS. Native world restoration, foreign-state
decoding, armour/free items, heavy scenes above 512 and durable sockets remain pending. No deploy; installed .67 unchanged.
Exact evidence: HANDOFF.

Previous source checkpoint **0.19.77**: import also reviews the whole file/archive and independent storage/mount choices before writing.
One atomic native ZIP retains exact original bytes as inert evidence. Changed sources, blocked archive members and failed conversion
write no partial copy; prefab-base scale normalization runs on the host thread. Whole-library budget/order/duplicate-ID multiplicity,
2000/2001 files and projected ZIP-entry caps are verified before publishing. Store tests and 228 isolated Unity layouts/input checks PASS.
Store14/portable5 unchanged; installed .67 unchanged, not deployed. Native world restoration, foreign state decoding, armour/free items,
heavy scenes above 512 and durable world sockets remain pending; the full goal stays active. Details are in HANDOFF.

Previous source checkpoint **0.19.76**: shared editor/Hammer export contents review shows counts/unknown/previous clears
and independent storage/mount choices before explicit confirmation. Back/cancel/source retention and partial unreadable-storage
eligibility are verified by real UI interactions. 204 isolated Unity layouts, including Russian review screens, and required checks PASS.
Store14/portable5 unchanged; installed .67 unchanged, not deployed. Direct import policy/multi-document conversion and world restoration
remain pending. Details and exact proof are in HANDOFF.

Previous source checkpoint **0.19.75**: Store v14 / portable v5 preserve independent storage/mount clear intent
without changing original packages or sources. Effective dependencies, editor visuals/tree/tools and Undo follow that intent.
Four export combinations, migration/rejection/atomicity, Release/112 Geometry/required checks and independent reviews PASS.
Choice/count/preflight UI and direct import policy remain pending; native world-state placement is still gated.
Installed .67 unchanged, not deployed. Details are in HANDOFF and the format contract.

Previous source checkpoint **0.19.74**: bounded read-only native v109 inventory inspection validates full records/dependencies
before capture. Immutable item fields retain exact custom strings; canonical encoding, zero/colliding hashes, cells/counts
and full consumption are checked without Inventory.Load, normalization or world writes. Native custom-map overflow rejects early.
Release/111 Geometry/Store/required checks and final isolated Unity180 plus capture fixtures PASS; independent reviews PASS.
Installed test .67 unchanged; policy UI, foreign storage conversion/restore and other remaining goal scopes are not complete.
See HANDOFF and the [format contract](../specs/roadmap/blueprint-format.md) for exact boundaries.

Previous source checkpoint **0.19.73**: native Container world capture is read-only, permission-gated and revision-checked.
Exact packages/capacity/item dependencies survive Store v13/portable v4/copy/history; known initialized empty
chests are distinct from unavailable data. Nested/derived/special/multiple storage and unsupported state reject atomically.
Release/110 Geometry/required checks and 180 isolated Unity states plus capture API fixtures PASS; no native game proof.
Installed test remains .67. Retain/clear UI, foreign-container decoding, armour/free items and native restoration,
heavy interactive scenes above 512 and durable world sockets remain unfinished. Capture limits are in HANDOFF.

Previous source checkpoint **0.19.72**: compatible player-known ItemStand items are authored from Tools through the existing catalogue.
One click attaches to an empty holder; populated replacement/removal requires confirmation. Undo restores the full original package;
the tree shows contents under the holder. Invalid mod-item records skip individually; missing native attach nodes are filtered.
Release/109 Geometry/required checks and 180 existing Unity layout states plus authoring interaction checks PASS; correctness/Ponytail PASS.
These images do not establish attachment-mode visual or Valheim acceptance. Store v13/portable v4 and the world-state guard remain unchanged.
Not deployed; test .67 unchanged. Ordinary free items, armour slots/poses, storage retain/clear prompts, native world restoration,
heavy interactive scenes above 512 and durable world sockets remain unfinished.

Previous source checkpoint **0.19.71**: world selection captures native ItemStand contents without modifying the stand or inventory.
The actual native view (including override), signed item hash and visual metadata are read; the full item-data package is retained
in the existing part payload with the validated `valheim-itemstand-v1` tag. Native/portable/copy/history preserve it unchanged;
portable dependencies include the attached item prefab. Missing/invalid/oversized state and armour stands reject saving explicitly.
Release/required checks and 180 fresh Unity states PASS; no native item package is executed or restored.
Store v13 / portable v4 and the shared world-placement guard remain unchanged. Not deployed; installed test remains .67.
Item authoring/catalogue/tree relations, armour slots/poses, world restoration, heavy scenes and world-socket durability remain unfinished.

Previous source checkpoint **0.19.70**: read-only ItemStand contents preview in the editor, thumbnails and rotatable preview.
Known PlanBuild item identity/variant/quality/orientation use verified native attachment rules on renderer-only clones;
horizontal/wall offsets, holder XYZ transforms, selection/Undo/copies, cache changes and missing/invalid-state reasons are Unity-tested.
Store v13 / portable v4 and the world foreign-state guard remain unchanged; no inventory/network/scripts are executed.
Release/required checks and 180 isolated Unity states PASS. Not deployed; installed test remains .67.
Full item catalogue/attachment authoring, armour rig/pose parity and native world reconstruction remain unfinished.

Previous source checkpoint **0.19.69**: part-bound foreign info/ZDO/chance and unknown source sections survive editor/history/native v13/portable v4 exchange.
Release/required checks and 180 isolated Unity states PASS; eight EditorBridge checks include real save/reopen.
Contents are inert and not displayed/restored; geometry editing is available, unsupported world placement blocked.
Independent correctness/Ponytail PASS. Not deployed, installed test remains .67. Full item/holder behavior is unfinished.

Previous source checkpoint **0.19.68**: compatible foreign imports retain exact valid UTF-8 source text and piece-row bindings in Store v12 / portable v3.
Release/required checks and 180 isolated native Unity states PASS; seven EditorBridge checks include actual controller save/reopen.
Content evidence is format + basename + text, not original archive/path identity; source text never executes.
Unsupported foreign object state/unknown sections, ordinary items/holder contents, heavy interactive scenes and world durability remain pending.
Not deployed; installed test version remains .67. Independent reviews are recorded in the current handoff.

Previous source checkpoint **0.19.67**: shared file browser/output settings, persistent result and exact foreign prefab scale.
Release/required checks, correctness/Ponytail and 180 isolated native Unity states PASS.
Installed in TerrainRamp-1.0-Test through the guarded script, DLL hashes verified; no Valheim launch.
[Player checks](testing/catalog-exchange-0.19.67.md). Faster scrolling/object-only support tree retained from .66.
The [lossless conversion requirement](../specs/roadmap/blueprint-format.md) is partially implemented:
source absolute scale is prepared atomically without shrinking; opaque foreign object/header preservation remains pending.
Heavy interactive budget 512 and world-support durability remain separate unfinished work.
Ostrix's 2026-10-06 goal extension: ordinary items and horizontal/wall/armour stand contents,
including editor attachments, preserved state/exchange and native world reconstruction; read-only ItemStand preview and native capture/preservation are implemented.
Select ordinary items from an item catalogue, place them freely or attach to a compatible holder/armour slot;
show the relationship in the tree/inspector and preserve it through holder movement, copy, Undo and exchange.
Ostrix's additional 2026-10-06 requirement: import/export must inspect storage inventories and ask retain/clear
for the resulting copy, with an independent retain/clear choice for stands, item mounts and armour displays.
Originals/world objects stay untouched; unknown storage is not silently emptied. Policy UI/restore remain pending.
See the storage policy in the [format contract](../specs/roadmap/blueprint-format.md).

Previous source checkpoint **0.19.64**: [independent support editor](../specs/roadmap/blueprint-support-frames.md).
Tree/inspector, G/R without moving parts, transient centre and persisted placement default.
Release/target checks, correctness/Ponytail and 156 Unity states PASS.
[Six player cases](testing/free-supports-0.19.64.md); not deployed for native game acceptance.
Quiet unselected markers and world persistence/reload/network remain. Installed .56 unchanged.

Previous source checkpoint **0.19.63**: [Hammer support-frame composition](../specs/roadmap/blueprint-support-frames.md).
Shared inverse pose for ghost visuals/collider/native snaps and geometric root; F9 bounds pivot remains separate.
Release/target checks and actual nested/mirrored Unity proof plus 144 states PASS; correctness/Ponytail PASS.
Not a complete player workflow: tree/G/R/transform centre/world durability pending, installed .56 unchanged.

Previous source checkpoint **0.19.62**: [support storage/exchange](../specs/roadmap/blueprint-support-frames.md).
Store v11 / portable v2, legacy migration/clear/rollback and real rotated support disk roundtrip pass.
108 Geometry + six EditorBridge + required checks/review and fresh 144 Unity states PASS.
Not exposed as a player workflow: placement-frame math/tree/G/R/world durability remain; .56 unchanged.

Previous source checkpoint **0.19.61**: [independent support data foundation](../specs/roadmap/blueprint-support-frames.md).
108 Geometry + five EditorBridge + Release/targeted checks and 144 Unity states PASS.
Not exposed/persisted yet: Store v10/portable v1 unchanged; tree/G/R and v11/v2 next.

Previous source checkpoint **0.19.60**: [shared pick geometry](../specs/roadmap/blueprint-render-performance.md).
512 walls: pick-array payload 50071344 → 107808 bytes, sync 550 → 223 ms; renderers unchanged.
144 real-size Unity states PASS; no game proof/deployment, budget 512 unchanged.
Free root support workflow approved; implementation and world durability remain.

Previous source checkpoint **0.19.59**: [bounded material-sharing measurements](../specs/roadmap/blueprint-render-performance.md).
512 actual walls: 10752 → 2 materials, sync 906 → 520 ms; renderer count unchanged.
Fresh 144 Unity states PASS, gameHostTested=false; interactive budget remains 512.
Heavy rendering/free-support authoring/world persistence/export remain incomplete.
Previous source checkpoint **0.19.58**: [native signs and evidence](../specs/roadmap/blueprint-signs.md).
v10/portable v1 preserve payloads; 109/353 external entries pass format. Native sign
integration is source/adapter-tested; actual world/reload/network acceptance remains.
[Free support frames](../specs/roadmap/blueprint-support-frames.md) are researched,
not implemented; socket persistence, heavy Unity rendering and export UI remain.
Installed **0.19.56** is unchanged; the older figures
below describe their respective historical builds, not current source limits.

Installed verified build: **0.19.55**, Store v9, Valheim 1.0.16.
.54 catalog acceptance: 2,3,5,6,7 accepted; 1,4,8 revised. .55 shrinks catalog
contents 20%, reserves hints space, separates Lighting and Furniture families,
filters actual recipe materials/tiers, adds icon costs/construction station and
name/category save confirmation. Gizmo family settings close immediately and
survive part selection in both sizing modes. 144 Workbench cases and targeted
checks pass; independent review PASSED. Installed only TerrainRamp-1.0-Test.
Current owner series keeps stable IDs 1–10; only 1,4,8,9,10 require replies.
Valheim is not tested. No BW-02/curves advance.
Verified test build **0.19.56** transfers external `.blueprint`/`.vbuild` sources
into detached editor drafts/insertion/native Hammer copies. Originals stay untouched;
terrain is excluded and unsupported object data blocks the whole file.
BiomeBlueprints: 353 cards, 62 format-compatible, 291 with rejection reasons;
host prefab availability remains separate. Build, targeted checks, source review and
144 Unity UI states pass after licence activation. .56 and inert ZIP deployed only
TerrainRamp-1.0-Test with verified SHA256; Valheim acceptance remains pending.
The 128 limit, #SnapPoints and object data need further support;
full BW-04/BW-05 export/interchange is not completed.
See blueprint-exchange-research.md and artifacts/unity-workbench-capture.log.

Historical .54 baseline:
Ostrix accepts .53 chair checks 1–2. The approved catalog now has Atlas/Quick
views, one-click placement, shared view filters, per-tab query state, named cards,
five main sections and workstation-linked upgrades from native StationExtension.
Unknown mod categories remain in All/search; current unlocked hammer inventory
is authoritative. Favorites/recent are editor-session only. Original control
icons compact favorites, details, reset and close; no new dependency/plugin.
132 Workbench states pass at four resolutions and 100/120/140% UI scale;
actual overlay raycasts use matching GameView sizes, camera mode is capture-only.
Long preview labels wrap, reserved user category IDs have a regression check.
Release/targeted checks and independent review pass. Valheim is not yet tested;
BW-01 awaits the single catalog game series 1–8. No BW-02/curves advance.
Ostrix's .49 report accepts the core point/pivot/frame/size workflow and resource
cards; optional B, helper opt-in and catalogue translation needed clarification.
.50 defaults to model-relative 3D sizing, retains screen mode, uses amber native
clasps and newly authored ivory helper knots with a distinct selected locking
collar. Near-cursor reveal, exact coordinates and untinted selection remain.
One Native / Native+ours filter now governs G sources, Q/E and targets; explicit
helper A remains an intentional exception. Optional B moves into Attachment and
still defines X along A→B with pivot at A, not midpoint or curve construction.
Experimental feature-endpoint markers are opt-in, bounded, editor-only and excluded
from saved anchors. Native catalogue type/material labels translate by stable ID;
external names stay intact. Ostrix accepts .50 checks 1–2, 4–5, 7 and 10;
3/9 reopen real mesh corners, 6 view depth, 8 helper capture.
.51 introduced aimed 2 m capture/adjacent switching and the approved View Z handle;
Ostrix accepts its checks 3–6. Checks 1–2 exposed the incorrect replacement of ordinary
bounds/middle helpers and duplicated large marks. .52 restores the ordinary set;
optional sharp mesh corners use small translucent dots, skip ordinary/native overlaps
and buried closed-component corners, including combined meshes. Legacy Array/save
anchors remain unchanged. Ostrix accepts .52 checks 1–2; the chair screenshot
reopens exact rendered corners. Native piece_chair02 uses decorative shader noise
while grips use undeformed vertices. Ostrix chose exact editor geometry: .53
disables native Piece ripple only on owned preview materials. Native chair draw,
point picking and helper magnet pass; Ostrix accepts .53 checks 1–2.
Older lists are archived, not extra answer lists. F9 mechanics are unchanged.
0.19.39 owner feedback accepts menu hover, language refresh and camera framing;
Transform selection is provisionally accepted. The owner accepts contour cleanup;
the focused 0.19.41 game series is accepted by owner report on 2026-10-01.
New requests concern collapsible hints, manipulator clutter, hidden attachment
context and compact viewer settings. Keyboard/tool access is approved in principle;
Ostrix approved the Scandinavian manipulator form study on 2026-10-01.
The full editor implementation is authorized. Owner feedback supersedes the .43
first-G gate: .45 keeps the original model material and shows the combined gizmo immediately.
G starts surface Move; R/S/axes retain exact operation-start numbers. Editor
handles are 25% larger. Vanilla points are brighter closed gold diamonds;
generated helpers, including midpoints, are smaller open coloured marks.
Current game tests have one answer list; older scenarios are archived.
Ctrl+1–4 selects tools; MMB click deletes in Select/adding while
MMB drag orbits. Six original engraved SVG/texture assets, filled scale and
screen-distance hover feedback extend the accepted Scandinavian forms.
Collapsible hints, compact dropdowns and active hidden-target context remain.
Ostrix's .45 report on 2026-10-02 accepts tests 1, 3–6 and 8–11, plus language
refresh in 12. Ostrix approved the [design](../specs/roadmap/snap-art-and-hud-proposal.md)
on 2026-10-02. Candidate .46 implements six thematic PNG point signs, Auto
surface movement with native-only Q/E/opt-in helpers and an Attachment chooser,
plus native resource/station icon cards with bounded overflow. Automated checks
and independent review pass; BW-01 awaits the focused owner series 1–8.
Owner screenshots of .46 reopen point/gizmo readability and HUD header spacing.
.47 increases their idle contrast and screen size; native card rows now start
below the measured multiline description. Shapes, snap positions and costs are
unchanged. BW-01 remains gated on game readability before the full 1–8 series.
Earlier .45 plain shapes above are historical, not the .46 artwork.
.48 replaces six PNGs with broad forged motifs and open centres. Gold native
radius is 16 px; coloured helpers 9 px. Pin/active states overlay instead of
replacing type; hover names it. Neutral colour and mipmaps replace RGB overdrive.
Workbench import now matches embedded texture dimensions/filtering. Coincident
colour/picking and 81 UI cases pass; BW-01 waits for owner point readability.
All development deployments, upgrades and game tests use **only
`TerrainRamp-1.0-Test`**. `Default/BuildWorks` is the source/artifact tree,
not an installed mod. Do not modify other profiles, mods or saves as a side effect.

Existing capabilities: indexed Hammer/native favorites; isolated editor;
numeric transforms, uniform scale 1–400%, nested groups, independent group/world
anchors; Array/Contour, Undo/Redo and contextual hints; Q/E source selection,
2 m target preview/aimed editor capture; 0.55 m unaimed magnetic capture; native world-series placement and
cancellation. Missing-prefab visuals already exist.

Release, Geometry, Store, EditorBridge, WorldLayout, localization and HostContract
passed on 2026-09-29. This is not current game acceptance. Earlier English and
slanted-piece Q/E acceptance remain historical. The short 0.19.38 smoke in another
profile does not close the dedicated-profile gate. Current checkpoint evidence
is recorded in HANDOFF and the [active pass](../specs/roadmap/current-pass.md).

## Status rules

Planned → In progress → Automatically verified → Awaiting owner → Accepted.
A regression reopens the affected gate. Record date, commit, game version,
profile, DLL hashes and untested scenarios. A build or screenshot never closes
Russian UI, save/reload or multiplayer checks. One bounded stage is active;
dependent changes wait for its gate, unrelated preparation may continue.

## Ordered stages

| ID | Deliverable | Dependency | Status |
| --- | --- | --- | --- |
| BW-00 | Dedicated-profile baseline and reliable handoff | Current core | Focused 0.19.41 owner series accepted; cost/refund accounting and networking pending |
| BW-01 | Consistent input, clear pivots/snap/working plane, compact F9 | BW-00 | .53 chair series accepted; .54 catalog/layout/native UI input automatically verified; awaiting catalog game series 1–8 |
| BW-02 | Group layout operations without resizing pieces | BW-01 | Planned; focused operation UI approval required |
| BW-03 | Persistent Array and minimal recipe persistence | BW-02 | Planned; safe Store migration and editing/Bake gate |
| BW-04 | Guides and curves in accepted slices | BW-03 + renewed owner discussion | Tool-semantics discussion required before coding |
| BW-05 | Safe exchange and dependency recovery | Stable Store; BW-03/04 before exchanging recipes/guides | Native14/portable5 exchange and .80 native mount adapter automatically verified; foreign-state decoding and world/save/client acceptance pending |
| BW-06 | Linked instances, original adaptive pieces, explicit terrain compatibility | Proven generators, persistence and relevant network gates | Deferred; separate contracts |

No promised dates or release numbers before scope and evidence are known.

## BW-00 Establish the current baseline

Enforce a single deployment target; test forbidden targets. Use the dedicated
profile's BepInEx/camera for host checks. Synchronize the existing source-only
GitHub checkout. Build, verify and install the DLL pair only while Valheim is closed.

Owner series: Hammer/favorites; editor menus/input; wall, sloped beam, roof and
furniture contact; Q/E above/below grid; fresh Array/Contour; independent anchors;
Esc/resource rollback; editor save/reopen and world reload; camera near-ground
movement/teardown; English/Russian. Multiplayer is a separate host/remote series
before new world/network behavior, not a blocker for preparing an isolated UI proposal.
Inspect missing/unsupported parts and explain blocking errors; preserve placeholder
data. Full dependency manifests belong to BW-05.

## BW-01 Make existing controls understandable

Latest owner feedback confirms furniture Q/E, nearby targets, below-grid placement,
group pivots, Array, two-part save/reopen, world reload and near-ground camera.
The partial-build Esc cleanup restores the same ghost, but returned resources
drop on the ground; exact cost/refund accounting still needs a count-based check.
One-part saves remain deliberately blocked by the current two-part blueprint contract.

The next UI approval covers an explicit category chooser (cycling a single category
looks inert), aggregate resources and required stations in the native piece panel
instead of a Resources popup, a per-mode shortcut settings page with rebinding,
and double-click Outliner framing while retaining F. Do not auto-jump the camera
after every placement. 0.19.41 draws the contour guide at full edge endpoints,
not the cursor's relative point. The approved F7/button hides blocking editor
visuals entirely; selection stays solid, temporary occluders do not intercept
selection, and document visibility/materials are unchanged. Sampled mesh rays
are still a heuristic, not pixel-perfect occlusion. Six labeled hint groups,
editor-only FOV slider/input/game reset and compact tree rows with anchor badges,
eye/lock columns and an overflow scrollbar are automatically verified. Their
design is accepted by owner report; FOV is disabled in orthographic view.
The [next editor proposal](../specs/roadmap/editor-visibility-proposal.md)
compares three ways to reduce manipulator clutter and recommends explicit active
control families with matched hit tests, collapsible hints and compact viewer
dropdowns. A target-only outline could retain attachment context under F7;
interior snap eligibility is a separate decision. These editor changes are implemented
in .42 after owner approval; they await the focused game series, not another design vote.
The [approved input revision](../specs/roadmap/editor-input-revision.md) replaces
.42 direct-action/plain-digit/MMB-navigation-only controls. In .44 selection shows
Combined immediately; G starts Surface Move, R/S start actions, Ctrl+1–4 selects
tools and MMB click deletes in Select/adding. One Space
popup with F3 search remains. Future tools must not renumber existing bindings;
modal digits belong to numeric entry. Typed values replace mouse deltas from the
original operation snapshot, with exact cancel/one Undo and next-step guidance.
G follows real surfaces excluding the selection; a ray miss
cannot confirm or silently use distant ground. P explicitly chooses the screen
plane; R uses the camera-facing plane until constrained, S is uniform. XYZ chooses
an axis, repeated XYZ switches World/Local, Shift+axis excludes that axis.
Q/E and wheel retain source-point/yaw behavior in surface placement. Independent
Move/Rotate/Points/Scale filters have matching visibility and hit tests; selection
shows Combined by default and Array retains its controls. Tooltips use actual glyph measurements and bounded
wrapping; EN/RU fit and supported scales remain part of owner UI acceptance.
Community translation overrides
need a documented file format and fallback/reload tests before shipping.

Approve a small wireframe before visible UI/icon changes. Distinguish placement
anchor, transform pivot, local/world axes and source/target snap points. Select
with editable objects keeps numeric fields; multi-selection explicitly labels
deltas. Bottom hints show the action/state and keys available now.

Decide explicitly between fixed Y=0 and a chosen working plane; auto-grounding
must not look like changing document coordinates. Always show the active snap
target, including crowded scenes with over 24 candidates. Keep 2 m preview separate
from 0.55 m capture; do not blindly change prefab origins.

Acceptance: no UI click-through construction; text owns typing; context menus
survive the intended gesture; gizmo/numeric changes agree; below-grid objects
remain visible/reachable; EN/RU panels contain all controls at supported UI scales.
Reuse existing views/math. Compact F9 comes here, not at the end of the roadmap.

## BW-02 Edit group layouts without changing size

Distinct operations: equal local per-element translation, fixed radial offset,
and position-only layout scaling. Target each piece or each immediate child
assembly as a whole. Expand selection by prefab/children/group and inversion;
then Align min/center/max, Distribute and duplicate Mirror.

Acceptance: four beams move inward without changing geometry size; intended
rotations and child-assembly distances survive; only intended nodes change;
preview/cancel/apply and one Undo. No general recipe framework or Store bump
is needed for these direct commands.

## BW-03 Save and re-edit an Array

Reuse existing Array math. Add only the first generator's recipe data: canonical
source subtree, parameters, deterministic logical IDs, anchor mapping and explicit
Bake. The [recipe contract](../specs/spec-pattern-recipes.md) defines safety,
not an obligation to ship every possible operation immediately.

Acceptance: count/spacing editable after reopen; no drift/lost hierarchy;
preview=commit=Bake; appropriate Undo/order handling; safe v9 migration;
unknown future recipes are preserved or blocked, never silently discarded.

## BW-04 Discuss guides before implementation

First agree with Ostrix: magnetic frames for manual construction, repeated-group
generation, or both; control-point editing, spacing/count, orientation/roll,
source/pivot, open/closed paths and persistence. **This includes Line A/B.**

After approval: Line A/B → polyline → arc → Bezier, each saved, editable and
cancellable. Existing curve tests do not approve the tool. Ordinary pieces stay
rigid. Adaptive original geometry needs a later mesh/UV/collider/snap/network
contract. Move Contour to a common evaluator only after that evaluator is proven.

## BW-05 Exchange without data loss

Versioned import/export, dependencies, ready/blocked reasons, identity collisions,
missing-piece recovery and lossless transforms/groups/anchors/metadata. Unknown
input must not overwrite valid documents. No automatic mod installation or
third-party mesh/texture extraction.

## BW-06 Expand from demonstrated needs

Linked instances with explicit Make Unique; original adaptive parts/materials;
then cooperation with a separately maintained terrain mod, without hidden earth
changes. No near-term node graph, expressions, full Fields/dynamics, arbitrary
external references or third-party mesh deformation.

## Checkpoints and GitHub synchronization

After each verified step: review the diff, run affected checks, obtain independent
correctness review for non-trivial changes, update EN/RU status, commit a working
checkpoint and push normally to [MaikiOS/BuildWorks](https://github.com/MaikiOS/BuildWorks).
Ostrix authorized this on 2026-09-30. Preserve unrelated changes. Publish only
source and safe docs: no force-push, saves, game DLLs, personal logs/paths or
automatic binary releases. On sync failure keep the local commit and report
the actual error, never claim success.

Pause for owner UI/icon approval, the prepared game-test series or the renewed
curve discussion. Resume from the recorded checkpoint, not archived STOP text.
Report new access/safety blockers truthfully. Deployment does not authorize
launching the game or changing saves.
