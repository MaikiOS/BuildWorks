using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using OstrixMods.BuildWorks.Geometry;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ConstructionPayoutCapacityChecks
    {
        private static int checks;
        internal static void Run()
        {
            checks = 0;
            var inventory = Inventory();
            var small = Entries(2, 10);
            Journal.RequireIssuanceBudget(inventory, small);
            Check(small.All(e => e.State == Journal.Ready && e.ObjectUser == 0 && e.ObjectId == 0), "Capacity check mutated entry state.");
            int consumed = 0;
            var large = Entries(1000, 20000);
            var paid = new Parts.PaidRow { Prefab = "Wood", Properties = Package(20000, Guid.Empty), Quality = 1, Paid = 2, Before = 5 };
            ValidateDebit(20000, paid);
            foreach (var entry in large) Journal.ValidateItem(paid, entry);
            Check(large.All(e => e.Blob.Length < 90000), "Large fixture violates the existing per-item limit.");
            IEnumerable<Journal.Entry> Lazy()
            { foreach (var entry in large) { consumed++; yield return entry; } }
            Refuse(() => Journal.RequireIssuanceBudget(inventory, Lazy()));
            Check(consumed > 0 && consumed < large.Length, "Over-budget accumulation consumed every entry before refusal.");

            // Tune only the payload length; real Encode decides the existing 16MiB boundary.
            const int count = 700;
            var probe = Entries(count, 10);
            int overhead = EncodedPrepared(inventory, probe) - probe.Sum(e => e.Blob.Length);
            int targetBlob = (16 * 1024 * 1024 - overhead - 8192) / count;
            int payload = targetBlob / 4 * 3 - 100;
            Journal.Entry[] edge = null;
            int preparedBytes = 0;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                edge = Entries(count, payload);
                preparedBytes = EncodedPrepared(inventory, edge);
                if (preparedBytes >= 16 * 1024 * 1024 - 12000 && preparedBytes <= 16 * 1024 * 1024 - 4000) break;
                payload += ((16 * 1024 * 1024 - 8192 - preparedBytes) / count) * 3 / 4;
            }
            Check(preparedBytes >= 16 * 1024 * 1024 - 12000 && preparedBytes <= 16 * 1024 * 1024 - 4000,
                "Prepared-fit fixture did not reach the intended boundary.");
            var edgePaid = new Parts.PaidRow { Prefab = "Wood", Properties = Package(payload, Guid.Empty), Quality = 1, Paid = 2, Before = 5 };
            ValidateDebit(payload, edgePaid);
            foreach (var entry in edge) Journal.ValidateItem(edgePaid, entry);
            Refuse(() => Journal.RequireIssuanceBudget(inventory, edge));
            Check(edge.All(e => e.State == Journal.Ready && e.ObjectUser == 0 && e.ObjectId == 0), "Refused capacity check mutated entries.");
            Console.WriteLine("PASS payout issuance capacity: " + checks + " checks; real debit validation, 1000 individually valid items exceed aggregate budget; Prepared " + preparedBytes +
                " bytes fits while mandatory saved issuance refuses. Actual serializer, no game/save/delete effects; later IDs/terminal/collection/inventory are not guaranteed.");
        }

        private static Journal.Entry[] Entries(int count, int payload)
            => Enumerable.Range(0, count).Select(_ => { Guid id = Guid.NewGuid(); return new Journal.Entry { Id = id, Blob = Package(payload, id) }; }).ToArray();
        private static string Package(int payload, Guid custody, int stack = 1)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                bool sealedItem = custody != Guid.Empty;
                int quantity = sealedItem ? 2 : stack;
                writer.Write((byte)110); writer.Write(10000); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((byte)(1 | 64 | 128 | (quantity != 1 ? 8 : 0)));
                if (quantity != 1) writer.Write((ushort)quantity);
                writer.Write("Wood".GetStableHashCode()); writer.Write((byte)(sealedItem ? 2 : 1));
                writer.Write("mod.capacity"); writer.Write(new string('x', payload));
                if (sealedItem) { writer.Write(Journal.CustodyKey); writer.Write(custody.ToString("N")); }
                writer.Write((byte)0); return Convert.ToBase64String(stream.ToArray());
            }
        }
        private static BlueprintForeignPartData Inventory(int payload = 0, int stack = 1)
        {
            byte[] item = Convert.FromBase64String(Package(payload, Guid.Empty, stack));
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(110); writer.Write((ushort)1); writer.Write(item, 1, item.Length - 1);
                return new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, "8:4:1:" + stack + "|Wood", Convert.ToBase64String(stream.ToArray()), null);
            }
        }
        private static void ValidateDebit(int payload, Parts.PaidRow paid)
        {
            var before = Inventory(payload, 5); var after = Inventory(payload, 3);
            var record = new Parts.Record { BeforeInfo = before.Info, BeforeBytes = before.Zdo, AfterInfo = after.Info, AfterBytes = after.Zdo,
                CostFree = false, Paid = new[] { paid } };
            try { typeof(Parts).GetMethod("ValidateDebits", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { record }); }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
            checks++;
        }
        private static int EncodedPrepared(BlueprintForeignPartData inventory, Journal.Entry[] entries)
        {
            var record = new Journal.Record { Project = Guid.NewGuid(), Operation = Guid.NewGuid(), Player = 1, World = 1,
                RemovalHash = new string('0', 64), Revision = 1, Phase = Journal.Prepared, BeforeGeneration = 1,
                BeforeInfo = inventory.Info, BeforeBytes = inventory.Zdo, Items = entries };
            try
            {
                var encode = typeof(Journal).GetMethod("Encode", BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(typeof(Journal.Record));
                return ((byte[])encode.Invoke(null, new object[] { record })).Length;
            }
            catch (TargetInvocationException error) when (error.InnerException != null) { throw error.InnerException; }
        }
        private static void Check(bool valid, string message) { checks++; if (!valid) throw new InvalidOperationException(message); }
        private static void Refuse(Action action)
        { checks++; try { action(); } catch (InvalidDataException) { return; } throw new InvalidOperationException("Known over-budget issuance was accepted."); }
    }
}
