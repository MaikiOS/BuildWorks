using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal static partial class BlueprintConstructionPayoutSeal
    {
        internal static readonly int MountMarkerKey = "ostrix_buildworks_mount_return_v1".GetStableHashCode();
        private static MountDropObservation mountRequest, mountHandler;
        private static readonly HashSet<ItemStand> cancelledMountStands = new HashSet<ItemStand>();
        internal static bool HasActiveDropObservation => active != null || mountHandler != null || mountRequest != null;

        // Observation/seal only. The caller supplies a live durable return intent and persists identity before marking.
        internal sealed class MountDropObservation : IDisposable
        {
            internal readonly ItemStand Stand;
            internal readonly ZDO Source;
            internal readonly BlueprintForeignPartData Original;
            internal readonly Guid Operation;
            internal readonly Player Player;
            private readonly ZNet network;
            private readonly long world, payer;
            private readonly ZNetView view;
            private readonly Func<BlueprintForeignPartData, bool> intentCurrent;
            internal readonly Action<ItemDrop> BindIdentity;
            private readonly int thread = Thread.CurrentThread.ManagedThreadId;
            internal readonly HashSet<ItemDrop> Candidates = new HashSet<ItemDrop>();
            internal ItemDrop Drop, Loading;
            internal bool IdentityBound, IdentityAttempted, HandlerSeen, HandlerFinished, Cancelled;
            internal string Error;
            internal BlueprintForeignPartData Observed { get; private set; }
            internal bool Complete => HandlerFinished && Error == null && Observed != null;
            internal bool OwnsLease => !Cancelled && ReferenceEquals(mountRequest, this);

            internal MountDropObservation(Player player, ItemStand stand, Guid operation,
                Func<BlueprintForeignPartData, bool> intentCurrent, Action<ItemDrop> bindIdentity)
            {
                Player = player; Stand = stand; Operation = operation;
                this.intentCurrent = intentCurrent; BindIdentity = bindIdentity;
                network = ZNet.instance; world = network ? network.GetWorldUID() : 0;
                payer = player ? player.GetPlayerID() : 0;
                view = stand ? stand.GetComponent<ZNetView>() : null; Source = view ? view.GetZDO() : null;
                if (operation == Guid.Empty || intentCurrent == null || bindIdentity == null ||
                    !stand || !BlueprintItemStandCapture.TryCapture(stand.GetComponent<Piece>(), out var original) || original == null ||
                    !BlueprintItemStandContents.TryRead(original, name => name.GetStableHashCode(), out var item) || item.SourceVersion != 110 || item.Equipped)
                    throw new InvalidOperationException("An actual full mounted item and durable return intent are required.");
                Original = original;
                if (!Current(true)) throw new InvalidOperationException("Native return observation requires the current private Local payer, owned idle stand and unchanged intent.");
            }

            internal bool Current(bool attached) => Thread.CurrentThread.ManagedThreadId == thread && !Cancelled && Error == null &&
                Player && Player == global::Player.m_localPlayer && global::Player.m_localPlayerExists && !Player.IsDead() && Player.GetPlayerID() == payer &&
                network && ReferenceEquals(ZNet.instance, network) && network.GetWorldUID() == world &&
                BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement && !network.IsSaving() &&
                !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) && !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter) &&
                Stand && Stand.m_canBeRemoved && BlueprintItemStandPlacement.OwnsView(Stand.gameObject, view) &&
                Source != null && !Source.m_uid.IsNone() && ReferenceEquals(view.GetZDO(), Source) &&
                BlueprintItemStandPlacement.IsIdleStand(Stand) && PrivateArea.CheckAccess(Stand.transform.position, 0f, false, false) &&
                (!attached || BlueprintItemStandCapture.TryCapture(Stand.GetComponent<Piece>(), out var actual) && actual != null &&
                    actual.Info == Original.Info && actual.Zdo == Original.Zdo) && intentCurrent(Original);

            internal void FinishLoad(ItemDrop drop)
            {
                if (!Current(true) || !IdentityBound || Candidates.Count != 1 || !ReferenceEquals(Drop, drop) ||
                    !BlueprintStorageLoadedInventory.MatchesMountedItem(drop.m_itemData, Original))
                    throw new InvalidOperationException("Native loaded drop differs from the actual mounted item.");
                var data = drop.GetComponent<ZNetView>()?.GetZDO();
                byte[] packet = Package(drop.m_itemData);
                if (data == null || data.GetByteArray(ZDOVars.s_itemData)?.SequenceEqual(packet) != true)
                    throw new InvalidOperationException("Native return did not write its actual observed packet.");
                Observed = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,
                    drop.m_itemData.m_dropPrefab.name + ":" + drop.m_itemData.m_variant.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" +
                    drop.m_itemData.m_quality.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":0", Convert.ToBase64String(packet), null);
            }
            public void Dispose()
            {
                Cancelled = true;
                // Release the global lease; the exact old stand keeps its late-handler fence.
                if (!HandlerFinished) cancelledMountStands.Add(Stand);
                if (ReferenceEquals(mountRequest, this)) mountRequest = null;
            }
        }

        internal static bool TryObserveMountDrop(Player player, ItemStand stand, Guid operation,
            Func<BlueprintForeignPartData, bool> currentDurableIntent, Action<ItemDrop> bindIdentity,
            out MountDropObservation observation, out string reason)
        {
            observation = null; reason = null;
            try
            {
                if (HasActiveDropObservation)
                    throw new InvalidOperationException("Another drop observation is still active or awaiting its owner handler.");
                cancelledMountStands.RemoveWhere(value => !value);
                if (cancelledMountStands.Contains(stand)) throw new InvalidOperationException("The cancelled native request may only resume from existing saved return evidence.");
                observation = new MountDropObservation(player, stand, operation, currentDurableIntent, bindIdentity);
                mountRequest = observation; return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        internal static bool BeginMountDropHandler(ItemStand stand, out MountDropObservation state)
        {
            state = null;
            if (cancelledMountStands.Contains(stand)) return false;
            if (mountRequest == null || !ReferenceEquals(mountRequest.Stand, stand)) return true;
            // Refused invocations never own the active or already completed handler's finalizer state.
            if (mountRequest.HandlerSeen)
            {
                if (ReferenceEquals(mountHandler, mountRequest)) mountRequest.Error = "Nested return handler was refused.";
                return false;
            }
            state = mountRequest;
            try
            {
                if (active != null || mountHandler != null || !state.Current(true))
                    throw new InvalidOperationException("Return handler context changed or was already consumed.");
                state.HandlerSeen = true; mountHandler = state; return true;
            }
            catch (Exception error) { state.Error = error.Message; return false; }
        }
        internal static Exception EndMountDropHandler(MountDropObservation state, Exception failure)
        {
            if (state == null) return failure;
            try
            {
                if (failure != null) state.Error = failure.Message;
                if (state.Error == null && (!state.Current(false) || state.Stand.HaveAttachment() || state.Observed == null || state.Candidates.Count != 1))
                    state.Error = "Native detach did not prove one loaded protected drop and an empty holder.";
                state.HandlerFinished = true;
                return failure ?? (state.Error == null ? null : new InvalidOperationException(state.Error));
            }
            finally
            {
                if (ReferenceEquals(mountHandler, state)) mountHandler = null;
                if (state.Cancelled && ReferenceEquals(mountRequest, state)) mountRequest = null;
            }
        }
        private static void ObserveMountAwake(ItemDrop drop)
        {
            var state = mountHandler;
            if (state == null || !drop || Utils.GetPrefabName(drop.gameObject) != state.Original.Info.Split(':')[0]) return;
            state.Candidates.Add(drop); held.Add(drop);
            if (state.Drop && !ReferenceEquals(state.Drop, drop)) state.Error = "Native detach created multiple matching drops.";
            else state.Drop = drop;
        }
        private static void BindMountAwake(ItemDrop drop, Exception failure)
        {
            var state = mountHandler;
            if (state == null || !state.Candidates.Contains(drop)) return;
            try
            {
                if (failure != null) throw failure;
                var view = drop.GetComponent<ZNetView>(); var data = view ? view.GetZDO() : null;
                if (!view || !view.IsValid() || !view.IsOwner() || data == null) return;
                if (!data.Persistent || data.m_uid.IsNone() || data.GetPrefab() != state.Original.Info.Split(':')[0].GetStableHashCode() ||
                    !state.Current(true) || state.Candidates.Count != 1 || !string.IsNullOrEmpty(data.GetString(MarkerKey, "")))
                    throw new InvalidOperationException("Provisional return drop identity differs from its native source.");
                string marker = data.GetString(MountMarkerKey, "");
                if (marker.Length != 0 && marker != state.Operation.ToString("N"))
                    throw new InvalidOperationException("Return drop already has a different marker.");
                if (!state.IdentityBound)
                {
                    if (state.IdentityAttempted) throw new InvalidOperationException("Uncertain identity write cannot be repeated.");
                    state.IdentityAttempted = true; state.BindIdentity(drop); state.IdentityBound = true;
                }
                data.Set(MountMarkerKey, state.Operation.ToString("N")); data.Set(OwnerKey, state.Player.GetPlayerID());
            }
            catch (Exception error)
            {
                state.Error = error.Message; drop.enabled = false; Hold(drop);
                var view = drop.GetComponent<ZNetView>(); var data = view ? view.GetZDO() : null;
                // Quarantine is not identity/credit proof; the already durable operation must keep an unbound spawn sealed.
                if (view && view.IsValid() && view.IsOwner() && data != null && data.Persistent &&
                    string.IsNullOrEmpty(data.GetString(MarkerKey, "")) &&
                    (string.IsNullOrEmpty(data.GetString(MountMarkerKey, "")) || data.GetString(MountMarkerKey, "") == state.Operation.ToString("N")))
                { data.Set(MountMarkerKey, state.Operation.ToString("N")); data.Set(OwnerKey, state.Player.GetPlayerID()); }
            }
        }
        internal static MountDropObservation BeginMountDropLoad(ItemDrop drop, ZDO source)
        {
            var state = mountHandler;
            if (state == null || !ReferenceEquals(state.Source, source)) return null;
            ObserveMountAwake(drop); BindMountAwake(drop, null);
            if (!state.Current(true) || !state.IdentityBound || state.Candidates.Count != 1 || !ReferenceEquals(state.Drop, drop) || state.Loading)
                throw new InvalidOperationException(state.Error ?? "Actual return load has no unique durable identity binding.");
            state.Loading = drop; return state;
        }
        internal static Exception EndMountDropLoad(MountDropObservation state, ItemDrop drop, Exception failure)
        {
            if (state == null) return failure;
            try
            {
                if (failure != null) throw failure;
                state.FinishLoad(drop); return null;
            }
            catch (Exception error) { state.Error = error.Message; return failure ?? error; }
            finally { state.Loading = null; }
        }
        private static bool MountHeld(ZDO data) => data != null && (!string.IsNullOrEmpty(data.GetString(MountMarkerKey, "")) ||
            mountHandler != null && mountHandler.Candidates.Any(drop => drop && ReferenceEquals(drop.GetComponent<ZNetView>()?.GetZDO(), data)));
        private static bool AllowMountSave(ItemDrop.ItemData item, ZDO data)
        {
            if (item == null || data == null || !string.IsNullOrEmpty(data.GetString(MarkerKey, ""))) return false;
            var state = mountHandler;
            if (state != null && state.Loading && ReferenceEquals(state.Loading.m_itemData, item) &&
                ReferenceEquals(state.Loading.GetComponent<ZNetView>()?.GetZDO(), data))
            {
                if (!state.Current(true) || !state.IdentityBound || !BlueprintStorageLoadedInventory.MatchesMountedItem(item, state.Original))
                    throw new InvalidOperationException("Return load may write only the actual source's full native-loaded properties.");
                return true;
            }
            try
            {
                return data.GetLong(OwnerKey, 0) != 0 && Guid.TryParseExact(data.GetString(MountMarkerKey, ""), "N", out var operation) &&
                    operation != Guid.Empty && data.GetByteArray(ZDOVars.s_itemData)?.SequenceEqual(Package(item)) == true;
            }
            catch { return false; }
        }
    }
}
