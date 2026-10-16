using System;
using System.Collections.Generic;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // shortcut: bounded one-wall source volume; replace with owned local regions before whole-house rendering.
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
        private const int Nx = 144, Ny = 112, Nz = 120;

        internal BlueprintFogSource(GameObject visual, Vector3 origin)
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
                Bake(); Noise = CreateNoise();
                int denseVertices = 0;
                foreach (Part part in Parts)
                {
                    part.Dense = BlueprintFogContactMesh.Create(part.Original, part.Data, part.ToMetres);
                    denseVertices = checked(denseVertices + part.Dense.vertexCount);
                    if (denseVertices > 500000) throw new InvalidOperationException("Fog region exceeds its total contact vertex budget.");
                }
            }
            catch { Dispose(); throw; }
        }

        private void Bake()
        {
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
                                ++samples; float u = i / (float)steps, v = j / (float)steps;
                                Vector3 p = a + (b - a) * u + (c - a) * v;
                                Vector3 q = Vector3.Scale(p - Minimum, grid);
                                int x = Mathf.FloorToInt(q.x), y = Mathf.FloorToInt(q.y), z = Mathf.FloorToInt(q.z);
                                if (x <= 1 || x >= Nx - 2 || y <= 1 || y >= Ny - 2 || z <= 1 || z >= Nz - 2)
                                    throw new InvalidOperationException("Fog source exceeds the padded one-wall region at " + p.ToString("F4") + "; no global scale correction is applied.");
                                Vector2 uv = part.Data.UV[ia] + (part.Data.UV[ib] - part.Data.UV[ia]) * u + (part.Data.UV[ic] - part.Data.UV[ia]) * v;
                                uv = Vector2.Scale(uv, scale) + offset;
                                Color color = (albedo ? albedo.GetPixelBilinear(uv.x, uv.y) : Color.white) * tint;
                                int cell = x + Nx * (y + Ny * z); cells[cell] = 1; color.a = 1; colors[cell] = color;
                            }
                        }
                    }
                }
            }
            finally { foreach (Texture2D texture in copies.Values) UnityEngine.Object.DestroyImmediate(texture); }
            foreach (float cell in cells) if (cell > 0) ++Occupied;
            if (Occupied == 0) throw new InvalidOperationException("Fog source contains no visible density.");
            foreach (int stride in new[] { 1, Nx, Nx * Ny }) { cells = Filter(cells, stride, true); colors = SpreadColor(colors, stride); }
            for (int repeat = 0; repeat < 2; ++repeat)
                foreach (int stride in new[] { 1, Nx, Nx * Ny }) { cells = Filter(cells, stride, false); colors = SpreadColor(colors, stride); }
            var bytes = new byte[cells.Length];
            for (int i = 0; i < bytes.Length; ++i) bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(cells[i]) * 255);
            Shape = new Texture3D(Nx, Ny, Nz, TextureFormat.R8, false) { name = "BuildWorks fog source", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
            Shape.SetPixelData(bytes, 0); Shape.Apply(false, false);
            Pigment = new Texture3D(Nx, Ny, Nz, TextureFormat.RGBA32, false) { name = "BuildWorks fog pigment", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
            Pigment.SetPixels(colors); Pigment.Apply(false, false);
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
        private static float[] Filter(float[] source, int stride, bool dilate)
        {
            var result = new float[source.Length];
            for (int z = 1; z < Nz - 1; ++z) for (int y = 1; y < Ny - 1; ++y) for (int x = 1; x < Nx - 1; ++x)
            { int i = x + Nx * (y + Ny * z); result[i] = dilate ? Mathf.Max(source[i], Mathf.Max(source[i - stride], source[i + stride])) : source[i - stride] * .25f + source[i] * .5f + source[i + stride] * .25f; }
            return result;
        }
        private static Color[] SpreadColor(Color[] source, int stride)
        {
            var result = (Color[])source.Clone();
            for (int z = 1; z < Nz - 1; ++z) for (int y = 1; y < Ny - 1; ++y) for (int x = 1; x < Nx - 1; ++x)
            { int i = x + Nx * (y + Ny * z); if (source[i].a > 0) continue; Color sum = source[i - stride] + source[i + stride]; if (sum.a > 0) result[i] = sum / sum.a; }
            return result;
        }
        private static Texture3D CreateNoise()
        {
            const int n = 64; var bytes = new byte[n * n * n];
            float Hash(int x, int y, int z) { uint h = unchecked((uint)((x & 15) * 374761393 + (y & 15) * 668265263 + (z & 15) * 1274126177)); h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) / (float)uint.MaxValue; }
            for (int z = 0; z < n; ++z) for (int y = 0; y < n; ++y) for (int x = 0; x < n; ++x)
            {
                int ix = x / 4, iy = y / 4, iz = z / 4;
                float u = Mathf.SmoothStep(0, 1, x % 4 / 4f), v = Mathf.SmoothStep(0, 1, y % 4 / 4f), w = Mathf.SmoothStep(0, 1, z % 4 / 4f);
                float low = Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, iz), Hash(ix + 1, iy, iz), u), Mathf.Lerp(Hash(ix, iy + 1, iz), Hash(ix + 1, iy + 1, iz), u), v);
                float high = Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, iz + 1), Hash(ix + 1, iy, iz + 1), u), Mathf.Lerp(Hash(ix, iy + 1, iz + 1), Hash(ix + 1, iy + 1, iz + 1), u), v);
                bytes[x + n * (y + n * z)] = (byte)(Mathf.Lerp(low, high, w) * 255);
            }
            var texture = new Texture3D(n, n, n, TextureFormat.R8, false) { name = "BuildWorks fog noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, hideFlags = HideFlags.HideAndDontSave };
            try { texture.SetPixelData(bytes, 0); texture.Apply(false, false); return texture; }
            catch { UnityEngine.Object.DestroyImmediate(texture); throw; }
        }
        private static bool ActiveBelow(Transform node, Transform root)
        { for (; node && node != root; node = node.parent) if (!node.gameObject.activeSelf) return false; return node == root; }
        private static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsInfinity(p.x) && !float.IsNaN(p.y) && !float.IsInfinity(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.z);
        public void Dispose()
        {
            foreach (Part part in Parts) if (part.Dense) UnityEngine.Object.DestroyImmediate(part.Dense);
            Parts.Clear();
            if (Shape) UnityEngine.Object.DestroyImmediate(Shape); Shape = null;
            if (Pigment) UnityEngine.Object.DestroyImmediate(Pigment); Pigment = null;
            if (Noise) UnityEngine.Object.DestroyImmediate(Noise); Noise = null;
        }
    }
}
