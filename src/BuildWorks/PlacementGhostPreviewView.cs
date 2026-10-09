using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal sealed class VisualCloneOwnedAssets : MonoBehaviour
    {
        private readonly List<UnityEngine.Object> assets =
            new List<UnityEngine.Object>();

        internal void Track(UnityEngine.Object asset)
        {
            if (asset) assets.Add(asset);
        }

        private void OnDestroy()
        {
            foreach (UnityEngine.Object asset in assets)
                if (asset) UnityEngine.Object.Destroy(asset);
            assets.Clear();
        }
    }

    internal sealed class PlacementGhostPreviewView : IDisposable
    {
        private readonly List<GameObject> instances = new List<GameObject>();
        private readonly List<GameObject> instanceSources = new List<GameObject>();
        private readonly List<(string, string, bool, bool, int?)> instanceContents = new List<(string, string, bool, bool, int?)>();
        private readonly List<BlueprintItemStandPlacement.Supply> liveSupplies = new List<BlueprintItemStandPlacement.Supply>();
        private readonly MaterialPropertyBlock ghostProperties = new MaterialPropertyBlock();
        private GameObject source;
        private readonly BlueprintBatchedPreview batched = new BlueprintBatchedPreview();
        internal bool HeavyReady => batched.Ready;
        internal int HeavyPreparedCount => batched.PreparedCount;
        internal string HeavyError { get; private set; }

        public void Show(
            GameObject placementGhost,
            IReadOnlyList<PrecisionPlacementSession.TransformSnapshot> plan,
            IReadOnlyList<Piece> pieces,
            int hiddenIndex = 0, Matrix4x4? root = null, Player supplier = null,
            IReadOnlyList<BlueprintItemStandPlacement.Supply> supplies = null, bool attachmentsOnly = false)
        {
            HeavyError = null;
            if (!placementGhost || plan == null || plan.Count < 1)
            {
                Hide();
                return;
            }
            if (plan.Count <= CompositeBlueprintStore.InteractivePartBudget && !attachmentsOnly && source != placementGhost)
            {
                Clear();
                source = placementGhost;
            }
            if (supplier && supplies == null)
            {
                BlueprintItemStandPlacement.SelectSupplies(supplier, plan.Select(pose => pose.ItemStandContents), liveSupplies);
                supplies = liveSupplies;
            }
            if (supplies != null && supplies.Count != plan.Count)
            { Hide(); HeavyError = "Mount supply plan does not match placement plan."; return; }
            if (plan.Count > CompositeBlueprintStore.InteractivePartBudget || attachmentsOnly)
            {
                Hide();
                ghostProperties.Clear();
                Renderer renderer = placementGhost.GetComponentInChildren<Renderer>(true);
                if (renderer) renderer.GetPropertyBlock(ghostProperties);
                try
                {
                    HeavyError = null;
                    batched.Prepare(plan, pieces, placementGhost.layer, supplies);
                    batched.Draw(plan, ghostProperties, placementGhost.layer,
                        hiddenIndex, root ?? Matrix4x4.identity, attachmentsOnly: attachmentsOnly);
                }
                catch (Exception error) when (error is ArgumentException || error is UnityException)
                { HeavyError = error.Message; }
                return;
            }
            ghostProperties.Clear();
            Renderer ghostRenderer = placementGhost.GetComponentInChildren<Renderer>(true);
            if (ghostRenderer) ghostRenderer.GetPropertyBlock(ghostProperties);

            bool hideOne = hiddenIndex >= 0 && hiddenIndex < plan.Count;
            int count = plan.Count;
            while (instances.Count < count)
            {
                instances.Add(null);
                instanceSources.Add(null);
                instanceContents.Add(default);
            }

            for (int index = 0; index < instances.Count; ++index)
            {
                if (index >= count)
                {
                    if (instances[index]) instances[index].SetActive(false);
                    continue;
                }
                int planIndex = index;
                var data = supplies == null ? plan[planIndex].ItemStandContents : supplies[planIndex]?.Contents;
                var contentKey = (data?.Format, data?.Info, data?.ContentsCleared ?? false, planIndex == hiddenIndex,
                    plan[planIndex].MaterialSeed);
                GameObject visualSource = pieces != null && pieces.Count == plan.Count &&
                    pieces[planIndex]
                    ? pieces[planIndex].gameObject
                    : placementGhost;
                if (!instances[index] || instanceSources[index] != visualSource || instanceContents[index] != contentKey)
                {
                    if (instances[index])
                    {
                        instances[index].SetActive(false);
                        UnityEngine.Object.Destroy(instances[index]);
                    }
                    instances[index] = null;
                    try
                    {
                        instances[index] = CreatePreview(
                            visualSource,
                            index,
                            hideOne ? ghostProperties : null,
                            placementGhost.layer, plan[planIndex].MaterialSeed);
                    }
                    catch (Exception error) when (error is ArgumentException || error is UnityException)
                    { Hide(); HeavyError = error.Message; return; }
                    instanceSources[index] = visualSource;
                    instanceContents[index] = contentKey;
                    if (planIndex == hiddenIndex)
                        foreach (Renderer renderer in instances[index].GetComponentsInChildren<Renderer>(true))
                            renderer.enabled = false;
                    BlueprintItemStandPreview.TryAdd(visualSource, instances[index], data, out string warning, out GameObject content);
                    if (warning != null)
                    { HeavyError = warning; instanceSources[index] = null; }
                    if (content && hideOne)
                        foreach (Renderer renderer in content.GetComponentsInChildren<Renderer>(true))
                            renderer.SetPropertyBlock(ghostProperties);
                }
                GameObject preview = instances[index];
                preview.SetActive(true);
                PrecisionPlacementSession.TransformSnapshot sample = plan[planIndex];
                preview.transform.SetPositionAndRotation(sample.Position, sample.Rotation);
                preview.transform.localScale = Vector3.Scale(
                    visualSource.transform.localScale, sample.Scale);
            }
        }

        public void Hide()
        {
            foreach (GameObject instance in instances)
            {
                if (instance) instance.SetActive(false);
            }
        }

        public void Clear()
        {
            foreach (GameObject instance in instances)
            {
                if (instance)
                {
                    instance.SetActive(false);
                    UnityEngine.Object.Destroy(instance);
                }
            }
            instances.Clear();
            instanceSources.Clear();
            instanceContents.Clear();
            liveSupplies.Clear();
            batched.Dispose();
            HeavyError = null;
            source = null;
        }

        public void Dispose() => Clear();

        private static GameObject CreatePreview(
            GameObject visualSource,
            int index,
            MaterialPropertyBlock ghostProperties,
            int layer, int? materialSeed)
        {
            GameObject visual = CreateVisualCloneCore(
                visualSource,
                "BuildWorks_PlacementPreview_" + index,
                layer,
                ghostProperties);
            try
            {
                BlueprintMaterialVariation.ApplyPreview(visualSource, visual, materialSeed);
                if (materialSeed.HasValue && ghostProperties != null)
                {
                    var block = new MaterialPropertyBlock();
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.GetPropertyBlock(block);
                        BlueprintMaterialVariation.ApplyGhostTint(block, ghostProperties);
                        renderer.SetPropertyBlock(block);
                    }
                }
                return visual;
            }
            catch
            {
                visual.SetActive(false);
                UnityEngine.Object.Destroy(visual);
                throw;
            }
        }

        internal static GameObject CreateVisualClone(
            GameObject visualSource,
            string name,
            int layer, int? materialSeed = null)
        {
            GameObject visual = CreateVisualCloneCore(visualSource, name, layer, null);
            try
            {
                BlueprintMaterialVariation.ApplyPreview(visualSource, visual, materialSeed);
                return visual;
            }
            catch
            {
                visual.SetActive(false);
                UnityEngine.Object.Destroy(visual);
                throw;
            }
        }

        private static GameObject CreateVisualCloneCore(
            GameObject visualSource,
            string name,
            int layer,
            MaterialPropertyBlock properties)
        {
            GameObject preview = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = layer
            };
            VisualCloneOwnedAssets owned = preview.AddComponent<VisualCloneOwnedAssets>();
            HashSet<Renderer> lodRenderers = new HashSet<Renderer>();
            HashSet<Renderer> lodZeroRenderers = new HashSet<Renderer>();
            foreach (LODGroup group in visualSource.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] levels = group.GetLODs();
                for (int level = 0; level < levels.Length; ++level)
                {
                    foreach (Renderer renderer in levels[level].renderers)
                    {
                        if (!renderer) continue;
                        lodRenderers.Add(renderer);
                        if (level == 0) lodZeroRenderers.Add(renderer);
                    }
                }
            }
            CopyVisualHierarchy(
                visualSource.transform,
                preview.transform,
                lodRenderers,
                lodZeroRenderers,
                properties,
                owned,
                layer);
            return preview;
        }

        private static void CopyVisualHierarchy(
            Transform source,
            Transform target,
            ISet<Renderer> lodRenderers,
            ISet<Renderer> lodZeroRenderers,
            MaterialPropertyBlock properties,
            VisualCloneOwnedAssets owned,
            int layer)
        {
            MeshRenderer sourceRenderer = source.GetComponent<MeshRenderer>();
            MeshFilter sourceFilter = source.GetComponent<MeshFilter>();
            if (sourceRenderer && sourceFilter && sourceFilter.sharedMesh &&
                (!lodRenderers.Contains(sourceRenderer) || lodZeroRenderers.Contains(sourceRenderer)))
            {
                MeshFilter filter = target.gameObject.AddComponent<MeshFilter>();
                filter.sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer renderer = target.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceRenderer.sharedMaterials;
                renderer.enabled = sourceRenderer.enabled;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (properties != null) renderer.SetPropertyBlock(properties);
            }

            SkinnedMeshRenderer sourceSkinned = source.GetComponent<SkinnedMeshRenderer>();
            if (sourceSkinned && sourceSkinned.sharedMesh &&
                !target.GetComponent<MeshFilter>() &&
                (!lodRenderers.Contains(sourceSkinned) ||
                    lodZeroRenderers.Contains(sourceSkinned)))
            {
                var baked = new Mesh
                {
                    name = "BuildWorks_BakedPreviewMesh",
                    hideFlags = HideFlags.HideAndDontSave
                };
                Mesh previewMesh = baked;
                try
                {
                    // The copied hierarchy applies the renderer transform scale once.
                    sourceSkinned.BakeMesh(baked, true);
                    owned.Track(baked);
                }
                catch (UnityException)
                {
                    UnityEngine.Object.Destroy(baked);
                    previewMesh = sourceSkinned.sharedMesh;
                }
                target.gameObject.AddComponent<MeshFilter>().sharedMesh = previewMesh;
                MeshRenderer renderer = target.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = sourceSkinned.sharedMaterials;
                renderer.enabled = sourceSkinned.enabled;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                if (properties != null) renderer.SetPropertyBlock(properties);
            }

            foreach (Transform sourceChild in source)
            {
                GameObject targetChild = new GameObject(sourceChild.name)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    layer = layer
                };
                Transform child = targetChild.transform;
                child.SetParent(target, false);
                child.localPosition = sourceChild.localPosition;
                child.localRotation = sourceChild.localRotation;
                child.localScale = sourceChild.localScale;
                CopyVisualHierarchy(
                    sourceChild,
                    child,
                    lodRenderers,
                    lodZeroRenderers,
                    properties,
                    owned,
                    layer);
                targetChild.SetActive(sourceChild.gameObject.activeSelf);
            }
        }
    }
}
