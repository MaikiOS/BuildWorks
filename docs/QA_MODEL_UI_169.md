<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# QA .168/.169: live evidence and manual acceptance

On2026-10-12 Ostrix stopped work on the local model tester. No further model inference or game input is scheduled. QA0.19.169 is already installed; [manual checks](MANUAL_QA_169_RU.md) need no new deployment. [Detailed evidence](QA_MODEL_UI_169_RU.md).

The actual .167 reply omitted uiTargets. .168 replaces Unity JsonUtility with DataContractJsonSerializer and tests published bytes rather than a captured object. Valheim confirms the array. .169 prioritizes scoped BuildWorks controls before the32-target cap, without changing raycast/occlusion/stale-ID guards; confirmed live. Exact permitted labels now constrain shared Button names.

Whole-mission file criteria require a previously absent store name/export file, nonempty ID, exact prefab multiset and unique target criteria. They do not prove transforms, editor reopening, costs, construction or reload. Hidden pwsh CP866 output is fixed with explicit UTF-8. The9167-token task exceeded6144; context is now16384. Model output schemas restrict UI choices to current permitted IDs and writable focused text fields. That final schema change passed CPU/review but has not run live.

Real attempts:212922 failed decoding before inference/input;213111 model HTTP400/context before input;213228 issued three model-chosen E presses, then attempted to label Repair as Import. Guard rejected the click before dispatch. Store SHA503E4E38EE123FEFE9841774FCAF317A751BEBD8A4C37B74E7C4F0BE598F3CCB/inventory13stacks unchanged, no new export.213510 failed before inference because process19392 no longer existed; cause is unproven. No complete UI mission or S01–S12 success. Journals preserved locally; no agent.lock.

Normal+QA .169 builds0warnings/errors; Respond12 actual-byte cases; protocol43/UI45/publisher; QA/native/Host Steam25185596/989localizations/compiled isolation pass. Python mission10 tests include15 mocked loops; independent reviews PASSED/Ponytail Ship. verify unavailable. Game tests remain distinct from these checks.

Installed BuildWorks SHA EC48BBB4CFB17D06CA21A39C715509B0E24E61C577A5D400D8BE3A0DABE11697; Geometry EC2315A128CA4AF1B19DD5BE69A93B69F18BB3F558F018C6C36A17D0E22C0261. Deployment only through Deploy-TestBuild.ps1 into TerrainRamp-1.0-Test; launch through Thunderstore Modded. QA god/ghost enabled, no nocost/debugmode. No main-save/other-mod changes.

18 unchanged manual fixtures are collected locally with hashes/provenance. External Forest Hut/CoffeeTable and all user data/logs/DLLs stay outside Git. Manual instructions exclude previously accepted cases and mark missing supplies, populated inventories/admin restore/persistent projects/fogv11/heavy construction/network as unfinished or blocked, never passed.
