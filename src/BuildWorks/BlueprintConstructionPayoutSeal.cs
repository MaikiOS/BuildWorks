using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;

namespace OstrixMods.BuildWorks
{
    // Pending returns stay sealed through reload; only a later durable collection protocol may release custody.
    internal static class BlueprintConstructionPayoutSeal
    {
        internal static readonly int MarkerKey = "ostrix_buildworks_refund_item_v1".GetStableHashCode();
        internal static readonly int OwnerKey = "ostrix_buildworks_refund_owner_v1".GetStableHashCode();
        private static readonly HashSet<ItemDrop> held = new HashSet<ItemDrop>();
        private sealed class Spawn
        {
            internal Journal.Entry Entry;
            internal ItemDrop.ItemData Source;
            internal ItemDrop Drop;
            internal long Player;
            internal bool Attempted;
            internal string Error;
            internal readonly HashSet<ItemDrop> Candidates = new HashSet<ItemDrop>();
        }
        private static Spawn active;

        internal static ItemDrop Create(Journal journal, Journal.Record record, int index, ItemDrop.ItemData item, Vector3 position)
        {
            var durable = journal.Read(record.Project);
            if (active != null || !BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement || durable?.Operation != record.Operation ||
                durable.Player != Player.m_localPlayer.GetPlayerID() || durable.Phase != Journal.Prepared || index < 0 || index >= durable.Items.Length ||
                durable.Items[index].State != Journal.Attempted || durable.Items[index].Id != record.Items[index].Id ||
                Convert.ToBase64String(Package(item)) != durable.Items[index].Blob) throw new InvalidOperationException("A matching durable payout intent is required before native creation.");
            var scope = new Spawn { Entry = durable.Items[index], Source = item, Player = durable.Player }; active = scope;
            try
            {
                var drop = ItemDrop.DropItem(item, item.m_stack, position, Quaternion.identity);
                var view = drop ? drop.GetComponent<ZNetView>() : null; var data = view ? view.GetZDO() : null;
                if (scope.Error != null || scope.Candidates.Count != 1 || !scope.Attempted || !drop || !ReferenceEquals(drop, scope.Drop) || !view || !view.IsValid() || !view.IsOwner() ||
                    data == null || data.m_uid.IsNone() || !data.Persistent || data.GetPrefab() != item.m_dropPrefab.name.GetStableHashCode() ||
                    data.GetString(MarkerKey, "") != scope.Entry.Id.ToString("N") || data.GetLong(OwnerKey, 0) != scope.Player ||
                    Convert.ToBase64String(Package(drop.m_itemData)) != scope.Entry.Blob ||
                    data.GetByteArray(ZDOVars.s_itemData)?.SequenceEqual(Convert.FromBase64String(scope.Entry.Blob)) != true)
                    throw new InvalidOperationException("Native payout identity, saved package or early seal differs; issuance cannot retry.");
                Hold(drop); return drop;
            }
            finally { active = null; }
        }
        internal static byte[] Package(ItemDrop.ItemData item)
        { var package = new ZPackage(); package.Write((byte)110); item.Save(package); return package.GetArray(); }
        internal static bool Tagged(ItemDrop.ItemData item) => item?.m_customData?.ContainsKey(Journal.CustodyKey) == true;
        internal static bool IsHeld(ZDO data) => data != null && (!string.IsNullOrEmpty(data.GetString(MarkerKey, "")) ||
            active != null && active.Candidates.Any(drop => drop && ReferenceEquals(drop.GetComponent<ZNetView>()?.GetZDO(), data)));
        internal static bool IsHeld(ItemDrop drop) => drop && (held.Contains(drop) || Tagged(drop.m_itemData) || IsHeld(drop.GetComponent<ZNetView>()?.GetZDO()));
        internal static bool AllowStack(ItemDrop drop)
        {
            if (IsHeld(drop)) return false;
            if (held.Count == 0) return true;
            // Native overlap uses colliders, whose edge can be within 4m even when their centre is farther away.
            return !Physics.OverlapSphere(drop.transform.position, 4f, LayerMask.GetMask("item"))
                .Any(collider => collider.attachedRigidbody && IsHeld(collider.attachedRigidbody.GetComponent<ItemDrop>()));
        }
        internal static void BeforeDrop(ItemDrop.ItemData item)
        {
            if (!Tagged(item)) return;
            if (active == null || active.Attempted || !ReferenceEquals(active.Source, item))
                throw new InvalidOperationException("Sealed payout cannot be copied or dropped outside its durable intent.");
            active.Attempted = true;
        }
        internal static void BeforeAwake(ItemDrop drop)
        {
            if (active == null || !active.Attempted || Utils.GetPrefabName(drop.gameObject) != active.Source.m_dropPrefab.name) return;
            held.Add(drop); active.Candidates.Add(drop);
            if (active.Drop && !ReferenceEquals(active.Drop, drop)) { active.Error = "Payout instantiated more than one matching item."; return; }
            active.Drop = drop;
        }
        internal static void AfterAwake(ItemDrop drop, Exception failure)
        {
            if (!drop) return;
            try
            {
                BeforeAwake(drop);
                var view = drop.GetComponent<ZNetView>(); var data = view ? view.GetZDO() : null;
                if (active != null && active.Candidates.Contains(drop))
                {
                    if (failure != null) active.Error = failure.Message;
                    if (view && view.IsValid() && view.IsOwner() && data != null && data.Persistent)
                    {
                        string marker = data.GetString(MarkerKey, "");
                        if (marker.Length != 0 && marker != active.Entry.Id.ToString("N"))
                            active.Error = "Early payout ZDO already has another marker.";
                        else { data.Set(MarkerKey, active.Entry.Id.ToString("N")); data.Set(OwnerKey, active.Player); }
                    }
                    Hold(drop); return;
                }
                if (!IsHeld(drop)) return;
                Hold(drop);
                // Either Awake may run first. Restore the native package before Start can save prefab defaults.
                if (data != null && data.GetByteArray(ZDOVars.s_itemData) != null) ItemDrop.LoadFromZDO(drop.m_itemData, data);
            }
            catch (Exception error)
            {
                if (active != null && active.Candidates.Contains(drop)) active.Error = error.Message;
                held.Add(drop); drop.enabled = false;
            }
        }
        internal static bool AllowSave(ItemDrop.ItemData item, ZDO data, int index)
        {
            if (index != -1 || !IsHeld(data) && !Tagged(item)) return true;
            try
            {
                if (data == null || !item.m_customData.TryGetValue(Journal.CustodyKey, out string id) ||
                    data.GetString(MarkerKey, "") != id || data.GetLong(OwnerKey, 0) == 0) return false;
                byte[] package = Package(item);
                return active != null && active.Error == null && active.Entry.Id.ToString("N") == id &&
                    Convert.ToBase64String(package) == active.Entry.Blob || data.GetByteArray(ZDOVars.s_itemData)?.SequenceEqual(package) == true;
            }
            catch { return false; }
        }
        private static void Hold(ItemDrop drop)
        {
            held.Add(drop); drop.m_autoPickup = false; drop.m_autoDestroy = false;
            var body = drop.GetComponent<Rigidbody>(); if (body) body.isKinematic = true;
        }
        internal static void Destroyed(ItemDrop drop) { held.Remove(drop); }
    }
}
