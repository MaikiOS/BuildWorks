using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    // One renderer-only source region; the caller owns the asset bundle and construction state.
    internal sealed class BlueprintFogVisual : IDisposable
    {
        private GameObject visual;
        private BlueprintFogSource source;
        private BlueprintFogSimulation simulation;
        private BlueprintFogChannels channelPool;
        private string channelOwner;
        private readonly List<Material> surfaces = new List<Material>();
        private Material volume;
        private readonly Vector4[] trail = new Vector4[24];
        private readonly Vector3 minimum, maximum;
        private Vector2 body;
        private float time;
        internal Vector3 Origin { get; private set; }
        internal GameObject Root => visual;
        internal BlueprintFogSource Source => source;
        internal BlueprintFogSimulation Simulation => simulation;
        internal Bounds VolumeBounds => new Bounds(Origin + (source.Minimum + source.Maximum) * .5f, source.Maximum - source.Minimum);

        internal BlueprintFogVisual(BlueprintFogAssets assets, GameObject prefab, Vector3 position,
            Quaternion rotation, Vector3 relativeScale, int? materialSeed, Vector3 origin, Vector3 actor, Vector2 capsule,
            BlueprintFogChannels channels = null, string owner = null, bool compactRegion = false)
        {
            if (assets == null || !prefab) throw new ArgumentException("Fog prefab/assets missing.");
            try
            {
                visual = PlacementGhostPreviewView.CreateVisualClone(prefab, "BuildWorks_FogVisual", prefab.layer, materialSeed);
                visual.SetActive(false);
                visual.transform.SetPositionAndRotation(position, rotation);
                visual.transform.localScale = Vector3.Scale(prefab.transform.localScale, relativeScale);
                Origin = origin;
                source = new BlueprintFogSource(visual, Origin, compactRegion);
                minimum = source.Minimum - new Vector3(1.2f, 0, 1);
                maximum = source.Maximum + new Vector3(1.2f, .6f, 1);
                Vector3 localActor = LocalActor(actor, out bool nearby);
                body = capsule;
                Vector3Int? grid = compactRegion ? BlueprintFogSimulation.CompactGrid(minimum, maximum) : (Vector3Int?)null;
                long reservedBytes = grid.HasValue ? (long)grid.Value.x * grid.Value.y * grid.Value.z * 80 : BlueprintFogSimulation.FieldBytes;
                BlueprintFogSimulation Create() => new BlueprintFogSimulation(assets, source.Shape, source.Pigment, source.Noise,
                    minimum, maximum, source.Minimum, source.Maximum, localActor, 37, grid);
                if (channels == null) simulation = Create();
                else
                {
                    bool created = false;
                    if (!channels.TryAcquire(owner, reservedBytes, () => { created = true; return Create(); }, out var channel))
                        throw new InvalidOperationException("Fog active owner budget is full; existing matter retained.");
                    if (!created) throw new InvalidOperationException("Fog owner already has a visual.");
                    simulation = channel; channelPool = channels; channelOwner = owner;
                }
                simulation.AdvanceTo(0, localActor, capsule, nearby);
                foreach (BlueprintFogSource.Part part in source.Parts)
                {
                    Material[] originals = part.Renderer.sharedMaterials, replacements = new Material[originals.Length];
                    var properties = new MaterialPropertyBlock(); part.Renderer.GetPropertyBlock(properties);
                    for (int slot = 0; slot < originals.Length; ++slot)
                    {
                        Material original = originals[slot];
                        if (!original) throw new InvalidOperationException("Fog prefab material missing.");
                        var replacement = new Material(assets.Surface) { name = "BuildWorks fog " + original.name, hideFlags = HideFlags.HideAndDontSave };
                        surfaces.Add(replacement); replacements[slot] = replacement;
                        foreach (string property in new[] { "_MainTex", "_BumpMap" })
                            if (original.HasProperty(property))
                            {
                                replacement.SetTexture(property, original.GetTexture(property));
                                replacement.SetTextureScale(property, original.GetTextureScale(property));
                                replacement.SetTextureOffset(property, original.GetTextureOffset(property));
                            }
                        foreach (string property in new[] { "_BumpScale", "_Cutoff" })
                            if (original.HasProperty(property)) replacement.SetFloat(property, original.GetFloat(property));
                        if (original.HasProperty("_Color")) replacement.SetColor("_Color", original.GetColor("_Color"));
                        if (properties.HasColor("_Color")) replacement.SetColor("_Color", properties.GetColor("_Color"));
                        replacement.SetFloat("_Opacity", .84f); replacement.SetFloat("_DenseContact", 1);
                        replacement.SetFloat("_ContactGeometry", 1);
                    }
                    // The known tint was copied; native random-property blocks must not override the fog shader's state.
                    part.Renderer.SetPropertyBlock(null); part.Renderer.sharedMaterials = replacements;
                    part.Renderer.shadowCastingMode = ShadowCastingMode.Off; part.Renderer.receiveShadows = true;
                    part.Filter.sharedMesh = part.Dense;
                }
                volume = new Material(assets.Volume) { name = "BuildWorks v11 volume", hideFlags = HideFlags.HideAndDontSave };
                volume.SetTexture("_Shape", source.Shape); volume.SetTexture("_Albedo", source.Pigment);
                trail[0] = new Vector4(localActor.x, localActor.y, localActor.z, nearby ? 1 : 0);
                Bind(); visual.SetActive(true);
            }
            catch { Dispose(); throw; }
        }

        internal void AdvanceTo(float clock, Vector3 actor, Vector2 capsule, bool emit)
        {
            if (simulation == null) throw new ObjectDisposedException(nameof(BlueprintFogVisual));
            Vector3 local = LocalActor(actor, out bool nearby);
            simulation.AdvanceTo(clock, local, capsule, emit && nearby);
            time = clock; body = capsule; trail[0] = new Vector4(local.x, local.y, local.z, nearby ? 1 : 0);
            Bind();
        }

        internal void RenderVolume(Camera camera, RenderTexture image, RenderTexture target,
            Vector3 beamOrigin, Vector3 beamDirection, float beamStrength)
        {
            if (!camera || camera.orthographic || camera.stereoEnabled || !volume)
                throw new InvalidOperationException("Fog v11 requires a live perspective mono camera.");
            if ((camera.depthTextureMode & DepthTextureMode.Depth) == 0)
                throw new InvalidOperationException("Fog camera must request depth before rendering.");
            BindVolume(volume, "");
            volume.SetVector("_CameraRight", camera.transform.right); volume.SetVector("_CameraUp", camera.transform.up);
            volume.SetVector("_CameraForward", camera.transform.forward);
            volume.SetVector("_FogCameraOrigin", camera.transform.position);
            volume.SetFloat("_Aspect", camera.aspect); volume.SetFloat("_TanHalfFov", Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
            volume.SetFloat("_Density", .85f); volume.SetVector("_BeamOrigin", beamOrigin - Origin);
            volume.SetVector("_BeamDirection", beamDirection.normalized); volume.SetFloat("_BeamStrength", beamStrength);
            volume.SetFloat("_BeamAttenuation", 1);
            Graphics.Blit(image, target, volume);
        }

        internal void BindVolume(Material material, string suffix)
        {
            if (simulation == null || !simulation.Smoke || source == null) throw new ObjectDisposedException(nameof(BlueprintFogVisual));
            material.SetTexture("_Shape" + suffix, source.Shape); material.SetTexture("_Albedo" + suffix, source.Pigment);
            material.SetTexture("_Noise" + suffix, source.Noise); material.SetTexture("_Wake" + suffix, simulation.Smoke);
            material.SetTexture("_MatterFlow" + suffix, simulation.Surface);
            material.SetVector("_VolumeMin" + suffix, source.Minimum); material.SetVector("_VolumeMax" + suffix, source.Maximum);
            material.SetVector("_WakeMin" + suffix, minimum); material.SetVector("_WakeMax" + suffix, maximum);
            material.SetVector("_MatterMin" + suffix, minimum); material.SetVector("_MatterMax" + suffix, maximum);
            material.SetVector("_FogOrigin" + suffix, Origin); material.SetVector("_Body" + suffix, body);
            material.SetFloat("_Clock" + suffix, time); material.SetFloat("_Density" + suffix, .85f); material.SetFloat("_Flow" + suffix, .12f);
            if (suffix.Length == 0)
            {
                material.SetVectorArray("_Trail", trail); material.SetFloat("_MatterEnabled", 1);
                material.SetFloat("_Accumulated", 1); material.SetFloat("_WakeEnabled", 1);
            }
            else material.SetVector("_ActorB", trail[0]);
        }

        private void Bind()
        {
            foreach (Material material in surfaces) Bind(material);
            Bind(volume);
        }
        private void Bind(Material material)
        {
            material.SetTexture("_Noise", source.Noise); material.SetTexture("_MatterFlow", simulation.Surface);
            material.SetVector("_MatterMin", minimum); material.SetVector("_MatterMax", maximum);
            material.SetFloat("_MatterEnabled", 1); material.SetFloat("_Accumulated", 1);
            material.SetVector("_FogOrigin", Origin); material.SetFloat("_Clock", time); material.SetFloat("_Disperse", 1);
            material.SetFloat("_Flow", .12f); material.SetVector("_Body", body); material.SetVectorArray("_Trail", trail);
        }
        private Vector3 LocalActor(Vector3 actor, out bool nearby)
        {
            if (float.IsNaN(actor.x) || float.IsInfinity(actor.x) || float.IsNaN(actor.y) || float.IsInfinity(actor.y) ||
                float.IsNaN(actor.z) || float.IsInfinity(actor.z)) throw new ArgumentException("Non-finite fog actor.");
            Vector3 local = actor - Origin;
            nearby = Mathf.Abs(local.x) <= 32 && Mathf.Abs(local.y) <= 32 && Mathf.Abs(local.z) <= 32;
            // An absent/distant actor must not put world-sized values into half-float fields.
            return nearby ? local : new Vector3(32, 32, 32);
        }

        public void Dispose()
        {
            if (visual) visual.SetActive(false);
            if (channelPool != null) { channelPool.Remove(channelOwner, simulation); channelPool = null; }
            else simulation?.Dispose();
            simulation = null;
            foreach (Material material in surfaces) if (material) UnityEngine.Object.DestroyImmediate(material);
            surfaces.Clear();
            if (volume) UnityEngine.Object.DestroyImmediate(volume); volume = null;
            if (visual) UnityEngine.Object.DestroyImmediate(visual); visual = null;
            source?.Dispose(); source = null;
        }
    }
}
