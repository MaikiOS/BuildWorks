using System;
using System.Collections.Generic;
using System.Linq;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintStorageTransfer
    {
        // Container.Load replaces ItemData references even when only our marker revision changed.
        private void RebindPreparedReturnSources()
        {
            var saved = BlueprintStorageTransferJournal.Read(JournalPath);
            if (!returning || saved.Schema < 3 || saved.Prepared.Operation != OperationId ||
                saved.Prepared.Route != BlueprintStorageTransferJournal.Direction.ToPlayer || saved.InFlight.HasValue ||
                saved.Observations.Count != 0 || saved.Terminal || source.GetAllItems().Count != entries.Count)
                throw new InvalidOperationException("Only an untouched prepared return can rebind native source references.");
            var unique = new HashSet<ItemDrop.ItemData>();
            for (int i = 0; i < entries.Count; i++)
            {
                var row = saved.Prepared.Items[i];
                var item = source.GetItemAt(row.SourceX, row.SourceY);
                if (item == null || !unique.Add(item) || item.m_dropPrefab.name != row.Prefab || item.m_stack != row.SourceQuantity ||
                    row.SourceSnapshot != entries[i].Input.Snapshot || row.Properties != entries[i].ProvidedProperties || row.Amount != entries[i].Input.Amount)
                    throw new InvalidOperationException("Prepared return differs from its current native source cells.");
                entries[i].BindCurrent(item);
            }
            if (!ContextValid() || !DestinationReady() || !RemainingAvailable())
                throw new InvalidOperationException("Prepared return custody changed during native cache reload.");
        }

        // A recorded pair must be verified, never saved again through the clean replay path.
        private static BlueprintStorageTransfer ResumeCurrentReturn(BlueprintStorageTransferJournal.Snapshot expected,
            Player player, Piece piece, Container container, ZNetView view, ConstructionBinding binding)
        {
            BlueprintStorageTransferJournal writer = null;
            BlueprintStorageTransfer candidate = null;
            try
            {
                if (binding == null || !binding.Current()) throw new InvalidOperationException("Saved return parent changed.");
                writer = BlueprintStorageTransferJournal.ResumeReturn(expected);
                var saved = BlueprintStorageTransferJournal.Read(writer.Path);
                var plan = saved.Prepared;
                if (plan.ConstructionProject != binding.Project || plan.ConstructionPart != binding.Part ||
                    plan.ConstructionFingerprint != binding.Fingerprint || plan.Player != player.GetPlayerID() ||
                    plan.Creator != piece.GetCreator() || plan.PrefabHash != view.GetZDO().GetPrefab() ||
                    plan.World != ZNet.instance.GetWorldUID() || plan.Width != player.GetInventory().GetWidth() ||
                    plan.Height != player.GetInventory().GetHeight())
                    throw new InvalidOperationException("Return no longer matches the saved payer, target or inventory grid.");
                string marker = BlueprintStorageCapture.TransferMarker(view.GetZDO());
                if (marker != plan.Parent.ToString("N") && (!string.IsNullOrEmpty(marker) || saved.Observations.Count != 0))
                    throw new InvalidOperationException("Return marker is missing or belongs to another operation.");
                ReceiverCells(player.GetInventory());
                var entries = new List<Entry>();
                var unique = new HashSet<ItemDrop.ItemData>();
                for (int i = 0; i < plan.Items.Length; i++)
                {
                    var row = plan.Items[i]; bool completed = i < saved.Observations.Count;
                    var item = completed ? player.GetInventory().GetItemAt(row.X, row.Y) : container.GetInventory().GetItemAt(row.SourceX, row.SourceY);
                    if (item == null || !unique.Add(item) || item.m_dropPrefab.name != row.Prefab || item.m_stack != row.Amount ||
                        item.m_shared.m_maxStackSize < row.Amount || item.m_shared.m_questItem || item.m_equipped ||
                        player.IsItemEquiped(item) || player.IsEquipActionQueued(item) ||
                        BlueprintItemStandPlacement.SnapshotItem(item, true) != row.Properties ||
                        !completed && (item.m_stack != row.SourceQuantity || BlueprintItemStandPlacement.SnapshotItem(item) != row.SourceSnapshot))
                        throw new InvalidOperationException("Current return stack, cell or properties changed.");
                    var entry = new Entry(new BlueprintItemStandPlacement.Reservation(item, row.Amount,
                        BlueprintItemStandPlacement.SnapshotItem(item)), row.X, row.Y);
                    if (completed)
                    {
                        var observed = saved.Observations[i];
                        entry.State = Result.Transferred; entry.DestinationItem = item;
                        entry.Attempted = observed.Attempted; entry.BeforeObserved = observed.BeforeObserved;
                        entry.SourceBefore = observed.SourceBefore; entry.SourceAfter = observed.SourceAfter;
                        entry.DestinationAfter = observed.DestinationAfter; entry.SourcePresentAfter = observed.SourcePresent;
                        entry.LocalSaveConfirmed = observed.LocalSave; entry.NativeReturn = observed.NativeReturn;
                    }
                    entries.Add(entry);
                }
                var current = new ConstructionBinding(binding.Project, binding.Part, binding.Fingerprint,
                    () => binding.Current() && writer.OnlyUnresolvedReturn());
                candidate = new BlueprintStorageTransfer(plan.Operation, player, piece, container, view, entries,
                    Array.Empty<BlueprintItemStandPlacement.Supply>(), returning: true, marker: plan.Parent, construction: current);
                candidate.journal = writer;
                foreach (var entry in entries.Where(entry => entry.State == Result.Transferred))
                {
                    candidate.receiverBefore.Remove(entry.DestinationItem);
                    candidate.receiverProperties.Remove(entry.DestinationItem);
                }
                candidate.markerRequired = !string.IsNullOrEmpty(marker);
                if (!candidate.ContextValid() || !candidate.DestinationReady() || !candidate.RemainingAvailable())
                    throw new InvalidOperationException("Return custody or another unresolved journal prevents resuming.");
                if (saved.Observations.Count > 0)
                {
                    var last = saved.Observations.Last();
                    if (InventoryDigest(candidate.source) != last.SourceManifest || InventoryDigest(candidate.destination) != last.DestinationManifest ||
                        BytesDigest(candidate.zdo.GetByteArray(ZDOVars.s_items, null)) != last.ZdoHash)
                        throw new InvalidOperationException("Return inventories differ from their complete observed manifests.");
                }
                if (string.IsNullOrEmpty(marker))
                {
                    candidate.zdo.Set(BlueprintStorageCapture.TransferMarkerKey, plan.Parent.ToString("N"));
                    candidate.markerRequired = true; candidate.waitingForMarker = true;
                    candidate.markerStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                }
                if (!candidate.ContextValid()) throw new InvalidOperationException("Return context changed before resuming.");
                if (candidate.CompleteInMemory && !saved.Terminal) writer.CompleteRecoveredReturn();
                return candidate;
            }
            catch { candidate?.Dispose(); writer?.Dispose(); throw; }
        }
    }
}
