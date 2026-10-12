# Local QA agent — first pilot, 2026-10-12

Ostrix clarified that the supplied review meant continuing local game-control
preparation. Qwen3.5:4b and LiquidAI LFM2.5-VL-3B Q4_K_M were downloaded into
existing Ollama. Qwen3.5 is selected for the bounded pilot. QA .164 installed,
both source/installed SHA match,166 config/import/save files retain bytes/size/mtime.
No game launched. deployment-verification.json under artifacts/local-game-agent-2026-10-12.

Actual pure-vision probe: three pinned .162 frames, two requests each. Complete
four-field matches: Qwen3.5 2/6, Qwen3-VL 4B 2/6, LFM0/6. First request latency
14.022/4.651/6.470s respectively; remaining ranges .431–.637/.336–.536/.275–.700s.
These models confuse fine HUD/empty grid details. Pure visual autonomy is not
accepted. The exact pilot prompt, with archived image **and recorded native UI
state**, selected the right command6/6: first7.375s, remaining.265–.344s.
This is not a live input/FPS/full-cycle test or a reliable percentile estimate.
Reports: artifacts/qa-vision/20261012-162023-8a594ebc,
20261012-162130-426b598b, 20261012-162407-b787d630;
artifacts/local-qa-agent/offline-a463edc0/report.json.

Run-LocalQAAgent.py reuses Invoke-QAControl.ps1. Local loop: observe/image,
model decision, fresh guard, bounded native Tab, native result check. Only
inventory/observe/stop decisions; no move/build/transfer/grant/game-launch/save
API. Completion requires InventoryGui.IsVisible true then false, same inventory
summary and finite position within15cm. Fixed isolated QA world, nonce, focused
living safe session, known UI module, 90s limit, exclusive runner lock, no replay
after timeout or unknown result. Reports contain JSON/Markdown/JSONL/screenshots.
Stop requests are not claimed as confirmed releases. Completed actions are not undone.

QA .164 adds persistent stopRevision to observations and optional guarded batch
requests. F12 during model inference or focus loss invalidates the decision,
including the race before a batch arrives. Physical intervention detection remains
the existing during-batch guard; no new between-batch detector is claimed.
Immediate host emergency stop is F12; report-directory stop.flag prevents the
next step. A crashed runner may leave its lock; inspect uncertain native state
before manually removing it. Never automatically restart/replay the pilot.

CPU4 tests include mocked full success, stop-after-model, unknown command, native
no-change and timeout without replay; finite/type guards cover both positions.
Protocol31 plus publisher, normal/QA0/0, Host25185596, localization989, compiled
isolation and deploy preflight pass. verify skill absent. First sandbox publisher
failed File.Replace; unsandboxed passed. Initial HostContract .163 version guard
was updated to .164. Independent review found NaN, fixed; final PASSED/Ponytail Ship.

After verified .164 deployment, launch only Thunderstore TerrainRamp-1.0-Test
Modded, same BW_QA_43d3719a identity. Stand safely on flat ground, close native
inventory/menus, keep game focused. First validate fresh loader/nonce/image; then
python.exe scripts/Run-LocalQAAgent.py --run. Expect native inventory open/close,
unchanged summary/position and two fresh matching receipts. Press F12 on unexpected
movement/UI/items or continued actions after stopping. Test F12 during inference
separately. No new grants, world reset or save edits. Hammer/snap/build/reload/network
scenarios remain outside this pilot and unfinished.

--self-test is CPU only; --probe-decisions loads the model on archived evidence
with no game input. See the Russian companion for exact user test instructions.
