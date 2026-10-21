# Checkpoint267 — a small render improvement, house queue still falls behind

2026-10-21. Installed QA **.250 unchanged**. Shader/tool edits remain a local candidate; no new DLL deployment. This does not accept v11 appearance or board-based completion.

Published SM5 candidate arrays became ordered uint masks. AABB, ascending owner additions,24light samples and a fresh traversal per sample are retained. Legacy SM4 and the baseline retain arrays. GPU passed453exact page comparisons,15negative cases,133mask controls and14frozen render comparisons with zero difference. This is limited coverage, not full28-owner house pixel parity. Both indexable arrays disappeared from compiled DXBC; baseline hash is unchanged. Declarations are not hardware register measurements.

Frozen28-owner measurement: median8lit frames770.431ms versus816.901ms,5.69%less elapsed; unlit263.909versus281.868ms. This includes CPU/GPU/drain/readback, not pure GPU timing or native FPS. The gain did not justify another unchanged full52-second trial.

Instead, installed250 ran a short stationary service diagnostic:10snapshots/~6.593s,82actual renders, production fog-frame growth. Aggregate queue56→508,maximum per-part queue1→18,minimum confirmed tick19→151 (~20.75ticks/observed second). Polling overhead is included and RPC snapshots are not atomic. No overflow in this short window; backlog growth is established. No walking pass or visual acceptance.

All93old project hashes unchanged; only owned unpaid plan/lock added, checksum/world/ID/rev2 verified. Inventory unchanged, no grants or paid building.4141ordinary save files preserved by membership/size/mtime only, not content hashes. Config differs only in header251→250. Normal quit and three bridge DLLs restored.

Recorded test defects: SetInt lost a low mask bit; SetInteger fixed the exact same strict test. The inherited probe overwrote the old checkpoint265 JSON; its original bytes/timestamp were lost. New receipt archived, current output path fixed, historical values reconstructed from the previously verified contract with an explicit provenance note; not presented as an original receipt. Old logs/source snapshots/guard proof remain unchanged. Initial audit stopped at config drift, then accepted only the exact verified header substitution.

Evidence: `artifacts/checkpoint-267/measurement.json`, audit/compiler/candidate-source; lighting run `LightingMeasurement-20261021-071814-a66d6fba`; ValheimQA `artifacts/20261021-073254`. CPU receipt validator passed1784negative cases. The captured pre-output-path C# bytes were recovered by an inverse byte substitution and matched the original source hash.

Next hypothesis: one grouped receipt readback after all submitted owners, retaining token-specific confirmation, history, queue bounds, quality and teardown safety. It is not implemented or proven to fix the deficit. Full house/v11, paid cancellation and networking remain open.
