using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal abstract partial class BlueprintConstructionMountContext
    {
        // A fresh actor never grants a fresh binding. Only the loaded world and native object lifecycle do.
        internal sealed class ReturnSourceBinding
        {
            internal readonly Guid Epoch;
            internal ItemDrop Drop;
            internal ZNetView View;
            internal ZDO Data;
            internal ZDOID Id;
            internal bool Loaded, Unloaded;
            internal ReturnSourceBinding(ItemDrop drop, bool loaded)
            { Epoch = CurrentReturnEpoch(); Drop = drop; View = drop.GetComponent<ZNetView>(); Data = View.GetZDO(); Id = Data.m_uid; Loaded = loaded; }
            internal bool Current(ItemDrop drop) => Epoch == CurrentReturnEpoch() && !Unloaded && ReferenceEquals(Drop, drop) &&
                View && View.IsValid() && View.IsOwner() && ReferenceEquals(drop.GetComponent<ZNetView>(), View) &&
                ReferenceEquals(View.GetZDO(), Data) && Data.m_uid == Id && !Id.IsNone();
            internal bool Matches(BlueprintForeignPartData mounted, BlueprintForeignPartData raw) => Current(Drop) &&
                Data.GetByteArray(ZDOVars.s_itemData)?.SequenceEqual(Convert.FromBase64String(raw.Zdo)) == true &&
                (Loaded ? BlueprintStorageLoadedInventory.MatchesMountedItem(Drop.m_itemData, raw) :
                    BlueprintStorageLoadedInventory.MatchesMountedItem(Drop.m_itemData, mounted) &&
                    Convert.ToBase64String(BlueprintConstructionPayoutSeal.Package(Drop.m_itemData)) == raw.Zdo);
        }
        private static ZNet epochNetwork;
        private static World epochWorld;
        private static ZDOMan epochManager;
        private static Guid returnEpoch;
        private static bool worldLoading, loadFailed;
        private static readonly Dictionary<Guid, ReturnSourceBinding> returnBindings = new Dictionary<Guid, ReturnSourceBinding>();
        private static readonly Dictionary<ItemDrop, ReturnSourceBinding> nativeLoads = new Dictionary<ItemDrop, ReturnSourceBinding>();
        private static bool SameReturnWorld() => ReferenceEquals(epochNetwork, ZNet.instance) &&
            ReferenceEquals(epochWorld, ZNet.instance ? ZNet.instance.GetWorld() : null) && ReferenceEquals(epochManager, ZDOMan.instance);
        private static void CaptureReturnWorld() { epochNetwork = ZNet.instance; epochWorld = ZNet.instance ? ZNet.instance.GetWorld() : null; epochManager = ZDOMan.instance; }
        private static Guid CurrentReturnEpoch()
        {
            if (worldLoading || !ZNet.instance || ZNet.instance.GetWorld() == null || ZDOMan.instance == null)
                throw new InvalidOperationException("Loaded world epoch is unavailable.");
            // A failed old world must not poison the next ZNet/World/manager lifetime.
            if (!SameReturnWorld()) { CaptureReturnWorld(); loadFailed = false; NewReturnEpoch(); }
            if (loadFailed || ZNet.m_loadError) throw new InvalidOperationException("Current native world load failed.");
            return returnEpoch;
        }
        private static void NewReturnEpoch() { returnEpoch = Guid.NewGuid(); returnBindings.Clear(); nativeLoads.Clear(); }
        internal static void BeginReturnWorldLoad() { CaptureReturnWorld(); worldLoading = true; loadFailed = false; NewReturnEpoch(); }
        internal static void EndReturnWorldLoad(Exception error)
        {
            CaptureReturnWorld(); worldLoading = false; loadFailed = error != null || ZNet.m_loadError;
            if (loadFailed) NewReturnEpoch();
        }
        internal static ReturnSourceBinding BindReturnSource(Guid operation, ItemDrop drop)
        {
            CurrentReturnEpoch();
            if (returnBindings.ContainsKey(operation)) throw new InvalidOperationException("Return already has a binding in this loaded world.");
            var binding = new ReturnSourceBinding(drop, false); returnBindings.Add(operation, binding); return binding;
        }
        internal static void ReturnSourceUnloaded(ItemDrop drop)
        {
            foreach (var binding in returnBindings.Values) if (ReferenceEquals(binding.Drop, drop)) binding.Unloaded = true;
            nativeLoads.Remove(drop);
        }
        internal static void ReturnSourceNativeLoaded(ItemDrop drop, Exception failure)
        {
            if (failure != null || !drop || worldLoading || loadFailed && SameReturnWorld() || ZNet.m_loadError || !ZNet.instance || ZNet.instance.GetWorld() == null || ZDOMan.instance == null) return;
            var view = drop.GetComponent<ZNetView>(); var data = view ? view.GetZDO() : null;
            if (!view || !view.IsValid() || data == null || !Guid.TryParseExact(data.GetString(BlueprintConstructionPayoutSeal.MountMarkerKey, ""), "N", out var op)) return;
            var bytes = data.GetByteArray(ZDOVars.s_itemData); if (bytes == null) return;
            var item = drop.m_itemData; if (item == null || !item.m_dropPrefab) return;
            var raw = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,
                item.m_dropPrefab.name + ":" + item.m_variant + ":" + item.m_quality + ":0", Convert.ToBase64String(bytes), null);
            if (!BlueprintStorageLoadedInventory.MatchesMountedItem(item, raw)) return;
            CurrentReturnEpoch();
            if (returnBindings.TryGetValue(op, out var old))
            {
                // Scene streaming can recreate a drop over the same ZDO; a copied marker or UID is insufficient.
                if (!ReferenceEquals(old.Data, data) || old.Id != data.m_uid ||
                    !ReferenceEquals(old.Drop, drop) && !old.Unloaded) return;
                old.Drop = drop; old.View = view; old.Loaded = true; old.Unloaded = false;
                nativeLoads[drop] = old; return;
            }
            nativeLoads[drop] = new ReturnSourceBinding(drop, true);
        }
        internal static ReturnSourceBinding ResolveReturnSource(BlueprintConstructionPartJournal.MountRecord mount, ItemDrop drop)
        {
            Guid epoch = CurrentReturnEpoch(), operation = mount.Return.Operation;
            if (returnBindings.TryGetValue(operation, out var bound))
            {
                if (!bound.Current(drop)) throw new InvalidOperationException("Return source was substituted in the same loaded world.");
                return bound;
            }
            if (mount.Return.SourceEpoch == epoch || !nativeLoads.TryGetValue(drop, out bound) || !bound.Current(drop))
                throw new InvalidOperationException("Remapping needs a verified native Load in a new world epoch or completed object lifecycle.");
            returnBindings.Add(operation, bound); return bound;
        }
    }
}
