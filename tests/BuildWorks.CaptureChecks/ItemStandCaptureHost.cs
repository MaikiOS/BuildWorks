// Minimal read-side host for the actual capture source; never a native/Unity acceptance test.
namespace UnityEngine
{
    public class Object
    {
        public bool Destroyed;
        public static implicit operator bool(Object value) => value != null && !value.Destroyed;
    }
    public class GameObject : Object
    {
        public string name = "ItemFixture";
        public readonly System.Collections.Generic.Dictionary<System.Type, Object> Components = new System.Collections.Generic.Dictionary<System.Type, Object>();
        public T GetComponent<T>() where T : Object => Components.TryGetValue(typeof(T), out var value) ? value as T : null;
    }
}
public sealed class Piece : UnityEngine.GameObject { }
public sealed class ArmorStand : UnityEngine.Object
{
    private ZNetView m_nview;
    private object m_queuedItem;
    private int m_pose;
    public object Queued { get => m_queuedItem; set => m_queuedItem = value; }
    public int Pose { get => m_pose; set => m_pose = value; }
    public ZNetView View { get => m_nview; set => m_nview = value; }
    public sealed class ArmorStandSlot { public int m_slot; }
    public System.Collections.Generic.List<ArmorStandSlot> m_slots = new System.Collections.Generic.List<ArmorStandSlot>();
}
public sealed class ItemDrop : UnityEngine.Object { }
public sealed class ZDO
{
    public uint DataRevision;
    public readonly System.Collections.Generic.Dictionary<int, int> Numbers = new System.Collections.Generic.Dictionary<int, int>();
    public readonly System.Collections.Generic.Dictionary<int, byte[]> Packets = new System.Collections.Generic.Dictionary<int, byte[]>();
    public System.Action<int> AfterRead;
    public int GetInt(int key, int fallback)
    { int value = Numbers.TryGetValue(key, out int result) ? result : fallback; AfterRead?.Invoke(key); return value; }
    public byte[] GetByteArray(int key, byte[] fallback)
    { byte[] value = Packets.TryGetValue(key, out var packet) ? packet : fallback; AfterRead?.Invoke(key); return value; }
}
public sealed class ZNetView : UnityEngine.Object
{
    public bool Valid = true;
    public ZDO Data = new ZDO();
    public bool IsValid() => Valid;
    public ZDO GetZDO() => Data;
}
public sealed class ItemStand : UnityEngine.Object
{
    private ZNetView m_nview;
    public ItemStand(ZNetView view) { m_nview = view; }
    public int GetAttachedItem() => m_nview.GetZDO().GetInt(ZDOVars.s_item, 0);
}
public sealed class ObjectDB : UnityEngine.Object
{
    public static ObjectDB instance = new ObjectDB();
    public UnityEngine.GameObject Item;
    public UnityEngine.GameObject GetItemPrefab(int hash) => hash == 42 ? Item : null;
}
public static class ZDOVars { public const int s_item = 1, s_variant = 2, s_quality = 3, s_type = 4, s_itemData = 5, s_pose = 6; }
public static class HashFixture
{
    public static int GetStableHashCode(this string value)
    { if (value == "ItemFixture") return 42; unchecked { int hash = 17; foreach (char c in value) hash = hash * 31 + c; return hash; } }
}
namespace CaptureChecks
{
    using System;
    using OstrixMods.BuildWorks;
    public static class Runner
    {
        private static int checks;
        private static void Require(bool condition, string reason) { ++checks; if (!condition) throw new Exception(reason); }
        private static Piece Fixture(out ZNetView view, bool occupied)
        {
            view = new ZNetView(); view.Data.Numbers[ZDOVars.s_item] = occupied ? 42 : 0;
            var piece = new Piece(); piece.Components[typeof(ItemStand)] = new ItemStand(view);
            var item = new UnityEngine.GameObject(); item.Components[typeof(ItemDrop)] = new ItemDrop(); ObjectDB.instance.Item = item;
            return piece;
        }
        public static int Run()
        {
            checks = 0;
            var piece = Fixture(out var view, false);
            Require(BlueprintItemStandCapture.TryCapture(piece, out var empty) && empty == null, "Stable empty capture failed.");
            piece = Fixture(out view, true);
            Require(BlueprintItemStandCapture.TryCapture(piece, out var full) && full.Info == "ItemFixture:0:1:0" && full.Zdo == null, "Stable legacy capture failed.");
            piece.Components[typeof(ArmorStand)] = new ArmorStand();
            Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "Uninitialized armour must not be silently discarded.");

            piece = Fixture(out view, false);
            var currentView = view;
            view.Data.AfterRead = _ => { currentView.Data = new ZDO(); currentView.Data.Numbers[ZDOVars.s_item] = 42; };
            Require(!BlueprintItemStandCapture.TryCapture(piece, out var reboundEmpty) && reboundEmpty == null,
                "BUG: empty capture accepted a replaced ZDO with the same revision.");
            foreach (int key in new[] { ZDOVars.s_variant, ZDOVars.s_quality, ZDOVars.s_type, ZDOVars.s_itemData })
            {
                piece = Fixture(out view, true); currentView = view; var original = view.Data;
                original.AfterRead = read => { if (read == key) currentView.Data = new ZDO(); };
                Require(!BlueprintItemStandCapture.TryCapture(piece, out var rebound) && rebound == null && original.DataRevision == 0,
                    "BUG: occupied capture accepted replaced ZDO after key " + key);
            }
            piece = Fixture(out view, true); currentView = view;
            view.Data.AfterRead = _ => currentView.Valid = false;
            Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "Invalidated view accepted.");
            piece = Fixture(out view, true); var changed = view.Data;
            changed.AfterRead = _ => ++changed.DataRevision;
            Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "Changed revision accepted.");
            piece = Fixture(out view, true); var retained = view.Data;
            Require(BlueprintItemStandCapture.TryCapture(piece, out _) && ReferenceEquals(view.GetZDO(), retained) && retained.DataRevision == 0,
                "Capture must neither rebind nor write native state.");
            ArmorChecks();
            return checks;
        }

        private static void ArmorChecks()
        {
            Piece Make(out ArmorStand armor)
            {
                var piece = Fixture(out var view, false);
                piece.Components.Remove(typeof(ItemStand));
                armor = new ArmorStand { View = view, Pose = 7 };
                armor.m_slots.Add(new ArmorStand.ArmorStandSlot { m_slot = 5 });
                armor.m_slots.Add(new ArmorStand.ArmorStandSlot { m_slot = 5 });
                piece.Components[typeof(ArmorStand)] = armor; return piece;
            }
            var piece = Make(out var stand);
            Require(BlueprintItemStandCapture.TryCapture(piece, out var data) &&
                OstrixMods.BuildWorks.Geometry.BlueprintArmorStandState.Read(data).Pose == 7, "Empty armor lost native pose fallback.");
            stand.View.Data.Numbers[ZDOVars.s_pose] = -9;
            stand.View.Data.Numbers["0_item".GetStableHashCode()] = 42;
            stand.View.Data.Numbers["0_variant".GetStableHashCode()] = 3;
            byte[] packet = { 110, 2, 8 };
            stand.View.Data.Packets["0_itemData".GetStableHashCode()] = packet;
            stand.View.Data.Packets["1_itemData".GetStableHashCode()] = new byte[] { 111, 4 };
            Require(BlueprintItemStandCapture.TryCapture(piece, out data), "Native armor snapshot failed.");
            var state = OstrixMods.BuildWorks.Geometry.BlueprintArmorStandState.Read(data);
            packet[0] = 0;
            Require(state.Pose == -9 && state.Slots.Count == 2 && state.Slots[0].ItemId == 42 && state.Slots[0].Prefab == "ItemFixture" &&
                state.Slots[0].Variant == 3 && state.Slots[0].Package[0] == 110 && state.Slots[1].ItemId == 0 && state.Slots[1].Package[0] == 111,
                "Pose/ordinal/active IDs/exact cloned packages changed.");
            stand.View.Data.Numbers["0_item".GetStableHashCode()] = 987;
            Require(BlueprintItemStandCapture.TryCapture(piece, out data) &&
                OstrixMods.BuildWorks.Geometry.BlueprintArmorStandState.Read(data).Slots[0].Prefab == null, "Unknown item hash was lost/guessed.");
            stand.Queued = new object();
            Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "Queued attachment captured.");
            foreach (string change in new[] { "zdo", "revision", "view", "queue", "list", "count", "descriptor", "visual", "pose" })
            {
                piece = Make(out stand); var current = stand; var original = current.View.Data;
                original.AfterRead = key => {
                    if (key != "1_itemData".GetStableHashCode()) return;
                    switch (change)
                    {
                        case "zdo": current.View.Data = new ZDO(); break;
                        case "revision": ++original.DataRevision; break;
                        case "view": current.View = new ZNetView(); break;
                        case "queue": current.Queued = new object(); break;
                        case "list": current.m_slots = new System.Collections.Generic.List<ArmorStand.ArmorStandSlot>(current.m_slots); break;
                        case "count": current.m_slots.Clear(); break;
                        case "descriptor": current.m_slots[0] = new ArmorStand.ArmorStandSlot { m_slot = 5 }; break;
                        case "visual": current.m_slots[0].m_slot = 9; break;
                        case "pose": current.Pose = 4; break;
                    }
                };
                Require(!BlueprintItemStandCapture.TryCapture(piece, out var rejected) && rejected == null, "Armor race accepted: " + change);
            }
            piece = Make(out stand); stand.View.Data.Packets["0_itemData".GetStableHashCode()] = new byte[24577];
            Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "Oversized armor item captured.");
            piece = Make(out stand); stand.m_slots[0] = null;
            Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "Null descriptor captured.");
            piece = Make(out stand); var saved = stand.View.Data;
            Require(BlueprintItemStandCapture.TryCapture(piece, out _) && saved.DataRevision == 0 && ReferenceEquals(saved, stand.View.Data),
                "Armor capture changed world state.");
        }
    }
}
