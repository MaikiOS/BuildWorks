# 296 — settled refund reload without replay effects

22October2026. InstalledQA267, fresh PID20084, batch/background DX11; actual TerrainRamp-1.0-Test/qaSaveRoot/cloud=false, existing identities/world1137745799. **16/16**, optional0,1.9s scenario after loading. No construction/grants/resets/render checks.

Both old settled debts remain cancelled/terminalBound, no board/count/units; each has2credits/2saved RAM+disk receipts,2CleanupSaved,0pending/sources. Current worker counters show no repeated save/credit/destruction requests; these are not permanent call tracing. Independent read-only review verified cancelled startup skips paid finalization and settled cleanup does not create a worker. All3inventories exact23entries/67Wood; all97project file hashes unchanged. Four receipts retained, no replay effect observed. Sources0 scans loaded ItemDrops, not every saved-world chunk.

SelectPayout.inventoryUnchanged/exactLastInventory compare against historical issuance/collection snapshots; false there does not indicate a current-run loss. Exact current-run comparisons and CleanupStatus.inventoryUnchanged were independently required.

Normal logout/menu/Quit/exit20084 and3bridge hash restorations verified.4141ordinary metadata unchanged; contents not hashed. Only9QA-save/control differences among250config baseline files; settings/projects unchanged. Only known optional backpack/menu/logout warnings.

Scope: startup/reload of these already collected private-local debts, not fresh before-pickup lifecycle, physical E, crash between terminal bind/plan replace, wider items/contents, paid UI or remote. Fog appearance/performance untested. Evidence: `artifacts/checkpoint-296/`, `ValheimQA/artifacts/20261022-081937/`.
