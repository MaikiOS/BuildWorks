using System;
using System.Collections.Generic;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Keep native ghost validation, materials and ZDO untouched; own only its visible copy.
    internal sealed class NativePlacementMaterialPreview : IDisposable
    {
        private GameObject source, ghost, visual;
        private int? seed;
        private Renderer[] hidden;
        private bool[] previousFlags;
        private readonly List<(Renderer Native, Renderer Copy, BlueprintMaterialVariation.Values Values, bool WasOff, int Slots)> renderers =
            new List<(Renderer, Renderer, BlueprintMaterialVariation.Values, bool, int)>();
        private readonly MaterialPropertyBlock nativeBlock = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock copyBlock = new MaterialPropertyBlock();

        internal bool Matches(GameObject prefab, GameObject target, int? value) =>
            visual && visual.activeSelf && source == prefab && ghost == target && seed == value;

        internal void Show(GameObject prefab, GameObject target, int? value)
        {
            if (!value.HasValue || !target || !target.activeInHierarchy) { Clear(); return; }
            try
            {
                if (!Matches(prefab, target, value))
                {
                    Clear();
                    if (!prefab || prefab == target || !BlueprintPlacementFrame.HasRigidParent(target.transform))
                        throw new ArgumentException("Material preview requires a distinct prefab and a rigid ghost frame.");
                    source = prefab; ghost = target; seed = value;
                    visual = PlacementGhostPreviewView.CreateVisualClone(target, "BuildWorks_NativeMaterialPreview", target.layer);
                    var recipes = BlueprintMaterialVariation.CapturePreview(prefab, visual, value.Value);
                    foreach (Renderer copy in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        Transform node = target.transform;
                        var path = new Stack<int>();
                        for (Transform child = copy.transform; child != visual.transform; child = child.parent)
                            path.Push(child.GetSiblingIndex());
                        while (path.Count > 0) node = node.GetChild(path.Pop());
                        Renderer original = node.GetComponent<MeshRenderer>();
                        if (!original) original = node.GetComponent<SkinnedMeshRenderer>();
                        if (!original) throw new ArgumentException("Material preview renderer hierarchy differs from native ghost.");
                        recipes.TryGetValue(copy, out var recipe);
                        renderers.Add((original, copy, recipe, original.forceRenderingOff, original.sharedMaterials.Length));
                    }
                    if (renderers.Count == 0) throw new ArgumentException("Material preview has no renderers.");
                    hidden = target.GetComponentsInChildren<Renderer>(true);
                    previousFlags = new bool[hidden.Length];
                    for (int index = 0; index < hidden.Length; ++index)
                    { previousFlags[index] = hidden[index].forceRenderingOff; hidden[index].forceRenderingOff = true; }
                }
                visual.transform.SetPositionAndRotation(target.transform.position, target.transform.rotation);
                visual.transform.localScale = target.transform.lossyScale;
                foreach (var pair in renderers)
                {
                    if (!pair.Native || !pair.Copy) throw new ArgumentException("Native material preview renderer was removed.");
                    pair.Native.GetPropertyBlock(nativeBlock);
                    pair.Copy.SetPropertyBlock(nativeBlock);
                    pair.Copy.GetPropertyBlock(copyBlock);
                    pair.Values?.Apply(copyBlock);
                    BlueprintMaterialVariation.ApplyGhostTint(copyBlock, nativeBlock);
                    pair.Copy.SetPropertyBlock(copyBlock);
                    // Indexed overrides also shadow native MaterialMan's global values.
                    for (int slot = 0; slot < pair.Slots; ++slot)
                    {
                        pair.Native.GetPropertyBlock(nativeBlock, slot);
                        pair.Copy.SetPropertyBlock(nativeBlock.isEmpty ? null : nativeBlock, slot);
                    }
                    pair.Copy.enabled = pair.Native.enabled;
                    pair.Copy.forceRenderingOff = pair.WasOff;
                }
            }
            catch { Clear(); throw; }
        }

        internal void Clear()
        {
            if (hidden != null)
                for (int index = 0; index < hidden.Length; ++index)
                    if (hidden[index]) hidden[index].forceRenderingOff = previousFlags[index];
            if (visual) { visual.SetActive(false); UnityEngine.Object.Destroy(visual); }
            source = ghost = visual = null; seed = null; hidden = null; previousFlags = null;
            renderers.Clear();
        }

        public void Dispose() => Clear();
    }
}
