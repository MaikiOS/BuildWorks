using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    // Renderer-only templates: no Piece, network view, inventory or per-part GameObjects.
    internal sealed class BlueprintBatchedPreview : IDisposable
    {
        private sealed class DrawPart
        {
            internal Mesh Mesh;
            internal Material Material;
            internal int Submesh;
            internal Matrix4x4 Local;
            internal bool Attachment;
        }

        private sealed class Template
        {
            internal GameObject Visual;
            internal Vector3 Scale;
            internal readonly List<DrawPart> Parts = new List<DrawPart>();
        }

        private sealed class Batch
        {
            internal readonly Matrix4x4[] Matrices = new Matrix4x4[1023];
            internal int Count;
        }

        private readonly Dictionary<(GameObject, string, string, bool), Template> templates =
            new Dictionary<(GameObject, string, string, bool), Template>();
        private readonly Dictionary<(Mesh, Material, int), Batch> batches =
            new Dictionary<(Mesh, Material, int), Batch>();
        private Template[] parts = Array.Empty<Template>();
        internal bool Ready { get; private set; }
        internal int PreparedCount { get; private set; }
        internal int TemplateCount => templates.Count;

        internal void Prepare(IReadOnlyList<PrecisionPlacementSession.TransformSnapshot> plan,
            IReadOnlyList<Piece> pieces, int layer)
        {
            Ready = false;
            PreparedCount = 0;
            if (plan == null || pieces == null || plan.Count != pieces.Count) return;
            if (parts.Length != plan.Count) parts = new Template[plan.Count];
            int created = 0;
            for (int index = 0; index < plan.Count; ++index)
            {
                if (!pieces[index]) return;
                GameObject source = pieces[index].gameObject;
                BlueprintForeignPartData data = plan[index].ItemStandContents;
                var key = (source, data?.Format, data?.Info, data?.ContentsCleared ?? false);
                if (!templates.TryGetValue(key, out Template template))
                {
                    // Spread expensive unique-prefab preparation across frames.
                    if (created == 2) return;
                    ++created;
                    GameObject visual = PlacementGhostPreviewView.CreateVisualClone(source,
                        "BuildWorks_BatchTemplate", layer);
                    template = new Template { Visual = visual, Scale = source.transform.localScale };
                    try
                    {
                        BlueprintItemStandPreview.TryAdd(source, visual, data, out string warning, out GameObject content);
                        if (warning != null) throw new ArgumentException(warning);
                        foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
                        {
                            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                            if (!renderer || !renderer.enabled || !filter.sharedMesh ||
                                !filter.gameObject.activeInHierarchy) continue;
                            Material[] materials = renderer.sharedMaterials;
                            for (int slot = 0; slot < materials.Length; ++slot)
                            {
                                if (!materials[slot] || filter.sharedMesh.subMeshCount == 0) continue;
                                template.Parts.Add(new DrawPart
                                {
                                    Mesh = filter.sharedMesh,
                                    Material = materials[slot],
                                    Submesh = Math.Min(slot, filter.sharedMesh.subMeshCount - 1),
                                    Local = visual.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix,
                                    Attachment = content && filter.transform.IsChildOf(content.transform)
                                });
                            }
                        }
                        if (template.Parts.Count == 0)
                            throw new ArgumentException("Blueprint part has no supported visible mesh: " + source.name);
                        visual.SetActive(false);
                        templates.Add(key, template);
                    }
                    catch
                    {
                        visual.SetActive(false);
                        UnityEngine.Object.Destroy(visual);
                        throw;
                    }
                }
                parts[index] = template;
                ++PreparedCount;
            }
            Ready = true;
        }

        internal void Draw(IReadOnlyList<PrecisionPlacementSession.TransformSnapshot> plan,
            MaterialPropertyBlock properties, int layer,
            int hiddenIndex, Matrix4x4 root, Camera camera = null)
        {
            if (!Ready || plan.Count != parts.Length) return;
            foreach (Batch batch in batches.Values) batch.Count = 0;
            for (int index = 0; index < plan.Count; ++index)
            {
                var pose = plan[index];
                Matrix4x4 part = root * Matrix4x4.TRS(pose.Position, pose.Rotation,
                    Vector3.Scale(parts[index].Scale, pose.Scale));
                foreach (DrawPart draw in parts[index].Parts)
                {
                    // The native ghost supplies the holder, not its saved item.
                    if (index == hiddenIndex && !draw.Attachment) continue;
                    var key = (draw.Mesh, draw.Material, draw.Submesh);
                    if (!batches.TryGetValue(key, out Batch batch))
                    {
                        batch = new Batch();
                        batches.Add(key, batch);
                    }
                    batch.Matrices[batch.Count++] = part * draw.Local;
                    if (batch.Count == batch.Matrices.Length)
                        Flush(key, batch, properties, layer, camera);
                }
            }
            foreach (var entry in batches)
                Flush(entry.Key, entry.Value, properties, layer, camera);
        }

        private static void Flush((Mesh mesh, Material material, int submesh) key,
            Batch batch, MaterialPropertyBlock properties, int layer, Camera camera)
        {
            if (batch.Count == 0) return;
            if (SystemInfo.supportsInstancing && key.material.enableInstancing)
                Graphics.DrawMeshInstanced(key.mesh, key.submesh, key.material, batch.Matrices,
                    batch.Count, properties, ShadowCastingMode.Off, false, layer, camera,
                    LightProbeUsage.Off);
            else
                for (int index = 0; index < batch.Count; ++index)
                    Graphics.DrawMesh(key.mesh, batch.Matrices[index], key.material, layer, camera,
                        key.submesh, properties, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
            batch.Count = 0;
        }

        public void Dispose()
        {
            foreach (Template template in templates.Values)
                if (template.Visual) UnityEngine.Object.Destroy(template.Visual);
            templates.Clear();
            batches.Clear();
            parts = Array.Empty<Template>();
            Ready = false;
            PreparedCount = 0;
        }
    }
}
