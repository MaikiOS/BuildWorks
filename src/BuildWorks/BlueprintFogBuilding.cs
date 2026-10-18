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
        private BlueprintFogAtlas published;
        private readonly BlueprintFogChannels channels;
        private readonly BlueprintFogChannels.SnapshotChannel[] snapshots;
        internal int Count => parts.Count;
        internal Vector3 Origin { get; private set; }
        internal float Clock { get; private set; }
        internal int QueuedSamples { get { int count = 0; foreach (var part in parts) count += part.LogicalSource?.Inputs.Count ?? 0; return count; } }
        internal int MinimumConfirmedTick { get { int tick = int.MaxValue; foreach (var part in parts) tick = Math.Min(tick, part.Snapshot?.State.ConfirmedTick ?? 0); return tick == int.MaxValue ? 0 : tick; } }
        internal uint LastFlags { get { uint flags = 0; foreach (var part in parts) flags |= part.Snapshot?.LastFlags ?? 0; return flags; } }
        internal long AtlasBytes => published != null ? published.Bytes + channels.SnapshotArena.RenderBytes : volume?.AtlasBytes ?? 0;

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
            this.channels = channels;
            if (channels.SnapshotArena != null) snapshots = new BlueprintFogChannels.SnapshotChannel[prefabs.Length];
            string owner = Guid.NewGuid().ToString("N");
            try
            {
                if (snapshots != null) published = new BlueprintFogAtlas(assets, channels);
                else volume = new BlueprintFogVolume(assets);
                for (int i = 0; i < prefabs.Length; ++i)
                {
                    var part = blueprint.parts[i]; Vector3 position = origin + rotation * part.position.ToVector3();
                    Quaternion facing = rotation * part.rotation.ToQuaternion();
                    Vector3 scale = part.PlacementScale(prefabs[i].transform.localScale), fieldOrigin = position;
                    if (snapshots != null)
                    {
                        if (!BlueprintPrefabMetadataCapture.TryGetLocalVisualBounds(prefabs[i], out Bounds bounds, true))
                            throw new InvalidOperationException("Published fog part bounds missing: " + part.prefabName);
                        // Centre the full v11 domain on geometry, independently from a corner/bottom prefab pivot.
                        fieldOrigin += facing * Vector3.Scale(bounds.center, Vector3.Scale(prefabs[i].transform.localScale, scale)) - Vector3.up * 1.55f;
                    }
                    parts.Add(new BlueprintFogVisual(assets, prefabs[i], position, facing,
                        scale, part.materialSeed, fieldOrigin, actor, capsule,
                        channels, owner + ":" + i, snapshots == null, true, origin));
                }
            }
            catch { Dispose(); throw; }
        }

        internal void AdvanceTo(float clock, Vector3 actor, Vector2 capsule, bool emit)
        {
            if (volume == null && published == null) throw new ObjectDisposedException(nameof(BlueprintFogBuilding));
            foreach (BlueprintFogVisual part in parts) part.AdvanceTo(clock, actor, capsule, emit);
            Pump();
            Clock = clock;
        }
        internal void Pump()
        {
            if (published != null)
            {
                channels.PumpSources();
                foreach (var part in parts) if (part.LogicalSource.Error != null || part.LogicalSource.Inputs.Faulted)
                    throw new InvalidOperationException(part.LogicalSource.Error ?? "Fog building input history is full; state retained.");
            }
        }
        internal void PrepareRender()
        {
            if (published == null) return;
            for (int i = 0; i < parts.Count; ++i) snapshots[i] = parts[i].Snapshot;
            published.PreparePublished(parts, snapshots);
        }
        internal void Render(Camera camera, RenderTexture image, RenderTexture target, Vector3 beamOrigin, Vector3 beamDirection, float strength)
        {
            if (volume == null && published == null) throw new ObjectDisposedException(nameof(BlueprintFogBuilding));
            if (published != null) published.Render(camera, image, target, parts, beamOrigin, beamDirection, strength, snapshots);
            else volume.RenderMany(camera, image, target, parts, beamOrigin, beamDirection, strength);
        }
        public void Dispose()
        {
            published?.Dispose(); published = null; volume?.Dispose(); volume = null;
            foreach (BlueprintFogVisual part in parts) part.Dispose(); parts.Clear();
        }
    }
}
