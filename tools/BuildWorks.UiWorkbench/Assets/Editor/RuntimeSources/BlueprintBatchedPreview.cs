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
            internal bool SignText;
            internal Renderer Renderer;
        }

        private sealed class Template
        {
            internal GameObject Visual;
            internal Vector3 Scale;
            internal readonly List<DrawPart> Parts = new List<DrawPart>();
        }

        private sealed class Batch
        {
            internal Matrix4x4[] Matrices = new Matrix4x4[1];
            internal int Count;
            internal readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
            internal bool PropertiesPrepared;
        }

        private readonly Dictionary<(GameObject, string, string, bool, string, bool), Template> templates =
            new Dictionary<(GameObject, string, string, bool, string, bool), Template>();
        private readonly HashSet<(GameObject, string, string, bool, string, bool)> activeTemplates =
            new HashSet<(GameObject, string, string, bool, string, bool)>();
        private readonly List<(GameObject, string, string, bool, string, bool)> expiredTemplates =
            new List<(GameObject, string, string, bool, string, bool)>();
        private readonly Dictionary<(Mesh, Material, int, BlueprintMaterialVariation.Values), Batch> batches =
            new Dictionary<(Mesh, Material, int, BlueprintMaterialVariation.Values), Batch>();
        private readonly Dictionary<(Template, int), Dictionary<Renderer, BlueprintMaterialVariation.Values>> variants =
            new Dictionary<(Template, int), Dictionary<Renderer, BlueprintMaterialVariation.Values>>();
        private readonly HashSet<(Template, int)> activeVariants = new HashSet<(Template, int)>();
        private readonly List<(Template, int)> expiredVariants = new List<(Template, int)>();
        private readonly List<Vector4> instanceVectors = new List<Vector4>(1023);
        private readonly MaterialPropertyBlock signProperties = new MaterialPropertyBlock();
        private Template[] parts = Array.Empty<Template>();
        private Dictionary<Renderer, BlueprintMaterialVariation.Values>[] partValues =
            Array.Empty<Dictionary<Renderer, BlueprintMaterialVariation.Values>>();
        internal bool Ready { get; private set; }
        internal int PreparedCount { get; private set; }
        internal int TemplateCount => templates.Count;

        internal void Prepare(IReadOnlyList<PrecisionPlacementSession.TransformSnapshot> plan,
            IReadOnlyList<Piece> pieces, int layer,
            IReadOnlyList<BlueprintItemStandPlacement.Supply> supplies = null)
        {
            Ready = false;
            PreparedCount = 0;
            if (plan == null || pieces == null || plan.Count != pieces.Count ||
                supplies != null && supplies.Count != plan.Count) return;
            if (parts.Length != plan.Count)
            {
                parts = new Template[plan.Count];
                partValues = new Dictionary<Renderer, BlueprintMaterialVariation.Values>[plan.Count];
            }
            activeVariants.Clear();
            activeTemplates.Clear();
            bool textAllowed = BlueprintSignPreview.Allowed;
            for (int index = 0; index < plan.Count; ++index)
            {
                if (!pieces[index]) return;
                var data = supplies == null ? plan[index].ItemStandContents : supplies[index]?.Contents;
                var key = (pieces[index].gameObject, data?.Format, data?.Info, data?.ContentsCleared ?? false,
                    plan[index].SignText, plan[index].SignText != null && textAllowed);
                activeTemplates.Add(key);
                if (plan[index].MaterialSeed.HasValue && templates.TryGetValue(key, out Template template))
                    activeVariants.Add((template, plan[index].MaterialSeed.Value));
            }
            expiredTemplates.Clear();
            foreach (var key in templates.Keys)
                if (!activeTemplates.Contains(key)) expiredTemplates.Add(key);
            foreach (var key in expiredTemplates)
            {
                GameObject retired = templates[key].Visual;
                if (retired) { retired.SetActive(false); UnityEngine.Object.Destroy(retired); }
                templates.Remove(key);
            }
            expiredVariants.Clear();
            foreach (var key in variants.Keys)
                if (!activeVariants.Contains(key)) expiredVariants.Add(key);
            foreach (var key in expiredVariants) variants.Remove(key);
            if (expiredVariants.Count > 0 || expiredTemplates.Count > 0) batches.Clear();
            int created = 0, createdVariants = 0;
            for (int index = 0; index < plan.Count; ++index)
            {
                if (!pieces[index]) return;
                GameObject source = pieces[index].gameObject;
                BlueprintForeignPartData data = supplies == null ? plan[index].ItemStandContents : supplies[index]?.Contents;
                var key = (source, data?.Format, data?.Info, data?.ContentsCleared ?? false, plan[index].SignText,
                    plan[index].SignText != null && textAllowed);
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
                        GameObject signText = BlueprintSignPreview.Add(source, visual, plan[index].SignText);
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
                                    Renderer = renderer,
                                    Local = visual.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix,
                                    Attachment = content && filter.transform.IsChildOf(content.transform) || signText && filter.transform.IsChildOf(signText.transform),
                                    SignText = signText && filter.transform.IsChildOf(signText.transform)
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
                partValues[index] = null;
                if (plan[index].MaterialSeed.HasValue)
                {
                    var variantKey = (template, plan[index].MaterialSeed.Value);
                    if (!variants.TryGetValue(variantKey, out var values))
                    {
                        // Seed recipes share geometry; bound their preparation independently.
                        if (createdVariants == 256) return;
                        ++createdVariants;
                        values = BlueprintMaterialVariation.CapturePreview(source, template.Visual, variantKey.Value);
                        variants.Add(variantKey, values);
                    }
                    partValues[index] = values;
                }
                ++PreparedCount;
            }
            Ready = true;
        }

        internal void Draw(IReadOnlyList<PrecisionPlacementSession.TransformSnapshot> plan,
            MaterialPropertyBlock properties, int layer,
            int hiddenIndex, Matrix4x4 root, Camera camera = null, bool attachmentsOnly = false)
        {
            if (!Ready || plan.Count != parts.Length) return;
            bool textAllowed = BlueprintSignPreview.Allowed;
            foreach (Batch batch in batches.Values) { batch.Count = 0; batch.PropertiesPrepared = false; }
            for (int index = 0; index < plan.Count; ++index)
            {
                var pose = plan[index];
                Matrix4x4 part = root * Matrix4x4.TRS(pose.Position, pose.Rotation,
                    Vector3.Scale(parts[index].Scale, pose.Scale));
                foreach (DrawPart draw in parts[index].Parts)
                {
                    if (draw.SignText && !textAllowed) continue;
                    // The native ghost supplies the holder, not its saved item.
                    if ((attachmentsOnly || index == hiddenIndex) && !draw.Attachment) continue;
                    if (draw.SignText)
                    {
                        // Different world scales need different SDF factors, but share the glyph mesh.
                        Matrix4x4 matrix = part * draw.Local;
                        draw.Renderer.SetPropertyBlock(properties);
                        draw.Renderer.GetPropertyBlock(signProperties);
                        BlueprintSignPreview.ApplyScale(draw.Renderer, signProperties, matrix.GetColumn(1).magnitude);
                        Graphics.DrawMesh(draw.Mesh, matrix, draw.Material, layer, camera,
                            draw.Submesh, signProperties, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
                        continue;
                    }
                    BlueprintMaterialVariation.Values values = null;
                    // Supplied mount contents retain their own appearance, like the small preview.
                    if (!draw.Attachment) partValues[index]?.TryGetValue(draw.Renderer, out values);
                    // shortcut: distinct seeds need separate draws; merge after native shader parity is proven.
                    var key = (draw.Mesh, draw.Material, draw.Submesh, values);
                    if (!batches.TryGetValue(key, out Batch batch))
                    {
                        batch = new Batch();
                        batches.Add(key, batch);
                    }
                    if (values != null && !batch.PropertiesPrepared)
                    {
                        // Unity copies an MPB through an owned renderer; the caller block stays untouched.
                        draw.Renderer.SetPropertyBlock(properties);
                        batch.Properties.Clear();
                        draw.Renderer.GetPropertyBlock(batch.Properties);
                        batch.PropertiesPrepared = true;
                    }
                    if (batch.Count == batch.Matrices.Length)
                        Array.Resize(ref batch.Matrices, Math.Min(1023, batch.Matrices.Length * 2));
                    batch.Matrices[batch.Count++] = part * draw.Local;
                    if (batch.Count == 1023)
                        Flush(key, batch, properties, layer, camera);
                }
            }
            foreach (var entry in batches)
                Flush(entry.Key, entry.Value, properties, layer, camera);
        }

        private void Flush((Mesh mesh, Material material, int submesh, BlueprintMaterialVariation.Values values) key,
            Batch batch, MaterialPropertyBlock properties, int layer, Camera camera)
        {
            if (batch.Count == 0) return;
            // A scalar caller override already reserves an array of length one in Unity's MPB.
            if (SystemInfo.supportsInstancing && key.material.enableInstancing &&
                (key.values == null || !key.values.HasCallerOverride(properties)))
            {
                if (key.values != null)
                {
                    key.values.ApplyInstances(batch.Properties, properties, instanceVectors, batch.Count);
                    properties = batch.Properties;
                }
                Graphics.DrawMeshInstanced(key.mesh, key.submesh, key.material, batch.Matrices,
                    batch.Count, properties, ShadowCastingMode.Off, false, layer, camera,
                    LightProbeUsage.Off);
            }
            else
            {
                if (key.values != null)
                {
                    key.values.Apply(batch.Properties);
                    BlueprintMaterialVariation.ApplyGhostTint(batch.Properties, properties);
                    properties = batch.Properties;
                }
                for (int index = 0; index < batch.Count; ++index)
                    Graphics.DrawMesh(key.mesh, batch.Matrices[index], key.material, layer, camera,
                        key.submesh, properties, ShadowCastingMode.Off, false, null, LightProbeUsage.Off);
            }
            batch.Count = 0;
        }

        public void Dispose()
        {
            foreach (Template template in templates.Values)
                if (template.Visual) UnityEngine.Object.Destroy(template.Visual);
            templates.Clear();
            activeTemplates.Clear();
            expiredTemplates.Clear();
            batches.Clear();
            variants.Clear();
            activeVariants.Clear();
            expiredVariants.Clear();
            parts = Array.Empty<Template>();
            partValues = Array.Empty<Dictionary<Renderer, BlueprintMaterialVariation.Values>>();
            Ready = false;
            PreparedCount = 0;
        }
    }
}
