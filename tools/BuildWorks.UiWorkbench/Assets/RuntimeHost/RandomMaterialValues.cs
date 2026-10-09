using System;
using System.Collections.Generic;
using UnityEngine;

// Inert host API fixture. Native GetValue parity is checked separately with the installed assembly.
public sealed class RandomMaterialValues : MonoBehaviour
{
    public sealed class VectorVariationProperty
    {
        public List<string> m_propertyNames = new List<string>();
        public Vector4 m_minimum, m_maximum;
        private System.Random random;
        public Vector4 GetValue(int seed)
        {
            random = new System.Random(seed);
            return new Vector4(
                Mathf.Lerp(m_minimum.x, m_maximum.x, (float)random.NextDouble()),
                Mathf.Lerp(m_minimum.y, m_maximum.y, (float)random.NextDouble()),
                Mathf.Lerp(m_minimum.z, m_maximum.z, (float)random.NextDouble()),
                Mathf.Lerp(m_minimum.w, m_maximum.w, (float)random.NextDouble()));
        }
    }
    public List<VectorVariationProperty> m_vectorProperties = new List<VectorVariationProperty>();
}
