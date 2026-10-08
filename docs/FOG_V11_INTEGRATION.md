# v11 integration points in BuildWorks

Status2026-10-08: owner-approved visual baseline; no game integration implemented.
Source handoff: artifacts/fog-ghost-study/wall-11/HANDOFF_BUILDWORKS_RU.md.
Frozen archive SHA/CRC105 entries and all57 live manifest files were verified.
MemPalace retrieved the earlier texture/UV review; SocratiCode retrieved current
Session preview/pause paths. Neither historical recall nor lab PASS proves game support.

## Existing boundaries

- PlacementGhostPreviewView.CreateVisualClone supplies renderer-only hierarchy and
  source textures, without collider/Piece/ZNetView. Create owned v11 material copies;
  keep shared prefab assets immutable and preserve pinned scale/BakeMesh semantics.
- PlacementGhostPreviewView.Show maps plan indices to visuals. A persistent project
  needs stable part IDs and authoritative unfinished-state masks; hiddenIndex is
  not durable construction state.
- BlueprintBatchedPreview.Prepare/Draw must retain heavy-scene batching. Filter
  unfinished parts before batching; a shared MaterialPropertyBlock does not express
  independent simulation ownership for every part.
- PrecisionPlacementSession.CompleteNativePlacement/PauseAutomaticPlacement track
  a transient queue. Finish/Clear destroys its preview; placementPlanIndex cannot
  replace a server-owned persistent project.
- FogGhostLab.OnPreCull/OnRenderImage connects surface deformation, dense contact
  meshes and the volume camera pass. Porting only a shader/cginc is insufficient;
  camera/depth/postprocessing compatibility and owned cleanup must be verified.

## Constraints and sequence

1. Installed UnityPlayer.dll identifies host6000.0.75f1, while the lab is6000.0.61f1
   DX11. Shader/compute support and asset loading need host proof; current csproj
   does not deliver these shader/compute assets.
2. v11 covers one wall and one capsule. Trail24 is one actor's history. SourceBin
   uses YZ32x32 cells without part ownership; overlapping walls/floors need correct
   ownership. The older whole-house scene07 is not a v11 whole-house test.
3. Ten96x80x80 ARGBHalf fields use46.875MiB before source textures/meshes/buffers.
   Do not allocate this per part. Measure shared/local-domain budgets, catch-up while
   in Evaluate, blocking GPU readback in reset/disable, time jumps and unload cleanup.
4. Durable project/part IDs, stake interactions, server-authorized operations and
   crash-safe escrow/refund are not implemented. Fog observes future confirmed
   project state; it does not own costs, permissions or persistence.

Start with one renderer-only real-prefab clone and owned v11 materials, graphics
capability checks and frozen-reference comparison. Then verify multiple rotated/
scaled parts, density ownership, player-body input, completion/cancel/unload and
house budgets. Attach a persistent project view only to a reliable project state.
This is an integration plan, not an implemented feature.

Keep the lab archive/extracted vanilla assets local. Resolve runtime models/textures
from available host prefabs. Goal addition: GOAL_AMENDMENT_FOG_V11_RU.md.
