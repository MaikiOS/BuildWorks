# Checkpoint274 — manual fog-house handoff

**Outcome: owner acceptance FAILED.** Fog queue overflow hid the house;boards were malformed/unusable. The initial pre-fault handoff snapshot is retained below;final outcome/cleanup follows.

2026-10-21. At Ostrix request,QA **0.19.252** installed and **visible** Valheim launched through preparedGABS withTerrainRamp-1.0-Test BepInEx only. Gameplay source270+shader271;receipt grouping269excluded,273rejected. BundleSHA `0BF387946A0215DA98087D796E3B34C02DE52A82AA9602D5CAE2E0CBE42A5A66`. Visual acceptance candidate,not public release.

Normal/QA0warnings/0errors,host/localization1122/1008 PASS. Firsthost check refused expected251;expectedversion updated252 and check repeated. Guarded deploy verified QA DLLSHA `9B55675D114D71B480338889E31EC6BC19C4732C52F74C970219BC01F4F6893E`,Geometry `0C27A7FBA028F97C065602B632A05BFD7B84AC9065F675600140CB8DE8CB8916`. Otherprofiles untouched.

PID34324,Valheimwindowhandle4002244,batchMode=false,isolatedQA/cloud=false,existingBW_QA_43d3719a world/character. background=true isApplication.runInBackground,not hiddenwindow. PassiveMobs/sessiongod+ghost verified,noon pinned. Created unpaid28part project,no resource grant/native building/terrain edit. ID `190ab9eea6ad46e28993f1b66008f207`,centre `(2568.474,43.360,110.140)`;player placed nearby facinghouse.

Handoff status:ready,visible28,board,nonphysical,inventoryUnchanged;no failure/inputblock. No auto-route/recorder/removal timer;leave house/input to owner. Appearance/smoothness await Ostrix;lastmaxQueue54does not prove headroom.

Evidence:`artifacts/checkpoint-274/` build/host/localization/deploy/session proofs;activeQA `../ValheimQA/artifacts/20261021-101656/` logs/backups/deployment. Game intentionally left running and bridge retained. After normal owner exit,collectresult/logs and runRestore-QAProbe for this run;cleanup is not yet complete.


## Owner acceptance: FAILED

The house disappeared; boards were malformed, white, floating and unusable with a hammer. BepInEx confirms `Fog input queue is full; observation rejected, accepted history retained`; the error handler hides the house. No removal timer was involved. Initial ready/visible28 was only a pre-fault snapshot.

After normal owner exit, logs were retained in `../ValheimQA/artifacts/20261021-101656/bepinex-owner-failure.log`; restore.json verifies all three bridge DLLs restored. Old paid projects/journals were preserved.
