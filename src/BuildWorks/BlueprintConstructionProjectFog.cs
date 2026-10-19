using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Store = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    // Presentation borrows authoritative snapshots; it never writes a plan, journal, inventory or world object.
    internal sealed class BlueprintConstructionProjectFog : MonoBehaviour
    {
        private sealed class Entry
        {
            internal Store.Record Record;
            internal Store.Part[] Parts;
            internal BlueprintFogBuilding Building;
            internal IEnumerator<object> Preparation;
            internal string Error;
            internal bool Admitted;
            internal float Clock;
        }
        private readonly Dictionary<Guid, Entry> entries = new Dictionary<Guid, Entry>();
        private readonly List<BlueprintFogVisual> owners = new List<BlueprintFogVisual>(32);
        private readonly List<BlueprintFogChannels.SnapshotChannel> snapshots = new List<BlueprintFogChannels.SnapshotChannel>(32);
        private BlueprintConstructionProjectRuntime runtime;
        private Action<Exception> log;
        private BlueprintFogAssets assets;
        private BlueprintFogChannels channels;
        private BlueprintFogAtlas atlas;
        private Camera view;
        private DepthTextureMode oldDepth, ownedDepth;
        private string failure;
        private bool closing;
        private bool layoutDirty = true;
        internal int RenderFrames { get; private set; }
        internal int VisibleParts => entries.Values.Sum(entry => entry.Building?.VisibleCount ?? 0);
        internal long FieldBytes => channels?.FieldBytes ?? 0;
        internal long RenderBytes => (atlas?.Bytes ?? 0) + (channels?.SnapshotArena?.RenderBytes ?? 0);
        internal int ActiveSources => channels?.Count ?? 0;
        internal int Assets => assets?.AssetCount ?? 0;
        internal int Kernels => assets?.KernelCount ?? 0;
        internal int BlockCapacity => channels?.SnapshotArena?.BlockCapacity ?? 0;
        internal int ResidentCapacity => channels?.SnapshotArena?.ResidentCapacity ?? 0;
        internal bool Has(Guid id) => entries.ContainsKey(id);
        internal string Error(Guid id) => failure ?? (entries.TryGetValue(id, out var entry) ? entry.Error : null);
        internal BlueprintFogBuilding Building(Guid id) => entries.TryGetValue(id, out var entry) ? entry.Building : null;
#if BUILDWORKS_TEST_STAND
        internal BlueprintFogChannels QaChannels => channels;
#endif

        internal void Initialize(BlueprintConstructionProjectRuntime owner, Action<Exception> report)
        { runtime = owner; log = report; view = GetComponent<Camera>(); }

        internal void Add(Store.Record record)
        {
            if (closing || entries.ContainsKey(record.Id)) return;
            var entry = new Entry { Record = record };
            entries.Add(record.Id, entry);
            try
            {
                entry.Parts = record.Parts.Where(part => runtime.ReadPart(record.Id, part.Id)?.Phase != Journal.Committed).ToArray();
                if (entry.Parts.Length == 0) return;
                int reserved = entries.Values.Sum(item => item.Building?.PlannedParts ?? 0) + (channels?.RetiringSources ?? 0);
                if (entry.Parts.Length > BlueprintFogAtlas.MaximumOwners - reserved)
                    throw new InvalidOperationException("Fog shared owner capacity is full; project and existing visuals retained.");
                if (failure != null) throw new InvalidOperationException(failure);
                EnsureResources();
                var blueprint = record.Blueprint;
                blueprint.parts = entry.Parts.Select(part => blueprint.parts[part.SourceIndex]).ToList();
                var poses = entry.Parts.Select(part =>
                {
                    var p = part.WorldPose.Values;
                    return (new Vector3(p[0], p[1], p[2]), new Quaternion(p[3], p[4], p[5], p[6]), new Vector3(p[7], p[8], p[9]));
                }).ToArray();
                float[] root = record.RootPose.Values;
                Body(out var actor, out var capsule);
                entry.Building = new BlueprintFogBuilding(assets, channels, blueprint, name => ZNetScene.instance.GetPrefab(name),
                    new Vector3(root[0], root[1], root[2]), Quaternion.identity, actor, capsule, deferPreparation: true,
                    worldPoses: poses, sharedDisplay: true);
                Refresh(record.Id);
                entry.Preparation = entry.Building.Prepare().GetEnumerator();
            }
            catch (Exception error)
            {
                if (assets != null && (channels == null || atlas == null)) Fail(error);
                FailEntry(entry, error);
            }
        }

        internal void Refresh(Guid id)
        {
            if (!entries.TryGetValue(id, out var entry) || entry.Building == null || entry.Error != null) return;
            try
            {
                for (int i = 0; i < entry.Parts.Length; ++i)
                {
                    int phase = runtime.ReadPart(id, entry.Parts[i].Id)?.Phase ?? 0;
                    if (phase == Journal.Committed) entry.Building.RemovePart(i);
                    else entry.Building.SetPartVisible(i, phase == 0 || phase == Journal.NoEffect);
                }
                layoutDirty = true;
            }
            catch (Exception error) { FailEntry(entry, error); }
        }
        private void EnsureResources()
        {
            if (assets != null) return;
            if (!view || view.orthographic || view.stereoEnabled) throw new InvalidOperationException("Project fog needs the main perspective camera.");
            assets = BlueprintFogAssets.LoadEmbedded();
            channels = BlueprintFogChannels.CreateArena(assets, 32, 32768, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D);
            atlas = new BlueprintFogAtlas(assets, channels);
            oldDepth = view.depthTextureMode; ownedDepth = oldDepth | DepthTextureMode.Depth; view.depthTextureMode = ownedDepth;
        }
        private static void Body(out Vector3 actor, out Vector2 size)
        {
            var collider = Player.m_localPlayer ? Player.m_localPlayer.GetComponent<CapsuleCollider>() : null;
            if (!collider) throw new InvalidOperationException("Project fog needs the current native player capsule.");
            Bounds bounds = collider.bounds; actor = bounds.center;
            size = new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y);
        }
        private void Update()
        {
            if (closing) { Close(); return; }
            if (failure != null || channels == null) return;
            try
            {
                Body(out var actor, out var capsule);
                bool preparedOne = false;
                foreach (var entry in entries.Values)
                {
                    if (entry.Error != null || entry.Building == null) continue;
                    if (entry.Preparation != null)
                    {
                        if (preparedOne) continue;
                        preparedOne = true;
                        try
                        {
                            if (entry.Preparation.MoveNext()) continue;
                            entry.Preparation.Dispose(); entry.Preparation = null;
                            entry.Building.SetDisplayActive(false);
                            Collect(true); entry.Building.AppendPublishedSources(owners, snapshots, true);
                            if (owners.Count > 0) atlas.PreparePublishedLayout(owners);
                            entry.Clock = entry.Building.Clock;
                            entry.Admitted = true; layoutDirty = true; entry.Building.SetDisplayActive(true);
                        }
                        catch (Exception error) { FailEntry(entry, error); }
                    }
                    if (entry.Admitted)
                    {
                        entry.Clock += Mathf.Min(Time.deltaTime, BlueprintFogSimulation.Step * 8);
                        if (Mathf.FloorToInt(entry.Clock / BlueprintFogSimulation.Step + .0001f) >
                            Mathf.FloorToInt(entry.Building.Clock / BlueprintFogSimulation.Step + .0001f))
                            entry.Building.AdvanceTo(entry.Clock, actor, capsule, !Player.m_localPlayer.IsDead());
                        else entry.Building.Pump();
                    }
                }
                channels.PumpSources();
            }
            catch (Exception error) { Fail(error); }
        }
        private void Collect(bool includeHidden = false)
        {
            owners.Clear(); snapshots.Clear();
            foreach (var entry in entries.Values)
                if (entry.Admitted && entry.Error == null) entry.Building?.AppendPublishedSources(owners, snapshots, includeHidden);
        }
        private void OnPreCull()
        {
            if (closing || failure != null || atlas == null) return;
            try
            {
                Collect();
                if (owners.Count == 0) atlas.ClearPublished();
                else
                {
                    if (layoutDirty) { atlas.PreparePublishedLayout(owners); layoutDirty = false; }
                    atlas.PreparePublished(owners, snapshots);
                }
            }
            catch (Exception error) { Fail(error); }
        }
        private void OnRenderImage(RenderTexture source, RenderTexture target)
        {
            if (closing || failure != null || atlas == null || owners.Count == 0) { Graphics.Blit(source, target); return; }
            try
            {
                Light sun = RenderSettings.sun; Vector3 direction = sun ? sun.transform.forward : Vector3.down;
                atlas.Render(view, source, target, owners, Player.m_localPlayer.transform.position - direction * 8,
                    direction, sun ? Mathf.Max(0, sun.intensity) * 1.6f : 0, snapshots);
                ++RenderFrames;
            }
            catch (Exception error) { Fail(error); Graphics.Blit(source, target); }
        }
        private void FailEntry(Entry entry, Exception error)
        {
            entry.Error = error.Message; entry.Admitted = false;
            layoutDirty = true;
            var pending = entry.Preparation; entry.Preparation = null;
            try { pending?.Dispose(); entry.Building?.Dispose(); entry.Building = null; }
            catch (Exception cleanup) { Fail(cleanup); }
            log?.Invoke(error);
        }
        private void Fail(Exception error)
        {
            if (failure != null) return;
            failure = error.Message;
            foreach (var entry in entries.Values) entry.Building?.SetDisplayActive(false);
            log?.Invoke(error);
        }
        internal void Remove(Guid id)
        {
            if (!entries.TryGetValue(id, out var entry)) return;
            entry.Preparation?.Dispose(); entry.Preparation = null;
            entry.Building?.Dispose(); entries.Remove(id); layoutDirty = true;
        }
        internal bool Close()
        {
            closing = true;
            try
            {
                foreach (var entry in entries.Values)
                { entry.Preparation?.Dispose(); entry.Preparation = null; entry.Building?.Dispose(); }
                entries.Clear(); owners.Clear(); snapshots.Clear();
                channels?.Dispose(); channels = null;
                atlas?.Dispose(); atlas = null;
                assets?.Dispose(); assets = null;
                if (view && view.depthTextureMode == ownedDepth) view.depthTextureMode = oldDepth;
                Destroy(this); return true;
            }
            catch (Exception error) { Fail(error); return false; }
        }
        private void OnDestroy() { if (assets != null || channels != null || atlas != null) Close(); }
    }
}
