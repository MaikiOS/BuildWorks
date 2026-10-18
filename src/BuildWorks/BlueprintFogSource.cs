using System;
using System.Collections.Generic;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // shortcut: one complete part per source; large parts still need spatial clipping before whole-house rendering.
    internal sealed class BlueprintFogSource : IDisposable
    {
        internal sealed class Part
        {
            internal MeshFilter Filter;
            internal MeshRenderer Renderer;
            internal Mesh Original, Dense;
            internal BlueprintEditorMeshData.SurfaceData Data;
            internal Matrix4x4 ToMetres;
        }
        internal readonly List<Part> Parts = new List<Part>();
        internal readonly Vector3 Minimum = new Vector3(-2.4f, -.7f, -1.7f), Maximum = new Vector3(2.4f, 3.8f, 1.7f);
        internal Texture3D Shape { get; private set; }
        internal Texture3D Pigment { get; private set; }
        internal Texture3D Noise { get; private set; }
        internal int Occupied { get; private set; }
        private readonly int Nx = 144, Ny = 112, Nz = 120;
        private bool preparing, prepared, disposed, cooperative;
        private long sliceStarted;
        internal string PreparationPhase { get; private set; }
        private void BeginSlice()
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogSource));
            sliceStarted = System.Diagnostics.Stopwatch.GetTimestamp();
        }
        private bool YieldDue => cooperative && (System.Diagnostics.Stopwatch.GetTimestamp() - sliceStarted) >= System.Diagnostics.Stopwatch.Frequency * .008;

        internal BlueprintFogSource(GameObject visual, Vector3 origin, bool compact = false, bool deferPreparation = false)
        {
            if (!visual || !Finite(origin)) throw new ArgumentException("Missing fog visual or local origin.");
            try
            {
                var meshes = new Dictionary<Mesh, BlueprintEditorMeshData.SurfaceData>();
                int vertices = 0;
                foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                    if (!renderer || !renderer.enabled || !ActiveBelow(filter.transform, visual.transform)) continue;
                    Mesh mesh = filter.sharedMesh;
                    if (!meshes.TryGetValue(mesh, out var data))
                    {
                        if (!BlueprintEditorMeshData.TryReadSurface(mesh, out data, out string error)) throw new InvalidOperationException(error);
                        meshes.Add(mesh, data);
                    }
                    vertices = checked(vertices + data.Positions.Length);
                    if (Parts.Count >= 64 || vertices > 250000) throw new InvalidOperationException("Fog source exceeds one-region geometry budget.");
                    Matrix4x4 matrix = Matrix4x4.Translate(-origin) * filter.transform.localToWorldMatrix;
                    Parts.Add(new Part { Filter = filter, Renderer = renderer, Original = mesh, Data = data, ToMetres = matrix });
                }
                if (Parts.Count == 0) throw new InvalidOperationException("Fog source has no visible supported surface.");
                if (compact)
                {
                    Vector3 low = Vector3.one * float.PositiveInfinity, high = Vector3.one * float.NegativeInfinity;
                    foreach (Part part in Parts) foreach (int[] indices in part.Data.Submeshes) foreach (int index in indices)
                    {
                        Vector3 point = part.ToMetres.MultiplyPoint3x4(part.Data.Positions[index]);
                        if (!Finite(point)) throw new InvalidOperationException("Fog source transform overflow.");
                        low = Vector3.Min(low, point); high = Vector3.Max(high, point);
                    }
                    if (!Finite(low) || !Finite(high)) throw new InvalidOperationException("Fog source has no indexed surface.");
                    if (low.x < -60 || low.y < -60 || low.z < -60 || high.x > 60 || high.y > 60 || high.z > 60)
                        throw new InvalidOperationException("Fog part requires spatial clipping before local source allocation.");
                    Vector3 voxel = new Vector3(4.8f / 144, 4.5f / 112, 3.4f / 120);
                    Minimum = low - voxel * 3;
                    Vector3 size = high - Minimum + voxel * 3;
                    Nx = Mathf.Max(8, Mathf.CeilToInt(size.x / voxel.x - .0001f));
                    Ny = Mathf.Max(8, Mathf.CeilToInt(size.y / voxel.y - .0001f));
                    Nz = Mathf.Max(8, Mathf.CeilToInt(size.z / voxel.z - .0001f));
                    Maximum = Minimum + Vector3.Scale(new Vector3(Nx, Ny, Nz), voxel);
                    if (Nx > 256 || Ny > 256 || Nz > 256 || (long)Nx * Ny * Nz > 144L * 112 * 120 ||
                        Minimum.x < -64 || Minimum.y < -64 || Minimum.z < -64 || Maximum.x > 64 || Maximum.y > 64 || Maximum.z > 64)
                        throw new InvalidOperationException("Fog part exceeds its compact source budget; spatial clipping is required.");
                }
                if (!deferPreparation) foreach (var step in Prepare(false)) { }
            }
            catch { Dispose(); throw; }
        }

        internal IEnumerable<object> Prepare(bool yieldBetweenSlices = true)
        {
            if (disposed || preparing || prepared) throw new InvalidOperationException("Fog source preparation is closed or already started.");
            preparing = true; cooperative = yieldBetweenSlices; BeginSlice();
            bool complete = false;
            try
            {
                foreach (var step in Bake()) yield return step;
                PreparationPhase = "noise";
                if (cooperative) { yield return null; BeginSlice(); }
                foreach (var step in CreateNoise()) yield return step;
                PreparationPhase = "mesh";
                if (cooperative) { yield return null; BeginSlice(); }
                int denseVertices = 0;
                foreach (Part part in Parts)
                {
                    part.Dense = BlueprintFogContactMesh.Create(part.Original, part.Data, part.ToMetres);
                    denseVertices = checked(denseVertices + part.Dense.vertexCount);
                    if (denseVertices > 500000) throw new InvalidOperationException("Fog region exceeds its total contact vertex budget.");
                    if (YieldDue) { yield return null; BeginSlice(); }
                }
                prepared = complete = true; PreparationPhase = "ready";
            }
            finally { preparing = false; if (!complete) Dispose(); }
        }

        internal bool MightContact(Vector3 previous, Vector3 current, Vector2 body)
        {
            if (!Shape) throw new ObjectDisposedException(nameof(BlueprintFogSource));
            Vector3 voxel = new Vector3((Maximum.x - Minimum.x) / Nx, (Maximum.y - Minimum.y) / Ny, (Maximum.z - Minimum.z) / Nz);
            // Include the .27m emission envelope, .065m surface displacement and the full trilinear footprint.
            Vector3 reach = new Vector3(body.x + .35f, body.y + .35f, body.x + .35f) + voxel;
            Vector3 low = Vector3.Min(previous, current) - reach, high = Vector3.Max(previous, current) + reach;
            if (high.x < Minimum.x || high.y < Minimum.y || high.z < Minimum.z ||
                low.x > Maximum.x || low.y > Maximum.y || low.z > Maximum.z) return false;
            int Start(float a, float b, float step, int count) => Mathf.Clamp(Mathf.FloorToInt((a - b) / step - .5f), 0, count - 1);
            int End(float a, float b, float step, int count) => Mathf.Clamp(Mathf.CeilToInt((a - b) / step - .5f), 0, count - 1);
            int x0 = Start(low.x, Minimum.x, voxel.x, Nx), x1 = End(high.x, Minimum.x, voxel.x, Nx);
            int y0 = Start(low.y, Minimum.y, voxel.y, Ny), y1 = End(high.y, Minimum.y, voxel.y, Ny);
            int z0 = Start(low.z, Minimum.z, voxel.z, Nz), z1 = End(high.z, Minimum.z, voxel.z, Nz);
            var density = Shape.GetPixelData<byte>(0);
            Vector2 path = new Vector2(current.x - previous.x, current.z - previous.z);
            float radius = body.x + .35f + voxel.magnitude, squared = radius * radius;
            for (int z = z0; z <= z1; ++z) for (int y = y0; y <= y1; ++y) for (int x = x0; x <= x1; ++x)
            {
                if (density[x + Nx * (y + Ny * z)] == 0) continue;
                Vector2 delta = new Vector2(Minimum.x + (x + .5f) * voxel.x - previous.x,
                    Minimum.z + (z + .5f) * voxel.z - previous.z);
                float t = path.sqrMagnitude > .000001f ? Mathf.Clamp01(Vector2.Dot(delta, path) / path.sqrMagnitude) : 0;
                if ((delta - path * t).sqrMagnitude <= squared) return true;
            }
            return false;
        }

        private IEnumerable<object> Bake()
        {
            PreparationPhase = "sampling";
            if (cooperative) { yield return null; BeginSlice(); }
            var cells = new float[Nx * Ny * Nz]; var colors = new Color[cells.Length];
            var copies = new Dictionary<Texture, Texture2D>(); int samples = 0;
            Vector3 size = Maximum - Minimum, grid = new Vector3(Nx / size.x, Ny / size.y, Nz / size.z);
            float spacing = Mathf.Min(size.x / Nx, size.y / Ny, size.z / Nz) * .65f;
            try
            {
                foreach (Part part in Parts)
                {
                    Material[] materials = part.Renderer.sharedMaterials;
                    if (materials.Length == 0) throw new InvalidOperationException("Fog source material missing.");
                    var block = new MaterialPropertyBlock(); part.Renderer.GetPropertyBlock(block);
                    for (int submesh = 0; submesh < part.Data.Submeshes.Length; ++submesh)
                    {
                        Material material = materials[Math.Min(submesh, materials.Length - 1)];
                        if (!material) throw new InvalidOperationException("Fog source material missing.");
                        Texture texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                        Texture2D albedo = null;
                        if (texture && !copies.TryGetValue(texture, out albedo)) { albedo = ReadableAlbedo(texture); copies.Add(texture, albedo); }
                        Color tint = block.HasColor("_Color") ? block.GetColor("_Color")
                            : material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                        float cutoff = Mathf.Max(.001f, material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0);
                        Vector2 scale = texture ? material.GetTextureScale("_MainTex") : Vector2.one;
                        Vector2 offset = texture ? material.GetTextureOffset("_MainTex") : Vector2.zero;
                        int[] indices = part.Data.Submeshes[submesh];
                        for (int t = 0; t < indices.Length; t += 3)
                        {
                            int ia = indices[t], ib = indices[t + 1], ic = indices[t + 2];
                            Vector3 a = part.ToMetres.MultiplyPoint3x4(part.Data.Positions[ia]);
                            Vector3 b = part.ToMetres.MultiplyPoint3x4(part.Data.Positions[ib]);
                            Vector3 c = part.ToMetres.MultiplyPoint3x4(part.Data.Positions[ic]);
                            if (!Finite(a) || !Finite(b) || !Finite(c)) throw new InvalidOperationException("Fog source transform overflow.");
                            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max((a - b).magnitude, Mathf.Max((b - c).magnitude, (c - a).magnitude)) / spacing));
                            if (steps > 256 || samples + (long)(steps + 1) * (steps + 2) / 2 > 5000000)
                                throw new InvalidOperationException("Fog source triangle sampling exceeds its budget.");
                            for (int i = 0; i <= steps; ++i) for (int j = 0; j <= steps - i; ++j)
                            {
                                if ((samples & 4095) == 0 && YieldDue) { yield return null; BeginSlice(); }
                                ++samples; float u = i / (float)steps, v = j / (float)steps;
                                Vector3 p = a + (b - a) * u + (c - a) * v;
                                Vector3 q = Vector3.Scale(p - Minimum, grid);
                                int x = Mathf.FloorToInt(q.x), y = Mathf.FloorToInt(q.y), z = Mathf.FloorToInt(q.z);
                                if (x <= 1 || x >= Nx - 2 || y <= 1 || y >= Ny - 2 || z <= 1 || z >= Nz - 2)
                                    throw new InvalidOperationException("Fog source exceeds the padded one-wall region at " + p.ToString("F4") + "; no global scale correction is applied.");
                                Vector2 uv = part.Data.UV[ia] + (part.Data.UV[ib] - part.Data.UV[ia]) * u + (part.Data.UV[ic] - part.Data.UV[ia]) * v;
                                uv = Vector2.Scale(uv, scale) + offset;
                                Color color = (albedo ? albedo.GetPixelBilinear(uv.x, uv.y) : Color.white) * tint;
                                if (color.a < cutoff) continue;
                                int cell = x + Nx * (y + Ny * z); cells[cell] = 1; color.a = 1; colors[cell] = color;
                            }
                        }
                    }
                }
            }
            finally { foreach (Texture2D texture in copies.Values) UnityEngine.Object.DestroyImmediate(texture); }
            for (int i = 0; i < cells.Length; ++i)
            {
                if (cells[i] > 0) ++Occupied;
                if ((i & 16383) == 0 && YieldDue) { yield return null; BeginSlice(); }
            }
            if (Occupied == 0) throw new InvalidOperationException("Fog source contains no visible density.");
            PreparationPhase = "filter";
            if (cooperative) { yield return null; BeginSlice(); }
            foreach (int stride in new[] { 1, Nx, Nx * Ny })
            {
                foreach (var step in Filter(cells, stride, true, result => cells = result)) yield return step;
                foreach (var step in SpreadColor(colors, stride, result => colors = result)) yield return step;
            }
            for (int repeat = 0; repeat < 2; ++repeat)
                foreach (int stride in new[] { 1, Nx, Nx * Ny })
                {
                    foreach (var step in Filter(cells, stride, false, result => cells = result)) yield return step;
                    foreach (var step in SpreadColor(colors, stride, result => colors = result)) yield return step;
                }
            PreparationPhase = "upload";
            if (cooperative) { yield return null; BeginSlice(); }
            var bytes = new byte[cells.Length];
            for (int i = 0; i < bytes.Length; ++i)
            {
                bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(cells[i]) * 255);
                if ((i & 16383) == 0 && YieldDue) { yield return null; BeginSlice(); }
            }
            Shape = new Texture3D(Nx, Ny, Nz, TextureFormat.R8, false) { name = "BuildWorks fog source", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
            Shape.SetPixelData(bytes, 0); Shape.Apply(false, false);
            PreparationPhase = "shape-uploaded";
            if (cooperative) { yield return null; BeginSlice(); }
            Pigment = new Texture3D(Nx, Ny, Nz, TextureFormat.RGBA32, false) { name = "BuildWorks fog pigment", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
            Pigment.SetPixels(colors); Pigment.Apply(false, false);
            PreparationPhase = "pigment-uploaded";
            if (cooperative) { yield return null; BeginSlice(); }
        }

        private static Texture2D ReadableAlbedo(Texture original)
        {
            RenderTexture target = RenderTexture.GetTemporary(256, 256, 0), previous = RenderTexture.active; Texture2D copy = null;
            try
            {
                Graphics.Blit(original, target); RenderTexture.active = target;
                copy = new Texture2D(256, 256, TextureFormat.RGBA32, false) { wrapMode = original.wrapMode };
                copy.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); copy.Apply(); return copy;
            }
            catch { if (copy) UnityEngine.Object.DestroyImmediate(copy); throw; }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); }
        }
        private IEnumerable<object> Filter(float[] source, int stride, bool dilate, Action<float[]> done)
        {
            var result = new float[source.Length];
            for (int z = 1; z < Nz - 1; ++z)
            {
            for (int y = 1; y < Ny - 1; ++y) for (int x = 1; x < Nx - 1; ++x)
            { int i = x + Nx * (y + Ny * z); result[i] = dilate ? Mathf.Max(source[i], Mathf.Max(source[i - stride], source[i + stride])) : source[i - stride] * .25f + source[i] * .5f + source[i + stride] * .25f; }
                if (YieldDue) { yield return null; BeginSlice(); }
            }
            done(result);
        }
        private IEnumerable<object> SpreadColor(Color[] source, int stride, Action<Color[]> done)
        {
            var result = (Color[])source.Clone();
            for (int z = 1; z < Nz - 1; ++z)
            {
            for (int y = 1; y < Ny - 1; ++y) for (int x = 1; x < Nx - 1; ++x)
            { int i = x + Nx * (y + Ny * z); if (source[i].a > 0) continue; Color sum = source[i - stride] + source[i + stride]; if (sum.a > 0) result[i] = sum / sum.a; }
                if (YieldDue) { yield return null; BeginSlice(); }
            }
            done(result);
        }
        private IEnumerable<object> CreateNoise()
        {
            const int n = 64; var bytes = new byte[n * n * n];
            float Hash(int x, int y, int z) { uint h = unchecked((uint)((x & 15) * 374761393 + (y & 15) * 668265263 + (z & 15) * 1274126177)); h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) / (float)uint.MaxValue; }
            for (int z = 0; z < n; ++z)
            {
            for (int y = 0; y < n; ++y) for (int x = 0; x < n; ++x)
            {
                int ix = x / 4, iy = y / 4, iz = z / 4;
                float u = Mathf.SmoothStep(0, 1, x % 4 / 4f), v = Mathf.SmoothStep(0, 1, y % 4 / 4f), w = Mathf.SmoothStep(0, 1, z % 4 / 4f);
                float low = Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, iz), Hash(ix + 1, iy, iz), u), Mathf.Lerp(Hash(ix, iy + 1, iz), Hash(ix + 1, iy + 1, iz), u), v);
                float high = Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, iz + 1), Hash(ix + 1, iy, iz + 1), u), Mathf.Lerp(Hash(ix, iy + 1, iz + 1), Hash(ix + 1, iy + 1, iz + 1), u), v);
                bytes[x + n * (y + n * z)] = (byte)(Mathf.Lerp(low, high, w) * 255);
            }
                if (YieldDue) { yield return null; BeginSlice(); }
            }
            var texture = new Texture3D(n, n, n, TextureFormat.R8, false) { name = "BuildWorks fog noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
            try { texture.SetPixelData(bytes, 0); texture.Apply(false, false); Noise = texture; }
            catch { UnityEngine.Object.DestroyImmediate(texture); throw; }
        }
        private static bool ActiveBelow(Transform node, Transform root)
        { for (; node && node != root; node = node.parent) if (!node.gameObject.activeSelf) return false; return node == root; }
        private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (Part part in Parts) if (part.Dense) UnityEngine.Object.DestroyImmediate(part.Dense);
            Parts.Clear();
            if (Shape) UnityEngine.Object.DestroyImmediate(Shape); Shape = null;
            if (Pigment) UnityEngine.Object.DestroyImmediate(Pigment); Pigment = null;
            if (Noise) UnityEngine.Object.DestroyImmediate(Noise); Noise = null;
        }
    }
}
