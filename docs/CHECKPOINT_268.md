# Checkpoint268 — validate unpaid cancellation replay

2026-10-21. Under the existing project lock, `CancelUnpaid` checks pending removal before returning a cancelled record. Replay requires original revision1, the same operation and no part execution evidence. A paid terminal cannot use the unpaid path even when its removal journal is missing. Rev1/rev2 framing is unchanged; genuine replay remains byte-stable.

Both regressions were reproduced before fixing: wrong revision and a synthetic paid terminal without removal. Project checks PASS; cancellation52assertions and payout117assertions PASS. Initial sandbox access failure is retained separately from the actual logic repro. Normal/QA and host/localization checks are recorded in this checkpoint. No verify skill was available; focused tests and independent/Ponytail review were used.

Installed250 unchanged; no deployment. Local normal/QA251 artifacts remain unpromoted and embed the previous bundle; mask shader267 is separate. Git receives only three store/test files and docs, not the pending267 shader/tool edits or DLLs. This is not full paid cancellation or board-independent collection.

Two independent reviews recommend retaining active1/cancelled2, with pending/settled returns represented by the existing payout journal, replacing the older rev3/rev4 technical draft. Before board retirement: exact source-owned collection without OpenBoard, payer/world/blob/uniqueness/distance, compatible cancelled-plan journal binding, cleanup after durable credit. Native pickup/merge remain blocked. Zero-cost cancellation and restart/fault/replay need their own proof. No EarthWorks/terrain integration.

Evidence: `artifacts/checkpoint-268/`; [checklist](LIVE_CHECKLIST_RU.md), [draft protocol](PROJECT_CANCELLATION_PROTOCOL.md).
