using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks;

internal static class ConstructionSourceDebitTests
{
    private static int checks;
    private static void Require(bool value, string message)
    { ++checks; if (!value) throw new InvalidOperationException(message); }
    private static bool Refuses(Action action)
    { try { action(); return false; } catch (InvalidOperationException) { return true; } catch (ArgumentException) { return true; } }
    private static Inventory Stock(int amount, string custom)
    {
        var inventory = new Inventory();
        inventory.Items.Add(ConstructionRefundTests.Item("wood", amount, 2, 14f, "payer", custom));
        return inventory;
    }
    private static Piece.Requirement[] Recipe(Inventory player) =>
        new[] { new Piece.Requirement { m_resItem = new ItemDrop { m_itemData = player.Items[0] } } };

    internal static void Run()
    {
        var player = Stock(5, "player"); var first = Stock(8, "first"); var second = Stock(12, "second");
        Guid a = Guid.NewGuid(), b = Guid.NewGuid();
        var sources = new List<(Guid Token, Inventory Inventory)> { (a, first), (b, second) };
        var payment = new BlueprintConstructionRefund(player, Recipe(player), sources);
        Require(!payment.Recorded && Refuses(() => payment.DescribeSourcePaid()), "Unobserved source debit exported");
        Require(player.Items[0].m_stack == 5 && first.Items[0].m_stack == 8 && second.Items[0].m_stack == 12,
            "Observer preparation changed a source inventory");
        sources.Clear(); // The observer must retain its own source list.
        player.Items[0].m_stack -= 2; first.Items[0].m_stack -= 3; second.Items[0].m_stack -= 5;
        payment.Complete();
        var paid = payment.DescribeSourcePaid();
        Require(payment.Recorded && payment.Error == null && paid.Length == 3 && paid.Sum(row => row.Paid) == 10,
            "Player plus two container debits not observed");
        Require(paid[0].Source == Guid.Empty && paid[0].Before == 5 && paid[0].Paid == 2 &&
            paid[1].Source == a && paid[1].Before == 8 && paid[1].Paid == 3 &&
            paid[2].Source == b && paid[2].Before == 12 && paid[2].Paid == 5, "Source identity/order/counts lost");
        Require(paid.Select(row => row.Item.m_customData["key"]).SequenceEqual(new[] { "player", "first", "second" }),
            "Separate source properties merged or replaced");
        paid[1].Item.m_customData["key"] = "export mutation";
        first.Items[0].m_customData["key"] = "later live mutation";
        payment.Complete();
        Require(payment.DescribeSourcePaid()[1].Item.m_customData["key"] == "first", "Completed observation lost detached properties");
        Require(!payment.IsFor(player) && !payment.IsFor(first) && Refuses(() => payment.DescribePaid()),
            "Multi-source debit entered player-only journal/transfer API");
        int drops = ItemDrop.Drops.Count;
        payment.RecordRemoval(true, true); payment.RecordNetworkRemoval(true);
        Require(!payment.TryRefund(default, true) && !payment.TryRefund(default, true) && ItemDrop.Drops.Count == drops,
            "Transient legacy refund issued external-source resources");

        player = Stock(5, "own"); first = Stock(8, "external"); second = Stock(12, "external");
        payment = new BlueprintConstructionRefund(player, Recipe(player), new[] { (a, first), (b, second) });
        first.Items[0].m_stack -= 4; second.Items.Clear();
        payment.Complete(); paid = payment.DescribeSourcePaid();
        Require(payment.Recorded && paid.Length == 2 && paid[0].Source == a && paid[0].Paid == 4 &&
            paid[1].Source == b && paid[1].Paid == 12, "Unchanged player hid external/full-stack debits");
        Require(paid[0].Item.m_customData["key"] == paid[1].Item.m_customData["key"] && paid[0].Source != paid[1].Source,
            "Equal-property sources collapsed");

        player = Stock(5, "own"); first = Stock(8, "partial");
        payment = new BlueprintConstructionRefund(player, Recipe(player), new[] { (a, first) });
        try { first.Items[0].m_stack -= 3; throw new InvalidOperationException("simulated provider caught after partial debit"); }
        catch (InvalidOperationException) { }
        payment.Complete(); paid = payment.DescribeSourcePaid();
        Require(payment.Recorded && paid.Length == 1 && paid[0].Source == a && paid[0].Paid == 3,
            "Caught partial debit disappeared or was replaced by recipe amount");
        Require(!payment.IsFor(player) && Refuses(() => payment.DescribePaid()) && ItemDrop.Drops.Count == drops,
            "Partial observation became legacy payment/refund authority");

        var recipe = Recipe(player);
        Require(Refuses(() => new BlueprintConstructionRefund(player, recipe, null)), "Null source list allowed");
        Require(Refuses(() => new BlueprintConstructionRefund(player, recipe, Array.Empty<(Guid, Inventory)>())), "Empty external list allowed");
        Require(Refuses(() => new BlueprintConstructionRefund(player, recipe, new[] { (Guid.Empty, first) })), "Player token reused externally");
        Require(Refuses(() => new BlueprintConstructionRefund(player, recipe, new[] { (a, first), (a, second) })), "Repeated source token allowed");
        Require(Refuses(() => new BlueprintConstructionRefund(player, recipe, new[] { (a, player) })), "Player inventory aliased externally");
        Require(Refuses(() => new BlueprintConstructionRefund(player, recipe, new[] { (a, first), (b, first) })), "Two tokens alias one inventory");
        Require(Refuses(() => new BlueprintConstructionRefund(player, recipe, new[] { (a, (Inventory)null) })), "Missing inventory allowed");
        Require(Refuses(() => new BlueprintConstructionRefund(player, recipe,
            Enumerable.Range(0, 128).Select(_ => (Guid.NewGuid(), first)).ToArray())), "Source budget not checked before effects");
        second = new Inventory(); second.Items.Add(first.Items[0]);
        Require(Refuses(() => new BlueprintConstructionRefund(player, recipe, new[] { (a, first), (b, second) })),
            "One physical item observed twice under different inventories");
        first.Items.Add(first.Items[0]);
        Require(Refuses(() => new BlueprintConstructionRefund(player, recipe, new[] { (a, first) })), "Duplicate initial cost reference allowed");

        foreach (string change in new[] { "increase", "negative", "properties", "replacement", "addition", "move", "duplicate" })
        {
            player = Stock(5, "own"); first = Stock(8, "external"); second = Stock(12, "other");
            payment = new BlueprintConstructionRefund(player, Recipe(player), new[] { (a, first), (b, second) });
            var item = first.Items[0];
            if (change == "increase") item.m_stack++;
            if (change == "negative") item.m_stack = -1;
            if (change == "properties") item.m_customData["key"] = "changed before observation";
            if (change == "replacement") { first.Items.Clear(); first.Items.Add(item.Clone()); }
            if (change == "addition") first.Items.Add(item.Clone());
            if (change == "move") { first.Items.Clear(); second.Items.Add(item); }
            if (change == "duplicate") first.Items.Add(item);
            payment.Complete(); payment.Complete();
            Require(!payment.Recorded && payment.Error != null && Refuses(() => payment.DescribeSourcePaid()) &&
                !payment.TryRefund(default, true) && ItemDrop.Drops.Count == drops, "Changed source was accepted: " + change);
        }
        player = Stock(5, "own"); first = Stock(8, "external");
        payment = new BlueprintConstructionRefund(player, Recipe(player), new[] { (a, first) });
        payment.Complete();
        Require(payment.Recorded && payment.DescribeSourcePaid().Length == 0 && !payment.IsFor(player),
            "Zero observed debit was converted to legacy/free authority");
        first = new Inventory();
        for (int i = 0; i < 4096; ++i) first.Items.Add(ConstructionRefundTests.Item("wood", 1, 1, 1f, "payer", "small"));
        Require(Refuses(() => new BlueprintConstructionRefund(player, Recipe(player), new[] { (a, first) })),
            "Item observation budget exceeded before effects");
        first.Items.RemoveAt(first.Items.Count - 1);
        payment = new BlueprintConstructionRefund(player, Recipe(player), new[] { (a, first) });
        payment.Complete();
        Require(payment.Recorded && payment.DescribeSourcePaid().Length == 0, "Exact item budget incorrectly refused");
        first = Stock(8, new string('x', 4 * 1024 * 1024));
        Require(Refuses(() => new BlueprintConstructionRefund(player, Recipe(player), new[] { (a, first) })),
            "Snapshot evidence budget exceeded before effects");
        foreach (string invalid in new[] { "null", "shared", "zero", "negative" })
        {
            first = Stock(8, "invalid");
            if (invalid == "null") first.Items[0] = null;
            if (invalid == "shared") first.Items[0].m_shared = null;
            if (invalid == "zero") first.Items[0].m_stack = 0;
            if (invalid == "negative") first.Items[0].m_stack = -1;
            Require(Refuses(() => new BlueprintConstructionRefund(player, Recipe(player), new[] { (a, first) })),
                "Invalid source admitted before effects: " + invalid);
        }
        foreach (bool invalidFirst in new[] { false, true })
        {
            player = Stock(5, "payer"); first = Stock(8, "healthy"); second = Stock(12, "unknown");
            payment = new BlueprintConstructionRefund(player, Recipe(player), invalidFirst ? new[] { (b, second), (a, first) } : new[] { (a, first), (b, second) });
            first.Items[0].m_stack -= 3;
            second.Items[0].m_customData["key"] = "changed during native";
            payment.Complete();
            var capture = payment.CaptureOutcome();
            var healthy = capture.Sources.Single(source => source.Token == a);
            var unknown = capture.Sources.Single(source => source.Token == b);
            Require(payment.Error != null && !payment.Recorded && healthy.Exact && healthy.Items.Single().Before == 8 &&
                healthy.Items.Single().After == 5 && !unknown.Exact && unknown.Items.Single().After == -1 &&
                unknown.Items.Single().Before == 12 && capture.Sources[0].Exact,
                "One invalid source erased another exact partial debit or became zero");
            healthy.Items[0].After = 8; unknown.Items[0].Properties = "mutated export";
            first.Items[0].m_stack = 1; second.Items[0].m_stack = 1;
            payment.Complete();
            var retained = payment.CaptureOutcome();
            Require(retained.Sources.Single(source => source.Token == a).Items.Single().After == 5 &&
                retained.Sources.Single(source => source.Token == b).Items.Single().Properties != "mutated export",
                "Repeated Complete or exported aliases replaced the first observation");
        }
        player = Stock(5, "payer");
        var unattempted = new BlueprintConstructionRefund(player, Recipe(player)).CaptureOutcome();
        Require(!unattempted.ExplicitSources && !unattempted.Sources.Single().Exact && unattempted.Sources.Single().Items.Single().After == -1,
            "Missing completion became proven unchanged");
        player = Stock(5, new string('x', 25000));
        var oversizedLegacy = new BlueprintConstructionRefund(player, Recipe(player), true);
        player.Items[0].m_stack -= 2; oversizedLegacy.Complete();
        var unsupported = oversizedLegacy.CaptureOutcome();
        Require(oversizedLegacy.Recorded && oversizedLegacy.Error == null && oversizedLegacy.DescribePaid().Single().Paid == 2 &&
            !unsupported.Sources.Single().Exact && unsupported.Sources.Single().Items.Single().After == -1,
            "Optional partial snapshot restricted an existing native transient recipe stack");
        oversizedLegacy.RecordRemoval(true, true); oversizedLegacy.RecordNetworkRemoval(true);
        int beforeLargeRefund = ItemDrop.Drops.Count;
        Require(oversizedLegacy.TryRefund(default, true) && ItemDrop.Drops.Count == beforeLargeRefund + 1 &&
            ItemDrop.Drops.Last().m_customData["key"].Length == 25000,
            "Unavailable partial snapshot lost legacy clone/refund properties");
        player = Stock(5, "payer");
        int serializerCalls = BlueprintItemStandPlacement.SnapshotCalls;
        var legacy = new BlueprintConstructionRefund(player, Recipe(player));
        player.Items[0].m_stack--;
        legacy.Complete();
        Require(BlueprintItemStandPlacement.SnapshotCalls == serializerCalls, "Ordinary transient construction serialized optional partial evidence");
        Require(legacy.IsFor(player) && !legacy.IsFor(new Inventory()) && legacy.DescribePaid().Single().Paid == 1 &&
            legacy.DescribeSourcePaid().Single().Source == Guid.Empty, "Existing player-only receipt/identity changed");
        Console.WriteLine("PASS " + checks + " actual multi-source debit observer assertions;inert inventories/serializer;no provider,authority,durable save/refund or game proof.");
    }
}

namespace OstrixMods.BuildWorks
{
    // The production observer calls the native record serializer; this fixture substitutes that boundary only.
    internal static class BlueprintItemStandPlacement
    {
        internal static int SnapshotCalls;
        internal static string SnapshotItem(ItemDrop.ItemData item)
        {
            SnapshotCalls++;
            string result = System.Text.Json.JsonSerializer.Serialize(new {
                Name = item.m_shared.m_name, Quality = item.m_quality, Durability = item.m_durability,
                Crafter = item.m_crafterName, Custom = item.m_customData });
            if (System.Text.Encoding.UTF8.GetByteCount(result) > 24576)
                throw new ArgumentException("Mounted item exceeds the substituted native snapshot byte limit.");
            return result;
        }
    }
}
