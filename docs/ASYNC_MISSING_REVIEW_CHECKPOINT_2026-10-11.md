<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .153 — initial missing-part review in the existing worker

Normal/QA sources .153; installed QA remains .146. No Valheim, Unity, native
input, deployment or save changes. The full goal remains active.

PrepareImportReview derives missing availability from its caller-side unique
prefab scale snapshot. The existing worker prepares MissingPartsReview alongside
normalization and contents: counts, provenance, empty-after-omission documents,
retained contents. Unchanged documents reuse their contents summary; filtered
copies are inspected separately. Repeated normalized document references remain
valid and each document contributes to totals.

Tick publishes the prepared snapshot without another host RefreshMissing.
Unknown/unloaded hosts do not invent missing IDs. No live host callbacks run in
the worker. Availability reflects preparation time; fresh omission validation
still rejects changed context/IDs or an empty result before publication. Source
documents/files, budgets, atomic writes, permissions and resource rules remain.

Full Store +353 BiomeBlueprints passed:351 format-compatible/2 explicitly blocked,
not prefab or placement acceptance. Tests cover unknown/unloaded host, direct vs
worker summaries, source preservation, repeated references, all-missing ZIP
members, stale availability and held-worker snapshots. Actual Tick CPU red/green
proves the old redundant RefreshMissing call and fixed snapshot publication;
existing success/error/Back/stale/two-stage cases pass. Normal/QA0/0,
Host25185596, localization988, compiled QA isolation, EditorBridge and scoped
format checks pass. Review conclusions, commits and hashes are recorded in
artifacts/checkpoint-0.19.153/verification.json; verify helper is unavailable.

No FPS/memory/total latency improvement is claimed. Host snapshots, other
RefreshMissing paths and some catalog work remain on the host thread. Back/Close
discard read-only results without CancellationToken. Workbench/native game tests
and placement/resources/cancel/reload/network remain open; unchanged Geometry
retains .150 evidence. Future installed QA >=.153 uses
[the merged import-window procedure](QA_0.19.151_RU.md); no user action now.
