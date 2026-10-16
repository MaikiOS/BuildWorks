using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintFogContactMesh
    {
        private struct Vertex
        {
            internal Vector3 Position, Normal;
            internal Vector4 Tangent;
            internal Vector2 UV;
            internal Color Weights;
        }

        internal static Mesh Create(Mesh source, BlueprintEditorMeshData.SurfaceData data, Matrix4x4 meshToMetres)
        {
            if (!source || data == null) throw new ArgumentException("Fog contact source missing.");
            for (int i = 0; i < 16; ++i)
                if (float.IsNaN(meshToMetres[i]) || float.IsInfinity(meshToMetres[i])) throw new ArgumentException("Non-finite fog transform.");
            float determinant = meshToMetres.determinant;
            if (determinant == 0 || float.IsNaN(determinant) || float.IsInfinity(determinant)) throw new ArgumentException("Singular fog transform.");
            const float spacingSquared = .16f * .16f;
            float maxScale = Mathf.Max(meshToMetres.MultiplyVector(Vector3.right).magnitude,
                Mathf.Max(meshToMetres.MultiplyVector(Vector3.up).magnitude, meshToMetres.MultiplyVector(Vector3.forward).magnitude));
            float localSpacingSquared = spacingSquared / (maxScale * maxScale);
            var positions = new List<Vector3>(); var normals = new List<Vector3>(); var tangents = new List<Vector4>();
            var uv = new List<Vector2>(); var aCorners = new List<Vector3>(); var bCorners = new List<Vector3>();
            var cCorners = new List<Vector3>(); var weights = new List<Color>(); var submeshes = new List<int[]>();
            double sourceArea = 0, resultArea = 0;
            foreach (int[] triangles in data.Submeshes)
            {
                var indices = new List<int>();
                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    int ia = triangles[triangle], ib = triangles[triangle + 1], ic = triangles[triangle + 2];
                    Vector3 sourceNormal = Vector3.Cross(data.Positions[ib] - data.Positions[ia], data.Positions[ic] - data.Positions[ia]);
                    sourceArea += sourceNormal.magnitude * .5;
                    Vertex Read(int index, Color bary) => new Vertex { Position = data.Positions[index], Normal = data.Normals[index],
                        Tangent = data.Tangents[index], UV = data.UV[index], Weights = bary };
                    void Emit(Vertex a, Vertex b, Vertex c, int recursion)
                    {
                        if (recursion >= 24 || positions.Count >= 500000) throw new InvalidOperationException("Fog contact subdivision exceeds its budget.");
                        bool Long(Vector3 edge) => edge.sqrMagnitude > localSpacingSquared || meshToMetres.MultiplyVector(edge).sqrMagnitude > spacingSquared;
                        int split = (Long(a.Position - b.Position) ? 1 : 0) | (Long(b.Position - c.Position) ? 2 : 0) | (Long(c.Position - a.Position) ? 4 : 0);
                        if (split != 0)
                        {
                            Vertex ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a); int next = recursion + 1;
                            switch (split)
                            {
                                case 1: Emit(a, ab, c, next); Emit(ab, b, c, next); break;
                                case 2: Emit(a, b, bc, next); Emit(a, bc, c, next); break;
                                case 4: Emit(a, b, ca, next); Emit(ca, b, c, next); break;
                                case 3: Emit(ab, b, bc, next); Emit(a, ab, c, next); Emit(ab, bc, c, next); break;
                                case 6: Emit(b, bc, ca, next); Emit(b, ca, a, next); Emit(bc, c, ca, next); break;
                                case 5: Emit(a, ab, ca, next); Emit(ab, b, c, next); Emit(ca, ab, c, next); break;
                                default: Emit(a, ab, ca, next); Emit(ab, b, bc, next); Emit(ca, bc, c, next); Emit(ab, bc, ca, next); break;
                            }
                            return;
                        }
                        if (positions.Count + 3 > 500000) throw new InvalidOperationException("Fog contact subdivision exceeds its vertex budget.");
                        Add(a); Add(b); Add(c);
                        Vector3 normal = Vector3.Cross(b.Position - a.Position, c.Position - a.Position);
                        if (Vector3.Dot(normal, sourceNormal) < -1e-8f) throw new InvalidOperationException("Fog subdivision reversed winding.");
                        resultArea += normal.magnitude * .5;
                        void Add(Vertex vertex)
                        {
                            indices.Add(positions.Count); positions.Add(vertex.Position); normals.Add(vertex.Normal); tangents.Add(vertex.Tangent);
                            uv.Add(vertex.UV); aCorners.Add(data.Positions[ia]); bCorners.Add(data.Positions[ib]); cCorners.Add(data.Positions[ic]); weights.Add(vertex.Weights);
                        }
                    }
                    Emit(Read(ia, new Color(1, 0, 0, 1)), Read(ib, new Color(0, 1, 0, 1)), Read(ic, new Color(0, 0, 1, 1)), 0);
                }
                submeshes.Add(indices.ToArray());
            }
            if (double.IsNaN(sourceArea) || double.IsInfinity(sourceArea) || double.IsNaN(resultArea) || double.IsInfinity(resultArea) ||
                Math.Abs(resultArea - sourceArea) >= Math.Max(.0001, sourceArea * .0001))
                throw new InvalidOperationException("Fog subdivision changed surface area.");
            var mesh = new Mesh { name = "BuildWorks fog contact " + source.name, indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.HideAndDontSave };
            try
            {
                mesh.SetVertices(positions); mesh.SetNormals(normals); mesh.SetTangents(tangents); mesh.SetUVs(0, uv);
                mesh.SetUVs(1, aCorners); mesh.SetUVs(2, bCorners); mesh.SetUVs(3, cCorners); mesh.SetColors(weights);
                mesh.subMeshCount = submeshes.Count;
                for (int i = 0; i < submeshes.Count; ++i) mesh.SetTriangles(submeshes[i], i, false);
                Bounds bounds = source.bounds; bounds.Expand(2); mesh.bounds = bounds; return mesh;
            }
            catch { UnityEngine.Object.DestroyImmediate(mesh); throw; }
        }

        private static Vertex Midpoint(Vertex a, Vertex b) => new Vertex {
            Position = (a.Position + b.Position) * .5f, Normal = (a.Normal + b.Normal) * .5f,
            Tangent = (a.Tangent + b.Tangent) * .5f, UV = (a.UV + b.UV) * .5f, Weights = (a.Weights + b.Weights) * .5f
        };
    }
}
