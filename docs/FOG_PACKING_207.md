# Fog packing .207

[Русский](FOG_PACKING_207_RU.md) · 2026-10-17 · [Current evidence](CURRENT_STATUS.md)

.207 removes the common XY pitch from atlas blocks. Each piece retains its own dimensions and filtering borders; bounded layout search runs only when dimensions change. Resolution, shader/compute, textures, and v11 formulas are unchanged. Coordinates commit only after atlas allocation succeeds.

Unity verification:

- Saved28-part Forest Hut dimensions:126955800 bytes instead of193460292, with the128MiB cap unchanged. This admits a metadata-derived layout, not an actual house render.
-100 deterministic randomized sets check repeatability, non-overlap, clipping, device/memory bounds; invalid and overflowing inputs are rejected.
- Existing4-frame/3-owner series: exact fields, order delta0, pair delta7.67e-7, border delta0, opaque-depth delta0, translation delta6.06e-5. Removal and cleanup preserve independent ownership.
- Successful layout A → injected allocation failure B → unchanged coordinates/textures and pixel-exact restored A. Real GPU memory exhaustion was not induced.

Final normal/QA builds0 errors/0 warnings; host, QA control, QA isolation, localization1104 and independent correctness/Ponytail PASSED/Ship passed. An intermediate parallel check temporarily locked the DLL and caused MSB3026 retries; a clean rebuild passed after the reader exited. The junction test needed sandbox permission and then passed.

QA0.19.207.0 installed only through guarded Deploy-TestBuild into TerrainRamp-1.0-Test, with matching hashes.143 previous protected files retain exact hashes/times;130 non-fixture files were additionally compared immediately before/after deployment. No game launch or public binary release.

**Still open:** full simulation fields for these28 parts require272384000 bytes against98304000. The sample contact set also exceeds that budget. Packing does not solve this separate issue. Next: bounded contact regions preserving unrecovered matter, then actual house/memory/FPS and persistent project/board. Last native visual remains .203/two walls.

Local-only evidence: `artifacts/fog-house-207`, including the saved Unity receipt. Metadata dimensions do not establish actual baked-prefab dimensions. Layout search is bounded, not optimal for every possible building.
