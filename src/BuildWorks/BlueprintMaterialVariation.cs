using System;
using System.Collections.Generic;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintMaterialVariation
    {
        private static readonly int[] GhostTintProperties = {
            Shader.PropertyToID("_Color"), Shader.PropertyToID("_BaseColor"), Shader.PropertyToID("_EmissionColor") };

        internal static void ApplyGhostTint(MaterialPropertyBlock target, MaterialPropertyBlock ghost)
        {
            if (ghost == null) return;
            foreach (int property in GhostTintProperties)
                if (ghost.HasColor(property)) target.SetColor(property, ghost.GetColor(property));
        }

        internal sealed class Values
        {
            internal readonly List<(int Property, Vector4 Value)> Vectors = new List<(int, Vector4)>();

            internal bool HasCallerOverride(MaterialPropertyBlock caller)
            {
                if (caller == null) return false;
                foreach (var vector in Vectors)
                    if (caller.HasProperty(vector.Property)) return true;
                return false;
            }

            internal void Apply(MaterialPropertyBlock block)
            {
                foreach (var vector in Vectors) block.SetVector(vector.Property, vector.Value);
            }

            internal void ApplyInstances(MaterialPropertyBlock block, MaterialPropertyBlock ghost,
                List<Vector4> scratch, int count)
            {
                foreach (var vector in Vectors)
                {
                    Vector4 value = ghost != null && Array.IndexOf(GhostTintProperties, vector.Property) >= 0 &&
                        ghost.HasColor(vector.Property) ? (Vector4)ghost.GetColor(vector.Property) : vector.Value;
                    scratch.Clear();
                    for (int index = 0; index < count; ++index) scratch.Add(value);
                    block.SetVectorArray(vector.Property, scratch);
                }
            }
        }

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
            var block = new MaterialPropertyBlock();
            foreach (var entry in CapturePreview(source, visual, seed.Value))
            {
                entry.Key.GetPropertyBlock(block);
                entry.Value.Apply(block);
                entry.Key.SetPropertyBlock(block);
            }
        }

        internal static Dictionary<Renderer, Values> CapturePreview(GameObject source, GameObject visual, int seed)
        {
            if (seed < 0 || !visual || !IsSupported(source))
                throw new ArgumentException("Unsupported blueprint material variation: " + (source ? source.name : "missing prefab"));
            var result = new Dictionary<Renderer, Values>();
            foreach (var component in source.GetComponentsInChildren<RandomMaterialValues>(true))
            {
                Transform scope = visual.transform;
                var path = new Stack<int>();
                for (Transform node = component.transform; node != source.transform; node = node.parent)
                    path.Push(node.GetSiblingIndex());
                while (path.Count > 0) scope = scope.GetChild(path.Pop());
                Renderer[] renderers = scope.GetComponentsInChildren<Renderer>(true);
                var values = new Values();
                for (int index = 0; index < component.m_vectorProperties.Count; ++index)
                {
                    var original = component.m_vectorProperties[index];
                    // GetValue changes its RNG field; use an owned descriptor, never the prefab's.
                    var property = new RandomMaterialValues.VectorVariationProperty
                    { m_minimum = original.m_minimum, m_maximum = original.m_maximum };
                    Vector4 value = property.GetValue(unchecked(seed + index));
                    foreach (string name in original.m_propertyNames)
                        values.Vectors.Add((Shader.PropertyToID(name), value));
                }
                foreach (Renderer renderer in renderers) result.Add(renderer, values);
            }
            return result;
        }

        private static bool Finite(Vector4 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
            !float.IsNaN(value.w) && !float.IsInfinity(value.w);
    }
}
