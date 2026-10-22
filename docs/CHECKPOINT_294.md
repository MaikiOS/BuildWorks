# 294 — native depth metadata and retained refund closure

22October2026. Installed QA267, existing isolated identities, non-batch DX11, ordinary3440×1440 camera/target0. **31/31**, optional0, no grants/construction/terrain. Temporary plane292 override removed afterwards.

Actual render callback saw Deferred Depth3440×1440, Unity Texture filterMode Point and texelSize(1/3440,1/1440,3440,1440). One read, same camera908136, postRender7→137, productionFrames145. This observes texture metadata, not hardware sampler state. No extra Camera.Render/readback/screenshot.

Validator corrected its invalid strict depthFrame>RPC hostFrame assumption: callbacks may execute later in that same frame. It requires >= plus initial reads0/seenfalse and actual callback progression. Gameplay guards retained.

Finalizer290 closed two previously fully collected Wood debts at startup: cancelled/terminalBound, no board, zero payout/source units; all3inventory snapshots exact23entries/67Wood. Only4of97files changed: two plans rev1→2 with exact cancel operation, two payout journals rev15→16 with Terminal. Independent read-only audit verified framing/checksums, unchanged remaining plan payload and unchanged Items/Collections/receipts. No new credit.

Normal logout/menu/Quit PID48032, restore3/hash checks.4141ordinary save metadata unchanged; contents not hashed. Configuration values unchanged; version comment changed. QA save/control changes expected. Fresh-process reload separately in [296](CHECKPOINT_296.md).

Scope: old settled private-local debts, not before-pickup lifecycle, physical E, crash windows, remote or visual acceptance. Evidence: `artifacts/checkpoint-294/` and `ValheimQA/artifacts/20261022-074720/`.
