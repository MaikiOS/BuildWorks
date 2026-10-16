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
            int width = 0, height = 0;
            foreach (Vector3Int block in blocks)
            {
                if (block.x < 3 || block.y < 3 || block.z < 3 || block.x > limit || block.y > limit || block.z > limit)
                    throw new InvalidOperationException("Fog atlas block exceeds device axis budget.");
                width = Mathf.Max(width, block.x); height = Mathf.Max(height, block.y);
            }
            int x = 0, y = 0, z = 0; Vector3Int result = Vector3Int.zero;
            for (int i = 0; i < blocks.Count; ++i)
            {
                if (z > limit - blocks[i].z) { z = 0; x += width; }
                if (x > limit - width) { x = 0; y += height; }
                if (y > limit - height) throw new InvalidOperationException("Fog atlas exceeds device axis budget.");
                offsets[i] = new Vector3Int(x, y, z); z += blocks[i].z;
                result = Vector3Int.Max(result, new Vector3Int(x + width, y + height, z));
            }
            if ((long)result.x * result.y * result.z * 18 > MaximumBytes)
                throw new InvalidOperationException("Fog atlas exceeds its render memory budget.");
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
            Vector3Int wanted = Layout(blocks, SystemInfo.maxTexture3DSize, offsets);
            Resize(wanted);
            int columnWidth = 0, columnHeight = 0;
            foreach (Vector3Int b in blocks) { columnWidth = Mathf.Max(columnWidth, b.x); columnHeight = Mathf.Max(columnHeight, b.y); }
            for (int i = 0; i < owners.Count; ++i)
            {
                Vector3Int block = blocks[i], offset = offsets[i];
                atlasOffset[i] = new Vector4(offset.x, offset.y, offset.z, 0);
                // Each Z column has the same XY pitch, including duplicated filtering borders.
                pack.SetVector("_PackSize", new Vector4(columnWidth, columnHeight, block.z, 0));
                pack.SetVector("_PackOffset", atlasOffset[i]); pack.SetVector("_PackShapeSize", shapeSize[i]); pack.SetVector("_PackFieldSize", fieldSize[i]);
                pack.SetTexture(kernel, "_PackShape", owners[i].Source.Shape); pack.SetTexture(kernel, "_PackSmoke", owners[i].Simulation.Smoke);
                pack.SetTexture(kernel, "_PackSurface", owners[i].Simulation.Surface); pack.SetTexture(kernel, "_PackedShape", shape);
                pack.SetTexture(kernel, "_PackedSmoke", smoke); pack.SetTexture(kernel, "_PackedSurface", surface);
                pack.Dispatch(kernel, (columnWidth + 7) / 8, (columnHeight + 7) / 8, (block.z + 3) / 4);
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
