<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .152 — submission normalization in the existing worker

Code b2fae84. Source normal/QA .152 only; installed QA remains .146. No Valheim,
Unity, input, deployment or save changes while Ostrix works. Full goal open.

TryImportReviewed retains stale-review and explicit missing-choice validation.
It takes fresh distinct-prefab scale/material/metadata copies, world level and
pure hash on the caller thread. A callback or world retaining its identity does
not justify reusing its old values. The existing importTask performs PrepareForHost,
explicit missing omission and metadata application before file operations.
Identity-return preparation gets a private ImportCopy. Metadata is copied at the
snapshot boundary and per part; source fallback semantics remain unchanged.

Missing-choice validation skips RetainedContents decoding while preserving every
decision field/context/count/empty-document refusal. Normal review still decodes
contents. The bool result means work started; normalization failures now arrive
through ImportError. The view keeps the actual error, clears the finished review
and requires Back plus a fresh review before another confirmation. This also fixes
the following Tick masking a worker failure with a stale-review message.

Exact source comparison, path/size/count/version/scale guards, shared library
budget, future order, ZIP validation and atomic File.Move remain. Format, costs,
item state, rights and networking are unchanged.

Tests: held submission worker with unique caller-only fresh snapshots; mutable
metadata isolation; raw/identity-normalized source preservation; fresh next metadata;
failed normalization without ImportDirectory; lightweight missing equivalence.
Actual Tick CPU harness red/green covers persistent error/Back/no retry, success,
stale and two stages. Full Store+353 corpus passes (351 format-compatible/2 blocked),
normalQA0/0, Host Steam25185596/localization988/isolation/EditorBridge/scoped format,
independent correctness and Ponytail Ship. These are not host placement proof.

Two unit-scale/no-metadata file-only probes on the same4096-part148645-byte file:
submit .151=2.625/0.116ms, .152=3.278/0.367ms; worker wait .151=539.132/418.862ms,
.152=461.514/290.282ms. This does not demonstrate submit or total import speed-up:
the old normalized/no-metadata fixture already had little preparation. No cache
reset, native UI, FPS or memory measurement. Frozen baseline source/DLL, input SHA,
logs and final receipt: artifacts/checkpoint-0.19.152. NuGet Access denied was retried
with permitted CPU access. No verify helper; Geometry unchanged, .150 suite retained.

Still open: host snapshots/RefreshMissing, real prefab/UI/Back/Close execution,
native latency/FPS/memory, placement/resources/cancel/reload/network. Workbench not
compiled/run. Closing an already confirmed import is not transaction cancellation;
review read-only jobs still finish within budgets, without CancellationToken.
Deferred merged Russian steps: [QA .151–.152](QA_0.19.151_RU.md).
