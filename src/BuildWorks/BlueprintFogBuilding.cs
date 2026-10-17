using System;
using System.Collections.Generic;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Renderer-only building view; assets and the global field pool are borrowed, construction state is external.
    internal sealed class BlueprintFogBuilding : IDisposable
    {
        private readonly List<BlueprintFogVisual> parts = new List<BlueprintFogVisual>();
        private BlueprintFogVolume volume;
        internal int Count => parts.Count;
        internal Vector3 Origin { get; private set; }
        internal float Clock { get; private set; }
        internal long AtlasBytes => volume?.AtlasBytes ?? 0;

        internal BlueprintFogBuilding(BlueprintFogAssets assets, BlueprintFogChannels channels,
            CompositeBlueprintStore.Blueprint blueprint, Func<string, GameObject> resolve,
            Vector3 origin, Quaternion rotation, Vector3 actor, Vector2 capsule)
        {
            if (assets == null || channels == null || resolve == null || blueprint?.parts == null ||
                blueprint.parts.Count < 1 || blueprint.parts.Count > BlueprintFogAtlas.MaximumOwners)
                throw new ArgumentException("Fog building requires 1..32 available parts and a shared field pool.");
            var prefabs = new GameObject[blueprint.parts.Count]; var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < prefabs.Length; ++i)
            {
                var part = blueprint.parts[i];
                if (part == null || string.IsNullOrWhiteSpace(part.stableId) || !ids.Add(part.stableId) ||
                    !(prefabs[i] = resolve(part.prefabName))) throw new ArgumentException("Fog building contains missing or duplicate parts.");
            }
            Origin = origin;
            string owner = Guid.NewGuid().ToString("N");
            try
            {
                volume = new BlueprintFogVolume(assets);
                for (int i = 0; i < prefabs.Length; ++i)
                {
                    var part = blueprint.parts[i]; Vector3 position = origin + rotation * part.position.ToVector3();
                    parts.Add(new BlueprintFogVisual(assets, prefabs[i], position, rotation * part.rotation.ToQuaternion(),
                        part.PlacementScale(prefabs[i].transform.localScale), part.materialSeed, position, actor, capsule,
                        channels, owner + ":" + i, true, true, origin));
                }
            }
            catch { Dispose(); throw; }
        }

        internal void AdvanceTo(float clock, Vector3 actor, Vector2 capsule, bool emit)
        {
            if (volume == null) throw new ObjectDisposedException(nameof(BlueprintFogBuilding));
            foreach (BlueprintFogVisual part in parts) part.AdvanceTo(clock, actor, capsule, emit);
            Clock = clock;
        }
        internal void Render(Camera camera, RenderTexture image, RenderTexture target, Vector3 beamOrigin, Vector3 beamDirection, float strength)
        {
            if (volume == null) throw new ObjectDisposedException(nameof(BlueprintFogBuilding));
            volume.RenderMany(camera, image, target, parts, beamOrigin, beamDirection, strength);
        }
        public void Dispose()
        {
            volume?.Dispose(); volume = null;
            foreach (BlueprintFogVisual part in parts) part.Dispose(); parts.Clear();
        }
    }
}
