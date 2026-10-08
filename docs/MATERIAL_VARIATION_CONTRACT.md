# Pending material variation data contract

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

Next required work: resolve the exact native RandomMaterialValues component,
derive values through its public API without mutating shared source properties,
apply owned per-renderer property blocks in editor/hover/thumbnail/small/heavy
and native palette ghosts, and synchronously write/read back the new owned
object's ZDO before native placement continues. Capture, cancellation, save/load
and multiplayer require their own proof. Never transfer old network identity.

Verification: normal/QA builds, deterministic document/Store tests, actual
Controller bridge, host contract and actual Unity editor save/reopen. Logs and
final gate status are recorded locally under artifacts/checkpoint-0.19.117.
Game appearance, native placement, reload and network are not verified here.
