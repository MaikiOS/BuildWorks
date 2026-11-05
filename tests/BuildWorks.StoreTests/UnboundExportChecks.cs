using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class UnboundExportChecks
    {
        private static void Check(bool value, string detail) { if (!value) throw new InvalidOperationException(detail); }
        private static UnboundImportContext Profile(string name, bool sync = true, bool distant = false, string host = "synthetic-host", byte objectType = 0) =>
            new UnboundImportContext(new BlueprintPrefabMetadata(1, name, name, "Piece", host,
                name == "wood_floor" ? new Point3(2, 3, 4) : new Point3(1, 1, 1)), sync,
                name != "piece_chest_wood", name == "sign", name == "sign", name == "piece_chest_wood" ? (5, 2) : ((int, int)?)null,
                (ForeignZdoChecks.Hash(name), true, distant, objectType));
        private static UnboundImportContext Profile(string name) => Profile(name, true);
        private static CompositeBlueprintStore.Blueprint Prepared(string text) => ExternalBlueprintLibrary.PrepareForHost(
            ExternalBlueprintLibrary.Parse(text, "input.vtbp"), _ => null, ForeignZdoChecks.Hash,
            resolvePrefabHash: id => id == ForeignZdoChecks.Hash("Wood") ? "Wood" : null, unboundContext: Profile);
        private static string Packet(string prefab, ushort flags, Action<BinaryWriter> body)
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                { writer.Write(flags); writer.Write(ForeignZdoChecks.Hash(prefab)); body(writer); }
                return Convert.ToBase64String(stream.ToArray());
            }
        }
        private static string Fixture(bool terrain = false, bool unknown = false)
        {
            var floor = Packet("wood_floor", 256, _ => { });
            var sign = Packet("sign", 336, w => { w.Write((byte)(unknown ? 2 : 1)); w.Write(ForeignZdoChecks.Hash("RandMatSeed"));
                w.Write(17); if (unknown) { w.Write(777); w.Write(1); } w.Write((byte)1); w.Write(ForeignZdoChecks.Hash("text")); w.Write("source text"); });
            byte[] items;
            using (var stream = new MemoryStream())
            {
                using (var w = new BinaryWriter(stream, Encoding.UTF8, true))
                { w.Write(110); w.Write((ushort)1); w.Write(0); w.Write((byte)0); w.Write((byte)0); w.Write((byte)0);
                    w.Write((byte)72); w.Write((ushort)3); w.Write(ForeignZdoChecks.Hash("Wood")); w.Write((byte)0); }
                items = stream.ToArray();
            }
            var chest = Packet("piece_chest_wood", 384, w => { w.Write((byte)1); w.Write(ForeignZdoChecks.Hash("items")); w.Write(items.Length); w.Write(items); });
            return "\uFEFFvtbp 2\r\n" + (terrain ? "#terrain|1|0.125\r\n" : "") +
                "wood_floor|-0|0|0|0|0|0|1|" + floor + "\r\nsign|1|0|0|0|0|0|1|" + sign +
                "\npiece_chest_wood|2|0|0|0|0|0|1|" + chest;
        }
        private static UnboundBlueprint Write(CompositeBlueprintStore.Blueprint source, string directory, string name)
        {
            var review = UnboundExport.Review(source, Profile);
            Check(review.Blockers.Count == 0, "Unbound blockers: " + string.Join(",", review.Blockers));
            Check(!Directory.Exists(Path.Combine(directory, name)), "Review published before confirmation");
            Check(review.TryWrite(source, Profile, directory, name, out string path, out string error), error);
            var native = BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(path, name + ".buildworks.json")));
            Check(native.sourceDocuments.Select(s => s.text).SequenceEqual(source.sourceDocuments.Select(s => s.text)), "Companion lost exact source bytes/text");
            var reimported = ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.LoadFile(Path.Combine(path, name + ".vtbp")).Single(),
                _ => null, ForeignZdoChecks.Hash, resolvePrefabHash: id => id == ForeignZdoChecks.Hash("Wood") ? "Wood" : null, unboundContext: Profile);
            Check(reimported.importError == null && reimported.parts.Count == source.parts.Count &&
                reimported.parts.All(part => part.foreignData?.format != UnboundBlueprint.Format), "Physical output did not reimport into known working state");
            return UnboundBlueprint.Parse(File.ReadAllText(Path.Combine(path, name + ".vtbp")));
        }
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var source = Prepared(Fixture());
            Check(source.importError == null && source.parts[2].foreignData?.format == BlueprintForeignPartData.NativeContainerFormat, "Invalid prepared fixture");
            var culture = CultureInfo.CurrentCulture;
            try
            {
                foreach (string name in new[] { "en-US", "ru-RU" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                    var edited = source.ImportCopy(); edited.parts[0].position.y = 1.25f; edited.parts[0].scale.x = 1.5f;
                    edited.parts[1].signText = "edited Привет"; edited.parts[1].materialSeed = 18;
                    edited.parts[1].rotation = new CompositeBlueprintStore.QuaternionData { y = .6f, w = .8f };
                    var output = Write(edited, directory, name);
                    Check(output.Pieces[0].Position.Y == 1.25 && output.Pieces[1].Rotation.Y == (double).6f &&
                        output.Pieces[1].State.TryGet<string>(ForeignZdoChecks.Hash("text"), out var text) && text == "edited Привет" &&
                        output.Pieces[1].State.TryGet<int>(ForeignZdoChecks.Hash("RandMatSeed"), out var seed) && seed == 18 &&
                        output.Pieces[0].State.TryGet<Point3>(ForeignZdoChecks.Hash("scale"), out var scale) && scale.X == 3 && scale.Y == 3 && scale.Z == 4,
                        "Output does not match independently expected working properties");
                    Check(output.Pieces[2].State.TryGet<byte[]>(ForeignZdoChecks.Hash("items"), out var inventory) &&
                        Convert.ToBase64String(inventory) == edited.parts[2].foreignData.zdo, "Retained inventory bytes changed");
                }
            }
            finally { CultureInfo.CurrentCulture = culture; }
            var cleared = source.ImportCopy();
            cleared.parts[2].foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(cleared.parts[2].foreignData.ToGeometry().WithoutContents(ForeignZdoChecks.Hash));
            string oldItems = source.parts[2].foreignData.zdo;
            var empty = Write(cleared, directory, "cleared");
            Check(empty.Pieces[2].State.TryGet<byte[]>(ForeignZdoChecks.Hash("items"), out var clearedItems) &&
                Convert.ToBase64String(clearedItems) == "bgAAAAAA" && cleared.parts[2].foreignData.zdo == oldItems &&
                source.parts[2].foreignData.zdo == oldItems, "Cleared output resurrected source items or mutated archive");
            var fresh = new CompositeBlueprintStore.Blueprint { id = Guid.NewGuid().ToString("N"), name = "Fresh", prefabName = "wood_floor", sourceDocuments = new System.Collections.Generic.List<CompositeBlueprintStore.SourceDocument>() };
            fresh.parts.Add(new CompositeBlueprintStore.Part { stableId = Guid.NewGuid().ToString("N"), prefabName = "wood_floor", displayName = "Floor", rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } });
            Check(CompositeBlueprintStore.Valid(fresh), "Source-free test fixture must be a valid native card");
            Check(Write(fresh, directory, "fresh").Pieces.Single().State.Persistent == true, "Fresh header not used");
            Check(UnboundExport.Review(fresh, Profile).HeaderChanges == 0, "Source-free parts report a provenance change");
            for (int flags = 0; flags < 4096; flags += 256)
            {
                var archived = Prepared("vtbp 2\nwood_floor|0|0|0|0|0|0|1|" + Packet("wood_floor", (ushort)flags, _ => { }));
                var review = UnboundExport.Review(archived, Profile);
                Check(review.HeaderChanges == (flags == 256 ? 0 : 1), "Changed source header count is wrong");
                var state = Write(archived, directory, "header-" + flags).Pieces.Single().State;
                Check(state.Persistent == true && state.Distant == false && state.NativeObjectType == 0,
                    "Paired export did not use the current native header");
            }
            Check(UnboundExport.Review(fresh, _ => null).Blockers.Any(b => b.Reason == UnboundExport.BlockReason.HostProfile), "Missing fresh facts accepted");
            Check(UnboundExport.Review(Prepared(Fixture(unknown: true)), Profile).Blockers.Any(b => b.Reason == UnboundExport.BlockReason.StateAdapter), "Unknown state omitted");
            var holder = fresh.ImportCopy(); holder.parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData { format = BlueprintForeignPartData.NativeItemStandFormat, info = "AxeIron:0:1:0" };
            Check(UnboundExport.Review(holder, Profile).Blockers.Any(b => b.Reason == UnboundExport.BlockReason.StateAdapter), "Holder silently flattened");
            var terrain = Prepared(Fixture(terrain: true));
            Check(Write(terrain, directory, "terrain").Terrain.Single() == .125f, "Unchanged terrain lost");
            terrain.parts[0].position.x += 1;
            Check(UnboundExport.Review(terrain, Profile).Blockers.Any(b => b.Reason == UnboundExport.BlockReason.TerrainFrame), "Moved terrain frame accepted");
            terrain = Prepared(Fixture(terrain: true)); terrain.parts.RemoveAt(0);
            Check(UnboundExport.Review(terrain, Profile).Blockers.Any(b => b.Reason == UnboundExport.BlockReason.TerrainFrame), "Deleted terrain-bound part accepted");
            var stale = UnboundExport.Review(source, Profile); var moved = source.ImportCopy(); moved.parts[0].position.z += 1;
            Check(!stale.TryWrite(moved, Profile, directory, "stale", out _, out string error) && error == "$buildworks_exchange.plan_changed", "Stale source published");
            var staleFacts = UnboundExport.Review(fresh, Profile);
            Check(!staleFacts.TryWrite(fresh, name => Profile(name, false), directory, "stale-facts", out _, out error) && error == "$buildworks_exchange.plan_changed",
                "Output-neutral capability drift accepted");
            var staleHeader = UnboundExport.Review(fresh, Profile);
            Check(!staleHeader.TryWrite(fresh, name => Profile(name, objectType: 2), directory, "stale-header", out _, out error) &&
                error == "$buildworks_exchange.plan_changed" && !Directory.Exists(Path.Combine(directory, "stale-header")),
                "Changed trusted native header was accepted after review");
            var cancelled = UnboundExport.Review(source, Profile); cancelled.CancelWrite(); bool stopped = false;
            try { cancelled.TryWrite(source, Profile, directory, "cancel", out _, out _); } catch (OperationCanceledException) { stopped = true; }
            Check(stopped && !Directory.Exists(Path.Combine(directory, "cancel")) && !Directory.EnumerateDirectories(directory, "*.tmp").Any(), "Cancelled pair left artifacts");
            byte[] bytes = Encoding.UTF8.GetBytes("test");
            // Exercise cancellation at the actual closed first-file boundary.
            BlueprintExportPair boundary = null; boundary = new BlueprintExportPair(bytes, bytes, ".vtbp", _ => boundary.CancelWrite()); stopped = false;
            try { boundary.TryWrite(() => (bytes, bytes), directory, "boundary", out _, out _); } catch (OperationCanceledException) { stopped = true; }
            Check(stopped && !Directory.Exists(Path.Combine(directory, "boundary")) && !Directory.EnumerateDirectories(directory, "*.tmp").Any(), "Partial foreign/native pair left behind");
            int hostThread = Thread.CurrentThread.ManagedThreadId, calls = 0;
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            store.ConfigureImportScale(_ => null, unboundContext: name => { Check(Thread.CurrentThread.ManagedThreadId == hostThread, "Host callback on worker"); ++calls; return Profile(name); });
            var storedReview = store.ReviewUnboundExport(source).GetAwaiter().GetResult();
            string storedPath = store.ExportUnbound(storedReview, source, directory, "store").GetAwaiter().GetResult();
            Check(calls == 6 && File.Exists(Path.Combine(storedPath, "store.vtbp")) && File.Exists(Path.Combine(storedPath, "store.buildworks.json")), "Store snapshots/pair incomplete");
            Console.WriteLine("PASS Unbound paired export: expected working pose/scale/text/seed, exact retained+cleared inventory, raw companion, fresh header, terrain frame refusal, stale facts/source and cancellation cleanup, host-only Store snapshots; synthetic only, no UI/producer/native consumer proof.");
        }
    }
}
