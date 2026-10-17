<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .109 — duplicate skinned preview scale

QA .109 installed; hashes match,48 QA files and library unchanged. Checkpoint:3683382. No game execution.

The shared visual clone now uses BakeMesh(baked, true) with its copied transform
hierarchy. No source prefab, blueprint bytes or whole-building scale is changed.
Thumbnail cache contents-v3 bypasses stale v2 images; old cache files may remain.

Measured installed piece_Lavalantern, bundle c4210710: root1×1×1 and Sphere.004
localScale20.7203922. The old clone applied the renderer scale twice, producing
about17m height. The fixed clone is about0.82m tall and matches the independent
static high mesh bounds center/size within0.05m. This is bounds, not vertex parity.

Passed normal/QA builds0/0, GeometryTests, StoreTests, HostContract, localization856, full QA
isolation, whitespace, Unity Workbench252 screenshots and actual-asset regression.
Independent correctness/ponytail review found no blockers. Deployment is recorded
separately in artifacts/checkpoint-0.19.109/verification.json.

No Valheim execution. Native placement/reload/network remain open. The distinct
canonical/Hammer prefab-scale basis problem is not resolved by this change.
See QA_0.19.109_RU.md and WORK_STATUS_RU.md.
