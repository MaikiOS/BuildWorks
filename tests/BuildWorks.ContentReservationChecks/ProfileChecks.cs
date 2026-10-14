using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal static partial class FileHelpers { internal enum FileSource { Auto = 1, Local = 2, Cloud = 4, Legacy = 8 } }
    internal sealed class PlayerProfile
    {
        internal FileHelpers.FileSource m_fileSource = FileHelpers.FileSource.Local;
        internal string Filename = "test", Name = "QA", Path;
        internal long Id = 100;
        internal static Action SavingFinished;
        internal string GetPath() => Path;
        internal string GetFilename() => Filename;
        internal string GetName() => Name;
        internal long GetPlayerID() => Id;
    }

    internal static class ProfileChecks
    {
        private static int assertions;
        private static void Check(bool value, string reason) { assertions++; if (!value) throw new Exception("Profile: " + reason); }
        private sealed class Fixture
        {
            internal byte[] Payload;
            internal readonly Dictionary<string, int> Offset = new Dictionary<string, int>();
            internal Fixture(byte[] inventory, byte[] playerData = null)
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
                {
                    Action<string> mark = key => Offset.Add(key, (int)stream.Position);
                    writer.Write(46); writer.Write(205); writer.Write(10);
                    for (int group = 0; group < 10; group++)
                    {
                        for (int i = 0; i < 205; i++) writer.Write((float)i);
                        if (group == 0) mark("mapCount");
                        for (int i = 0; i < 3; i++) writer.Write(0);
                        if (group == 0) mark("enemyCount");
                        writer.Write(5); for (int i = 0; i < 10; i++) writer.Write(0);
                    }
                    mark("firstSpawn"); writer.Write(true); mark("worldCount"); writer.Write(1);
                    writer.Write(123L);
                    for (int point = 0; point < 3; point++) { writer.Write(false); for (int i = 0; i < 3; i++) writer.Write(0f); }
                    for (int i = 0; i < 3; i++) writer.Write(0f);
                    writer.Write(true); mark("mapLength"); writer.Write(3); writer.Write(new byte[] { 1, 2, 3 });
                    mark("name"); writer.Write("QA"); mark("id"); writer.Write(100L);
                    writer.Write(""); writer.Write(false); writer.Write(123L);
                    mark("havePlayer"); writer.Write(true); mark("playerLength"); writer.Write(0);
                    int begin = (int)stream.Position; mark("playerVersion"); writer.Write(33);
                    for (int i = 0; i < 4; i++) writer.Write(10f);
                    mark("guardian"); writer.Write(""); writer.Write(0f); mark("inventory"); writer.Write(inventory);
                    writer.Write(new byte[] { 90, 91, 92 }); // Deliberately opaque tail, no full Player.Load claim.
                    if (playerData != null) { stream.Position = begin; writer.Write(playerData); stream.SetLength(stream.Position); }
                    int end = (int)stream.Position; stream.Position = Offset["playerLength"]; writer.Write(end - begin);
                    writer.Flush(); Payload = stream.ToArray();
                }
            }
            internal byte[] File(byte[] payload = null)
            {
                payload = payload ?? Payload;
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                using (var sha = SHA512.Create())
                { writer.Write(payload.Length); writer.Write(payload); writer.Write(64); writer.Write(sha.ComputeHash(payload)); writer.Flush(); return stream.ToArray(); }
            }
            internal byte[] Changed(string key, int value)
            { var result = (byte[])Payload.Clone(); Array.Copy(BitConverter.GetBytes(value), 0, result, Offset[key], 4); return File(result); }
            internal byte[] ChangedByte(string key, byte value)
            { var result = (byte[])Payload.Clone(); result[Offset[key]] = value; return File(result); }
        }
        internal static byte[] FileFor(Player player)
        {
            BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var data);
            var package = new ZPackage(); player.Save(package);
            return new Fixture(Convert.FromBase64String(data.Zdo), package.GetArray()).File();
        }
        private static bool Verify(byte[] file, BlueprintForeignPartData expected, out NativePlayerProfileReadback.Evidence evidence)
        {
            using (var stream = new MemoryStream(file))
                return NativePlayerProfileReadback.TryVerify(stream, 100, "QA", expected, name => name.GetStableHashCode(), out evidence, out _);
        }
        internal static void Run()
        {
            var frame = new TransferChecks.Frame();
            Check(frame.Begin() && frame.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "transfer setup");
            Check(BlueprintStorageCapture.TrySnapshotInventory(frame.Player.GetInventory(), out var expected), "snapshot setup");
            var fixture = new Fixture(Convert.FromBase64String(expected.Zdo)); byte[] valid = fixture.File();
            Check(Verify(valid, expected, out var evidence) && evidence.Player == 100 && evidence.ProfileHash.Length == 128 &&
                evidence.InventoryHash.Length == 64 && evidence.InventoryBytes == Convert.FromBase64String(expected.Zdo).Length, "exact profile not verified");
            foreach (int length in new[] { 0, 1, 3, 4, 71, 72, valid.Length - 65, valid.Length - 64, valid.Length - 1 })
                Check(!Verify(valid.Take(length).ToArray(), expected, out _), "truncation accepted " + length);
            foreach (int offset in new[] { 0, 4, valid.Length - 65, valid.Length - 1 })
            { byte[] bad = (byte[])valid.Clone(); bad[offset] ^= 1; Check(!Verify(bad, expected, out _), "corruption accepted"); }
            Check(!Verify(valid.Concat(new byte[] { 0 }).ToArray(), expected, out _), "outer trailing byte accepted");
            foreach (var change in new[] { ("mapCount", -1), ("mapCount", int.MaxValue), ("enemyCount", 6), ("worldCount", -1),
                ("worldCount", 1025), ("mapLength", -1), ("mapLength", int.MaxValue), ("playerLength", -1),
                ("playerLength", int.MaxValue), ("playerVersion", 32), ("inventory", 106), ("id", 101) })
                Check(!Verify(fixture.Changed(change.Item1, change.Item2), expected, out _), "rehashed invalid field accepted " + change);
            Check(!Verify(fixture.ChangedByte("firstSpawn", 2), expected, out _), "noncanonical bool");
            Check(!Verify(fixture.ChangedByte("havePlayer", 0), expected, out _), "missing player");
            Check(!Verify(fixture.ChangedByte("name", 0xff), expected, out _), "invalid string framing");
            Check(!Verify(fixture.ChangedByte("guardian", 0xff), expected, out _), "invalid player prefix");
            Check(!Verify(fixture.ChangedByte("name", 1), expected, out _), "wrong player name");
            byte[] wrong = (byte[])fixture.Payload.Clone(); wrong[0] = 45;
            Check(!Verify(fixture.File(wrong), expected, out _), "old profile");
            wrong = (byte[])fixture.Payload.Clone(); wrong[fixture.Offset["inventory"] + 6] ^= 1;
            Check(!Verify(fixture.File(wrong), expected, out _), "different provided durability accepted");
            using (var tooLarge = new MemoryStream(new byte[NativePlayerProfileReadback.MaximumProfileBytes + 1]))
                Check(!NativePlayerProfileReadback.TryVerify(tooLarge, 100, "QA", expected, name => name.GetStableHashCode(), out _, out _), "oversized file");
            string root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BuildWorks-profile-check-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var profile = new PlayerProfile { Path = System.IO.Path.Combine(root, "test.fch") }; Game.instance.Profile = profile;
                Check(frame.Transfer.TryPreparePlayerReadback(out var receipt, out string error), error);
                Check(receipt.Operation == frame.Operation && !receipt.TryVerify(out _, out _), "missing canonical profile accepted");
                System.IO.File.WriteAllBytes(profile.Path + ".new", valid);
                Check(!receipt.TryVerify(out _, out _), "temporary file substituted");
                System.IO.File.WriteAllBytes(profile.Path, valid);
                Check(receipt.TryVerify(out _, out error), error);
                Check(BlueprintStorageTransferJournal.Read(frame.Transfer.JournalPath).RequiresReconciliation &&
                    !BlueprintStorageCapture.CanRemoveEmpty(frame.Placed, frame.Player), "local readback released world hold");
                foreach (var source in new[] { FileHelpers.FileSource.Auto, FileHelpers.FileSource.Cloud, FileHelpers.FileSource.Legacy })
                {
                    profile.m_fileSource = source;
                    Check(!receipt.TryVerify(out _, out _) && !frame.Transfer.TryPreparePlayerReadback(out _, out _), "nonlocal readback accepted");
                }
                profile.m_fileSource = FileHelpers.FileSource.Local;
                profile.Id++; Check(!receipt.TryVerify(out _, out _), "profile identity change"); profile.Id--;
                profile.Path += ".old"; Check(!receipt.TryVerify(out _, out _), "alternate profile path"); profile.Path = profile.Path.Substring(0, profile.Path.Length - 4);
                frame.Item.m_durability -= 1; Check(!receipt.TryVerify(out _, out _), "changed live source accepted"); frame.Item.m_durability += 1;
                Check(receipt.TryVerify(out _, out _), "restored unchanged state denied");
                frame.Placed.Container.View.Data.Strings.Clear(); Check(!receipt.TryVerify(out _, out _), "missing target marker accepted");
            }
            finally { Game.instance.Profile = null; Directory.Delete(root, true); }
            var pending = new TransferChecks.Frame(); Check(pending.Begin() && !pending.Transfer.TryPreparePlayerReadback(out _, out _), "unperformed transfer accepted");
            Console.WriteLine("PASS " + assertions + " canonical-profile assertions: bounded current parser, integrity, identity, exact provided items and real fixture filesystem; no game saves invoked.");
        }
    }
}
