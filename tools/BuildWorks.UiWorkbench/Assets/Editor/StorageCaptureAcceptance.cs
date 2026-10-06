using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class StorageCaptureAcceptance
{
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("Storage capture: " + message); }

    internal static string Run()
    {
        var root = new GameObject("InertCaptureChest");
        var network = new GameObject("InertContainerNetworkOverride");
        var playerRoot = new GameObject("InertCapturePlayer");
        var itemRoot = new GameObject("StorageFixtureWood");
        ObjectDB previous = ObjectDB.instance;
        try
        {
            var piece = root.AddComponent<Piece>();
            var player = playerRoot.AddComponent<Player>();
            Require(BlueprintStorageCapture.TryCapture(piece, player, out var none) && none == null, "ordinary Piece rejected");
            var nested = new GameObject("NestedStorage"); nested.transform.SetParent(root.transform);
            nested.AddComponent<Container>();
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "nested storage silently skipped"); Object.DestroyImmediate(nested);
            var container = root.AddComponent<Container>();
            var view = network.AddComponent<ZNetView>();
            typeof(Container).GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(container, view);
            ObjectDB.instance = new ObjectDB(); ObjectDB.instance.Items.Add(itemRoot.name, itemRoot);
            var item = itemRoot.AddComponent<ItemDrop>().m_itemData;
            item.m_dropPrefab = itemRoot; item.m_stack = 12; item.m_quality = 3;
            item.m_customData.Add("mod:payload", new string('x', 40000));
            container.Inventory.Items.Add(item);
            byte[] bytes = FixturePackage(itemRoot.name, item.m_customData["mod:payload"]);
            container.Inventory.Snapshot = (byte[])bytes.Clone();
            var zdo = view.GetZDO(); zdo.Set(ZDOVars.s_items, bytes);
            int writes = zdo.Writes;
            string original = Convert.ToBase64String(bytes);
            Require(BlueprintStorageCapture.TryCapture(piece, player, out var state) &&
                state.Format == BlueprintForeignPartData.NativeContainerFormat && state.Info == "5:2:1:12|StorageFixtureWood" &&
                state.Zdo == original && zdo.Writes == writes && ReferenceEquals(zdo.GetByteArray(ZDOVars.s_items, null), bytes),
                "capture lost data or wrote live ZDO");
            Require(BlueprintContainerInventory.TryRead(state, name => name.GetStableHashCode(), out var inspection) &&
                inspection.Count == 1 && inspection[0].Quality == 3 && inspection[0].Stack == 12 &&
                inspection[0].CustomData["mod:payload"].Length == 40000, "native field inspection lost contents");
            bytes[bytes.Length - 1] = 2; container.Inventory.Snapshot = (byte[])bytes.Clone();
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "invalid native records captured as verified state");
            bytes[bytes.Length - 1] = 0; container.Inventory.Snapshot = (byte[])bytes.Clone();
            bytes[20] ^= 1;
            Require(state.Zdo == original, "snapshot aliases source bytes");
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "live/package mismatch accepted");
            bytes[20] ^= 1;
            // Three occupied slots previously let A|B,C,C split into a false A,B,C dependency manifest.
            ObjectDB.instance.Items.Remove(itemRoot.name); itemRoot.name = "A|B"; ObjectDB.instance.Items.Add(itemRoot.name, itemRoot);
            var other = new GameObject("C"); other.transform.SetParent(itemRoot.transform); ObjectDB.instance.Items.Add(other.name, other);
            container.Inventory.Items.Add(new ItemDrop.ItemData { m_dropPrefab = other, m_gridPos = new Vector2Int(1, 0) });
            container.Inventory.Items.Add(new ItemDrop.ItemData { m_dropPrefab = other, m_gridPos = new Vector2Int(2, 0) });
            bytes[4] = 3; container.Inventory.Snapshot = (byte[])bytes.Clone(); zdo.Set(ZDOVars.s_items, bytes);
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "ambiguous prefab delimiter corrupted dependencies");
            container.Inventory.Items.Clear(); container.Inventory.Items.Add(item);
            ObjectDB.instance.Items.Remove(itemRoot.name); itemRoot.name = "StorageFixtureWood"; ObjectDB.instance.Items.Add(itemRoot.name, itemRoot);
            bytes[4] = 1; container.Inventory.Snapshot = (byte[])bytes.Clone(); zdo.Set(ZDOVars.s_items, bytes);
            ObjectDB.instance.Items[itemRoot.name] = other;
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "mismatched prefab resolution accepted");
            ObjectDB.instance.Items[itemRoot.name] = itemRoot;
            container.Denied = true; Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "access bypass"); container.Denied = false;
            container.Busy = true; Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "busy inventory accepted"); container.Busy = false;
            container.m_checkGuardStone = true; PrivateArea.Allowed = false;
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "ward bypass"); PrivateArea.Allowed = true;
            container.m_autoDestroyEmpty = true; Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "special inventory accepted"); container.m_autoDestroyEmpty = false;
            view.Valid = false; Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "invalid network view accepted"); view.Valid = true;
            container.Inventory.OnSave = () => zdo.Set(ZDOVars.s_items, bytes);
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "changed revision accepted"); container.Inventory.OnSave = null;
            container.Inventory.OnSave = () => container.Busy = true;
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "became busy during read"); container.Inventory.OnSave = null; container.Busy = false;
            item.m_customData["mod:payload"] = new string('x', BlueprintForeignPartData.MaximumContainerBytes);
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "unbounded serialization accepted"); item.m_customData.Clear();
            for (int index = 0; index < 32768; ++index) item.m_customData.Add("k" + index, "v");
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "native 15-bit custom map count would overflow"); item.m_customData.Clear();
            item.m_gridPos = new Vector2Int(5, 0); Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "invalid grid cell accepted"); item.m_gridPos = Vector2Int.zero;
            container.Inventory.Items.Add(item); Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "duplicate cell accepted"); container.Inventory.Items.Clear();
            foreach (byte[] invalid in new[] { null, new byte[2], new byte[BlueprintForeignPartData.MaximumContainerBytes + 1] })
            { zdo.Set(ZDOVars.s_items, invalid); Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "missing/oversized state treated as empty"); }
            byte[] empty = { 109, 0, 0, 0, 0, 0 }; zdo.Set(ZDOVars.s_items, empty); container.Inventory.Snapshot = empty;
            writes = zdo.Writes;
            Require(BlueprintStorageCapture.TryCapture(piece, player, out var emptyState) && emptyState.Info == "5:2:0:0" &&
                zdo.Writes == writes && emptyState.Zdo == Convert.ToBase64String(empty), "known empty state not preserved");
            zdo.Set(ZDOVars.s_items, (byte[])null); zdo.Set(ZDOVars.s_addedDefaultItems, 1); writes = zdo.Writes;
            Require(BlueprintStorageCapture.TryCapture(piece, player, out var freshEmpty) && freshEmpty.Info == "5:2:0:0" &&
                freshEmpty.Zdo == Convert.ToBase64String(empty) && zdo.Writes == writes && zdo.GetByteArray(ZDOVars.s_items, null) == null,
                "initialized fresh empty chest was rejected or live data was written");
            container.Inventory.Items.Add(item);
            Require(!BlueprintStorageCapture.TryCapture(piece, player, out _), "initialized nonempty missing package accepted");
            var derivedRoot = new GameObject("InertDerivedStorage"); derivedRoot.transform.SetParent(playerRoot.transform);
            var derivedPiece = derivedRoot.AddComponent<Piece>(); var derived = derivedRoot.AddComponent<DerivedStorage>();
            typeof(Container).GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(derived, view);
            derived.Inventory.Snapshot = empty;
            Require(!BlueprintStorageCapture.TryCapture(derivedPiece, player, out _), "derived storage treated as fully native state");
            return "Actual read-only Container capture on inert API fixture: verified v109 records/dependencies, full private package comparison, immutable >32K snapshot, custom map boundary, known empty, access/ward/busy/revision/special/missing/grid/size rejection; no inventory restore or world writes. Native serializer layout pinned separately by HostContract.";
        }
        finally
        {
            ObjectDB.instance = previous; PrivateArea.Allowed = true;
            Object.DestroyImmediate(root); Object.DestroyImmediate(network); Object.DestroyImmediate(playerRoot); Object.DestroyImmediate(itemRoot);
        }
    }

    private static byte[] FixturePackage(string prefab, string customValue)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(109); writer.Write((ushort)1); writer.Write(12500);
            writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)204);
            writer.Write((ushort)3); writer.Write((ushort)12); writer.Write(prefab.GetStableHashCode());
            writer.Write((byte)1); writer.Write("mod:payload"); writer.Write(customValue); writer.Write((byte)0);
            writer.Flush(); return stream.ToArray();
        }
    }
}
