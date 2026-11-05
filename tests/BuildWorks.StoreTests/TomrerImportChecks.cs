using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class TomrerImportChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var fresh = RoundTrip(directory, "bw-qa-tomrer-t01-20261026-130535.blueprint", 1,
                "E876F574BE77275D0B3401B4C6A488B93576450B0B5F99CBE0E421DF3BE71397");
            Require(fresh.parts[0].prefabName == "woodwall" && fresh.parts[0].sourceLine == 3 &&
                fresh.parts[0].position.x == 0 && fresh.parts[0].rotation.w == 1 &&
                fresh.parts[0].scale.x == 1 && fresh.parts[0].scale.y == 1 && fresh.parts[0].scale.z == 1 &&
                fresh.parts[0].signText == null && fresh.parts[0].foreignData == null,
                "Fresh producer's nine fields acquired invented optional state or lost defaults");
            var tail = RoundTrip(directory, "bw-qa-tomrer-t03-tail-20261026-130535.blueprint", 2,
                "D16E22EC76931AD7F8F840712124450C74AA14BE6285D623D5803F7D3A493FC6");
            Require(tail.parts[1].signText == "BuildWorks: Привет, Ostrix!" && tail.parts[1].sourceLine == 5 &&
                tail.sourceDocuments[0].text.Contains(";1;1;1;"), "Producer's fourteen-field re-export lost sign/tail");
            var workshop = RoundTrip(directory, "workshop.blueprint", 16,
                "F260F307861C26843743A5E0376DF058976C770ED70D65AD0E3A9AED14D6ADF2");
            Require(workshop.sourceDocuments[0].text.Contains("#Icon:piece_workbench") &&
                !workshop.sourceDocuments[0].ToGeometry().HasOpaqueSections,
                "Known descriptive Icon was lost or treated as unknown executable state");

            const string nine = "sign;;0;0;0;0;0;0;1";
            var noInfo = Parse(nine);
            var blank = Parse(nine + ";\"\"");
            Require(noInfo.importError == null && noInfo.parts.Single().signText == null &&
                blank.importError == null && blank.parts.Single().signText == null &&
                noInfo.sourceDocuments[0].text != blank.sourceDocuments[0].text,
                "Absent/empty legacy Info changed its existing semantics or exact source");
            var opaqueTail = Parse(nine + ";\"Text\";1;2;3;opaque;0.5");
            Require(opaqueTail.importError == null && opaqueTail.parts[0].signText == "Text" &&
                opaqueTail.parts[0].foreignData.zdo == "opaque" && opaqueTail.parts[0].foreignData.chance == "0.5" &&
                opaqueTail.parts[0].scale.y == 2, "Optional tail was discarded or rewritten");

            foreach (string invalid in new[] { "sign;;0;0;0;0;0;0", nine.Replace(";0;0;0;1", ";0;0;0;0"),
                nine.Replace(";;0;", ";;NaN;"), nine.Replace(";;0;", ";;Infinity;"),
                nine.Replace(";;0;", ";;513;"), nine.Replace("sign;", "../sign;"),
                nine + ";\"\";1", nine + ";\"\";1;1", nine + ";\"\";1;0;1",
                nine + ";\"\";1;1;1;opaque;0.5;extra" })
            {
                var rejected = Parse(nine + "\n" + invalid);
                Require(rejected.importError != null && rejected.parts.Count == 0 && rejected.importPartCount == 2,
                    "Malformed mandatory fields/partial scale exposed a partial construction: " + invalid);
            }
            string header = "#Name:Header\n#Icon:piece_workbench\n#Pieces\n";
            var icon = ExternalBlueprintLibrary.Parse(header + nine, "icon.blueprint");
            var future = ExternalBlueprintLibrary.Parse(header + nine + "\n#Future\n" + nine + "\n#Pieces\n" + nine,
                "future.blueprint");
            Require(icon.importError == null && !icon.sourceDocuments[0].ToGeometry().HasOpaqueSections &&
                icon.sourceDocuments[0].ToGeometry().HasPartAt(4, "sign") &&
                future.importError == null && future.parts.Count == 2 &&
                future.sourceDocuments[0].ToGeometry().HasOpaqueSections &&
                !future.sourceDocuments[0].ToGeometry().HasPartAt(6, "sign") &&
                future.sourceDocuments[0].ToGeometry().HasPartAt(8, "sign"),
                "Icon handling weakened unknown-section/source-row boundaries");
            var iconInUnknown = ExternalBlueprintLibrary.Parse(header + nine + "\n#Future\n#Icon:woodwall\n" + nine,
                "unknown-icon.blueprint");
            var vbuildIcon = ExternalBlueprintLibrary.Parse("#Name:Vbuild\n#Icon:woodwall\n#Pieces\nwoodwall;0;0;0;1;0;0;0",
                "icon.vbuild");
            Require(iconInUnknown.parts.Count == 1 && iconInUnknown.sourceDocuments[0].ToGeometry().HasOpaqueSections &&
                vbuildIcon.importError == null && vbuildIcon.sourceDocuments[0].ToGeometry().HasOpaqueSections,
                "Icon resumed an unknown section or changed the vbuild dialect");
            var iconPrepared = ExternalBlueprintLibrary.PrepareForHost(icon,
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
            using (var json = JsonReaderWriterFactory.CreateJsonReader(BlueprintExchangeFormat.Encode(
                ExternalBlueprintLibrary.DetachedCopy(iconPrepared)),
                System.Xml.XmlDictionaryReaderQuotas.Max))
            {
                var xml = new System.Xml.XmlDocument(); xml.Load(json);
                Require(xml.SelectSingleNode("/root/requiredPrefabIds").InnerText == "sign",
                    "Descriptive icon became a prefab dependency");
            }
            Console.WriteLine("PASS Tomrer real T01 nine/T03 fourteen/author ten fields: reviewed archive, raw source, native/portable reopen; Icon inert; absent/blank Info; mandatory negatives atomic. No game proof.");
        }

        private static CompositeBlueprintStore.Blueprint Parse(string row) =>
            ExternalBlueprintLibrary.Parse("#Name:Tomrer\n#Pieces\n" + row, "tomrer.blueprint");

        private static CompositeBlueprintStore.Blueprint RoundTrip(string directory, string name, int parts, string expectedSha)
        {
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "tomrer", name);
            byte[] bytes = File.ReadAllBytes(file);
            using (var hash = SHA256.Create())
                Require(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "") == expectedSha,
                    "Expected exact pinned producer bytes: " + name);
            var source = ExternalBlueprintLibrary.LoadFile(file).Single();
            Require(source.importError == null && source.parts.Count == parts, name + ": " + source.importError);
            string raw = Encoding.UTF8.GetString(bytes);
            Require(source.sourceDocuments.Single().text == raw && source.sourceDocuments[0].name == name,
                "Source bytes/name not retained: " + name);
            foreach (var part in source.parts)
                Require(source.sourceDocuments[0].ToGeometry().HasPartAt(part.sourceLine, part.prefabName),
                    "Physical source row detached: " + name);

            string folder = Path.Combine(directory, Path.GetFileNameWithoutExtension(name));
            Directory.CreateDirectory(folder);
            string library = Path.Combine(folder, "library.json");
            var store = new CompositeBlueprintStore(library);
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0);
            var review = CompositeBlueprintStore.ReviewImportFile(file, ForeignZdoChecks.Hash, 0);
            review = store.PrepareImportReview(review, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Require(!Directory.Exists(store.ImportDirectory) && review.Documents.Single().parts.Count == parts,
                "Review mutated library or lost producer parts");
            Require(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error), error);
            var task = (Task)typeof(CompositeBlueprintStore).GetField("importTask",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(store);
            Require(task != null && task.Wait(60000), "Import worker timeout");
            for (int i = 0; i < 1000; ++i)
            { store.CatalogAll(); if (!store.CatalogLoading) break; System.Threading.Thread.Sleep(10); }
            Require(!store.CatalogLoading && store.ImportError == null, "Import catalog did not settle");
            var imported = store.CatalogAll().Single();
            Require(imported.parts.Count == parts && imported.sourceDocuments.Single().text == raw,
                "Imported archive lost data/source");
            using (var zip = ZipFile.OpenRead(store.ImportResultPath))
            using (var copy = new MemoryStream())
            {
                using (var input = zip.GetEntry("original/source.bin").Open()) input.CopyTo(copy);
                Require(copy.ToArray().SequenceEqual(bytes), "Archived original changed");
            }
            Require(store.TryGetOrCreatePlacementCopy(imported, out var saved, out error), error);
            byte[] portable = BlueprintExchangeFormat.Encode(saved);
            var reopened = new CompositeBlueprintStore(library).All().Single();
            var decoded = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(portable));
            var reimported = ExternalBlueprintLibrary.Parse(Encoding.UTF8.GetString(portable), "copy.buildworks.json");
            reimported.id = saved.id;
            Require(portable.SequenceEqual(BlueprintExchangeFormat.Encode(reopened)) &&
                portable.SequenceEqual(BlueprintExchangeFormat.Encode(decoded)) &&
                portable.SequenceEqual(BlueprintExchangeFormat.Encode(reimported)) &&
                saved.sourceDocuments.Single().text == raw && File.ReadAllBytes(file).SequenceEqual(bytes),
                "Native/portable reopen/reimport changed fields/dependencies/original");
            return source;
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
