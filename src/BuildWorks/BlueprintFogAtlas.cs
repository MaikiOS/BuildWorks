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
        private readonly HashSet<BlueprintFogVisual> unique = new HashSet<BlueprintFogVisual>();
        private readonly Vector3Int[] offsets = new Vector3Int[MaximumOwners];
        private readonly Vector4[] origins = new Vector4[MaximumOwners], restMin = new Vector4[MaximumOwners], restMax = new Vector4[MaximumOwners],
            fieldMin = new Vector4[MaximumOwners], fieldMax = new Vector4[MaximumOwners], actors = new Vector4[MaximumOwners],
            shapeSize = new Vector4[MaximumOwners], fieldSize = new Vector4[MaximumOwners], atlasOffset = new Vector4[MaximumOwners];
        internal long Bytes => shape ? (long)size.x * size.y * size.z * 18 : 0;

        internal BlueprintFogAtlas(BlueprintFogAssets assets)
        {
            if (assets == null || !assets.Volume || !assets.Solver) throw new ArgumentException("Fog atlas assets missing.");
            try
            {
                material = new Material(assets.Volume) { hideFlags = HideFlags.HideAndDontSave };
                material.DisableKeyword("BUILDWORKS_FOG_TWO_SOURCES"); material.EnableKeyword("BUILDWORKS_FOG_ATLAS");
                pack = UnityEngine.Object.Instantiate(assets.Solver); pack.hideFlags = HideFlags.HideAndDontSave;
                kernel = pack.FindKernel("PackVolume");
            }
            catch { Dispose(); throw; }
        }

        internal static Vector3Int Layout(IReadOnlyList<Vector3Int> blocks, int limit, Vector3Int[] offsets)
        {
            if (blocks == null || blocks.Count < 1 || blocks.Count > MaximumOwners || offsets == null || offsets.Length < blocks.Count || limit < 1)
                throw new ArgumentException("Fog atlas layout input invalid.");
            Vector3Int minimum = Vector3Int.zero; long occupied = 0;
            foreach (Vector3Int block in blocks)
            {
                if (block.x < 3 || block.y < 3 || block.z < 3 || block.x > limit || block.y > limit || block.z > limit)
                    throw new InvalidOperationException("Fog atlas block exceeds device axis budget.");
                if ((long)block.x * block.y > MaximumBytes / 18 / block.z)
                    throw new InvalidOperationException("Fog atlas block exceeds its render memory budget.");
                occupied += (long)block.x * block.y * block.z;
                minimum = Vector3Int.Max(minimum, block);
            }
            if (occupied > MaximumBytes / 18) throw new InvalidOperationException("Fog atlas exceeds its render memory budget.");
            var order = new int[blocks.Count];
            for (int i = 0; i < order.Length; ++i) order[i] = i;
            Array.Sort(order, (a, b) => {
                int compare = blocks[b].z.CompareTo(blocks[a].z);
                if (compare == 0) compare = blocks[b].y.CompareTo(blocks[a].y);
                if (compare == 0) compare = blocks[b].x.CompareTo(blocks[a].x);
                return compare == 0 ? a.CompareTo(b) : compare;
            });
            var trial = new Vector3Int[blocks.Count];
            Vector3Int result = Vector3Int.zero; long best = MaximumBytes / 18 + 1;
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
            Vector3 beamOrigin, Vector3 beamDirection, float beamStrength)
        {
            if (!material) throw new ObjectDisposedException(nameof(BlueprintFogAtlas));
            if (!camera || camera.orthographic || camera.stereoEnabled || (camera.depthTextureMode & DepthTextureMode.Depth) == 0)
                throw new InvalidOperationException("Fog atlas requires a perspective mono camera with depth.");
            if (owners == null || owners.Count < 1 || owners.Count > MaximumOwners) throw new ArgumentException("Fog atlas owner count invalid.");
            blocks.Clear(); unique.Clear(); Bounds bounds = default;
            Vector3 frame = camera.transform.position;
            frame = new Vector3(Mathf.Floor(frame.x / 128) * 128, Mathf.Floor(frame.y / 128) * 128, Mathf.Floor(frame.z / 128) * 128);
            for (int i = 0; i < owners.Count; ++i)
            {
                BlueprintFogVisual owner = owners[i];
                if (owner == null || !unique.Add(owner) || owner.Source == null || !owner.Source.Shape || owner.Simulation == null ||
                    !owner.Simulation.Smoke || !owner.Simulation.Surface) throw new ArgumentException("Fog atlas needs distinct live owners.");
                if (i == 0) bounds = owner.VolumeBounds; else bounds.Encapsulate(owner.VolumeBounds);
                Texture3D source = owner.Source.Shape; RenderTexture field = owner.Simulation.Smoke;
                blocks.Add(new Vector3Int(Mathf.Max(source.width, field.width) + 2, Mathf.Max(source.height, field.height) + 2,
                    Mathf.Max(source.depth, field.volumeDepth) + 2));
                Vector3 origin = owner.Origin - frame;
                origins[i] = new Vector4(origin.x, origin.y, origin.z, owner.Clock);
                restMin[i] = owner.Source.Minimum; restMax[i] = owner.Source.Maximum;
                fieldMin[i] = owner.FieldMinimum; fieldMax[i] = owner.FieldMaximum; actors[i] = owner.ActorRadius;
                shapeSize[i] = new Vector4(source.width, source.height, source.depth, 0);
                fieldSize[i] = new Vector4(field.width, field.height, field.volumeDepth, owner.BodyHalfHeight);
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
                // Pack only this block, retaining its own duplicated filtering borders.
                pack.SetVector("_PackSize", new Vector4(block.x, block.y, block.z, 0));
                pack.SetVector("_PackOffset", atlasOffset[i]); pack.SetVector("_PackShapeSize", shapeSize[i]); pack.SetVector("_PackFieldSize", fieldSize[i]);
                pack.SetTexture(kernel, "_PackShape", owners[i].Source.Shape); pack.SetTexture(kernel, "_PackSmoke", owners[i].Simulation.Smoke);
                pack.SetTexture(kernel, "_PackSurface", owners[i].Simulation.Surface); pack.SetTexture(kernel, "_PackedShape", shape);
                pack.SetTexture(kernel, "_PackedSmoke", smoke); pack.SetTexture(kernel, "_PackedSurface", surface);
                pack.Dispatch(kernel, (block.x + 7) / 8, (block.y + 7) / 8, (block.z + 3) / 4);
            }
            material.SetTexture("_AtlasShape", shape); material.SetTexture("_AtlasSmoke", smoke); material.SetTexture("_AtlasSurface", surface);
            // BlueprintFogSource creates the same immutable procedural noise for every owner.
            material.SetTexture("_Noise", owners[0].Source.Noise);
            material.SetVector("_AtlasSize", new Vector4(size.x, size.y, size.z, 0)); material.SetInt("_RegionCount", owners.Count);
            material.SetVectorArray("_RegionOriginClock", origins); material.SetVectorArray("_RegionRestMin", restMin); material.SetVectorArray("_RegionRestMax", restMax);
            material.SetVectorArray("_RegionFieldMin", fieldMin); material.SetVectorArray("_RegionFieldMax", fieldMax); material.SetVectorArray("_RegionActorRadius", actors);
            material.SetVectorArray("_RegionShapeSize", shapeSize); material.SetVectorArray("_RegionFieldSize", fieldSize); material.SetVectorArray("_RegionOffset", atlasOffset);
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
            Vector3Int wanted = Layout(requested, limit, pending);
            // Commit coordinates only after allocation succeeds, so the old atlas remains reusable on failure.
            allocate(wanted);
            Array.Copy(pending, offsets, requested.Count);
            layoutBlocks.Clear(); for (int i = 0; i < requested.Count; ++i) layoutBlocks.Add(requested[i]);
            layoutLimit = limit;
        }

        private void Resize(Vector3Int wanted)
        {
            if (shape && wanted == size) return;
            RenderTexture a = null, b = null, c = null;
            try
            {
                a = Allocate(wanted, RenderTextureFormat.RHalf); b = Allocate(wanted, RenderTextureFormat.ARGBHalf); c = Allocate(wanted, RenderTextureFormat.ARGBHalf);
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
        public void Dispose()
        {
            Release(ref shape); Release(ref smoke); Release(ref surface);
            if (material) UnityEngine.Object.DestroyImmediate(material); material = null;
            if (pack) UnityEngine.Object.DestroyImmediate(pack); pack = null;
        }
    }
}
