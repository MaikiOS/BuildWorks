using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal sealed class WorldSelectionVolumeView : IDisposable
    {
        private GameObject root;
        private Material material;
        private LineRenderer line;
        private readonly Vector3[] points = new Vector3[16];
        private static readonly int[] Corners = { 0, 1, 3, 2, 0, 4, 5, 1, 5, 7, 3, 7, 6, 2, 6, 4 };

        public void Show(Vector3 minimum, Vector3 maximum)
        {
            if (!root)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default");
                if (!shader) return;
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                root = new GameObject("BuildWorks_SelectionVolume") { hideFlags = HideFlags.HideAndDontSave };
                line = root.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.positionCount = points.Length;
                line.startWidth = line.endWidth = .04f;
                line.startColor = line.endColor = new Color(.35f, .85f, .95f, .75f);
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
            }
            for (int i = 0; i < points.Length; ++i)
            {
                int corner = Corners[i];
                points[i] = new Vector3((corner & 1) == 0 ? minimum.x : maximum.x,
                    (corner & 4) == 0 ? minimum.y : maximum.y,
                    (corner & 2) == 0 ? minimum.z : maximum.z);
            }
            line.SetPositions(points);
            root.SetActive(true);
        }

        public void Hide() { if (root) root.SetActive(false); }
        public void Dispose()
        {
            if (root) UnityEngine.Object.Destroy(root);
            if (material) UnityEngine.Object.Destroy(material);
            root = null;
            material = null;
            line = null;
        }
    }
}
