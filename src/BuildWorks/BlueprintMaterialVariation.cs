using System;
using System.Collections.Generic;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintMaterialVariation
    {
        internal static bool IsSupported(GameObject source)
        {
            if (!source) return false;
            var components = source.GetComponentsInChildren<RandomMaterialValues>(true);
            if (components.Length == 0) return false;
            foreach (var component in components)
            {
                if (component.m_vectorProperties == null || component.m_vectorProperties.Count == 0) return false;
                foreach (var property in component.m_vectorProperties)
                    if (property == null || property.m_propertyNames == null || property.m_propertyNames.Count == 0 ||
                        !Finite(property.m_minimum) || !Finite(property.m_maximum) ||
                        property.m_propertyNames.Exists(string.IsNullOrWhiteSpace)) return false;
                // Overlapping native MaterialMan containers overwrite each other's blocks.
                foreach (var other in components)
                    if (other != component && component.transform.IsChildOf(other.transform)) return false;
            }
            return true;
        }

        internal static void ApplyPreview(GameObject source, GameObject visual, int? seed)
        {
            if (!seed.HasValue) return;
            if (seed.Value < 0 || !visual || !IsSupported(source))
                throw new ArgumentException("Unsupported blueprint material variation: " + (source ? source.name : "missing prefab"));
            var block = new MaterialPropertyBlock();
            foreach (var component in source.GetComponentsInChildren<RandomMaterialValues>(true))
            {
                Transform scope = visual.transform;
                var path = new Stack<int>();
                for (Transform node = component.transform; node != source.transform; node = node.parent)
                    path.Push(node.GetSiblingIndex());
                while (path.Count > 0) scope = scope.GetChild(path.Pop());
                Renderer[] renderers = scope.GetComponentsInChildren<Renderer>(true);
                for (int index = 0; index < component.m_vectorProperties.Count; ++index)
                {
                    var original = component.m_vectorProperties[index];
                    // GetValue changes its RNG field; use an owned descriptor, never the prefab's.
                    var property = new RandomMaterialValues.VectorVariationProperty
                    { m_minimum = original.m_minimum, m_maximum = original.m_maximum };
                    Vector4 value = property.GetValue(unchecked(seed.Value + index));
                    foreach (string name in original.m_propertyNames)
                        foreach (Renderer renderer in renderers)
                        {
                            renderer.GetPropertyBlock(block);
                            block.SetVector(Shader.PropertyToID(name), value);
                            renderer.SetPropertyBlock(block);
                        }
                }
            }
        }

        private static bool Finite(Vector4 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
            !float.IsNaN(value.w) && !float.IsInfinity(value.w);
    }
}
