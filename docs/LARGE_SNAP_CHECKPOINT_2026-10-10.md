# 0.19.124 — large custom snap sets

Source checkpoint only. Installed QA remains0.19.117 in TerrainRamp-1.0-Test.
The four archival format refusals are cleared, not their Valheim acceptance.

The separate aggregate custom-point limit20000 now reaches parser, Geometry
document/insertion/Undo, Store, portable exchange and reverse collector.
Native22/portable11 distinguish the larger list. Legacy21/10 accept<=512 and
refuse larger lists without rewriting source.20001 is refused atomically.
Ordinary anchors512, coordinates±512, per-piece world codec512 and world
selection part count512 remain. No resource, permission or content changes.

Actual archive2v2t5.0.0, SHA256
`4A6E6A53F246B5CF4C7EA9472F726D9F35687BCE978F28AB47A020AEE759DD88`,
is unchanged. Arani5062/6596, Bridge770/2012, Decay4070/11790,
Flabellum6265/17125 parts/points pass raw order/coordinates → document → native
reopen → portable reimport → separate SaveAs ID. Full209 corpus now201 parsed,
8 explicitly refused,4 former count refusals cleared, no new refusal.
Seven remaining failures exceed current coordinate bounds; one is legacy
object_data text. Exact names/lines: local `store-corpus-final.log`.

Geometry/Store, normal/QA0 warnings/0 errors, Host25185596, localization930,
compiled QA isolation PASS. Full Unity492 completed2026-10-10T08:43:19.0141278Z.
Inert unit-base prefabs verify all ordered Hammer child transforms, production
partition, float32 local/world positions, owner Apply, read-only replica Append,
no duplicate/native-midpoint inputs and bounded large reverse collection.
Maxima per piece46/14/24/17 remain below512. Reverse capture keeps established
proximity dedup; source storage preserves authored multiplicity. Initial double
expectation was corrected to the unchanged float32 codec; failure logs retained.
Missing `verify` helper was replaced by direct checks and independent reviews.
Reviews/commit are recorded in `artifacts/checkpoint-0.19.124/verification.json`.

One shared-list Unity sample: Arani185.68ms, Bridge17.58ms, Decay580.23ms,
Flabellum1215.53ms. Repeated Append List.Contains is the next measured bottleneck
and blocks deployment pending repair. Creating17125 children took76.86ms.
These are diagnostic single samples, not FPS or a radius-filtered game query.
Actual models/prefab scales, Session capture, build/reload and multiplayer are
not proven. No deployment, Valheim launch or save mutation.
