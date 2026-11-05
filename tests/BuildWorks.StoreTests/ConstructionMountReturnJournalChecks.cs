using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static partial class ConstructionMountJournalChecks
    {
        private static int returnAssertions;
        private static void ReturnCheck(bool value, string why)
        { returnAssertions++; if (!value) throw new Exception("Mount return journal: " + why); }
        private static void ReturnRefuse<T>(Action action) where T : Exception
        { returnAssertions++; try { action(); } catch (T) { return; } throw new Exception("Expected return refusal: " + typeof(T).Name); }

        private static void RunReturns(string directory)
        {
            returnAssertions = 0;
            var f = Fixture(directory); var mountOperation = Guid.NewGuid(); var operation = Guid.NewGuid();
            var supplied = Mounted(durability: 9412); var before = Inventory(0, 1);
            var bytes = Convert.FromBase64String(before.Zdo);
            Array.Copy(BitConverter.GetBytes(9412), 0, bytes, 6, 4);
            before = new BlueprintForeignPartData(before.Format, before.Info, Convert.ToBase64String(bytes), null);
            f.Journal.PrepareMount(f.Plan.Id, f.Part, mountOperation, 17, 90, 4, before, supplied, true, out _);
            ReturnRefuse<InvalidOperationException>(() => Prepare());
            var after = Inventory(0, 0);
            var attached = f.Journal.ObserveMount(f.Plan.Id, f.Part, mountOperation, after, supplied);
            f.Journal.ConfirmMountSaved(f.Plan.Id, f.Part, mountOperation, Saved(after, attached.Receipt));
            string path = Path.Combine(f.Plans.DirectoryPath, f.Plan.Id.ToString("N") + "." + f.Part.ToString("N") + ".bwmount");
            string parentPath = Path.ChangeExtension(path, ".bwpart"); var parentBytes = File.ReadAllBytes(parentPath);
            var legacy = File.ReadAllBytes(path);
            ReturnCheck(!Encoding.UTF8.GetString(legacy).Contains("\"Return\""), "absent return changed legacy JSON shape");
            foreach (var id in new[] { Guid.Empty, mountOperation, f.Parent })
                ReturnRefuse<InvalidDataException>(() => Prepare(op: id));
            foreach (uint generation in new[] { 0u, 7u, uint.MaxValue })
                ReturnRefuse<InvalidDataException>(() => Prepare(generation: generation));
            ReturnRefuse<InvalidOperationException>(() => Prepare(player: 18));
            ReturnRefuse<InvalidOperationException>(() => Prepare(actual: Mounted("archive-B", 9999)));
            ReturnCheck(legacy.SequenceEqual(File.ReadAllBytes(path)), "rejected preparation rewrote the saved mount");
            var intent = Prepare();
            ReturnRefuse<InvalidOperationException>(() => Confirm());
            ReturnCheck(intent.Return.Phase == Journal.MountReturnIntent && intent.Revision == 4 && intent.RequiresReconciliation,
                "return intent is not durable/unsettled");
            ReturnCheck(Prepare(fresh: false).Revision == 4, "same intent replay advanced revision");
            ReturnRefuse<InvalidOperationException>(() => Prepare(op: Guid.NewGuid()));
            ReturnRefuse<InvalidOperationException>(() => Observe(Mounted(durability: 9411)));
            ReturnRefuse<InvalidDataException>(() => Bind(user: 0));
            ReturnRefuse<InvalidDataException>(() => Bind(id: 0));
            ReturnCheck(f.Journal.ReadMount(f.Plan.Id, f.Part).Return.Phase == Journal.MountReturnIntent,
                "rejected binding mutated the durable intent");
            var bound = Bind();
            ReturnRefuse<InvalidOperationException>(() => Confirm());
            ReturnCheck(bound.Revision == 5 && bound.Return.Phase == Journal.MountReturnBound && Bind().Revision == 5,
                "identity was not bound once before observation");
            ReturnRefuse<InvalidOperationException>(() => Bind(id: 6));
            ReturnRefuse<InvalidOperationException>(() => Prepare(fresh: false));
            ReturnRefuse<InvalidOperationException>(() => Observe(Mounted(durability: 9411), empty: false));
            var boundBytes = File.ReadAllBytes(path);
            foreach (var wrong in new[] { Mounted(durability: 9412), Mounted(durability: 9410), Mounted("changed", 9411),
                Mounted(durability: 9411, equipped: true), Mounted(durability: 9411, quality: 2),
                Mounted(durability: 9411, variant: 1), Mounted(durability: 9411, prefab: "ShieldBanded"),
                Mounted(durability: 9411, orientation: 1) })
            {
                ReturnRefuse<InvalidDataException>(() => Observe(wrong));
                ReturnCheck(boundBytes.SequenceEqual(File.ReadAllBytes(path)), "rejected properties changed the bound record");
            }
            var actual = Mounted(durability: 9411);
            foreach (var offset in new[] { 5, 6, 7, 8, 9, Convert.FromBase64String(actual.Zdo).Length - 1 })
            {
                var changed = Convert.FromBase64String(actual.Zdo); changed[offset] ^= 1;
                ReturnRefuse<InvalidDataException>(() => Observe(new BlueprintForeignPartData(actual.Format, actual.Info,
                    Convert.ToBase64String(changed), null)));
            }
            var observed = Observe(actual);
            ReturnCheck(observed.Return.Phase == Journal.MountReturnObserved && observed.Revision == 6 && observed.RequiresReconciliation,
                "actual observed drop incorrectly settled return custody");
            ReturnCheck(observed.MountedBytes == supplied.Zdo && observed.Return.ObservedBytes == actual.Zdo && supplied.Zdo != actual.Zdo,
                "original A and observed native-loaded B were conflated");
            ReturnCheck(Observe(actual).Revision == 6 && Bind().Revision == 6, "same observation/identity replay rewrote evidence");
            ReturnRefuse<InvalidOperationException>(() => Observe(Mounted("other", 9411)));
            ReturnRefuse<InvalidOperationException>(() => Cancel(f));
            ReturnCheck(parentBytes.SequenceEqual(File.ReadAllBytes(parentPath)), "return rewrote the paid structural receipt");
            observed.Return.ObservedBytes = supplied.Zdo;
            var reopened = new Journal(new Plans(f.Plans.DirectoryPath, 42)).ReadMount(f.Plan.Id, f.Part);
            ReturnCheck(reopened.Return.ObservedBytes == actual.Zdo && reopened.MountedBytes == supplied.Zdo,
                "returned DTO aliases evidence or fresh reader lost return state");
            var terminal = File.ReadAllBytes(path);
            string oldReturnJson = Encoding.UTF8.GetString(terminal, 12, BitConverter.ToInt32(terminal, 8));
            oldReturnJson = oldReturnJson.Substring(oldReturnJson.IndexOf("\"Return\":", StringComparison.Ordinal));
            ReturnCheck(!oldReturnJson.Contains("\"Saved\"") && !oldReturnJson.Contains("\"SavedDropHash\""),
                "old observed-return JSON gained unset optional saved fields");
            foreach (var change in new Func<string, string>[] {
                json => json.Replace("\"BeforeGeneration\":8", "\"BeforeGeneration\":7"),
                json => json.Replace("\"ObjectUser\":90,\"ObjectId\":5", "\"ObjectUser\":0,\"ObjectId\":5"),
                json => json.Replace("\"Return\":{", "\"Return\":{\"unknown\":1,") })
            {
                var json = Encoding.UTF8.GetString(terminal, 12, BitConverter.ToInt32(terminal, 8)); var changed = change(json);
                ReturnCheck(changed != json, "corruption fixture failed to change its target");
                RewriteReturn(changed);
                ReturnRefuse<InvalidDataException>(() => f.Journal.ReadMount(f.Plan.Id, f.Part));
            }
            File.WriteAllBytes(path, terminal);
            foreach (string bad in new[] { "before", "generation", "profile", "inventory", "holder", "drop", "missing" })
            {
                var pair = ReturnPair(); string dropHash = Digest(actual.Zdo);
                if (bad == "before") pair.BeforeGeneration++;
                if (bad == "generation") pair.Generation++;
                if (bad == "profile") pair.ProfileHash = "bad";
                if (bad == "inventory") pair.InventoryHash = new string('F', 64);
                if (bad == "holder") pair.WorldPayloadHash = new string('F', 64);
                if (bad == "drop") dropHash = new string('F', 64);
                ReturnRefuse<InvalidDataException>(() => f.Journal.ConfirmMountReturnSaved(f.Plan.Id, f.Part, operation,
                    bad == "missing" ? null : pair, dropHash));
                ReturnCheck(terminal.SequenceEqual(File.ReadAllBytes(path)), "invalid saved pair changed observed return: " + bad);
            }
            var savedReturn = Confirm();
            ReturnCheck(savedReturn.Return.Phase == Journal.MountReturnSaved && savedReturn.Revision == 7 && savedReturn.RequiresReconciliation,
                "saved empty-holder/drop pair incorrectly settled the return");
            ReturnCheck(Confirm().Revision == 7 && Observe(actual).Revision == 7 && Bind().Revision == 7,
                "same saved pair/observation/identity replay rewrote evidence");
            ReturnRefuse<InvalidOperationException>(() => Confirm(new string('A', 128)));
            ReturnRefuse<InvalidOperationException>(() => Cancel(f));
            savedReturn.Return.Saved.ProfileHash = "mutated";
            var reopenedSaved = new Journal(new Plans(f.Plans.DirectoryPath, 42)).ReadMount(f.Plan.Id, f.Part);
            ReturnCheck(reopenedSaved.Return.Saved.ProfileHash == new string('E', 128) && reopenedSaved.Return.SavedDropHash == Digest(actual.Zdo),
                "fresh reader lost/aliased saved return evidence");
            ReturnCheck(parentBytes.SequenceEqual(File.ReadAllBytes(parentPath)), "return save rewrote paid parent");
            ReturnCheck(!Directory.EnumerateFiles(f.Plans.DirectoryPath, "*.preparing").Any(), "return write leaked preparation files");
            foreach (var sample in new[] { (Before: 0, After: 0), (Before: 5, After: 4), (Before: 10000, After: 10000) })
            {
                var other = Fixture(Path.Combine(directory, "cents-" + sample.Before)); var mount = Guid.NewGuid(); var ret = Guid.NewGuid();
                var original = Mounted(durability: sample.Before); var inventory = Inventory(0, 1);
                var raw = Convert.FromBase64String(inventory.Zdo); Array.Copy(BitConverter.GetBytes(sample.Before), 0, raw, 6, 4);
                inventory = new BlueprintForeignPartData(inventory.Format, inventory.Info, Convert.ToBase64String(raw), null);
                other.Journal.PrepareMount(other.Plan.Id, other.Part, mount, 17, 90, 4, inventory, original, true, out _);
                var saved = other.Journal.ObserveMount(other.Plan.Id, other.Part, mount, Inventory(0, 0), original);
                other.Journal.ConfirmMountSaved(other.Plan.Id, other.Part, mount, Saved(Inventory(0, 0), saved.Receipt));
                other.Journal.PrepareMountReturn(other.Plan.Id, other.Part, mount, ret, 17, 8, Inventory(1, 0), original, out _);
                other.Journal.BindMountReturnIdentity(other.Plan.Id, other.Part, ret, 90, 5);
                var result = other.Journal.ObserveMountReturn(other.Plan.Id, other.Part, ret, Mounted(durability: sample.After), true);
                ReturnCheck(result.Return.Phase == Journal.MountReturnObserved && result.MountedBytes == original.Zdo,
                    "exact near-cent/zero native-load-save model rejected or rewrote source");
            }
            RunCreditChecks(directory);
            Console.WriteLine("PASS mounted-return journal: " + returnAssertions + " assertions; durable detach/saved pair/credit intent and exact saved inventory+receipt, no replay/cancel. Cleanup dispatch/terminal file transitions covered with evidence fixtures; no native execution/reload.");

            string Digest(string base64) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Convert.FromBase64String(base64))).Replace("-", ""); }
            Journal.SavePair ReturnPair(string profile = null) => new Journal.SavePair { BeforeGeneration = 8, Generation = 9,
                ProfileHash = profile ?? new string('E', 128), InventoryHash = Digest(Inventory(1, 0).Zdo),
                WorldPayloadHash = Digest(Convert.ToBase64String(attached.Receipt)) };
            Journal.MountRecord Confirm(string profile = null) =>
                f.Journal.ConfirmMountReturnSaved(f.Plan.Id, f.Part, operation, ReturnPair(profile), Digest(Mounted(durability: 9411).Zdo));

            Journal.MountRecord Prepare(Guid? op = null, long player = 17, uint generation = 8, BlueprintForeignPartData actual = null, bool fresh = true)
            {
                var result = f.Journal.PrepareMountReturn(f.Plan.Id, f.Part, mountOperation, op ?? operation, player, generation,
                    Inventory(1, 0), actual ?? supplied, out bool created);
                ReturnCheck(created == fresh, "fresh return intent flag differs"); return result;
            }
            Journal.MountRecord Bind(long user = 90, uint id = 5) => f.Journal.BindMountReturnIdentity(f.Plan.Id, f.Part, operation, user, id);
            Journal.MountRecord Observe(BlueprintForeignPartData drop, bool empty = true) =>
                f.Journal.ObserveMountReturn(f.Plan.Id, f.Part, operation, drop, empty);
            void RewriteReturn(string json)
            {
                var payload = Encoding.UTF8.GetBytes(json);
                using (var file = File.Create(path)) using (var writer = new BinaryWriter(file)) using (var sha = SHA256.Create())
                { writer.Write(terminal.Take(8).ToArray()); writer.Write(payload.Length); writer.Write(payload); writer.Write(sha.ComputeHash(payload)); }
            }
        }
    }
}
