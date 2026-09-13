using System.Collections.Generic;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed class WorldSelectionHighlight
    {
        private readonly Dictionary<GameObject, Dictionary<Renderer, MaterialPropertyBlock[]>> originalsByObject =
            new Dictionary<GameObject, Dictionary<Renderer, MaterialPropertyBlock[]>>();
        private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
        internal void Set(GameObject piece, bool selected, bool hovered)
        {
            if (!piece) return;
            if (!selected && !hovered)
            {
                if (!originalsByObject.TryGetValue(piece, out var saved)) return;
                foreach (var renderer in saved)
                    if (renderer.Key)
                        for (int index = 0; index < renderer.Value.Length; ++index)
                            renderer.Key.SetPropertyBlock(renderer.Value[index], index);
                originalsByObject.Remove(piece);
                return;
            }
            if (!originalsByObject.TryGetValue(piece, out var originals))
            {
                originals = new Dictionary<Renderer, MaterialPropertyBlock[]>();
                foreach (Renderer renderer in piece.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                    var blocks = new MaterialPropertyBlock[renderer.sharedMaterials.Length];
                    for (int index = 0; index < blocks.Length; ++index)
                    {
                        blocks[index] = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(blocks[index], index);
                    }
                    originals.Add(renderer, blocks);
                }
                originalsByObject.Add(piece, originals);
            }
            Color tint = selected ? new Color(1f, 0.55f, 0.12f, 1f) : new Color(0.6f, 0.8f, 1f, 1f);
            foreach (var entry in originals)
            {
                if (!entry.Key) continue;
                for (int index = 0; index < entry.Value.Length; ++index)
                {
                    // MaterialMan updates the global block for wear/support. An indexed
                    // overlay keeps selection stable and restores the live native block on exit.
                    entry.Key.GetPropertyBlock(properties, index);
                    if (properties.isEmpty)
                        entry.Key.GetPropertyBlock(properties);
                    properties.SetColor("_Color", tint);
                    properties.SetColor("_BaseColor", tint);
                    properties.SetColor("_EmissionColor", tint * 0.4f);
                    entry.Key.SetPropertyBlock(properties, index);
                }
            }
        }

        internal void Clear()
        {
            foreach (GameObject target in new List<GameObject>(originalsByObject.Keys))
                Set(target, false, false);
            originalsByObject.Clear();
        }
    }
}
