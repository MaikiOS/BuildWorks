## 2026-10-10 — QA0.19.135: positioning and scale boundaries

Code ba977c4. Separate Align action retains the missing record/marker. Recorded
bounds centre aligns with visible own-part bounds after full rotation, using
prefab/pinned scales. Unknown dimensions use only saved origin/rotation with an
explicit notice. Own sizes/layout, groups, source, supports and old replacement
remain intact. Atomic Undo, cancel, catalogue, stale refusal and Save/reopen tested.
One button row avoids clipping at1280x720. Geometry/full Store, normal/QA0/0,
Host25185596, localization944, isolation/preflight and Unity492 at13:49:51.6814707UTC
passed; final correctness/Ponytail PASSED/Ship. verify unavailable.
Guarded installation only to TerrainRamp-1.0-Test; both hashes match, Default
clean,137 existing config/import/save files unchanged. No game/save/network run.
Includes .133 warm source copy and .134 confirmable1–1000% scale limits.
Manual instructions: docs/QA_0.19.135_RU.md. Dual-axis and continuous mouse pending.
