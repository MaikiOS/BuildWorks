using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ReservationChecks
{
    internal static class RecoveryChecks
    {
        private static int assertions;
        private static void Check(bool value, string reason)
        { assertions++; if (!value) throw new InvalidOperationException("Recovery: " + reason); }

        // New native references, remapped UID and reset revision model the inspected load contract.
        // This is an inert host fixture, not a Valheim world/profile reload.
        private sealed class Loaded
        {
            internal readonly TransferChecks.Frame Original;
            internal readonly Player Player = new Player();
            internal readonly GameObject Placed = new GameObject();
            internal readonly string Path;
            internal Loaded(bool partial = false, bool fullGrid = false, string evidence = "delivered")
            {
                Original = new TransferChecks.Frame(second: partial);
                if (fullGrid)
                {
                    Original.Player.Inventory.Width = 1; Original.Player.Inventory.Height = 1; Original.Item.m_gridPos = new Vector2i(0, 0);
                    Original.Supply = new BlueprintItemStandPlacement.Supply(new System.Collections.Generic.List<BlueprintItemStandPlacement.Reservation> {
                        new BlueprintItemStandPlacement.Reservation(Original.Item, 2, BlueprintItemStandPlacement.SnapshotItem(Original.Item)) });
                    Original.Held[0] = Original.Supply;
                }
                Check(Original.Begin(), Original.Error);
                if (evidence == "pending") Original.Placed.Container.Inventory.AfterCredit = () => Original.Journal.Dispose();
                if (evidence == "uncertain") Original.Placed.Container.Inventory.AfterCredit = () => throw new InvalidOperationException("native callback");
                if (evidence == "unchanged") Original.Placed.Container.Inventory.NoChange = true;
                if (evidence != "prepared") Original.Transfer.MoveNext();
                Path = Original.Transfer.JournalPath; Original.Transfer.Dispose();
                Player.Inventory.Width = Original.Player.Inventory.Width; Player.Inventory.Height = Original.Player.Inventory.Height;
                Player.Inventory.Items.AddRange(Original.Player.Inventory.Items.Select(value => value.Clone()));
                Placed.Piece = new Piece { gameObject = Placed }; Placed.Container = new Container(3);
                Placed.Container.Inventory.Items.AddRange(Original.Placed.Container.Inventory.Items.Select(value => value.Clone()));
                Placed.Container.View.Data.m_uid = new ZDOID { User = 1, Id = 77 };
                Placed.Container.LoadForTest(Original.Operation.ToString("N"));
                Game.instance = new Game(); ZNet.instance = new ZNet(); ZDOMan.instance = new ZDOMan();
                ReservationChecks.Player.m_localPlayer = Player;
            }
        }
        private static void Refused(Loaded fixture, string reason)
        {
            int files = Directory.GetFiles(BlueprintStorageTransferJournal.Root).Length;
            Check(BlueprintStorageTransfer.InspectRecovery(fixture.Path, fixture.Player, fixture.Placed, out var detail) == BlueprintStorageTransfer.RecoveryState.NeedsReconciliation &&
                !string.IsNullOrEmpty(detail), "classifier accepted " + reason);
            if (reason == "source-quantity" || reason == "source-properties")
                Check(detail.EndsWith("Player inventory manifest changed.", StringComparison.Ordinal),
                    "recovery did not identify the changed player manifest");
            Check(!BlueprintStorageTransfer.TryBeginRecoveredReturn(fixture.Path, fixture.Player, fixture.Placed, out _, out _) &&
                fixture.Player.Inventory.MoveCalls == 0 && Directory.GetFiles(BlueprintStorageTransferJournal.Root).Length == files,
                "denial moved items or created child: " + reason);
        }
        internal static void Run()
        {
            foreach (bool partial in new[] { false, true })
            {
                var loaded = new Loaded(partial: partial); byte[] raw = (byte[])loaded.Placed.Container.View.Data.Raw.Clone();
                Check(BlueprintStorageTransfer.InspectRecovery(loaded.Path, loaded.Player, loaded.Placed, out var reason) == BlueprintStorageTransfer.RecoveryState.ExactDelivered,
                    reason ?? "fresh native references not rebound");
                Check(loaded.Placed.Container.View.Data.DataRevision == 0 && loaded.Placed.Container.View.Data.Raw.SequenceEqual(raw) &&
                    loaded.Player.Inventory.MoveCalls == 0 && Directory.GetFiles(BlueprintStorageTransferJournal.Root).Length == 1, "classifier mutated state");
                Check(BlueprintStorageTransfer.TryBeginRecoveredReturn(loaded.Path, loaded.Player, loaded.Placed, out var inverse, out reason), reason);
                using (inverse)
                {
                    var record = BlueprintStorageTransferJournal.Read(inverse.JournalPath);
                    Check(record.Schema == 5 && record.Prepared.TargetUser == 1 && record.Prepared.TargetId == 77 &&
                        record.Prepared.TargetMarker == loaded.Original.Operation && record.Prepared.Parent == loaded.Original.Operation,
                        "recovery used the old native UID instead of marker/current UID");
                    Check(inverse.MoveNext() == BlueprintStorageTransfer.Result.Transferred && inverse.CompleteInMemory, "recovered return failed");
                    var returned = inverse.Entries[0].DestinationItem;
                    Check(returned.m_stack == 2 && returned.Custom == "provided" && returned.m_quality == 2 && returned.m_durability == 7.53f &&
                        !ReferenceEquals(returned, loaded.Original.Transfer.Entries[0].DestinationItem) && loaded.Placed.Container.Inventory.Items.Count == 0 &&
                        loaded.Player.Inventory.Items.Sum(value => value.m_stack) == (partial ? 4 : 3), "recovery recreated archive attrs/lost custody");
                    Check(!BlueprintStorageTransfer.TryBeginRecoveredReturn(loaded.Path, loaded.Player, loaded.Placed, out _, out _) && loaded.Player.Inventory.MoveCalls == 1,
                        "recovered child replayed");
                    Check(BlueprintStorageTransferJournal.HasUnresolved(1, loaded.Original.Operation.ToString("N")) &&
                        !BlueprintStorageCapture.CanRemoveEmpty(loaded.Placed, loaded.Player), "recovery released hold");
                }
            }
            foreach (string fault in new[] { "marker-missing", "marker-wrong", "source-quantity", "source-properties", "target-quantity", "target-properties",
                "target-cell", "extra", "raw", "cache", "owner", "access", "busy", "loading", "world", "prefab-name", "quest", "max-stack", "queue", "capture-callback" })
            {
                var loaded = new Loaded(); var container = loaded.Placed.Container; var item = container.Inventory.Items[0];
                switch (fault)
                {
                    case "marker-missing": container.View.Data.Strings.Clear(); break;
                    case "marker-wrong": container.View.Data.Strings[BlueprintStorageCapture.TransferMarkerKey] = Guid.NewGuid().ToString("N"); break;
                    case "source-quantity": loaded.Player.Inventory.Items[0].m_stack = 3; break; // both copies persisted after a crash
                    case "source-properties": loaded.Player.Inventory.Items[0].Custom = "different"; break;
                    case "target-quantity": item.m_stack++; break;
                    case "target-properties": item.m_durability--; break;
                    case "target-cell": item.m_gridPos.x++; break;
                    case "extra": var extra = Program.Item("Stone", 1); extra.m_gridPos = new Vector2i(2, 0); container.Inventory.Items.Add(extra); break;
                    case "raw": container.View.Data.Raw = null; break;
                    case "cache": container.MakeRevisionStale(); break;
                    case "owner": container.View.Owner = false; break;
                    case "access": container.Allowed = false; break;
                    case "busy": container.InUse = true; break;
                    case "loading": container.SetLoading(true); break;
                    case "world": ZNet.instance.World.m_uid++; break;
                    case "prefab-name": loaded.Placed.name = "another"; break;
                    case "quest": item.m_shared.m_questItem = true; break;
                    case "max-stack": item.m_shared.m_maxStackSize = 1; break;
                    case "queue": loaded.Player.Queue.Add(item); break;
                    case "capture-callback": BlueprintStorageCapture.Captured = () => loaded.Player.Inventory.Items[0].Custom = "changed in capture"; break;
                }
                try { Refused(loaded, fault); } finally { BlueprintStorageCapture.Captured = null; }
            }
            Refused(new Loaded(fullGrid: true), "full player inventory");
            foreach (string evidence in new[] { "prepared", "pending", "uncertain", "unchanged" }) Refused(new Loaded(evidence: evidence), evidence);
            foreach (int schema in new[] { 1, 2 })
            {
                var legacy = new Loaded(); ReturnChecks.WriteLegacy(legacy.Path, schema);
                Check(BlueprintStorageTransferJournal.Read(legacy.Path).Schema == schema &&
                    BlueprintStorageTransferJournal.HasUnresolved(1, "unrelated-marker"), "legacy UID relocation released hold");
                Refused(legacy, "legacy v" + schema);
            }
            var conflict = new Loaded(); File.WriteAllText(System.IO.Path.Combine(BlueprintStorageTransferJournal.Root,
                BlueprintStorageTransferJournal.ReturnOperationId(conflict.Original.Operation).ToString("N") + ".bwtransfer"), "partial child");
            Refused(conflict, "conflicting/corrupt child");
            var outside = new Loaded(); string outsideDirectory = System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "external");
            Directory.CreateDirectory(outsideDirectory); string outsidePath = System.IO.Path.Combine(outsideDirectory, System.IO.Path.GetFileName(outside.Path));
            File.Copy(outside.Path, outsidePath);
            Check(BlueprintStorageTransfer.InspectRecovery(outsidePath, outside.Player, outside.Placed, out _) == BlueprintStorageTransfer.RecoveryState.NeedsReconciliation &&
                !BlueprintStorageTransfer.TryBeginRecoveredReturn(outsidePath, outside.Player, outside.Placed, out _, out _) && outside.Player.Inventory.MoveCalls == 0,
                "foreign directory journal claimed this installation's child");
            MarkerChecks();
            Console.WriteLine("PASS " + assertions + " recovery/marker assertions: remapped native UID/reset revision and fresh references on inert host; no real reload/disk/server proof.");
        }

        private static void MarkerChecks()
        {
            var waiting = new TransferChecks.Frame();
            waiting.Placed.Container.View.Data.BeforeSet = () => Check(BlueprintStorageTransferJournal.Read(System.IO.Path.Combine(BlueprintStorageTransferJournal.Root,
                waiting.Operation.ToString("N") + ".bwtransfer")).Schema == 5, "marker preceded flushed preparation");
            Check(waiting.Begin(synchronizeMarker: false), waiting.Error);
            Check(waiting.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Ready && waiting.Placed.Container.Inventory.MoveCalls == 0 &&
                !waiting.Transfer.Entries[0].Attempted && !BlueprintStorageTransferJournal.Read(waiting.Transfer.JournalPath).InFlight.HasValue, "marker wait attempted native move");
            waiting.Placed.Container.SyncCacheForTest();
            Check(waiting.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Ready && waiting.Placed.Container.Inventory.MoveCalls == 0 &&
                waiting.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred && waiting.Placed.Container.Inventory.MoveCalls == 1, "synced marker never progressed");
            var timeout = new TransferChecks.Frame(); Check(timeout.Begin(synchronizeMarker: false), timeout.Error);
            typeof(BlueprintStorageTransfer).GetField("markerStarted", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(timeout.Transfer,
                System.Diagnostics.Stopwatch.GetTimestamp() - 31 * System.Diagnostics.Stopwatch.Frequency);
            Check(timeout.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Uncertain && timeout.Transfer.JournalError != null &&
                timeout.Placed.Container.Inventory.MoveCalls == 0, "unbounded marker sync wait");
            foreach (bool after in new[] { false, true })
            {
                var broken = new TransferChecks.Frame(); Action failure = () => throw new InvalidOperationException("marker write failure");
                if (after) broken.Placed.Container.View.Data.AfterSet = failure; else broken.Placed.Container.View.Data.BeforeSet = failure;
                Check(!broken.Begin() && broken.Item.m_stack == 3 && broken.Placed.Container.Inventory.MoveCalls == 0 &&
                    BlueprintStorageTransferJournal.HasUnresolved(1) && !BlueprintStorageCapture.CanRemoveEmpty(broken.Placed, broken.Player),
                    "marker failure lost preparation/custody hold");
            }
        }
    }
}
