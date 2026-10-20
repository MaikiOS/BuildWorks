# Current BuildWorks status

2026-10-20. QA **0.19.250**, gameplay DLL/shader unchanged. Actual render-only house run **FAILED**: fog input queue overflow at11–14FPS. [264](CHECKPOINT_264.md).

854Camera.Render calls/52.072s,493production fog frames at cleanup;36FPS only after failure. EarlyStop7/ownership3/cleanup pass, no second route/final recovery. Next exact sparse-lookup optimization with parity and no appearance/quality change. Full board cancellation remains open.

Requirements/current task: [owner checklist](LIVE_CHECKLIST_RU.md). Technical evidence/causes: [WORK_STATUS](WORK_STATUS_RU.md). History: [journal](DEVELOPMENT_JOURNAL.md). [Document rules](PROGRESS_RULES.md).

| Area | Implemented / automated evidence | Valheim or user evidence | Still open |
| --- | --- | --- | --- |
| Precision placement and editor | XYZ, paired axes, 1–1000% component scale, groups, anchors, Undo/Redo, Save As, filters and interior camera | Owner accepted specific scale/save/camera/catalog cases; ordinary snap checked in .193 | Broader rotated/modded contacts, pointer wrapping edge cases and full visual acceptance |
| Imported construction | Explicit prefab migration, supported text/material paths, real native pieces | Forest Hut (28) paid pieces and exact transform/inventory reload in [.195](ROTATION_PERSISTENCE_195.md) | All formats/prefabs, constructive state, large buildings and networking |
| Formats and missing parts | Native store25/portable14; supported .blueprint/.vbuild import, reviewed export, missing records/bounds/copy alignment | Limited import/editor cases accepted | Unknown models/behavior are not restored by archiving; wider native corpus acceptance |
| Catalog and file exchange | Categories, Save As, delete/hide, automatic portable file, Share, batch import/export | Limited batch .197 import/export/reentry proved | Full batch UI/dirty draft/restart; some owner checks remain |
| Server library and sharing | Administrator publication; players/admin visibility, verified personal copies, provenance; in-process checks | Host+remote not accepted | Live access/reconnect/restart; direct player-to-player transfer |
| Items and storage | Separate storage/mount inclusion; supplied-item preservation and journal foundations | Limited chest/inventory comparison only | Durable settlement, cancellation/refund/crash/replay, all holders/armor/admin restore and network authority |
| v11 fog | Production renderer/unpaid28-part plan; external QA CaptureAt |264:real rendering FAILED11..14FPS/queue overflow;263:simulation-only;262:Clear still/FAILED343/520 | Matched v11/smoothness,measured render cadence,complete repeat recording,GPU budget/heavy houses/network |
| Unfinished construction | .232 Hammer/plan/board/ProjectPart; .244 sealed payout; .243 removal/Prepared resume/freeze | 260:24+24, remaining3 sources cleaned/four CleanupSaved total, fresh app/replay without effects;67Wood/4receipts; earlier payment/continuation/TRS remain scoped | All objects/contents,full restitution/cancellation/general recovery,secondary collapse,HUD/native E,server/remote |
| World selection and heavy chain | Capture/volume foundations and targeted checks | Basic capture accepted; later coordinate/height UX issues reported | Editable volume/gizmos, boundary rules, full loaded-area and performance acceptance |
| World objects and EarthWorks | No terrain integration dependency | None claimed | Admin-priced nonrecipe objects; EarthWorks is a later separate integration |

Source, tests, deployment, Valheim, owner and host+remote remain separate. [264](CHECKPOINT_264.md) retains the reproduced house failure; passing failure audit is not a passed scenario. Appearance/smoothness,full terminal/public cancellation,rollback/network remain open.

Prior chronology and failures remain in [the historical snapshot](PROGRESS_SNAPSHOT_252_RU.md) and checkpoint reports; [the roadmap](ROADMAP.md) describes future product milestones. The goal mechanism text is unchanged; .250 installation is separate from the native .249 series.
