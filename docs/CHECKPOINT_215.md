# .215 — resident v11 state and height-edit completion

2026-10-18 · [Русский](CHECKPOINT_215_RU.md) · [Complete open scope](LIVE_CHECKLIST_RU.md)

QA **0.19.215.0** installed only in TerrainRamp-1.0-Test; DLL SHA `328CCC9841FCA6B21529F52CD89D177684E09D8974D48A7D59E5E1FA75AC39AE`. All143 profile data hashes stayed unchanged during installation. The subsequent authorized game run saved its QA character/world normally. No public binary release.

## Changes and automated evidence

Volume height previews remain cheap; drag/wheel/reset/text completion refreshes actual membership. Clamped no-op edits do not dirty or query the selection. Invalid bounds retain the previous selection and pending refresh. Actual Unity controls cover48 rectangle/3 catalog/4 height cases; CPU checks also cover cancellation, invalid text and repeated completion. ReleaseOnly now records a failure even when preflight or proof validation throws.

The strict fog runtime keeps an admitted source and its exact GPU history until explicit removal; idle no longer skips input samples or discards residual velocity after apparent recovery. Logical admission is bounded by pool capacity. Normal/QA/Editor builds0/0; Geometry131, WorldSelectionChecks, host/control/isolation/localization1104 and1654 corrupted-evidence rejections/32 metadata checks passed. Independent correctness/complexity reviews passed; `verify` is unavailable.

GPU continuous v11 from initial idle through contact/recovery/reentry passed **56 exact field/ledger comparisons and18 visual comparisons**: mean3.11514e-8, local0.000244141; cold negative control0.114075 remains sensitive. Recovery90/42frames; Physics240commits/2rejections/664fields/498ledgers/28atlas/242metadata; Frames220real frames. Gates were not relaxed. Local receipts: `artifacts/checkpoint-215`, `release-regression`, `fog-published-render`, `fog-runtime-recovery`, `fog-arena-physics`, `fog-arena-frames`. A sandbox Unity repeat crashed before the scenario (exit -536870911); its log was retained and the non-sandbox repeat passed48/3/4.

## Actual background Valheim

Ostrix authorized one direct background GABS launch with the QA preloader, an explicit exception to Modded for this run. ValAgentBridge1.8.0 confirmed TerrainRamp-1.0-Test, background/batchMode/qaSaveRoot=true, cloud=false. Connection6.203s; existing world17.266s. No desktop mouse/keyboard input, save reset, free items, terrain editing or construction.

The main scenario passed **40/40 in0.5s**; identity refinement passed **40/40 in0.2s**, no optional failures or runtime warnings/errors. Real catalog button entry, one selected piece before completion and three expected wall IDs after the actual HUD callback; invalid bounds preserve membership/dirty, correction refreshes, clamped no-op remains clean. Area/initial bounds were direct transient test setup, not proof of manual mouse drawing or HUD appearance. The complete bridge-visible inventory/items/slots/durability/equipment/backpacks matched; the existing Hammer was unequipped again. Building costs/rights are outside this case.

Two intermediate automation failures remain recorded: two different controls shared the selection label; an attempted unsupported `ui/click path` argument was rejected. The qualified current control ID then passed without weaker assertions. Initial missing optional Backpack API warnings are separate from the clean main scenario.

Normal logout → main_menu → Application.Quit and owned PID exit were confirmed. Three original bridge DLLs were hash-restored. Only the BuildWorks config version header changed;4137 ordinary save files retained size/mtime (not full content hashes). QA world/character saves changed normally; token/receipt/catalog were not reset. Private JSON/logs/scenarios: `Default/ValheimQA/artifacts/20261018-093027`, excluded from GitHub. Background screen/capture is not HUD proof.

## Remaining boundary

Strict runtime is **not activated by Building**; the working bundle is unchanged. The admitted bounded pool passes, not the28-piece house: velocity alone137625600 bytes exceeds the98304000 safety cap; published dynamic atlas295210496 bytes/height2296 still needs memory/domain/packing work. Caps remain intact. Next: exact whole-house v11, then durable project/stake/board, paid contributions/refunds, reload/server. Full editable-area gizmos/live world membership, item/sign visuals, multiplayer and owner visual acceptance remain open.

Optional owner UX check: existing QA world/Hammer → blueprints → select in world; draw around existing pieces, drag upper/lower height and release. Preview moves during drag; membership updates on release/wheel/text completion. Close without saving/building. No new blueprint fixture or resources are needed. Previous rectangle/import-focus acceptance is still pending; batch sources remain `artifacts/Blueprints-for-import/BuildWorks-tests/Batch-183`.
