using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    // One renderer-only source region; the caller owns the asset bundle and construction state.
    internal sealed class BlueprintFogVisual : IDisposable
    {
        private readonly BlueprintFogAssets assets;
        private readonly bool lazyFields;
        private readonly Vector3Int? grid;
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
        internal Vector3 NoiseOffset { get; private set; }
        internal GameObject Root => visual;
        internal BlueprintFogSource Source => source;
        internal BlueprintFogSimulation Simulation => simulation;
        internal Texture Smoke => simulation != null ? (Texture)simulation.Smoke : assets.Empty;
        internal Texture Surface => simulation != null ? (Texture)simulation.Surface : assets.Empty;
        internal Vector3Int FieldSize => simulation?.GridSize ?? Vector3Int.one;
        internal Vector3 FieldMinimum => minimum;
        internal Vector3 FieldMaximum => maximum;
        internal Vector4 ActorRadius => new Vector4(trail[0].x, trail[0].y, trail[0].z, body.x);
        internal float BodyHalfHeight => body.y;
        internal float Clock => time;
        internal Bounds VolumeBounds => new Bounds(Origin + (source.Minimum + source.Maximum) * .5f, source.Maximum - source.Minimum);

        internal BlueprintFogVisual(BlueprintFogAssets assets, GameObject prefab, Vector3 position,
            Quaternion rotation, Vector3 relativeScale, int? materialSeed, Vector3 origin, Vector3 actor, Vector2 capsule,
            BlueprintFogChannels channels = null, string owner = null, bool compactRegion = false, bool lazyFields = false,
            Vector3? noiseOrigin = null)
        {
            if (assets == null || !prefab) throw new ArgumentException("Fog prefab/assets missing.");
            if (lazyFields && (!compactRegion || channels == null || string.IsNullOrWhiteSpace(owner)))
                throw new ArgumentException("Lazy fog requires a compact region and named channel pool.");
            ValidateClockBody(0, capsule); this.assets = assets; this.lazyFields = lazyFields;
            try
            {
                visual = PlacementGhostPreviewView.CreateVisualClone(prefab, "BuildWorks_FogVisual", prefab.layer, materialSeed);
                visual.SetActive(false);
                visual.transform.SetPositionAndRotation(position, rotation);
                visual.transform.localScale = Vector3.Scale(prefab.transform.localScale, relativeScale);
                Origin = origin;
                NoiseOffset = origin - (noiseOrigin ?? origin); BlueprintFogSimulation.Validate(NoiseOffset);
                source = new BlueprintFogSource(visual, Origin, compactRegion);
                minimum = source.Minimum - new Vector3(1.2f, 0, 1);
                maximum = source.Maximum + new Vector3(1.2f, .6f, 1);
                Vector3 localActor = LocalActor(actor, out bool nearby);
                body = capsule;
                grid = compactRegion ? BlueprintFogSimulation.CompactGrid(minimum, maximum) : (Vector3Int?)null;
                channelPool = channels; channelOwner = owner;
                if (!lazyFields)
                {
                    Activate(localActor, 0);
                    simulation.AdvanceTo(0, localActor, capsule, nearby);
                }
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
            if (source == null) throw new ObjectDisposedException(nameof(BlueprintFogVisual));
            ValidateClockBody(clock, capsule);
            Vector3 local = LocalActor(actor, out bool nearby), previous = new Vector3(trail[0].x, trail[0].y, trail[0].z);
            bool contact = lazyFields && emit && nearby && source.MightContact(previous, local, capsule);
            if (contact && simulation == null)
            {
                if (Mathf.FloorToInt(clock / BlueprintFogSimulation.Step + .0001f) -
                    Mathf.FloorToInt(time / BlueprintFogSimulation.Step + .0001f) > 8)
                    throw new ArgumentException("Fog activation exceeds eight fixed steps.");
                Activate(previous, time);
            }
            if (simulation != null)
            {
                simulation.AdvanceTo(clock, local, capsule, emit && nearby, !lazyFields || contact);
                if (lazyFields && !contact && channelPool.TryReleaseRecovered(channelOwner)) simulation = null;
            }
            time = clock; body = capsule; trail[0] = new Vector4(local.x, local.y, local.z, nearby ? 1 : 0);
            Bind();
        }

        private void Activate(Vector3 actor, float clock)
        {
            BlueprintFogSimulation Create(BlueprintFogScratch scratch = null) => new BlueprintFogSimulation(assets, source.Shape, source.Pigment, source.Noise,
                minimum, maximum, source.Minimum, source.Maximum, actor, 37, grid, clock, scratch, NoiseOffset);
            if (channelPool == null) { simulation = Create(); return; }
            Vector3Int size = grid ?? new Vector3Int(BlueprintFogSimulation.Nx, BlueprintFogSimulation.Ny, BlueprintFogSimulation.Nz);
            bool created = false;
            if (!channelPool.TryAcquire(channelOwner, size, scratch => { created = true; return Create(scratch); }, out var channel))
                throw new InvalidOperationException("Fog active owner budget is full; existing matter retained. Grid " + size +
                    ", active owners " + channelPool.Count + ", allocated field bytes " + channelPool.FieldBytes + ".");
            if (!created) throw new InvalidOperationException("Fog owner already has a visual.");
            simulation = channel;
        }

        private void ValidateClockBody(float clock, Vector2 capsule)
        {
            bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
            if (!Finite(clock) || clock < time || clock > 86400 || !Finite(capsule.x) || !Finite(capsule.y) ||
                capsule.x <= 0 || capsule.y < capsule.x || capsule.y > 8)
                throw new ArgumentException("Invalid fog clock or body capsule.");
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
            if (source == null || !Smoke || !Surface) throw new ObjectDisposedException(nameof(BlueprintFogVisual));
            material.SetTexture("_Shape" + suffix, source.Shape); material.SetTexture("_Albedo" + suffix, source.Pigment);
            material.SetTexture("_Noise" + suffix, source.Noise); material.SetTexture("_Wake" + suffix, Smoke);
            material.SetTexture("_MatterFlow" + suffix, Surface);
            material.SetVector("_VolumeMin" + suffix, source.Minimum); material.SetVector("_VolumeMax" + suffix, source.Maximum);
            material.SetVector("_WakeMin" + suffix, minimum); material.SetVector("_WakeMax" + suffix, maximum);
            material.SetVector("_MatterMin" + suffix, minimum); material.SetVector("_MatterMax" + suffix, maximum);
            material.SetVector("_FogOrigin" + suffix, Origin); material.SetVector("_Body" + suffix, body);
            material.SetVector("_NoiseOffset" + suffix, NoiseOffset);
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
            material.SetTexture("_Noise", source.Noise); material.SetTexture("_MatterFlow", Surface);
            material.SetVector("_MatterMin", minimum); material.SetVector("_MatterMax", maximum);
            material.SetFloat("_MatterEnabled", 1); material.SetFloat("_Accumulated", 1);
            material.SetVector("_FogOrigin", Origin); material.SetFloat("_Clock", time); material.SetFloat("_Disperse", 1);
            material.SetVector("_NoiseOffset", NoiseOffset);
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
            if (channelPool != null) { if (simulation != null) channelPool.Remove(channelOwner, simulation); channelPool = null; }
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
