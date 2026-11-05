using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class OpenKeepExportChecks
    {
        private static CompositeBlueprintStore.VectorData? Unit(string _) => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string fixture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "openkeep", "hut_2x2.json");
            var source = ExternalBlueprintLibrary.LoadFile(fixture).Single();
            foreach (OpenKeepExport.Consumer target in Enum.GetValues(typeof(OpenKeepExport.Consumer)))
            {
                var review = OpenKeepExport.Review(source, target, Unit);
                Require(review.Blockers.Count == 0 && review.Parts == 28 && review.OriginalSite, "Actual author input blocked");
                Require(review.TryWrite(source, Unit, directory, target.ToString(), out string result, out string error), error);
                var foreign = ExternalBlueprintLibrary.LoadFile(Path.Combine(result, target + ".json")).Single();
                var native = BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(result, target + ".buildworks.json")));
                Require(foreign.importError == null && foreign.parts.Count == 28 &&
                    native.sourceDocuments.Single().text == source.sourceDocuments.Single().text &&
                    native.parts.Select((p, i) => p.sourceRecord == i + 1).All(x => x), "Paired export lost full source/origin");
                Require(foreign.sourceDocuments.Single().ToGeometry().OpenKeep.LevelHalf == 2.5, "Known site lost");
            }
            var changed = ExternalBlueprintLibrary.DetachedCopy(source);
            var stale = OpenKeepExport.Review(changed, OpenKeepExport.Consumer.OpenKeep, Unit);
            changed.parts[0].position.x += 1;
            Require(!stale.TryWrite(changed, Unit, directory, "stale", out _, out string staleError) &&
                staleError == "$buildworks_exchange.plan_changed" && !Directory.Exists(Path.Combine(directory, "stale")), "Stale review published");
            var cancelled = OpenKeepExport.Review(source, OpenKeepExport.Consumer.OpenKeep, Unit); cancelled.CancelWrite();
            bool cancelledRaised = false; try { cancelled.TryWrite(source, Unit, directory, "cancel", out _, out _); }
            catch (OperationCanceledException) { cancelledRaised = true; }
            Require(cancelledRaised && !Directory.Exists(Path.Combine(directory, "cancel")) && !Directory.EnumerateDirectories(directory, "*.tmp").Any(), "Cancelled export left files");
            byte[] nativeBytes = BlueprintExportPair.Snapshot(source).Native, foreignBytes = Encoding.UTF8.GetBytes("{}");
            string staging = null;
            BlueprintExportPair midWrite = null;
            midWrite = new BlueprintExportPair(nativeBytes, foreignBytes, ".json", folder => {
                staging = folder;
                Require(File.ReadAllBytes(Path.Combine(folder, "mid-write.json")).SequenceEqual(foreignBytes) &&
                    !File.Exists(Path.Combine(folder, "mid-write.buildworks.json")), "Cancellation did not reach exact first-file boundary");
                midWrite.CancelWrite();
            });
            bool stoppedDuringWrite = false;
            try { midWrite.TryWrite(() => (nativeBytes, foreignBytes), directory, "mid-write", out _, out _); }
            catch (OperationCanceledException) { stoppedDuringWrite = true; }
            Require(stoppedDuringWrite && staging != null && !Directory.Exists(staging) &&
                !Directory.Exists(Path.Combine(directory, "mid-write")), "Mid-write cancellation left staging or published one file");
            Console.WriteLine("PASS cancellation after closed first foreign file: native not written, staging removed, no final directory");
            var rotated = ExternalBlueprintLibrary.DetachedCopy(source); rotated.parts[0].rotation.x = .1f; rotated.parts[0].rotation.w = (float)Math.Sqrt(.99);
            Require(OpenKeepExport.Review(rotated, OpenKeepExport.Consumer.OpenKeep, Unit).Blockers.Any(b => b.Reason == OpenKeepExport.BlockReason.Rotation), "Pitch/roll silently flattened");
            var scaled = ExternalBlueprintLibrary.DetachedCopy(source); scaled.parts[0].scale.x = 2;
            Require(OpenKeepExport.Review(scaled, OpenKeepExport.Consumer.OpenKeep, Unit).Blockers.Any(b => b.Reason == OpenKeepExport.BlockReason.Scale), "Custom scale silently omitted");
            scaled.parts[0].scale.x = 1; scaled.parts[0].baseScale = Unit("");
            Require(OpenKeepExport.Review(scaled, OpenKeepExport.Consumer.OpenKeep, _ => null).Blockers.Any(b => b.Reason == OpenKeepExport.BlockReason.PrefabScaleUnknown), "Unknown pinned prefab basis assumed unit");
            var extra = ExternalBlueprintLibrary.Parse("{\"name\":\"Extra\",\"site\":{\"level_half\":2,\"clear_radius\":3,\"stand\":[0,-2]},\"pieces\":[[\"wood_floor\",0,0,0,0,{\"future\":1}]]}", "extra.json");
            Require(OpenKeepExport.Review(extra, OpenKeepExport.Consumer.OpenKeep, Unit).Blockers.Count == 0 &&
                OpenKeepExport.Review(extra, OpenKeepExport.Consumer.DevBridge, Unit).Blockers.Any(b => b.Reason == OpenKeepExport.BlockReason.DevBridgeExtra), "Consumer tail rules conflated");
            var noSite = ExternalBlueprintLibrary.Parse("{\"name\":\"NoSite\",\"pieces\":[[\"wood_floor\",0,0,0,0]]}", "no-site.json");
            Require(OpenKeepExport.Review(noSite, OpenKeepExport.Consumer.DevBridge, Unit).Blockers.Any(b => b.Reason == OpenKeepExport.BlockReason.DevBridgeSite), "Missing DevBridge site invented over existing source");
            var own = ExternalBlueprintLibrary.DetachedCopy(source); own.sourceDocuments.Clear();
            foreach (var part in own.parts) { part.sourceDocumentId = null; part.sourceRecord = 0; }
            var derived = OpenKeepExport.Review(own, OpenKeepExport.Consumer.DevBridge, Unit);
            Require(derived.Blockers.Count == 0 && derived.DerivedSite && derived.TryWrite(own, Unit, directory, "derived", out _, out _), "New native document export did not create declared defaults");
            var edited = ExternalBlueprintLibrary.DetachedCopy(source);
            edited.name = "Edited hut";
            edited.parts[0].position.x += 1.25f; edited.parts[0].position.y += .5f;
            edited.parts[0].rotation = new CompositeBlueprintStore.QuaternionData { y = (float)Math.Sin(Math.PI / 8), w = (float)Math.Cos(Math.PI / 8) };
            edited.parts.Reverse();
            foreach (OpenKeepExport.Consumer target in Enum.GetValues(typeof(OpenKeepExport.Consumer)))
            {
                var review = OpenKeepExport.Review(edited, target, Unit);
                Require(review.TryWrite(edited, Unit, directory, "edited-" + target, out string editedPath, out string editedError), editedError);
                var foreign = ExternalBlueprintLibrary.LoadFile(Path.Combine(editedPath, "edited-" + target + ".json")).Single();
                var last = foreign.parts.Last();
                Require(foreign.name == edited.name && foreign.parts.Count == 28 &&
                    last.position.x == edited.parts.Last().position.x && last.position.y == edited.parts.Last().position.y &&
                    Math.Abs(last.rotation.y - edited.parts.Last().rotation.y) < .000001 &&
                    foreign.sourceDocuments.Single().ToGeometry().OpenKeep.LevelHalf == 2.5, "Edited pose/order/name/site export changed");
            }
            var store = new CompositeBlueprintStore(Path.Combine(directory, "snapshot-library.json"));
            foreach (string unsafeId in new[] { "wood\"quoted", "wood\u2028line", "wood\u2029line" })
            {
                var unusual = own.ImportCopy(); unusual.parts[0].prefabName = unsafeId;
                var unsafeReview = OpenKeepExport.Review(unusual, OpenKeepExport.Consumer.DevBridge, Unit);
                Require(unsafeReview.Blockers.Any(b => b.Reason == OpenKeepExport.BlockReason.DevBridgePrefab) &&
                    !unsafeReview.TryWrite(unusual, Unit, directory, "unsafe-id", out _, out _) &&
                    !Directory.Exists(Path.Combine(directory, "unsafe-id")), "Unsafe ID reached unescaped DevBridge command profile");
                Require(OpenKeepExport.Review(unusual, OpenKeepExport.Consumer.OpenKeep, Unit).Blockers.Count == 0,
                    "DevBridge string restriction leaked into normal OpenKeep JSON");
            }
            Console.WriteLine("PASS DevBridge quoted/Unicode-newline prefab refusal before publication; OpenKeep JSON unchanged");
            own.parts[0].scaleIsAbsolute = true;
            int callerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            float hostScale = 1;
            store.ConfigureImportScale(_ => {
                Require(System.Threading.Thread.CurrentThread.ManagedThreadId == callerThread, "OpenKeep prefab resolver entered file worker");
                return new CompositeBlueprintStore.VectorData { x = hostScale, y = 1, z = 1 };
            });
            var hostReview = store.ReviewOpenKeepExport(own, OpenKeepExport.Consumer.OpenKeep).GetAwaiter().GetResult();
            store.ExportOpenKeep(hostReview, own, directory, "async-host").GetAwaiter().GetResult();
            var oldHost = store.ReviewOpenKeepExport(own, OpenKeepExport.Consumer.OpenKeep).GetAwaiter().GetResult();
            hostScale = 2;
            bool staleHost = false;
            try { store.ExportOpenKeep(oldHost, own, directory, "stale-host").GetAwaiter().GetResult(); }
            catch (IOException failure) { staleHost = failure.Message == "$buildworks_exchange.plan_changed"; }
            Require(staleHost && !Directory.Exists(Path.Combine(directory, "stale-host")), "Changed host basis used stale foreign review");
            const string reliefText = "{\"name\":\"Synthetic relief, not a producer capture\",\"source\":\"BuildWorks synthetic QA\",\"pieces\":[[\"wood_floor\",0,0,0,0]]," +
                "\"site\":{\"level_half\":10,\"clear_radius\":4,\"stand\":[0,-3],\"water\":{\"floor\":1,\"depth\":0.25}," +
                "\"relief\":{\"x\":-1,\"z\":-1,\"width\":2,\"depth\":2,\"offsets\":[-0.0,0.25,-0.5,1.5]}," +
                "\"terrain\":[{\"op\":\"level\",\"at\":[0,0],\"half\":2,\"y\":0},{\"op\":\"level\",\"at\":[1,1],\"half\":0.5,\"y\":0.75}," +
                "{\"op\":\"paint\",\"at\":[0,0],\"half\":2,\"paint\":\"grass\"},{\"op\":\"paint\",\"at\":[1,1],\"half\":0.5,\"paint\":\"paved\"}]}}";
            File.WriteAllText(Path.Combine(directory, "synthetic-relief-source.json"), reliefText, new UTF8Encoding(false));
            var relief = ExternalBlueprintLibrary.Parse(reliefText, "synthetic-relief.json");
            relief.parts[0].position.x = 1.25f;
            foreach (OpenKeepExport.Consumer target in Enum.GetValues(typeof(OpenKeepExport.Consumer)))
            {
                var review = OpenKeepExport.Review(relief, target, Unit);
                if (target == OpenKeepExport.Consumer.DevBridge)
                {
                    Require(review.Blockers.Any(b => b.Reason == OpenKeepExport.BlockReason.DevBridgeRelief) &&
                        !review.TryWrite(relief, Unit, directory, "relief-DevBridge", out _, out _) &&
                        !Directory.Exists(Path.Combine(directory, "relief-DevBridge")), "DevBridge silently replaced relief by a flat pad");
                    Console.WriteLine("PASS DevBridge relief+level_half refusal: original known height grid retained, no flat fallback export");
                    continue;
                }
                Require(review.TryWrite(relief, Unit, directory, "relief-" + target, out string reliefPath, out string reliefError), reliefError);
                var model = ExternalBlueprintLibrary.LoadFile(Path.Combine(reliefPath, "relief-" + target + ".json")).Single().sourceDocuments.Single().ToGeometry().OpenKeep;
                Require(model.Relief.Width == 2 && model.Relief.Offsets.Count == 4 && model.Terrain.Count == 4 && model.Water != null,
                    "Synthetic known relief/terrain/water lost on edited export");
            }
            Console.WriteLine("PASS paired OpenKeep/DevBridge export: full native source backup,known site,origin,stale/cancel/no partial pair,explicit rotation/scale/site/tail blockers and declared new-document defaults");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
