# 257 — refund-source cleanup journal and v11 comparison preparation

2026-10-20. Source/CPU checkpoint only. Existing QA .247 remains installed with [256's](CHECKPOINT_256.md) native evidence. No new deployment. [Detailed report](CHECKPOINT_257_RU.md).

Added an optional per-credit cleanup record. Old absent fields retain canonical encoding. Cleanup intent requires an already credited row, the payer's exact permanent saved receipt, valid world generation and no other uncertain credit/cleanup operation. Confirmation requires complete mapped absence for the exact single source marker/world at N+1 and the same profile hash/receipt. Exact replays do not write; changed proofs refuse. No inventory mutation or terminal project cancellation is introduced.

Full StoreTests passed, including117 payout assertions (previously76): omission compatibility, intent exclusion, semantically corrupt but correctly checksummed data, wrong proofs, exact replay and unchanged files on rejection. Final normal/QA builds in isolated output folders have zero errors/warnings. Independent correctness and lean reviews passed for this journal slice. `verify` skill is absent and not claimed.

Native cleanup executor is not connected. Four old sources remain sealed;67Wood is the last native balance from256. Cleanup/crash/rollback/UI/terminal cancellation and networking remain unverified;8.7/8.8 stay open.

Prepared a local, GPU-unverified longwall4 comparison harness with shared actor/time/camera and three streams: frozen sequential, current dense sequential, current published atlas. Sequential overlap differs mathematically from joint integration; this diagnostic extension is not a newly approved four-owner reference. The source-only checkpoint excludes this unfinished GPU branch.

Preflight rejected stale historical Gamma225 shader hashes. The existing Gamma-reference builder produced a current bundle and passed shader/load/kernel/arena/reload checks without changing the mod's embedded asset. The frozen simulation still matches v11. Before launching the new probe, safety checks found Ostrix playing Default-Compat-Test (PID46876), so no probe/game/input action followed. No comparison recording or visual acceptance exists yet. Future fixed-time footage is not native FPS evidence.

Private evidence: `artifacts/checkpoint-257/` final Store/build logs, read-only/lean reviews and Gamma build log; shader manifest at `artifacts/fog-runtime-assets-gamma/verification.json`. Earlier file-access failure and a test's ambiguous Removal/RemovalHash key check are retained with the corrected passing run. Next: connect controlled native source removal and execute the matched visual probe when the owner session has ended. Full goal scope remains unchanged.
