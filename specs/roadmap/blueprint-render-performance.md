<!-- docs-role: design record; reviewed 2026-10-17 -->
> Design/specification record: requirements below are not blanket implementation or acceptance claims. [Current state and queue](../../docs/CURRENT_STATUS.md).

# Blueprint editor rendering — checkpoint 0.19.60

## Shared pick geometry — 0.19.60

2026-10-06. Root-local vertices and feature edges are shared by exact persistent
Mesh + nested matrix, while each visual retains its own renderer/state. GPU reads
remain cached per persistent mesh. Transient baked geometry is never retained;
temporary previews can reuse entries but cannot grow the root-geometry cache.
Scene disposal releases both caches. Native/world prefabs are unchanged.

| Parts | Unique pick-array payload bytes .59 → .60 | Sync ms .59 → .60 |
| ---: | ---: | ---: |
| 128 | 12525360 → 107808 | 139.588 → 53.369 |
| 512 | 50071344 → 107808 | 549.886 → 223.499 |

Actual woodwall: 21 root geometry entries, 2 materials; renderer counts still
2688/10752. Array payload excludes object/native/GPU overhead, not total RAM.
Single-machine timings are not FPS or large-construction capacity proof.
Evidence: artifacts/performance-0.19.60/; baseline is .59 with the same extended
fixture. Nested rotated/nonuniform/mirrored children, unreadable GPU mesh, exact
pick/normal/bounds/feature-corner parity, preview transform/reset, delete/Undo,
transient exclusion, preview-cache churn and disposal pass.

Fresh 144-state Unity Workbench PASS 2026-10-06T09:15:01Z, gameHostTested=false.
The fixture now forces the real fixed-size GameView repaint before test steps;
all native raycasts stay ScreenSpaceOverlay, screenshot-only camera targets never
span a yielded frame. Real Screen dimensions are asserted, not substituted.
Release and targeted Geometry/Store/Localization/HostContract/WorldLayout/
EditorBridge/DeploymentChecks pass. Independent correctness and Ponytail review;
verify skill unavailable. Interactive budget 512, installed .56 and saves unchanged.
Free supports are approved for implementation; batching/world persistence/export
remain incomplete.

## Historical material-sharing checkpoint 0.19.59

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
