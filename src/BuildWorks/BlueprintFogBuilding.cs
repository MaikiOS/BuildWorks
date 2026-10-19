using System;
using System.Collections.Generic;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Renderer-only building view; assets and the global field pool are borrowed, construction state is external.
    internal sealed class BlueprintFogBuilding : IDisposable
    {
        private readonly List<BlueprintFogVisual> parts = new List<BlueprintFogVisual>();
        private readonly List<BlueprintFogVisual> visibleParts = new List<BlueprintFogVisual>();
        private readonly BlueprintFogVisual[] indexedParts;
        private readonly bool[] removed, visible, prepared;
        private BlueprintFogVolume volume;
        private BlueprintFogAtlas published;
        private readonly BlueprintFogChannels channels;
        private readonly List<BlueprintFogChannels.SnapshotChannel> snapshots;
        private readonly Func<BlueprintFogVisual>[] preparations;
        private bool preparing, disposed;
        private bool renderPrepared;
        private int preparingIndex = -1, removedCount;
        internal bool Ready { get; private set; }
        internal int PreparedParts { get; private set; }
        internal int PlannedParts => preparations.Length - removedCount;
        internal string PreparationPhase => Ready ? "ready" : parts.Count == 0 ? "parts" : parts[parts.Count - 1].Source?.PreparationPhase ?? "closed";
        internal int Count => parts.Count;
        internal int VisibleCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < visible.Length; ++i)
                    if (!removed[i] && visible[i] && prepared[i] && indexedParts[i] != null) ++count;
                return count;
            }
        }
        internal Vector3 Origin { get; private set; }
        internal float Clock { get; private set; }
        internal int QueuedSamples { get { int count = 0; foreach (var part in parts) count += part.LogicalSource?.Inputs.Count ?? 0; return count; } }
        internal int MinimumConfirmedTick { get { int tick = int.MaxValue; foreach (var part in parts) tick = Math.Min(tick, part.Snapshot?.State.ConfirmedTick ?? 0); return tick == int.MaxValue ? 0 : tick; } }
        internal uint LastFlags { get { uint flags = 0; foreach (var part in parts) flags |= part.Snapshot?.LastFlags ?? 0; return flags; } }
        internal long AtlasBytes => published != null ? published.Bytes + channels.SnapshotArena.RenderBytes : volume?.AtlasBytes ?? 0;

        internal BlueprintFogBuilding(BlueprintFogAssets assets, BlueprintFogChannels channels,
            CompositeBlueprintStore.Blueprint blueprint, Func<string, GameObject> resolve,
            Vector3 origin, Quaternion rotation, Vector3 actor, Vector2 capsule, bool deferPreparation = false,
            IReadOnlyList<(Vector3 Position, Quaternion Rotation, Vector3 Scale)> worldPoses = null)
        {
            if (assets == null || channels == null || resolve == null || blueprint?.parts == null ||
                blueprint.parts.Count < 1 || blueprint.parts.Count > BlueprintFogAtlas.MaximumOwners)
                throw new ArgumentException("Fog building requires 1..32 available parts and a shared field pool.");
            if (worldPoses != null && worldPoses.Count != blueprint.parts.Count)
                throw new ArgumentException("Fog world poses must match the frozen blueprint parts.");
            var prefabs = new GameObject[blueprint.parts.Count]; var ids = new HashSet<string>(StringComparer.Ordinal);
            indexedParts = new BlueprintFogVisual[prefabs.Length]; removed = new bool[prefabs.Length];
            visible = new bool[prefabs.Length]; prepared = new bool[prefabs.Length];
            for (int i = 0; i < prefabs.Length; ++i)
            {
                var part = blueprint.parts[i];
                if (part == null || string.IsNullOrWhiteSpace(part.stableId) || !ids.Add(part.stableId) ||
                    !(prefabs[i] = resolve(part.prefabName))) throw new ArgumentException("Fog building contains missing or duplicate parts.");
            }
            Origin = origin;
            this.channels = channels;
            if (channels.SnapshotArena != null) snapshots = new List<BlueprintFogChannels.SnapshotChannel>(prefabs.Length);
            if (deferPreparation && snapshots == null) throw new ArgumentException("Deferred building requires the strict published pool.");
            preparations = new Func<BlueprintFogVisual>[prefabs.Length];
            string owner = Guid.NewGuid().ToString("N");
            try
            {
                if (snapshots != null) published = new BlueprintFogAtlas(assets, channels);
                else volume = new BlueprintFogVolume(assets);
                for (int i = 0; i < prefabs.Length; ++i)
                {
                    var part = blueprint.parts[i]; Vector3 position, scale; Quaternion facing;
                    if (worldPoses != null)
                    { var pose = worldPoses[i]; position = pose.Position; facing = pose.Rotation; scale = pose.Scale; }
                    else
                    {
                        position = origin + rotation * part.position.ToVector3(); facing = rotation * part.rotation.ToQuaternion();
                        scale = part.PlacementScale(prefabs[i].transform.localScale);
                    }
                    if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsNaN(position.z) ||
                        Mathf.Abs(position.x) > 1000000 || Mathf.Abs(position.y) > 1000000 || Mathf.Abs(position.z) > 1000000)
                        throw new ArgumentException("Fog world position is nonfinite or outside the world coordinate bound.");
                    BlueprintFogSimulation.Validate(scale);
                    float norm = Quaternion.Dot(facing, facing);
                    if (float.IsNaN(norm) || float.IsInfinity(norm) || Mathf.Abs(norm - 1) > .001f ||
                        scale.x <= 0 || scale.y <= 0 || scale.z <= 0)
                        throw new ArgumentException("Fog world pose is not a finite rigid rotation and positive scale.");
                    Vector3 fieldOrigin = position; visible[i] = true;
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
                    for (int i = 0; i < preparations.Length; ++i)
                    { indexedParts[i] = preparations[i](); parts.Add(indexedParts[i]); prepared[i] = true; ++PreparedParts; }
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
                for (int i = 0; i < preparations.Length; ++i)
                {
                    if (disposed) yield break;
                    if (removed[i]) continue;
                    preparingIndex = i;
                    var visual = preparations[i](); indexedParts[i] = visual; parts.Add(visual);
                    using (var pending = visual.Prepare().GetEnumerator())
                        while (!disposed && !removed[i] && pending.MoveNext()) yield return pending.Current;
                    preparingIndex = -1;
                    if (disposed) yield break;
                    if (removed[i]) { RetirePart(i); continue; }
                    prepared[i] = true; ++PreparedParts;
                    yield return null;
                }
                PrepareRender();
                for (int i = 0; i < indexedParts.Length; ++i)
                    if (indexedParts[i] != null) indexedParts[i].Root.SetActive(visible[i]);
                Array.Clear(preparations, 0, preparations.Length); Ready = complete = true;
            }
            finally { preparingIndex = -1; preparing = false; if (!complete) Dispose(); }
        }

        internal void SetPartVisible(int index, bool value)
        {
            CheckPart(index);
            if (removed[index]) { if (value) throw new InvalidOperationException("Retired fog part cannot be shown again."); return; }
            if (visible[index] == value) return;
            visible[index] = value; renderPrepared = false;
            if (indexedParts[index]?.Root) indexedParts[index].Root.SetActive(Ready && value);
        }

        internal void RemovePart(int index)
        {
            CheckPart(index);
            if (removed[index]) return;
            removed[index] = true; visible[index] = false; ++removedCount; renderPrepared = false;
            if (indexedParts[index]?.Root) indexedParts[index].Root.SetActive(false);
            // A suspended preparation owns its enumerator; close it at the next yield boundary.
            if (preparingIndex != index) RetirePart(index);
        }

        private void CheckPart(int index)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogBuilding));
            if (index < 0 || index >= indexedParts.Length) throw new ArgumentOutOfRangeException(nameof(index));
        }

        private void RetirePart(int index)
        {
            var part = indexedParts[index];
            if (part != null) { part.Dispose(); parts.Remove(part); indexedParts[index] = null; }
            preparations[index] = null;
            if (prepared[index]) { prepared[index] = false; --PreparedParts; }
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
                renderPrepared = false;
                for (int i = 0; i < parts.Count; ++i)
                    if (parts[i].LogicalSource.Error != null || parts[i].LogicalSource.Inputs.Faulted)
                        throw new InvalidOperationException("Fog part " + i + ": " +
                            (parts[i].LogicalSource.Error ?? "Fog building input history is full; state retained."));
            }
        }
        internal void PrepareRender()
        {
            if (disposed) return;
            visibleParts.Clear(); snapshots?.Clear();
            for (int i = 0; i < indexedParts.Length; ++i)
                if (!removed[i] && visible[i] && prepared[i])
                { visibleParts.Add(indexedParts[i]); snapshots?.Add(indexedParts[i].Snapshot); }
            if (published != null)
            {
                if (visibleParts.Count == 0) published.ClearPublished();
                else published.PreparePublished(visibleParts, snapshots);
            }
            renderPrepared = true;
        }
        internal void Render(Camera camera, RenderTexture image, RenderTexture target, Vector3 beamOrigin, Vector3 beamDirection, float strength)
        {
            if (!Ready) throw new InvalidOperationException("Fog building is not prepared.");
            if (volume == null && published == null) throw new ObjectDisposedException(nameof(BlueprintFogBuilding));
            if (!renderPrepared) PrepareRender();
            if (visibleParts.Count == 0) { Graphics.Blit(image, target); return; }
            if (published != null) published.Render(camera, image, target, visibleParts, beamOrigin, beamDirection, strength, snapshots);
            else volume.RenderMany(camera, image, target, visibleParts, beamOrigin, beamDirection, strength);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Ready = false;
            published?.Dispose(); published = null; volume?.Dispose(); volume = null;
            foreach (BlueprintFogVisual part in parts) part.Dispose(); parts.Clear();
            visibleParts.Clear(); snapshots?.Clear();
            if (indexedParts != null) Array.Clear(indexedParts, 0, indexedParts.Length);
            if (preparations != null) Array.Clear(preparations, 0, preparations.Length);
        }
    }
}
