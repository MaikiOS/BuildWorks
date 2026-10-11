# .154 — PlanBuild snap rows with trailing fields

Code3a2c82b, normal/QA sources .154. Not deployed; installed QA remains .146.
No Valheim/Unity/input/save changes; full goal active.

BiomeBlueprints1.0.3 house/dome documents previously failed at #SnapPoints line9
with7/12 fields (dome includes a final empty field). The SHA-verified pinned
[PlanBuild SnapPointEntry](https://github.com/sirskunkalot/PlanBuild/blob/e12ed86ff2498bbd4a9c0f5d81cb388e3aaeb549/PlanBuild/Blueprints/SnapPointEntry.cs)
reads only the first XYZ fields. The isolated original reader confirms both
actual rows under en-US/ru-RU. Online pinned-raw fetch returned Cache miss;
verified local source and execution supply the evidence.

Only .blueprint now permits >=3 snap fields. Split is capped at4 fragments;
XYZ finite/range/count guards remain, as does strict .vbuild grammar. Complete
source rows and provenance survive native/portable round trips. Extra fields'
meaning/behavior is not restored. A computed warning count appears in contents
and import review, plus the separate PlanBuild export review. No schema changes.
Third-party export emits only XYZ; original tails remain in its full native
companion. This does not promise rotation/size/other unknown behavior.

Owned fixture27 reproduces the old failure and passes cultures/round trips,
warning aggregation/export, strict invalid prefixes,20001 rejection and long tail.
Full Store353 now353 parsed/0 refusals. Corpus209 remains202/7, including four
large document round trips. Typed packets133738 exact EOF;115707 recognized
construction states;27905 retain blockers in the inert unit-base probe. The
214 increase is newly counted state from the two newly parsed documents.
Normal/QA0/0, full Geometry, Host25185596, localization989, QA isolation,
EditorBridge, scoped format and correctness/Ponytail Ship passed. Receipts and
hashes: artifacts/checkpoint-0.19.154/verification.json. verify helper unavailable.

Seven coordinate outliers remain refused; no rebase/drop. vbuild ZDO scale
semantics remain unsupported without a verified external writer/reader contract.
Native IL alone does not establish that contract. Unity warning layout, native
snap/ghost/build/resources/cancel/reload/network and total latency/FPS/memory
remain untested. [Deferred fixture27 procedure](QA_0.19.154_RU.md); no user action now.
