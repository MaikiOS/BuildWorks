using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PlannerImportChecks
    {
        [System.Runtime.Serialization.DataContract]
        private sealed class RegistrySnapshot
        {
            [System.Runtime.Serialization.DataMember(Name = "names")]
            public string[] Names;
        }
        private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        private static CompositeBlueprintStore.VectorData? Basis(string _) => new CompositeBlueprintStore.VectorData { x = 2, y = 3, z = 4 };

        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string fixtures = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "planner");
            foreach (string file in Directory.GetFiles(fixtures, "*.json")) RoundTrip(file, directory);
            string raw = File.ReadAllText(Path.Combine(fixtures, "planner-three-pieces.json"));
            var model = PlannerBlueprint.Parse(raw);
            void Refused(string text)
            {
                var result = ExternalBlueprintLibrary.Parse(text, "invalid.json");
                Check(result.importError != null && result.parts.Count == 0 && result.groups.Count == 0, "Partial/invalid planner accepted");
            }
            Refused(raw.Replace("\"version\": 3", "\"version\": 4"));
            Refused(raw.Replace("\"floor-2\"", "\"unknown-piece\""));
            Refused(raw.Replace("\"Main\"", "\"<b>Main</b>\""));
            Refused(raw.Replace("\"pieceId\":", "\"prefab\":\"wood_floor\",\"pieceId\":null,\"ignoredPieceId\":")
                .Replace("\"position\":", "\"lostPosition\":").Replace("\"rotation\":", "\"lostRotation\":"));
            Refused(raw.Replace("\"pieceId\":", "\"prefab\":\"wood_floor\",\"pieceId\":null,\"ignoredPieceId\":")
                .Replace("\"position\":", "\"lostPosition\":").Replace("\"rotation\":", "\"lostRotation\":")
                .Replace("\"title\": \"Untitled build\",", ""));
            Check(ExternalBlueprintLibrary.Parse("{\"formatVersion\":1,\"layers\":[],\"pieces\":[[\"wood_floor\",0,0,0,0]]}", "openkeep.json")
                .sourceDocuments.Single().format == OpenKeepBlueprint.Format, "One unknown header field hijacks OpenKeep family");
            var layers = model.Layers.Concat(Enumerable.Range(0, 65 - model.Layers.Count)
                .Select(i => new PlannerBlueprint.Layer("extra" + i, "Extra " + i))).ToArray();
            Refused(model.WritePieces(model.Pieces.Select(p => (p.Prefab, p.Position, p.Rotation, p.Record, p.LayerId)), layers: layers));
            string hybrid = raw.Replace("\"pieceId\":", "\"prefab\":\"wood_floor\",\"pieceId\":");
            Check(PlannerBlueprint.IsJsonBlueprint(hybrid) && BetterBuildingBlueprint.IsJsonBlueprint(hybrid), "Ambiguous fixture is not ambiguous");
            Refused(hybrid);
            string input = Path.Combine(directory, "input.json"); File.WriteAllText(input, raw, new UTF8Encoding(false));
            string zipPath = Path.Combine(directory, "mixed.zip");
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var entry in new[] { ("planner.json", raw), ("openkeep.json", "{\"formatVersion\":1,\"pieces\":[[\"wood_floor\",0,0,0,0]]}"),
                    ("betterbuilding.json", "{\"pieces\":[{\"prefab\":\"wood_floor\"}]}"), ("settings.json", "{\"hello\":\"world\"}") })
                    using (var writer = new StreamWriter(zip.CreateEntry(entry.Item1).Open(), new UTF8Encoding(false))) writer.Write(entry.Item2);
            }
            var mixed = ExternalBlueprintLibrary.LoadFile(zipPath);
            Check(mixed.Count == 3 && mixed.All(b => b.importError == null) && mixed.Select(b => b.sourceDocuments.Single().format).Distinct().Count() == 3,
                "Mixed JSON families/config filtering changed");
            Console.WriteLine("PASS planner real multi/empty/deleted layers; raw/native35/portable23/editor/absolute-scale; mixed JSON ZIP and fail-closed guards; no game proof.");
        }

        private static void RoundTrip(string file, string directory)
        {
            byte[] original = File.ReadAllBytes(file);
            var source = ExternalBlueprintLibrary.LoadFile(file).Single();
            Check(source.importError == null && source.importAbsoluteScale, "Planner file dispatch: " + source.importError);
            var evidence = source.sourceDocuments.Single().ToGeometry(); var model = evidence.Planner;
            Check(evidence.Format == PlannerBlueprint.Format && evidence.OriginalBytes().SequenceEqual(original), "Planner original bytes lost");
            Check(source.groups.Count == model.Layers.Count && source.parts.Count == model.Pieces.Count &&
                source.groups.All(g => g.sourceDocumentId == evidence.Id && model.Layers.Any(l => l.Id == g.sourceLayerId && l.Name == g.name)), "Empty/deleted/source layers changed");
            foreach (var p in model.Pieces)
            {
                var part = source.parts[p.Record - 1];
                Check(part.parentGroupId == source.groups.Single(g => g.sourceLayerId == p.LayerId).stableId &&
                    part.sourceDocumentId == evidence.Id && part.sourceRecord == p.Record && part.sourceFrameApplied, "Planner membership/address/frame lost");
            }
            long budget = ExternalBlueprintLibrary.MaximumTotalBytes;
            Check(ExternalBlueprintLibrary.BrowsableFile(file, ref budget), "Planner JSON hidden from browser");
            var pending = ExternalBlueprintLibrary.PrepareForHost(source, _ => null);
            Check(pending.parts.All(p => p.scaleIsAbsolute), "Unknown prefab basis silently resolved");
            var prepared = ExternalBlueprintLibrary.PrepareForHost(source, Basis);
            Check(prepared.parts.All(p => !p.scaleIsAbsolute && p.baseScale.HasValue && p.baseScale.Value.x == 1 && p.baseScale.Value.y == 1 && p.baseScale.Value.z == 1),
                "Producer absolute unit scale replaced by prefab intrinsic scale");
            var detached = ExternalBlueprintLibrary.DetachedCopy(prepared);
            Check(CompositeBlueprintStore.Valid(detached), "Detached planner invalid");
            var snapshot = BlueprintExportPair.Snapshot(detached).Document;
            Check(snapshot.groups.Count == model.Layers.Count && snapshot.parts.All(p => snapshot.groups.Any(g => g.stableId == p.parentGroupId)), "Editor snapshot loses layers");
            string path = Path.Combine(directory, Path.GetFileNameWithoutExtension(file) + ".library.json");
            var store = new CompositeBlueprintStore(path);
            Check(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
            foreach (var b in new[] { saved, new CompositeBlueprintStore(path).All().Single(), BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved))) })
                Check(b.groups.Count == model.Layers.Count && b.sourceDocuments.Single().text == evidence.Text && b.groups.All(g => g.sourceDocumentId == evidence.Id), "Native/portable planner source/group roundtrip");
            Check(File.ReadAllBytes(file).SequenceEqual(original), "Planner input overwritten");
        }

        internal static void Actual(string cases, string directory)
        {
            Run(directory);
            var small = ExternalBlueprintLibrary.LoadFile(Path.Combine(cases, "producer-run-v1", "planner-three-pieces.json")).Single();
            foreach (string extension in new[] { ".blueprint", ".vbuild" })
            {
                var nativePair = ExternalBlueprintLibrary.LoadFile(Path.Combine(cases, "producer-run-v1", "planner-three-pieces" + extension)).Single();
                Check(nativePair.importError == null && nativePair.parts.Count == small.parts.Count &&
                    nativePair.parts.Zip(small.parts, (p, n) => p.prefabName == n.prefabName &&
                        Math.Abs(p.position.x - n.position.x) < .000002 && Math.Abs(p.position.y - n.position.y) < .000002 &&
                        Math.Abs(p.position.z - n.position.z) < .000002 &&
                        Math.Abs(Math.Abs(p.rotation.x * n.rotation.x + p.rotation.y * n.rotation.y +
                            p.rotation.z * n.rotation.z + p.rotation.w * n.rotation.w) - 1) < .000002).All(ok => ok),
                    "Original native export profile differs from planner: " + extension);
            }
            foreach (string stem in new[] { "catalog-yaw0", "catalog-yaw22-added" })
            {
                string file = Path.Combine(cases, "producer-catalog-v1", stem + ".json"); RoundTrip(file, directory);
                var source = ExternalBlueprintLibrary.LoadFile(file).Single();
                var native = ExternalBlueprintLibrary.LoadFile(Path.ChangeExtension(file, ".blueprint")).Single();
                Check(source.parts.Count == native.parts.Count && source.parts.Zip(native.parts, (p, n) => p.prefabName == n.prefabName &&
                    Math.Abs(p.position.x - n.position.x) < 0.000002 && Math.Abs(p.position.y - n.position.y) < 0.000002 && Math.Abs(p.position.z - n.position.z) < 0.000002 &&
                    Math.Abs(Math.Abs(p.rotation.x * n.rotation.x + p.rotation.y * n.rotation.y + p.rotation.z * n.rotation.z + p.rotation.w * n.rotation.w) - 1) < 0.000002).All(ok => ok),
                    "Planner host projection differs from real native writer pair");
                using (var registry = File.OpenRead(Path.Combine(cases, "native326-registry-names.json")))
                {
                    var snapshot = (RegistrySnapshot)new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(RegistrySnapshot)).ReadObject(registry);
                    var names = new HashSet<string>(snapshot.Names, StringComparer.Ordinal);
                    CompositeBlueprintStore.VectorData? NativeBasis(string name) => names.Contains(name) ? Basis(name) : null;
                    int ownerThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                    var pipeline = new CompositeBlueprintStore(Path.Combine(directory, "pipeline-" + stem, "library.json"), backgroundImports: true);
                    pipeline.ConfigureImportScale(name =>
                    {
                        Check(System.Threading.Thread.CurrentThread.ManagedThreadId == ownerThread, "Worker touched host prefab resolver");
                        return NativeBasis(name);
                    }, prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0);
                    pipeline.ConfigurePrefabMetadata(name =>
                    {
                        Check(System.Threading.Thread.CurrentThread.ManagedThreadId == ownerThread, "Worker touched host metadata resolver");
                        return names.Contains(name) ? CompositeBlueprintStore.PrefabMetadata.FromGeometry(
                            new BlueprintPrefabMetadata(1, name, name, "Piece", "fixture-registry", new Point3(1, 1, 1))) : null;
                    });
                    for (int attempt = 0; attempt < 2000; ++attempt)
                    {
                        pipeline.CatalogAll();
                        if (!pipeline.CatalogLoading) break;
                        System.Threading.Thread.Sleep(10);
                    }
                    Check(!pipeline.CatalogLoading && pipeline.ImportError == null, "Initial production catalog did not become ready");
                    var inspected = CompositeBlueprintStore.ReviewImportFile(file, ForeignZdoChecks.Hash, 0);
                    var reviewed = pipeline.PrepareImportReview(inspected, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
                    Check(reviewed.Missing.PrefabIds.Count == 0 && reviewed.Documents.Single().parts.All(p => names.Contains(p.prefabName)),
                        "Production snapshot review lost canonical targets or invented missing parts");
                    Check(pipeline.TryImportReviewed(reviewed, false, false, ForeignZdoChecks.Hash, out string pipelineError), pipelineError);
                    for (int attempt = 0; attempt < 2000; ++attempt)
                    {
                        pipeline.CatalogAll();
                        if (!pipeline.CatalogLoading) break;
                        System.Threading.Thread.Sleep(10);
                    }
                    Check(!pipeline.CatalogLoading && pipeline.ImportError == null && pipeline.CatalogAll().Single().parts.All(p => names.Contains(p.prefabName)),
                        "Production import/catalog snapshot lost canonical targets: " + pipeline.ImportError);
                    Check(pipeline.CatalogAll().Single().parts.All(p => p.metadata != null && p.metadata.ToGeometry().PrefabId == p.prefabName),
                        "Effective native metadata was not recaptured by production pipeline");
                    var exactFirst = ExternalBlueprintLibrary.PrepareForHost(source, Basis);
                    Check(exactFirst.parts.Zip(source.parts, (p, s) => p.prefabName == s.prefabName && p.originalPrefabName == null).All(ok => ok),
                        "Existing exact prefab was replaced by a case alias");
                    var absent = ExternalBlueprintLibrary.PrepareForHost(source, _ => null);
                    Check(absent.parts.All(p => p.scaleIsAbsolute && p.originalPrefabName == null), "Unmeasured alias target invented native identity");
                    var projected = ExternalBlueprintLibrary.PrepareForHost(source, NativeBasis);
                    string[] missing = projected.parts.Select(p => p.prefabName).Distinct(StringComparer.Ordinal)
                        .Where(name => !names.Contains(name)).ToArray();
                    Check(missing.Length == 0, "Planner names absent from measured native registry: " + string.Join(",", missing));
                    Check(projected.importError == null && projected.parts.All(p => !p.scaleIsAbsolute), "Native projection left unresolved parts");
                    var projectedNative = ExternalBlueprintLibrary.PrepareForHost(native, NativeBasis);
                    Check(projectedNative.parts.Zip(projected.parts, (n, p) => n.prefabName == p.prefabName).All(ok => ok),
                        "Producer native export and planner JSON effective identities differ");
                    foreach (var part in projected.parts)
                    {
                        var original = source.parts.Single(p => p.stableId == part.stableId);
                        var expected = names.Where(n => string.Equals(n, original.prefabName, StringComparison.OrdinalIgnoreCase)).ToArray();
                        Check(expected.Length == 1 && part.prefabName == expected[0] &&
                            part.sourceRecord == original.sourceRecord && part.parentGroupId == original.parentGroupId &&
                            (part.originalPrefabName ?? part.prefabName) == original.prefabName,
                            "Measured native identity/source binding mismatch");
                    }
                    var roundtrip = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(
                        BlueprintExchangeFormat.Encode(ExternalBlueprintLibrary.DetachedCopy(projected))));
                    Check(CompositeBlueprintStore.Valid(roundtrip) && roundtrip.sourceDocuments.Single().text == source.sourceDocuments.Single().text,
                        "Projected portable/source roundtrip failed");
                    var export = PlannerExport.Review(roundtrip, NativeBasis);
                    string folder = "native-projection-" + stem;
                    Check(export.Blockers.Count == 0 && export.TryWrite(roundtrip, NativeBasis, directory, folder, out var written, out _) &&
                        File.ReadAllText(Path.Combine(written, folder + ".json")) == File.ReadAllText(file), "Native projection changed no-edit planner JSON");
                    if (stem == "catalog-yaw0")
                    {
                        Check(PlannerPrefabNames.Native("ARMORSTAND") == "ARMORSTAND" &&
                            !BlueprintPrefabMigration.IsRecordedPair("ARMORSTAND", "ArmorStand"), "Generic case alias accepted");
                        var foreign = ExternalBlueprintLibrary.Parse("{\"pieces\":[{\"prefab\":\"armorstand\"}]}", "foreign.json");
                        var unchanged = ExternalBlueprintLibrary.PrepareForHost(foreign, Basis);
                        Check(unchanged.importError == null && unchanged.parts.Single().prefabName == "armorstand" &&
                            unchanged.parts[0].originalPrefabName == null, "Unrelated JSON family was silently remapped");
                        var forged = ExternalBlueprintLibrary.DetachedCopy(unchanged);
                        forged.parts[0].originalPrefabName = "armorstand"; forged.parts[0].prefabName = forged.prefabName = "ArmorStand";
                        Check(!CompositeBlueprintStore.Valid(forged), "Cross-format prefab provenance forgery accepted");
                        string legacy = Path.Combine(cases, "legacy-cache326-" + stem + ".buildworks.json");
                        byte[] bytes = File.ReadAllBytes(legacy);
                        var old = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(bytes));
                        var preparedOld = ExternalBlueprintLibrary.PrepareForHost(old, NativeBasis);
                        Check(CompositeBlueprintStore.Valid(preparedOld) && preparedOld.parts.All(p => names.Contains(p.prefabName)) &&
                            preparedOld.sourceDocuments.Single().text == old.sourceDocuments.Single().text &&
                            File.ReadAllBytes(legacy).SequenceEqual(bytes), "Frozen portable23 source/identity compatibility failed");
                    }
                }
                Console.WriteLine("PASS actual planner/native writer pair " + stem + " " + source.parts.Count + " parts; no game/prefab-resolution proof.");
            }
        }
    }
}
