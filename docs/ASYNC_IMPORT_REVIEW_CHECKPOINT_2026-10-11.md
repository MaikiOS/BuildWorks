# .151 — background import review preparation

Source normal/QA .151 only; installed QA remains .146. No Valheim, Unity, input,
deployment or save changes while Ostrix works. The full goal remains open.

After file inspection, the host previously ran all PrepareForHost copies and
contents summaries synchronously. It now snapshots scale per distinct prefab,
material-seed support, world identity and level. Task.Run receives those values
and a pure name hash. Live Unity callbacks run only on the caller thread.
Source bytes and format remain unchanged.

The view waits for both stages. Back/Close discard pending results. A changed
world, level or resolver generation rejects stale publication/confirmation;
Store checks again before writing. The view guard only applies to review:
own-import catalogue generation changes must reach the result, and an old review
must not freeze the browser after Back. Those two review blockers were fixed.
File/size/count/version/scale/path/shared-budget checks, fresh file validation
and atomic publication remain. Costs, resources, permissions and network state
were not changed.

Full Store plus353 BiomeBlueprints sources pass:351 format-compatible/2 explicitly
blocked, with unchanged archive SHA. This is not host-prefab or placement proof.
The held-worker regression proves caller-only snapshots, one lookup per repeated
prefab, unchanged source and stale context/level/resolver refusal without writes.
The new CPU harness extracts actual Tick and covers own import to ShowResult once,
browser after Back, stale confirmation/result, two stages and worker failure.
Old-guard red and fixed-source green logs are retained.

Normal/QA builds:0 warnings/0 errors. Host Steam25185596, localization988,
compiled QA isolation, EditorBridge and scoped Store/View formatting pass.
Independent correctness review: fixed then Ship; final Ponytail/commits and hashes
are recorded in `artifacts/checkpoint-0.19.151/verification.json`. Sandbox file/pipe
denials were retried with permitted CPU access. No verify helper is installed.
Geometry unchanged; its full .150 suite was not repeated.

This step does not prove total import speed. Snapshots, RefreshMissing and fresh
confirmation PrepareForHost/CaptureMetadata still use the host thread. Abandoned
read-only jobs finish within existing file budgets; no CancellationToken was
added. Workbench wait/Back tests were updated but not compiled or run in Unity.
Native UI latency, FPS/memory, placement/resources, reload and network remain open.
Deferred Russian steps: [QA .151](QA_0.19.151_RU.md). Do not repeat accepted checks.
