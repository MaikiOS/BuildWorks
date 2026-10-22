# Checkpoint307 — refund properties, file checks only

22 October2026. No production/version change. Verified QA.273 remains installed; .274 is still the uninstalled306 candidate.

Targeted `--construction-payout` passes prior200 plus new289 assertions. Six synthetic paid rows cover distinct Wood properties, Resin, FineWood, Copper/source109 and Chain. Sealed and clean packages separately reject changes to prefab, quality, variant, durability, world level, crafter ID/name, cheated, custom data, pickedUp/equipped and quantity. Clean items reject custody markers.

Collection order5→2→0→4→1→3 exercises sparse indices. Each step reloads the journal and compares complete inventory bytes/summary with independently built expected records, preserving old records, separate new cells and same-prefab property differences. Final12stacks/30units have no custody markers. Positive109→110 uses representable cheated=false.

The initial fixture failure changed a nonserialized crafter name when crafter ID=0. Only that absent field is skipped; every remaining mutation must alter the package bytes. Failed log retained separately. Targeted test log: `artifacts/checkpoint-307/payout-properties.log`; independent correctness/Ponytail review required before checkpoint. `verify` is absent. Both targeted and general StoreTests invoke the corpus; full StoreTests was not run here.

Synthetic quality/variant values do not prove live prefab support. Profile evidence is a fixture, not native save proof. No native payment, issuance, interruption/restart or UI acceptance is claimed. No game/save/DLL changes. Next: verify isolation, deploy/test .274 and run the prepared306 native interruption/restart.
