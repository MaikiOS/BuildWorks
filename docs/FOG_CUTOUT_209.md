# .209: source cutout and the failed .208 house run

[Русский](FOG_CUTOUT_209_RU.md) · [Current evidence](CURRENT_STATUS.md)

## Actual Valheim evidence

The existing isolated QA world was loaded through Thunderstore Modded/GABS. A temporary 28-prefab Forest Hut rendered on .208. Resting fields: 0 bytes; atlas: 84293388 bytes. After approaching: 6 active owners, 61030400 field bytes and 89936460 atlas bytes. The ghost did not block ordinary movement.

**The complete run FAILED:** walking toward the far wall produced `Fog active owner budget is full; existing matter retained.` Peak allocation before refusal was 92856320 bytes against the 98304000 limit. The QA controller disabled and cleaned up the visual. Full traversal recovery and re-entry were not tested. Individual FPS observations were 57 resting and 24 during contact; these are observations, not a controlled performance benchmark.

Unexpected roof protrusions remain unexplained: geometry variants and volume in transparent texture regions are being investigated. The visual run started and ended with an empty inventory; no items were granted and no world pieces were built. Cleanup was confirmed and QA exited normally. This run does not establish payment, project reload or multiplayer correctness.

## .209 fix and evidence

`BlueprintFogSource.Bake` now rejects samples with alpha below `max(.001, _Cutoff)`, matching the surface shader. UV transforms and tint alpha remain applied. The v11 shaders/compute, memory caps, spending and refunds are unchanged. False density in a transparent half of a model was reproduced before the code change.

Passing checks after the fix: opaque/transparent halves, mirrored UVs and no false contact; existing Lazy series (5 comparison frames/28 synthetic records); approved v11 comparison (6 frames, equal Shape/Pigment/Noise); normal/QA builds with 0 errors/0 warnings; host/control/isolation/localization1104. Independent correctness PASSED and Ponytail Ship. The `verify` skill is not installed; direct review, builds and targeted checks were used.

QA **0.19.209.0** installed with the guarded script; SHA `50263A0D3F8DA4C08E7600E2FCB8A51B5C0509511EE321DB1F8BE30EBE510927`; 143 protected files retained exact hashes/mtimes. **.209 has not run in Valheim yet.** Neither the roof appearance nor the house allocation failure is declared fixed. Local receipts are in `artifacts/fog-contact-208` and `artifacts/fog-cutout-209`, excluded from the public repository.

Next: check the actual roof after the alpha fix and reduce active simulation regions while retaining matter until true recovery. Do not raise the cap or clear neighboring fields to pass the test. Then repeat full traversal/standing/recovery/re-entry and measure FPS. The durable project and board follow a working native house.
