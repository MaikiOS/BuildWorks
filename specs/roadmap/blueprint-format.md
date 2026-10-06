# Blueprint format and large-construction work

**English** | [Русский](blueprint-format_RU.md)

Current source **0.19.69**: Store v13 / portable v4 retain inert per-part foreign info/ZDO/chance;
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
This extension is recorded, not implemented. The existing goal remains active.

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
