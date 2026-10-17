<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# Combined v11 volume: .203

2026-10-16. BlueprintFogVolume samples two independent owners at the same world position before scattering/extinction. FogVolumePair.cginc uses a separate local shader keyword; the approved one-owner path remains. Visuals now use the bounded pool without duplicate ownership or silent eviction. Late disposal of an old visual removes only its expected simulation, preserving a reused owner ID; regression passed.

The fine-box diagonal is capped at12m: up to480 fine0.025m steps plus160 coarse padding steps fit the768 iteration bound. Oversized regions are rejected explicitly. Two active sets retain93.75MiB of fields, excluding source textures/geometry/other memory.

## Evidence

- Unity6000.0.61f1 Linear/HDR/Deferred: two owned walls,90° rotation/nonuniform scale/distinct origins, four AB/BA states. Worst mean RGB order delta1.226433e-9; sequential-Blit negative order delta0.0000755853, combined-vs-sequential0.0124544.
- One-owner/remaining-owner and opaque-depth comparisons exactly0. Distant translation delta0.0000445434 within the unchanged0.005 threshold. Callback exactly once; finite full frames. Idle/contact/return frames inspected. Duplicate/full/span/reused-owner/cleanup guards passed.
- Independent transport6 full comparisons/2149 overlapping voxels and stale/smoke-only/surface-only/re-entry/recovery checks retained. Frozen270 ticks/18 complete exact comparisons pass. After the final light-branch-only change compute stayed unchanged; affected pair/Gamma visual6 frames2 translations/Linear color0 repeated. Workbench settings restored byte-exact.
- Normal/QA .203 builds0 errors/0 warnings; Host Steam25909357, QA-control/isolation, localization1104 pass. Verify helper unavailable; direct checks plus independent correctness PASSED/Ponytail Ship completed, including final fixes.

Bundle:3 owned assets,55,982bytes, SHA `62C805B9BF4DD5A02E22DB53CA017CDEB9FE8545DD4C65273DFF1D1583348C1B`; no game models/textures. Installed QA0.19.203.0 DLL SHA `713376870BAD5674B98099944C6F12B10C5D45A21A64E34DFB0837E1EBC5851C`;143 protected files retained SHA/UTCmtime at final deployment.

## Native scope

Thunderstore QA Modded only; isolated prior saves verified before entry. Two native woodwalls, relative90°/A scale1/B scale1.15,0.90,1.10, separate field origins. Transform getters returned expected rounded strings, not byte-exact matrices. Actual capsule/movement: both damage0→1→1 standing→0 after exit/8s; smoke3.38867188/4.32421875. No Failure; both roots/reference removed. Native contact/recovery images inspected.

Final visible14 inventory entries/quantities/slots/displayed durability unchanged, Wood9; full inventory bytes not compared. Legacy WAL SHA `95A4C7430457CDF2130246421F144F607F2B4E4ED0F17894C447E553BB602CF8` unchanged. Protections restored, game closed normally, owned helper stopped, endpoint absent. No paid construction/project persistence/network executed by this renderer-only probe.

## Performance and limits

Initial native candidate observed14FPS idle/7 after contact readbacks versus232 with fog removed at3440×1440. Preserved diagnostic-original evidence is not a readiness claim. Replacing a ternary24-sample light call with explicit `[branch] if(mask>.005)` preserved formulas and image regressions. Final QA observations: full121, simulation-only232, surface-only228, volume-only123, full-again121; stable contact observation90. QA skips restored false before contact tests. These are single-scene observations, not a controlled GPU benchmark or whole-house/hardware guarantee; stage GPU timings and total memory remain unmeasured.

Initial ActiveOwners inspection assertion failed because computed getters are omitted; explicit getter returned2 without restarting visuals. Exit screenshot refresh found no target after normal process termination; absence separately verified. Evidence/source fingerprints and saved native frames: artifacts/fog-volume-203 and fog-runtime-pair; private game assets/logs/images/saves are not published.

Next bounded active-region admission for more concurrent contacts and heavy/native arbitrary transforms, then persistent unfinished project/stake/board/material continuation/cancel-refund/reload/server. Owner visual acceptance and whole-house performance remain open. Ordinary construction does not use this fog yet; costs/permissions/items unchanged. Full goal ACTIVE.
