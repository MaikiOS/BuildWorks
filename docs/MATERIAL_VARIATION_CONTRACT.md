# Pending material variation data contract

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
