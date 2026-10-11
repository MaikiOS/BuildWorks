using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ForeignZdoChecks
    {
        // Match the verified host string hash, without loading the game in this file-only test.
        internal static int Hash(string value)
        {
            unchecked
            {
                int a = 5381, b = a;
                for (int index = 0; index < value.Length && value[index] != '\0'; index += 2)
                {
                    a = ((a << 5) + a) ^ value[index];
                    if (index == value.Length - 1 || value[index + 1] == '\0') break;
                    b = ((b << 5) + b) ^ value[index + 1];
                }
                return a + b * 1566083941;
            }
        }
        internal static void Run()
        {
            PreparationScope();
            MaterialSeeds();
            BackgroundSubmission();
            Require(Hash("item") == -1126421579 && Hash("health") == -883022132, "Host hash drift");
            string raw = Package(w => {
                w.Write(9); w.Write((byte)2);
                w.Write(Hash("health")); w.Write(-1f); w.Write(Hash("WearNTear.m_health")); w.Write(-2f);
                w.Write((byte)4);
                foreach (string key in new[] { "HasFields", "HasFieldsWearNTear", "HasFieldsStaticTarget", "StaticTarget.m_randomTarget" })
                { w.Write(Hash(key)); w.Write(key == "StaticTarget.m_randomTarget" ? 0 : 1); }
            });
            string text = "#Name:Native rules\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1;" + raw + "\nwoodwall;;1;0;0;0;0;0;1;\"\"";
            var source = ExternalBlueprintLibrary.Parse(text, "rules.blueprint");
            var prepared = Prepare(source);
            Require(prepared.importError == null && prepared.parts[0].foreignData == null && source.parts[0].foreignData.zdo == raw &&
                prepared.sourceDocuments[0].ToGeometry().Text == text && !CompositeBlueprintStore.HasForeignData(prepared),
                "Native-rules conversion lost original evidence or left admin fields active");
            var portable = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(ExternalBlueprintLibrary.DetachedCopy(prepared))));
            Require(portable.parts[0].foreignData == null && portable.sourceDocuments[0].ToGeometry().Text == text, "Conversion not portable/lossless archive");
            string unknown = Package(w => { w.Write(8); w.Write((byte)1); w.Write(Hash("Unreviewed.m_field")); w.Write(1); });
            Require(Prepare(ExternalBlueprintLibrary.Parse(text.Replace(raw, unknown), "unknown.blueprint")).parts[0].foreignData.zdo == unknown,
                "Unknown field silently discarded");
            string wrongType = Package(w => { w.Write(8); w.Write((byte)1); w.Write(Hash("health")); w.Write(100); });
            Require(Prepare(ExternalBlueprintLibrary.Parse(text.Replace(raw, wrongType), "wrong.blueprint")).parts[0].foreignData.zdo == wrongType,
                "Wrong metadata type silently discarded");
            string sibling = Package(w => { w.Write(17); w.Write((byte)1); w.Write(Hash("health")); w.Write(-1f);
                w.Write((byte)1); w.Write(Hash("health")); w.Write("unknown sibling"); });
            Require(Prepare(ExternalBlueprintLibrary.Parse(text.Replace(raw, sibling), "sibling.blueprint")).parts[0].foreignData.zdo == sibling,
                "Known typed key consumed its unknown sibling");
            var modified = ExternalBlueprintLibrary.Parse(text, "unbound.blueprint");
            modified.parts[0].foreignData.zdo = Package(w => { w.Write(1); w.Write((byte)1); w.Write(Hash("health")); w.Write(50f); });
            Require(Prepare(modified).parts[0].foreignData != null, "Unarchived modified payload discarded under unrelated source evidence");
            string sign = Package(w => { w.Write(16); w.Write((byte)2); w.Write(Hash("text")); w.Write("Дом Ostrix\nВход");
                w.Write(Hash("author")); w.Write("старый автор"); });
            var signSource = Prepare(ExternalBlueprintLibrary.Parse(text.Replace("woodwall", "sign").Replace(raw, sign), "sign.blueprint"));
            Require(signSource.parts[0].signText == "Дом Ostrix\nВход" && signSource.parts[0].foreignData == null, "ZDO sign lost");
            string legacySign = text.Replace("woodwall", "sign").Replace(";\"\";", ";\"quoted\" literal;");
            var legacySource = Prepare(ExternalBlueprintLibrary.Parse(legacySign.Replace(raw, sign), "legacy-sign-zdo.blueprint"));
            Require(legacySource.importError == null && legacySource.parts[0].signText == "Дом Ostrix\nВход" &&
                legacySource.parts[0].foreignData == null && legacySource.sourceDocuments.Single().text == legacySign.Replace(raw, sign),
                "Legacy literal refused source binding or replaced authoritative ZDO sign text");
            var legacyUnknown = Prepare(ExternalBlueprintLibrary.Parse(legacySign.Replace(raw, unknown), "legacy-sign-unknown.blueprint"));
            Require(legacyUnknown.importError == null && legacyUnknown.parts[0].foreignData?.zdo == unknown,
                "Legacy literal discarded unknown sign ZDO");
            var unarchivedSignInfo = ExternalBlueprintLibrary.Parse(text.Replace("woodwall", "sign").Replace(raw, sign), "unarchived-sign.blueprint");
            unarchivedSignInfo.parts[0].foreignData.info = "unarchived override";
            Require(Prepare(unarchivedSignInfo).parts[0].foreignData?.info == "unarchived override", "Unarchived sign Info lost");
            var signFallback = Prepare(ExternalBlueprintLibrary.Parse(text.Replace("woodwall", "sign").Replace(";\"\";", ";\"stale Info\";"), "sign-fallback.blueprint"));
            Require(signFallback.parts[0].foreignData != null, "Info falsely used as missing ZDO text fallback");
            string scaled = Package(w => { w.Write(2); w.Write((byte)1); w.Write(Hash("scale")); w.Write(2f); w.Write(3f); w.Write(4f); });
            var vbuild = Prepare(ExternalBlueprintLibrary.Parse("woodwall;0;0;0;1;0;0;0;" + scaled + "\nwoodwall;0;0;0;1;1;0;0", "scaled.vbuild"));
            Require(vbuild.parts[0].foreignData != null, "vbuild absolute ZDO scale discarded without row-scale replacement");
            string mount = Package(w => { w.Write(25); w.Write((byte)1); w.Write(Hash("durability")); w.Write(20.125f);
                w.Write((byte)2); w.Write(Hash("quality")); w.Write(3); w.Write(Hash("worldLevel")); w.Write(0);
                w.Write((byte)1); w.Write(Hash("item")); w.Write("AxeIron"); });
            var mounted = Prepare(ExternalBlueprintLibrary.Parse(text.Replace("woodwall", "itemstand").Replace(raw, mount), "mount.blueprint"));
            Require(mounted.parts[0].foreignData.format == BlueprintForeignPartData.NativeItemStandFormat &&
                BlueprintItemStandContents.TryRead(mounted.parts[0].foreignData.ToGeometry(), Hash, out var item) &&
                item.DurabilityHundredths == 2012 && item.Quality == 3 && item.Prefab == "AxeIron", "Legacy mount did not become native full snapshot");
            var malformed = Prepare(ExternalBlueprintLibrary.Parse(text.Replace(raw, "not base64"), "bad.blueprint"));
            Require(malformed.parts[0].foreignData != null, "Malformed state enabled construction");
            Console.WriteLine("PASS foreign typed conversion: ordinary survival rules, exact archived source, sign, full mount, unknown/malformed refusal");
        }
        private static CompositeBlueprintStore.Blueprint Prepare(CompositeBlueprintStore.Blueprint source) =>
            ExternalBlueprintLibrary.PrepareForHost(source, _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, Hash, 0);
        private static void MaterialSeeds()
        {
            string Packet(int seed, bool unknown = false) => Package(w => {
                w.Write(8); w.Write((byte)(unknown ? 2 : 1)); w.Write(Hash("RandMatSeed")); w.Write(seed);
                if (unknown) { w.Write(Hash("unknown")); w.Write(1); }
            });
            CompositeBlueprintStore.Blueprint Parse(string packet, bool vbuild = false) => ExternalBlueprintLibrary.Parse(vbuild
                ? "woodwall;0;0;0;1;0;0;0;" + packet + "\nwoodwall;0;0;0;1;1;0;0"
                : "#Name:Seed\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1;" + packet + "\nwoodwall;;1;0;0;0;0;0;1;\"\"", vbuild ? "seed.vbuild" : "seed.blueprint");
            CompositeBlueprintStore.Blueprint Convert(CompositeBlueprintStore.Blueprint source, bool supported = true) =>
                ExternalBlueprintLibrary.PrepareForHost(source, _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                    Hash, 0, _ => supported);
            foreach (bool vbuild in new[] { false, true })
                foreach (int seed in new[] { 0, 1234, int.MaxValue })
                {
                    var source = Parse(Packet(seed), vbuild); var converted = Convert(source);
                    Require(converted.importError == null && converted.parts[0].materialSeed == seed && converted.parts[0].foreignData == null,
                        "Eligible material seed not converted");
                    Require(source.parts[0].materialSeed == null && source.parts[0].foreignData.zdo == Packet(seed) &&
                        source.sourceDocuments[0].ToGeometry().Text == converted.sourceDocuments[0].ToGeometry().Text,
                        "Material conversion mutated source or provenance");
                    var roundtrip = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(ExternalBlueprintLibrary.DetachedCopy(converted))));
                    Require(roundtrip.parts[0].materialSeed == seed &&
                        CompositeBlueprintStore.CopyWithContentsPolicy(roundtrip, true, true, Hash).parts[0].materialSeed == seed,
                        "Roundtrip/contents settings lost constructive material state");
                }
            foreach (string packet in new[] { Packet(-1), Packet(1234, true),
                Package(w => { w.Write(1); w.Write((byte)1); w.Write(Hash("RandMatSeed")); w.Write(1234f); }),
                Package(w => { w.Write(24); w.Write((byte)1); w.Write(Hash("RandMatSeed")); w.Write(1);
                    w.Write((byte)1); w.Write(Hash("RandMatSeed")); w.Write("1"); }) })
            {
                var converted = Convert(Parse(packet));
                Require(converted.parts[0].materialSeed == null && converted.parts[0].foreignData.zdo == packet,
                    "Invalid or partially understood material packet was extracted");
            }
            Require(Convert(Parse(Packet(1234)), false).parts[0].materialSeed == null &&
                Prepare(Parse(Packet(1234))).parts[0].foreignData != null, "Missing capability callback invented support");
            var rebound = Parse(Packet(1234)); rebound.parts[0].foreignData.zdo = Packet(12);
            Require(Convert(rebound).parts[0].materialSeed == null, "Unbound source material packet converted");
            var conflict = Parse(Packet(1234)); conflict.parts[0].materialSeed = 9;
            Require(Convert(conflict).parts[0].materialSeed == 9 && Convert(conflict).parts[0].foreignData != null,
                "Foreign state silently overrode an existing material seed");
            var store = new CompositeBlueprintStore(Path.Combine(Path.GetTempPath(), "BuildWorks-seed-review-" + Guid.NewGuid() + ".json"));
            bool support = true; object context = new object(); int thread = Environment.CurrentManagedThreadId;
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, () => context,
                Hash, () => 0, _ => {
                    Require(Environment.CurrentManagedThreadId == thread, "Worker accessed the live host material resolver");
                    return support;
                });
            var raw = Parse(Packet(1234));
            var review = new CompositeBlueprintStore.ImportReview { Source = "source.blueprint", Bytes = new byte[] { 1, 2 },
                Documents = new System.Collections.Generic.List<CompositeBlueprintStore.Blueprint> { raw } };
            var prepared = store.PrepareImportReview(review, Hash).GetAwaiter().GetResult();
            Require(prepared.Contents.Unknown == 0 && prepared.Documents[0].parts[0].materialSeed == 1234 &&
                raw.parts[0].materialSeed == null && prepared.Source == review.Source && ReferenceEquals(prepared.Bytes, review.Bytes),
                "Host review lost source or reported supported appearance unknown");
            var snapshot = store.MaterialSeedSupportSnapshot(prepared.Documents); support = false;
            Require(System.Threading.Tasks.Task.Run(() => CompositeBlueprintStore.InspectContents(prepared.Documents[0], Hash, snapshot)).Result.Unknown == 0 &&
                CompositeBlueprintStore.InspectContents(prepared.Documents[0], Hash, store.SupportsMaterialSeed).Unknown == 1 &&
                store.PrepareImportReview(review, Hash).GetAwaiter().GetResult().Contents.Unknown > 0, "Material summary used a stale host or worker resolver");
            support = true; context = null;
            Require(!store.SupportsMaterialSeed("woodwall"), "Absent host context declared material support");
            context = new object();
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                () => context, supportsMaterialSeed: _ => true);
            prepared.Documents[0].parts[0].sourceDocumentId = null; prepared.Documents[0].parts[0].sourceLine = 0;
            prepared.Documents[0].parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = BlueprintForeignPartData.NativeContainerFormat, info = "5:2:0:0", zdo = "bQAAAAAA" };
            var contentsReview = store.PrepareImportReview(prepared, Hash).GetAwaiter().GetResult();
            Require(contentsReview.Contents.Storages == 1 && contentsReview.Contents.Unknown == 0 &&
                contentsReview.Contents.CanClearStorage && contentsReview.Contents.CanClearMounts,
                "Host review dropped the inspection hash or blocked contents choices for supported material");
            Require(CompositeBlueprintStore.CopyWithContentsPolicy(contentsReview.Documents[0], true, false, Hash)
                .parts[0].materialSeed == 1234, "Clearing storage lost independent material state");
            Console.WriteLine("PASS source-bound blueprint/vbuild material seeds: zero/Max, capability, strict types, sibling atomicity, provenance, portable and independent contents settings");
            Console.WriteLine("PASS host material review: conservative raw, supported/unsupported current host, source unchanged, pure worker snapshot and lost-context refusal");
            BackgroundReview(Parse(Packet(1234)));
        }

        private static void BackgroundReview(CompositeBlueprintStore.Blueprint raw)
        {
            string directory = Path.Combine(Path.GetTempPath(), "BuildWorks-background-review-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
                int owner = Environment.CurrentManagedThreadId, scaleReads = 0, level = 0;
                object context = new object(), originalContext = context;
                Action host = () => Require(Environment.CurrentManagedThreadId == owner, "Worker accessed live host state");
                store.ConfigureImportScale(_ => { host(); ++scaleReads; return new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }; },
                    () => { host(); return context; }, Hash, () => { host(); return level; }, _ => { host(); return true; });
                var review = new CompositeBlueprintStore.ImportReview { Source = "source.blueprint", Bytes = new byte[] { 1, 2 },
                    Documents = new System.Collections.Generic.List<CompositeBlueprintStore.Blueprint> { raw, raw } };
                using (var entered = new System.Threading.ManualResetEventSlim())
                using (var release = new System.Threading.ManualResetEventSlim())
                {
                    var pending = store.PrepareImportReview(review, name =>
                    {
                        entered.Set();
                        Require(Environment.CurrentManagedThreadId != owner, "Preparation still ran on the caller thread");
                        Require(release.Wait(10000), "Background review test release timeout");
                        return Hash(name);
                    });
                    try
                    {
                        Require(entered.Wait(10000) && !pending.IsCompleted && scaleReads == 1,
                            "Review did not return a pending worker or snapshotted the same prefab repeatedly");
                        context = new object(); level = 1;
                    }
                    finally { release.Set(); }
                    var prepared = pending.GetAwaiter().GetResult();
                    Require(prepared.Documents.Count == 2 && prepared.Documents.All(document => document.parts[0].materialSeed == 1234) &&
                        prepared.Contents.Unknown == 0 && ReferenceEquals(prepared.Bytes, review.Bytes) && raw.parts[0].materialSeed == null,
                        "Background normalization lost frozen host values or mutated the raw review");
                    Require(prepared.Missing.Known && ReferenceEquals(prepared.Missing.HostContext, originalContext) &&
                        prepared.Missing.Parts == 0 && prepared.Missing.RetainedContents.Unknown == 0,
                        "Background missing review called a live host or lost its frozen context/material summary");
                    Require(!prepared.IsHostCurrent() && !store.TryImportReviewed(prepared, false, false, Hash, out string error) &&
                        error.Contains("contents_stale") && !Directory.Exists(store.ImportDirectory),
                        "Stale prepared review was accepted or wrote an import");
                    context = originalContext; level = 0;
                    Require(prepared.IsHostCurrent(), "Unchanged captured host was rejected");
                    level = 1;
                    Require(!prepared.IsHostCurrent(), "World-level change was not detected");
                    level = 0;
                    store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, () => context);
                    Require(!prepared.IsHostCurrent(), "Resolver reconfiguration did not invalidate the pending review");
                }
                Console.WriteLine("PASS asynchronous import review: caller-only distinct prefab snapshot, held worker, unchanged source, context/world/resolver invalidation and no stale writes");
            }
            finally { Directory.Delete(directory, true); }
        }
        private static void PreparationScope()
        {
            string Text(Func<string, int> hash, bool unknown = false)
            {
                string raw = Package(w => { w.Write(1); w.Write((byte)(unknown ? 2 : 1));
                    w.Write(hash("health")); w.Write(12f);
                    if (unknown) { w.Write(hash("unknown_state")); w.Write(7f); } });
                return "#Name:Preparation\n#Pieces\n" + string.Concat(Enumerable.Range(0, 64).Select(index =>
                    "woodwall;Workbench;" + index + ";0;0;0;0;0;1;\"\";1;1;1;" + raw + ";1\n"));
            }
            var scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
            foreach (int salt in new[] { 0, 12345 })
            {
                Func<string, int> hash = name => unchecked(Hash(name) + salt);
                string text = Text(hash);
                var source = ExternalBlueprintLibrary.Parse(text, "scope.blueprint");
                int reads = 0;
                Func<string, int> measured = name => { if (name == "HasFieldsWearNTear") ++reads; return hash(name); };
                var prepared = ExternalBlueprintLibrary.PrepareForHost(source, _ => scale, measured, 0);
                Require(prepared.importError == null && prepared.parts.Count == 64 && prepared.parts.All(part => part.foreignData == null),
                    "Scoped preparation lost supported state or reused another hash table");
                Require(reads == 1, "Ordinary ZDO field table rebuilt per part: " + reads);
                Require(source.parts.All(part => part.foreignData?.zdo != null) && source.sourceDocuments[0].text == text,
                    "Scoped preparation modified source evidence");
                string path = Path.Combine(Path.GetTempPath(), "BuildWorks.Scope." + Guid.NewGuid().ToString("N") + ".blueprint");
                try
                {
                    File.WriteAllText(path, text); reads = 0;
                    var review = CompositeBlueprintStore.ReviewImportFile(path, measured, 0);
                    Require(reads == 1 && review.Documents.Single().parts.All(part => part.foreignData == null) &&
                        File.ReadAllText(path) == text, "Review rebuilt field table or changed input");
                }
                finally { if (File.Exists(path)) File.Delete(path); }
                var unknown = ExternalBlueprintLibrary.PrepareForHost(
                    ExternalBlueprintLibrary.Parse(Text(hash, true), "unknown.blueprint"), _ => scale, hash, 0);
                Require(unknown.parts.Count == 64 && unknown.parts.All(part => part.foreignData?.zdo != null),
                    "Scoped table consumed unknown sibling state");
            }
            var plain = ExternalBlueprintLibrary.Parse("#Name:Plain\n#Pieces\nwoodwall;Workbench;0;0;0;0;0;0;1;\"\";1;1;1\n", "plain.blueprint");
            Require(ExternalBlueprintLibrary.PrepareForHost(plain, _ => scale,
                _ => throw new InvalidOperationException("Hash called without eligible state"), 0).importError == null,
                "Ordinary state table was not lazy");
            Console.WriteLine("PASS ordinary ZDO table: one per preparation/review, independent hashes, unknown state and immutable source");
        }
        private static void BackgroundSubmission()
        {
            string folder = Path.Combine(Path.GetTempPath(), "BuildWorks-submit-worker-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string packet = Package(w => { w.Write(8); w.Write((byte)1); w.Write(Hash("RandMatSeed")); w.Write(1234); });
                string source = Path.Combine(folder, "submit.blueprint");
                File.WriteAllText(source, "#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";2;.5;1.25;" + packet +
                    "\nwoodwall;;1;0;0;0;0;0;1;\"\";2;.5;1.25");
                var raw = CompositeBlueprintStore.ReviewImportFile(source, Hash, 0);
                byte[] original = File.ReadAllBytes(source);
                int owner = Environment.CurrentManagedThreadId, scaleReads = 0, supportReads = 0, metadataReads = 0, level = 0;
                bool support = true, hold = true;
                var scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
                var metadata = CompositeBlueprintStore.PrefabMetadata.FromGeometry(new BlueprintPrefabMetadata(1,
                    "woodwall", "Snapshot wall", "Piece", "host-snapshot", new Point3(1, 1, 1),
                    new Point3(-1, -1, -1), new Point3(1, 1, 1)));
                void Host() => Require(Environment.CurrentManagedThreadId == owner, "Import worker called live host resolver");
                using (var entered = new System.Threading.ManualResetEventSlim())
                using (var release = new System.Threading.ManualResetEventSlim())
                {
                    var store = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
                    store.ConfigureImportScale(_ => { Host(); scaleReads++; return scale; },
                        prefabHash: name =>
                        {
                            if (hold)
                            {
                                Require(Environment.CurrentManagedThreadId != owner, "Submission normalized on caller thread");
                                entered.Set();
                                Require(release.Wait(10000), "Submission worker release timed out");
                            }
                            return Hash(name);
                        }, worldLevel: () => { Host(); return level; },
                        supportsMaterialSeed: _ => { Host(); supportReads++; return support; });
                    store.ConfigurePrefabMetadata(_ => { Host(); metadataReads++; return metadata; });
                    try
                    {
                        Require(store.TryImportReviewed(raw, false, false, Hash, out string error), error);
                        Require(entered.Wait(10000) && store.CatalogLoading && !Directory.Exists(store.ImportDirectory) &&
                            scaleReads == 1 && supportReads == 1 && metadataReads == 1,
                            "Submission did not return with a held worker and one snapshot per unique prefab");
                        metadata.name = "Changed after submit"; scale.x = 3; support = false; level = 1;
                        hold = false; release.Set();
                        Require(System.Threading.SpinWait.SpinUntil(() => { store.CatalogAll(); return !store.CatalogLoading; }, 10000) &&
                            store.ImportError == null, store.ImportError ?? "Submission timeout");
                        var result = ExternalBlueprintLibrary.LoadFile(store.ImportResultPath).Single();
                        Require(result.parts[0].materialSeed == 1234 && result.parts[0].baseScale.Value.x == 2 &&
                            result.parts.All(part => part.metadata.name == "Snapshot wall") &&
                            !ReferenceEquals(result.parts[0].metadata, result.parts[1].metadata) &&
                            raw.Documents[0].parts.All(part => part.metadata == null) && raw.Documents[0].parts[0].materialSeed == null &&
                            File.ReadAllBytes(source).SequenceEqual(original), "Submission lost frozen values or mutated source/metadata");

                        // Already-normalized documents take PrepareForHost's identity return path.
                        string nativeSource = Path.Combine(folder, "normalized.buildworks.json");
                        File.WriteAllBytes(nativeSource, BlueprintExchangeFormat.Encode(ExternalBlueprintLibrary.DetachedCopy(result)));
                        var normalized = CompositeBlueprintStore.ReviewImportFile(nativeSource, Hash);
                        metadata.name = "Fresh submission";
                        Require(store.TryImportReviewed(normalized, false, false, Hash, out error), error);
                        Require(System.Threading.SpinWait.SpinUntil(() => { store.CatalogAll(); return !store.CatalogLoading; }, 10000) &&
                            store.ImportError == null, store.ImportError ?? "Normalized submission timeout");
                        Require(ExternalBlueprintLibrary.LoadFile(store.ImportResultPath).Single().parts.All(part => part.metadata.name == "Fresh submission") &&
                            normalized.Documents[0].parts.All(part => part.metadata.name == "Snapshot wall"),
                            "Identity normalization mutated the review or reused stale metadata");
                    }
                    finally { hold = false; release.Set(); }
                }
                var invalid = new CompositeBlueprintStore(Path.Combine(folder, "invalid", "library.json"));
                invalid.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 0, y = 1, z = 1 },
                    prefabHash: Hash, worldLevel: () => 0, supportsMaterialSeed: _ => true);
                Require(invalid.TryImportReviewed(raw, false, false, Hash, out string failure), failure);
                Require(System.Threading.SpinWait.SpinUntil(() => { invalid.CatalogAll(); return !invalid.CatalogLoading; }, 10000) &&
                    invalid.ImportError?.Contains("scale_range") == true && !Directory.Exists(invalid.ImportDirectory) &&
                    File.ReadAllBytes(source).SequenceEqual(original), "Worker normalization failure published or changed source");
                Console.WriteLine("PASS background submission: held worker, unique caller snapshots, mutable metadata isolation, source preservation and fresh normalized import");
            }
            finally { Directory.Delete(folder, true); }
        }

        private static string Package(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            { write(writer); writer.Flush(); return Convert.ToBase64String(stream.ToArray()); }
        }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
