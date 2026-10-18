# .226 — paid native continuation and world reload

18 October 2026 · [Русский](CHECKPOINT_226_RU.md)

The existing isolated QA world passed a bounded four-wall structural/local-host test.
This is not the completed unfinished-building module.

| Evidence | Result |
| --- | --- |
| Build and automated checks | Normal/QA0 errors/0 warnings; full Store/Geometry, host1.0.20/25909357, localization1118, independent review |
| Paid native scenario | 16/16 in3.3s; reconcile the existing operation without a second charge at81Wood, then a separate Continue builds3more walls for6Wood:81→75 |
| Paired persistence | Four Committed operations with exact TRS, unique operation markers, creator and receipts; saved native profile/world readback |
| World logout/login | 10/10 in1.2s;4parts/TRS/receipts and75Wood unchanged; project and4journal files byte-identical |
| Installation | QA0.19.226 only in TerrainRamp-1.0-Test; DLL SHA256 `43E1DC7AF5F5F68CB083D34CE47E75970CF1F026BF00D8DC06A761EF3FD640F7` |
| Cleanup | Normal logout/Application.Quit and3bridge DLLs restored; stand retained, no grants/resets/terrain edits |

Fixed causes: a half-buried test wall (fixture placement only), v109 item envelopes losing
v110 cheated provenance, a contradictory open-board input guard, and session-local ZDOIDs
being mistaken for persistent identity. Reload lookup now scans all matching-prefab ZDOs
for the exact unique operation marker, deduplicates native iteration results, then uses
the current network ID solely to locate the instance. Existing committed receipts remain immutable.
CPU regression uses the captured Wood110 record and rejects mislabeled109/lost provenance;
native snapshot inspection passed for16inventory entries. Exact property checks were retained.

Private evidence: `ValheimQA/artifacts/20261018-194820/paid-report.json`,
`reload-report.json`, project-before/after hashes, `mcp.jsonl`, `bepinex.log`,
`verification.json` and `restore.json`. Earlier refused attempts are retained in runs
185907,191122,193059 and193643; they were not silently treated as passes.

Still open: ordinary construction creating durable projects; v11 on unfinished parts;
native-prefab/whole-house visual and performance acceptance; paid cancellation/refunds;
shared storage, contents/mounts/admin restoration; server/remote authority. Recovery only
handles an exact prior Intent with unambiguous object and surviving stacks, excluding
sign/material state; Observed failures remain reconciliation work. No automatic retry/refund.
This proves handlers/state, not physical E/raycast/UGUI/HUD or a full post-build process restart.
The verify skill is unavailable; the listed direct checks/review were used.

Current work is standalone house construction without foundation/site preparation.
EarthWorks terrain integration comes later, and BuildWorks cancellation never changes terrain.
All14goal blocks remain open as appropriate in the live checklist.
