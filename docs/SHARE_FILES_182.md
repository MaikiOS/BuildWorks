<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .182 — portable files and Share

**English** | [Русский](SHARE_FILES_182_RU.md)

Implemented, checked and installed as QA .182. The complete BuildWorks goal remains open; no Valheim or network acceptance is claimed.

| Scenario | Implementation | Evidence |
| --- | --- | --- |
| Save/update owned blueprint | Commit primary library, then update full `share/<id>.buildworks.json`; rename/category/preview keep the same file | Store ShareFileChecks, actual Controller bridge |
| Share | Editor catalog and Hammer use one result dialog: complete path, copy path, open folder | Actual UI: saved version, dirty draft/history intact, conflict and retry |
| Partial failure | Primary commit stays successful; failed files remain tracked by ID until retry/delete | A fails → B succeeds → A still pending; retry writes derived files only, no repeated Save As |
| External card | Full external hash filename and compatible portable GUID snapshot; provenance and internal links retained | Immutable source bytes/sourceDocuments/part IDs and no native card |
| Delete | Validate managed file ownership; restore derived file if primary commit fails | Foreign-file conflict, locked library and rollback |

`blueprints.json` remains authoritative. `share` is separate from scanned `imports` and server publication. Old libraries are not bulk migrated; the next save or Share creates the file. Pending failure tracking lasts for the current Store session; Share can prepare the file again after restart. No new persistence schema, service, dependency or player permission is introduced.

Copies contain all data supported by the native portable format, including source evidence. Archiving unknown fields is not recovered behavior. Foreign IDs, invalid/future documents, reparse paths and oversized files are rejected before replacement. Portable IDs are not the identity of new world/network objects.

Final normal/QA builds: 0 warnings/errors. Store, Geometry, Localization runtime, Controller bridge, actual Session layout, actual exchange Tick, Host and deployment guards passed. Localization:1030 keys. Final Unity:540 screenshots at4 resolutions/3 scales, plus actual Share/draft/failure/Save As retry interactions. Russian ready/error windows inspected at1280×720/140%. Correctness PASSED; Ponytail Ship after fixing the pending-failure reset. Formatter still reports135 inherited WHITESPACE diagnostics, identical to181 after normalizing shifted line numbers; no new diagnostics. `verify` is absent; direct checks/reviews are used.

Evidence and failed runs: `artifacts/checkpoint-0.19.182`. Installed DLL pair hashes and all103 config/token/provision file comparisons are recorded separately. No game/model/input/save changes; catalog remains0 owned/0 imports.

Next: batch import/export, then server library and personal player transfers. Contents, v11 projects, world selection, heavy blueprints, admin and network requirements remain open in the full goal. [Manual QA](MANUAL_QA_182.md).
