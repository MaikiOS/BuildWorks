# .210: native fog house memory and traversal

[Русский](FOG_SCRATCH_210_RU.md) · [Current evidence](CURRENT_STATUS.md)

## Change and automated checks

Six temporary GPU fields are now shared serially only by owners with exactly equal grid dimensions. Each part retains four independent persistent fields. The v11 formulas and shader/compute are unchanged. The98304000-byte field cap remains; unrecovered owners are never evicted. Scratch is released with its last exact-grid owner.

Scratch:360 ticks and36 bit-exact comparisons of four persistent fields against the previous independent simulation; different dimensions, eight-step catch-up, distinct states, duplicate/failing factory rollback, GPU-recovery release and admission-before-allocation passed. Twelve saved-dimension owners use96075776 bytes; this is separate from native evidence. Lazy:28 synthetic records/five frames, maximum mean image difference0.000137874 and foreground0.00169127. Frozen270/18, normal/QA builds0 errors/0 warnings, host/control/isolation/localization1104 passed. Independent correctness PASSED and Ponytail Ship. The `verify` skill is unavailable; direct review and targeted checks were used.

QA **0.19.210.0** was deployed through the guarded script, SHA `A96AC9314A3F30176CD6CC358FE95A439ABB25B8EAD6269F2287AE3358CB734B`. All143 protected files retained exact hashes/mtimes.

## Native Valheim evidence and limits

Thunderstore Modded/GABS loaded the existing QA world/character. The temporary Forest Hut has28 actual prefab parts; idle field allocation is0. Forward passage, far-side exit, reverse re-entry/standing and final exit passed without failure. Peak fields were **87408640 bytes**: the previous .208 allocation failure did not recur on this route. After each full exit and15 seconds of recovery: **0 owners/0 field bytes**. This proves computed recovery completed; full visual acceptance is separate.

**Performance is not accepted.** At3440×1440, isolated samples were205 FPS before the house at the initial camera,59 idle and8 during some contacts. At one fixed position, the switch matrix returned:normal26; simulation stopped/volume on25; both off215; simulation on/volume off213; both restored25. This is short diagnostic sampling, not a stable benchmark. It points to the volume path, including packing and raymarch; their individual costs are not yet measured. Disabled volume is not working v11.

The .209 source cutout fix ran as part of .210. House screenshots were retained, but all roof elements and whole-house agreement with the approved appearance are not accepted yet. Density and visual quality were not reduced for speed.

CloseFogVisual removed the temporary house; fog=null, inventory0→0, no grants or world pieces created. The QA world was normally saved/left and the game closed using Exit. This does not prove payment, a durable project, project reload or multiplayer. Entry logged optional Backpack API warnings; fog scripts recorded no new errors.

Local receipts/screenshots: `artifacts/fog-scratch-210`; no extracted game data is published. Next: isolate packing versus volume-integration cost and optimize while preserving v11. The durable unfinished project/stake/board, continuation/cancel/confirmed refund/reload/server remain the next required product result.
