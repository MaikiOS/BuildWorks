using System;
using System.Collections.Generic;
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
        private readonly MaterialPropertyBlock ghostProperties = new MaterialPropertyBlock();
        private GameObject source;

        public void Show(
            GameObject placementGhost,
            IReadOnlyList<PrecisionPlacementSession.TransformSnapshot> plan,
            IReadOnlyList<Piece> pieces,
            int hiddenIndex = 0)
        {
            if (!placementGhost || plan == null || plan.Count < 2)
            {
                Hide();
                return;
            }
            if (source != placementGhost)
            {
                Clear();
                source = placementGhost;
            }

            ghostProperties.Clear();
            Renderer ghostRenderer = placementGhost.GetComponentInChildren<Renderer>(true);
            if (ghostRenderer) ghostRenderer.GetPropertyBlock(ghostProperties);

            bool hideOne = hiddenIndex >= 0 && hiddenIndex < plan.Count;
            int count = plan.Count - (hideOne ? 1 : 0);
            while (instances.Count < count)
            {
                instances.Add(null);
                instanceSources.Add(null);
            }

            for (int index = 0; index < instances.Count; ++index)
            {
                if (index >= count)
                {
                    if (instances[index]) instances[index].SetActive(false);
                    continue;
                }
                int planIndex = hideOne && index >= hiddenIndex ? index + 1 : index;
                GameObject visualSource = pieces != null && pieces.Count == plan.Count &&
                    pieces[planIndex]
                    ? pieces[planIndex].gameObject
                    : placementGhost;
                if (!instances[index] || instanceSources[index] != visualSource)
                {
                    if (instances[index])
                    {
                        instances[index].SetActive(false);
                        UnityEngine.Object.Destroy(instances[index]);
                    }
                    instances[index] = CreatePreview(
                        visualSource,
                        index,
                        hideOne ? ghostProperties : null,
                        placementGhost.layer);
                    instanceSources[index] = visualSource;
                }
                GameObject preview = instances[index];
                preview.SetActive(true);
                PrecisionPlacementSession.TransformSnapshot sample = plan[planIndex];
                preview.transform.SetPositionAndRotation(sample.Position, sample.Rotation);
                preview.transform.localScale = Vector3.Scale(
                    visualSource.transform.lossyScale, sample.Scale);
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
            source = null;
        }

        public void Dispose() => Clear();

        private static GameObject CreatePreview(
            GameObject visualSource,
            int index,
            MaterialPropertyBlock ghostProperties,
            int layer)
        {
            return CreateVisualClone(
                visualSource,
                "BuildWorks_PlacementPreview_" + index,
                layer,
                ghostProperties);
        }

        internal static GameObject CreateVisualClone(
            GameObject visualSource,
            string name,
            int layer)
        {
            return CreateVisualClone(visualSource, name, layer, null);
        }

        private static GameObject CreateVisualClone(
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
                    sourceSkinned.BakeMesh(baked);
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
