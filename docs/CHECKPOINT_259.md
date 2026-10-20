# 259 — native refund cleanup and matched v11 footage

2026-10-20. QA **0.19.249** installed only in TerrainRamp-1.0-Test. **12/12 +9/9** new native steps pass, including a full application restart. Checklist8.7/8.8 and v11/house9.2–9.7 remain open. [Русский подробный отчёт](CHECKPOINT_259_RU.md).

## Change and verification

The [258](CHECKPOINT_258.md) executor ran in Valheim for the first time. .248 stopped before effects because JsonUtility omitted a nested QA credit object. .249 flattens five QA-only status fields; gameplay cleanup and scenario assertions are unchanged. Version/host pin updated. No new snap fix or gameplay fog/shader change.

Normal/QA builds0errors/0warnings;22 actual native-guard and31 actual Tick assertions with inert host boundaries PASS; host/localization1122keys/1008references PASS. `verify` is unavailable. Initial restore encountered `NuGet.targets(196,5): error : Access to the path is denied.` Build with unchanged dependencies and `--no-restore` passed; initial failure preserved.

Installed QA SHA256 `AB286BF596FB39E92C0974433181261DED5D4261CCF513209D50AA4A928311F5`; normal `C5C01A472F2DEAB4A2CB006592E7C00DFE2440E65775C8D8552A5080D9F807B4`; Geometry `EE7C108E682F0CB6FDA85AB56EA8CEA63F7D41637C5BF2DD0CFA7E46DA3BF457`.

## Native results

Existing isolated BW_QA_43d3719a, local world1137745799/cloud=false. PassiveMobs=true and session-wide god/ghost verified. No grants, new construction, resets or recredit. Only Collections[0] of retained project `064a5fd538b442d98a4c1c0935a6d2ba` cleaned; second project `fe485bcc1a4041d591d5a1015060d913` unchanged.

| Check | Result | Limit |
| --- | --- | --- |
| First cleanup12/12 | Exact source absent from both registries; world save172→173, CleanupSaved, destroy/world/profile attempts1/1/1 | One retained entry |
| Full app restart9/9 | Saved cleanup survives; replay attempts0/0/0 | Normal restart, no crash/rollback injection |
| Profile |67Wood, exact credited inventory, all4 permanent receipt key/value pairs independently recomputed | Two retained4-Wood debts |
| Project files | One intended payout transition; PaidRows preserved; other76 of77 files unchanged | Not a whole-world assertion |
| Replay files | Canonical profile and both payout hashes identical around replay | Later normal exit saves normally |
| Session cleanup | All4 native sessions quit normally and restore3 bridge DLLs;4141 ordinary save files preserve membership/size/mtime | No ordinary-save content hashes |

Success evidence: `../ValheimQA/artifacts/20261020-173042/cleanup-first-report.json` and `20261020-174000/replay-report.json`. Earlier replay `20261020-173553` stopped at teleport before cleanup; corrected runner waits for actual cooldown≥2 and no teleport, preserving nine scenario assertions. .248 pre-effect failure `20261020-172122` retained; project hashes unchanged. Independent native review PASS.

Config audit separates expected isolated QA saves/control, the single audited payout and the BuildWorks247→249 version comment from settings. Values and unrelated configurations unchanged. The initial overbroad audit incorrectly included expected QA save writes; final report lists every difference explicitly.

## Matched visual diagnostic

Own Editor harness records frozen v11 sequential, current dense sequential and current published joint-volume streams with matched camera/trajectory/source fields. Frozen assets unchanged. Single-wall control5triples/3source comparisons: published/dense MAE≤9.28e-7. Four-section selected probe20triples/48comparisons. Full run **1444triples/4332PNG**,361continuous frames for each of4paths.

Local video: `artifacts/checkpoint-259/matched-video/v11-longwall-four-paths-comparison.mp4`. Left=frozen, middle=current dense, right=current published. Two across-wall and two along-wall paths; not four independent surface normals. Fixed30Hz simulation is not native FPS/player proof. Four-owner frozen composition is a diagnostic extension of the approved single-owner scene; order-dependent sequential rendering is not declared an approved joint-volume baseline.

Frozen/dense MAE≤4.89e-5; published/dense reaches0.00204. Inspected contact-sheet frames show different light trails. This isolates a multi-owner path difference without deciding which is visually correct. Continuous capture completeness does not imply human inspection of every frame. Composition investigation, native house and owner appearance acceptance remain necessary.

Initial probe reported12 persistent allocations. Harness now explicitly releases manually evaluated laboratory simulation buffers; corrected single/directions/full runs have no allocation warning. Exact allocation stacks were not traced. Unity exits0, settings restored byte-for-byte; no concurrent game. Source/completeness independently reviewed. Unity licensing token-update message retained; no shader/compiler errors.

Next: localize multi-owner composition; then native house. Parallel8.7: remaining3sources/fault cases;8.8: simultaneous current absence proof for construction/refund objects and durable terminal recovery across world rollback. Simply marking Cancelled conflicts with current cancellation-journal validation and is not sufficient. Public board cancellation, larger refunds, cascading destruction, content and host+remote remain unverified. No new mandatory owner manual series assigned.

Evidence root `artifacts/checkpoint-259/`: build/test logs, native/session verification, independent saved-profile parser, reviews; matched run logs/exit/settings, video and4332raw-frame hashes. Saves/assets/video excluded from Git. Full14sections/115IDs/16scoped checkmarks preserved; this document does not change Goal API.
