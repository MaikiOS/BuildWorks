using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Local native transfer primitive. World placement stays blocked until durable recovery/server acknowledgement exists.
    internal sealed class BlueprintStorageTransfer
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo ViewField = typeof(Container).GetField("m_nview", Private);
        private static readonly FieldInfo LoadingField = typeof(Container).GetField("m_loading", Private);
        private static readonly FieldInfo RevisionField = typeof(Container).GetField("m_lastRevision", Private);
        private static readonly MethodInfo Access = typeof(Container).GetMethod("CheckAccess", Private);

        internal enum Result { Ready, Unchanged, Transferred, Uncertain }
        internal sealed class Entry
        {
            internal readonly BlueprintItemStandPlacement.Reservation Input;
            internal readonly int X, Y;
            internal readonly string ProvidedProperties;
            internal Result State;
            internal bool Attempted, BeforeObserved, QueuedBefore, EquippedBefore, QueuedAfter, EquippedAfter;
            internal int SourceBefore, SourceAfter, DestinationAfter;
            internal bool SourcePresentAfter, LocalSaveConfirmed;
            internal bool? NativeReturn;
            internal ItemDrop.ItemData DestinationItem;
            internal string Error;
            internal Entry(BlueprintItemStandPlacement.Reservation input, int x, int y)
            { Input = input; X = x; Y = y; ProvidedProperties = BlueprintItemStandPlacement.SnapshotItem(input.Item, true); }
        }

        private readonly Player player;
        private readonly Piece piece;
        private readonly Container container;
        private readonly Inventory source, destination;
        private readonly ZNetView view;
        private readonly ZDO zdo;
        private readonly ZDOID targetId;
        private readonly ZNet network;
        private readonly ZDOMan objects;
        private readonly long worldId;
        private readonly IReadOnlyList<Entry> entries;
        private readonly IReadOnlyList<BlueprintItemStandPlacement.Supply> otherSupplies;
        internal readonly Guid OperationId;
        internal IReadOnlyList<Entry> Entries => entries;
        internal bool CompleteInMemory => entries.All(entry => entry.State == Result.Transferred);

        private BlueprintStorageTransfer(Guid operationId, Player player, Piece piece, Container container,
            ZNetView view, List<Entry> entries, IEnumerable<BlueprintItemStandPlacement.Supply> otherSupplies)
        {
            OperationId = operationId; this.player = player; this.piece = piece; this.container = container; this.view = view;
            source = player.GetInventory(); destination = container.GetInventory(); zdo = view.GetZDO(); targetId = zdo.m_uid;
            network = ZNet.instance; objects = ZDOMan.instance; worldId = network.GetWorld().m_uid;
            this.entries = Array.AsReadOnly(entries.ToArray());
            this.otherSupplies = Array.AsReadOnly(otherSupplies.ToArray());
        }

        internal static bool TryBegin(Guid operationId, Player player, GameObject placed, BlueprintForeignPartData data,
            BlueprintItemStandPlacement.Supply supply, BlueprintConstructionRefund payment,
            IReadOnlyList<BlueprintItemStandPlacement.Supply> heldSupplies,
            out BlueprintStorageTransfer transfer, out string error)
        {
            transfer = null; error = null;
            try
            {
                if (operationId == Guid.Empty || !player || !placed || payment == null || !payment.Recorded || payment.Error != null ||
                    !payment.IsFor(player.GetInventory()) || supply == null || supply.Item != null || supply.Items.Count == 0 ||
                    heldSupplies == null || heldSupplies.Count(value => ReferenceEquals(value, supply)) != 1 ||
                    supply.Items.Count > 4096 || data == null || data.ContentsCleared ||
                    !BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out var demands))
                    throw new InvalidOperationException("Transfer requires a recorded payment and a complete container reservation.");
                Piece piece = placed.GetComponent<Piece>();
                Container container = placed.GetComponent<Container>();
                var grid = BlueprintStorageCapture.PrefabGrid(placed);
                var view = ViewField?.GetValue(container) as ZNetView;
                if (!piece || !container || !grid.HasValue || !view || !view.IsValid() || view.GetZDO() == null ||
                    !ZNet.instance || ZDOMan.instance == null || ZNet.instance.GetWorld() == null ||
                    container.GetInventory() == null || supply.Items.Count > checked(grid.Value.Width * grid.Value.Height) ||
                    container.GetInventory().GetWidth() != grid.Value.Width || container.GetInventory().GetHeight() != grid.Value.Height ||
                    container.GetInventory().GetAllItems().Count != 0 || !BlueprintItemStandPlacement.IsAvailable(player, supply))
                    throw new InvalidOperationException("Transfer target is not an empty supported native container.");
                var wanted = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var demand in demands)
                { wanted.TryGetValue(demand.Prefab, out int count); wanted[demand.Prefab] = checked(count + demand.Stack); }
                var references = new HashSet<ItemDrop.ItemData>();
                var entries = new List<Entry>();
                foreach (var input in supply.Items)
                {
                    if (!references.Add(input.Item) || input.Amount < 1 || input.Amount > ushort.MaxValue ||
                        input.Amount > input.Item.m_shared.m_maxStackSize || input.Item.m_shared.m_questItem ||
                        !wanted.TryGetValue(input.Item.m_dropPrefab.name, out int count) || count < input.Amount)
                        throw new InvalidOperationException("Provided items do not match the transferable container recipe.");
                    wanted[input.Item.m_dropPrefab.name] = count - input.Amount;
                    entries.Add(new Entry(input, entries.Count % grid.Value.Width, entries.Count / grid.Value.Width));
                }
                if (wanted.Values.Any(count => count != 0)) throw new InvalidOperationException("Container reservation is incomplete.");
                var candidate = new BlueprintStorageTransfer(operationId, player, piece, container, view, entries,
                    heldSupplies.Where(value => value != null && !ReferenceEquals(value, supply)));
                if (!candidate.ContextValid() || !candidate.RemainingAvailable() || !BlueprintStorageCapture.TryCapture(piece, player, out _))
                    throw new InvalidOperationException("Native container state or access could not be verified.");
                transfer = candidate; return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        private static bool OwnedAccess(Player player, Piece piece, Container container, ZNetView view) =>
            player && player == Player.m_localPlayer && Player.m_localPlayerExists && piece && container &&
            Game.instance && !Game.instance.IsShuttingDown() &&
            piece.GetCreator() == player.GetPlayerID() && !InventoryGui.IsVisible() && !container.IsInUse() &&
            LoadingField != null && !(bool)LoadingField.GetValue(container) && Access != null &&
            (bool)Access.Invoke(container, new object[] { player.GetPlayerID() }) &&
            PrivateArea.CheckAccess(piece.transform.position, 0f, false, false) && view && view.IsValid() && view.IsOwner() &&
            ReferenceEquals(ViewField.GetValue(container), view) && view.GetZDO() != null && !view.GetZDO().m_uid.IsNone() && RevisionField != null;

        private bool ContextValid() => OwnedAccess(player, piece, container, view) && ReferenceEquals(ZNet.instance, network) &&
            ReferenceEquals(ZDOMan.instance, objects) && network && network.GetWorld()?.m_uid == worldId &&
            ReferenceEquals(player.GetInventory(), source) && ReferenceEquals(container.GetInventory(), destination) &&
            ReferenceEquals(view.GetZDO(), zdo) && zdo.m_uid.Equals(targetId);

        // Read backing lists into detached maps before any callbacks can mutate them.
        private static Dictionary<ItemDrop.ItemData, int> Counts(Inventory inventory)
        {
            var items = inventory.GetAllItems().ToArray();
            if (items.Length > 4096) throw new InvalidOperationException("Inventory exceeds the native observation budget.");
            var result = new Dictionary<ItemDrop.ItemData, int>();
            foreach (var item in items)
            {
                if (item == null || item.m_stack < 0) throw new InvalidOperationException("Invalid live inventory quantity.");
                result.Add(item, item.m_stack);
            }
            return result;
        }

        private static bool SameCounts(Dictionary<ItemDrop.ItemData, int> before, Inventory inventory,
            ItemDrop.ItemData changed, int after, bool remove)
        {
            var expected = new Dictionary<ItemDrop.ItemData, int>(before);
            if (remove) expected.Remove(changed); else expected[changed] = after;
            var actual = Counts(inventory);
            return actual.Count == expected.Count && expected.All(entry => actual.TryGetValue(entry.Key, out int count) && count == entry.Value);
        }

        private bool DeliveredItemsIntact() => entries.Where(entry => entry.State == Result.Transferred).All(entry =>
            ReferenceEquals(destination.GetItemAt(entry.X, entry.Y), entry.DestinationItem) &&
            entry.DestinationItem.m_stack == entry.Input.Amount && !entry.DestinationItem.m_equipped &&
            BlueprintItemStandPlacement.SnapshotItem(entry.DestinationItem, true) == entry.ProvidedProperties);

        private bool RemainingAvailable(Entry completed = null)
        {
            var pending = entries.Where(entry => entry.State == Result.Ready && !ReferenceEquals(entry, completed)).Select(entry => entry.Input).ToList();
            var held = new List<BlueprintItemStandPlacement.Supply>(otherSupplies);
            if (pending.Count > 0) held.Add(new BlueprintItemStandPlacement.Supply(pending));
            return BlueprintItemStandPlacement.SuppliesAvailable(player, held);
        }

        internal Result MoveNext()
        {
            Entry blocked = entries.FirstOrDefault(entry => entry.State == Result.Unchanged || entry.State == Result.Uncertain);
            if (blocked != null) return blocked.State;
            Entry entry = entries.FirstOrDefault(value => value.State == Result.Ready);
            if (entry == null) return Result.Transferred;
            Dictionary<ItemDrop.ItemData, int> sourceBefore = null, destinationBefore = null;
            uint revision = 0;
            bool moveCalled = false;
            try
            {
                if (!ContextValid() || !DeliveredItemsIntact() || !RemainingAvailable() || !source.ContainsItem(entry.Input.Item) || entry.Input.Item.m_shared.m_questItem ||
                    entry.Input.Item.m_stack < entry.Input.Amount ||
                    BlueprintItemStandPlacement.SnapshotItem(entry.Input.Item) != entry.Input.Snapshot ||
                    destination.GetItemAt(entry.X, entry.Y) != null)
                    throw new InvalidOperationException("Transfer context, source binding or empty cell changed.");
                sourceBefore = Counts(source); destinationBefore = Counts(destination);
                entry.SourceBefore = entry.Input.Item.m_stack;
                entry.BeforeObserved = true;
                revision = zdo.DataRevision;
                entry.QueuedBefore = player.IsEquipActionQueued(entry.Input.Item);
                entry.EquippedBefore = player.IsItemEquiped(entry.Input.Item) || entry.Input.Item.m_equipped;
                entry.Attempted = true; // Never repeat native calls after false or an exception.
                player.RemoveEquipAction(entry.Input.Item);
                player.UnequipItem(entry.Input.Item, false);
                if (!ContextValid() || player.IsEquipActionQueued(entry.Input.Item) || player.IsItemEquiped(entry.Input.Item) ||
                    entry.Input.Item.m_equipped || entry.Input.Item.m_stack != entry.SourceBefore ||
                    !source.ContainsItem(entry.Input.Item) || destination.GetItemAt(entry.X, entry.Y) != null ||
                    BlueprintItemStandPlacement.SnapshotItem(entry.Input.Item, true) != entry.ProvidedProperties)
                    throw new InvalidOperationException("Native unequip did not leave the provided item transferable.");
                moveCalled = true;
                entry.NativeReturn = destination.MoveItemToThis(source, entry.Input.Item, entry.Input.Amount, entry.X, entry.Y);
            }
            catch (Exception exception) { entry.Error = exception.Message; }
            try
            {
                entry.QueuedAfter = player && player.IsEquipActionQueued(entry.Input.Item);
                entry.EquippedAfter = player && (player.IsItemEquiped(entry.Input.Item) || entry.Input.Item.m_equipped);
                entry.SourcePresentAfter = source.ContainsItem(entry.Input.Item);
                entry.SourceAfter = entry.SourcePresentAfter ? entry.Input.Item.m_stack : 0;
                entry.DestinationItem = destination.GetItemAt(entry.X, entry.Y);
                entry.DestinationAfter = entry.DestinationItem?.m_stack ?? 0;
                if (sourceBefore == null || destinationBefore == null)
                    entry.State = Result.Unchanged;
                else if (entry.SourceAfter == entry.SourceBefore && entry.DestinationItem == null &&
                    SameCounts(sourceBefore, source, entry.Input.Item, entry.SourceBefore, false) &&
                    Counts(destination).Count == destinationBefore.Count &&
                    destinationBefore.All(value => destination.ContainsItem(value.Key) && value.Key.m_stack == value.Value) &&
                    DeliveredItemsIntact() &&
                    BlueprintItemStandPlacement.SnapshotItem(entry.Input.Item, true) == entry.ProvidedProperties)
                    entry.State = Result.Unchanged;
                else
                {
                    bool targetExact = moveCalled && DeliveredItemsIntact() && entry.DestinationItem != null && !ReferenceEquals(entry.DestinationItem, entry.Input.Item) &&
                        entry.DestinationAfter == entry.Input.Amount && !entry.DestinationItem.m_equipped &&
                        BlueprintItemStandPlacement.SnapshotItem(entry.DestinationItem, true) == entry.ProvidedProperties &&
                        destinationBefore.Count + 1 == destination.GetAllItems().Count &&
                        destinationBefore.All(value => destination.ContainsItem(value.Key) && value.Key.m_stack == value.Value);
                    bool exact = targetExact && RemainingAvailable(entry) &&
                        entry.SourceAfter == entry.SourceBefore - entry.Input.Amount &&
                        entry.Input.Item.m_stack == entry.SourceAfter && !entry.QueuedAfter && !entry.EquippedAfter &&
                        BlueprintItemStandPlacement.SnapshotItem(entry.Input.Item, true) == entry.ProvidedProperties &&
                        SameCounts(sourceBefore, source, entry.Input.Item, entry.SourceAfter, entry.SourceAfter == 0);
                    entry.LocalSaveConfirmed = targetExact && ContextValid() && zdo.DataRevision != revision &&
                        (uint)RevisionField.GetValue(container) == zdo.DataRevision &&
                        BlueprintStorageCapture.TryCapture(piece, player, out _);
                    entry.State = exact && entry.LocalSaveConfirmed ? Result.Transferred : Result.Uncertain;
                }
            }
            catch (Exception exception) { entry.Error = entry.Error ?? exception.Message; entry.State = Result.Uncertain; }
            return entry.State;
        }
    }
}
