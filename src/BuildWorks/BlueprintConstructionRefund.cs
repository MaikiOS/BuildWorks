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
            internal Inventory Inventory;
            internal Guid Source;
            internal string Snapshot;
            internal int Before, Paid;
            internal bool Attempted, Refunded;
        }

        private readonly List<Input> inputs = new List<Input>();
        private readonly Inventory inventory;
        private readonly HashSet<string> names;
        private readonly List<(Guid Token, Inventory Inventory)> sources = new List<(Guid, Inventory)>();
        private readonly bool externalSources;
        private readonly bool captureOutcome;
        private bool completed;
        private BlueprintConstructionPaymentOutcome outcome;
        internal bool Recorded { get; private set; }
        internal string Error { get; private set; }
        private bool removalConfirmed, networkRemovalQueued;
        internal bool NetworkRemovalQueued => networkRemovalQueued;
        internal bool IsFor(Inventory source) => !externalSources && ReferenceEquals(inventory, source);
        internal (ItemDrop.ItemData Item, int Before, int Paid)[] DescribePaid()
        {
            if (!Recorded || Error != null) throw new InvalidOperationException("Construction debit is not verified.");
            if (externalSources) throw new InvalidOperationException("External debits require source-aware construction evidence.");
            return inputs.Where(input => input.Paid > 0).Select(input => (input.Copy.Clone(), input.Before, input.Paid)).ToArray();
        }

        internal (Guid Source, ItemDrop.ItemData Item, int Before, int Paid)[] DescribeSourcePaid()
        {
            if (!Recorded || Error != null) throw new InvalidOperationException("Construction debit is not verified.");
            return inputs.Where(input => input.Paid > 0)
                .Select(input => (input.Source, input.Copy.Clone(), input.Before, input.Paid)).ToArray();
        }

        internal BlueprintConstructionRefund(Inventory inventory, Piece.Requirement[] requirements)
            : this(inventory, requirements, false, false) { }

        internal BlueprintConstructionRefund(Inventory inventory, Piece.Requirement[] requirements, bool captureOutcome)
            : this(inventory, requirements, false, captureOutcome) { }

        private BlueprintConstructionRefund(Inventory inventory, Piece.Requirement[] requirements, bool external, bool captureOutcome)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            externalSources = external;
            this.captureOutcome = captureOutcome;
            names = new HashSet<string>((requirements ?? Array.Empty<Piece.Requirement>())
                .Where(requirement => requirement?.m_resItem != null)
                .Select(requirement => requirement.m_resItem.m_itemData.m_shared.m_name), StringComparer.Ordinal);
            AddSource(Guid.Empty, inventory);
        }

        // Tokens identify observations; authority and durable intent must be supplied by the provider coordinator.
        internal BlueprintConstructionRefund(Inventory inventory, Piece.Requirement[] requirements,
            IReadOnlyList<(Guid Token, Inventory Inventory)> external) : this(inventory, requirements, true, true)
        {
            if (external == null || external.Count < 1 || external.Count > 127)
                throw new ArgumentException("External debit observation requires one to 127 distinct sources.", nameof(external));
            var tokens = new HashSet<Guid> { Guid.Empty };
            foreach (var source in external)
            {
                if (!tokens.Add(source.Token) || source.Inventory == null ||
                    sources.Any(previous => ReferenceEquals(previous.Inventory, source.Inventory)))
                    throw new ArgumentException("External source identity or inventory is repeated or missing.", nameof(external));
                AddSource(source.Token, source.Inventory);
            }
            var items = new HashSet<ItemDrop.ItemData>();
            long snapshotCharacters = 0;
            foreach (Input input in inputs)
            {
                if (!items.Add(input.Original)) throw new ArgumentException("Sources share a recipe item reference.", nameof(external));
                snapshotCharacters += input.Snapshot.Length;
                if (snapshotCharacters > 4 * 1024 * 1024)
                    throw new ArgumentException("External debit snapshots exceed their evidence budget.", nameof(external));
            }
        }

        private void AddSource(Guid token, Inventory source)
        {
            sources.Add((token, source));
            foreach (ItemDrop.ItemData item in source.GetAllItems())
            {
                if (externalSources && (item == null || item.m_shared == null || item.m_stack < 1))
                    throw new ArgumentException("External observation requires valid inventory stacks.");
                if (item.m_stack > 0 && names.Contains(item.m_shared.m_name))
                {
                    if (externalSources && inputs.Count == 4096)
                        throw new ArgumentException("External debit observation exceeds its item budget.");
                    string snapshot = null;
                    if (externalSources) snapshot = BlueprintItemStandPlacement.SnapshotItem(item);
                    else if (captureOutcome)
                    {
                        // Optional partial evidence must not restrict the existing native transient refund path.
                        try { snapshot = BlueprintItemStandPlacement.SnapshotItem(item); } catch (Exception) { }
                    }
                    inputs.Add(new Input { Original = item, Copy = item.Clone(), Before = item.m_stack,
                        Snapshot = snapshot, Source = token, Inventory = source });
                }
            }
        }

        internal void Complete()
        {
            if (completed) return;
            completed = true;
            if (Recorded || Error != null) { if (captureOutcome) outcome = ObserveOutcome(); return; }
            try
            {
                if (externalSources)
                    foreach (var source in sources)
                    {
                        var current = new HashSet<ItemDrop.ItemData>();
                        foreach (ItemDrop.ItemData item in source.Inventory.GetAllItems())
                            if (item == null || !current.Add(item) || item.m_shared == null || item.m_stack < 1 ||
                                names.Contains(item.m_shared.m_name) && !inputs.Any(input =>
                                    ReferenceEquals(input.Inventory, source.Inventory) && ReferenceEquals(input.Original, item)))
                                throw new InvalidOperationException("External recipe inventory changed its item identities.");
                    }
                var items = new HashSet<ItemDrop.ItemData>();
                foreach (Input input in inputs)
                {
                    if (!items.Add(input.Original))
                        throw new InvalidOperationException("Repeated native inventory item reference before placement.");
                    int remaining = input.Inventory.ContainsItem(input.Original) ? input.Original.m_stack : 0;
                    if (remaining < 0) throw new InvalidOperationException("Negative native inventory stack after placement.");
                    if (externalSources && (remaining > input.Before ||
                        BlueprintItemStandPlacement.SnapshotItem(input.Original) != input.Snapshot))
                        throw new InvalidOperationException("External recipe item quantity or properties changed unexpectedly.");
                    input.Paid = Math.Max(0, input.Before - remaining);
                }
                if (!captureOutcome) inputs.RemoveAll(input => input.Paid == 0);
                Recorded = true;
            }
            catch (Exception error)
            { Error = "Construction debit could not be verified: " + error.Message; }
            if (captureOutcome) outcome = ObserveOutcome();
        }

        internal BlueprintConstructionPaymentOutcome CaptureOutcome()
        {
            var captured = outcome ?? ObserveOutcome();
            return new BlueprintConstructionPaymentOutcome { ExplicitSources = captured.ExplicitSources,
                Sources = captured.Sources.Select(source => new BlueprintConstructionPaymentOutcome.Source {
                    Token = source.Token, Exact = source.Exact,
                    Items = source.Items.Select(item => new BlueprintConstructionPaymentOutcome.Item {
                        Properties = item.Properties, Before = item.Before, After = item.After }).ToArray() }).ToArray() };
        }

        private BlueprintConstructionPaymentOutcome ObserveOutcome()
        {
            var result = new BlueprintConstructionPaymentOutcome { ExplicitSources = externalSources };
            result.Sources = sources.Select(source => {
                var tracked = inputs.Where(input => ReferenceEquals(input.Inventory, source.Inventory)).ToArray();
                var observation = new BlueprintConstructionPaymentOutcome.Source { Token = source.Token,
                    Items = tracked.Select(input => new BlueprintConstructionPaymentOutcome.Item {
                        Properties = input.Snapshot, Before = input.Before }).ToArray() };
                try
                {
                    if (!completed) return observation;
                    var current = new HashSet<ItemDrop.ItemData>();
                    foreach (var item in source.Inventory.GetAllItems())
                        if (item == null || !current.Add(item) || item.m_shared == null || item.m_stack < 1 ||
                            names.Contains(item.m_shared.m_name) && !tracked.Any(input => ReferenceEquals(input.Original, item)))
                            throw new InvalidOperationException("Recipe item identities changed.");
                    for (int i = 0; i < tracked.Length; ++i)
                    {
                        var input = tracked[i];
                        int remaining = current.Contains(input.Original) ? input.Original.m_stack : 0;
                        if (input.Snapshot == null || remaining > input.Before || BlueprintItemStandPlacement.SnapshotItem(input.Original) != input.Snapshot)
                            throw new InvalidOperationException("Recipe item properties or quantity changed.");
                        observation.Items[i].After = remaining;
                    }
                    observation.Exact = true;
                }
                catch (Exception)
                { foreach (var item in observation.Items) item.After = -1; }
                return observation;
            }).ToArray();
            return result;
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
            // shortcut: external observations need all-source persistence and sealed payout before provider placement is enabled.
            if (externalSources) return false;
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
