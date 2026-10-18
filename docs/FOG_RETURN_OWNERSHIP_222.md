# 222 — missing source coordinate in fog return

2026-10-18 · [Русский](FOG_RETURN_OWNERSHIP_222_RU.md) · [Full checklist](LIVE_CHECKLIST_RU.md)

Diagnostic checkpoint, **not a .222 QA binary or completed fix**. Installed0.19.221.0 remains SHA `2A146A973FB18C3D244A7C963240CCA458BB0A03C0AAD84A2B21F53BDC770577`. Runtime C#/compute/bundle, approved archive, costs/rights/saves unchanged. No Valheim launch; isolated Unity DX11 checks only.

The approved wall is normal to X, along Z; native four-section wall is normal to Z, along X. Runtime SourceBin uses RestUV(p).yz, discarding source X. Independent part owners do not separate positions inside a part; camera cannot affect this indexing.

Actual compiled native .221 solver bundle SHA `A24A7CED68795563AAE47A40113BB55C6944E4DA2D0FDC7BF32A1B023F5CF99E` was tested with one displaced sample, initial damage.5, emit0, distant actor. AccumulateLedger accounts1024 away-mass in bin524. AdvanceVelocity retains source A at.5 **and an unrelated previously damaged position B1.8m along X at.5**. A separate Z control recovers to.458251953. This proves wrong retention of old damage, not creation of a new hole without contact or the sole cause of every visual defect.

A generated isolated16×32×32 candidate changes only source-bin indexing/3D neighbours; threshold768 and other kernels unchanged. It retains A at.5 while B and Z control recover to.458251953; one bin8389 still accounts1024. `scripts/Prepare-FogOwnershipCandidate.py` verifies source hash against the compiled bundle manifest before generating the test fixture used by FogReturnOwnershipCheck.RunCandidate. Generated shader/game assets are not shipped. Runtime source/bundle untouched. First run failed `Ownership probe readback incomplete` because it read one3D layer; full bounded layer collection fixed, first failure preserved. Both checks exit0; independent review, no installed verify skill.

The candidate does **not** prove complete transport/recovery, mixed origins, distribution between bins, repeated contact, yaw0/90, tilted/scaled/nonplanar parts, approved look, native rendering/FPS, actual pool budget enforcement or projects/board.

Next runtime change needs immutable pool/generation LedgerLayout: exact LegacyYZ oracle and Spatial3D candidate. No live switching, remapping old peak/away or heuristic plane selection. Wire solver/staged validation, owner+stage ledgers, arena copy/commit dispatch and complete budget accounting. Sparse map/payload/velocity format unchanged; Ledger[2] remains one uint.

For28owners/7resident/8192blocks, all29 owner+stage pairs grow by `29×(16384−1024)×8=3,563,520B`:94,478,116→98,041,636B of98,304,000, leaving262,364B. **Calculated, not measured production allocation or proven whole-house capacity**; larger house storage reserve remains unresolved, four-wall32768 is a separate fixture. Never disable safety limits.3D bins still approximate averaged source positions; cell dimensions.30×.140625×.10625m are anisotropic, and threshold/neighbours may alter return duration after mass is split.

Required next checks: immutable layout/bounds/budget rejection; legacy exact; separated X sources; equivalent yaw0/90 and nonplanar part; full contact/hold/exit/reentry field+image series; matched native close-camera long-wall capture; house. Durable project/board follows verified v11 and remains a main goal.

Colour/grayscale five-stage lab/native sheets retain original image hashes, lab camera and native quaternion/FOV55. **Visual parity BLOCKED:** different cameras, wall/body scale, lighting, paths and simulation age. Diagnostic side by side only, no invented tolerances/global pixel score or animation acceptance.

Private evidence: BuildWorks/artifacts/fog-return-ownership-222 (reproduction/spatial-candidate/candidate-source JSON, sheets/manifest, GPU logs/first failure). Source-only diagnostic C#/generator are public; no frames/binaries. Native .22130/30 remains valid for its front-pass numeric scope, not arbitrary orientation/surfaces. All14 blocks retained; goal active.
