# .155 — source-bound holder Info and typed numeric item IDs

Code **db9e508**, normal/QA .155. **Not installed:** QA remains .146.
No Valheim, Unity, input, deployment, game saves or push. Full goal stays active.

The existing typed-ZDO holder adapter previously required item:String. It now accepts
item:Int32 only with exact four-field Info from the same archived source row.
Name/hash and decoded variant/quality/type must agree. Missing, malformed, rebound
or conflicting Info remains blocked; every unconsumed outer field or typed sibling
still blocks the complete conversion. No global hash lookup or guessed item name.
The existing five-argument API and string path remain. Native22/portable11 unchanged.

The result uses the existing valheim-itemstand-v1 path and independent contents
policy. Resource/provided-item attributes, authority and administrator restoration
rules are unchanged. Exact source evidence remains archived.

SHA-verified pinned PlanBuild commit e12ed86ff2498bbd4a9c0f5d81cb388e3aaeb549:
[Blueprint.cs](https://github.com/sirskunkalot/PlanBuild/blob/e12ed86ff2498bbd4a9c0f5d81cb388e3aaeb549/PlanBuild/Blueprints/Blueprint.cs)
captures a name from the int item ID into four-field Info;
[PlacementComponent.cs](https://github.com/sirskunkalot/PlanBuild/blob/e12ed86ff2498bbd4a9c0f5d81cb388e3aaeb549/PlanBuild/Blueprints/Components/PlacementComponent.cs)
consumes that Info and hashes the name. The external DataEntry wire reader was
verified separately. **This supports a narrow combination of known contracts.**
Pinned PieceEntry.FromBlueprint does not read the extra ZDO columns; this is not
proof that pinned PlanBuild writes combined 14-column rows or reads our extensions.
Owned fixture28 tests the combination; it is not a downloaded blueprint.

Checks: reproduced old refusal; full snapshot/native/portable/source reopen,
independent contents exclusion, hash/attribute/source mismatch and unknown sibling
rejection; Geometry exact native packet preservation with grid/world/custom data;
full Geometry/Store, normal/QA0 warnings/errors, Host Steam25185596, localization989,
compiled QA isolation, scoped formats, correctness/Ponytail Ship.

Corpora unchanged: Biome353/353; typed packets133738, converted115707, retained27905;
second corpus202/209 with seven previous coordinate refusals. Both source ZIPs unchanged.
Baseline .154 census has234/353 documents with unknown barriers,27905 foreign part
records and8 documents with opaque sections, using unit prefab bases without actual
host/material capability. There are zero item:Int32 rows among5450 Biome holder rows:
no corpus blocker lift is claimed. Unknown numeric keys, Door state and named Center
still need separate verified handling.

A null-Info crash found by negative tests was fixed; failed logs retained. .NET temp
permissions needed the authorized CPU run. Wrong isolation script name was corrected;
the actual compiled check passed. verify helper unavailable; direct checks and reviews.

Receipts: artifacts/checkpoint-0.19.155/verification.json and
artifacts/conversion-audit-2026-10-11/remaining-state.json.
Not checked: native UI/items/resources/cancel/reload/network/import latency, Unity
Workbench or unchanged EditorBridge. [Deferred fixture28 QA](QA_0.19.155_RU.md).
