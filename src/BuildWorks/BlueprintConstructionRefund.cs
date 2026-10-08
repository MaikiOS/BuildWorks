using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Tracks one native placement call. Durable server jobs require their own persisted transfer protocol.
    internal sealed class BlueprintConstructionRefund
    {
        private sealed class Input
        {
            internal ItemDrop.ItemData Original, Copy;
            internal int Before, Paid;
            internal bool Attempted, Refunded;
        }

        private readonly List<Input> inputs = new List<Input>();
        private readonly Inventory inventory;
        internal bool Recorded { get; private set; }
        internal string Error { get; private set; }
        private bool removalConfirmed, networkRemovalQueued;
        internal bool NetworkRemovalQueued => networkRemovalQueued;

        internal BlueprintConstructionRefund(Inventory inventory, Piece.Requirement[] requirements)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            var names = new HashSet<string>((requirements ?? Array.Empty<Piece.Requirement>())
                .Where(requirement => requirement?.m_resItem != null)
                .Select(requirement => requirement.m_resItem.m_itemData.m_shared.m_name), StringComparer.Ordinal);
            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                if (item.m_stack > 0 && names.Contains(item.m_shared.m_name))
                    inputs.Add(new Input { Original = item, Copy = item.Clone(), Before = item.m_stack });
        }

        internal void Complete()
        {
            if (Recorded || Error != null) return;
            try
            {
                foreach (Input input in inputs)
                {
                    int remaining = inventory.ContainsItem(input.Original) ? input.Original.m_stack : 0;
                    if (remaining < 0) throw new InvalidOperationException("Negative native inventory stack after placement.");
                    input.Paid = Math.Max(0, input.Before - remaining);
                }
                inputs.RemoveAll(input => input.Paid == 0);
                Recorded = true;
            }
            catch (Exception error)
            { Error = "Construction debit could not be verified: " + error.Message; }
        }

        internal void RecordRemoval(bool blockDrop, bool succeeded)
        {
            if (!succeeded || !blockDrop)
            {
                Error = "Construction removal did not confirm suppression of native resource drops; refund is blocked.";
                return;
            }
            removalConfirmed = true;
        }

        internal void RecordNetworkRemoval(bool succeeded)
        {
            if (succeeded) networkRemovalQueued = true;
            else Error = "Native network removal failed; refund is blocked.";
        }

        internal bool TryRefund(Vector3 position, bool networkObjectRemoved)
        {
            if (!Recorded || Error != null) return false;
            if (!removalConfirmed || !networkRemovalQueued || !networkObjectRemoved)
                return false;
            foreach (Input input in inputs)
            {
                if (input.Paid == 0 || input.Refunded) continue;
                if (input.Attempted) return false;
                input.Attempted = true;
                try
                {
                    ItemDrop dropped = ItemDrop.DropItem(input.Copy, input.Paid, position, Quaternion.identity);
                    if (!dropped) throw new InvalidOperationException("Native item drop returned no object.");
                    input.Refunded = true;
                }
                catch (Exception error)
                {
                    // DropItem can throw after spawning: retrying would create duplicate items.
                    Error = "Construction refund result is uncertain; automatic retry is blocked: " + error.Message;
                    Debug.LogError("BuildWorks " + Error);
                    return false;
                }
            }
            return true;
        }
    }
}
