using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal sealed partial class Player
    {
        internal readonly Dictionary<string, string> m_customData = new Dictionary<string, string>();
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        internal void Save(ZPackage package)
        {
            BlueprintStorageCapture.TrySnapshotInventory(GetInventory(), out var inventory);
            var writer = package.Writer;
            writer.Write(33); for (int i = 0; i < 4; i++) writer.Write(10f);
            writer.Write(""); writer.Write(0f); writer.Write(Convert.FromBase64String(inventory.Zdo));
            ProfileChecks.WriteEmptyNativeTail(writer, m_customData);
        }
    }

    internal static partial class PersistenceChecks
    {
        private static int assertions;
        private static void Check(bool value, string reason) { assertions++; if (!value) throw new InvalidOperationException("Persistence: " + reason); }
        private sealed class Rig : IDisposable
        {
            internal readonly string Root = Path.Combine(Path.GetTempPath(), "BuildWorks-pair-" + Guid.NewGuid().ToString("N"));
            internal readonly TransferChecks.Frame Frame = new TransferChecks.Frame();
            internal readonly PlayerProfile Profile;
            internal BlueprintStoragePersistence Verification;
            internal int ProfileSaves, WorldSaves;
            internal Rig(bool durabilityBoundary = false, bool linked = false, Func<bool> current = null)
            {
                Directory.CreateDirectory(Root); SaveSystem.Generation = 7; SaveSystem.Flags = 0; ZNet.Open = false;
                ZNet.instance.World.Directory = Root;
                Profile = new PlayerProfile { Path = Path.Combine(Root, "test.fch") }; Game.instance.Profile = Profile;
                if (durabilityBoundary)
                {
                    var hammer = Program.Item("Hammer", 1); hammer.m_gridPos = new Vector2i(1, 0); hammer.m_durability = 18.879f;
                    Frame.Player.Inventory.Items.Add(hammer);
                }
                Check((linked ? ConstructionTransferBindingChecks.Begin(Frame, current ?? (() => true)) : Frame.Begin()) &&
                    Frame.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred, Frame.Error ?? "transfer setup");
                File.WriteAllBytes(Profile.Path, ProfileChecks.FileFor(Frame.Player));
                Check(Frame.Transfer.TryPreparePlayerReadback(out var prior, out _) && prior.TryVerify(out _, out _), "old file is not a valid matching-inventory control");
                Game.instance.SaveAction = SaveProfile;
                ZNet.instance.SaveAction = SaveWorld;
            }
            internal void SaveProfile()
            {
                ProfileSaves++;
                var writer = new FileWriter(Profile.Path + ".new", ProfileChecks.FileFor(Frame.Player));
                writer.Finish();
                FileHelpers.ReplaceOldFile(Profile.Path, Profile.Path + ".new", Profile.Path + ".old", null, FileHelpers.FileSource.Local);
                PlayerProfile.SavingFinished?.Invoke();
            }
            internal void SaveWorld()
            {
                WorldSaves++; SaveSystem.Generation++;
                Frame.Placed.Container.View.Data.ByteArrays.TryGetValue(BlueprintConstructionPartExecution.ReceiptKey, out var receipt);
                WorldGenerationChecks.WriteWorld(Frame, Root, SaveSystem.Generation, receipt: receipt);
            }
            internal bool Begin() => Frame.Transfer.TryBeginPersistence(out Verification, out _);
            internal BlueprintStoragePersistence.Result Finish()
            {
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (Verification.State != BlueprintStoragePersistence.Result.Verified && Verification.State != BlueprintStoragePersistence.Result.Refused && DateTime.UtcNow < deadline)
                { Verification.Tick(); Thread.Sleep(1); }
                Check(Verification.State == BlueprintStoragePersistence.Result.Verified || Verification.State == BlueprintStoragePersistence.Result.Refused, "worker fixture timed out");
                return Verification.State;
            }
            public void Dispose()
            {
                Frame.Transfer.Dispose(); SaveSystem.Flags = 0; SaveSystem.Generation = 7; ZNet.Open = false;
                Game.instance.SaveAction = null; ZNet.instance.SaveAction = null;
                FileHelpers.ReplaceThrows = false; FileHelpers.LeaveTemporary = false;
                Directory.Delete(Root, true);
            }
        }

        internal static void Run()
        {
            CheckPairedReturnRecovery();
            CheckReturnResume();
            CheckProjectReturn();
            using (var rig = new Rig(linked: true))
            {
                var tick = new PartContentsTickProbe(rig.Frame.Transfer);
                string path = rig.Frame.Transfer.JournalPath;
                Check(!BlueprintStorageTransferJournal.Read(path).Settled, "fixture was already settled");
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!tick.Finished && DateTime.UtcNow < deadline) { tick.Tick(); Thread.Sleep(1); }
                Check(tick.Finished && tick.Status == "committed" && BlueprintStorageTransferJournal.Read(path).Settled,
                    tick.Reason ?? "actual project contents tick did not settle the linked transfer");
                Check(rig.ProfileSaves == 1 && rig.WorldSaves == 1 && rig.Frame.Placed.Container.Inventory.MoveCalls == 1,
                    "contents coordinator repeated a native move or save");
                byte[] settled = File.ReadAllBytes(path); tick.Tick();
                Check(settled.SequenceEqual(File.ReadAllBytes(path)) && rig.ProfileSaves == 1 && rig.WorldSaves == 1,
                    "finished coordinator replayed settlement/save");
            }
            bool validParent = true;
            using (var rig = new Rig(linked: true, current: () => validParent))
            {
                var tick = new PartContentsTickProbe(rig.Frame.Transfer);
                string path = rig.Frame.Transfer.JournalPath; validParent = false;
                tick.Tick();
                Check(tick.Finished && tick.Status == "reconciliation" && !BlueprintStorageTransferJournal.Read(path).Settled &&
                    rig.ProfileSaves == 0 && rig.WorldSaves == 0 && rig.Frame.Placed.Container.Inventory.MoveCalls == 1,
                    "actual contents tick saved/settled/replayed after losing parent authority");
            }
            validParent = true;
            using (var rig = new Rig(linked: true, current: () => validParent))
            {
                var tick = new PartContentsTickProbe(rig.Frame.Transfer);
                string path = rig.Frame.Transfer.JournalPath;
                ZNet.instance.SaveAction = () => { rig.SaveWorld(); validParent = false; };
                tick.Tick();
                Check(tick.Finished && tick.Status == "reconciliation" && !BlueprintStorageTransferJournal.Read(path).Settled &&
                    rig.Frame.Placed.Container.Inventory.MoveCalls == 1,
                    "actual contents tick settled or repeated movement after a save callback invalidated its parent");
            }
            using (var rig = new Rig(durabilityBoundary: true))
            {
                rig.Frame.Player.m_customData["BuildWorks.StorageSaveProof"] = "previous-owned-value";
                Check(rig.Begin(), "exact save refused");
                Check(rig.Finish() == BlueprintStoragePersistence.Result.Verified, rig.Verification.Reason);
                Check(rig.Verification.TryVerifyCurrent(out var currentError), currentError);
                SaveSystem.Generation++;
                Check(!rig.Verification.TryVerifyCurrent(out _), "stale generation remained settlement authority");
                SaveSystem.Generation--;
                Check(!Task.Run(() => rig.Verification.TryVerifyCurrent(out _)).Result, "settlement proof accepted another thread");
                float providedDurability = rig.Frame.Item.m_durability;
                rig.Frame.Item.m_durability = 0;
                Check(!rig.Verification.TryVerifyCurrent(out _), "changed provided properties remained settlement authority");
                rig.Frame.Item.m_durability = providedDurability;
                Check(rig.Verification.TryVerifyCurrent(out currentError), currentError);
                var saved = BlueprintStorageTransferJournal.Read(rig.Frame.Transfer.JournalPath);
                Check(saved.Schema == 5 && saved.Terminal && saved.SavedPair != null && saved.SavedPair.BeforeGeneration == 7 &&
                    saved.SavedPair.Generation == 8 && saved.SavedPair.Player == 100 && saved.SavedPair.World == 1 &&
                    saved.SavedPair.Marker == rig.Frame.Operation && saved.SavedPair.ProfileHash.Length == 128 &&
                    saved.SavedPair.WorldInventoryHash == saved.Observations.Last().ZdoHash && saved.SavedPair.PlayerInventory != null &&
                    saved.SavedPair.SourceSession == BlueprintStoragePlayerProfile.ProcessSession &&
                    saved.SavedPair.SourcePlayerInstance == rig.Frame.Player.GetInstanceID(), "paired WAL evidence missing");
                Check(saved.RequiresReconciliation && !BlueprintStorageCapture.CanRemoveEmpty(rig.Frame.Placed, rig.Frame.Player), "pair authorized settlement or destructive cleanup");
                StorageSettlementChecks.Run(rig.Frame, rig.Root, saved);
                Check(rig.Frame.Player.m_customData["BuildWorks.StorageSaveProof"] == "previous-owned-value", "prior player metadata overwritten");
                Check(File.ReadAllBytes(rig.Profile.Path).SequenceEqual(ProfileChecks.FileFor(rig.Frame.Player)), "saved player metadata changed by verification");
                Check(!rig.Frame.Transfer.TryBeginPersistence(out _, out _) && rig.ProfileSaves == 1 && rig.WorldSaves == 1, "automatic save replay");
                string copy = Path.Combine(rig.Root, Path.GetFileName(rig.Frame.Transfer.JournalPath));
                byte[] bytes;
                using (var file = new FileStream(rig.Frame.Transfer.JournalPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(file)) bytes = reader.ReadBytes(checked((int)file.Length));
                int pairOffset = 8; for (int i = 0; i < 4; i++) pairOffset += BitConverter.ToInt32(bytes, pairOffset) + 36;
                File.WriteAllBytes(copy, bytes.Take(pairOffset).ToArray());
                Check(BlueprintStorageTransferJournal.Read(copy).SavedPair == null && BlueprintStorageTransferJournal.Read(copy).RequiresReconciliation, "crash before pair lost hold");
                ReturnChecks.WriteLegacy(copy, 3);
                Check(BlueprintStorageTransferJournal.Read(copy).Schema == 3 && BlueprintStorageTransferJournal.Read(copy).SavedPair == null &&
                    BlueprintStorageTransferJournal.Read(copy).RequiresReconciliation, "legacy v3 compatibility or hold lost");
                File.WriteAllBytes(copy, bytes.Take(bytes.Length - 1).ToArray());
                bool refused = false; try { BlueprintStorageTransferJournal.Read(copy); } catch (InvalidDataException) { refused = true; }
                Check(refused, "torn pair accepted");
                File.WriteAllBytes(copy, bytes.Concat(bytes.Skip(pairOffset)).ToArray());
                refused = false; try { BlueprintStorageTransferJournal.Read(copy); } catch (InvalidDataException) { refused = true; }
                Check(refused, "duplicate pair accepted");
                int size = BitConverter.ToInt32(bytes, pairOffset);
                byte[] body = bytes.Skip(pairOffset + 4).Take(size).ToArray();
                foreach (int field in new[] { 57, 61, 65, 73, 81, 99, 228, 293 })
                {
                    var changed = (byte[])body.Clone(); changed[field] ^= 1;
                    if (field == 99 || field == 228 || field == 293) changed[field] = (byte)(body[field] == (byte)'0' ? '1' : '0');
                    using (var output = new MemoryStream())
                    using (var writer = new BinaryWriter(output))
                    using (var hash = SHA256.Create())
                    { writer.Write(bytes.Take(pairOffset).ToArray()); writer.Write(changed.Length); writer.Write(changed); writer.Write(hash.ComputeHash(changed)); File.WriteAllBytes(copy, output.ToArray()); }
                    refused = false; try { BlueprintStorageTransferJournal.Read(copy); } catch (InvalidDataException) { refused = true; }
                    // Profile hash is not a signature; inventory hash must match its retained exact bytes.
                    Check(field == 99 ? !refused : refused, "checksummed pair field validation " + field);
                }
                File.WriteAllBytes(copy, bytes); ReturnChecks.WriteLegacy(copy, 4);
                var legacy4 = BlueprintStorageTransferJournal.Read(copy);
                Check(legacy4.Schema == 4 && legacy4.SavedPair != null && legacy4.SavedPair.PlayerInventory == null &&
                    legacy4.RequiresReconciliation, "legacy v4 inferred a native projection or released custody");
                LoadedInventoryChecks.Run(rig.Frame, saved.SavedPair, rig.Profile.Path);
            }
            foreach (string fault in new[] { "open-failed", "close-failed", "already-finished", "double-finish", "wrong-path", "no-writer", "replace-throw", "replace-no-move" })
            using (var rig = new Rig())
            {
                // A fully valid stale temporary file must not pass a failed writer or replacement.
                byte[] stale = ProfileChecks.FileFor(rig.Frame.Player);
                File.WriteAllBytes(rig.Profile.Path + ".new", stale);
                Game.instance.SaveAction = () =>
                {
                    var writer = new FileWriter(rig.Profile.Path + (fault == "wrong-path" ? ".other" : ".new"), stale,
                        fault == "open-failed" ? FileWriter.WriterStatus.OpenFailed : fault == "already-finished" ? FileWriter.WriterStatus.CloseSucceeded : FileWriter.WriterStatus.OpenSucceeded);
                    writer.FailClose = fault == "close-failed";
                    if (fault != "no-writer") writer.Finish();
                    if (fault == "double-finish") writer.Finish();
                    FileHelpers.ReplaceThrows = fault == "replace-throw";
                    FileHelpers.LeaveTemporary = fault == "replace-no-move";
                    FileHelpers.ReplaceOldFile(rig.Profile.Path, rig.Profile.Path + ".new", rig.Profile.Path + ".old", null, FileHelpers.FileSource.Local);
                    PlayerProfile.SavingFinished?.Invoke();
                };
                Check(!rig.Begin(), "failed current writer/replacement accepted stale temporary file " + fault);
                Check(BlueprintStorageTransferJournal.Read(rig.Frame.Transfer.JournalPath).SavedPair == null, "failed writer produced pair " + fault);
                Check(!rig.Frame.Player.m_customData.ContainsKey("BuildWorks.StorageSaveProof"), "verification changed metadata " + fault);
            }
            // Failure and exception paths release the observer; the next exact native-shaped save can succeed.
            using (var rig = new Rig())
            {
                rig.SaveProfile();
                Check(File.ReadAllBytes(rig.Profile.Path).SequenceEqual(ProfileChecks.FileFor(rig.Frame.Player)), "inactive observer altered ordinary save");
                Check(rig.Begin() && rig.Finish() == BlueprintStoragePersistence.Result.Verified, "observer leaked after writer failures");
                Check(File.ReadAllBytes(rig.Profile.Path).SequenceEqual(ProfileChecks.FileFor(rig.Frame.Player)), "verification introduced saved metadata");
            }
            foreach (string fault in new[] { "old-file", "missing-callback", "duplicate-callback", "wrong-thread", "profile-throw", "cloud", "character-block", "world-block", "client", "dedicated", "open", "peer", "busy", "zero", "overflow", "world-skip" })
            using (var rig = new Rig())
            {
                switch (fault)
                {
                    case "old-file": Game.instance.SaveAction = () => PlayerProfile.SavingFinished?.Invoke(); break;
                    case "missing-callback": Game.instance.SaveAction = () => File.WriteAllBytes(rig.Profile.Path, ProfileChecks.FileFor(rig.Frame.Player)); break;
                    case "duplicate-callback": Game.instance.SaveAction = () => { rig.SaveProfile(); PlayerProfile.SavingFinished?.Invoke(); }; break;
                    case "wrong-thread": Game.instance.SaveAction = () => { File.WriteAllBytes(rig.Profile.Path, ProfileChecks.FileFor(rig.Frame.Player)); Task.Run(() => PlayerProfile.SavingFinished?.Invoke()).Wait(); }; break;
                    case "profile-throw": Game.instance.SaveAction = () => throw new IOException("native profile fixture failure"); break;
                    case "cloud": rig.Profile.m_fileSource = FileHelpers.FileSource.Cloud; break;
                    case "character-block": SaveSystem.Flags = SaveSystemSessionFlags.DontSaveCharacter; break;
                    case "world-block": SaveSystem.Flags = SaveSystemSessionFlags.DontSaveWorld; break;
                    case "client": ZNet.instance.Server = false; break;
                    case "dedicated": ZNet.instance.Dedicated = true; break;
                    case "open": ZNet.Open = true; break;
                    case "peer": ZNet.instance.Peers.Add(new object()); break;
                    case "busy": ZNet.instance.Saving = true; break;
                    case "zero": SaveSystem.Generation = 0; break;
                    case "overflow": SaveSystem.Generation = uint.MaxValue; break;
                    case "world-skip": ZNet.instance.SaveAction = () => { }; break;
                }
                Check(!rig.Begin(), "precondition/save failure accepted " + fault);
                Check(BlueprintStorageTransferJournal.Read(rig.Frame.Transfer.JournalPath).SavedPair == null, "failed start recorded pair " + fault);
                Check(!rig.Frame.Player.m_customData.ContainsKey("BuildWorks.StorageSaveProof"), "temporary metadata leaked " + fault);
                Check(!rig.Frame.Transfer.TryBeginPersistence(out _, out _), "failed request replayed " + fault);
            }
            foreach (string fault in new[] { "wrong-generation", "missing-world", "changed-profile", "changed-custody", "host", "cancel", "dispose" })
            using (var rig = new Rig())
            {
                // Start an asynchronous world save before it increments its native generation.
                ZNet.instance.SaveAction = () => { rig.WorldSaves++; ZNet.instance.Saving = true; };
                Check(rig.Begin() && rig.Verification.Tick() == BlueprintStoragePersistence.Result.Saving, "async before-BeginSave race refused");
                if (fault == "cancel") rig.Verification.Cancel();
                if (fault == "dispose") rig.Frame.Transfer.Dispose();
                SaveSystem.Generation = fault == "wrong-generation" ? 9u : 8u;
                if (fault != "missing-world") WorldGenerationChecks.WriteWorld(rig.Frame, rig.Root, SaveSystem.Generation);
                ZNet.instance.Saving = false;
                if (fault == "changed-profile") { rig.Frame.Player.m_customData["changed-after-save"] = "fixture"; File.WriteAllBytes(rig.Profile.Path, ProfileChecks.FileFor(rig.Frame.Player)); }
                if (fault == "changed-custody") rig.Frame.Item.m_durability--;
                if (fault == "host") ZNet.instance = new ZNet { World = ZNet.instance.World };
                Check(rig.Finish() == BlueprintStoragePersistence.Result.Refused, "changed/cancelled operation accepted " + fault);
                Check(BlueprintStorageTransferJournal.Read(rig.Frame.Transfer.JournalPath).SavedPair == null, "partial pair recorded " + fault);
            }
            using (var rig = new Rig())
            {
                ZNet.instance.SaveAction = () => ZNet.instance.Saving = true;
                Check(rig.Begin(), "delayed save did not start");
                Check(Task.Run(() => rig.Verification.Tick()).Result == BlueprintStoragePersistence.Result.Refused, "worker accepted game receipts");
                Check(rig.Verification.State == BlueprintStoragePersistence.Result.Saving, "off-thread probe mutated owner state");
                SaveSystem.Generation = 8; WorldGenerationChecks.WriteWorld(rig.Frame, rig.Root, 8); ZNet.instance.Saving = false;
                Check(rig.Finish() == BlueprintStoragePersistence.Result.Verified, rig.Verification.Reason);
            }
            using (var rig = new Rig())
            {
                var returned = rig.Frame.Transfer;
                Check(returned.TryBeginReturn(out var reverse, out string error), error);
                Check(reverse.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "return setup");
                Check(reverse.TryBeginPersistence(out var proof, out error), error);
                rig.Verification = proof;
                Check(rig.Finish() == BlueprintStoragePersistence.Result.Verified, proof.Reason);
                Check(BlueprintStorageTransferJournal.Read(reverse.JournalPath).SavedPair.Marker == rig.Frame.Operation &&
                    BlueprintStorageTransferJournal.Read(reverse.JournalPath).Prepared.Operation != rig.Frame.Operation, "return child lost parent marker");
                reverse.Dispose();
            }
            Console.WriteLine("PASS " + assertions + " paired persistence assertions; actual adapters/journal, inert save API and temporary files; no native game save, settlement or network proof.");
        }
        private static BlueprintStorageTransfer PrepareCurrentReturn(TransferChecks.Frame frame, Guid operation, object binding)
        {
            var items = frame.Placed.Container.Inventory.Items.Select(item => new BlueprintItemStandPlacement.Reservation(
                item, item.m_stack, BlueprintItemStandPlacement.SnapshotItem(item))).ToArray();
            try
            {
                return (BlueprintStorageTransfer)typeof(BlueprintStorageTransfer).GetMethod("PrepareReturn",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null,
                    new object[] { operation, frame.Player, frame.Placed.Piece, frame.Placed.Container, frame.Placed.RootView, items, binding });
            }
            catch (System.Reflection.TargetInvocationException error) { throw error.InnerException; }
        }
        private static void CheckProjectReturn()
        {
            foreach (bool unmarked in new[] { false, true })
            using (var rig = new Rig(linked: true))
            {
                Check(rig.Begin() && rig.Finish() == BlueprintStoragePersistence.Result.Verified &&
                    rig.Frame.Transfer.TrySettleConstruction(out _), "forward fixture not settled");
                var parent = BlueprintStorageTransferJournal.Read(rig.Frame.Transfer.JournalPath).Prepared;
                object binding = typeof(BlueprintStorageTransfer).GetField("construction",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(rig.Frame.Transfer);
                // Current contents differ from the original supplied recipe: one stack spent, another added/damaged.
                var chest = rig.Frame.Placed.Container;
                chest.Inventory.Items[0].m_stack = 1;
                var added = Program.Item("Stone", 4, "current-added"); added.m_durability = 1.25f; added.m_gridPos = new Vector2i(1, 0);
                chest.Inventory.Items.Add(added); chest.SaveForTest();
                if (unmarked) chest.View.Data.Set(BlueprintStorageCapture.TransferMarkerKey, "");
                chest.SyncCacheForTest();
                int starts = 0;
                BlueprintStorageTransfer reverse = null;
                BlueprintStorageTransfer.ReturnProducer = () => { starts++; return reverse = PrepareCurrentReturn(rig.Frame, parent.Operation, binding); };
                var tick = new ContentsReturnTickProbe(() => { });
                Check(!tick.Finished && starts == 1 && rig.Frame.Player.Inventory.MoveCalls == 0, "prepared coordinator moved contents before Tick");
                if (unmarked)
                {
                    tick.Tick();
                    Check(!tick.Finished && rig.Frame.Player.Inventory.MoveCalls == 0, "marker synchronization was not transient");
                    var loaded = chest.Inventory.Items.Select(item => item.Clone()).ToArray();
                    chest.Inventory.Items.Clear(); chest.Inventory.Items.AddRange(loaded);
                    chest.LoadForTest(parent.Operation.ToString("N")); tick.Tick();
                }
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!tick.Finished && DateTime.UtcNow < deadline) { tick.Tick(); Thread.Sleep(1); }
                Check(tick.Finished && tick.Status == "returned" && chest.Inventory.Items.Count == 0, tick.Reason ?? "return not finished");
                var received = rig.Frame.Player.Inventory.Items.Single(item => item.m_dropPrefab.name == "Stone");
                Check(BlueprintItemStandPlacement.SnapshotItem(received, true) == BlueprintItemStandPlacement.SnapshotItem(added, true) &&
                    received.m_stack == 4 && received.m_durability == 1.25f &&
                    rig.Frame.Player.Inventory.Items.Where(item => item.m_dropPrefab.name == "Wood").Sum(item => item.m_stack) == 2 &&
                    rig.Frame.Player.Inventory.MoveCalls == 2 && rig.ProfileSaves == 2 && rig.WorldSaves == 2,
                    "current native stacks/properties or exactly-once pair differ");
                var history = BlueprintStorageTransferJournal.ReadCancellation(parent.ConstructionProject, parent.ConstructionPart,
                    parent.Operation, parent.World, parent.Player, parent.ConstructionFingerprint, parent.Paid);
                Check(history.Forward.Settled && history.Returned.Settled && history.Returned.Prepared.Paid.Length == 0 &&
                    !BlueprintStorageTransferJournal.HasUnresolved(parent.World), "return did not settle against saved linked parent");
                bool refused = false;
                try { BlueprintStorageTransferJournal.ReadConstruction(parent.ConstructionProject, parent.ConstructionPart,
                    parent.Operation, parent.World, parent.Player, parent.ConstructionFingerprint, parent.Paid); }
                catch (InvalidOperationException) { refused = true; }
                Check(refused, "forward completion reused after return");
                byte[] bytes = File.ReadAllBytes(reverse.JournalPath); tick.Tick();
                Check(starts == 1 && bytes.SequenceEqual(File.ReadAllBytes(reverse.JournalPath)) && rig.Frame.Player.Inventory.MoveCalls == 2,
                    "finished return coordinator replayed");
                chest.Inventory.Items.Add(Program.Item("Stone", 1)); chest.SaveForTest(); chest.SyncCacheForTest();
                bool repeated = false;
                try { PrepareCurrentReturn(rig.Frame, parent.Operation, binding); } catch (InvalidOperationException) { repeated = true; }
                Check(repeated && bytes.SequenceEqual(File.ReadAllBytes(reverse.JournalPath)) && rig.Frame.Player.Inventory.MoveCalls == 2,
                    "existing child was recreated for later additions");
                // Read-only restart join does not require an old forward file for a manually filled paid shell.
                string forwardPath = rig.Frame.Transfer.JournalPath, moved = forwardPath + ".fixture";
                rig.Frame.Transfer.Dispose();
                File.Move(forwardPath, moved);
                try
                {
                    var onlyReturn = BlueprintStorageTransferJournal.ReadCancellation(parent.ConstructionProject, parent.ConstructionPart,
                        parent.Operation, parent.World, parent.Player, parent.ConstructionFingerprint, parent.Paid);
                    Check(onlyReturn.Forward == null && onlyReturn.Returned.Settled, "return-only linked shell lost durable child");
                }
                finally { File.Move(moved, forwardPath); }
                bool wrongParent = false;
                try { BlueprintStorageTransferJournal.ReadCancellation(Guid.NewGuid(), parent.ConstructionPart, parent.Operation,
                    parent.World, parent.Player, parent.ConstructionFingerprint, parent.Paid); }
                catch (InvalidDataException) { wrongParent = true; }
                Check(wrongParent, "different construction parent reused return");
            }
            foreach (string failure in new[] { "context", "unchanged", "save" })
            using (var rig = new Rig(linked: true))
            {
                Check(rig.Begin() && rig.Finish() == BlueprintStoragePersistence.Result.Verified && rig.Frame.Transfer.TrySettleConstruction(out _),
                    "refusal fixture not settled");
                object binding = typeof(BlueprintStorageTransfer).GetField("construction",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(rig.Frame.Transfer);
                BlueprintStorageTransfer.ReturnProducer = () => PrepareCurrentReturn(rig.Frame, rig.Frame.Operation, binding);
                bool valid = true;
                var tick = new ContentsReturnTickProbe(() => { if (!valid) throw new InvalidOperationException("board moved"); });
                if (failure == "context") valid = false;
                if (failure == "unchanged") rig.Frame.Player.Inventory.NoChange = true;
                if (failure == "save") Game.instance.SaveAction = () => throw new IOException("fixture save failed");
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (!tick.Finished && DateTime.UtcNow < deadline) { tick.Tick(); Thread.Sleep(1); }
                Check(tick.Finished && tick.Status == "reconciliation" && rig.Frame.Player.Inventory.MoveCalls == (failure == "context" ? 0 : 1) &&
                    BlueprintStorageTransferJournal.HasUnresolved(1), "failed " + failure + " return continued or released hold");
            }
            int emptyChecks = 0; BlueprintStorageTransfer.ReturnProducer = () => { emptyChecks++; return null; };
            Check(new ContentsReturnTickProbe(() => { }, 2).Finished && emptyChecks == 2, "already empty parts were not checked separately");
            BlueprintStorageTransfer.ReturnProducer = () => throw new InvalidOperationException("unsettled child");
            bool interrupted = false;
            try { new ContentsReturnTickProbe(() => { }); } catch (InvalidOperationException) { interrupted = true; }
            Check(interrupted, "interrupted child was skipped");
            BlueprintStorageTransfer.ReturnProducer = null;
            Console.WriteLine("PASS actual return factory and coordinator Advance/Tick: changed native stacks, marked/unmarked wait, paired settlement, parent/forward joins, no replay, empty/context/interrupted refusal. Plan lookup/producer authority substituted; no game/reload/removal proof.");
        }
    }
}
