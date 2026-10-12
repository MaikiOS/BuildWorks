## 2026-10-12 — .160 native: refusal measured, visible refund verified

One TEST24 attempt built floor/wall2/4, then chest NoRayHits. A real floor hit
within range had renderer gap5.34cm versus BuildWorks tolerance2.38cm.
Native PieceRayTest has no1% renderer rule; shared contact correction remains.
Second .159 cancellation and new .160 cancellation: Wood42+50, two swords,
no reprovisioning. Full build, item properties/reload/network remain unproven.
Ostrix exited to work in Nuke: source/CPU work only, no Valheim/Unity/GPU launches.
Next entry follows correction/checks; do not repeat this diagnostic attempt.
[Native .160 evidence](QA_NATIVE_160_2026-10-12.md).
History follows; waiting for the first .160 observation is no longer current.

# .160 — refused contact diagnostics, QA deployed

QA **0.19.160.0** replaced .159 on12 October only in `TerrainRamp-1.0-Test`.
Source: `db59dd6b3fc3220c0b0152218ba385ee040de52d`. This observes `NoRayHits`;
it does not fix the chest or prove construction acceptance.

The failure-only QA hook reports ghost pose/scale/bounds/tolerance/mask/range
and nearby colliders with six bounded centre probes. It never supplies an
accepted contact or changes pose, synchronization, costs or permissions.
Normal DLL excludes both hook and type. Six diagnostic probes do not replace
the54 production probes; `truncated=true` means the32-hit buffer may be truncated,
including the exactly32 case.

ContactOnly runs separately from full UI capture. Missing material-state source
copy and inert Workbench host compile fixtures were repaired; these fixtures
are not native Valheim simulation or network/persistence proof.

Geometry, normal/QA0/0, Host Steam25185596, loc989, compiled isolation and Unity
ContactOnly passed. Actual Physics measured7cm without changing position/refusal;
floor/ceiling/wall/range/rigidbody/scale checks passed. On12 October, canonical/
mirror/Unity source hashes matched; normal/QA/Host/isolation/loc were rechecked.
Correctness qa160_final_review: PASSED; Ponytail: Ship.

Guarded dry run/deploy and both installed SHA checks passed. All156 config/import/
save files retained their paths, sizes, hashes and modification timestamps;
Default clean, game closed during installation.

BuildWorks.dll SHA256:
`E004DF776322A75D68521583E9C392AF90AB9AA8C59BD5EBC4A27EFFBBBEB8A1`

BuildWorks.Geometry.dll SHA256:
`EC2315A128CA4AF1B19DD5BE69A93B69F18BB3F558F018C6C36A17D0E22C0261`

Evidence: `artifacts/checkpoint-0.19.160`, including earlier compile failures;
current checks/review/deploy under `resume-2026-10-12`;
Unity receipt/log/source proof under `artifacts/placement-contact`.
Early ContactOnly runs overwrote shared full UI log/source proof. Paths are now
separate; old492 screenshots/status are history, not a fresh .160 full UI pass
or current source matching proof.

Next: Ostrix launches **Thunderstore → TerrainRamp-1.0-Test → Modded**, enters
the same isolated QA world and reports readiness. Fresh nonce/state/capture first;
verify second .159 cancellation balance (Wood42+50, two swords), collect/refund
investigation before any new spending. Never reprovision or replay old requests.
Use the already imported **TEST24**, source file
`BepInEx/config/BuildWorks/test-fixtures/24-contents-empty.buildworks.json`.
Disable both contents-placement options for one empty-construction contact test.
Record actual collider/bounds/gap/mask/range on failure2/4, then cancel normally
and check refund. On focus loss stop without replay. Catalogue review prepares
a ghost; world-click review confirmation continues the pending placement.

See [Russian operational steps](CONTACT_DIAGNOSTICS_160_2026-10-12_RU.md).
Do not widen tolerance or move fixtures from visual metadata alone: the6.95cm
visual gap is not native collider evidence. Native .160/full contents/reload/
network remain unproven; populated storage transfer disabled, full goal active.
