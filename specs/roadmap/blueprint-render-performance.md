# Blueprint editor rendering — checkpoint 0.19.59

**English** | [Русский](blueprint-render-performance_RU.md)

2026-10-06. Source only, not deployed. This step reduces duplicated preview
materials; it does not deliver the requested heavy-construction workflow yet.

## Change and measured evidence

Ordinary document visuals share scene-owned copies keyed by their exact source
Material. Hover/selection remain renderer-local property blocks. Temporary tinted
contour/duplicate/placement previews keep their own materials. Deleting one part
cannot destroy a shared material; scene disposal releases it. Prefab/world
materials and synchronous budget 512 are unchanged.

Isolated Unity 6000.0.61f1, actual imported `woodwall`, 21 renderers per wall;
128/512-part grid. Setup and one-piece warmup excluded from sync timing. Single
machine/sample, not a statistical speed guarantee. Live managed delta is neither
native/GPU memory nor peak/allocation measurement.

| Parts | Material copies before → after | Sync ms before → after | Live managed bytes before → after |
| ---: | ---: | ---: | ---: |
| 128 | 2688 → 2 | 233.905 → 124.735 | 21336064 → 21483520 |
| 512 | 10752 → 2 | 906.255 → 519.532 | 88006656 → 84070400 |

Renderer count is unchanged: 2688/10752. Optimized mean resync (10 passes):
0.397/2.034 ms; mean exact mesh pick (20 passes): 0.098/0.403 ms. The small memory
change confirms materials alone do not remove hierarchy/geometry duplication.

Runnable coverage: `scripts/Capture-UiWorkbench.ps1` includes
`BlueprintScenePerformanceAcceptance`. It checks actual part/render/material
counts, mesh picking, isolated hover and preview tint, delete/Undo reuse, complete
source-material invariance and scene/material destruction. Imported MaterialToggle
drawers are initialized explicitly before the full serialized baseline; no later
keyword/property differences are ignored. Initial invariant failure differed only
by Unity adding `_ADDRAIN_ON`; the diagnostic pair is retained locally. A sandboxed
Capture also failed Licensing IPC; the same script passed with permitted access.

Local measurements/source hashes: `artifacts/performance-0.19.59/` (baseline,
optimized, verification status). Fresh 144-state Workbench PASS completed
2026-10-05T21:49:45Z, `gameHostTested=false`. Release 0 warnings/errors;
Geometry, Store (109/353 actual archive entries), Localization, EditorBridge,
WorldLayout, DeploymentChecks and current HostContract PASS. Formatting exits 0
with workspace-load warnings. No `verify` skill is installed. Independent
correctness and Ponytail review cover the material-cache boundary.

## Next work, not delivered

Read-only architecture research recommends reusable prefab-local geometry before
batch rendering: one immutable prototype per exact resolved prefab, preserving
LOD0, nested TRS, enabled/active state and baked skinned geometry. First prove
pick/normal/bounds/snap parity with rotated/nonuniform children; then replace
per-part renderer hierarchies with lightweight records and supported batching.
Do not combine the whole document or change native world-piece placement.

Heavy opening must be time-budgeted, cancellable and staged: old projection stays
valid until atomic success; failed/cancelled edits restore the document. Thumbnail
rendering must share the same projection without allocating picking data. Shader,
device/fallback and large outliner performance still need measurements. Do not
raise 512 just because storage can hold 50000.

Free support-frame authoring/world persistence and real export actions remain
separate pending work. World placement, reload, remote client and actual game
performance are untested. Installed .56 and all worlds/saves remain unchanged.
