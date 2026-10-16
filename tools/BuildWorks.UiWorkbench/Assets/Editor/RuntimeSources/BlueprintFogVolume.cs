using System;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // One integration pass for the bounded pair; transport ownership stays with each visual.
    internal sealed class BlueprintFogVolume : IDisposable
    {
        private Material material;
        internal BlueprintFogVolume(BlueprintFogAssets assets)
        {
            if (assets == null || !assets.Volume) throw new ArgumentException("Fog volume shader missing.");
            material = new Material(assets.Volume) { name = "BuildWorks fog pair volume", hideFlags = HideFlags.HideAndDontSave };
            material.EnableKeyword("BUILDWORKS_FOG_TWO_SOURCES");
        }

        internal void Render(Camera camera, RenderTexture image, RenderTexture target,
            BlueprintFogVisual first, BlueprintFogVisual second, Vector3 beamOrigin, Vector3 beamDirection, float beamStrength)
        {
            if (!material) throw new ObjectDisposedException(nameof(BlueprintFogVolume));
            if (first == null) throw new ArgumentException("Fog first source missing.");
            if (second == null) { first.RenderVolume(camera, image, target, beamOrigin, beamDirection, beamStrength); return; }
            if (ReferenceEquals(first, second)) throw new ArgumentException("A fog owner cannot be integrated twice.");
            Bounds combined = first.VolumeBounds; combined.Encapsulate(second.VolumeBounds);
            if (combined.size.magnitude > 12) throw new InvalidOperationException("Fog pair span exceeds its integration budget.");
            if (!camera || camera.orthographic || camera.stereoEnabled || (camera.depthTextureMode & DepthTextureMode.Depth) == 0)
                throw new InvalidOperationException("Fog pair requires a perspective mono camera with depth.");
            first.BindVolume(material, ""); second.BindVolume(material, "B");
            material.SetVector("_CameraRight", camera.transform.right); material.SetVector("_CameraUp", camera.transform.up);
            material.SetVector("_CameraForward", camera.transform.forward); material.SetVector("_FogCameraOrigin", camera.transform.position);
            material.SetFloat("_Aspect", camera.aspect); material.SetFloat("_TanHalfFov", Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
            material.SetVector("_BeamOrigin", beamOrigin - first.Origin); material.SetVector("_BeamDirection", beamDirection.normalized);
            material.SetFloat("_BeamStrength", beamStrength); material.SetFloat("_BeamAttenuation", 1);
            Graphics.Blit(image, target, material);
        }

        public void Dispose()
        { if (material) UnityEngine.Object.DestroyImmediate(material); material = null; }
    }
}
