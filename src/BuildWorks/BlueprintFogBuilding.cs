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
        private readonly Func<BlueprintFogVisual>[] preparations;
        private bool preparing, disposed;
        internal bool Ready { get; private set; }
        internal int PreparedParts { get; private set; }
        internal int PlannedParts => preparations.Length;
        internal string PreparationPhase => Ready ? "ready" : parts.Count == 0 ? "parts" : parts[parts.Count - 1].Source?.PreparationPhase ?? "closed";
        internal int Count => parts.Count;
        internal Vector3 Origin { get; private set; }
        internal float Clock { get; private set; }
        internal int QueuedSamples { get { int count = 0; foreach (var part in parts) count += part.LogicalSource?.Inputs.Count ?? 0; return count; } }
        internal int MinimumConfirmedTick { get { int tick = int.MaxValue; foreach (var part in parts) tick = Math.Min(tick, part.Snapshot?.State.ConfirmedTick ?? 0); return tick == int.MaxValue ? 0 : tick; } }
        internal uint LastFlags { get { uint flags = 0; foreach (var part in parts) flags |= part.Snapshot?.LastFlags ?? 0; return flags; } }
        internal long AtlasBytes => published != null ? published.Bytes + channels.SnapshotArena.RenderBytes : volume?.AtlasBytes ?? 0;

        internal BlueprintFogBuilding(BlueprintFogAssets assets, BlueprintFogChannels channels,
            CompositeBlueprintStore.Blueprint blueprint, Func<string, GameObject> resolve,
            Vector3 origin, Quaternion rotation, Vector3 actor, Vector2 capsule, bool deferPreparation = false)
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
            if (deferPreparation && snapshots == null) throw new ArgumentException("Deferred building requires the strict published pool.");
            preparations = new Func<BlueprintFogVisual>[prefabs.Length];
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
                    // Freeze inputs before yielding; later library edits cannot mutate the preparing building.
                    var prefab = prefabs[i]; int? seed = part.materialSeed; string id = owner + ":" + i;
                    preparations[i] = () => new BlueprintFogVisual(assets, prefab, position, facing,
                        scale, seed, fieldOrigin, actor, capsule,
                        channels, id, snapshots == null, true, origin, deferPreparation);
                }
                if (!deferPreparation)
                {
                    foreach (var create in preparations) { parts.Add(create()); ++PreparedParts; }
                    Array.Clear(preparations, 0, preparations.Length); Ready = true;
                }
            }
            catch { Dispose(); throw; }
        }

        internal IEnumerable<object> Prepare()
        {
            if (disposed || preparing || Ready) throw new InvalidOperationException("Fog building preparation is closed or already started.");
            preparing = true; bool complete = false;
            try
            {
                foreach (var create in preparations)
                {
                    var visual = create(); parts.Add(visual);
                    foreach (var step in visual.Prepare()) yield return step;
                    ++PreparedParts;
                    yield return null;
                }
                PrepareRender();
                foreach (var part in parts) part.Root.SetActive(true);
                Array.Clear(preparations, 0, preparations.Length); Ready = complete = true;
            }
            finally { preparing = false; if (!complete) Dispose(); }
        }

        internal void AdvanceTo(float clock, Vector3 actor, Vector2 capsule, bool emit)
        {
            if (!Ready) throw new InvalidOperationException("Fog building is not prepared.");
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
                for (int i = 0; i < parts.Count; ++i)
                    if (parts[i].LogicalSource.Error != null || parts[i].LogicalSource.Inputs.Faulted)
                        throw new InvalidOperationException("Fog part " + i + ": " +
                            (parts[i].LogicalSource.Error ?? "Fog building input history is full; state retained."));
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
            if (!Ready) throw new InvalidOperationException("Fog building is not prepared.");
            if (volume == null && published == null) throw new ObjectDisposedException(nameof(BlueprintFogBuilding));
            if (published != null) published.Render(camera, image, target, parts, beamOrigin, beamDirection, strength, snapshots);
            else volume.RenderMany(camera, image, target, parts, beamOrigin, beamDirection, strength);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Ready = false;
            published?.Dispose(); published = null; volume?.Dispose(); volume = null;
            foreach (BlueprintFogVisual part in parts) part.Dispose(); parts.Clear();
            if (preparations != null) Array.Clear(preparations, 0, preparations.Length);
        }
    }
}
