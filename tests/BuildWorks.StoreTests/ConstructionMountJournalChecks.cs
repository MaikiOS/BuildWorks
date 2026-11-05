using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;
using B = OstrixMods.BuildWorks.CompositeBlueprintStore;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static partial class ConstructionMountJournalChecks
    {
        private static int assertions;
        internal static void Run(string directory)
        {
            assertions = 0;
            var f = Fixture(Path.Combine(directory, "paid")); var operation = Guid.NewGuid();
            var before = Inventory(1, 1); var after = Inventory(1, 0); var supplied = Mounted();
            string parentPath = Path.Combine(f.Plans.DirectoryPath, f.Plan.Id.ToString("N") + "." + f.Part.ToString("N") + ".bwpart");
            string path = Path.ChangeExtension(parentPath, ".bwmount"); byte[] parentBytes = File.ReadAllBytes(parentPath);
            Refuse<InvalidOperationException>(() => Prepare(empty: false));
            Refuse<InvalidOperationException>(() => f.Journal.PrepareMount(f.Plan.Id, f.Part, operation, 18, 90, 4, before, supplied, true, out _));
            Refuse<InvalidDataException>(() => Prepare(item: Mounted("archive-B", 9999)));
            Refuse<InvalidDataException>(() => Prepare(item: Mounted(equipped: true)));
            Refuse<InvalidDataException>(() => Prepare(id: 0));
            Refuse<InvalidDataException>(() => Prepare(op: f.Parent));
            Refuse<InvalidDataException>(() => Prepare(op: Guid.Empty));
            Check(!File.Exists(path), "Rejected preparation created a mount journal");
            var intent = Prepare();
            Refuse<InvalidOperationException>(() => Cancel(f));
            Check(intent.Phase == Journal.Intent && intent.RequiresReconciliation && intent.Revision == 1,
                "Mount intent was not durable");
            Check(Prepare(expectedNew: false).Revision == 1, "Same intent replay created another operation");
            Refuse<InvalidOperationException>(() => Prepare(op: Guid.NewGuid()));
            Refuse<InvalidDataException>(() => Prepare(item: Mounted("another-A"), expectedNew: false));
            Refuse<InvalidOperationException>(() => f.Journal.ConfirmMountSaved(f.Plan.Id, f.Part, operation, new Journal.SavePair()));
            byte[] initial = File.ReadAllBytes(path);
            Refuse<InvalidDataException>(() => Observe(before));
            Refuse<InvalidDataException>(() => Observe(Inventory(0, 0)));
            Refuse<InvalidOperationException>(() => Observe(after, Mounted("archive-B", 9999)));
            Check(initial.SequenceEqual(File.ReadAllBytes(path)), "Rejected observation modified the intent");
            var observed = Observe(after);
            Refuse<InvalidOperationException>(() => Cancel(f));
            Check(observed.Phase == Journal.Observed && observed.RequiresReconciliation && observed.Revision == 2,
                "Observed mount was not retained");
            Check(Observe(after).Revision == 2, "Observation replay advanced the journal");
            observed.Receipt[0] ^= 1; observed.MountedBytes = Mounted("forged").Zdo;
            Check(f.Journal.ReadMount(f.Plan.Id, f.Part).MountedBytes == supplied.Zdo, "Returned DTO aliases persisted evidence");
            Refuse<InvalidOperationException>(() => f.Journal.MountNoEffect(f.Plan.Id, f.Part, operation, before, true));
            var receipt = f.Journal.ReadMount(f.Plan.Id, f.Part).Receipt;
            var proof = Saved(after, receipt);
            foreach (Action<Journal.SavePair> change in new Action<Journal.SavePair>[] {
                p => p.Generation++, p => p.BeforeGeneration = 0, p => p.ProfileHash = new string('A', 64),
                p => p.InventoryHash = Digest(Convert.FromBase64String(before.Zdo)), p => p.WorldPayloadHash = new string('0', 64) })
            {
                var invalid = Saved(after, receipt); change(invalid);
                Refuse<InvalidDataException>(() => f.Journal.ConfirmMountSaved(f.Plan.Id, f.Part, operation, invalid));
            }
            var committed = f.Journal.ConfirmMountSaved(f.Plan.Id, f.Part, operation, proof);
            Refuse<InvalidOperationException>(() => Cancel(f));
            Check(committed.Phase == Journal.Committed && committed.Revision == 3 && !committed.RequiresReconciliation,
                "Matching save evidence did not commit the mount");
            Check(f.Journal.ConfirmMountSaved(f.Plan.Id, f.Part, operation, proof).Revision == 3 && Observe(after).Phase == Journal.Committed,
                "Committed replay changed the terminal operation");
            Refuse<InvalidOperationException>(() => Prepare(op: Guid.NewGuid()));
            var changedProof = Saved(after, receipt); changedProof.ProfileHash = new string('B', 128);
            Refuse<InvalidOperationException>(() => f.Journal.ConfirmMountSaved(f.Plan.Id, f.Part, operation, changedProof));
            Check(parentBytes.SequenceEqual(File.ReadAllBytes(parentPath)), "Mount rewrote the paid structural parent");
            var reopened = new Journal(new Plans(f.Plans.DirectoryPath, 42)).ReadMount(f.Plan.Id, f.Part);
            Check(reopened.Phase == Journal.Committed && reopened.MountedBytes == supplied.Zdo,
                "Fresh store instance did not reopen exact mounted item evidence");
            byte[] terminal = File.ReadAllBytes(path);
            byte[] wrongVersion = (byte[])terminal.Clone(); wrongVersion[5] = (byte)'2'; File.WriteAllBytes(path, wrongVersion);
            Refuse<InvalidDataException>(() => f.Journal.ReadMount(f.Plan.Id, f.Part));
            byte[] corrupt = (byte[])terminal.Clone(); corrupt[20] ^= 1; File.WriteAllBytes(path, corrupt);
            Refuse<InvalidDataException>(() => f.Journal.ReadMount(f.Plan.Id, f.Part));
            File.WriteAllBytes(path, terminal.Concat(new byte[] { 0 }).ToArray());
            Refuse<InvalidDataException>(() => f.Journal.ReadMount(f.Plan.Id, f.Part));
            Rewrite(json => json.Replace("\"ParentFingerprint\":\"" + committed.ParentFingerprint,
                "\"ParentFingerprint\":\"" + new string('F', 64)));
            Refuse<InvalidDataException>(() => f.Journal.ReadMount(f.Plan.Id, f.Part));
            Rewrite(json => json.Insert(1, "\"unknown\":0,"));
            Refuse<InvalidDataException>(() => f.Journal.ReadMount(f.Plan.Id, f.Part));
            File.WriteAllBytes(path, terminal);
            Check(!Directory.EnumerateFiles(f.Plans.DirectoryPath, "*.preparing").Any(), "Temporary mount files leaked");

            var noEffect = Fixture(Path.Combine(directory, "no-effect")); var noOp = Guid.NewGuid();
            noEffect.Journal.PrepareMount(noEffect.Plan.Id, noEffect.Part, noOp, 17, 90, 4, before, supplied, true, out _);
            Refuse<InvalidOperationException>(() => noEffect.Journal.MountNoEffect(noEffect.Plan.Id, noEffect.Part, noOp, before, false));
            Refuse<InvalidOperationException>(() => noEffect.Journal.MountNoEffect(noEffect.Plan.Id, noEffect.Part, noOp, after, true));
            Check(noEffect.Journal.MountNoEffect(noEffect.Plan.Id, noEffect.Part, noOp, before, true).Phase == Journal.NoEffect,
                "Proven no-effect was not recorded");
            noEffect.Journal.PrepareMount(noEffect.Plan.Id, noEffect.Part, Guid.NewGuid(), 17, 90, 4, before, supplied, true, out bool retry);
            Check(retry, "Proven no-effect prevented a new operation");
            foreach (var mode in new[] { "uncommitted", "free" })
            {
                var blocked = Fixture(Path.Combine(directory, mode), mode);
                Refuse<InvalidOperationException>(() => blocked.Journal.PrepareMount(blocked.Plan.Id, blocked.Part,
                    Guid.NewGuid(), 17, 90, 4, before, supplied, true, out _));
                if (mode == "uncommitted")
                {
                    var row = new Journal.PaidRow { Prefab = "SwordIron", Before = 1, Paid = 1, X = 1, Quality = 1, Properties = supplied.Zdo };
                    Refuse<InvalidDataException>(() => blocked.Journal.Observe(blocked.Plan.Id, blocked.Part, blocked.Parent,
                        31, 9, "itemstand".GetStableHashCode(), 17, Inventory(3, 0), false, new[] { row }));
                    row.Properties = Mounted(equipped: true).Zdo;
                    Check(blocked.Journal.Observe(blocked.Plan.Id, blocked.Part, blocked.Parent, 31, 9,
                        "itemstand".GetStableHashCode(), 17, Inventory(3, 0), false, new[] { row }).Phase == Journal.Observed,
                        "Legacy payment lost its strict equipped-property comparison");
                }
            }
            var sources = Fixture(Path.Combine(directory, "sources"), "sources");
            var child = sources.Journal.PrepareMount(sources.Plan.Id, sources.Part, Guid.NewGuid(), 17, 90, 4, before, supplied, true, out bool created);
            Check(created && child.ParentFingerprint == Journal.FingerprintSources(sources.Journal.ReadPayment(sources.Plan.Id, sources.Part).Sources),
                "Mount did not bind the saved enrolled-source parent");
            Refuse<InvalidOperationException>(() => sources.Journal.PrepareMount(sources.Plan.Id, sources.Part,
                Guid.NewGuid(), 18, 90, 4, before, supplied, true, out _));
            var cleared = Fixture(Path.Combine(directory, "cancel-no-effect")); var clearedOp = Guid.NewGuid();
            cleared.Journal.PrepareMount(cleared.Plan.Id, cleared.Part, clearedOp, 17, 90, 4, before, supplied, true, out _);
            cleared.Journal.MountNoEffect(cleared.Plan.Id, cleared.Part, clearedOp, before, true);
            string malformed = Path.Combine(cleared.Plans.DirectoryPath, cleared.Plan.Id.ToString("N") + ".invalid.bwmount");
            File.WriteAllText(malformed, "");
            Refuse<InvalidDataException>(() => Cancel(cleared));
            File.Delete(malformed);
            Check(Cancel(cleared).Phase == BlueprintConstructionCancellationJournal.Prepared,
                "Proven no-effect blocked ordinary structural cancellation");
            Refuse<InvalidOperationException>(() => cleared.Journal.PrepareMount(cleared.Plan.Id, cleared.Part,
                Guid.NewGuid(), 17, 90, 4, before, supplied, true, out _));
            foreach (var sample in new[] { (Name: "SwordIron", Quality: 2, Variant: 0), (Name: "ShieldBanded", Quality: 1, Variant: 1) })
            {
                var actual = Fixture(Path.Combine(directory, "actual-" + sample.Name), prefab: sample.Name, quality: sample.Quality, variant: sample.Variant);
                var actualBefore = Inventory(1, 1, sample.Name, sample.Quality, sample.Variant);
                var actualItem = Mounted(prefab: sample.Name, quality: sample.Quality, variant: sample.Variant); var op = Guid.NewGuid();
                var prepared = actual.Journal.PrepareMount(actual.Plan.Id, actual.Part, op, 17, 90, 4, actualBefore, actualItem, true, out bool fresh);
                Check(fresh && prepared.MountedInfo == actualItem.Info, "Archived quality/variant replaced the player's actual item");
                var actualAfter = Inventory(1, 0);
                var attached = actual.Journal.ObserveMount(actual.Plan.Id, actual.Part, op, actualAfter, actualItem);
                Check(actual.Journal.ConfirmMountSaved(actual.Plan.Id, actual.Part, op, Saved(actualAfter, attached.Receipt)).Phase == Journal.Committed,
                    "Actual supplied quality/variant failed observed/save transitions");
            }
            Refuse<InvalidDataException>(() => f.Journal.PrepareMount(f.Plan.Id, f.Part, Guid.NewGuid(), 17, 90, 4,
                Inventory(1, 1, "ShieldBanded"), Mounted(prefab: "ShieldBanded"), true, out _));
            Refuse<InvalidDataException>(() => Prepare(item: Mounted(orientation: 1)));
            Console.WriteLine("PASS mounted-item journal: " + assertions + " assertions; exact supplied properties/equipped normalization, parent/payment binding, intent/observation/paired proof/replay/corruption/no-effect. Save and target witnesses are fixtures; no native attach/return/reload executed.");
            RunReturns(Path.Combine(directory, "return-journal"));

            Journal.MountRecord Prepare(Guid? op = null, BlueprintForeignPartData item = null, bool empty = true, uint id = 4, bool expectedNew = true)
            {
                var result = f.Journal.PrepareMount(f.Plan.Id, f.Part, op ?? operation, 17, 90, id, before, item ?? supplied, empty, out bool fresh);
                Check(fresh == expectedNew, "Fresh mount preparation flag differs"); return result;
            }
            Journal.MountRecord Observe(BlueprintForeignPartData inventory, BlueprintForeignPartData item = null) =>
                f.Journal.ObserveMount(f.Plan.Id, f.Part, operation, inventory, item ?? supplied);
            void Rewrite(Func<string, string> change)
            {
                string json = Encoding.UTF8.GetString(terminal, 12, BitConverter.ToInt32(terminal, 8));
                byte[] payload = Encoding.UTF8.GetBytes(change(json));
                using (var stream = File.Create(path)) using (var writer = new BinaryWriter(stream)) using (var sha = SHA256.Create())
                { writer.Write(terminal.Take(8).ToArray()); writer.Write(payload.Length); writer.Write(payload); writer.Write(sha.ComputeHash(payload)); }
            }
        }

        private static BlueprintConstructionCancellationJournal.Record Cancel((Plans Plans, Journal Journal, Plans.Record Plan, Guid Part, Guid Parent) fixture) =>
            new BlueprintConstructionCancellationJournal(fixture.Plans).Prepare(fixture.Plan.Id, fixture.Plan.Revision, Guid.NewGuid(), 17);

        private static (Plans Plans, Journal Journal, Plans.Record Plan, Guid Part, Guid Parent) Fixture(string folder, string mode = "paid",
            string prefab = "SwordIron", int quality = 1, int variant = 0, long payer = 17)
        {
            Directory.CreateDirectory(folder); var library = new B(Path.Combine(folder, "library.json"));
            var source = new B.Part { stableId = "stand", prefabName = "itemstand", displayName = "Stand",
                position = new B.VectorData(), rotation = new B.QuaternionData { w = 1 },
                scale = new B.VectorData { x = 1, y = 1, z = 1 }, baseScale = new B.VectorData { x = 1, y = 1, z = 1 }, editorVisible = true,
                foreignData = B.ForeignPartData.FromGeometry(Mounted("archive-B", 9999, prefab: prefab)) };
            Check(library.TrySaveDocument("Mount fixture", "QA", new[] { source }, Array.Empty<B.Group>(), Array.Empty<B.VectorData>(),
                "stand", null, out var blueprint, out string error), error);
            var plans = new Plans(Path.Combine(folder, "projects"), 42); var pose = new Plans.Pose(new float[] { 5, 2, 7, 0, 0, 0, 1, 1, 1, 1 });
            var plan = plans.Create(Guid.NewGuid(), blueprint, pose, new[] { pose }); var part = plan.Parts[0].Id;
            var journal = new Journal(plans); var parent = Guid.NewGuid(); var before = Inventory(3, 1, prefab, quality, variant); var after = Inventory(1, 1, prefab, quality, variant);
            var row = new Journal.PaidRow { Prefab = "Wood", Before = 3, Paid = 2, Quality = 1, Properties = Properties("Wood", 1, 0, false, 10000, null) };
            if (mode == "sources")
            {
                var token = Guid.NewGuid();
                Journal.SourceState[] Sources(bool observed) => new[] {
                    new Journal.SourceState { Creator = payer, BeforeInfo = before.Info, BeforeBytes = before.Zdo,
                        AfterInfo = observed ? after.Info : null, AfterBytes = observed ? after.Zdo : null,
                        Paid = observed ? new[] { row } : Array.Empty<Journal.PaidRow>() },
                    new Journal.SourceState { Token = token, ObjectUser = 100, ObjectId = 1, PrefabHash = "piece_chest_wood".GetStableHashCode(), Creator = payer,
                        BeforeInfo = before.Info, BeforeBytes = before.Zdo, AfterInfo = observed ? before.Info : null, AfterBytes = observed ? before.Zdo : null } };
                journal.PrepareSources(plan.Id, part, parent, payer, "fixture", new string('A', 64), new string('B', 64), Sources(false), out _);
                var observed = journal.ObserveSources(plan.Id, part, parent, 31, 9, "itemstand".GetStableHashCode(), payer, Sources(true), false);
                journal.ConfirmSavedSources(plan.Id, part, parent, new Journal.SourcesSavePair { Placement = Saved(after, observed.Receipt),
                    Sources = new[] { new Journal.SourceSave { Token = token, ObjectUser = 100, ObjectId = 1, World = 42, Generation = 8,
                        InventoryHash = Digest(Convert.FromBase64String(before.Zdo)) } } });
            }
            else
            {
                journal.Prepare(plan.Id, part, parent, payer, before, out _);
                if (mode != "uncommitted")
                {
                    bool free = mode == "free";
                    var observed = journal.Observe(plan.Id, part, parent, 31, 9, "itemstand".GetStableHashCode(), payer,
                        free ? before : after, free, free ? Array.Empty<Journal.PaidRow>() : new[] { row });
                    journal.ConfirmSaved(plan.Id, part, parent, Saved(free ? before : after, observed.Receipt));
                }
            }
            return (plans, journal, plan, part, parent);
        }
        internal static (Plans Plans, Journal Journal, Plans.Record Plan, Guid Part, Guid Parent) RuntimeFixture(string folder) => Fixture(folder, payer:100);
        private static Journal.SavePair Saved(BlueprintForeignPartData inventory, byte[] receipt) => new Journal.SavePair {
            BeforeGeneration = 7, Generation = 8, ProfileHash = new string('A', 128),
            InventoryHash = Digest(Convert.FromBase64String(inventory.Zdo)), WorldPayloadHash = Digest(receipt) };
        private static BlueprintForeignPartData Inventory(int wood, int sword, string prefab = "SwordIron", int quality = 1, int variant = 0)
        {
            using (var output = new MemoryStream()) using (var writer = new BinaryWriter(output))
            {
                writer.Write(110); writer.Write((ushort)((wood > 0 ? 1 : 0) + (sword > 0 ? 1 : 0)));
                if (wood > 0) WriteItem(writer, "Wood", wood, 0, false, 10000, null);
                if (sword > 0) WriteItem(writer, prefab, sword, 1, true, 8123, "actual-A", quality, variant);
                string names = string.Concat(new[] { sword > 0 ? prefab : null, wood > 0 ? "Wood" : null }
                    .Where(name => name != null).OrderBy(name => name, StringComparer.Ordinal).Select(name => "|" + name));
                return new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                    "8:4:" + ((wood > 0 ? 1 : 0) + (sword > 0 ? 1 : 0)) + ":" + (wood + sword) + names,
                    Convert.ToBase64String(output.ToArray()), null);
            }
        }
        private static BlueprintForeignPartData Mounted(string custom = "actual-A", int durability = 8123, bool equipped = false,
            string prefab = "SwordIron", int quality = 1, int variant = 0, int orientation = 0) =>
            new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, prefab + ":" + variant + ":" + quality + ":" + orientation,
                Properties(prefab, 1, 1, equipped, durability, custom, quality, variant), null);
        private static string Properties(string prefab, int stack, int x, bool equipped, int durability, string custom, int quality = 1, int variant = 0)
        {
            using (var output = new MemoryStream()) using (var writer = new BinaryWriter(output))
            { writer.Write((byte)110); WriteItem(writer, prefab, stack, x, equipped, durability, custom, quality, variant); return Convert.ToBase64String(output.ToArray()); }
        }
        private static void WriteItem(BinaryWriter writer, string prefab, int stack, int x, bool equipped, int durability, string custom, int quality = 1, int variant = 0)
        {
            writer.Write(durability); writer.Write((byte)x); writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((byte)(64 | (stack == 1 ? 0 : 8) | (equipped ? 2 : 0) | (custom == null ? 0 : 128 | 32 | 1) |
                (quality == 1 ? 0 : 4) | (variant == 0 ? 0 : 16)));
            if (quality != 1) writer.Write((ushort)quality);
            if (stack != 1) writer.Write((ushort)stack);
            if (variant != 0) writer.Write(variant);
            if (custom != null) { writer.Write((long)888); writer.Write("Actual supplied"); }
            writer.Write(prefab.GetStableHashCode());
            if (custom != null) { writer.Write((byte)1); writer.Write("mod:key"); writer.Write(custom); }
            writer.Write((byte)(custom == null ? 0 : 1));
        }
        private static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Check(bool value, string why) { assertions++; if (!value) throw new Exception(why); }
        private static void Refuse<T>(Action action) where T : Exception
        { assertions++; try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    }
}
