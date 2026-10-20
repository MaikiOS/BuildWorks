using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal static partial class ProfileChecks
    {
        private static bool ReadSavedReceipt(byte[] file, out NativePlayerProfileReadback.CustomDataEvidence evidence,
            long player = 100, string name = "QA")
        {
            using (var stream = new MemoryStream(file))
                return NativePlayerProfileReadback.TryReadCustomData(stream, player, name, out evidence, out _);
        }

        private static void RunSavedReceipts(BlueprintForeignPartData expected)
        {
            int before = assertions;
            byte[] inventory = Convert.FromBase64String(expected.Zdo);
            foreach (int version in new[] { 33, 34 })
            {
                var fixture = new TailFixture(inventory, version);
                byte[] file = fixture.Profile.File();
                Check(ReadSavedReceipt(file, out var saved) && saved.CustomData[TailFixture.Receipt] == "credited-once" &&
                    saved.Player == 100 && saved.ProfileHash.Length == 128, "saved receipt read without live snapshot " + version);
                Check(saved.GetType().GetProperty("Inventory") == null && saved.GetType().GetProperty("InventoryHash") == null,
                    "receipt proof can be mistaken for full inventory evidence");
                using (var stream = new MemoryStream(file))
                    Check(NativePlayerProfileReadback.TryVerifyInventoryAndCustomData(stream, 100, "QA", expected, s => s.GetStableHashCode(), out var pair, out _) &&
                        pair.Inventory.Info == expected.Info && pair.Inventory.Zdo == expected.Zdo && pair.CustomData[TailFixture.Receipt] == "credited-once",
                        "saved credit pair must prove both exact inventory and receipt without a fresh save");
                Check(!ReadSavedReceipt(file, out _, 101) && !ReadSavedReceipt(file, out _, 100, "wrong"), "receipt player identity not bound");
                bool readOnly = false;
                try { ((IDictionary<string, string>)saved.CustomData)[TailFixture.Receipt] = "forged"; }
                catch (NotSupportedException) { readOnly = true; }
                Check(readOnly, "mutable recovery receipt evidence");
                for (int length = fixture.Offsets["tail"]; length < fixture.PlayerData.Length; length++)
                {
                    var bad = new Fixture(inventory, fixture.PlayerData.Take(length).ToArray());
                    Check(!ReadSavedReceipt(bad.File(), out var failed) && failed == null, "truncated recovery tail " + length);
                }
                foreach (string section in new[] { "recipes", "stations", "set0", "texts", "foods", "skills", "custom", "uiLength" })
                    foreach (int count in new[] { -1, int.MaxValue })
                        Check(!ReadSavedReceipt(new Fixture(inventory, fixture.ChangedInt(section, count)).File(), out _), "recovery count " + section);
                Check(!ReadSavedReceipt(new Fixture(inventory, fixture.ChangedString("customKey", TailFixture.Receipt)).File(), out _), "duplicate receipt accepted");
                Check(!ReadSavedReceipt(fixture.Profile.Changed("playerVersion", 35), out _), "unsupported receipt player schema");

                // Remove every inventory item, keeping the serialized permanent receipt. No old hash/snapshot is supplied.
                byte[] emptyInventory = new byte[] { 110, 0, 0, 0, 0, 0 };
                byte[] spent = fixture.PlayerData.Take(fixture.Offsets["tail"] - inventory.Length).Concat(emptyInventory)
                    .Concat(fixture.PlayerData.Skip(fixture.Offsets["tail"])).ToArray();
                var spentProfile = new Fixture(emptyInventory, spent);
                Check(ReadSavedReceipt(spentProfile.File(), out var spentProof) && spentProof.CustomData[TailFixture.Receipt] == "credited-once",
                    "spending the returned inventory lost the permanent receipt");
                Check(!Verify(spentProfile.File(), expected, out _), "receipt-only proof unexpectedly matched the old inventory");
                using (var stream = new MemoryStream(spentProfile.File()))
                    Check(!NativePlayerProfileReadback.TryVerifyInventoryAndCustomData(stream, 100, "QA", expected, s => s.GetStableHashCode(), out var wrongPair, out _) && wrongPair == null,
                        "receipt present but old/wrong inventory must not settle an unconfirmed credit");
                using (var stream = new MemoryStream(spentProfile.File()))
                    Check(!NativePlayerProfileReadback.TryVerifyPlayerData(stream, 100, "QA", expected, name => name.GetStableHashCode(),
                        fixture.PlayerData, out _, out _), "fresh exact Player.Save proof widened to receipt-only semantics");

                // Structurally valid native record of an unavailable mod. Recovery must not load/resolve its prefab.
                byte[] unknownInventory;
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(110); writer.Write((ushort)1); writer.Write(100); writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((byte)0); writer.Write((byte)64); writer.Write(1234567); writer.Write((byte)0);
                    writer.Flush(); unknownInventory = stream.ToArray();
                }
                byte[] unknown = fixture.PlayerData.Take(fixture.Offsets["tail"] - inventory.Length).Concat(unknownInventory)
                    .Concat(fixture.PlayerData.Skip(fixture.Offsets["tail"])).ToArray();
                Check(ReadSavedReceipt(new Fixture(unknownInventory, unknown).File(), out var unknownProof) &&
                    unknownProof.CustomData[TailFixture.Receipt] == "credited-once", "unavailable prefab prevented structural receipt lookup");
                int inventoryAt = fixture.Offsets["tail"] - inventory.Length;
                byte[] zeroHash = (byte[])unknown.Clone(); Array.Clear(zeroHash, inventoryAt + 14, 4);
                Check(ReadSavedReceipt(new Fixture(unknownInventory, zeroHash).File(), out _), "zero prefab hash prevented receipt lookup");
                byte[] unnamed = unknown.Take(inventoryAt + 14).Concat(unknown.Skip(inventoryAt + 18)).ToArray();
                unnamed[inventoryAt + 13] = 0; // Native Save omits the hash when m_dropPrefab is null.
                Check(ReadSavedReceipt(new Fixture(unknownInventory, unnamed).File(), out _), "native record without prefab prevented receipt lookup");
                byte[] customInventory;
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(110); writer.Write((ushort)1); writer.Write(100); writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((byte)0); writer.Write((byte)(32 | 64 | 128)); writer.Write(1L);
                    writer.Write(new string('a', 1023) + "😀" + new string('b', 1023) + "Ж");
                    writer.Write(1234567); writer.Write((byte)1); writer.Write("native-mod-field");
                    writer.Write(new string('x', 1023) + "😀" + new string('y', 1024 * 1024) + "Z"); writer.Write((byte)0);
                    writer.Flush(); customInventory = stream.ToArray();
                }
                byte[] customPlayer = fixture.PlayerData.Take(inventoryAt).Concat(customInventory)
                    .Concat(fixture.PlayerData.Skip(fixture.Offsets["tail"])).ToArray();
                Check(ReadSavedReceipt(new Fixture(customInventory, customPlayer).File(), out var longFields) &&
                    longFields.CustomData[TailFixture.Receipt] == "credited-once", "bounded skipping of large split-UTF8 item fields failed");
                byte[] invalidItemUtf8 = (byte[])customPlayer.Clone(); invalidItemUtf8[inventoryAt + customInventory.Length - 2] = 0xff;
                Check(!ReadSavedReceipt(new Fixture(customInventory, invalidItemUtf8).File(), out _), "invalid item UTF8 was skipped without validation");
                for (int offset = 0; offset < unknownInventory.Length; offset++)
                {
                    if (offset != 0 && offset != 4 && offset != 13 && offset != 18) continue;
                    byte[] bad = (byte[])unknown.Clone();
                    if (offset == 0) bad[fixture.Offsets["tail"] - inventory.Length] = 106;
                    if (offset == 4) bad[fixture.Offsets["tail"] - inventory.Length + 4] = 255;
                    if (offset == 13) bad[fixture.Offsets["tail"] - inventory.Length + 13] = 0; // Required prefab bit.
                    if (offset == 18) bad[fixture.Offsets["tail"] - inventory.Length + 18] = 2; // Unknown extra flags.
                    Check(!ReadSavedReceipt(new Fixture(unknownInventory, bad).File(), out _), "bad compact inventory framing " + offset);
                }
                byte[] corrupt = (byte[])file.Clone(); corrupt[5] ^= 1;
                Check(!ReadSavedReceipt(corrupt, out _), "receipt checksum not checked");
                Check(!ReadSavedReceipt(file.Concat(new byte[] { 0 }).ToArray(), out _), "receipt outer trailing bytes accepted");
                Check(!ReadSavedReceipt(new Fixture(inventory).File(), out _), "opaque legacy tail used as receipt proof");
            }

            var previousPlayer = Player.m_localPlayer;
            var previousGame = Game.instance; var previousNetwork = ZNet.instance; var previousObjects = ZDOMan.instance;
            string previousConfig = BepInEx.Paths.ConfigPath;
            var frame = new TransferChecks.Frame();
            string root = Path.Combine(Path.GetTempPath(), "BuildWorks-saved-receipt-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var profile = new PlayerProfile { Path = Path.Combine(root, "test.fch") }; Game.instance.Profile = profile;
                var fixture = new TailFixture(inventory, 34);
                File.WriteAllBytes(profile.Path, fixture.Profile.File());
                Check(BlueprintStoragePlayerProfile.TryReadSavedCustomData(frame.Player, () => true, out var proof, out var reason) &&
                    proof.CustomData[TailFixture.Receipt] == "credited-once", reason);
                Check(BlueprintStorageCapture.TrySnapshotInventory(frame.Player.GetInventory(), out var live), "credit wrapper live fixture");
                var exactPair = new TailFixture(Convert.FromBase64String(live.Zdo), 34);
                File.WriteAllBytes(profile.Path, exactPair.Profile.File());
                Check(BlueprintStoragePlayerProfile.TryCapture(frame.Player, Guid.NewGuid(), () => true, out var captured, out _) &&
                    captured.TryVerifySavedCredit(out var paired, out _) && paired.CustomData[TailFixture.Receipt] == "credited-once" &&
                    paired.Inventory.Zdo == live.Zdo, "saved credit wrapper did not read exact canonical pair");
                byte[] empty = new byte[] { 110, 0, 0, 0, 0, 0 };
                File.WriteAllBytes(profile.Path, new TailFixture(empty, 34).Profile.File());
                Check(!captured.TryVerifySavedCredit(out var rejectedPair, out _) && rejectedPair == null &&
                    BlueprintStoragePlayerProfile.TryReadSavedCustomData(frame.Player, () => true, out var receiptOnly, out _) &&
                    receiptOnly.CustomData[TailFixture.Receipt] == "credited-once",
                    "receipt-only lookup must not settle missing saved credited items");
                File.WriteAllBytes(profile.Path, fixture.Profile.File());
                frame.Player.m_customData[TailFixture.Receipt] = "not-saved";
                frame.Player.GetInventory().Items.Clear();
                Check(BlueprintStoragePlayerProfile.TryReadSavedCustomData(frame.Player, () => true, out var unchanged, out _) &&
                    unchanged.CustomData[TailFixture.Receipt] == "credited-once", "lookup trusted mutable live map or inventory");
                Check(!BlueprintStoragePlayerProfile.TryReadSavedCustomData(frame.Player, () => false, out _, out _), "context loss allowed receipt proof");
                int calls = 0;
                Check(!BlueprintStoragePlayerProfile.TryReadSavedCustomData(frame.Player, () => ++calls == 1, out var failed, out _) && failed == null,
                    "post-read context loss returned receipt evidence");
                foreach (var source in new[] { FileHelpers.FileSource.Auto, FileHelpers.FileSource.Cloud, FileHelpers.FileSource.Legacy })
                {
                    profile.m_fileSource = source;
                    Check(!BlueprintStoragePlayerProfile.TryReadSavedCustomData(frame.Player, () => true, out _, out _), "nonlocal receipt accepted");
                }
                profile.m_fileSource = FileHelpers.FileSource.Local;
                profile.Path += ".old";
                Check(!BlueprintStoragePlayerProfile.TryReadSavedCustomData(frame.Player, () => true, out _, out _), "alternate receipt filename accepted");
            }
            finally
            {
                Game.instance = previousGame; ZNet.instance = previousNetwork; ZDOMan.instance = previousObjects;
                Player.m_localPlayer = previousPlayer; BepInEx.Paths.ConfigPath = previousConfig;
                Directory.Delete(root, true);
            }
            Console.WriteLine("PASS " + (assertions - before) + " recovery receipt assertions: canonical file, spent/changed inventory, unavailable prefabs, immutable evidence and Local context; no collection/save invoked.");
        }
    }
}
