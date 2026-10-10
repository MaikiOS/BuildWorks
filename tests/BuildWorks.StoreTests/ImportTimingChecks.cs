using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ImportTimingChecks
    {
        internal static int Run(string source, string imports, string directory)
        {
            byte[] original = File.ReadAllBytes(source);
            Console.WriteLine("File-only import timings; unit-scale resolver, no Unity prefab metadata, UI or thumbnails. Fresh store per run; OS/JIT caches are not flushed.");
            Console.WriteLine("run,input_bytes,documents,parts,existing_documents,initial_catalog_ms,review_ms,prepare_ms,missing_ms,submit_ms,worker_wait_ms,catalog_ready_ms,max_catalog_poll_ms");
            for (int run = 1; run <= 2; ++run)
            {
                string folder = Path.Combine(directory, "run" + run);
                var store = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
                Directory.CreateDirectory(store.ImportDirectory);
                if (imports != null)
                    foreach (string file in Directory.EnumerateFiles(imports))
                        File.Copy(file, Path.Combine(store.ImportDirectory, Path.GetFileName(file)));
                store = new CompositeBlueprintStore(Path.Combine(folder, "library.json"), true);
                store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                    prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0);
                var clock = Stopwatch.StartNew();
                WaitReady(store, out _);
                double initial = clock.Elapsed.TotalMilliseconds;
                int existing = store.CatalogAll().Count;
                clock.Restart();
                var review = CompositeBlueprintStore.ReviewImportFile(source, ForeignZdoChecks.Hash, 0);
                double read = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                review = store.PrepareImportReview(review, ForeignZdoChecks.Hash);
                double prepare = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                store.InspectMissing(review.Documents, ForeignZdoChecks.Hash);
                double missing = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                if (!store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error))
                    throw new InvalidOperationException(error);
                double submit = clock.Elapsed.TotalMilliseconds;
                var worker = (Task)typeof(CompositeBlueprintStore).GetField("importTask",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(store);
                clock.Restart();
                if (worker == null || !worker.Wait(60000)) throw new InvalidOperationException("Import worker timeout");
                double workerWait = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                WaitReady(store, out double peak);
                double ready = clock.Elapsed.TotalMilliseconds;
                string result = Path.GetFileName(store.ImportResultPath);
                var added = store.CatalogAll().Where(item => item.importSource == result ||
                    (item.importSource ?? "").StartsWith(result + "/", StringComparison.Ordinal)).ToArray();
                if (store.ImportError != null || added.Length != review.Documents.Count ||
                    added.Any(item => item.importError != null) || store.CatalogAll().Count != existing + added.Length)
                    throw new InvalidOperationException("Imported catalogue mismatch: " + store.ImportError);
                using (var zip = ZipFile.OpenRead(store.ImportResultPath))
                using (var copy = new MemoryStream())
                {
                    using (var input = zip.GetEntry("original/source.bin").Open()) input.CopyTo(copy);
                    if (!copy.ToArray().SequenceEqual(original)) throw new InvalidOperationException("Archived source changed");
                }
                if (!File.ReadAllBytes(source).SequenceEqual(original)) throw new InvalidOperationException("Input source changed");
                Console.WriteLine(FormattableString.Invariant($"{run},{original.Length},{review.Documents.Count},{review.Documents.Sum(item => item.parts.Count)},{existing},{initial:F3},{read:F3},{prepare:F3},{missing:F3},{submit:F3},{workerWait:F3},{ready:F3},{peak:F3}"));
            }
            return 0;
        }

        private static void WaitReady(CompositeBlueprintStore store, out double peak)
        {
            peak = 0;
            var total = Stopwatch.StartNew();
            do
            {
                var poll = Stopwatch.StartNew();
                store.CatalogAll();
                peak = Math.Max(peak, poll.Elapsed.TotalMilliseconds);
                if (!store.CatalogLoading) return;
                if (total.Elapsed.TotalSeconds >= 60) throw new InvalidOperationException("Catalogue preparation timeout");
                Thread.Sleep(1);
            } while (true);
        }

        internal static int RunCatalog(string imports, string directory)
        {
            Console.WriteLine("File-only independent probes; unit-scale resolver, no Unity/UI/thumbnails. Clone/evidence probes are not additive sub-stages of prepare. OS/JIT caches are not flushed.");
            Console.WriteLine("run,documents,parts,sources,read_ms,clone_ms,cloned_evidence_ms,warm_clone_evidence_ms,prepare_ms,foreign_parts,zdo_parts,hash_calls");
            for (int run = 1; run <= 2; ++run)
            {
                string folder = Path.Combine(directory, "catalog" + run);
                Directory.CreateDirectory(folder);
                foreach (string file in Directory.EnumerateFiles(imports))
                    File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
                var clock = Stopwatch.StartNew();
                var raw = ExternalBlueprintLibrary.Load(folder);
                double read = clock.Elapsed.TotalMilliseconds;
                string[] files = Directory.GetFiles(folder);
                byte[][] original = files.Select(File.ReadAllBytes).ToArray();
                clock.Restart();
                var copies = raw.Select(document => document.ImportCopy()).ToArray();
                double clone = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                foreach (var document in copies)
                    Geometry.BlueprintSourceDocument.Index(document.sourceDocuments?.Select(source => source.ToGeometry()));
                double evidence = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                foreach (var document in copies)
                    Geometry.BlueprintSourceDocument.Index(document.ImportCopy().sourceDocuments?.Select(source => source.ToGeometry()));
                double warm = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                long hashCalls = 0;
                var prepared = raw.Select(document => ExternalBlueprintLibrary.PrepareForHost(document,
                    _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, name => { ++hashCalls; return ForeignZdoChecks.Hash(name); }, 0)).ToArray();
                double prepare = clock.Elapsed.TotalMilliseconds;
                for (int index = 0; index < files.Length; ++index)
                    if (!File.ReadAllBytes(files[index]).SequenceEqual(original[index]))
                        throw new InvalidOperationException("Catalogue probe changed source bytes");
                for (int index = 0; index < raw.Count; ++index)
                    if (raw[index].parts.Count != prepared[index].parts.Count && prepared[index].importError == null)
                        throw new InvalidOperationException("Catalogue probe dropped valid parts");
                Console.WriteLine(FormattableString.Invariant($"{run},{raw.Count},{raw.Sum(document => document.parts.Count)},{raw.Sum(document => document.sourceDocuments?.Count ?? 0)},{read:F3},{clone:F3},{evidence:F3},{warm:F3},{prepare:F3},{raw.Sum(document => document.parts.Count(part => part.foreignData != null))},{raw.Sum(document => document.parts.Count(part => part.foreignData?.zdo != null))},{hashCalls}"));
            }
            return 0;
        }

        internal static int RunCatalogPhases(string imports, string directory)
        {
            Console.WriteLine("File-only catalogue phases; temporary copies, unit scale/no material support. No Unity/UI/thumbnails. Independent distinct-name probe is not additive. Worker wait is remaining time after scheduling; OS/JIT caches are not flushed.");
            Console.WriteLine("run,documents,parts,unique_prefabs,read_ms,distinct_names_probe_ms,sync_schedule_ms,scale_callback_ms,material_callback_ms,worker_wait_ms,final_catalog_ms,cached_catalog_ms");
            for (int run = 1; run <= 2; ++run)
            {
                string folder = Path.Combine(directory, "phases" + run);
                Directory.CreateDirectory(folder);
                foreach (string file in Directory.EnumerateFiles(imports))
                    File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
                string[] files = Directory.GetFiles(folder);
                byte[][] original = files.Select(File.ReadAllBytes).ToArray();
                var clock = Stopwatch.StartNew();
                var raw = ExternalBlueprintLibrary.Load(folder);
                double read = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                string[] names = raw.SelectMany(source => source.parts).Select(part => part.prefabName)
                    .Distinct(StringComparer.Ordinal).ToArray();
                double distinct = clock.Elapsed.TotalMilliseconds;
                var store = new CompositeBlueprintStore(Path.Combine(folder, "empty", "library.json"), true);
                WaitReady(store, out _);
                int thread = Thread.CurrentThread.ManagedThreadId, scales = 0, materials = 0;
                long scaleTicks = 0, materialTicks = 0;
                store.ConfigureImportScale(name =>
                {
                    long start = Stopwatch.GetTimestamp();
                    if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Scale callback left caller thread");
                    ++scales;
                    var scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
                    scaleTicks += Stopwatch.GetTimestamp() - start;
                    return scale;
                }, prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0, supportsMaterialSeed: name =>
                {
                    long start = Stopwatch.GetTimestamp();
                    if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Material callback left caller thread");
                    ++materials;
                    materialTicks += Stopwatch.GetTimestamp() - start;
                    return false;
                });
                // Inject parsed inputs only; measure the real CatalogAll snapshot, worker and publication paths.
                typeof(CompositeBlueprintStore).GetField("externalBlueprints", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(store, raw);
                clock.Restart();
                store.CatalogAll();
                double schedule = clock.Elapsed.TotalMilliseconds;
                var worker = (Task)typeof(CompositeBlueprintStore).GetField("preparationTask", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(store);
                clock.Restart();
                if (worker == null || !worker.Wait(60000)) throw new InvalidOperationException("Catalogue worker timeout");
                double wait = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                var catalog = store.CatalogAll();
                double final = clock.Elapsed.TotalMilliseconds;
                clock.Restart();
                var cached = store.CatalogAll();
                double cache = clock.Elapsed.TotalMilliseconds;
                if (store.CatalogLoading || store.ImportError != null || catalog.Count != raw.Count ||
                    !catalog.SequenceEqual(((Task<System.Collections.Generic.List<CompositeBlueprintStore.Blueprint>>)worker).Result) ||
                    !catalog.Select(item => item.importSource).SequenceEqual(raw.Select(item => item.importSource)) ||
                    !catalog.Select(item => item.parts.Count).SequenceEqual(raw.Select(item => item.parts.Count)) ||
                    !ReferenceEquals(cached, catalog) || scales != names.Length || materials != names.Length)
                    throw new InvalidOperationException("Catalogue phase probe changed catalogue or callback semantics");
                for (int index = 0; index < files.Length; ++index)
                    if (!File.ReadAllBytes(files[index]).SequenceEqual(original[index]))
                        throw new InvalidOperationException("Catalogue phase probe changed source bytes");
                double tickMs = 1000d / Stopwatch.Frequency;
                Console.WriteLine(FormattableString.Invariant($"{run},{raw.Count},{raw.Sum(item => item.parts.Count)},{names.Length},{read:F3},{distinct:F3},{schedule:F3},{scaleTicks * tickMs:F3},{materialTicks * tickMs:F3},{wait:F3},{final:F3},{cache:F3}"));
            }
            return 0;
        }
    }
}
