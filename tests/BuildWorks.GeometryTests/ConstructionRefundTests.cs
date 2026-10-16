using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks;

internal static class ConstructionRefundTests
{
    internal static void Run()
    {
        var inventory = new Inventory();
        var wood = Item("wood", 10, 2, 14f, "owner", "custom");
        var otherWood = Item("wood", 3, 1, 6f, "other", "different");
        var unrelated = Item("iron", 7, 4, 22f, "iron owner", "iron data");
        inventory.Items.AddRange(new[] { wood, otherWood, unrelated });
        var recipe = new[] { new Piece.Requirement { m_resItem = new ItemDrop { m_itemData = wood } } };

        ItemDrop.Drops.Clear();
        var free = new BlueprintConstructionRefund(inventory, recipe);
        free.Complete();
        free.RecordRemoval(true, true);
        free.RecordNetworkRemoval(true);
        Require(free.Recorded && free.TryRefund(default, true) && ItemDrop.Drops.Count == 0,
            "No-cost placement minted recipe resources");

        var payment = new BlueprintConstructionRefund(inventory, recipe);
        wood.m_stack -= 4;
        inventory.Items.Remove(otherWood);
        unrelated.m_stack = 1;
        payment.Complete();
        var described = payment.DescribePaid();
        Require(described.Length == 2 && described[0].Before == 10 && described[0].Paid == 4 && described[1].Paid == 3 &&
            described[0].Item.m_quality == 2 && described[0].Item.m_durability == 14f, "Debit evidence changed provided properties or amounts");
        described[0].Item.m_customData["key"] = "mutated exported copy";
        Require(payment.DescribePaid()[0].Item.m_customData["key"] == "custom", "Exported debit evidence aliases the refund snapshot");
        wood.m_stack = 0;
        wood.m_customData["key"] = "changed after debit";
        payment.Complete();
        Require(!payment.TryRefund(default, true), "A removal request without native confirmation refunded resources");
        payment.RecordRemoval(blockDrop: true, succeeded: true);
        payment.RecordNetworkRemoval(true);
        Require(!payment.TryRefund(default, false), "Local GO destruction with surviving network identity refunded resources");
        Require(payment.TryRefund(default, true) && payment.TryRefund(default, true) && ItemDrop.Drops.Count == 2,
            "Refund was lost or repeated");
        Require(ItemDrop.Drops[0].m_stack == 4 && ItemDrop.Drops[1].m_stack == 3,
            "Refund used recipe amounts or included unrelated inventory changes");
        Require(ItemDrop.Drops[0].m_quality == 2 && ItemDrop.Drops[0].m_durability == 14f &&
            ItemDrop.Drops[0].m_crafterName == "owner" && ItemDrop.Drops[0].m_customData["key"] == "custom" &&
            ItemDrop.Drops[1].m_quality == 1 && ItemDrop.Drops[1].m_customData["key"] == "different",
            "Supplied item properties were repaired, merged or replaced");

        var repeated = Item("wood", 10, 2, 14f, "owner", "custom");
        var damagedInventory = new Inventory();
        damagedInventory.Items.AddRange(new[] { repeated, repeated });
        var duplicatePayment = new BlueprintConstructionRefund(damagedInventory, recipe);
        repeated.m_stack -= 4;
        duplicatePayment.Complete();
        duplicatePayment.Complete();
        duplicatePayment.RecordRemoval(true, true);
        duplicatePayment.RecordNetworkRemoval(true);
        int dropsBefore = ItemDrop.Drops.Count;
        Require(!duplicatePayment.Recorded && duplicatePayment.Error != null &&
            !duplicatePayment.TryRefund(default, true) && !duplicatePayment.TryRefund(default, true) &&
            ItemDrop.Drops.Count == dropsBefore,
            "A repeated source stack reference refunded the same debit twice");

        var twinA = Item("wood", 10, 2, 14f, "owner", "custom");
        var twinB = twinA.Clone();
        var distinctInventory = new Inventory();
        distinctInventory.Items.AddRange(new[] { twinA, twinB });
        var distinctPayment = new BlueprintConstructionRefund(distinctInventory, recipe);
        twinA.m_stack -= 4;
        twinB.m_stack -= 2;
        distinctPayment.Complete();
        distinctPayment.RecordRemoval(true, true);
        distinctPayment.RecordNetworkRemoval(true);
        Require(distinctPayment.Recorded && distinctPayment.Error == null &&
            distinctPayment.TryRefund(default, true) && distinctPayment.TryRefund(default, true) &&
            ItemDrop.Drops.Count == dropsBefore + 2 &&
            ItemDrop.Drops[dropsBefore].m_stack == 4 && ItemDrop.Drops[dropsBefore + 1].m_stack == 2,
            "Separate equal-property stacks were merged or refunded more than once");

        wood.m_stack = 3;
        var incomplete = new BlueprintConstructionRefund(inventory, recipe);
        Require(!incomplete.TryRefund(default, true), "Unverified debit was refunded");
        wood.m_stack = -1;
        incomplete.Complete();
        Require(!incomplete.Recorded && incomplete.Error != null && !incomplete.TryRefund(default, true),
            "Invalid native inventory state was accepted");

        foreach (bool throwAfterSpawn in new[] { false, true })
        {
            wood.m_stack = 3;
            var failedDrop = new BlueprintConstructionRefund(inventory, recipe);
            wood.m_stack = 1;
            failedDrop.Complete();
            failedDrop.RecordRemoval(blockDrop: true, succeeded: true);
            failedDrop.RecordNetworkRemoval(true);
            int before = ItemDrop.Drops.Count;
            ItemDrop.ThrowBefore = !throwAfterSpawn;
            ItemDrop.ThrowAfter = throwAfterSpawn;
            Require(!failedDrop.TryRefund(default, true) && !failedDrop.TryRefund(default, true) && failedDrop.Error != null,
                "Uncertain native drop was retried");
            Require(ItemDrop.Drops.Count == before + (throwAfterSpawn ? 1 : 0),
                "Native drop exception duplicated items");
            ItemDrop.ThrowBefore = ItemDrop.ThrowAfter = false;
        }
        foreach (bool ordinaryDestruction in new[] { false, true })
        {
            wood.m_stack = 3;
            var unconfirmed = new BlueprintConstructionRefund(inventory, recipe);
            wood.m_stack = 0;
            unconfirmed.Complete();
            int before = ItemDrop.Drops.Count;
            unconfirmed.RecordRemoval(blockDrop: !ordinaryDestruction, succeeded: ordinaryDestruction);
            unconfirmed.RecordRemoval(blockDrop: true, succeeded: true);
            Require(!unconfirmed.TryRefund(default, true) && ItemDrop.Drops.Count == before,
                "Uncertain/ordinary native destruction plus later fallback duplicated recipe drops");
        }
        // Native debit precedes PlacePiece; its failure handler can queue rollback before UpdatePlacement finalizes the debit.
        wood.m_stack = 5;
        var delayed = new BlueprintConstructionRefund(inventory, recipe);
        wood.m_stack -= 2;
        int delayedBefore = ItemDrop.Drops.Count;
        Require(!delayed.TryRefund(default, true), "Failure refunded before the native debit snapshot completed");
        delayed.Complete();
        Require(!delayed.TryRefund(default, true), "Completed debit refunded while the failed object still existed");
        delayed.RecordRemoval(true, true);
        delayed.RecordNetworkRemoval(true);
        Require(!delayed.TryRefund(default, false), "Failed object's surviving network identity allowed a refund");
        Require(delayed.TryRefund(default, true) && delayed.TryRefund(default, true) &&
            ItemDrop.Drops.Count == delayedBefore + 1 && ItemDrop.Drops[delayedBefore].m_stack == 2 &&
            ItemDrop.Drops[delayedBefore].m_customData["key"] == "changed after debit",
            "Deferred post-placement failure lost or duplicated the actual paid resources");
        Console.WriteLine("PASS actual construction debit/refund: no-cost, distinct/equal stacks, duplicate source refusal, item properties, idempotence, uncertain native failure; inert host doubles, no game proof");
    }

    private static ItemDrop.ItemData Item(string name, int count, int quality, float durability, string crafter, string custom) =>
        new ItemDrop.ItemData
        {
            m_shared = new ItemDrop.SharedData { m_name = name },
            m_stack = count,
            m_quality = quality,
            m_durability = durability,
            m_crafterName = crafter,
            m_customData = new Dictionary<string, string> { ["key"] = custom }
        };

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}

// Inert doubles exercise the production ledger without loading Unity or spawning game items.
internal sealed class Inventory
{
    internal readonly List<ItemDrop.ItemData> Items = new List<ItemDrop.ItemData>();
    internal List<ItemDrop.ItemData> GetAllItems() => Items;
    internal bool ContainsItem(ItemDrop.ItemData item) => Items.Contains(item);
}

internal sealed class Piece
{
    internal sealed class Requirement { internal ItemDrop m_resItem; }
}

internal sealed class ItemDrop
{
    internal sealed class SharedData { internal string m_name; }
    internal sealed class ItemData
    {
        internal SharedData m_shared;
        internal int m_stack, m_quality;
        internal float m_durability;
        internal string m_crafterName;
        internal Dictionary<string, string> m_customData;
        internal ItemData Clone()
        {
            var clone = (ItemData)MemberwiseClone();
            clone.m_customData = new Dictionary<string, string>(m_customData);
            return clone;
        }
    }
    internal ItemData m_itemData;
    internal static readonly List<ItemData> Drops = new List<ItemData>();
    internal static bool ThrowBefore, ThrowAfter;
    public static implicit operator bool(ItemDrop value) => value != null;
    internal static ItemDrop DropItem(ItemData item, int count, UnityEngine.Vector3 position, UnityEngine.Quaternion rotation)
    {
        if (ThrowBefore) throw new InvalidOperationException("before spawn");
        var clone = item.Clone();
        clone.m_stack = count;
        Drops.Add(clone);
        if (ThrowAfter) throw new InvalidOperationException("after spawn");
        return new ItemDrop { m_itemData = clone };
    }
}

namespace UnityEngine
{
    internal struct Vector3 { }
    internal struct Quaternion { internal static Quaternion identity => default; }
    internal static class Debug { internal static void LogError(string message) { } }
}
