<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Material variation data contract — game acceptance open

## 2026-10-09 — supported material placement gate; game acceptance open

The blanket materialSeed world rejection is replaced by a resolved-prefab check:
null stays ordinary; nonnegative seeds require BlueprintMaterialState.Supports.
Opaque source sections, unknown foreign state, native identity/ownership, scales,
signs, item-stand and matching visual-copy creation guards are retained.
Editor pending warnings and import/export summaries use current host capability;
background export inspection gets a pure snapshot. Conservative file inspection
is refined on the host before showing import choices. The hash dependency stays
explicit, preserving native storage/mount counts and independent clear choices.
Host review normalization currently runs on the UI thread; heavy latency remains
a measurement task, not an optimization claim.

Actual CheckMaterial and MaterialMan methods transfer stored zero/Max seeds to
renderer parameters on inert objects with explicit bindings. Native Start's
binding/scheduling is inspected by HostContract; a direct inactive Start trial
failed with Missing nview and is not game timing proof. Product code was not
changed to bypass host lifecycle. Source/ghost ZDOs stay unchanged.
Normal/QA0/0, Store (including lost inspection-hash regression), Controller bridge,
actual Session, HostContract25185596, localization880, QA guards and format passed.
Workbench360 passed UTC2026-10-09T13:23:07.4766485Z;
its first run caught the lost hash; the dependency was fixed before the successful
rerun. Independent correctness and Ponytail review passed. See artifacts/
checkpoint-0.19.117/material-readiness-verification.json for hashes/results.

This supersedes the earlier blanket-block notes below. No deployment, Valheim
launch, saves or network execution; installed QA .116 remains unchanged. Native
Start timing, real shaders/child lifecycles, actual placement/failure/refund and
reload/network still require the isolated game scenario. Active goal incomplete.
Next: supported stable QA fixture/build, then XYZ/delete/replacement/filter/camera.

## 2026-10-09 — typed material conversion and native visual copy; not deployed

External `.blueprint`/`.vbuild` RandMatSeed becomes typed materialSeed only for an
exact source-bound packet, nonnegative Int32, completely understood sibling
state and a verified supported prefab. Missing capability, ambiguous types,
unknown siblings and changed provenance stay inert; source bytes are retained.
Catalogue workers use a main-thread capability snapshot. File review remains
conservative. Material seed is independent of storage/mounted-content choices;
derived identity changes when typed seeds change, avoiding stale thumbnails.

The native ghost keeps its validation, material blocks and network state. An
owned visual copy receives the seeded recipe, pose, live validation tint and
global/indexed property blocks; cancellation/error restores native visibility.
Session contains preparation errors and creation still requires a matching copy.
Normal/QA builds 0/0, Store, Controller bridge, actual-host Session, HostContract
25185596, localization880, QA guards and format passed (existing format warning).
Workbench360 passed UTC 2026-10-09T13:01:26.6063597Z; seed A/B/A, XYZ pose,
cache, native global/indexed overrides and cleanup passed. Independent correctness
and Ponytail review passed. Evidence: artifacts/checkpoint-0.19.117/
material-conversion-native-preview-verification.json. verify helper unavailable.

Seeded world preflight remains blocked: native Start timing, actual Valheim
shaders/child lifecycles, failure/refund, reload and network are unverified.
Installed QA .116 is unchanged; no deployment, game launch or save writes.
Next: supported-prefab preflight and native Start lifecycle, then stable snap QA
and remaining XYZ/delete/replacement/filter/interior-camera requests.
The active goal remains incomplete; these are isolated checks, not game acceptance.

## 2026-10-09 — native state boundary, not deployed

The existing Instantiate wrapper now rejects inherited init-ZDO state before
creating a seeded piece. After native Awake, it writes only RandMatSeed to a
valid, owned ZDO distinct from the source and current ghost, then reads it back.
This happens before scale/snaps, construction registration and native payment.
Failure retains the existing cleanup/exception route; a known shared source or
ghost ZDO receives local GameObject cleanup only, avoiding network deletion of
that identity. No owner, identity, wear, inventory or resource state is restored.

World capture reads a stable native seed and legacy workspace saving retains it.
Pieces without RandomMaterialValues keep a null seed. Missing/negative state,
unsupported descriptors, disabled components and nested foreign network scopes
are refused with a localized message; no appearance is invented.

Actual-host inert tests passed for native Set/Get, seed 0/Max, capture, null no-op,
ownership, source/reference/UID isolation, empty UID, init-ZDO refusal and cleanup
classification. Native ZPackage memory serialization/deserialization retains the
seed and the recipient's distinct identity. This is not a network or world-save
test. Normal/QA builds, Geometry, Store, Controller bridge, HostContract25185596,
localization880, QA compiled/profile guards and format passed (existing format
workspace warning). The fixture initially omitted native Valid initialization;
both failed runs were diagnosed before correcting the fixture. Product guards
were not relaxed. The verify helper is absent. Renderer/UI assets did not change;
the previous Workbench360 proof was not rerun for this native-state stage.

Seeded world preflight remains blocked. Typed external conversion, native ghost,
RandomMaterialValues.Start timing/appearance, in-game failure/refund, reload and
multiplayer remain open. No deployment, Valheim launch or save writes.
See `artifacts/checkpoint-0.19.117/native-material-state-verification.json` for
final hashes, review and the retained actual-host result. Earlier hashes below
belong to earlier stages and are not the current source build.

## 2026-10-09 — placement preview stage, not deployed

MaterialSeed now follows passive and expanded placement snapshots. Small preview
cache keys include it; rejected descriptors hide owned visuals and report an error.
Heavy previews share prefab/content geometry templates across seeds, cache owned
native vector recipes, and group only identical recipe contexts. Preparation
creates at most two templates and 256 recipes per call; obsolete recipes/batches
are pruned. Matrix buffers grow from one to 1023 rather than reserving 1023 for
every distinct seed. Distinct seeds still require separate draw groups: 50000-row
submission checks are not a frame-rate acceptance claim.

GPU instancing receives repeated vector arrays for each exact group. Scalar
caller collisions use ordinary DrawMesh, retaining the native ghost tint and
leaving caller/source blocks and shared materials untouched. Supplied mount
contents retain their own appearance in both small and heavy previews. This is
preview consistency; native MaterialMan behavior on dynamic mounts remains QA.

Workbench360 passed UTC 2026-10-09T12:07:05.0996737Z. An actually unused layer
isolates the render proof; solid cube regions reject unrelated geometry. Saved
clones and batched/fallback images agree for uniform and instanced shader vectors,
seed A/B/A, grown/multiple-flush groups and caller collisions. Additional checks
cover small cache/error cleanup, seeded holder/mount isolation, 50000 distinct
seeds on one geometry template, bounded preparation/buffers and cache pruning.
Actual-host GetValue parity and Session seed 0/Max/null/repeat/source-isolation
checks passed against the final normal DLL. These synthetic shader images do not
establish Valheim shader appearance, native ghost updates or world persistence.

Normal/QA builds: zero warnings/errors. Geometry, Store, localization878,
Controller bridge, actual Session/host checks, HostContract25185596, compiled
QA separation, launch-profile preflight and format passed (format retains the
existing workspace warning). The verify helper remains unavailable.
Normal SHA: `D4383896C0B6F08BAD85CD21B18AB77AD562BA5731CFD4AFD0F03814FCC341C5`.
QA SHA: `E3E47E441527A79515D362D57B2D901BE67136CEC57537355BC7B5403072B6E3`.
Installed QA SHA is still
`867E5E474A3C1B335500E47F1B2DC3A6250AC1FAA36E6D92E766E904F33DCF3A`.
No deployment, Valheim launch, save writes or network test.

Failures corrected during this stage: old mount-test tuple cast; GPU repeated
instance lost its vector; Unity array-size cap after scalar assignment; allocation
test reused an intentionally grown buffer; render proof shared an occupied layer.
Independent review found holder variation leaking onto heavy mount contents;
the attachment exclusion and small/heavy regression now pass. A malformed tool
call (`missing field cmd`) and wrong documentation paths were corrected; neither
was a product check. Independent final correctness/Ponytail review passed with
no blocking findings or needless complexity; it relied on the checks above.

Remaining: typed external RandMatSeed conversion, native ghost update, capture,
fresh-ZDO write/readback and failure rollback, actual shaders/FPS, reload/network.
Keep seeded world preflight blocked until these agree. This section supersedes
the small/heavy preview TODO below; it does not complete imported construction.
Evidence: `artifacts/ui-runtime/material-placement-check.txt`, the material PNGs,
`verification-status.json`, `source-sha256.txt`, and actual WorldLayout result.

## 2026-10-09 — renderer stage, not deployed

`BlueprintMaterialVariation` now evaluates owned native vector descriptors via
`RandomMaterialValues.VectorVariationProperty.GetValue(unchecked(seed + index))`.
The source descriptor's RNG and shared materials are untouched. Values are
applied to renderer-level property blocks only on owned visual copies, within
the native component's exact nested scope, including inactive children.
Non-finite descriptors, invalid names and overlapping MaterialMan scopes are
unsupported; their order-dependent renderer behavior is not guessed.

The adapter is connected to editor visuals/temporary copies, thumbnail rendering,
rotatable preview sessions and palette visual construction. Editor hover restores
the seed blocks, and both document and temporary caches include the seed.
An unsupported saved seed stays intact and the editor still opens known geometry
with its existing pending-state warning. The adapter itself rejects unsupported
sources. Palette construction for seeded records remains unreachable through
the world preflight until the other adapters are complete.

Actual installed-host API parity passed on inert Unity objects: seed 0/1234/Max,
index overflow, untouched source RNG, nested renderer scope and unrelated MPB.
Final normal DLL SHA: `2EAD9064B87CD0721C31EAB9F7568E857D335AEDFA2E9662FB304583D55CD1EE`.
Workbench360 completed UTC 2026-10-09T11:35:08: clone values/source isolation,
editor hover/restore, same-record seed change and temporary-copy cache change;
ordinary Save As still preserves an unsupported seed. These are API/property
checks, not native Valheim shader appearance or game placement acceptance.

Normal/QA builds, Geometry, Store353, localization878+unit, Controller bridge,
actual Session17+host material case, HostContract and format passed. Independent
correctness/Ponytail review passed. The verify helper remains unavailable.
Test failures fixed before acceptance: CS0117 (test used nonexistent geometry
constants), MissingComponentException (test used ?. on a Unity fake-null), and
Unsupported blueprint material variation: woodwall (editor initially refused
an unsupported saved seed; now it retains it and opens the known geometry).

Live evidence: `artifacts/ui-runtime/verification-status.json`,
`artifacts/ui-runtime/source-sha256.txt`,
`artifacts/fix-0.19.22/world-layout-checks/result.txt`.
Installed QA .116 SHA remains
`867E5E474A3C1B335500E47F1B2DC3A6250AC1FAA36E6D92E766E904F33DCF3A`.
No deployment, Valheim launch, save writes or network acceptance in this stage.

Remaining: external typed conversion, small/heavy world preview and native ghost
updates, capture and fresh-ZDO write/readback before placement completes.
Do not remove the seeded-record world guard until these agree. A shared MPB
does not prove that a shader supports per-instance variation: heavy batching
needs a verified shader-aware path or exact distinct property groups without
creating one template GameObject per seed. The current host's synchronous
`WearNTear.OnPlaced` callback inside `Player.PlacePiece` is a candidate for the
fresh-ZDO write; it is researched, not implemented. Reload/network still need QA.

## Stored data preparation

This is a source-only preparation step after .116, not finished RandMatSeed
conversion or an installed release. Valheim was not launched.

Native document20 / portable JSON10 add optional `Part.materialSeed: int?`;
the editor document exposes immutable `MaterialSeed`. Null means unspecified,
zero is an actual seed, and the supported scalar range is 0..Int32.MaxValue.
Negative, fractional, overflowing, wrongly typed and duplicate input is rejected.
Older schemas reject the new field, including explicit null; older documents
without it remain readable. Source documents and their exact archived bytes
are unchanged. Old BuildWorks readers safely refuse the newer schema.

The field is independent of signs, source references, transforms, prefab
metadata and mount/container payloads. Clone, duplicate, insertion, Array/Contour
copies and history retain it. Explicit document updates may clear it; the legacy
part-only update retains an omitted value like the existing metadata contract.
Editor/Store/JSON/ZIP/reopen paths preserve it without assigning identity or costs.

Until the renderer and native adapters are complete, a document containing this
field remains explicitly pending in content review, editor status and world
preflight. The external converter still archives RandMatSeed as unsupported
state. No import readiness is gained from merely storing an integer.

The renderer stage above supersedes the original renderer TODO. Remaining world
adapters, capture, cancellation, save/load and multiplayer require their own
proof. Never transfer old network identity.

Verification: normal/QA builds, deterministic document/Store tests, actual
Controller bridge, host contract and actual Unity editor save/reopen. Logs and
final gate status are recorded locally under artifacts/checkpoint-0.19.117.
Game appearance, native placement, reload and network are not verified here.
