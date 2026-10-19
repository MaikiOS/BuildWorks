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
public sealed class ArmorStand : UnityEngine.Object { }
public sealed class ItemDrop : UnityEngine.Object { }
public sealed class ZDO
{
    public uint DataRevision;
    public readonly System.Collections.Generic.Dictionary<int, int> Numbers = new System.Collections.Generic.Dictionary<int, int>();
    public System.Action<int> AfterRead;
    public int GetInt(int key, int fallback)
    { int value = Numbers.TryGetValue(key, out int result) ? result : fallback; AfterRead?.Invoke(key); return value; }
    public byte[] GetByteArray(int key, byte[] fallback) { AfterRead?.Invoke(key); return fallback; }
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
public static class ZDOVars { public const int s_item = 1, s_variant = 2, s_quality = 3, s_type = 4, s_itemData = 5; }
public static class HashFixture { public static int GetStableHashCode(this string value) => 42; }
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
            Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "Unsupported armour pose/equipment must not be discarded.");

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
            return checks;
        }
    }
}
