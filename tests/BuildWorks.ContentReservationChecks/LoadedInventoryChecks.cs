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
                Check(!BlueprintStoragePlayerProfile.TryVerifyLoaded(frame.Player, proof, () => true, out _, out _), "same Player used as a reload");
                var loaded = new Player();
                loaded.Inventory.Width = frame.Player.Inventory.Width; loaded.Inventory.Height = frame.Player.Inventory.Height;
                loaded.Inventory.Items.AddRange(frame.Player.Inventory.Items.Select(item => item.Clone()));
                Check(BlueprintContainerInventory.TryRead(proof.PlayerInventory, name => name.GetStableHashCode(), out var rows), "paired rows invalid");
                foreach (var row in rows) loaded.Inventory.GetItemAt(row.X, row.Y).m_durability = (float)row.DurabilityHundredths.Value * 0.01f;
                Player.m_localPlayer = loaded;
                Check(BlueprintStorageLoadedInventory.Matches(loaded.Inventory, proof.PlayerInventory), "exact native Load projection rejected");
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
                    loaded.Inventory.Items.Clear(); loaded.Inventory.Items.AddRange(before); item = before[0]; loaded.Inventory.Width = width;
                }
                File.WriteAllBytes(profilePath, ProfileChecks.FileFor(loaded));
                Check(!File.ReadAllBytes(profilePath).SequenceEqual(profileBytes), "durability boundary fixture did not reproduce native truncation");
                Check(!BlueprintStoragePlayerProfile.TryVerifyLoaded(loaded, proof, () => true, out _, out _), "additional Save generation accepted");
                File.WriteAllBytes(profilePath, profileBytes);
                Check(BlueprintStoragePlayerProfile.TryVerifyLoaded(loaded, proof, () => true, out _, out _), "restored exact fixture refused");
                Check(File.ReadAllBytes(profilePath).SequenceEqual(profileBytes) && loaded.Inventory.MoveCalls == 0,
                    "verification wrote profile or moved supplied items");
            }
            finally { File.WriteAllBytes(profilePath, profileBytes); Player.m_localPlayer = original; }
            Console.WriteLine("PASS " + assertions + " exact first-load inventory assertions; actual comparator/parser, inert Player/profile; no native reload or settlement proof.");
        }
    }
}
