using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using OstrixMods.BuildWorks;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

// Actual Session Begin/End and EndNative; native inventories/objects/save callbacks are substituted.
namespace PaymentOutcomeBoundary
{
    internal sealed class Inventory { internal BlueprintForeignPartData Before, After; internal bool Completed; internal Journal.PaidRow[] Paid; internal BlueprintConstructionPaymentOutcome Observation = null; }
    internal sealed class Player
    {
        internal static Player m_localPlayer;
        internal Inventory Inventory;
        internal Inventory GetInventory() => Inventory;
        internal bool NoCostCheat() => false;
    }
    internal sealed class Piece
    {
        internal sealed class Requirement { internal Piece m_resItem = new Piece(); internal int m_amount = 2; }
        internal Requirement[] m_resources = Array.Empty<Requirement>();
        public static implicit operator bool(Piece value) => value != null;
    }
    internal sealed class BlueprintConstructionRefund
    {
        private readonly Inventory inventory;
        internal readonly bool CaptureEnabled;
        internal string Error => null;
        internal BlueprintConstructionRefund(Inventory inventory, Piece.Requirement[] recipe, bool captureOutcome) { this.inventory = inventory; CaptureEnabled = captureOutcome; }
        internal void Complete() { inventory.Completed = true; }
        internal bool IsFor(Inventory other) => inventory.Observation?.ExplicitSources != true && ReferenceEquals(inventory, other);
        internal BlueprintConstructionPaymentOutcome CaptureOutcome() => inventory.Observation ?? new BlueprintConstructionPaymentOutcome {
            Sources = new[] { new BlueprintConstructionPaymentOutcome.Source { Exact = inventory.Completed,
                Items = inventory.Paid.Select(row => new BlueprintConstructionPaymentOutcome.Item {
                    Properties = row.Properties, Before = row.Before, After = inventory.Completed ? row.Before - row.Paid : -1 }).ToArray() } } };
        internal (Journal.PaidRow Item, int Before, int Paid)[] DescribePaid() =>
            inventory.Paid.Select(row => (row, row.Before, row.Paid)).ToArray();
    }
    internal static class BlueprintStorageCapture
    {
        internal static bool TrySnapshotInventory(Inventory inventory, out BlueprintForeignPartData after)
        { after = inventory.Completed ? inventory.After : inventory.Before; return after != null; }
    }
    internal static class BlueprintResourceHudView { internal static bool Free(string key) => false; }
    internal static class GlobalKeys { internal const string NoBuildCost = "free"; }
    internal static class BlueprintRotationState { internal static void RequireFreshCreation() { } }
    internal static class BuildWorksLocalization { internal static string Text(string key) => key; }
    internal sealed partial class Execution
    {
        internal Journal journal;
        internal Plans.Record plan;
        internal Plans.Part part;
        internal Guid operation;
        internal Inventory inventory;
        internal BlueprintForeignPartData before;
        internal readonly object persistence = null, storageTransfer = null;
        internal bool RequiresPlacement => true;
        internal Player player;
        internal Piece Piece = new Piece();
        internal bool Finished, nativeAttempt, nativeSuccess = false, signFailed = false;
        internal bool Saving => false;
        internal bool created => false;
        internal object zdo => null;
        internal string Status, Reason;
        internal bool Context() => true;
        private bool ObjectValid() => true;
        private static Journal.PaidRow Row(Journal.PaidRow item, int before, int paid) => item;
        private void Observe(BlueprintForeignPartData after, bool free, Journal.PaidRow[] paid) =>
            journal.Observe(plan.Id, part.Id, operation, 31, 9, "woodwall".GetStableHashCode(), 17, after, free, paid);
        internal void Refuse(string reason) { Reason = reason; Finished = true; Status = "reconciliation"; }
    }
    internal sealed partial class Session
    {
        private enum PlacementState { Armed }
        private enum SessionMode { None, BlueprintWorldPlacement, ProjectPart }
        private PlacementState state = PlacementState.Armed;
        private SessionMode sessionMode = SessionMode.ProjectPart;
        private bool automaticPlacementRunning = true;
        private Piece selectedPiece = new Piece();
        private BlueprintConstructionRefund nativeConstructionPayment;
        private Execution nativeConstructionPart;
        private bool nativeConstructionFailed;
        private bool pendingConstructionCompletion, pendingConstructionSignFailed = false;
        internal Piece pendingNativePiece;
        internal int pendingNativePlanIndex;
        internal bool allowNativeGhostUpdate;
        internal Execution projectPart;
        internal void UseWorldMode() { projectPart = null; sessionMode = SessionMode.BlueprintWorldPlacement; automaticPlacementRunning = false; }
        private void Finish() { sessionMode = SessionMode.None; }
        private void RestoreAutomaticCooldown(Player player) { }
        private void ShowStatus(Player player, string message) { }
        private void Cancel() { }
        private void CompleteNativePlacement(Player player, bool signFailed) { }
    }
    internal static class Checks
    {
        internal static void Run(string folder)
        {
            var fixture = File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "construction", "bwpp01-legacy-vectors.tsv"))
                .Select(line => line.Split('\t')).ToDictionary(line => line[0]);
            Guid project = new Guid("d7714c25-4b1a-4f65-9a25-25e5efbdbf01");
            var store = new Plans(folder, 42);
            File.WriteAllBytes(Path.Combine(folder, project.ToString("N") + ".bwproject"), Convert.FromBase64String(fixture["plan"][1]));
            var plan = store.Load(project); var part = plan.Parts.Single();
            string path = Path.Combine(folder, project.ToString("N") + "." + part.Id.ToString("N") + ".bwpart");
            var journal = new Journal(store);
            File.WriteAllBytes(path, Convert.FromBase64String(fixture["observed-paid"][1]));
            var paid = journal.Read(project, part.Id).Paid;
            File.WriteAllBytes(path, Convert.FromBase64String(fixture["intent"][1]));
            var intent = journal.Read(project, part.Id);
            var inventory = new Inventory { Before = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                fixture["before"][1], fixture["before"][2], null), After = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                fixture["after"][1], fixture["after"][2], null), Paid = paid };
            var player = Player.m_localPlayer = new Player { Inventory = inventory };
            var execution = new Execution { journal = journal, plan = plan, part = part, operation = intent.Operation, inventory = inventory, before = inventory.Before, player = player };
            var session = new Session { projectPart = execution };
            var payment = session.BeginConstructionPayment(player);
            if (!payment.CaptureEnabled) throw new Exception("Project boundary did not select durable capture");
            session.EndConstructionPayment(player, payment, null);
            if (journal.ReadPayment(project, part.Id).Legacy?.Phase == Journal.Intent)
                throw new Exception("Actual Begin/End native failure with a debit retained only Intent; partial payment was not durable.");
            int checks = 0;
            var partial = new Journal(store).ReadPayment(project, part.Id).Outcome;
            Check(partial != null && partial.Original.Operation == intent.Operation && partial.Phase == Journal.OutcomeUnsettled &&
                partial.Origin.SequenceEqual(Convert.FromBase64String(fixture["intent"][1])) &&
                partial.Observation.Sources.Single().Items.Single().After == paid[0].Before - paid[0].Paid,
                "Reload lost exact origin or captured debit");
            byte[] persisted = File.ReadAllBytes(path);
            Check(journal.RecordOutcome(project, part.Id, intent.Operation, partial.Observation, partial.Failure) && File.ReadAllBytes(path).SequenceEqual(persisted),
                "Identical outcome replay changed bytes");
            Refuse(() => journal.RecordOutcome(project, part.Id, intent.Operation, partial.Observation, "changed"));
            Refuse(() => journal.RecordOutcome(project, part.Id, Guid.NewGuid(), partial.Observation, partial.Failure));
            Check(File.ReadAllBytes(path).SequenceEqual(persisted), "Conflicting replay replaced outcome");
            Review();
            Refuse(() => journal.Read(project, part.Id));
            Refuse(() => journal.Prepare(project, part.Id, Guid.NewGuid(), 17, inventory.After, out _));

            foreach (bool swallowed in new[] { false, true })
            {
                Reset(); payment = session.BeginConstructionPayment(player);
                session.OnNativePlacementException(player, null);
                Check(ReferenceEquals(session.projectPart, execution) && !execution.Finished, "Nested abort discarded execution before outer completion");
                session.EndConstructionPayment(player, payment, swallowed ? null : new InvalidOperationException("nested native failure"));
                Check(journal.ReadPayment(project, part.Id).Outcome != null && execution.Finished && session.projectPart == null,
                    "Nested abort bypassed durable outcome or cleanup");
                Review();
            }
            Reset(); inventory.Paid = Array.Empty<Journal.PaidRow>();
            inventory.After = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, fixture["before"][1], fixture["before"][2], null);
            payment = session.BeginConstructionPayment(player); session.EndConstructionPayment(player, payment, null);
            Check(journal.ReadPayment(project, part.Id).Outcome != null, "Unchanged payer after native falsely proved all-source no-effect");

            Reset(); execution.nativeSuccess = true;
            payment = session.BeginConstructionPayment(player); session.EndConstructionPayment(player, payment, null);
            Check(File.ReadAllBytes(path).SequenceEqual(Convert.FromBase64String(fixture["observed-paid"][1])), "Successful V1 payment changed bytes or format");
            foreach (string phase in new[] { "observed-paid", "committed-paid", "no-effect" })
            {
                Reset(); payment = session.BeginConstructionPayment(player);
                File.WriteAllBytes(path, Convert.FromBase64String(fixture[phase][1]));
                session.EndConstructionPayment(player, payment, new Exception("after durable phase"));
                Check(File.ReadAllBytes(path).SequenceEqual(Convert.FromBase64String(fixture[phase][1])), "Stronger phase was downgraded");
            }
            Reset(); inventory.After = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, fixture["before"][1], fixture["before"][2], null);
            execution.StopBeforeNative("cancel before native");
            Check(File.ReadAllBytes(path).SequenceEqual(Convert.FromBase64String(fixture["no-effect"][1])), "Proven pre-native no-effect changed");

            Reset(); inventory.After = null;
            payment = session.BeginConstructionPayment(player); session.EndConstructionPayment(player, payment, new Exception("snapshot unavailable"));
            Check(journal.ReadPayment(project, part.Id).Outcome?.Observation.Sources.Single().Items.Single().After == paid[0].Before - paid[0].Paid,
                "Snapshot/error gate erased available captured debit");

            Reset(); Guid healthy = Guid.NewGuid(), unknown = Guid.NewGuid();
            inventory.Observation = new BlueprintConstructionPaymentOutcome { ExplicitSources = true, Sources = new[] {
                new BlueprintConstructionPaymentOutcome.Source { Exact = true },
                new BlueprintConstructionPaymentOutcome.Source { Token = healthy, Exact = true, Items = new[] {
                    new BlueprintConstructionPaymentOutcome.Item { Before = paid[0].Before, After = paid[0].Before - paid[0].Paid, Properties = paid[0].Properties } } },
                new BlueprintConstructionPaymentOutcome.Source { Token = unknown, Exact = false, Items = new[] {
                    new BlueprintConstructionPaymentOutcome.Item { Before = paid[0].Before, After = -1, Properties = paid[0].Properties } } } } };
            payment = session.BeginConstructionPayment(player); session.EndConstructionPayment(player, payment, new Exception("external partial"));
            partial = new Journal(store).ReadPayment(project, part.Id).Outcome;
            Check(partial.Observation.ExplicitSources && partial.Observation.Sources[1].Token == healthy &&
                partial.Observation.Sources[1].Items.Single().After == paid[0].Before - paid[0].Paid &&
                !partial.Observation.Sources[2].Exact && partial.Observation.Sources[2].Items.Single().After == -1,
                "Actual completion lost exact external/unknown observations");
            Review();

            Reset(); payment = session.BeginConstructionPayment(player);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                session.EndConstructionPayment(player, payment, new Exception("native failed"));
            Check(File.ReadAllBytes(path).SequenceEqual(Convert.FromBase64String(fixture["intent"][1])) &&
                execution.Finished && execution.Reason.Contains("Partial outcome could not be saved"), "Failed atomic replace lost origin or escaped cleanup");
            Review();
            foreach (Action<BlueprintConstructionPaymentOutcome> mutation in new Action<BlueprintConstructionPaymentOutcome>[] {
                value => value.Sources[0].Items[0].After = -1,
                value => value.Sources[0].Items[0].Properties = "not-base64",
                value => value.Sources[0].Items[0].Before = 0,
                value => value.Sources[0].Token = Guid.NewGuid(),
                value => value.ExplicitSources = true,
                value => value.Sources = Array.Empty<BlueprintConstructionPaymentOutcome.Source>() })
            {
                Reset(); payment = session.BeginConstructionPayment(player); payment.Complete(); var bad = payment.CaptureOutcome(); mutation(bad);
                Refuse(() => journal.RecordOutcome(project, part.Id, intent.Operation, bad, "bad evidence"));
                Check(File.ReadAllBytes(path).SequenceEqual(Convert.FromBase64String(fixture["intent"][1])), "Invalid observation replaced original intent");
            }
            Reset(); payment = session.BeginConstructionPayment(player); session.EndConstructionPayment(player, payment, null);
            persisted = File.ReadAllBytes(path);
            foreach (int offset in new[] { 0, 8, persisted.Length - 1 })
            {
                byte[] corrupt = (byte[])persisted.Clone(); corrupt[offset] ^= 0x20; File.WriteAllBytes(path, corrupt);
                Refuse(() => journal.ReadPayment(project, part.Id));
                Check(File.ReadAllBytes(path).SequenceEqual(corrupt), "Corrupt read rewrote evidence");
            }
            File.WriteAllBytes(path, persisted);
            foreach (var size in new[] { (Count: 32, Payload: 3500), (Count: 43, Payload: 24350) })
            {
                Reset(); var large = LargeInventory(size.Count, size.Payload);
                Journal.RequireCapacity(large.Before, large.Rows);
                File.Delete(path); journal.Prepare(project, part.Id, intent.Operation, 17, large.Before, out _);
                byte[] largeOrigin = File.ReadAllBytes(path);
                Check(largeOrigin.Length > 100000 && Convert.FromBase64String(large.Before.Zdo).Length <= BlueprintForeignPartData.MaximumContainerBytes,
                    "Large-origin test did not reach the serializer quota concern within native bounds");
                inventory.Before = inventory.After = execution.before = large.Before; inventory.Paid = large.Rows;
                payment = session.BeginConstructionPayment(player); session.EndConstructionPayment(player, payment, new Exception("large native outcome"));
                partial = new Journal(store).ReadPayment(project, part.Id).Outcome;
                Check(partial != null && partial.Origin.SequenceEqual(largeOrigin) && partial.Observation.Sources.Single().Items.Length == size.Count &&
                    partial.Observation.Sources.Single().Items.All(item => item.After == 0),
                    "Admitted large origin/observations failed real serializer roundtrip: " + execution.Reason);
                Review();
                Console.WriteLine("PASS actual BWPP03 size: nativeBytes=" + Convert.FromBase64String(large.Before.Zdo).Length +
                    "; originBytes=" + largeOrigin.Length + "; outcomeBytes=" + new FileInfo(path).Length + "; recipeItems=" + size.Count);
            }
            Reset(); var maximum = LargeInventory(43, 24350);
            File.Delete(path); journal.Prepare(project, part.Id, intent.Operation, 17, maximum.Before, out _);
            inventory.Before = inventory.After = execution.before = maximum.Before; inventory.Paid = maximum.Rows;
            inventory.Observation = new BlueprintConstructionPaymentOutcome { ExplicitSources = true,
                Sources = Enumerable.Range(0, 3).Select(index => new BlueprintConstructionPaymentOutcome.Source {
                    Token = index == 0 ? Guid.Empty : Guid.NewGuid(), Items = maximum.Rows.Select(row =>
                        new BlueprintConstructionPaymentOutcome.Item { Before = row.Before, Properties = row.Properties }).ToArray() }).ToArray() };
            byte[] admissionOrigin = File.ReadAllBytes(path);
            Refuse(() => session.BeginConstructionPayment(player));
            Check(!execution.nativeAttempt && File.ReadAllBytes(path).SequenceEqual(admissionOrigin),
                "Oversized actual serializer capacity reached native attempt or rewrote intent");
            Reset(); inventory.Observation = new BlueprintConstructionPaymentOutcome { Sources = new[] {
                new BlueprintConstructionPaymentOutcome.Source { Items = new[] { new BlueprintConstructionPaymentOutcome.Item { Before = 3 } } } } };
            payment = session.BeginConstructionPayment(player); session.EndConstructionPayment(player, payment, new Exception("optional legacy snapshot unavailable"));
            partial = new Journal(store).ReadPayment(project, part.Id).Outcome;
            Check(!partial.Observation.Sources.Single().Exact && partial.Observation.Sources.Single().Items.Single().Properties == null &&
                partial.Observation.Sources.Single().Items.Single().After == -1,
                "Unavailable optional properties became exact, zero, or undurable");
            Reset(); session.UseWorldMode();
            payment = session.BeginConstructionPayment(player);
            Check(!payment.CaptureEnabled && !execution.nativeAttempt, "Ordinary world placement enrolled durable capture or project attempt");
            session.EndConstructionPayment(player, payment, null);
            Console.WriteLine("PASS BWPP03 partial outcome: " + checks + " actual Begin/End/nested-abort/pre-native-stop + durable reload/replay/IO/corruption assertions; native inventories, objects and save callbacks substituted; no provider/game proof.");

            void Reset()
            {
                File.WriteAllBytes(path, Convert.FromBase64String(fixture["intent"][1]));
                inventory = new Inventory { Before = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                    fixture["before"][1], fixture["before"][2], null), After = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                    fixture["after"][1], fixture["after"][2], null), Paid = paid };
                player = Player.m_localPlayer = new Player { Inventory = inventory };
                execution = new Execution { journal = journal, plan = plan, part = part, operation = intent.Operation, inventory = inventory, before = inventory.Before, player = player };
                session = new Session { projectPart = execution };
            }
            void Check(bool condition, string message) { ++checks; if (!condition) throw new Exception(message); }
            void Refuse(Action action) { ++checks; try { action(); } catch (InvalidOperationException) { return; } catch (InvalidDataException) { return; } throw new Exception("Invalid evidence admitted"); }
            void Review()
            {
                Check(BlueprintConstructionCancellationAction.Read(store, project, 1, 17) == ConstructionCancellationAction.Review && Journal.HasExecutionEvidence(store, plan),
                    "Unsettled evidence escaped review or became absent");
            }
        }
        private static (BlueprintForeignPartData Before, Journal.PaidRow[] Rows) LargeInventory(int count, int payload)
        {
            var rows = new Journal.PaidRow[count];
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(110); writer.Write((ushort)count);
                for (int index = 0; index < count; ++index)
                {
                    WriteItem(writer, index, payload);
                    using (var item = new MemoryStream())
                    using (var itemWriter = new BinaryWriter(item))
                    {
                        itemWriter.Write((byte)110); WriteItem(itemWriter, index, payload);
                        string properties = Convert.ToBase64String(item.ToArray());
                        if (!BlueprintItemStandContents.TryRead(new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,
                            "Wood:0:1:0", properties, null), name => name.GetStableHashCode(), out _)) throw new Exception("Large recipe fixture is not native-readable");
                        rows[index] = new Journal.PaidRow { Prefab = "Wood", Before = 1, Paid = 1, X = index, Quality = 1, Properties = properties };
                    }
                }
                return (new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                    "256:1:" + count + ":" + count + "|Wood", Convert.ToBase64String(stream.ToArray()), null), rows);
            }
        }
        private static void WriteItem(BinaryWriter writer, int index, int payload)
        {
            writer.Write(0); writer.Write((byte)index); writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((byte)(64 | 128)); writer.Write("Wood".GetStableHashCode());
            writer.Write((byte)1); writer.Write("key"); writer.Write(new string('x', payload)); writer.Write((byte)0);
        }
    }
}
