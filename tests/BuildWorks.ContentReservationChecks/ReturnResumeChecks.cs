using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace ReservationChecks
{
    internal static partial class PersistenceChecks
    {
        private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private static object ParentBinding(TransferChecks.Frame frame) => typeof(BlueprintStorageTransfer).GetField("construction", PrivateInstance).GetValue(frame.Transfer);
        private static BlueprintStorageTransfer ResumeCurrent(TransferChecks.Frame frame, BlueprintStorageTransferJournal.Snapshot saved, object binding)
        {
            try
            {
                return (BlueprintStorageTransfer)typeof(BlueprintStorageTransfer).GetMethod("ResumeCurrentReturn", PrivateStatic).Invoke(null,
                    new object[] { saved, frame.Player, frame.Placed.Piece, frame.Placed.Container, frame.Placed.RootView, binding });
            }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static List<int> Frames(byte[] bytes)
        {
            var offsets = new List<int>();
            for (int i = 8; i < bytes.Length; i += BitConverter.ToInt32(bytes, i) + 36) offsets.Add(i);
            return offsets;
        }
        private static void OpenInChild(string path, bool success)
        {
            var info = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--open-return-journal \"" + path + "\"") {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var child = Process.Start(info))
            {
                if (!child.WaitForExit(30000)) { child.Kill(); throw new Exception("Return journal child timed out."); }
                Check((child.ExitCode == 0) == success, "cross-process journal lock/open differs: " + child.StandardError.ReadToEnd());
            }
        }
        private static void OtherHold(Rig rig, string kind)
        {
            var plan = BlueprintStorageTransferJournal.Read(rig.Frame.Transfer.JournalPath).Prepared;
            plan.Operation = Guid.NewGuid(); plan.ConstructionPart = Guid.NewGuid();
            if (kind != "other6") { plan.ConstructionProject = plan.ConstructionPart = Guid.Empty; plan.ConstructionFingerprint = null; }
            if (kind == "otherworld4") plan.World = 2;
            string path;
            using (var journal = BlueprintStorageTransferJournal.Begin(BlueprintStorageTransferJournal.Root, plan)) path = journal.Path;
            if (kind == "legacy4" || kind == "otherworld4")
            { ReturnChecks.WriteLegacy(path, 4); Check(BlueprintStorageTransferJournal.Read(path).Schema == 4, "legacy sibling fixture invalid"); }
            if (kind == "corrupt-sibling") File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        }
        private static void CheckReturnResume()
        {
            foreach (string stage in new[] { "prepare", "unmarked-prepare", "prefix", "observed-final", "terminal", "otherworld4" })
            using (var rig = new Rig(linked: true))
            {
                Check(rig.Begin() && rig.Finish() == BlueprintStoragePersistence.Result.Verified && rig.Frame.Transfer.TrySettleConstruction(out _), "resume forward setup");
                var frame = rig.Frame; var chest = frame.Placed.Container;
                var added = Program.Item("Stone", 3, "added-current"); added.m_gridPos = new Vector2i(1, 0); chest.Inventory.Items.Add(added); chest.SaveForTest();
                object binding = ParentBinding(frame);
                var original = PrepareCurrentReturn(frame, frame.Operation, binding); string path = original.JournalPath;
                if (stage == "prefix" || stage == "observed-final" || stage == "terminal") Check(original.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "resume prefix setup");
                if (stage == "observed-final" || stage == "terminal") Check(original.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "resume final setup");
                original.Dispose();
                if (stage == "observed-final")
                { byte[] bytes = JournalChecks.ReadBytes(path); File.WriteAllBytes(path, bytes.Take(Frames(bytes).Last()).ToArray()); }
                if (stage == "unmarked-prepare") chest.View.Data.Strings[BlueprintStorageCapture.TransferMarkerKey] = "";
                // Fresh item references, remapped native UID and reset revision; not a native game/process reload.
                var source = chest.Inventory.Items.Select(item => item.Clone()).ToArray(); chest.Inventory.Items.Clear(); chest.Inventory.Items.AddRange(source);
                var receiver = frame.Player.Inventory.Items.Select(item => item.Clone()).ToArray(); frame.Player.Inventory.Items.Clear(); frame.Player.Inventory.Items.AddRange(receiver);
                chest.View.Data.m_uid = new ZDOID { User = 81, Id = 93 };
                chest.LoadForTest(stage == "unmarked-prepare" ? "" : frame.Operation.ToString("N"));
                if (stage == "otherworld4") OtherHold(rig, stage);
                var saved = BlueprintStorageTransferJournal.Read(path); byte[] prefix = JournalChecks.ReadBytes(path);
                int moves = frame.Player.Inventory.MoveCalls, remaining = chest.Inventory.Items.Count;
                OpenInChild(path, true);
                Check(prefix.SequenceEqual(JournalChecks.ReadBytes(path)), "child open modified evidence");
                using (var resumed = ResumeCurrent(frame, saved, binding))
                {
                    OpenInChild(path, false);
                    bool duplicate = false;
                    try { ResumeCurrent(frame, BlueprintStorageTransferJournal.Read(path), binding); } catch (IOException) { duplicate = true; }
                    Check(duplicate, "second writer claimed return");
                    Check(resumed.OperationId == saved.Prepared.Operation && JournalChecks.ReadBytes(path).Take(prefix.Length).SequenceEqual(prefix), "resume changed old ID/hash prefix");
                    BlueprintStorageTransfer.ReturnProducer = () => resumed;
                    var tick = new ContentsReturnTickProbe(() => { });
                    if (stage == "unmarked-prepare")
                    {
                        tick.Tick();
                        var loaded = chest.Inventory.Items.Select(item => item.Clone()).ToArray();
                        chest.Inventory.Items.Clear(); chest.Inventory.Items.AddRange(loaded);
                        chest.LoadForTest(frame.Operation.ToString("N"));
                    }
                    var deadline = DateTime.UtcNow.AddSeconds(5);
                    while (!tick.Finished && DateTime.UtcNow < deadline) { tick.Tick(); Thread.Sleep(1); }
                    Check(tick.Finished && tick.Status == "returned", tick.Reason ?? "resumed coordinator unfinished");
                    var finished = BlueprintStorageTransferJournal.Read(path);
                    Check(finished.Settled && finished.Prepared.TargetUser != chest.View.Data.m_uid.UserID && finished.Prepared.TargetId != chest.View.Data.m_uid.ID,
                        "resume rewrote old native IDs or did not settle");
                    Check(frame.Player.Inventory.MoveCalls == moves + remaining && chest.Inventory.Items.Count == 0 &&
                        rig.ProfileSaves == 2 && rig.WorldSaves == 2 && !BlueprintStorageTransferJournal.HasUnresolved(1), "completed rows replayed or save/hold differs");
                    byte[] done = JournalChecks.ReadBytes(path); tick.Tick();
                    Check(done.SequenceEqual(JournalChecks.ReadBytes(path)) && frame.Player.Inventory.MoveCalls == moves + remaining, "finished resumed coordinator repeated effects");
                }
            }
            foreach (string fault in new[] { "pending", "unchanged", "uncertain", "paired", "settled", "stale-reader", "torn", "checksum",
                "source-stack", "source-properties", "source-extra", "receiver-properties", "receiver-extra", "grid", "marker", "cache", "owner", "parent", "other6", "legacy4", "corrupt-sibling", "late-other6",
                "marker-reload-changed", "legacy4-unmarked", "prepared-only-reader" })
            {
                bool current = true;
                using (var rig = new Rig(linked: true, current: () => current))
                {
                    Check(rig.Begin() && rig.Finish() == BlueprintStoragePersistence.Result.Verified && rig.Frame.Transfer.TrySettleConstruction(out _), "negative resume forward setup");
                    var frame = rig.Frame; var chest = frame.Placed.Container;
                    var stone = Program.Item("Stone", 2); stone.m_gridPos = new Vector2i(1, 0); chest.Inventory.Items.Add(stone); chest.SaveForTest();
                    object binding = ParentBinding(frame); var original = PrepareCurrentReturn(frame, frame.Operation, binding);
                    string path = original.JournalPath; var stale = BlueprintStorageTransferJournal.Read(path);
                    if (fault == "unchanged") frame.Player.Inventory.NoChange = true;
                    if (fault == "uncertain") frame.Player.Inventory.AfterCredit = () => throw new IOException("fixture native callback");
                    if (fault != "marker-reload-changed" && fault != "legacy4-unmarked") original.MoveNext();
                    frame.Player.Inventory.NoChange = false; frame.Player.Inventory.AfterCredit = null;
                    if (fault == "paired" || fault == "settled")
                    {
                        original.MoveNext(); Check(original.TryBeginPersistence(out rig.Verification, out _) && rig.Finish() == BlueprintStoragePersistence.Result.Verified, "paired resume fixture");
                        if (fault == "settled") Check(original.TrySettleConstruction(out _), "settled resume fixture");
                    }
                    original.Dispose();
                    if (fault == "pending") { byte[] bytes = JournalChecks.ReadBytes(path); File.WriteAllBytes(path, bytes.Take(Frames(bytes)[2]).ToArray()); }
                    var saved = BlueprintStorageTransferJournal.Read(path);
                    if (fault == "prepared-only-reader") saved = BlueprintStorageTransferJournal.Read(path, preparedOnly: true);
                    if (fault == "stale-reader") saved = stale;
                    if (fault == "torn") { byte[] bytes = JournalChecks.ReadBytes(path); File.WriteAllBytes(path, bytes.Take(bytes.Length - 1).ToArray()); }
                    if (fault == "checksum") { byte[] bytes = JournalChecks.ReadBytes(path); bytes[bytes.Length - 1] ^= 1; File.WriteAllBytes(path, bytes); }
                    if (fault == "source-stack") chest.Inventory.Items[0].m_stack++;
                    if (fault == "source-properties") chest.Inventory.Items[0].Custom = "changed";
                    if (fault == "source-extra") { var extra = Program.Item("Wood", 1); extra.m_gridPos = new Vector2i(2, 0); chest.Inventory.Items.Add(extra); }
                    if (fault == "receiver-properties") frame.Item.Custom = "changed";
                    if (fault == "receiver-extra") { var extra = Program.Item("Wood", 1); extra.m_gridPos = new Vector2i(3, 0); frame.Player.Inventory.Items.Add(extra); }
                    if (fault == "grid") frame.Player.Inventory.Width--;
                    if (fault == "marker") chest.View.Data.Strings[BlueprintStorageCapture.TransferMarkerKey] = Guid.NewGuid().ToString("N");
                    if (fault == "owner") chest.View.Owner = false;
                    if (fault == "parent") current = false;
                    if (new[] { "other6", "legacy4", "corrupt-sibling" }.Contains(fault)) OtherHold(rig, fault);
                    if (fault == "legacy4-unmarked") OtherHold(rig, "legacy4");
                    if (fault == "marker-reload-changed" || fault == "legacy4-unmarked") chest.View.Data.Strings[BlueprintStorageCapture.TransferMarkerKey] = "";
                    chest.SaveForTest(); if (fault == "cache") chest.MakeRevisionStale();
                    byte[] before = JournalChecks.ReadBytes(path); int moves = frame.Player.Inventory.MoveCalls;
                    bool refused = false;
                    try
                    {
                        using (var resumed = ResumeCurrent(frame, saved, binding))
                        {
                            if (fault == "marker-reload-changed")
                            {
                                var loaded = chest.Inventory.Items.Select(item => item.Clone()).ToArray(); loaded[0].Custom = "changed during native load";
                                chest.Inventory.Items.Clear(); chest.Inventory.Items.AddRange(loaded); chest.LoadForTest(frame.Operation.ToString("N"));
                                Check(resumed.MoveNext() == BlueprintStorageTransfer.Result.Uncertain && BlueprintStorageTransferJournal.Read(path).Observations.Count == 0,
                                    "changed cache reload entered native move or synthesized observation");
                            }
                            else
                            {
                                if (fault != "late-other6") throw new Exception("Invalid return resume accepted: " + fault);
                                frame.Player.BeforeUnequip = () => OtherHold(rig, "other6");
                                Check(resumed.MoveNext() != BlueprintStorageTransfer.Result.Transferred, "late sibling did not revoke native move");
                            }
                            refused = true;
                        }
                    }
                    catch (Exception error) when (error is InvalidOperationException || error is IOException || error is InvalidDataException || error is System.UnauthorizedAccessException) { refused = true; }
                    Check(refused && frame.Player.Inventory.MoveCalls == moves, "invalid resume moved a stack: " + fault);
                    if (fault != "late-other6") Check(before.SequenceEqual(JournalChecks.ReadBytes(path)), "refused resume changed evidence: " + fault);
                    if (fault == "legacy4-unmarked") Check(string.IsNullOrEmpty(BlueprintStorageCapture.TransferMarker(chest.View.Data)), "legacy hold allowed marker mutation");
                    Check(BlueprintStorageTransferJournal.HasUnresolved(1) || fault == "settled", "refused resume cleared hold: " + fault);
                }
            }
            BlueprintStorageTransfer.ReturnProducer = null;
            Console.WriteLine("PASS actual linked return resume: prepare/prefix/final-observation/terminal, fresh item references/remapped UID, locked same writer and separate-process open; suffix-only moves/pair/settlement; pending/changed/other6/legacy4/corrupt/late-hold refusal. Host/paid-parent authority substituted, no native reload proof.");
        }
    }
}
