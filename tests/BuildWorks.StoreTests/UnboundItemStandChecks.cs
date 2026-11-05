using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class UnboundItemStandChecks
    {
        private static void Check(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
        private static UnboundImportContext Context(string name, int variants = 3) => new UnboundImportContext(
            new BlueprintPrefabMetadata(1, name, name, "Piece", "substituted-holder-host", new Point3(1, 1, 1)), true, false, false, false,
            nativeHeader: (ForeignZdoChecks.Hash(name), true, false, (byte)2),
            itemStandItems: new[] { new UnboundItemStandItem("SwordIron", variants, 4, new[] { 0, 1 }) });
        private static byte[] Item()
        {
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write((byte)110); writer.Write(8123); writer.Write((byte)2); writer.Write((byte)3); writer.Write((byte)1);
                    writer.Write((byte)245); writer.Write((ushort)3); writer.Write(2); writer.Write((long)888); writer.Write("Synthetic crafter");
                    writer.Write(ForeignZdoChecks.Hash("SwordIron")); writer.Write((byte)1); writer.Write("mod:key"); writer.Write("full\0data"); writer.Write((byte)1);
                }
                return stream.ToArray();
            }
        }
        internal static void Run(string directory)
        {
            Check(!Directory.Exists(directory) && !File.Exists(directory), "Holder checks require a new output directory");
            Directory.CreateDirectory(directory);
            var item = Item();
            var fields = new Dictionary<string, object> { ["item"] = ForeignZdoChecks.Hash("SwordIron"), ["variant"] = 2,
                ["quality"] = 3, ["type"] = 1, ["itemData"] = item, ["creator"] = (long)111 };
            string packet = BlueprintForeignZdo.CreateNative(ForeignZdoChecks.Hash("itemstand"), true, false, 2).WriteNative(
                fields.Select(field => new KeyValuePair<int, object>(ForeignZdoChecks.Hash(field.Key), field.Value)));
            string text = "\uFEFFvtbp 2\r\nitemstand|0|0|0|0|0|0|1|" + packet + "\r\n";
            string input = Path.Combine(directory, "holder.vtbp"); File.WriteAllText(input, text, new UTF8Encoding(false));
            byte[] original = File.ReadAllBytes(input);
            int hostThread = Thread.CurrentThread.ManagedThreadId, resolves = 0, profiles = 0;
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"), backgroundImports: true);
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, prefabHash: ForeignZdoChecks.Hash,
                resolvePrefabHash: hash => { Check(Thread.CurrentThread.ManagedThreadId == hostThread, "Item resolver ran on worker"); ++resolves;
                    return hash == ForeignZdoChecks.Hash("SwordIron") ? "SwordIron" : null; },
                unboundContext: name => { Check(Thread.CurrentThread.ManagedThreadId == hostThread, "Holder profile ran on worker"); ++profiles; return Context(name); });
            void Ready() => Check(SpinWait.SpinUntil(() => { store.CatalogAll(); return !store.CatalogLoading; }, 10000), "Holder catalog timeout");
            Ready();
            var review = store.PrepareImportReview(CompositeBlueprintStore.ReviewImportFile(input, ForeignZdoChecks.Hash), ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Check(review.Documents.Single().parts[0].foreignData?.format == BlueprintForeignPartData.NativeItemStandFormat &&
                review.Contents.Mounts == 1 && review.Contents.Unknown == 0 && resolves > 0 && profiles > 0, "Holder snapshot/import review did not resolve current item");
            Check(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error), error); Ready();
            Check(store.ImportError == null, "Holder import failed: " + store.ImportError);
            var source = store.CatalogAll().Single();
            Check(source.parts[0].foreignData.zdo == Convert.ToBase64String(item), "Holder import changed archived item properties");
            source.parts[0].foreignData.info = "SwordIron:2:3:0";
            var prepared = ExternalBlueprintLibrary.PrepareForHost(source, _ => null, ForeignZdoChecks.Hash,
                resolvePrefabHash: hash => hash == ForeignZdoChecks.Hash("SwordIron") ? "SwordIron" : null, unboundContext: name => Context(name));
            Check(prepared.importError == null && prepared.parts[0].foreignData.info == "SwordIron:2:3:0", "Revalidation rejected a source-bound orientation edit");
            UnboundSourceChecks.Editor(prepared);
            Check(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out error), error);
            var reopened = new CompositeBlueprintStore(Path.Combine(directory, "library.json")).All().Single();
            Check(reopened.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original), "Native holder source changed");
            CorpusRoundTripChecks.RunSaved(Path.Combine(directory, "library.json"), saved.id);
            byte[] portable = BlueprintExchangeFormat.Encode(saved);
            Check(BlueprintExchangeFormat.Encode(BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(portable))).SequenceEqual(portable), "Holder portable fields changed");
            var export = UnboundExport.Review(prepared, name => Context(name));
            Check(export.Blockers.Count == 0 && export.Mounts == 1 && export.ClearedMounts == 0, "Holder export blocked or counts missing");
            Check(export.TryWrite(prepared, name => Context(name), directory, "pair", out string pair, out error), error);
            var foreign = ExternalBlueprintLibrary.LoadFile(Path.Combine(pair, "pair.vtbp")).Single();
            var again = ExternalBlueprintLibrary.PrepareForHost(foreign, _ => null, ForeignZdoChecks.Hash,
                resolvePrefabHash: hash => hash == ForeignZdoChecks.Hash("SwordIron") ? "SwordIron" : null, unboundContext: name => Context(name));
            Check(again.importError == null && again.parts[0].foreignData.info == "SwordIron:2:3:0" &&
                again.parts[0].foreignData.zdo == Convert.ToBase64String(item), "Holder foreign reimport lost exact working state");
            var companion = BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(pair, "pair.buildworks.json")));
            Check(companion.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original) &&
                !foreign.sourceDocuments.Single().ToGeometry().Unbound.Pieces[0].State.HasHash(ForeignZdoChecks.Hash("creator")), "Holder raw companion or authority sanitization failed");
            Check(!export.TryWrite(prepared, name => Context(name, 4), directory, "stale", out _, out error) &&
                error == "$buildworks_exchange.plan_changed" && !Directory.Exists(Path.Combine(directory, "stale")), "Changed holder catalog facts were ignored");
            var cleared = prepared.ImportCopy();
            cleared.parts[0].foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(
                cleared.parts[0].foreignData.ToGeometry().WithoutContents(ForeignZdoChecks.Hash));
            var clearReview = UnboundExport.Review(cleared, name => Context(name));
            Check(clearReview.Blockers.Count == 0 && clearReview.ClearedMounts == 1 &&
                clearReview.TryWrite(cleared, name => Context(name), directory, "cleared", out _, out error), "Cleared holder export failed: " + error);
            var empty = ExternalBlueprintLibrary.LoadFile(Path.Combine(directory, "cleared", "cleared.vtbp")).Single();
            var emptyPrepared = ExternalBlueprintLibrary.PrepareForHost(empty, _ => null, ForeignZdoChecks.Hash, unboundContext: name => Context(name));
            Check(emptyPrepared.importError == null && emptyPrepared.parts[0].foreignData == null &&
                !empty.sourceDocuments.Single().ToGeometry().Unbound.Pieces[0].State.HasHash(ForeignZdoChecks.Hash("itemData")), "Clear resurrected holder itemData");
            var emptyReview = UnboundExport.Review(emptyPrepared, name => Context(name));
            Check(emptyReview.Blockers.Count == 0 && emptyReview.Mounts == 1 && emptyReview.ClearedMounts == 0, "Empty holder count or export admission changed");
            var forged = prepared.ImportCopy(); forged.parts[0].foreignData = null;
            Check(!CompositeBlueprintStore.ValidSources(forged.parts, forged.sourceDocuments), "Source-bound holder silently lost its item");
            Check(File.ReadAllBytes(input).SequenceEqual(original), "Holder producer file mutated");
            Console.WriteLine("PASS Unbound ItemStand file/store/paired cycle: host-only item/catalog snapshots, complete item bytes, orientation edit, native36/portable24, raw source/authority/clear-empty/stale-facts/drop guards; synthetic capabilities only, no placement/staged consumer/reload/network");
        }
    }
}
