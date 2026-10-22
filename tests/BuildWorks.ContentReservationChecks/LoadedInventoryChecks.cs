using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal static class LoadedInventoryChecks
    {
        private static int assertions;
        private static void Check(bool value, string message)
        { assertions++; if (!value) throw new InvalidOperationException("Loaded inventory: " + message); }

        internal static void Run(TransferChecks.Frame frame, BlueprintStorageTransferJournal.Persistence proof, string profilePath)
        {
            var original = Player.m_localPlayer;
            byte[] profileBytes = File.ReadAllBytes(profilePath);
            try
            {
                Check9412(profilePath);
                File.WriteAllBytes(profilePath, profileBytes); Player.m_localPlayer = original;
                Check(BlueprintStoragePlayerProfile.TryReadSavedCredit(original, proof.PlayerInventory, () => true, out var sameProcess, out var error) &&
                    sameProcess.InventoryHash == proof.PlayerInventoryHash && sameProcess.CustomData != null, "same-process saved pair: " + error);
                Check(!BlueprintStoragePlayerProfile.TryVerifyLoaded(frame.Player, proof, () => true, out _, out _), "same Player used as a reload");
                var loaded = new Player();
                loaded.Inventory.Width = frame.Player.Inventory.Width; loaded.Inventory.Height = frame.Player.Inventory.Height;
                loaded.Inventory.Items.AddRange(frame.Player.Inventory.Items.Select(item => item.Clone()));
                Check(BlueprintContainerInventory.TryRead(proof.PlayerInventory, name => name.GetStableHashCode(), out var rows), "paired rows invalid");
                foreach (var row in rows) loaded.Inventory.GetItemAt(row.X, row.Y).m_durability = (float)row.DurabilityHundredths.Value * 0.01f;
                Player.m_localPlayer = loaded;
                Check(BlueprintStorageLoadedInventory.Matches(loaded.Inventory, proof.PlayerInventory), "exact native Load projection rejected");
                Check(BlueprintStoragePlayerProfile.TryReadSavedCredit(loaded, proof.PlayerInventory, () => true, out var savedCredit, out error) &&
                    savedCredit.InventoryHash == proof.PlayerInventoryHash && savedCredit.CustomData != null, "loaded saved-credit pair: " + error);
                Check(!BlueprintStoragePlayerProfile.TryReadSavedCredit(loaded, proof.PlayerInventory, () => false, out _, out _), "saved credit changed context accepted");
                Check(BlueprintStoragePlayerProfile.TryVerifyLoaded(loaded, proof, () => true, out var evidence, out var reason) &&
                    evidence.InventoryHash == proof.PlayerInventoryHash, reason);
                Check(BlueprintStorageTransfer.InspectRecovery(frame.Transfer.JournalPath, loaded, frame.Placed, out reason) ==
                    BlueprintStorageTransfer.RecoveryState.ExactDelivered, reason);
                Check(!BlueprintStoragePlayerProfile.TryVerifyLoaded(loaded, proof, () => false, out _, out _), "changed context accepted");
                var item = loaded.Inventory.Items[0];
                foreach (string fault in new[] { "durability", "quantity", "quality", "variant", "world", "picked-up", "equipped", "cheated", "crafter", "name", "custom", "grid", "duplicate", "capacity" })
                {
                    var before = loaded.Inventory.Items.Select(value => value.Clone()).ToArray(); int width = loaded.Inventory.Width;
                    switch (fault)
                    {
                        case "durability": item.m_durability -= 0.0001f; break;
                        case "quantity": item.m_stack++; break;
                        case "quality": item.m_quality++; break;
                        case "variant": item.m_variant++; break;
                        case "world": item.m_worldLevel++; break;
                        case "picked-up": item.m_pickedUp = !item.m_pickedUp; break;
                        case "equipped": item.m_equipped = !item.m_equipped; break;
                        case "cheated": item.m_cheated = !item.m_cheated; break;
                        case "crafter": item.m_crafterID++; break;
                        case "name": item.m_crafterName = "different"; break;
                        case "custom": item.Custom = "different"; break;
                        case "grid": item.m_gridPos.x++; break;
                        case "duplicate": loaded.Inventory.Items.Add(item.Clone()); break;
                        case "capacity": loaded.Inventory.Width++; break;
                    }
                    Check(!BlueprintStoragePlayerProfile.TryVerifyLoaded(loaded, proof, () => true, out _, out _), "accepted " + fault);
                    Check(!BlueprintStoragePlayerProfile.TryReadSavedCredit(loaded, proof.PlayerInventory, () => true, out _, out _), "saved credit accepted " + fault);
                    loaded.Inventory.Items.Clear(); loaded.Inventory.Items.AddRange(before); item = before[0]; loaded.Inventory.Width = width;
                }
                File.WriteAllBytes(profilePath, ProfileChecks.FileFor(loaded));
                Check(!File.ReadAllBytes(profilePath).SequenceEqual(profileBytes), "durability boundary fixture did not reproduce native truncation");
                Check(!BlueprintStoragePlayerProfile.TryVerifyLoaded(loaded, proof, () => true, out _, out _), "additional Save generation accepted");
                Check(!BlueprintStoragePlayerProfile.TryReadSavedCredit(loaded, proof.PlayerInventory, () => true, out _, out _), "saved credit accepted reserialized disk pair");
                File.WriteAllBytes(profilePath, profileBytes);
                Check(BlueprintStoragePlayerProfile.TryVerifyLoaded(loaded, proof, () => true, out _, out _), "restored exact fixture refused");
                Check(BlueprintStoragePlayerProfile.TryReadSavedCredit(loaded, proof.PlayerInventory, () => true, out _, out _), "saved credit restored exact fixture refused");
                Check(File.ReadAllBytes(profilePath).SequenceEqual(profileBytes) && loaded.Inventory.MoveCalls == 0,
                    "verification wrote profile or moved supplied items");
            }
            finally { File.WriteAllBytes(profilePath, profileBytes); Player.m_localPlayer = original; }
            Console.WriteLine("PASS " + assertions + " exact first-load inventory assertions; actual comparator/parser, inert Player/profile; no native reload or settlement proof.");
        }

        private static void Check9412(string profilePath)
        {
            var player = new Player(); var hammer = Program.Item("Hammer", 1); hammer.m_durability = 94.12f;
            player.Inventory.Items.Add(hammer); Player.m_localPlayer = player;
            Check(BlueprintStorageCapture.TrySnapshotInventory(player.Inventory, out var expected), "9412 snapshot missing");
            Check(BlueprintContainerInventory.TryRead(expected, name => name.GetStableHashCode(), out var rows) &&
                rows.Single().DurabilityHundredths == 9412, "9412 saved fixture missing");
            File.WriteAllBytes(profilePath, ProfileChecks.FileFor(player));
            hammer.m_durability = (float)rows.Single().DurabilityHundredths.Value * 0.01f;
            Check(BlueprintStorageCapture.TrySnapshotInventory(player.Inventory, out var actual), "9412 projected snapshot missing");
            Check(BlueprintContainerInventory.TryRead(actual, name => name.GetStableHashCode(), out var loaded), "9412 projected rows missing");
            Check(loaded.Single().DurabilityHundredths == 9411, "9412 native projection did not truncate to 9411: live=" +
                hammer.m_durability.ToString("R") + "; saved=" + loaded.Single().DurabilityHundredths);
            Check(BlueprintStoragePlayerProfile.TryReadSavedCredit(player, expected, () => true, out _, out var error), error);
            hammer.m_durability -= 0.0001f;
            Check(!BlueprintStoragePlayerProfile.TryReadSavedCredit(player, expected, () => true, out _, out _), "9412 fixture accepted extra wear");
            hammer.m_durability = (float)rows.Single().DurabilityHundredths.Value * 0.01f;
            File.WriteAllBytes(profilePath, ProfileChecks.FileFor(player));
            Check(!BlueprintStoragePlayerProfile.TryReadSavedCredit(player, expected, () => true, out _, out _), "9412 fixture accepted 9411 disk replacement");
        }
    }
}
