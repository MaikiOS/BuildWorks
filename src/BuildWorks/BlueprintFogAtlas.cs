using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    // Derived render data only: the independent simulations remain the owners of all matter.
    internal sealed class BlueprintFogAtlas : IDisposable
    {
        internal const int MaximumOwners = 32;
        internal const long MaximumBytes = 128L * 1024 * 1024;
        private Material material;
        private ComputeShader pack;
        private readonly int kernel;
        private RenderTexture shape, smoke, surface;
        private Vector3Int size;
        private readonly List<Vector3Int> blocks = new List<Vector3Int>(MaximumOwners);
        private readonly List<Vector3Int> layoutBlocks = new List<Vector3Int>(MaximumOwners);
        private int layoutLimit;
        internal long PackDispatches { get; private set; }
        private readonly BlueprintFogVisual[] packedOwners = new BlueprintFogVisual[MaximumOwners];
        private readonly Texture[] packedSmoke = new Texture[MaximumOwners], packedSurface = new Texture[MaximumOwners];
        private readonly int[] packedTicks = new int[MaximumOwners];
        private readonly HashSet<BlueprintFogVisual> unique = new HashSet<BlueprintFogVisual>();
        private readonly BlueprintFogChannels publishedPool;
        private ComputeBuffer bindings;
        private readonly BlueprintFogVisual[] preparedOwners = new BlueprintFogVisual[MaximumOwners];
        private readonly BlueprintFogChannels.SnapshotChannel[] preparedSnapshots = new BlueprintFogChannels.SnapshotChannel[MaximumOwners];
        private int preparedCount;
        private readonly uint[] bindingWords = new uint[MaximumOwners * 5];
        private readonly Vector4[] dynamicOffsets = new Vector4[MaximumOwners];
        private readonly HashSet<BlueprintFogChannels.SnapshotChannel> uniqueSnapshots = new HashSet<BlueprintFogChannels.SnapshotChannel>();
        private readonly Vector3Int[] offsets = new Vector3Int[MaximumOwners];
        private readonly Vector4[] origins = new Vector4[MaximumOwners], restMin = new Vector4[MaximumOwners], restMax = new Vector4[MaximumOwners],
            fieldMin = new Vector4[MaximumOwners], fieldMax = new Vector4[MaximumOwners], actors = new Vector4[MaximumOwners],
            shapeSize = new Vector4[MaximumOwners], fieldSize = new Vector4[MaximumOwners], atlasOffset = new Vector4[MaximumOwners],
            noiseOffset = new Vector4[MaximumOwners];
        internal long Bytes => shape ? (long)size.x * size.y * size.z * (publishedPool == null ? 18 : 2) : 0;

        internal BlueprintFogAtlas(BlueprintFogAssets assets)
        {
            if (assets == null || !assets.Volume || !assets.Solver) throw new ArgumentException("Fog atlas assets missing.");
            try
            {
                material = new Material(assets.Volume) { hideFlags = HideFlags.HideAndDontSave };
                material.DisableKeyword("BUILDWORKS_FOG_TWO_SOURCES"); material.EnableKeyword("BUILDWORKS_FOG_ATLAS");
                material.EnableKeyword("BUILDWORKS_FOG_DORMANT_FAST");
                pack = UnityEngine.Object.Instantiate(assets.Solver); pack.hideFlags = HideFlags.HideAndDontSave;
                kernel = pack.FindKernel("PackVolume");
            }
            catch { Dispose(); throw; }
        }

        internal BlueprintFogAtlas(BlueprintFogAssets assets, BlueprintFogChannels snapshots) : this(assets)
        {
            try
            {
                if (snapshots == null || snapshots.SnapshotArena == null) throw new ArgumentException("Published atlas requires a snapshot pool.");
                publishedPool = snapshots;
                UnityEngine.Object.DestroyImmediate(pack); pack = UnityEngine.Object.Instantiate(assets.Arena);
                pack.hideFlags = HideFlags.HideAndDontSave; kernel = pack.FindKernel("PackStaticSource");
                bindings = new ComputeBuffer(MaximumOwners * 5, 4);
                material.EnableKeyword("BUILDWORKS_FOG_PUBLISHED");
            }
            catch { Dispose(); throw; }
        }

        // Call before geometry is drawn, not just from the later OnRenderImage volume pass.
        internal void PreparePublished(IReadOnlyList<BlueprintFogVisual> owners, IReadOnlyList<BlueprintFogChannels.SnapshotChannel> snapshots)
        {
            if (!material) throw new ObjectDisposedException(nameof(BlueprintFogAtlas));
            if (publishedPool == null || owners == null || snapshots == null || owners.Count < 1 || owners.Count > MaximumOwners || snapshots.Count != owners.Count)
                throw new ArgumentException("Published fog binding count invalid.");
            unique.Clear(); uniqueSnapshots.Clear();
            for (int i = 0; i < owners.Count; ++i)
            {
                if (owners[i] == null || owners[i].Source == null || !unique.Add(owners[i]) ||
                    (snapshots[i] != null && !uniqueSnapshots.Add(snapshots[i]))) throw new ArgumentException("Published fog owners must be distinct and live.");
                publishedPool.WriteSnapshotBinding(snapshots[i], bindingWords, i * 5);
                dynamicOffsets[i] = new Vector4(0, 0, (snapshots[i]?.Slot ?? 0) * 82, 0);
            }
            for (int i = 0; i < preparedCount; ++i) if (!unique.Contains(preparedOwners[i])) preparedOwners[i].UnbindPublished();
            bindings.SetData(bindingWords);
            for (int i = 0; i < owners.Count; ++i)
            {
                owners[i].BindPublished(publishedPool, bindings, i, dynamicOffsets[i]);
                preparedOwners[i] = owners[i]; preparedSnapshots[i] = snapshots[i];
            }
            preparedCount = owners.Count;
        }

        internal static Vector3Int Layout(IReadOnlyList<Vector3Int> blocks, int limit, Vector3Int[] offsets)
            => Layout(blocks, limit, offsets, 18, MaximumBytes);

        private static Vector3Int Layout(IReadOnlyList<Vector3Int> blocks, int limit, Vector3Int[] offsets, int bytesPerVoxel, long budget)
        {
            if (blocks == null || blocks.Count < 1 || blocks.Count > MaximumOwners || offsets == null || offsets.Length < blocks.Count || limit < 1)
                throw new ArgumentException("Fog atlas layout input invalid.");
            Vector3Int minimum = Vector3Int.zero; long occupied = 0;
            foreach (Vector3Int block in blocks)
            {
                if (block.x < 3 || block.y < 3 || block.z < 3 || block.x > limit || block.y > limit || block.z > limit)
                    throw new InvalidOperationException("Fog atlas block exceeds device axis budget.");
                if ((long)block.x * block.y > budget / bytesPerVoxel / block.z)
                    throw new InvalidOperationException("Fog atlas block exceeds its render memory budget.");
                occupied += (long)block.x * block.y * block.z;
                minimum = Vector3Int.Max(minimum, block);
            }
            if (occupied > budget / bytesPerVoxel) throw new InvalidOperationException("Fog atlas exceeds its render memory budget.");
            var order = new int[blocks.Count];
            for (int i = 0; i < order.Length; ++i) order[i] = i;
            Array.Sort(order, (a, b) => {
                int compare = blocks[b].z.CompareTo(blocks[a].z);
                if (compare == 0) compare = blocks[b].y.CompareTo(blocks[a].y);
                if (compare == 0) compare = blocks[b].x.CompareTo(blocks[a].x);
                return compare == 0 ? a.CompareTo(b) : compare;
            });
            var trial = new Vector3Int[blocks.Count];
            Vector3Int result = Vector3Int.zero; long best = budget / bytesPerVoxel + 1;
            // shortcut: bounded shelf search, not an optimal bin packer; revisit if a real admitted house cannot fit.
            List<int> heights = Candidates(minimum.y, limit);
            foreach (int width in Candidates(minimum.x, limit)) foreach (int height in heights)
            {
                int x = 0, y = 0, z = 0, row = 0, layer = 0; Vector3Int extent = Vector3Int.zero; bool fits = true;
                foreach (int i in order)
                {
                    Vector3Int block = blocks[i];
                    if (x > width - block.x) { x = 0; y += row; row = 0; }
                    if (y > height - block.y) { x = y = row = 0; z += layer; layer = 0; }
                    if (z > limit - block.z) { fits = false; break; }
                    trial[i] = new Vector3Int(x, y, z);
                    extent = Vector3Int.Max(extent, trial[i] + block);
                    x += block.x; row = Mathf.Max(row, block.y); layer = Mathf.Max(layer, block.z);
                }
                if (!fits || (long)extent.x * extent.y > (best - 1) / extent.z) continue;
                best = (long)extent.x * extent.y * extent.z; result = extent;
                Array.Copy(trial, offsets, blocks.Count);
            }
            if (result == Vector3Int.zero) throw new InvalidOperationException("Fog atlas cannot fit its device/render memory budget.");
            return result;
        }

        private static List<int> Candidates(int minimum, int limit)
        {
            var result = new List<int>(40);
            for (int i = 0; i < 32 && minimum <= limit - i * 8; ++i) result.Add(minimum + i * 8);
            for (long value = minimum; value < limit; value *= 2)
                if (!result.Contains((int)value)) result.Add((int)value);
            if (!result.Contains(limit)) result.Add(limit);
            return result;
        }

        internal void Render(Camera camera, RenderTexture image, RenderTexture target, IReadOnlyList<BlueprintFogVisual> owners,
            Vector3 beamOrigin, Vector3 beamDirection, float beamStrength, IReadOnlyList<BlueprintFogChannels.SnapshotChannel> snapshots = null)
        {
            if (!material) throw new ObjectDisposedException(nameof(BlueprintFogAtlas));
            if (!camera || camera.orthographic || camera.stereoEnabled || (camera.depthTextureMode & DepthTextureMode.Depth) == 0)
                throw new InvalidOperationException("Fog atlas requires a perspective mono camera with depth.");
            if (owners == null || owners.Count < 1 || owners.Count > MaximumOwners) throw new ArgumentException("Fog atlas owner count invalid.");
            if (publishedPool != null)
            {
                if (snapshots == null || snapshots.Count != owners.Count || preparedCount != owners.Count)
                    throw new InvalidOperationException("Prepare published fog before drawing geometry.");
                for (int i = 0; i < owners.Count; ++i) if (!ReferenceEquals(preparedOwners[i], owners[i]) || !ReferenceEquals(preparedSnapshots[i], snapshots[i]))
                    throw new InvalidOperationException("Prepare changed fog owners before drawing geometry.");
                PreparePublished(owners, snapshots);
            }
            else if (snapshots != null) throw new ArgumentException("Legacy fog atlas cannot consume snapshot owners.");
            blocks.Clear(); unique.Clear(); Bounds bounds = default;
            Vector3 frame = camera.transform.position;
            frame = new Vector3(Mathf.Floor(frame.x / 128) * 128, Mathf.Floor(frame.y / 128) * 128, Mathf.Floor(frame.z / 128) * 128);
            for (int i = 0; i < owners.Count; ++i)
            {
                BlueprintFogVisual owner = owners[i];
                if (owner == null || !unique.Add(owner) || owner.Source == null || !owner.Source.Shape || !owner.Smoke || !owner.Surface) throw new ArgumentException("Fog atlas needs distinct live owners.");
                if (i == 0) bounds = owner.VolumeBounds; else bounds.Encapsulate(owner.VolumeBounds);
                Texture3D source = owner.Source.Shape; Vector3Int field = publishedPool == null ? owner.FieldSize : new Vector3Int(96, 80, 80);
                blocks.Add(publishedPool != null ? new Vector3Int(source.width + 2, source.height + 2, source.depth + 2) :
                    new Vector3Int(Mathf.Max(source.width, field.x) + 2, Mathf.Max(source.height, field.y) + 2, Mathf.Max(source.depth, field.z) + 2));
                Vector3 origin = owner.Origin - frame;
                origins[i] = new Vector4(origin.x, origin.y, origin.z, owner.Clock);
                noiseOffset[i] = owner.NoiseOffset;
                restMin[i] = owner.Source.Minimum; restMax[i] = owner.Source.Maximum;
                fieldMin[i] = owner.FieldMinimum; fieldMax[i] = owner.FieldMaximum; actors[i] = owner.ActorRadius;
                shapeSize[i] = new Vector4(source.width, source.height, source.depth, owner.Simulation == null ? 0 : 1);
                fieldSize[i] = new Vector4(field.x, field.y, field.z, owner.BodyHalfHeight);
            }
            int steps = Mathf.CeilToInt(bounds.size.magnitude / .025f) + 162;
            if (bounds.size.magnitude > 64 || (long)steps * owners.Count > 32768)
                throw new InvalidOperationException("Fog atlas exceeds its bounded integration work.");
            int limit = SystemInfo.maxTexture3DSize;
            bool changed = layoutLimit != limit || layoutBlocks.Count != blocks.Count;
            for (int i = 0; !changed && i < blocks.Count; ++i) changed = layoutBlocks[i] != blocks[i];
            if (changed) ChangeLayout(blocks, limit, Resize);
            for (int i = 0; i < owners.Count; ++i)
            {
                Vector3Int block = blocks[i], offset = offsets[i];
                atlasOffset[i] = new Vector4(offset.x, offset.y, offset.z, 0);
                BlueprintFogVisual owner = owners[i]; Texture currentSmoke = owner.Smoke, currentSurface = owner.Surface;
                int tick = owner.Simulation?.Tick ?? -1;
                if (publishedPool != null)
                {
                    if (ReferenceEquals(packedOwners[i], owner)) continue;
                    pack.SetInts("_StaticSize", block.x, block.y, block.z); pack.SetInts("_StaticOffset", offset.x, offset.y, offset.z);
                    pack.SetInts("_StaticShapeSize", owner.Source.Shape.width, owner.Source.Shape.height, owner.Source.Shape.depth);
                    pack.SetTexture(kernel, "_StaticSource", owner.Source.Shape); pack.SetTexture(kernel, "_StaticAtlas", shape);
                    pack.Dispatch(kernel, (block.x + 7) / 8, (block.y + 7) / 8, (block.z + 3) / 4);
                    ++PackDispatches; packedOwners[i] = owner; continue;
                }
                // Source is immutable; field contents change only on a simulation tick or texture replacement.
                if (ReferenceEquals(packedOwners[i], owner) && ReferenceEquals(packedSmoke[i], currentSmoke) &&
                    ReferenceEquals(packedSurface[i], currentSurface) && packedTicks[i] == tick) continue;
                // Pack only this block, retaining its own duplicated filtering borders.
                pack.SetVector("_PackSize", new Vector4(block.x, block.y, block.z, 0));
                pack.SetVector("_PackOffset", atlasOffset[i]); pack.SetVector("_PackShapeSize", shapeSize[i]); pack.SetVector("_PackFieldSize", fieldSize[i]);
                pack.SetTexture(kernel, "_PackShape", owners[i].Source.Shape); pack.SetTexture(kernel, "_PackSmoke", owners[i].Smoke);
                pack.SetTexture(kernel, "_PackSurface", owners[i].Surface); pack.SetTexture(kernel, "_PackedShape", shape);
                pack.SetTexture(kernel, "_PackedSmoke", smoke); pack.SetTexture(kernel, "_PackedSurface", surface);
                pack.Dispatch(kernel, (block.x + 7) / 8, (block.y + 7) / 8, (block.z + 3) / 4);
                ++PackDispatches;
                packedOwners[i] = owner; packedSmoke[i] = currentSmoke; packedSurface[i] = currentSurface; packedTicks[i] = tick;
            }
            material.SetTexture("_AtlasShape", shape);
            material.SetTexture("_AtlasSmoke", publishedPool?.SnapshotArena.Smoke ?? smoke);
            material.SetTexture("_AtlasSurface", publishedPool?.SnapshotArena.Surface ?? surface);
            if (publishedPool != null)
            {
                material.SetBuffer("_FogBindings", bindings); material.SetBuffer("_PublishedMetadata", publishedPool.SnapshotArena.Buffers["_PublishedMetadata"]);
                material.SetInt("_PublishedSlots", publishedPool.SnapshotCapacity);
                material.SetVector("_DynamicAtlasSize", new Vector4(98, 82, 82 * publishedPool.SnapshotCapacity, 0));
                material.SetVectorArray("_RegionDynamicOffset", dynamicOffsets);
            }
            // BlueprintFogSource creates the same immutable procedural noise for every owner.
            material.SetTexture("_Noise", owners[0].Source.Noise);
            material.SetVector("_AtlasSize", new Vector4(size.x, size.y, size.z, 0)); material.SetInt("_RegionCount", owners.Count);
            material.SetVectorArray("_RegionOriginClock", origins); material.SetVectorArray("_RegionRestMin", restMin); material.SetVectorArray("_RegionRestMax", restMax);
            material.SetVectorArray("_RegionFieldMin", fieldMin); material.SetVectorArray("_RegionFieldMax", fieldMax); material.SetVectorArray("_RegionActorRadius", actors);
            material.SetVectorArray("_RegionShapeSize", shapeSize); material.SetVectorArray("_RegionFieldSize", fieldSize); material.SetVectorArray("_RegionOffset", atlasOffset);
            material.SetVectorArray("_RegionNoiseOffset", noiseOffset);
            material.SetVector("_FineMin", bounds.min - frame); material.SetVector("_FineMax", bounds.max - frame); material.SetInt("_IntegrationSteps", steps);
            material.SetVector("_FogCameraOrigin", camera.transform.position - frame);
            material.SetVector("_CameraRight", camera.transform.right); material.SetVector("_CameraUp", camera.transform.up); material.SetVector("_CameraForward", camera.transform.forward);
            material.SetFloat("_Aspect", camera.aspect); material.SetFloat("_TanHalfFov", Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
            material.SetVector("_BeamOrigin", beamOrigin - frame); material.SetVector("_BeamDirection", beamDirection.normalized);
            material.SetFloat("_BeamStrength", beamStrength); material.SetFloat("_BeamAttenuation", 1);
            Graphics.Blit(image, target, material);
        }

        internal void ChangeLayout(IReadOnlyList<Vector3Int> requested, int limit, Action<Vector3Int> allocate)
        {
            if (allocate == null) throw new ArgumentNullException(nameof(allocate));
            var pending = new Vector3Int[MaximumOwners];
            Vector3Int wanted = publishedPool == null ? Layout(requested, limit, pending) :
                Layout(requested, limit, pending, 2, MaximumBytes - publishedPool.SnapshotArena.RenderBytes);
            // Commit coordinates only after allocation succeeds, so the old atlas remains reusable on failure.
            allocate(wanted);
            Array.Copy(pending, offsets, requested.Count);
            layoutBlocks.Clear(); for (int i = 0; i < requested.Count; ++i) layoutBlocks.Add(requested[i]);
            layoutLimit = limit;
            ClearPackedOwners();
        }

        private void Resize(Vector3Int wanted)
        {
            if (shape && wanted == size) return;
            RenderTexture a = null, b = null, c = null;
            try
            {
                a = Allocate(wanted, RenderTextureFormat.RHalf);
                if (publishedPool == null) { b = Allocate(wanted, RenderTextureFormat.ARGBHalf); c = Allocate(wanted, RenderTextureFormat.ARGBHalf); }
            }
            catch { Release(ref a); Release(ref b); Release(ref c); throw; }
            Release(ref shape); Release(ref smoke); Release(ref surface); shape = a; smoke = b; surface = c; size = wanted;
        }
        private static RenderTexture Allocate(Vector3Int size, RenderTextureFormat format)
        {
            var result = new RenderTexture(size.x, size.y, 0, format, RenderTextureReadWrite.Linear) {
                dimension = TextureDimension.Tex3D, volumeDepth = size.z, enableRandomWrite = true, filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            if (!result.Create()) { UnityEngine.Object.DestroyImmediate(result); throw new InvalidOperationException("Fog render atlas allocation failed."); }
            return result;
        }
        private static void Release(ref RenderTexture value)
        { if (value) { value.Release(); UnityEngine.Object.DestroyImmediate(value); } value = null; }
        private void ClearPackedOwners()
        { Array.Clear(packedOwners, 0, MaximumOwners); Array.Clear(packedSmoke, 0, MaximumOwners); Array.Clear(packedSurface, 0, MaximumOwners); }
        public void Dispose()
        {
            for (int i = 0; i < preparedCount; ++i) preparedOwners[i]?.UnbindPublished();
            preparedCount = 0;
            Release(ref shape); Release(ref smoke); Release(ref surface);
            bindings?.Release(); bindings = null;
            ClearPackedOwners();
            if (material) UnityEngine.Object.DestroyImmediate(material); material = null;
            if (pack) UnityEngine.Object.DestroyImmediate(pack); pack = null;
        }
    }
}
