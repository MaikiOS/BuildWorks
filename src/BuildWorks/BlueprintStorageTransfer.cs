using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Local native transfer primitive. World placement stays blocked until durable recovery/server acknowledgement exists.
    internal sealed class BlueprintStorageTransfer : IDisposable
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo ViewField = typeof(Container).GetField("m_nview", Private);
        private static readonly FieldInfo LoadingField = typeof(Container).GetField("m_loading", Private);
        private static readonly FieldInfo RevisionField = typeof(Container).GetField("m_lastRevision", Private);
        private static readonly MethodInfo Access = typeof(Container).GetMethod("CheckAccess", Private);

        internal enum Result { Ready, Unchanged, Transferred, Uncertain }
        internal enum RecoveryState { NeedsReconciliation, ExactDelivered }
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
        private readonly bool returning;
        private readonly Dictionary<ItemDrop.ItemData, int> receiverBefore;
        private readonly Dictionary<ItemDrop.ItemData, string> receiverProperties;
        private readonly int receiverWidth, receiverHeight;
        private readonly string targetMarker;
        private bool markerRequired, waitingForMarker;
        private long markerStarted;
        private bool cancelling;
        private BlueprintStorageTransferJournal journal;
        internal string JournalPath => journal?.Path;
        internal string JournalError { get; private set; }
        internal readonly Guid OperationId;
        internal IReadOnlyList<Entry> Entries => entries;
        internal bool CompleteInMemory => !cancelling && JournalError == null && entries.All(entry => entry.State == Result.Transferred);

        private BlueprintStorageTransfer(Guid operationId, Player player, Piece piece, Container container,
            ZNetView view, List<Entry> entries, IEnumerable<BlueprintItemStandPlacement.Supply> otherSupplies, bool returning = false, Guid marker = default(Guid))
        {
            OperationId = operationId; this.player = player; this.piece = piece; this.container = container; this.view = view;
            this.returning = returning;
            targetMarker = (marker == Guid.Empty ? operationId : marker).ToString("N"); markerRequired = returning;
            source = returning ? container.GetInventory() : player.GetInventory();
            destination = returning ? player.GetInventory() : container.GetInventory(); zdo = view.GetZDO(); targetId = zdo.m_uid;
            network = ZNet.instance; objects = ZDOMan.instance; worldId = network.GetWorld().m_uid;
            this.entries = Array.AsReadOnly(entries.ToArray());
            this.otherSupplies = Array.AsReadOnly(otherSupplies.ToArray());
            if (returning)
            {
                receiverBefore = Counts(destination);
                receiverProperties = receiverBefore.Keys.ToDictionary(item => item, item => BlueprintItemStandPlacement.SnapshotItem(item));
                receiverWidth = destination.GetWidth(); receiverHeight = destination.GetHeight();
            }
        }

        internal bool TryBeginReturn(out BlueprintStorageTransfer transfer, out string error)
        {
            transfer = null; error = null;
            try
            {
                Guid operationId = BlueprintStorageTransferJournal.ReturnOperationId(OperationId);
                if (!returning && BlueprintStorageTransferJournal.HasReturnEvidence(OperationId)) cancelling = true;
                if (operationId == Guid.Empty || operationId == OperationId || returning || cancelling || JournalError != null || !ContextValid() ||
                    entries.Any(entry => entry.State == Result.Uncertain) || !BlueprintStorageCapture.PrefabGrid(piece.gameObject).HasValue ||
                    (uint)RevisionField.GetValue(container) != zdo.DataRevision || !BlueprintStorageCapture.TryCapture(piece, player, out _))
                    throw new InvalidOperationException("Return requires a verified same-session transfer and current container access.");
                var persisted = BlueprintStorageTransferJournal.Read(JournalPath);
                var delivered = entries.Where(entry => entry.State == Result.Transferred).ToArray();
                if (persisted.Schema != 3 || persisted.Prepared.Operation != OperationId || persisted.Prepared.Route != BlueprintStorageTransferJournal.Direction.ToContainer ||
                    persisted.Prepared.World != worldId || persisted.Prepared.Player != player.GetPlayerID() ||
                    persisted.Prepared.TargetUser != targetId.UserID || persisted.Prepared.TargetId != targetId.ID ||
                    persisted.Prepared.Creator != piece.GetCreator() || persisted.Prepared.PrefabHash != zdo.GetPrefab() ||
                    persisted.Prepared.Items.Length != entries.Count || persisted.InFlight.HasValue ||
                    persisted.Observations.Any(value => value.State == (int)Result.Uncertain) ||
                    persisted.Observations.Count(value => value.State == (int)Result.Transferred) != delivered.Length || delivered.Length == 0 ||
                    !BlueprintStorageTransferJournal.CanStartReturn(OperationId, worldId, player.GetPlayerID(), OperationId) ||
                    container.GetInventory().GetAllItems().Count != delivered.Length)
                    throw new InvalidOperationException("Transfer evidence or container contents need reconciliation before return.");
                transfer = BeginReturn(persisted, delivered, recovered: false); return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        internal static RecoveryState InspectRecovery(string path, Player player, GameObject placed, out string reason)
        {
            try { ReadRecovery(path, player, placed, out _); reason = null; return RecoveryState.ExactDelivered; }
            catch (Exception exception) { reason = exception.Message; return RecoveryState.NeedsReconciliation; }
        }

        // Explicit recovery only; never called by journal scanning, rollback guards or automatic startup.
        internal static bool TryBeginRecoveredReturn(string path, Player player, GameObject placed,
            out BlueprintStorageTransfer transfer, out string error)
        {
            transfer = null; error = null;
            try
            {
                var binding = ReadRecovery(path, player, placed, out var persisted);
                transfer = binding.BeginReturn(persisted, binding.entries.ToArray(), recovered: true); return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        private static BlueprintStorageTransfer ReadRecovery(string path, Player player, GameObject placed,
            out BlueprintStorageTransferJournal.Snapshot persisted)
        {
            // Only this installation's retained file can claim its deterministic child.
            if (System.IO.Path.GetFullPath(path) != System.IO.Path.Combine(System.IO.Path.GetFullPath(BlueprintStorageTransferJournal.Root),
                System.IO.Path.GetFileName(path))) throw new InvalidOperationException("Recovery journal is outside the active transfer directory.");
            persisted = BlueprintStorageTransferJournal.Read(path);
            var plan = persisted.Prepared;
            if (persisted.Schema != 3 || plan.Route != BlueprintStorageTransferJournal.Direction.ToContainer || persisted.InFlight.HasValue ||
                persisted.Observations.Count == 0 || persisted.Observations.Any(value => value.State != (int)Result.Transferred))
                throw new InvalidOperationException("Recovery requires a fully observed forward prefix; legacy, pending or uncertain evidence stays held.");
            Piece piece = placed ? placed.GetComponent<Piece>() : null;
            Container container = placed ? placed.GetComponent<Container>() : null;
            var view = container ? ViewField?.GetValue(container) as ZNetView : null;
            var grid = BlueprintStorageCapture.PrefabGrid(placed);
            if (!player || !piece || !container || !view || !view.IsValid() || view.GetZDO() == null ||
                !grid.HasValue || grid.Value.Width != plan.Width || grid.Value.Height != plan.Height ||
                container.GetInventory()?.GetWidth() != plan.Width || container.GetInventory().GetHeight() != plan.Height ||
                !ZNet.instance || ZNet.instance.GetWorld() == null || ZDOMan.instance == null ||
                plan.World != ZNet.instance.GetWorld().m_uid || plan.Player != player.GetPlayerID() || plan.Creator != piece.GetCreator() ||
                BlueprintStorageCapture.TransferMarker(view.GetZDO()) != plan.TargetMarker.ToString("N") ||
                plan.PrefabHash != view.GetZDO().GetPrefab() || plan.PieceName != piece.gameObject.name ||
                !BlueprintStorageTransferJournal.CanStartReturn(plan.Operation, plan.World, plan.Player, plan.TargetMarker) ||
                !OwnedAccess(player, piece, container, view))
                throw new InvalidOperationException("Recovery world, player, target, prefab, grid or operation identity changed.");
            var delivered = new List<Entry>();
            for (int i = 0; i < persisted.Observations.Count; i++)
            {
                var row = plan.Items[i]; var item = container.GetInventory().GetItemAt(row.X, row.Y);
                if (item == null || item.m_dropPrefab?.name != row.Prefab || item.m_stack != row.Amount ||
                    item.m_shared.m_maxStackSize < 1 || item.m_stack > item.m_shared.m_maxStackSize || item.m_shared.m_questItem ||
                    item.m_equipped || player.IsItemEquiped(item) || player.IsEquipActionQueued(item) ||
                    BlueprintItemStandPlacement.SnapshotItem(item, true) != row.Properties)
                    throw new InvalidOperationException("Recovery cannot bind the observed provided item in its recorded cell.");
                delivered.Add(new Entry(new BlueprintItemStandPlacement.Reservation(item, row.Amount, BlueprintItemStandPlacement.SnapshotItem(item)), row.X, row.Y) {
                    State = Result.Transferred, DestinationItem = item });
            }
            var binding = new BlueprintStorageTransfer(plan.Operation, player, piece, container, view, delivered,
                Array.Empty<BlueprintItemStandPlacement.Supply>()) { markerRequired = true };
            if (!binding.MatchesRecovery(persisted)) throw new InvalidOperationException("Current inventories do not match the recorded custody; nothing will be moved.");
            var occupied = ReceiverCells(binding.source);
            if (checked(binding.source.GetWidth() * binding.source.GetHeight()) - occupied.Count < delivered.Count)
                throw new InvalidOperationException("Recovery return needs separate empty player cells; nothing is merged or dropped.");
            return binding;
        }

        private bool MatchesRecovery(BlueprintStorageTransferJournal.Snapshot persisted)
        {
            var last = persisted.Observations.Last();
            return ContextValid() && !returning && BlueprintStorageCapture.TryCapture(piece, player, out _) &&
                container.GetInventory().GetAllItems().Count == entries.Count &&
                (uint)RevisionField.GetValue(container) == zdo.DataRevision &&
                InventoryDigest(source) == last.SourceManifest && InventoryDigest(destination) == last.DestinationManifest &&
                BytesDigest(zdo.GetByteArray(ZDOVars.s_items, null)) == last.ZdoHash && ContextValid() &&
                (uint)RevisionField.GetValue(container) == zdo.DataRevision;
        }

        private BlueprintStorageTransfer BeginReturn(BlueprintStorageTransferJournal.Snapshot persisted, Entry[] delivered, bool recovered)
        {
            Guid operationId = BlueprintStorageTransferJournal.ReturnOperationId(OperationId);
            Inventory receiver = player.GetInventory();
            int width = receiver.GetWidth(), height = receiver.GetHeight();
            if (width < 1 || width > 256 || height < 1 || height > 256) throw new InvalidOperationException("Unsupported return inventory grid.");
            var occupied = ReceiverCells(receiver);
            var cells = new List<(int X, int Y)>();
            for (int y = 0; y < height && cells.Count < delivered.Length; y++)
                for (int x = 0; x < width && cells.Count < delivered.Length; x++)
                    if (!occupied.Contains(y * width + x)) cells.Add((x, y));
            if (cells.Count != delivered.Length) throw new InvalidOperationException("Return needs separate empty player inventory cells; no item is dropped or merged.");
            var returns = new List<Entry>();
            for (int i = 0; i < delivered.Length; i++)
            {
                Entry original = delivered[i]; ItemDrop.ItemData item = original.DestinationItem;
                if (item == null || !ReferenceEquals(container.GetInventory().GetItemAt(original.X, original.Y), item) ||
                    item.m_stack != original.Input.Amount || item.m_shared.m_maxStackSize < 1 || item.m_stack > item.m_shared.m_maxStackSize ||
                    item.m_shared.m_questItem || item.m_equipped ||
                    player.IsItemEquiped(item) || player.IsEquipActionQueued(item) ||
                    original.Input.Amount != persisted.Prepared.Items[i].Amount || original.X != persisted.Prepared.Items[i].X || original.Y != persisted.Prepared.Items[i].Y ||
                    item.m_dropPrefab.name != persisted.Prepared.Items[i].Prefab)
                    throw new InvalidOperationException("Delivered native item identity or quantity changed; explicit reconciliation is required.");
                // Return its current attributes, including damage; never reconstruct the archived/initial state.
                returns.Add(new Entry(new BlueprintItemStandPlacement.Reservation(item, item.m_stack,
                    BlueprintItemStandPlacement.SnapshotItem(item)), cells[i].X, cells[i].Y));
            }
            var candidate = new BlueprintStorageTransfer(operationId, player, piece, container, view, returns,
                Array.Empty<BlueprintItemStandPlacement.Supply>(), returning: true, marker: OperationId);
            if (!candidate.ContextValid() || !candidate.DestinationReady() || !candidate.RemainingAvailable() || recovered && !MatchesRecovery(persisted))
                throw new InvalidOperationException("Return inventories changed while preparing.");
            cancelling = true; // Even a partial/uncertain child Prepare must never allow forward continuation.
            candidate.journal = BlueprintStorageTransferJournal.Begin(BlueprintStorageTransferJournal.Root, new BlueprintStorageTransferJournal.Plan {
                Operation = operationId, Parent = OperationId, Route = BlueprintStorageTransferJournal.Direction.ToPlayer,
                World = worldId, Player = player.GetPlayerID(), Creator = piece.GetCreator(), TargetUser = targetId.UserID, TargetId = targetId.ID,
                Width = width, Height = height, PieceName = piece.gameObject.name, PrefabHash = zdo.GetPrefab(),
                Items = returns.Select(entry => new BlueprintStorageTransferJournal.Item { Prefab = entry.Input.Item.m_dropPrefab.name,
                    SourceX = entry.Input.Item.m_gridPos.x, SourceY = entry.Input.Item.m_gridPos.y, SourceQuantity = entry.Input.Item.m_stack,
                    Amount = entry.Input.Amount, X = entry.X, Y = entry.Y, SourceSnapshot = entry.Input.Snapshot, Properties = entry.ProvidedProperties }).ToArray() });
            return candidate;
        }

        private static HashSet<int> ReceiverCells(Inventory receiver)
        {
            int width = receiver.GetWidth(), height = receiver.GetHeight();
            if (width < 1 || width > 256 || height < 1 || height > 256) throw new InvalidOperationException("Unsupported return inventory grid.");
            var occupied = new HashSet<int>();
            foreach (var value in Counts(receiver))
                if (value.Value < 1 || value.Key.m_gridPos.x < 0 || value.Key.m_gridPos.x >= width || value.Key.m_gridPos.y < 0 || value.Key.m_gridPos.y >= height ||
                    !occupied.Add(value.Key.m_gridPos.y * width + value.Key.m_gridPos.x)) throw new InvalidOperationException("Player inventory grid needs reconciliation before return.");
            return occupied;
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
                if (!string.IsNullOrEmpty(BlueprintStorageCapture.TransferMarker(candidate.zdo)) ||
                    BlueprintStorageTransferJournal.HasUnresolved(candidate.worldId))
                    throw new InvalidOperationException("Target container already has unresolved transfer evidence.");
                candidate.journal = BlueprintStorageTransferJournal.Begin(BlueprintStorageTransferJournal.Root, new BlueprintStorageTransferJournal.Plan {
                    Operation = operationId, World = candidate.worldId, Player = player.GetPlayerID(), Creator = piece.GetCreator(),
                    TargetUser = candidate.targetId.UserID, TargetId = candidate.targetId.ID, Width = grid.Value.Width, Height = grid.Value.Height,
                    PieceName = piece.gameObject.name, PrefabHash = candidate.zdo.GetPrefab(),
                    Paid = payment.DescribePaid().Select(debit => new BlueprintStorageTransferJournal.Payment {
                        Prefab = debit.Item.m_dropPrefab.name, Properties = BlueprintItemStandPlacement.SnapshotItem(debit.Item),
                        Before = debit.Before, Paid = debit.Paid, X = debit.Item.m_gridPos.x, Y = debit.Item.m_gridPos.y }).ToArray(),
                    Items = entries.Select(entry => new BlueprintStorageTransferJournal.Item { Prefab = entry.Input.Item.m_dropPrefab.name,
                        SourceX = entry.Input.Item.m_gridPos.x, SourceY = entry.Input.Item.m_gridPos.y, SourceQuantity = entry.Input.Item.m_stack,
                        Amount = entry.Input.Amount, X = entry.X, Y = entry.Y, SourceSnapshot = entry.Input.Snapshot, Properties = entry.ProvidedProperties }).ToArray() });
                try
                {
                    if (!candidate.ContextValid() || !string.IsNullOrEmpty(BlueprintStorageCapture.TransferMarker(candidate.zdo)))
                        throw new InvalidOperationException("Target identity changed before marking the prepared transfer.");
                    candidate.markerRequired = true;
                    candidate.zdo.Set(BlueprintStorageCapture.TransferMarkerKey, candidate.targetMarker);
                    if (!candidate.ContextValid()) throw new InvalidOperationException("Persistent target marker could not be verified.");
                    candidate.waitingForMarker = true; candidate.markerStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                }
                catch { candidate.Dispose(); throw; }
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
            ReferenceEquals(player.GetInventory(), returning ? destination : source) && ReferenceEquals(container.GetInventory(), returning ? source : destination) &&
            ReferenceEquals(view.GetZDO(), zdo) && zdo.m_uid.Equals(targetId) &&
            (!markerRequired || BlueprintStorageCapture.TransferMarker(zdo) == targetMarker);

        // Read backing lists into detached maps before any callbacks can mutate them.
        private static Dictionary<ItemDrop.ItemData, int> Counts(Inventory inventory)
        {
            var items = inventory.GetAllItems().ToArray();
            if (items.Length > 4096) throw new InvalidOperationException("Inventory exceeds the native observation budget.");
            var result = new Dictionary<ItemDrop.ItemData, int>();
            foreach (var item in items)
            {
                if (item == null || item.m_stack < 0 || item.m_stack > ushort.MaxValue) throw new InvalidOperationException("Invalid live inventory quantity.");
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

        private bool ReceiverItemsIntact() => !returning || destination.GetWidth() == receiverWidth && destination.GetHeight() == receiverHeight &&
            receiverBefore.All(value => destination.ContainsItem(value.Key) && value.Key.m_stack == value.Value &&
            BlueprintItemStandPlacement.SnapshotItem(value.Key) == receiverProperties[value.Key]);

        private bool DestinationReady() => DeliveredItemsIntact() && ReceiverItemsIntact() &&
            destination.GetAllItems().Count == (returning ? receiverBefore.Count : 0) + entries.Count(entry => entry.State == Result.Transferred) &&
            (!returning || source.GetAllItems().Count == entries.Count(entry => entry.State != Result.Transferred)) &&
            (uint)RevisionField.GetValue(container) == zdo.DataRevision && BlueprintStorageCapture.TryCapture(piece, player, out _);

        private bool RemainingAvailable(Entry completed = null)
        {
            if (returning) return entries.Where(entry => entry.State == Result.Ready && !ReferenceEquals(entry, completed)).All(entry =>
                source.ContainsItem(entry.Input.Item) && entry.Input.Item.m_stack == entry.Input.Amount &&
                entry.Input.Item.m_shared.m_maxStackSize >= 1 && entry.Input.Amount <= entry.Input.Item.m_shared.m_maxStackSize &&
                !entry.Input.Item.m_shared.m_questItem && !entry.Input.Item.m_equipped &&
                !player.IsItemEquiped(entry.Input.Item) && !player.IsEquipActionQueued(entry.Input.Item) && destination.GetItemAt(entry.X, entry.Y) == null &&
                BlueprintItemStandPlacement.SnapshotItem(entry.Input.Item) == entry.Input.Snapshot);
            var pending = entries.Where(entry => entry.State == Result.Ready && !ReferenceEquals(entry, completed)).Select(entry => entry.Input).ToList();
            var held = new List<BlueprintItemStandPlacement.Supply>(otherSupplies);
            if (pending.Count > 0) held.Add(new BlueprintItemStandPlacement.Supply(pending));
            return BlueprintItemStandPlacement.SuppliesAvailable(player, held);
        }

        internal Result MoveNext()
        {
            if (JournalError != null) return Result.Uncertain;
            if (!returning && BlueprintStorageTransferJournal.HasReturnEvidence(OperationId)) cancelling = true;
            if (cancelling) return Result.Unchanged;
            if (waitingForMarker)
            {
                try
                {
                    if (!ContextValid()) throw new InvalidOperationException("Marked transfer context changed before native inventory synchronization.");
                    if ((uint)RevisionField.GetValue(container) != zdo.DataRevision || !BlueprintStorageCapture.TryCapture(piece, player, out _))
                    {
                        if ((System.Diagnostics.Stopwatch.GetTimestamp() - markerStarted) / (double)System.Diagnostics.Stopwatch.Frequency > 30)
                            throw new InvalidOperationException("Native container synchronization timed out; prepared transfer remains held.");
                        return Result.Ready; // Preparation only: no BeforeMove, unequip or item mutation has happened.
                    }
                    waitingForMarker = false;
                    return Result.Ready;
                }
                catch (Exception exception) { JournalError = exception.Message; return Result.Uncertain; }
            }
            Entry blocked = entries.FirstOrDefault(entry => entry.State == Result.Unchanged || entry.State == Result.Uncertain);
            if (blocked != null) return blocked.State;
            Entry entry = entries.FirstOrDefault(value => value.State == Result.Ready);
            if (entry == null) return Result.Transferred;
            Dictionary<ItemDrop.ItemData, int> sourceBefore = null, destinationBefore = null;
            uint revision = 0;
            bool moveCalled = false;
            try
            {
                if (!ContextValid() || !DestinationReady() || !RemainingAvailable() || !source.ContainsItem(entry.Input.Item) || entry.Input.Item.m_shared.m_questItem ||
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
                try
                {
                    journal.BeforeMove(new BlueprintStorageTransferJournal.Attempt { Index = entries.ToList().IndexOf(entry), SourceBefore = entry.SourceBefore,
                        SourceX = entry.Input.Item.m_gridPos.x, SourceY = entry.Input.Item.m_gridPos.y, Queued = entry.QueuedBefore, Equipped = entry.EquippedBefore,
                        EmptyCell = destination.GetItemAt(entry.X, entry.Y) == null, Revision = revision, ContainerRevision = (uint)RevisionField.GetValue(container),
                        SourceSnapshot = BlueprintItemStandPlacement.SnapshotItem(entry.Input.Item), Properties = entry.ProvidedProperties,
                        SourceManifest = InventoryDigest(source), DestinationManifest = InventoryDigest(destination), ZdoHash = BytesDigest(zdo.GetByteArray(ZDOVars.s_items, null)) });
                }
                catch (Exception error) { JournalError = error.Message; throw; }
                entry.Attempted = true; // Never repeat native calls after false or an exception.
                player.RemoveEquipAction(entry.Input.Item);
                player.UnequipItem(entry.Input.Item, false);
                if (!ContextValid() || !DestinationReady() || returning && !RemainingAvailable() || zdo.DataRevision != revision || player.IsEquipActionQueued(entry.Input.Item) || player.IsItemEquiped(entry.Input.Item) ||
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
                    bool targetExact = moveCalled && DeliveredItemsIntact() && ReceiverItemsIntact() && entry.DestinationItem != null && !ReferenceEquals(entry.DestinationItem, entry.Input.Item) &&
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
            if (JournalError != null) return Result.Uncertain;
            try
            {
                var observed = new BlueprintStorageTransferJournal.Observation { Index = entries.ToList().IndexOf(entry), State = (int)entry.State,
                    SourceBefore = entry.SourceBefore, SourceAfter = entry.SourceAfter, DestinationAfter = entry.DestinationAfter, SourceObjectStack = entry.Input.Item.m_stack,
                    Attempted = entry.Attempted, BeforeObserved = entry.BeforeObserved, SourcePresent = entry.SourcePresentAfter, LocalSave = entry.LocalSaveConfirmed,
                    QueuedBefore = entry.QueuedBefore, EquippedBefore = entry.EquippedBefore, QueuedAfter = entry.QueuedAfter, EquippedAfter = entry.EquippedAfter,
                    NativeReturn = entry.NativeReturn, Error = entry.Error, SourceX = entry.Input.Item.m_gridPos.x, SourceY = entry.Input.Item.m_gridPos.y };
                // Evidence collection may fail too. Preserve the known custody observations and mark the rest unknown.
                try
                {
                    observed.SourceProperties = EvidenceProperties(entry.Input.Item, entry.ProvidedProperties, observed);
                    observed.DestinationProperties = entry.DestinationItem == null ? null : EvidenceProperties(entry.DestinationItem, entry.ProvidedProperties, observed);
                    observed.SourceManifest = InventoryDigest(source); observed.DestinationManifest = InventoryDigest(destination);
                    observed.Revision = zdo.DataRevision; observed.ContainerRevision = (uint)RevisionField.GetValue(container);
                    observed.ZdoHash = BytesDigest(zdo.GetByteArray(ZDOVars.s_items, null));
                }
                catch (Exception error) { observed.State = (int)Result.Uncertain; observed.Error = observed.Error ?? error.Message; entry.State = Result.Uncertain; }
                observed.Error = ErrorText(observed.Error);
                entry.State = (Result)observed.State;
                journal.Observed(observed);
            }
            catch (Exception error) { JournalError = error.Message; return Result.Uncertain; }
            return entry.State;
        }

        private static string BytesDigest(byte[] bytes, int maximum = BlueprintForeignPartData.MaximumContainerBytes)
        {
            if (bytes == null) return "missing";
            if (bytes.Length > maximum) throw new InvalidOperationException("Inventory evidence exceeds the native byte budget.");
            using (var hash = System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }
        private static string ErrorText(string text)
        {
            if (text == null || text.Length <= 1024) return text;
            int count = char.IsHighSurrogate(text[1023]) ? 1023 : 1024;
            return text.Substring(0, count);
        }
        private static string EvidenceProperties(ItemDrop.ItemData item, string expected, BlueprintStorageTransferJournal.Observation observed)
        {
            string properties = BlueprintItemStandPlacement.SnapshotItem(item, true);
            if (System.Text.Encoding.UTF8.GetByteCount(properties) <= Math.Max(System.Text.Encoding.UTF8.GetByteCount(expected), 80)) return properties;
            observed.State = (int)Result.Uncertain;
            observed.Error = "Unexpected oversized properties: only their SHA256 is retained in this observation; initial provided properties remain in preparation.";
            return "sha256:" + BytesDigest(System.Text.Encoding.UTF8.GetBytes(properties), 8 * 1024 * 1024);
        }
        private static string InventoryDigest(Inventory inventory)
        {
            var items = Counts(inventory); long budget = 0;
            using (var hash = System.Security.Cryptography.SHA256.Create())
            {
                foreach (var item in items.OrderBy(value => value.Key.m_gridPos.y).ThenBy(value => value.Key.m_gridPos.x))
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(item.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + BlueprintItemStandPlacement.SnapshotItem(item.Key) + "\n");
                    budget += bytes.Length;
                    if (budget > 8 * 1024 * 1024) throw new InvalidOperationException("Inventory manifest exceeds its evidence budget.");
                    hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
                }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0); return BitConverter.ToString(hash.Hash).Replace("-", "");
            }
        }
        public void Dispose() { JournalError = JournalError ?? "Transfer journal closed; reconciliation is required before reuse."; journal?.Dispose(); }
    }
}
