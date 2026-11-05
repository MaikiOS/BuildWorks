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

        internal static void RunMountedItems()
        {
            int before = assertions;
            var supplied = Program.Item("ShieldBanded", 1, "actual 🪵"); supplied.m_quality = 2; supplied.m_variant = 1;
            supplied.m_gridPos = new Vector2i(5,4); supplied.m_worldLevel = 3; supplied.m_pickedUp = true;
            supplied.m_crafterID = 888; supplied.m_crafterName = "Острикс"; supplied.m_durability = 94.12f;
            foreach(int version in new[]{109,110})
            {
                supplied.m_cheated = version == 110;
                byte[] packet = Convert.FromBase64String(BlueprintItemStandPlacement.SnapshotItem(supplied)); packet[0]=(byte)version;
                var source = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,"ShieldBanded:1:2:0",Convert.ToBase64String(packet),null);
                Check(BlueprintItemStandContents.TryRead(source,name=>name.GetStableHashCode(),out var row),"mounted fixture invalid");
                var loaded = supplied.Clone(); loaded.m_durability=(float)row.DurabilityHundredths.Value*0.01f;
                Check(BlueprintStorageLoadedInventory.MatchesMountedItem(loaded,source),"actual native loaded item rejected");
                Check(!BlueprintStorageLoadedInventory.MatchesMountedItem(supplied,source),"pre-load float substituted for exact first-load projection");
                foreach(Action<ItemDrop.ItemData> mutate in new Action<ItemDrop.ItemData>[] {
                    i=>i.m_durability-=0.0001f,i=>i.m_stack++,i=>i.m_quality++,i=>i.m_variant++,i=>i.m_worldLevel++,
                    i=>i.m_pickedUp=!i.m_pickedUp,i=>i.m_equipped=!i.m_equipped,i=>i.m_cheated=!i.m_cheated,
                    i=>i.m_crafterID++,i=>i.m_crafterName="other",i=>i.Custom="other",i=>i.m_customData.Add("extra","value"),
                    i=>i.m_gridPos.x++,i=>i.m_gridPos.y++,i=>i.m_dropPrefab=new Prefab{name="SwordIron"},
                    i=>i.m_customData=null,i=>i.m_shared=null,i=>i.m_dropPrefab=null
                }) {
                    var changed=loaded.Clone(); mutate(changed);
                    Check(!BlueprintStorageLoadedInventory.MatchesMountedItem(changed,source),"changed mounted property accepted");
                }
                Check(!BlueprintStorageLoadedInventory.MatchesMountedItem(null,source),"missing live item accepted");
                Check(!BlueprintStorageLoadedInventory.MatchesMountedItem(loaded,new BlueprintForeignPartData(source.Format,source.Info,source.Zdo,null,true)),"cleared item grants a match");
                Check(source.Zdo==Convert.ToBase64String(packet) && supplied.Custom=="actual 🪵" && loaded.Custom=="actual 🪵","matcher mutated packet or supplied item");
            }
            supplied.m_cheated=false; supplied.m_durability=0.05f;
            string nearPacket=BlueprintItemStandPlacement.SnapshotItem(supplied);
            var near=new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,"ShieldBanded:1:2:0",nearPacket,null);
            Check(BlueprintItemStandContents.TryRead(near,name=>name.GetStableHashCode(),out var boundary) && boundary.DurabilityHundredths==5,"near-cent fixture invalid");
            var nearLoaded=supplied.Clone();nearLoaded.m_durability=(float)boundary.DurabilityHundredths.Value*0.01f;
            Check(BlueprintStorageLoadedInventory.MatchesMountedItem(nearLoaded,near) && BlueprintItemStandPlacement.SnapshotItem(nearLoaded)!=nearPacket,
                "native first-load comparison incorrectly requires reserialized byte identity");
            int bits=BitConverter.ToInt32(BitConverter.GetBytes(nearLoaded.m_durability),0);
            nearLoaded.m_durability=BitConverter.ToSingle(BitConverter.GetBytes(bits+1),0);
            Check(!BlueprintStorageLoadedInventory.MatchesMountedItem(nearLoaded,near),"one-bit durability difference was hidden by tolerance");
            Check(!BlueprintStorageLoadedInventory.MatchesMountedItem(supplied,null),"missing packet accepted");
            Check(!BlueprintStorageLoadedInventory.MatchesMountedItem(supplied,new BlueprintForeignPartData(near.Format,near.Info,null,null)),"authoring recipe accepted as actual snapshot");
            Check(!BlueprintStorageLoadedInventory.MatchesMountedItem(supplied,Program.Container(8,supplied)),"inventory package accepted as mounted item");
            Console.WriteLine("PASS mounted native-load matching: "+(assertions-before)+" exact-property assertions; full109/110, near-cent and one-bit controls; no authority, mutation, native Load or return execution");
        }
    }
}
