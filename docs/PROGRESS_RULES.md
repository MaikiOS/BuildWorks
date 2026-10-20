# BuildWorks progress-document rules

Document roles clarified at Ostrix's request on2026-10-20. [Русский](PROGRESS_RULES_RU.md).

- `GOAL_FULL_RU.md` and the actual goal hold the agreed scope and completion conditions.
- `LIVE_CHECKLIST_RU.md` is the owner-facing requirement checklist. Its14sections match the goal in title/order. Stable subpoint IDs identify delivered/open/current/manual work; technical chronology does not belong there.
- `WORK_STATUS_RU.md` holds current implementation evidence, limitations, causes, dependencies and the next technical action, keyed to the same sections/subpoints. Do not maintain a competing requirement list.
- `CURRENT_STATUS.md` and its Russian version provide a short public evidence matrix.
- `DEVELOPMENT_JOURNAL.md` and its Russian version hold dated changes, reasons, validation and report links. They do not replace the open requirements or roadmap.
- Checkpoint/manual reports and local artifacts contain exact versions, hashes, commands, counters, failures and reproducible procedures. `ROADMAP.md` and its Russian version describe future product milestones and dependencies.

## Checklist rules

1. Keep the goal's14sections and stable subpoint IDs. Append new items within their section; do not delete or renumber open/closed requirements for brevity. If the goal itself changes, record the mapping explicitly.
2. One line describes one observable outcome. `[x] Готово` means exactly that scoped outcome is proven. `[ ] Частично`, `В работе`, `Проверить вручную`, `Не сделано` remain open. Mark source-only/local-host/single-fixture/owner/network boundaries where material.
3. A technical experiment is evidence, not a newly completed product requirement. Saved sealed refund drops do not complete user collection or paid cancellation.
4. Replace the short current-action line and installed QA version when work changes. Do not prepend a checkpoint narrative after each turn. "In progress" describes an unfinished task, not a continuously running process.
5. Keep technical details in WORK_STATUS/reports and chronology in the journal. Preserve the complete goal and open items; do not infer overall completion from the number of checkmarks.
6. Retain Ostrix's accepted scoped cases. Repeat them only for an affected change, regression or specific new uncertainty, with the reason stated.
7. Keep a separate owner-test queue linked to subpoint IDs. Before assigning a test, verify the installed build and provide ready files/fixtures/authorized supplies, initial state, Russian UI actions, expected outcome and failure signs. A stale manual report must be refreshed first.
8. Distinguish source, automated checks, deployment, Valheim state, owner acceptance and host+remote evidence. Tool success/build success is not end-to-end proof. Retain failed reports unchanged.

## Entry templates and document boundary

A live item keeps its stable ID and observable player outcome: `- [ ] **8.7 В работе:** player outcome; what is available and what remains before this exact condition is met.` Keep internal classes, file-write protocols, hashes, assertion counts and checkpoint narratives in WORK_STATUS/reports. A technical subtask is not a newly completed goal requirement.

For the same ID, WORK_STATUS records implementation → evidence and tested version → unconfirmed/broken behavior → cause/dependency → next technical action → evidence link. Separate the verified snapshot from unfinished source edits; a new source file alone is not working behavior.

A journal entry records date → change and reason → verification result → report link. Append history there; update the current WORK_STATUS conclusions in place. Do not copy journal entries into the top of either current document.

Before saving, check that Ostrix can see which goal item is done/open, what is being worked on and whether owner action is needed without reading internal protocols or build history. Simplify the checklist entry if this boundary has drifted.

After a verified step, update its existing checklist items, current technical evidence, dated journal and test readiness. Verify14sections/IDs/links and scope preservation, then checkpoint/sync under existing authorization. Documentation-only corrections do not require a new DLL or game launch.

Document edits do not change the Goal API. Do not claim a goal transition when only files changed. Pause/resume follows Ostrix; completion requires the full objective audit. The pre-reorganization text is retained in [a historical snapshot](PROGRESS_SNAPSHOT_252_RU.md), not another current checklist.
