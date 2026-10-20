# 262 — close house view and a separately retained recording failure

2026-10-20. QA **0.19.249**, its gameplay DLL/Geometry and shader remain unchanged. Only the external ValheimQA capture helper changed. **The17/17-step still control and5 images passed; the new long recording failed.** These results are not combined into an overall pass. [Русский](CHECKPOINT_262_RU.md).

## Controlled stills

`../ValheimQA/artifacts/20261020-193106/` uses existing isolated QA identities, cloud=false, verified PassiveMobs and session god/ghost. One owned unpaid28-part Forest Hut/board:12setup and5close checks. Five1280×720images share one camera: original environment, prior weather at noon, Clear, another idle Clear frame, then the same location after canceling only the owned project. The latter is not an empty-world claim.

This run already began in **Clear**, with noon pinned. The close active-house image has no broad white band; the post-close baseline is clean too. Earlier262 run192241 began in **Misty** and shows strong haze. Blueprint geometry, root/all28part poses and camera coordinates were independently compared and match. Runs/animation times differ, so this is not pixel parity or complete causal isolation. Evidence supports an environment/weather contribution and does not justify calling the band a permanent BuildWorks defect. Weather/effect interaction remains unresolved; instant env/status changes alone do not establish a settled image.

Diagnostic sheet: `artifacts/checkpoint-262/weather-house-control.png`; observations in `visual-observations.json`. No new approved v11 reference. Source review found nonzero beam scatter at zero density inside the expanded volume; frozen v11 has a similar expression. That is a candidate contribution, not proof that it caused the whole white band. Shader was not tuned to that hypothesis.

## Retained failures

| Run | Actual outcome | Meaning |
| --- | --- | --- |
|192241 |12/12setup, three stills, batch fails at step10 when CaptureAt correctly refuses recorder ownership | test/run_file unconditionally treats success=false as failure before expect; negative test moved outside that runner. Original failure preserved |
|192629 |Three range/degenerate-view guards and a separate ownership guard pass; its3-frame recording completes. Main batch fails at step45,44successful | Long recording FAILED: `Recording backpressure: no frame may be silently dropped.` No complete520-frame result |

Second run wrote343consecutive PNGs, submitted/read back344of520requested, ended failed and released all resources with no pending readback/writer. The512MiB output cap was not reached. Committed-frame submission-to-readback-processing latency averaged104.84ms/max169.14ms; encode averaged24.22ms. These are not GPU execution timings. Both CPU slots were occupied; no silent frame drop was accepted. Independent review found no demonstrated slot leak. Buffer/queue/interval/520-frame checks were not relaxed. Successful261 recordings remain separate evidence and do not pass this attempt.

Run193106 is a distinct still/background control without long recording. Its17/17 does not replace12/80/5, smoothness or traversal appearance acceptance. Next: observe native house/traversal cadence without PNG-readback load, then calibrate bounded capture and rerun the complete no-drop series.

## Small QA addition and isolation

Existing `ValheimQA/render-probe/RenderProbe.cs` gains CaptureAt(position,lookAt), using the existing Read/camera/RT cleanup. It requires a loaded isolated QA world, finite distinct points within64m of the player and no recorder ownership. Existing Capture remains. No new controller, dependency or permanent mod. The helper is loaded from the workspace into one QA process and does not replace BuildWorks.dll.

Helper build:0warnings/errors; Python protocol suite6/6. Native checks cover three invalid range/degenerate views with no new PNG and refusal while recorder-owned; the separate3-frame recording, pixels and camera restoration were verified. Nonfinite/wrong-profile guards were source-reviewed; another player profile was not launched. Sources/hashes retained in `artifacts/checkpoint-262/source/` and `source-snapshot.json`. `verify` skill absent; build/targeted native checks/independent reviews used.

All three runs create and cancel only their own unpaid plan. Saved checksums/identity/world/revision2/cancellation operation verified.81/83/85 prior project-file hashes remain; existing refund journals/inventory preserved. No grants, native construction, terrain/reset/catalog changes. Original weather/time pins and exact debugTime restored with readback, without advancing network time or promising the same random live weather instantly.4141 ordinary save files retain membership/size/mtime; contents not hashed. No unexpected config changes. Every logout→menu→Quit completes and restore3 verifies. Installed QA hashes remain `AB286BF596FB39E92C0974433181261DED5D4261CCF513209D50AA4A928311F5` and Geometry `EE7C108E682F0CB6FDA85AB56EA8CEA63F7D41637C5BF2DD0CFA7E46DA3BF457`; no other profile updated.

Evidence: `artifacts/checkpoint-262/{control-verification,recording-failure,session-verification,visual-observations}.json`, collectors/snapshots/reviews and the three native runs. Both failures remain failed. All14sections/115IDs/16scoped checks are retained without status upgrades. Goal API remains active/objective unchanged. v11 9.2–9.7, paid terminal/public board cancellation8.8, faults/rollback and networking remain open.
