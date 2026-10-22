using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using OstrixMods.BuildWorks.Geometry;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Removal = OstrixMods.BuildWorks.BlueprintConstructionCancellationJournal;
using Payout = OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ConstructionPayoutPropertyChecks
    {
        // Synthetic format-level properties; no claim that a live prefab supports these quality/variant values.
        private sealed class Resource
        {
            internal string Prefab, CrafterName = "";
            internal int Quality = 1, Variant, Durability = 10000, World, Version = 110;
            internal long Crafter;
            internal bool Cheated, PickedUp = true, Equipped;
            internal Dictionary<string, string> Custom = new Dictionary<string, string>();
            internal Resource Copy()
            { var result = (Resource)MemberwiseClone(); result.Custom = new Dictionary<string, string>(Custom); return result; }
        }
        private static int checks;
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder); checks = 0;
            var resources = new[] {
                new Resource { Prefab = "Wood" },
                new Resource { Prefab = "Wood", Quality = 3, Variant = 2, Durability = 12345, World = 2, Crafter = 77,
                    CrafterName = "Мастер 🪵", Cheated = true, Custom = new Dictionary<string, string> { ["mod.grade"] = "дуб", ["mod.note"] = "строка\n二" } },
                new Resource { Prefab = "Resin", Quality = 2, Variant = 1, Durability = 0, World = 1, Crafter = 88 },
                new Resource { Prefab = "FineWood", Variant = 1, Durability = 4200, Crafter = 99, Custom = new Dictionary<string, string> { ["метка"] = "", ["origin"] = "kept" } },
                new Resource { Prefab = "Copper", Version = 109, Quality = 4, Variant = 3, World = 3, Crafter = 123, CrafterName = "Old record", Custom = new Dictionary<string, string> { ["mod.v"] = "109" } },
                new Resource { Prefab = "Chain", Quality = 2, Durability = 1, World = 4, Cheated = true }
            };
            var library = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
            var part = new CompositeBlueprintStore.Part { stableId = "wall", prefabName = "woodwall", displayName = "Wall",
                position = new CompositeBlueprintStore.VectorData(), rotation = new CompositeBlueprintStore.QuaternionData { w = 1 },
                scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, baseScale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, editorVisible = true };
            Check(library.TrySaveDocument("Property fixture", "QA", new[] { part }, Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), "wall", null, out var blueprint, out var error), error);
            var plans = new BlueprintConstructionProjectStore(Path.Combine(folder, "projects"), 42);
            var parts = new Parts(plans); var removal = new Removal(plans); var payout = new Payout(plans);
            var pose = new BlueprintConstructionProjectStore.Pose(new float[] { 0, 0, 0, 0, 0, 0, 1, 1, 1, 1 });
            var plan = plans.Create(Guid.NewGuid(), blueprint, pose, new[] { pose });
            Guid build = Guid.NewGuid(), operation = Guid.NewGuid();
            var before = Inventory(resources.Select((r, i) => (r, 5, i)).ToArray());
            var after = Inventory(resources.Select((r, i) => (r, 3, i)).ToArray());
            var paid = resources.Select((r, i) => new Parts.PaidRow { Prefab = r.Prefab, Properties = Package(r, 1, i, Guid.Empty, r.Version),
                Before = 5, Paid = 2, X = i, Y = 0, Quality = r.Quality, Variant = r.Variant }).ToArray();
            parts.Prepare(plan.Id, plan.Parts[0].Id, build, 17, before, out _);
            var observed = parts.Observe(plan.Id, plan.Parts[0].Id, build, 31, 1, "woodwall".GetStableHashCode(), 17, after, false, paid);
            parts.ConfirmSaved(plan.Id, plan.Parts[0].Id, build, new Parts.SavePair { BeforeGeneration = 4, Generation = 5,
                ProfileHash = new string('A', 128), InventoryHash = Digest(Convert.FromBase64String(after.Zdo)), WorldPayloadHash = Digest(observed.Receipt) });
            removal.Prepare(plan.Id, 1, operation, 17); removal.BeginRemoval(plan.Id, operation, 7);
            removal.ConfirmRemovedSaved(plan.Id, operation, new Removal.SaveProof { BeforeGeneration = 7, Generation = 8,
                ProfileHash = new string('A', 128), InventoryHash = Digest(Convert.FromBase64String(after.Zdo)) });
            var entries = resources.Select(r => { Guid id = Guid.NewGuid(); return new Payout.Entry { Id = id, Blob = Package(r, 2, 0, id) }; }).ToArray();
            (string name, Action<Resource> apply)[] changes = {
                ("prefab", r => r.Prefab = "Stone"), ("quality", r => r.Quality++), ("variant", r => r.Variant++),
                ("durability", r => r.Durability++), ("world", r => r.World++), ("crafter", r => r.Crafter++),
                ("crafterName", r => r.CrafterName += "changed"), ("cheated", r => r.Cheated = !r.Cheated),
                ("custom", r => r.Custom["unexpected"] = "value"), ("pickedUp", r => r.PickedUp = false), ("equipped", r => r.Equipped = true) };
            for (int i = 0; i < resources.Length; i++)
            {
                foreach (var change in changes)
                {
                    // Native item format omits the crafter name when the crafter ID is zero.
                    if (change.name == "crafterName" && resources[i].Crafter == 0) continue;
                    var changed = resources[i].Copy(); change.apply(changed); var bad = entries.ToArray();
                    bad[i] = new Payout.Entry { Id = entries[i].Id, Blob = Package(changed, 2, 0, entries[i].Id) };
                    Check(bad[i].Blob != entries[i].Blob, "Sealed mutation was not encoded: " + change.name);
                    Refuse(() => payout.Prepare(plan.Id, operation, 17, 8, after, bad), "sealed row " + i + "/" + change.name);
                }
                var wrongQuantity = entries.ToArray(); wrongQuantity[i] = new Payout.Entry { Id = entries[i].Id, Blob = Package(resources[i], 3, 0, entries[i].Id) };
                Refuse(() => payout.Prepare(plan.Id, operation, 17, 8, after, wrongQuantity));
            }
            Check(!Directory.EnumerateFiles(plans.DirectoryPath, "*.bwpayout").Any(), "Rejected properties created a payout journal.");
            payout.Prepare(plan.Id, operation, 17, 8, after, entries);
            for (int i = 0; i < entries.Length; i++) { payout.BeginItem(plan.Id, operation, i); payout.Observe(plan.Id, operation, i, 31, (uint)(i + 10)); }
            payout.BeginSave(plan.Id, operation);
            payout.ConfirmSaved(plan.Id, operation, new Payout.Proof { Generation = 9, ProfileHash = new string('A', 128),
                InventoryHash = Digest(Convert.FromBase64String(after.Zdo)), ItemHashes = entries.Select(e => Digest(Convert.FromBase64String(e.Blob))).ToArray() });
            var current = after; var receipts = new Dictionary<string, string>(); int[] order = { 5, 2, 0, 4, 1, 3 };
            for (int step = 0; step < order.Length; step++)
            {
                int i = order[step], cell = 6 + step;
                foreach (var change in changes)
                {
                    if (change.name == "crafterName" && resources[i].Crafter == 0) continue;
                    var changed = resources[i].Copy(); change.apply(changed);
                    string bad = Package(changed, 2, cell, Guid.Empty);
                    Check(bad != Package(resources[i], 2, cell, Guid.Empty), "Clean mutation was not encoded: " + change.name);
                    Refuse(() => Payout.CreditInventory(current, bad, paid[i], cell, 0), "clean row " + i + "/" + change.name);
                }
                Refuse(() => Payout.CreditInventory(current, Package(resources[i], 3, cell, Guid.Empty), paid[i], cell, 0));
                Refuse(() => Payout.CreditInventory(current, Package(resources[i], 2, cell, Guid.NewGuid()), paid[i], cell, 0));
                var intent = payout.BeginCredit(plan.Id, operation, i, current, Package(resources[i], 2, cell, Guid.Empty), cell, 0, new string('B', 128));
                current = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, intent.Collections[i].AfterInfo, intent.Collections[i].AfterBytes, null);
                receipts.Add(Payout.ReceiptKey(intent.Items[i].Id), Payout.ReceiptValue(intent, i));
                payout.ConfirmSavedCredit(plan.Id, operation, i, FullProof(current, receipts));
                var reloaded = new Payout(plans).Read(plan.Id);
                Check(reloaded.Collections[i].Entry == entries[i].Id && reloaded.Collections[i].Phase == Payout.ProfileCredited, "Reload lost exact indexed credit.");
                var expected = Inventory(resources.Select((r, j) => (r, 3, j)).Concat(order.Take(step + 1).Select((j, k) => (resources[j], 2, 6 + k))).ToArray());
                Check(current.Info == expected.Info && current.Zdo == expected.Zdo, "Credit changed/merged properties, another stack, or insertion order.");
            }
            Check(BlueprintContainerInventory.TryRead(current, name => name.GetStableHashCode(), out var final) && final.Count == 12 &&
                final.Sum(i => i.Stack) == 30 && final.Count(i => i.Prefab == "Wood") == 4 && final.All(i => !i.CustomData.ContainsKey(Payout.CustodyKey)),
                "Final multi-resource inventory lost exact separate stacks or retained custody data.");
            Console.WriteLine("PASS payout property corpus: " + checks + " assertions; six synthetic paid rows/five prefabs, exact property refusal,109→110 and sparse reload. Native prefab/payment/save support is not claimed.");
        }
        private static BlueprintForeignPartData Inventory((Resource resource, int count, int x)[] rows)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(110); writer.Write((ushort)rows.Length);
                foreach (var row in rows) { byte[] item = Convert.FromBase64String(Package(row.resource, row.count, row.x, Guid.Empty)); writer.Write(item, 1, item.Length - 1); }
                string info = "16:4:" + rows.Length + ":" + rows.Sum(r => r.count) + "|" + string.Join("|", rows.Select(r => r.resource.Prefab).Distinct().OrderBy(n => n, StringComparer.Ordinal));
                return new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, info, Convert.ToBase64String(stream.ToArray()), null);
            }
        }
        private static string Package(Resource r, int count, int x, Guid marker, int version = 110)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                int customCount = r.Custom.Count + (marker == Guid.Empty ? 0 : 1);
                writer.Write((byte)version); writer.Write(r.Durability); writer.Write((byte)x); writer.Write((byte)0); writer.Write((byte)r.World);
                writer.Write((byte)((r.PickedUp ? 1 : 0) | (r.Equipped ? 2 : 0) | (r.Quality != 1 ? 4 : 0) | (count != 1 ? 8 : 0) |
                    (r.Variant != 0 ? 16 : 0) | (r.Crafter != 0 ? 32 : 0) | 64 | (customCount > 0 ? 128 : 0)));
                if (r.Quality != 1) writer.Write((ushort)r.Quality); if (count != 1) writer.Write((ushort)count); if (r.Variant != 0) writer.Write(r.Variant);
                if (r.Crafter != 0) { writer.Write(r.Crafter); writer.Write(r.CrafterName); }
                writer.Write(r.Prefab.GetStableHashCode());
                if (customCount > 0) writer.Write((byte)customCount);
                foreach (var pair in r.Custom) { writer.Write(pair.Key); writer.Write(pair.Value); }
                if (marker != Guid.Empty) { writer.Write(Payout.CustodyKey); writer.Write(marker.ToString("N")); }
                writer.Write((byte)(r.Cheated ? 1 : 0)); return Convert.ToBase64String(stream.ToArray());
            }
        }
        private static NativePlayerProfileReadback.Evidence FullProof(BlueprintForeignPartData inventory, Dictionary<string, string> receipts)
            => (NativePlayerProfileReadback.Evidence)Activator.CreateInstance(typeof(NativePlayerProfileReadback.Evidence), BindingFlags.Instance | BindingFlags.NonPublic,
                null, new object[] { 17L, new string('C', 128), null, 0, inventory, new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(receipts)) }, null);
        private static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Check(bool valid, string reason) { checks++; if (!valid) throw new InvalidOperationException(reason); }
        private static void Refuse(Action action, string reason = "quantity/custody") { checks++; try { action(); } catch (InvalidDataException) { return; } throw new InvalidOperationException("Changed properties were accepted: " + reason); }
    }
}
