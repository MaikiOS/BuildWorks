# Gizmo anchor renderer pool — source0.19.119

Source only; installed isolated QA0.19.117 pair remains unchanged. No deployment,
Valheim launch, world or save changes.

The before-fix Unity regression reproduced `Unbounded anchor renderer allocation:
600` with600 source points on the previous417b0c6 code. The production gizmo now
reuses at most512 main anchor renderer slots. Larger sets choose eligible points
nearest the cursor while retaining selected, pinned and the full hit-test winner.
Sets up to512 keep identity slot order. Coordinates/order are never truncated or
changed. Moving the cursor makes other source points enter the pool. Show-all on
a dense set displays the nearest512 plus those priorities, rather than drawing
every saved point simultaneously.

Slot index is separate from source point index for picking, pinning, types,
colors, radii and feedback. Full-set hit testing precedes visual shortlisting,
preserving reverse source equality, smaller coincident helper priority and the
within0.5px near-depth choice. Shrinking/empty/hidden/mode changes clear old hits.

Verified: normal/QA0warnings0errors; Store PASS; Unity6000.0.61f1 final receipt
2026-10-09T18:30:54.7021987Z with492 existing screenshots at four resolutions and
three scales; new600/17125-point renderer bounds, original16000/17000 pick/pin,
cursor remapping, coincident helper/native/source order, near-depth winner,
unchanged source data, large-small-empty/native-only/plane/behind/Hide, and pooled
world-occlusion600→300→0 cleanup. Inspected the separately isolated600-point image:
existing clasp artwork and pinned highlights retained. Not a game-house visual proof.

HostContractSteam25185596, localization930keys/resources, compiled QA isolation,
correctness and Ponytail review passed. The host visibility guard now checks
Show→PrepareAnchorHandles and shared visibility/source mapping/full hit testing;
it was not removed. verify unavailable; direct checks used. Initial sandbox Unity
exit-536870911 with missing LicenseClient is excluded from test evidence; the
before-fix repro and final runs used authorized local license access. Store tests
also required an authorized unsandboxed retry after Unity DLL access refusal.

Open: projection/visibility still scan eligible source points and the large
candidate list is sorted. This bounds renderer allocation, not all CPU/GPU work
or Valheim FPS. Storage512 has not been raised; all12 foreign parse refusals
remain. Native21/portable10 schemas unchanged. Next: bounded spatial queries,
exact accelerated nearest-part assignment, coordinated storage/parser/editor/
portable guards and large roundtrips. Keep per-piece network codec512 and
preflight before spending. Deployment/game/reload/network remain separate.
No new owner test is requested for this source-only step; QA.117 scenario stays current.

Evidence: local artifacts/checkpoint-0.19.119 baseline, before/final Unity logs,
verification-status.json, large-gizmo-check.txt, large-gizmo-pool.png,
source-sha256.txt and build/Store/Host/localization/QA logs. No third-party assets
added to Git. See Russian companion for complete limits.
