# Blueprint format and large-construction work

**English** | [Русский](blueprint-format_RU.md)

Current source **0.19.77**: shared import preflight snapshots and validates every file/archive member before library writes.
Counts and storage/mount keep-clear choices cover all documents. Confirmation normalizes foreign absolute scales once on the Unity
thread, converts private recipes to native14/portable5 and atomically publishes one ZIP. original/source.bin and original/name.txt
retain exact input bytes/name inertly; archive paths are never extracted. Original files/current documents/world remain unchanged.
Changed source files, any blocked member and conversion failure reject without partial library output. Keep never resurrects cleared state.
Short sibling staging paths avoid the Unity Windows path-length failure reproduced by actual UI acceptance.
The full candidate library is loaded in exact final filename order under the shared 128MiB budget before Move;
every previously valid and incoming stable ID is retained with multiplicity, including identical duplicate ZIP names.
Exactly 2000 files fit; 2001 reject. Review also reserves two inert entries and paired previews under the ZIP-entry cap.
Store/required checks and 228 isolated Unity EN/RU layouts/input scenarios PASS; native world restoration remains gated, not game proof.

Previous source **0.19.76**: shared export preflight reads effective counts in a background task and asks independent storage/mount
choices before confirmation. Back/cancel never write; destination/format remain fixed during review.
Unreadable native storage disables storage clearing, not valid mount clearing; opaque unknown state disables both.
Already-cleared raw storage is re-inspected; no archival contents are reported as effective.
Store14/portable5 unchanged. Required checks/reviews and 204 isolated Unity EN/RU layout states PASS; not game proof.

| Scenario | Implementation | Evidence |
| --- | --- | --- |
| Export counts/independent keep-clear/Back/cancel/error | CompositeBlueprintStore.InspectContents; BlueprintExchangeView | Store + ControllerInputAcceptance.RunExchangeLifecycle; Unity 2026-10-06T19:23:45.645088Z |
| English/Russian review layout | Existing shared exchange window; catalogue test adapter | 1280–3440 px, UI100–140%, 24 new review captures |
| Import policy and atomic multi-document conversion | ReviewImportFile; TryImportReviewed; shared exchange window | Store: four choices, mixed/stale/unknown/budget/duplicate-ID rejection, exact source and cache IDs; actual UI import choices/Back/retry; Unity 2026-10-06T19:49:59.0236127Z |
| Native world restoration and remote/save-load behavior | Pending; foreign-state guard unchanged | Not achieved |

Previous source **0.19.75**: Store v14 / portable v5 add optional contentsCleared for native Container/ItemStand only.
Independent storage/mount export choices act on a validated private copy. Exact raw Info/ZDO stay archival;
requiredPrefabIds reflects effective contents. Retain never resurrects a prior cleared copy.
Old v13/v4 inputs default to retained contents; old-schema new fields and invalid/duplicate/unknown flags reject.
Clearing storage requires full native v109 inspection with verified hashes; unknown/unreadable/opaque data
rejects clearing atomically with no output and no original mutation. Preview/tree/tools and Undo preserve the distinction.
Release/112 Geometry/Store/required checks and independent reviews PASS; no world reconstruction or game acceptance.
Choice/count/preflight UI and direct import policy remain pending. Installed .67 unchanged; no deployment.

Previous source **0.19.74**: read-only native v109 record inspection returns detached immutable item fields, including exact
custom strings and crafter data, while original bytes remain authoritative. Strings/counts are bounded before allocation.
Cells, occupied count, total units and every dependency are verified; zero/colliding/undeclared hashes, invalid UTF-8,
noncanonical optional/default fields, string/custom-map encodings and incomplete/trailing records refuse with no partial output.
Capture gates on this inspection after exact raw/private Save equality and rejects native custom maps above 32767 entries.
Installed native serializer/loader/hash/count and BinaryWriter primitive IL are pinned by HostContract. Native v13/portable v4 unchanged.
Existing inert packages are preserved without execution even if inspection fails; future exchange policy/world restore must inspect
again rather than treating descriptive metadata as proven contents. No Inventory.Load or reconstruction was introduced.
Release/111 Geometry/Store/required checks and final Unity180/capture fixtures plus independent reviews PASS; not game evidence.
Retain/clear UI, foreign-container decoding and world restoration remain pending.

Previous source **0.19.73**: world selection preserves an ordinary root-owned native Container's exact inventory bytes
without writing live state. Access/ward/busy/revision and native prefab-name resolution are checked; derived/nested/special
storage refuses explicitly. Initialized fresh empty chests have a verified canonical empty packet; unknown absence is not empty.
Existing foreignData tag valheim-container-v1 contains width:height:occupiedSlots:units, sorted unique prefab names separated
by |, and exact canonical Base64 bytes. Source names containing | refuse capture; no escaping or silent dependency splitting.
Current packet version109/4096-slot/1-MiB bounds and a 16-MiB aggregate capture budget reject, never truncate. This manifest
is declarative only: raw records are not decoded, executed or restored. Future restore must validate every item against them.
Native v13/portable v4/copy/history preserve >32K packages and include item dependencies. Existing envelope fields are unchanged;
old readers reject the unknown tag instead of silently losing it. Release/required checks and 180 isolated Unity cases plus
read-only capture API fixtures PASS, not native Valheim save/reload. Retain/clear UI and foreign decoding/world restore remain pending.

Previous source **0.19.72**: editable native ItemStand has an attachment catalogue of compatible installed player-known items.
Tools attach/remove; empty-holder click commits a default recipe (quality 1, variant/orientation 0), populated-holder replacement/removal
requires confirmation. Renderer-only preflight, effective locks/visibility and stale-reference checks prevent partial edits.
Undo restores the full previous package; holder movement/copies/native/portable/history use the existing part payload.
Contents appear as a holder-selecting child row, not an independent piece. Invalid mod-item names skip individually with warning;
items without native attach nodes are filtered. No new schema, item inventory, world restoration or foreign execution.
Release/required checks and existing 180 isolated layout images plus authoring interaction tests PASS, not new-mode/game visual acceptance.
Ordinary free items, armour slots/poses, storage policy UI and world reconstruction remain unfinished.

Previous source **0.19.71**: read-only native ItemStand capture is integrated into world selection before any blueprint write.
The actual private native view respects `m_netViewOverride`; signed prefab hashes resolve through ObjectDB.
The existing part payload accepts `valheim-itemstand-v1`: plain `prefab:variant:quality:orientation` in info,
canonical Base64 of the exact full native `s_itemData` bytes in zdo, no chance. Visual metadata is validated;
the package is size/header/encoding checked but never decoded, executed or claimed behaviorally valid.
Legacy absent item packages remain absent; empty stands have no contents payload. Missing items, invalid data,
oversized packages and ArmorStand slot/pose state reject the whole save instead of silently losing data.
Native v13, portable v4, controller copies/history and save/reopen preserve exact immutable state.
The portable dependency list also includes native captured item prefabs; altered omissions reject.
This new tag needs no envelope-shape migration; older readers reject it rather than silently dropping it.
Only visual metadata is used by the existing renderer-only preview. The world foreign-state guard stays closed:
no inventory consumption, arbitrary ZDO writes, ItemData.Load, item creation or native restoration occurs.
Build/host contracts, Store/Bridge checks and 180 fresh isolated Unity states PASS, not Valheim capture/reload proof.

Previous source **0.19.70**: renderer-only ItemStand preview is implemented in Scene/thumbnail/rotatable preview.
The verified PlanBuild four-field info schema is decoded for installed items only. Native read-only helpers select
the target/attach subtree/orientation offsets and quality scale; native ItemStyle changes owned materials only.
Invalid/missing/unsupported state reports a reason in the editor and retains raw data. Particle effects,
unknown equipment scripts and unsupported renderer types fail explicitly; manual previews surface the reason,
and failed thumbnail refresh invalidates generated disk cache. No gameplay prefab, inventory, ZDO, RPC
or native SetVisualItem is created/executed.
Preview caches rebuild when immutable foreign payload changes; thumbnail cache names version the content renderer.
Build/host contract and isolated Unity fixtures pass; these prove supported static preview rules, not native gameplay parity.
ArmorStand requires an inert skeleton/bone-binding/pose fixture: current VisEquipment/SetPose paths require network data
and scripted cloth/effects. Full catalogue/attachment authoring, typed native item state and world restoration remain pending.
Store v13 / portable v4 and the shared foreign-state world-placement guard remain unchanged.

Previous source **0.19.69**: Store v13 / portable v4 retain inert per-part foreign info/ZDO/chance;
unknown root sections remain exact source text. Editor transforms/copy/history/save/exchange preserve them.
Source-only v12/v3 migrate. Native execution/display adapters are not implemented: editor warning and shared
world guard prevent silently placing empty holders or dropping object behavior. Geometry editing is available.
This is preservation, not full behavioral compatibility or ordinary item support.
The source hash/row binding does not prove foreignData token equality; future native adapters must
validate/decode supported state before applying it, not trust mutable DTOs as provenance.

Previous source **0.19.68**: Store v12 / portable v3 add optional immutable source content evidence and piece-row references.
For compatible `.blueprint`/`.vbuild` imports, format + basename + exact valid UTF-8 text (BOM/newlines included)
are retained through native save/reopen, portable exchange, insertion, duplication and Undo/Redo.
Archive entry/path origin is not persisted and is not claimed; identical format/name/text deliberately deduplicate.
Sources are inert, capped at 8 MiB each / 64 documents / 24 MiB total, with bounded piece-row indexing.
Old v11/v2 migrate; new fields under old versions, modified hashes and invalid row references reject atomically.
Unsupported object info/ZDO/chance and unknown sections still block whole imports: this is not full compatibility.

Previous source **0.19.67**: file browser/output settings and persistent results are implemented in editor/Hammer;
foreign absolute scale is prepared from actual prefab bases once, without guessed shrinking or partial conversion.
Support actions live in Tools, not object rows. World reload/network durability remains unverified.
Full foreign header/object-data preservation and heavy interactive scenes below remain pending.
Checkpoint **0.19.62**: [support storage/exchange and remaining workflow](blueprint-support-frames.md).
Store v11 adds optional supports (stableId/name/position/rotation) and placementSupportId;
portable v2 carries the same payload. Old v10/v1 migrate without support data or source writes;
mislabeled versions with new support fields are rejected. Native sign integration is preserved.
The storage checkpoint did not yet implement tree/gizmo and placement-frame math/world durability.
The .57 measurements and pending list below are that checkpoint's historical record.

## Owner requirement: lossless foreign construction conversion (2026-10-06)

Convert foreign prefab constructions into the native format without silently dropping source data.
Preserve headers/creator/description, exact transforms, text, custom sockets and foreign object data;
keep source-only fields inert until a verified native adapter can apply their behavior. Retaining the
source bytes alone is archival preservation, not proof that an edited building behaves identically.
Terrain data may be retained as inert source metadata; terrain execution remains outside scope.
Original files remain untouched. Never call an incomplete adapter fully compatible.

PlanBuild captures `piece.transform.localScale` and assigns that exact value on ghost clones:
[primary Blueprint.cs](https://raw.githubusercontent.com/sirskunkalot/PlanBuild/master/PlanBuild/Blueprints/Blueprint.cs).
BuildWorks stores relative prefab multipliers. The .67 adapter distinguishes these semantics,
resolves actual positive prefab root scales on the Unity thread and prepares an atomic cached copy.
Non-unit/small bases and native roundtrip pass; mirrored/unsupported bases remain blocked.
Do not apply a guessed global shrink/clamp to large pieces. Missing prefab dependencies
must remain identifiable, not be substituted. Unsupported transforms/data stay blocked, not discarded.

Required exchange UX: visible file selection (not compulsory memorized paths), filename/output location,
format capability information and a persistent result. Native export is authoritative; foreign output
cannot promise supports/groups/XYZ/text that its target format does not encode. Regression gates:
exact unedited conversion and native round-trip, edited property preservation, missing dependencies,
cancel/errors/collision/no overwrite, source content evidence and real measured geometry in Unity/Valheim.

2026-10-05. Approved by Ostrix: preserve prefab-built constructions, large documents,
sign text, authored snap points and conversion from other building mods. This does
not authorize arbitrary mesh extraction, terrain editing or world/save changes.

## Work and evidence gates

### Goal extension: ordinary items and equipment displays (2026-10-06)

Ostrix added item support to the same active goal. The editor must represent ordinary game items,
especially items attached to horizontal/wall item stands and equipment on armour stands.
Preview-only loose items and holder-bound items are distinct from native building parts.
Required editor journey: item catalogue → choose item → place freely or select a compatible holder/slot →
preview → confirm/cancel. Show attached items under their holder in the object tree and identify the
holder/slot in the inspector; attachment actions belong to tools, not fake object rows.
Moving a holder moves its contents; duplicate remaps owner IDs/slots, while detach/removal and Undo
restore a valid relationship. Test horizontal and wall stands separately, plus a multi-slot armour stand.
Holder attachments retain an explicit owning part/slot relationship, follow holder transforms,
and survive duplicate/insertion/Undo, native save and portable import/export.
Preserve item identity and supported state (quantity, quality, variant, durability and mod metadata);
do not claim unverified metadata has identical gameplay behavior. Unsupported holder/item combinations
must remain recognizable with a reason, without substitution or silent deletion.

World reconstruction must use verified native item/holder APIs and respect resource, ownership,
permission and networking rules: a blueprint is a recipe, not an item duplication mechanism.
Proof: editor selection/attachment/removal and Undo; exact disk/portable roundtrip; imported display
state; actual holder placement, contents after save/reload, removal without duplication and remote client.
Modded items/holders require capability checks; installing arbitrary foreign code is not authorized.
The full extension remains unfinished; .72 implements ItemStand attachment authoring, preview and native capture/preservation, not free items/armour/world restoration. The existing goal remains active.

### Goal extension: storage and display contents policy (2026-10-06)

At both import and export, inspect supported storage objects and ask independently:
1. Storage inventory (chests and other storage types): retain full contents or clear them in the resulting copy.
2. Display attachments (horizontal/wall ItemStand, armour stands and other item mounts): retain or clear separately.

Do not merge these choices: clearing chest inventories must not silently empty equipment displays.
Show the affected object/item counts and any unknown/unsupported contents before confirmation.
An unreadable/unsupported storage type is not an empty container: preserve its inert original data and explain
the limitation; do not claim an editable/restorable conversion or silently substitute a different holder.
Apply the policy to the new draft/export copy only, never the source file, current document or world objects.
Keep the retained source evidence intact even when the editable copy is intentionally emptied.
Cancel/error leaves the source and document unchanged. Copy/history/native and portable roundtrip must
preserve the chosen state; re-export prompts again rather than hiding a previous choice.
Retaining contents is a recipe/state requirement, not permission to spawn free items or bypass native costs,
inventory ownership, access, server authority or duplication protection.
Required proof: independent choices, native/modded storage, filled/empty/unknown, import/export/cancel/error,
exact original-state roundtrip, intentionally empty copy, stand/armour independence, save/reload and remote client.
This requirement is recorded in the active goal; .73 captures supported native storage only. Exchange-wide inspection,
policy UI, foreign-storage decoding and world restoration are not implemented yet.

| Stage | Owner | Required proof |
| --- | --- | --- |
| Document/portable format and converters | Main integration; blueprint_format_contract57 independent review | Exact roundtrip, old-file migration, rejection without partial data, originals unchanged |
| Large editor scenes | Main implementation; blueprint_render_audit57 measurements/review | Real Unity open/pick/Undo/thumbnail times, memory, cancel/teardown, stable part selection |
| Signs | Main host integration; render agent verified current Sign API | Payload follows each placement step; native SetText; capture, place, save/reload, remote client |
| Authored sockets | Main editor/world integration; format agent contract review | Create/delete/Undo, exact frames, persistent native-piece sockets, reload and host/client |
| Ordinary items / holder contents | Main integration; format/native API review | Holder-slot relations, exact item state, editor Undo/exchange, costs/no duplication, save/reload and host/client |

Agent creation hit the current thread limit; two existing agents were reused for
read-only research/review. Shared, coupled source edits remain with the main agent.
No successful code test is a claim that placement/reload/network passed in Valheim.

## First checkpoint: source 0.19.57

Internal library **v10** adds optional `Part.signText` and `Blueprint.customSnapPoints`.
Older versions 1–9 retain their migration paths; missing new fields mean no payload.
Unknown/duplicate JSON fields and future versions block writing, retaining the file.
Atomic native writes remain in place.

Portable **v1** `.buildworks.json` uses the standard JSON serializer:

```json
{"format":"buildworks-blueprint","version":1,"requiredPrefabIds":["woodwall"],"blueprint":{}}
```

This abbreviated envelope shows the keys, not a valid two-piece fixture. The blueprint
contains all v10 fields, including exact prefab IDs, poses/scales, stable part/group
IDs, visibility/lock, primary frame and preview settings. `requiredPrefabIds` is the
sorted distinct list of actually referenced prefab IDs, not guessed mod ownership.
Required fields, duplicate/unknown fields, strings, transforms, graph and byte/depth
limits are checked. Missing pieces are a separate host check; none are substituted.

`customSnapPoints` holds **additional** sockets in blueprint-root local metres.
`null` and `[]` both mean no additional sockets; neither disables native/derived
points. Old Store update calls retain the current list when omitted; explicit `[]`
clears it. Derived `anchors` remain separate. Insertion converts sockets through
the inserted root pose; Undo restores the payload. Editor authoring/part binding
and durable world use are later stages, not delivered by this field alone.

Sign text is at most 4096 characters; CR/LF/tab and markup are preserved, other
control characters rejected. It follows clone/array/contour/insertion/Undo and
the Controller/Store bridge. Arbitrary info/ZDO is not sign text. Foreign `info`
remains blocked until prefab capability and native post-placement apply are verified.

The reader accepts bounded exact `#SnapPoints` XYZ triples and `.buildworks.json`.
Detached copies remap all hierarchy/pivot/primary IDs and preserve both new fields.
External cache identity includes file path and contents, avoiding native/export
collisions. The source archive is never extracted, executed, rewritten or redistributed.

Storage ceiling: **50000 parts/document**, **100000 parts/library**, still 32 native
blueprints / 12 categories. Portable file: 32 MiB; native library: 128 MiB; foreign
text files: 8 MiB; aggregate external reads: 128 MiB. These are safety bounds, not
proven gameplay capacity. Current synchronous **interactive budget is 512**.
Every edit/placement/thumbnail/direct-preview path guards this budget; large entries
remain data/catalogue-visible but cannot allocate a huge preview in one frame.
Placement with sign/custom-socket payload is explicitly blocked until its host
integration is ready, rather than placing with silently missing behavior.

## Measured CPU data (not rendering/FPS)

Opt-in checks in GeometryTests (`--benchmark N`) and StoreTests (`--benchmark N`).
Single-machine samples, startup included; live managed bytes are not peak/allocated
bytes. Geometry: 20 passes/operations averaged; exchange: one complete roundtrip.

| Parts | Document open ms | All visibility/lock queries ms | Move→Undo→Redo→Undo ms |
| ---: | ---: | ---: | ---: |
| 128 | 2.029 | 0.036 | 0.791 |
| 1000 | 2.825 | 0.486 | 5.921 |
| 5000 | 6.478 | 0.784 | 10.401 |
| 39487 | 41.142 | 2.648 | 50.912 |

Portable 39487-part fixture: 8305976 bytes, encode 283.280 ms, strict decode
449.134 ms, exact final part/count retained. Lazy ID indexes remove repeated linear
searches; same-count Undo invalidates indexes. Array count math now uses 64-bit bounds.
Mesh picking skips a farther bounds hit before triangle tests, preserving hole/occluder tests.
No baked binary mesh container, instancing, chunk cache or world monolith was added.

## Remaining acceptance — not complete

Checkpoint checks: Release (zero warnings/errors), Geometry, Store (353 actual
archive sources: 96 format-compatible / 257 blocked), Localization (751 keys),
EditorBridge, WorldLayout, DeploymentChecks, HostContract and independent correctness
review PASS. Ponytail review: no unnecessary framework; direct preview budget bypass fixed.
Fresh Unity Workbench: 144 states PASS, 2026-10-05T20:55:27Z, gameHostTested=false.
`dotnet format --verify-no-changes` exits 0 but reports workspace-load warnings;
the actual build and standalone checks above provide compilation proof. No verify
skill is installed. No deployment, Valheim launch or network/world test occurred.

1. Native sign capture and apply **after** successful native placement, with per-step
   payload (not blueprint array index). No resource/permission/ownership bypass.
2. Authored-socket creation/deletion with stable owning part/frame and exact world
   reconstruction on placed native pieces, reload, reconnect and remote client.
3. Budgeted/cancellable heavy-scene opening and derived render acceleration measured
   in real Unity. Source document stays authoritative; native world pieces stay independent.
4. Export/import actions in the actual catalogue/Hammer, dependency diagnostics and
   measured use of actual heavy library entries. Codec tests alone do not deliver this UI.
5. One clear owner game-test series only after these workflows are implemented;
   save/reload/network acceptance cannot be performed by static checks or Workbench.

The approved implementation remains active. The installed .56 baseline is unchanged
until an explicit verified deployment; source .57 is not a finished heavy-model release.
