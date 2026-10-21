# Current BuildWorks status

2026-10-21. Installed QA **.250 unchanged**. [267](CHECKPOINT_267.md): short stationary Valheim diagnostics confirm queue growth; local shader candidate saves5.69% isolated elapsed, not a house fix. [268](CHECKPOINT_268.md): unpaid cancellation replay hardened in source/tests, no deployment.

Polling overhead is included; pure GPU time is unmeasured. Full house/v11/paid cancellation remain open. Shader/tool267 edits stay local; source-only268 publishes only the store/test fix and docs.

Requirements/current task: [owner checklist](LIVE_CHECKLIST_RU.md). Technical evidence/causes: [WORK_STATUS](WORK_STATUS_RU.md). History: [journal](DEVELOPMENT_JOURNAL.md). [Document rules](PROGRESS_RULES.md).

| Area | Implemented / automated evidence | Valheim or user evidence | Still open |
| --- | --- | --- | --- |
| Precision placement and editor | XYZ, paired axes, 1–1000% component scale, groups, anchors, Undo/Redo, Save As, filters and interior camera | Owner accepted specific scale/save/camera/catalog cases; ordinary snap checked in .193 | Broader rotated/modded contacts, pointer wrapping edge cases and full visual acceptance |
| Imported construction | Explicit prefab migration, supported text/material paths, real native pieces | Forest Hut (28) paid pieces and exact transform/inventory reload in [.195](ROTATION_PERSISTENCE_195.md) | All formats/prefabs, constructive state, large buildings and networking |
| Formats and missing parts | Native store25/portable14; supported .blueprint/.vbuild import, reviewed export, missing records/bounds/copy alignment | Limited import/editor cases accepted | Unknown models/behavior are not restored by archiving; wider native corpus acceptance |
| Catalog and file exchange | Categories, Save As, delete/hide, automatic portable file, Share, batch import/export | Limited batch .197 import/export/reentry proved | Full batch UI/dirty draft/restart; some owner checks remain |
| Server library and sharing | Administrator publication; players/admin visibility, verified personal copies, provenance; in-process checks | Host+remote not accepted | Live access/reconnect/restart; direct player-to-player transfer |
| Items and storage | Separate storage/mount inclusion; supplied-item preservation and journal foundations | Limited chest/inventory comparison only | Durable settlement, cancellation/refund/crash/replay, all holders/armor/admin restore and network authority |
| v11 fog | Renderer/unpaid28; local mask candidate453/15/133/14exact |267:82renders/~6.6s,queue56→508;265/264 full run FAILED;installed250 | Queue service headroom,v11 appearance/walk/house/heavy builds/network |
| Unfinished construction | .232 Hammer/plan/board/ProjectPart; .244 sealed payout; .243 removal/Prepared resume/freeze | 260:24+24, remaining3 sources cleaned/four CleanupSaved total, fresh app/replay without effects;67Wood/4receipts; earlier payment/continuation/TRS remain scoped | All objects/contents,full restitution/cancellation/general recovery,secondary collapse,HUD/native E,server/remote |
| World selection and heavy chain | Capture/volume foundations and targeted checks | Basic capture accepted; later coordinate/height UX issues reported | Editable volume/gizmos, boundary rules, full loaded-area and performance acceptance |
| World objects and EarthWorks | No terrain integration dependency | None claimed | Admin-priced nonrecipe objects; EarthWorks is a later separate integration |

Source, tests, deployment, Valheim, owner and host+remote remain separate. [265](CHECKPOINT_265.md) retains the failed candidate trial; passing failure audit is not a passed scenario. Appearance/smoothness,full terminal/public cancellation,rollback/network remain open.

Prior chronology and failures remain in [the historical snapshot](PROGRESS_SNAPSHOT_252_RU.md) and checkpoint reports; [the roadmap](ROADMAP.md) describes future product milestones. The goal mechanism text is unchanged; .250 installation is separate from the native .249 series.
