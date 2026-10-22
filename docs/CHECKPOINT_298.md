# 298 — QA startup fixed; native fog measurement incomplete

22 October 2026. Goal9.2–9.7 remains open. This fixes one Windows QA-channel
failure; installed BuildWorks QA267 and its ordinary fog are unchanged.

## Change and checks

Local GABS `SaveRuntimeState` retries its existing atomic rename on transient
Windows contention, at most41 calls/40 five-ms pauses. Each retry uses the same
fully written, synced, closed temp. Ownership, transition fencing, permissions,
final error and cleanup remain intact. No target deletion, copy or partial rewrite.
The speculative share-delete reader did not solve legacy replacement and was reverted.

The old implementation reproduces the held-reader failure. Three new tests cover
released/persistent reader contention, immediate non-transient failure and concurrent
complete snapshots. The entire process package and `go vet ./...` pass. `make` is
absent; the equivalent Makefile `go build` produced the separate candidate.
**Full `go test ./...` remains FAILED:** all23 failing test names also fail with
the previous implementation on Windows. Baseline additionally records an in-use
test executable cleanup failure. Independent correctness/Ponytail review permits
the scoped fix, not a claim of full-suite success.

Local GABS checkpoint: `7aee48b36e6aa1148288c553df54a956a0ea15b6`; no upstream push.
The checked local GABS remains in the existing executable path, with its original
backed up. Candidate SHA-256:
`D943904820FCC266A81D5C763E6B8EEE780EDB5C51B836D1E4D30B5B8D73042D`.

## Real host result

GABS connected in6.687s; existing-world entry took17.109s. Verified QA profile,
identities, world UID1137745799, isolated saves/cloud=false, nonbatch background DX11.

Scenario297 FAILED at step19:18/85 successful. Windows supplied3440×1351 rather
than the requested3440×1440, so the exact-size ObserveScreen guard rejected it.
The borrowed route, shader, camera and weather were restored.

A separate298 scenario preserves85 steps and297/289/289/297 order with four strict
3440×1351 windows and matching validator. Astra approved this as a distinct
same-size comparison; changed aspect means no1440-performance claim or direct
comparison to earlier FPS.

After restoring the original shader, the log recorded
`Fog input queue is full; observation rejected, accepted history retained.`
The second scenario FAILED at step18:17/85 successful, AttachExisting rejected the
project even though Building.Ready remained true. Ready alone is insufficient to
prove health. No further measurement/retry, queue reset or capacity increase occurred.
**Zero contact cadence windows completed.**

## Cleanup and limits

Both shader observers confirmed shader/camera restoration. Weather restored;
original pinned noon retained. Normal logout/main_menu/Application.Quit completed,
owned PID39100 exited, all three original bridge DLLs restored/hash-checked.
97project hashes and4141ordinary-save metadata entries are unchanged. Only expected
QA-save/control files changed on normal exit. Four setup inventory snapshots match
exactly:23entries/67Wood; this is not a post-reload inventory test. No construction,
grants, resets, terrain or ordinary-world actions.

Local evidence: `artifacts/checkpoint-298/verification.json`, `qa-audit.json`, red/green
regression logs, process/vet/full-suite and baseline logs, `gabs-source-checkpoint.patch`,
the297failed-setup and298failed-ready reports. Host/MCP logs:
`../ValheimQA/artifacts/20261022-095036/`.

Next: resolve the queue/preparation cause without discarding accepted history or
using Ready to hide errors, then measure without an extended original-shader idle.
[297 local GPU checks](CHECKPOINT_297.md) remain scoped evidence. Native speed,
approved v11 appearance/house, owner acceptance, paid UI/physical E and remote play
are not established.
