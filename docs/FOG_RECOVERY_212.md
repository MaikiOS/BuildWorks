# .212 — fog recovery lifetime fix

[Русский](FOG_RECOVERY_212_RU.md) · [Current status](CURRENT_STATUS.md)

On .211 the owner saw the house disappear three times after passage. The native log recorded `Fog recovery readback failed; retain its owner.` A separate five-minute renderer also failed before its timer. This is a defect, not expected shape recovery.

A Unity PlayMode regression reproduced the same error before the fix: a completed `AsyncGPUReadbackRequest` expires while body contact suspends polling. Unity retains its result for one frame. The callback now immediately copies one `uint` and its captured revision into managed fields; it retains no temporary GPU array. A reply predating new contact cannot authorize release. Real GPU errors still fail closed, and Dispose prevents callback resurrection. [Unity lifetime contract](https://docs.unity.cn/6000.0/Documentation/ScriptReference/Rendering.AsyncGPUReadbackRequest.html).

| Level | Evidence |
| --- | --- |
| Before fix | Cross-frame regression reproduced the native readback error |
| Unity after fix | 24 real frames: expired-handle negative control, delayed polling of copied result, stale/fresh revision, disposal with a pending request |
| Regressions | Scratch360/36; Lazy28/5 and10 exact RGB comparisons; normal/QA builds0 warnings/0 errors; host/control/isolation/localization1104 |
| Independent review | Correctness PASSED, separate Ponytail-review Ship |
| Deployment | QA0.19.212.0, SHA `71E5B6DF7A260E8E9FBBD479F0A4E07908ADBD1D09CAB31028EA0DB00CE2A469`; all143 current catalog/config/QA-save files retained their hashes and timestamps |
| Valheim .212 | Native .212: the28-part house passed both ways,35/35 steps in44s; contact6/8 owners, both recoveries0/0 bytes, peak87408640; inventory unchanged, no errors. Owner rejected the .212 appearance: dissolution/recovery differs from v11 and passage stutters. Visual integration is BLOCKED. |

QA-only `StartFogHouseManual` provides a separate300-second real-time inspection. Focus loss/pause retain only this explicitly selected visual while stopping automatic input immediately. F12, explicit stop, QA disable and controller destruction still close the visual. Ordinary automatic scenarios retain their previous focus-stop behavior. This is a temporary diagnostic house, not a durable construction project.

Local evidence: `artifacts/fog-recovery-212` contains the red-before-fix log, recovery-verification, Scratch/Lazy, builds/contracts and deploy-receipt. The `verify` skill is unavailable; direct checks were used. The shader/compute bundle and approved v11 sources are unchanged.

The tested native route no longer reproduces disappearance. Next: owner inspection acceptance without automatic input. Roof appearance, low full-simulation speed, visual acceptance, durable project/board, resources/reload/server remain open. Earlier .210/.211 evidence does not replace this acceptance.

## Why visual acceptance failed

The owner compared the native house with the laboratory: dissolution/recovery differs and passage severely stutters. The35/35 technical route checked absence of erroneous removal, not the correct effect.

`Test-FogSimulation.ps1 -DomainOnly` reproduces a difference on the actual local approved-scene wall: identical camera/body/path/time/light, Linear,360 ticks/12 captured frames. The main full/compact pair also changes source-grid alignment and ledger bounds. An additional pair shares the identical source/rest/noise/seed and voxel spacing/centers: full96×80×80 versus cropped48×56×72 domain. At tick165 its volume-weighted smoke-alpha measure is approximately1.03 versus0.58 (44% loss); at tick180 approximately0.64 versus0.32. This is a comparative alpha integral, not conservation-of-mass proof. Complete GPU readback counts are checked.

Whole-frame mean RGB still stays below the previous0.005 threshold: identical background hid the local difference. The diagnostic explicitly reports visualAccepted=false. Local images/data: `artifacts/fog-runtime-domain`. The wide branch here uses runtime Linear rendering; this is a causal domain comparison, not renewed v11 color acceptance.

Two additional source-level gaps remain: YZ ledger aliases distinct X positions on a rotated wall/floor; noise phase follows individual pivots instead of one building frame. Their dynamic checks remain pending. The next engineering slice must retain the physical smoke domain and compare bounded dispatch against full dispatch without losing state. Memory limits, ownership and whole-house speed need separate proof. No post-.212 runtime shader/solver fix or replacement DLL is claimed from this diagnostic.
