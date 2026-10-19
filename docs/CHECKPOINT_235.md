# .235 — item-stand snapshot identity

2026-10-19 · [Русский](CHECKPOINT_235_RU.md)

**Normal/QA0.19.235 built; installed QA remains .233.** No user game/GPU use or
deployment. This does not fix whole-house fog.

Both final guards in `BlueprintItemStandCapture` now require the same ZDO reference.
Previously the same ZNetView could be rebound to another ZDO with an equal revision,
and stale empty/occupied data was accepted. This follows the existing material
capture identity guard. Format, resources, inventory and permissions are unchanged.

The regression compiles actual capture source with a fake read-side host and the
real Geometry serializer. Old source failed the empty/new-occupied-ZDO case;
fixed source passed11 stable/race/revision/invalid-view/nonmutation/ArmorStand-rejection
checks. These are CPU results, not a reproduced native event or network acceptance.

Normal/QA builds0warnings/0errors, HostContract1.0.20/Steam25909357, localization1121,
compiled QA separation and independent reviews passed. The initial NuGet sandbox
denial was resolved by building with cache access. The IL gate checks Roslyn's
reference-inequality branches, not a ReferenceEquals method call.
Local proof: `artifacts/checkpoint-235/{repro-before.json,repro-before.log,
capture-checks.json,capture-checks.log,build-normal.log,build-qa.log,
host-contract.log,localization.log,qa-separation.log}`.

Parallel ArmorStand research confirms a separate native pose/slot/package adapter
is required. Visual cache is not proof of an empty stand; stale itemData with itemID0
is not an active item. Its unsupported-state guard remains; equipment/pose handling
and native capture/reload are unfinished.

Next, when GPU is free: the prepared checkpoint234 PollingCandidate, then a proven
runtime fix and native house verification. All14 goal blocks and active status remain.
