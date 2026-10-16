using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using OstrixMods.BuildWorks;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogRuntimeMeshCheck
{
    [StructLayout(LayoutKind.Sequential)] private struct PositionNormal { public Vector3 p, n; }
    [StructLayout(LayoutKind.Sequential)] private struct TangentUV { public Vector4 t; public Vector2 uv; }
    [StructLayout(LayoutKind.Sequential)] private struct Short4 { public short x, y, z, w; }
    [StructLayout(LayoutKind.Sequential)] private struct Half2 { public ushort x, y; }
    private static readonly Vector3[] P = { new Vector3(-1,0,0),new Vector3(-.25f,0,0),new Vector3(-1,1,0),
        new Vector3(.25f,0,0),new Vector3(1,0,0),new Vector3(1,1,0) };
    private static readonly Vector2[] UV = { Vector2.zero,Vector2.right,Vector2.up,Vector2.zero,Vector2.right,Vector2.one };

    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-mesh"); Directory.CreateDirectory(output);
        int checkedVertices = 0, checkedTriangles = 0, cases = 0;
        foreach (int mode in new[] { 0, 1, 2 })
        {
            var mesh = Source(mode); Mesh dense = null;
            try
            {
                Require(BlueprintEditorMeshData.TryReadSurface(mesh, out var data, out string error), error);
                Require(data.Submeshes.Length == 2 && data.Submeshes[0].SequenceEqual(new[] { 0, 1, 2 }) &&
                    data.Submeshes[1].SequenceEqual(new[] { 3, 4, 5 }), "Submesh/baseVertex mapping lost");
                Require(data.Positions.SequenceEqual(P) && data.UV.SequenceEqual(UV), "Source position/UV changed");
                for (int i = 0; i < 6; ++i)
                {
                    Require(data.Normals[i] == Vector3.forward, "Source normal lost");
                    Require(data.Tangents[i] == new Vector4(1, 0, 0, i < 3 ? 1 : -1), "Tangent handedness lost");
                }
                var matrix = Matrix4x4.TRS(new Vector3(2500, 45, -1900), Quaternion.Euler(13, 37, 21), new Vector3(2, .5f, 1.3f));
                dense = BlueprintFogContactMesh.Create(mesh, data, matrix);
                Vector3[] p = dense.vertices;
                var cornersA = new System.Collections.Generic.List<Vector3>(); dense.GetUVs(1, cornersA);
                var cornersB = new System.Collections.Generic.List<Vector3>(); dense.GetUVs(2, cornersB);
                var cornersC = new System.Collections.Generic.List<Vector3>(); dense.GetUVs(3, cornersC);
                Color[] weights = dense.colors; Vector3[] normals = dense.normals; Vector4[] tangents = dense.tangents; Vector2[] uv = dense.uv;
                Require(dense.subMeshCount == 2 && p.Length > 6, "Dense surface/submesh missing");
                for (int submesh = 0; submesh < 2; ++submesh)
                {
                    int[] indices = dense.GetTriangles(submesh); int first = submesh * 3;
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                        Require(matrix.MultiplyVector(p[ia] - p[ib]).magnitude <= .16001f &&
                            matrix.MultiplyVector(p[ib] - p[ic]).magnitude <= .16001f &&
                            matrix.MultiplyVector(p[ic] - p[ia]).magnitude <= .16001f, "Subdivision exceeds world-metre spacing");
                        Require(Vector3.Cross(p[ib] - p[ia], p[ic] - p[ia]).z >= 0, "Subdivision flipped winding");
                        ++checkedTriangles;
                    }
                    foreach (int index in indices)
                    {
                        Color w = weights[index];
                        Require(Mathf.Abs(w.r + w.g + w.b - 1) < 1e-6f, "Barycentric sum changed");
                        Require(cornersA[index] == P[first] && cornersB[index] == P[first + 1] && cornersC[index] == P[first + 2], "Source corners lost");
                        Require((p[index] - (P[first] * w.r + P[first + 1] * w.g + P[first + 2] * w.b)).sqrMagnitude < 1e-10f, "Source shape changed");
                        Require((uv[index] - (UV[first] * w.r + UV[first + 1] * w.g + UV[first + 2] * w.b)).sqrMagnitude < 1e-10f, "Interpolated UV changed");
                        Require(normals[index] == Vector3.forward && tangents[index] == new Vector4(1, 0, 0, submesh == 0 ? 1 : -1), "Normal/tangent seam changed");
                        ++checkedVertices;
                    }
                }
                bool rejected = false;
                try { var invalid = BlueprintFogContactMesh.Create(mesh, data, Matrix4x4.zero); UnityEngine.Object.DestroyImmediate(invalid); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "Singular transform accepted");
                Require(BlueprintEditorMeshData.TryReadSurface(mesh, out var after, out error) && after.Positions.SequenceEqual(P), "Source mesh mutated");
                var rootObject = new GameObject("Renderer-only fog source", typeof(MeshFilter), typeof(MeshRenderer));
                var red = new Material(Shader.Find("Standard")); var green = new Material(red);
                red.color = Color.red; green.color = Color.green;
                try
                {
                    rootObject.GetComponent<MeshFilter>().sharedMesh = mesh;
                    rootObject.GetComponent<MeshRenderer>().sharedMaterials = new[] { red, green };
                    Vector3 anchor = new Vector3(2500, 45, -1900);
                    rootObject.transform.SetPositionAndRotation(anchor, Quaternion.Euler(0, 90, 0));
                    rootObject.SetActive(false);
                    using (var source = new BlueprintFogSource(rootObject, anchor))
                    {
                        Require(source.Occupied > 0 && source.Parts.Count == 1, "Source density not prepared");
                        Color[] pigment = source.Pigment.GetPixels();
                        Require(pigment.Any(color => color.a > 0 && color.r > .5f && color.g < .1f) &&
                            pigment.Any(color => color.a > 0 && color.g > .5f && color.r < .1f), "Material slots lost in source pigment");
                        Require(rootObject.GetComponentsInChildren<Collider>(true).Length == 0, "Source preparation created collision");
                        Require(source.Parts[0].Original == mesh && rootObject.GetComponent<MeshFilter>().sharedMesh == mesh, "Source preparation changed the visible mesh");
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(rootObject); UnityEngine.Object.DestroyImmediate(red); UnityEngine.Object.DestroyImmediate(green); }
                ++cases;
            }
            finally { if (dense) UnityEngine.Object.DestroyImmediate(dense); UnityEngine.Object.DestroyImmediate(mesh); }
        }
        var missing = new Mesh { vertices = P, triangles = new[] { 0, 1, 2 } };
        try { Require(!BlueprintEditorMeshData.TryReadSurface(missing, out _, out string error) && !string.IsNullOrEmpty(error), "Missing surface attributes were invented"); }
        finally { UnityEngine.Object.DestroyImmediate(missing); }
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
            passed = true, cases = cases, checkedVertices = checkedVertices, checkedTriangles = checkedTriangles,
            unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(), gameHostTested = false, renderedLookTested = false
        }, true));
        Debug.Log("BuildWorks fog source mesh: readable / unreadable multi-stream / Half-SNorm / submesh-baseVertex / dense source attributes PASSED");
    }

    private static Mesh Source(int mode)
    {
        var mesh = new Mesh { name = "Fog source format " + mode };
        if (mode == 0)
        {
            mesh.vertices = P; mesh.normals = Enumerable.Repeat(Vector3.forward, 6).ToArray(); mesh.uv = UV;
            mesh.tangents = Enumerable.Range(0, 6).Select(i => new Vector4(1, 0, 0, i < 3 ? 1 : -1)).ToArray();
            mesh.subMeshCount = 2; mesh.SetTriangles(new[] { 0, 1, 2 }, 0, false); mesh.SetTriangles(new[] { 0, 1, 2 }, 1, false, 3);
            mesh.RecalculateBounds(); return mesh;
        }
        if (mode == 1)
        {
            mesh.SetVertexBufferParams(6, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.Float32, 4, 1),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 1));
            mesh.SetVertexBufferData(Enumerable.Range(0, 6).Select(i => new PositionNormal { p = P[i], n = Vector3.forward }).ToArray(), 0, 0, 6, 0);
            mesh.SetVertexBufferData(Enumerable.Range(0, 6).Select(i => new TangentUV { t = new Vector4(1, 0, 0, i < 3 ? 1 : -1), uv = UV[i] }).ToArray(), 0, 0, 6, 1);
        }
        else
        {
            mesh.SetVertexBufferParams(6, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
                new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.SNorm16, 4, 1),
                new VertexAttributeDescriptor(VertexAttribute.Tangent, VertexAttributeFormat.SNorm16, 4, 2),
                new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2, 3));
            mesh.SetVertexBufferData(P, 0, 0, 6, 0);
            mesh.SetVertexBufferData(Enumerable.Repeat(new Short4 { z = 32767 }, 6).ToArray(), 0, 0, 6, 1);
            mesh.SetVertexBufferData(Enumerable.Range(0, 6).Select(i => new Short4 { x = 32767, w = (short)(i < 3 ? 32767 : -32767) }).ToArray(), 0, 0, 6, 2);
            mesh.SetVertexBufferData(UV.Select(v => new Half2 { x = (ushort)(v.x == 0 ? 0 : 15360), y = (ushort)(v.y == 0 ? 0 : 15360) }).ToArray(), 0, 0, 6, 3);
        }
        mesh.SetIndexBufferParams(6, IndexFormat.UInt16); mesh.SetIndexBufferData(new ushort[] { 0, 1, 2, 0, 1, 2 }, 0, 0, 6);
        mesh.subMeshCount = 2; mesh.SetSubMesh(0, new SubMeshDescriptor(0, 3)); mesh.SetSubMesh(1, new SubMeshDescriptor(3, 3) { baseVertex = 3 });
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 4); mesh.UploadMeshData(true); return mesh;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report { public bool passed, gameHostTested, renderedLookTested; public int cases, checkedVertices, checkedTriangles; public string unity, graphics; }
}
