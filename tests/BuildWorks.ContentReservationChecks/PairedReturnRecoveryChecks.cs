using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal static partial class PersistenceChecks
    {
        // Actual pair framing/readback, simulated old process session and native loaded references; not a game restart.
        private sealed class SavedReturnFixture : IDisposable
        {
            internal readonly Rig Rig;
            internal readonly byte[] Receipt = { 6, 2, 9 };
            internal readonly string Path;
            internal readonly object Binding;
            internal readonly BlueprintStorageTransferJournal.Snapshot Saved;
            internal readonly byte[] Prefix;
            internal bool Current = true;
            internal SavedReturnFixture(bool oldSession = true, bool otherInstance = false, bool wrongSourceManifest = false)
            {
                Rig = new Rig(linked: true, current: () => Current);
                Check(Rig.Begin() && Rig.Finish() == BlueprintStoragePersistence.Result.Verified && Rig.Frame.Transfer.TrySettleConstruction(out _), "paired recovery forward setup");
                Binding = ParentBinding(Rig.Frame);
                var data = Rig.Frame.Placed.Container.View.Data;
                data.Strings[BlueprintConstructionPartExecution.MarkerKey] = Rig.Frame.Operation.ToString("N");
                data.ByteArrays[BlueprintConstructionPartExecution.ReceiptKey] = (byte[])Receipt.Clone();
                var reverse = PrepareCurrentReturn(Rig.Frame, Rig.Frame.Operation, Binding); Path = reverse.JournalPath;
                Check(reverse.MoveNext() == BlueprintStorageTransfer.Result.Transferred && reverse.TryBeginPersistence(out Rig.Verification, out _) &&
                    Rig.Finish() == BlueprintStoragePersistence.Result.Verified, "paired recovery saved return setup");
                Check(reverse.TryPreparePlayerReadback(out var profileRead, out _) && profileRead.TryVerify(out _, out _), "paired profile read");
                Check(profileRead.TryVerify(out var profile, out _), "paired profile proof");
                Check(reverse.TryPrepareWorldReadback(out var worldRead, out _) && worldRead.TryStart(CancellationToken.None, out _), "paired world read");
                var until = DateTime.UtcNow.AddSeconds(5);
                while (!worldRead.IsCompleted && DateTime.UtcNow < until) Thread.Sleep(1);
                Check(worldRead.TryFinish(out var world, out _), "paired world proof");
                var pair = BlueprintStorageTransferJournal.Read(Path).SavedPair;
                reverse.Dispose();
                byte[] bytes = JournalChecks.ReadBytes(Path); File.WriteAllBytes(Path, bytes.Take(Frames(bytes).Last()).ToArray());
                // Recreate a crash fixture through the real journal API; only the process witness is substituted.
                using (var writer = BlueprintStorageTransferJournal.ResumeReturn(BlueprintStorageTransferJournal.Read(Path)))
                    writer.RecordPersistence(pair.BeforeGeneration, pair.Generation, profile, world,
                        oldSession ? Guid.NewGuid() : BlueprintStoragePlayerProfile.ProcessSession,
                        otherInstance ? Rig.Frame.Player.GetInstanceID() + 1 : Rig.Frame.Player.GetInstanceID());
                if (wrongSourceManifest) ChangeObservedSourceManifest(Path);
                Saved = BlueprintStorageTransferJournal.Read(Path); Prefix = JournalChecks.ReadBytes(Path);
                var items = Rig.Frame.Player.Inventory.Items.Select(item => item.Clone()).ToArray();
                Rig.Frame.Player.Inventory.Items.Clear(); Rig.Frame.Player.Inventory.Items.AddRange(items);
                Check(BlueprintContainerInventory.TryRead(Saved.SavedPair.PlayerInventory, name => name.GetStableHashCode(), out var rows), "paired projection rows");
                foreach (var row in rows) Rig.Frame.Player.Inventory.GetItemAt(row.X, row.Y).m_durability = (float)((float)row.DurabilityHundredths.Value * .01f);
                data.m_uid = new ZDOID { User = 77, Id = 999 }; Rig.Frame.Placed.Container.LoadForTest(Rig.Frame.Operation.ToString("N"));
            }
            internal BlueprintStorageTransfer Begin()
            {
                try
                {
                    return (BlueprintStorageTransfer)typeof(BlueprintStorageTransfer).GetMethod("ResumePairedReturn", PrivateStatic).Invoke(null,
                        new object[] { Saved, Rig.Frame.Player, Rig.Frame.Placed.Piece, Rig.Frame.Placed.Container, Rig.Frame.Placed.RootView, Binding, Receipt });
                }
                catch (TargetInvocationException error) { throw error.InnerException; }
            }
            internal void World(byte[] receipt = null, bool separate = false)
                => WorldGenerationChecks.WriteWorld(Rig.Frame, Rig.Root, SaveSystem.Generation, receipt: receipt ?? Receipt, separateReceipt: separate);
            public void Dispose() { Rig.Dispose(); }
        }

        private static void SplitReceiptChunks(SavedReturnFixture fixture)
        {
            fixture.World(separate: true);
            var expected = NativeWorldStorageReadback.Expectation.ForExactBlob(1, fixture.Rig.Frame.Operation, 123, 100,
                ZDOVars.s_creator, BlueprintConstructionPartExecution.MarkerKey, BlueprintConstructionPartExecution.ReceiptKey, fixture.Receipt);
            Check(NativeWorldStorageFiles.TryVerify(fixture.Rig.Root, SaveSystem.Generation, expected, out var receipt, out _), "split fixture receipt read");
            uint generation = SaveSystem.Generation;
            string chunk = System.IO.Path.Combine(fixture.Rig.Root, "01_02__3_" + generation + ".chunk");
            byte[] bytes = File.ReadAllBytes(chunk); int split = checked((int)receipt.ObjectOffset);
            byte[] Header(byte[] objects)
            {
                using (var memory = new MemoryStream())
                using (var writer = new BinaryWriter(memory)) { writer.Write((short)41); writer.Write(1); writer.Write(objects); return memory.ToArray(); }
            }
            File.WriteAllBytes(chunk, Header(bytes.Skip(6).Take(split - 6).ToArray()));
            File.WriteAllBytes(System.IO.Path.Combine(fixture.Rig.Root, "02_03__3_" + generation + ".chunk"), Header(bytes.Skip(split).ToArray()));
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            {
                writer.Write((short)41); writer.Write(2); writer.Write(2);
                foreach (ushort key in new ushort[] { 258, 515 }) { writer.Write(key); writer.Write((byte)3); writer.Write(generation); writer.Write(1); }
                File.WriteAllBytes(System.IO.Path.Combine(fixture.Rig.Root, "_main." + generation + ".chunks"), memory.ToArray());
            }
        }

        private static BlueprintStorageTransfer.PairedRecoveryResult FinishPaired(BlueprintStorageTransfer transfer)
        {
            var until = DateTime.UtcNow.AddSeconds(5);
            var state = BlueprintStorageTransfer.PairedRecoveryResult.Pending;
            while (state == BlueprintStorageTransfer.PairedRecoveryResult.Pending && DateTime.UtcNow < until)
            { state = transfer.TickPairedReturnRecovery(out _); if (state == BlueprintStorageTransfer.PairedRecoveryResult.Pending) Thread.Sleep(1); }
            Check(state != BlueprintStorageTransfer.PairedRecoveryResult.Pending, "paired verifier worker timed out");
            return state;
        }

        private static void ChangeProfileMetadata(string path)
        {
            byte[] file = File.ReadAllBytes(path);
            int length = BitConverter.ToInt32(file, 0); byte[] payload = file.Skip(4).Take(length).ToArray();
            // First native profile statistic; retain every inventory byte and rebuild the real envelope digest.
            Array.Copy(BitConverter.GetBytes(123f), 0, payload, 12, 4);
            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output))
            using (var hash = SHA512.Create())
            {
                writer.Write(payload.Length); writer.Write(payload); writer.Write(64); writer.Write(hash.ComputeHash(payload));
                File.WriteAllBytes(path, output.ToArray());
            }
        }

        private static void ChangeObservedSourceManifest(string path)
        {
            var observed = BlueprintStorageTransferJournal.Read(path).Observations.Single(); observed.SourceManifest = new string('A', 64);
            byte[] payload;
            using (var memory = new MemoryStream())
            using (var fields = new BinaryWriter(memory))
            {
                typeof(BlueprintStorageTransferJournal).GetMethod("WriteObservation", PrivateStatic).Invoke(null, new object[] { fields, observed });
                payload = memory.ToArray();
            }
            byte[] input = JournalChecks.ReadBytes(path), previous = new byte[32];
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory))
            using (var hash = SHA256.Create())
            {
                writer.Write(input.Take(8).ToArray());
                foreach (int offset in Frames(input))
                {
                    int length = BitConverter.ToInt32(input, offset); byte[] body = input.Skip(offset + 4).Take(length).ToArray();
                    if (body[24] == 3) body = body.Take(57).Concat(payload).ToArray();
                    Array.Copy(previous, 0, body, 25, 32); previous = hash.ComputeHash(body);
                    writer.Write(body.Length); writer.Write(body); writer.Write(previous);
                }
                File.WriteAllBytes(path, memory.ToArray());
            }
        }

        private static void CheckPairedReturnRecovery()
        {
            foreach (string mode in new[] { "same-generation", "later-generation", "lower-generation", "metadata-before", "worker-probe", "caller-receipt-mutation" })
            using (var fixture = new SavedReturnFixture())
            {
                Console.WriteLine("Paired recovery fixture: " + mode);
                var rig = fixture.Rig; var frame = rig.Frame;
                if (mode == "later-generation") { SaveSystem.Generation++; fixture.World(); }
                if (mode == "lower-generation") { SaveSystem.Generation = 1; fixture.World(); }
                if (mode == "metadata-before") ChangeProfileMetadata(rig.Profile.Path);
                int moves = frame.Player.Inventory.MoveCalls, profiles = rig.ProfileSaves, worlds = rig.WorldSaves;
                using (var recovered = fixture.Begin())
                {
                    if (mode == "caller-receipt-mutation") fixture.Receipt[0] ^= 1;
                    Check(recovered.RecoveringSavedPair && !recovered.CompleteInMemory && recovered.MoveNext() == BlueprintStorageTransfer.Result.Unchanged &&
                        !recovered.TryBeginPersistence(out _, out _) && !recovered.TrySettleConstruction(out _), "paired recovery entered normal move/save/session settlement path");
                    bool duplicate = false;
                    try { fixture.Begin(); } catch (IOException) { duplicate = true; }
                    Check(duplicate, "paired return allowed second writer");
                    if (mode == "worker-probe")
                    {
                        Check(Task.Run(() => recovered.TickPairedReturnRecovery(out _)).Result == BlueprintStorageTransfer.PairedRecoveryResult.Refused &&
                            fixture.Prefix.SequenceEqual(JournalChecks.ReadBytes(fixture.Path)), "worker completed/poisoned paired recovery");
                    }
                    BlueprintStorageTransfer.ReturnProducer = () => recovered;
                    var tick = new ContentsReturnTickProbe(() => { });
                    var until = DateTime.UtcNow.AddSeconds(5);
                    while (!tick.Finished && DateTime.UtcNow < until) { tick.Tick(); Thread.Sleep(1); }
                    Check(tick.Finished && tick.Status == "returned", tick.Reason ?? "paired coordinator incomplete");
                    var final = BlueprintStorageTransferJournal.Read(fixture.Path); byte[] complete = JournalChecks.ReadBytes(fixture.Path);
                    Check(final.Settled && final.Frames == fixture.Saved.Frames + 1 && complete.Take(fixture.Prefix.Length).SequenceEqual(fixture.Prefix) &&
                        complete[Frames(complete).Last() + 28] == 6 && final.SavedPair.Generation == fixture.Saved.SavedPair.Generation &&
                        final.SavedPair.SourceSession == fixture.Saved.SavedPair.SourceSession && !BlueprintStorageTransferJournal.HasUnresolved(1),
                        "pair recovery rewrote old prefix/pair or appended more than Kind6");
                    Check(frame.Player.Inventory.MoveCalls == moves && rig.ProfileSaves == profiles && rig.WorldSaves == worlds,
                        "paired verifier moved an item or requested another save");
                    Check(recovered.TickPairedReturnRecovery(out _) == BlueprintStorageTransfer.PairedRecoveryResult.Settled &&
                        complete.SequenceEqual(JournalChecks.ReadBytes(fixture.Path)), "paired verifier repeated Kind6");
                }
                bool replay = false; try { fixture.Begin(); } catch (InvalidOperationException) { replay = true; }
                Check(replay, "settled pair was reopened");
            }
            foreach (string fault in new[] { "same-process", "same-process-other-instance", "live-player-stack", "live-player-property", "live-player-cell", "canonical-profile",
                "live-storage", "live-receipt", "live-marker", "world-receipt", "split-objects", "split-chunks", "missing-current-ok", "other6", "legacy4", "corrupt-sibling",
                "late-generation", "late-directory", "late-profile-reference", "late-profile-metadata", "late-player", "late-receipt", "late-parent", "late-other6", "cancel", "wrong-source-manifest" })
            using (var fixture = new SavedReturnFixture(oldSession: !fault.StartsWith("same-process"), otherInstance: fault == "same-process-other-instance", wrongSourceManifest: fault == "wrong-source-manifest"))
            {
                var rig = fixture.Rig; var frame = rig.Frame; var data = frame.Placed.Container.View.Data;
                if (fault == "live-player-stack") frame.Player.Inventory.Items[0].m_stack++;
                if (fault == "live-player-property") frame.Player.Inventory.Items[0].Custom = "changed";
                if (fault == "live-player-cell") frame.Player.Inventory.Items[0].m_gridPos = new Vector2i(7, 3);
                if (fault == "canonical-profile") { byte[] bytes = File.ReadAllBytes(rig.Profile.Path); bytes[bytes.Length - 1] ^= 1; File.WriteAllBytes(rig.Profile.Path, bytes); }
                if (fault == "live-storage") { var item = Program.Item("Wood", 1); item.m_gridPos = new Vector2i(0, 0); frame.Placed.Container.Inventory.Items.Add(item); frame.Placed.Container.SaveForTest(); }
                if (fault == "live-receipt") data.ByteArrays[BlueprintConstructionPartExecution.ReceiptKey] = new byte[] { 1 };
                if (fault == "live-marker") data.Strings[BlueprintStorageCapture.TransferMarkerKey] = "";
                if (fault == "world-receipt") fixture.World(new byte[] { 7 });
                if (fault == "split-objects") fixture.World(separate: true);
                if (fault == "split-chunks") SplitReceiptChunks(fixture);
                if (fault == "missing-current-ok")
                {
                    SaveSystem.Generation++; fixture.World();
                    File.Delete(System.IO.Path.Combine(rig.Root, "_main." + SaveSystem.Generation + ".ok"));
                }
                if (new[] { "other6", "legacy4", "corrupt-sibling" }.Contains(fault)) OtherHold(rig, fault);
                bool refused = false;
                try
                {
                    using (var recovered = fixture.Begin())
                    {
                        if (fault == "late-generation") SaveSystem.Generation++;
                        if (fault == "late-directory") ZNet.instance.World.Directory = rig.Root + ".other";
                        if (fault == "late-profile-reference") Game.instance.Profile = new PlayerProfile { Path = rig.Profile.Path };
                        if (fault == "late-profile-metadata") ChangeProfileMetadata(rig.Profile.Path);
                        if (fault == "late-player") frame.Player.Inventory.Items[0].Custom = "changed";
                        if (fault == "late-receipt") data.ByteArrays[BlueprintConstructionPartExecution.ReceiptKey] = new byte[] { 1 };
                        if (fault == "late-parent") fixture.Current = false;
                        if (fault == "late-other6") OtherHold(rig, "other6");
                        if (fault == "cancel") recovered.Dispose();
                        refused = FinishPaired(recovered) == BlueprintStorageTransfer.PairedRecoveryResult.Refused;
                    }
                }
                catch (Exception error) when (error is InvalidOperationException || error is InvalidDataException || error is IOException) { refused = true; }
                Check(refused && fixture.Prefix.SequenceEqual(JournalChecks.ReadBytes(fixture.Path)) &&
                    BlueprintStorageTransferJournal.Read(fixture.Path).RequiresReconciliation && rig.ProfileSaves == 2 && rig.WorldSaves == 2 &&
                    frame.Player.Inventory.MoveCalls == 1, "invalid paired recovery wrote/moved/saved: " + fault);
            }
            BlueprintStorageTransfer.ReturnProducer = null;
            Console.WriteLine("PASS actual paired return verifier/coordinator: current canonical profile/full native-load projection, two real mapped-world proofs on same generation/object, Kind6-only completion; no moves/saves/pair rewrite, 26 refusals including split objects/chunks and valid-chain contradictory source manifest. Old process session and paid-parent/native host substituted, no actual game restart proof.");
        }
    }
}
