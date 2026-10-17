<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .108 — staged heavy editor loading and virtual outliner

QA .108 installed; hashes match,48 QA files and library unchanged. Checkpoint:e51c840. No game execution.

Automated checks passed; deployment and Valheim acceptance are separate evidence.

Documents over128 parts load at most16 visuals or8 ms per Update, always advancing
at least one part. The loading dialog supports cancellation; editing is blocked until
completion. Cancellation/failure disposes the partial scene without saving. One complex
part and final scene passes are not bounded by this frame budget.

Trees over256 rows instantiate only visible rows plus overscan. Full records, groups,
supports, contents and missing-prefab entries remain in the document. Warning aggregation
deduplicates strings. Legacy entrypoints route through the existing modern controller.

Normal builds retain512 parts. QA builds allow50000 only after validating the loaded,
isolated local world/player with no peers. UNITY_EDITOR Workbench has the same test
capacity. File/number/memory/geometry protections remain. World capture still has512;
heavy editing, history, save and finalization require further profiling.

Evidence: normal/QA0 errors/warnings; GeometryTests, StoreTests, HostContract,
localization856, QA isolation and whitespace. Actual Unity controller exercised1025
external records: bounded updates, cancel/reopen, failure cleanup, virtual last row,
missing transform/Undo/Redo, native save/reopen, unchanged source and stale-revision
rejection.68 load updates, about1.9s including yielded frames; not game FPS evidence.
Full Workbench passed252 screenshots across4 resolutions and3 UI scales. Independent
correctness and ponytail review passed.

A separate FogGhostStudy.Preview occupied the normal Workbench; it was not stopped.
Tests used artifacts/ui-workbench-108. ProjectPath accepts only direct tools/artifacts
copies. Obsolete test expectations for Editor512 and native format15 were corrected;
the actual format remains native16/portable6.

No Valheim execution. Lava Lantern world scale, heavy construction/reload, server,
durable jobs and administrative restoration remain open. See WORK_STATUS_RU.md.
