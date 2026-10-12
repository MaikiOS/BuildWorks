# Local camera/walk and TEST24 — 2026-10-12

Installed QA0.19.165 was unchanged. Native console submission, ghost/god on and
arrival at2560/41.099388/128 are confirmed by replies and viewed frames. The owner
recovered the grave and handed input back; no items or terrain were reissued.

The existing Python runner adds `--scenario camera-walk`. Qwen3.5:4b chose the two
bounded stages without Codex between actions. Run20261012-174106-d4f55d82 completed
in12.765s: yaw+3.008°/0m, then forward0.622m/no turn. Model replies7.579s/0.829s
include loads6.427s/0.042s. All15 inventory stacks stayed unchanged. This is a
host-assisted scripted pilot, not accepted pure vision or free navigation.

Fresh session/stop/focus/inventory/pose checks, yard/range/time limits and no-replay
remain. Five CPU tests pass. Independent review caught a mostly sideways step;
regression RED, forward-projection fix GREEN, re-review PASSED/Ponytail Ship.
Verify skill is unavailable. A prior outside-yard run and stale-revision command
refused before input; they remain recorded failures, not successful actions.

Codex then used native input to select TEST24, disable storage and mount contents
independently, observe its ghost and build4/4. An initial NoRayHits selection
reported ghost_missing; ground-facing re-selection recovered. Cause remains
unproven. A malformed pointer command produced no UI change before correction.
Native Valid alone is not visual proof.

Native build at18:00:33 consumed14Wood/4FineWood/1BronzeNails. Remaining Wood30+11,
FineWood0/Nails0, two SwordIron retained; other counts unchanged. The hammer was
removed and the real chest opened empty. New stand is near2560/41/124. Evidence:
`artifacts/local-qa-agent/runtime-165-20261012`, frames31–35 and construction log36.
The [Russian protocol](LOCAL_QA_MOTION_2026-10-12_RU.md) records exact positions.

Next: owner relaunches the same isolated world through Thunderstore Modded, then
check four pieces, empty chest/mount, balance and no reissue. No new DLL needed.
Reload, full snap matrix, actual F12, FPS/memory, item attributes, filled storage,
network and autonomous hammer/build remain open. Goal API currently reports paused;
documents do not resume it. Proposed insertion point corrected to section13 before
the current order-of-work section (renumber that section14).
