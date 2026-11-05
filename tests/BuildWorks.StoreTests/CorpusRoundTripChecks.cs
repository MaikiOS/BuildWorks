using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class CorpusRoundTripChecks
    {
        internal static void RunSaved(string libraryPath, string blueprintId)
        {
            byte[] original = File.ReadAllBytes(libraryPath);
            var saved = new CompositeBlueprintStore(libraryPath).All().Single(b => b.id == blueprintId);
            byte[] portable = BlueprintExchangeFormat.Encode(saved);
            string text = Encoding.UTF8.GetString(portable);
            Require(portable.SequenceEqual(BlueprintExchangeFormat.Encode(BlueprintExchangeFormat.Decode(text))),
                "Captured document changed during portable roundtrip");
            var reimported = ExternalBlueprintLibrary.Parse(text, "captured.buildworks.json");
            Require(reimported.importError == null, "Captured document reimport failed: " + reimported.importError);
            reimported.id = saved.id;
            Require(portable.SequenceEqual(BlueprintExchangeFormat.Encode(reimported)),
                "Captured document reimport changed fields or dependencies");
            Require(original.SequenceEqual(File.ReadAllBytes(libraryPath)), "Source library changed during read-only check");
            Console.WriteLine("PASS saved capture: " + saved.parts.Count +
                " parts; actual store reopen/portable/full-field reimport; source bytes unchanged. No game reload or network proof.");
        }

        internal static void Run(string archivePath, string directory)
        {
            string sourceHash = Hash(archivePath);
            int expected = sourceHash == "36C5A04DF80294C6B49B21C916D808FCE60698B17479BFAB7C95F850BE12C707" ? 353 :
                sourceHash == "4A6E6A53F246B5CF4C7EA9472F726D9F35687BCE978F28AB47A020AEE759DD88" ? 209 :
                sourceHash == "C8D3E1C2938FFA18A687B9074AA517796D1A7C065ED26DEDB88272B5440A2E62" ? 4 : 0;
            int expectedParsed = expected == 209 ? 202 : expected;
            Require(expected != 0, "Unrecognized test corpus");
            int total = 0, parsed = 0, converted = 0, roundtrips = 0;
            long readBytes = 0;
            using (var archive = ZipFile.OpenRead(archivePath))
            foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase) ||
                e.FullName.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase)))
            {
                ++total;
                Require(total <= expected && entry.Length <= BlueprintExchangeFormat.MaximumBytes &&
                    (readBytes += entry.Length) <= 512L * 1024 * 1024, "Corpus size limit");
                string text;
                using (var reader = new StreamReader(entry.Open(), Encoding.UTF8, true)) text = reader.ReadToEnd();
                var source = ExternalBlueprintLibrary.Parse(text, entry.FullName);
                if (source.importError != null)
                { Console.WriteLine("PARSE REFUSAL " + entry.FullName + " " + source.importError); continue; }
                ++parsed;
                if (expected == 4)
                {
                    int expectedParts = Path.GetFileName(entry.FullName) switch
                    {
                        "PuP_terminator.blueprint" => 13163,
                        "sheepshank-catharishallv3(1).blueprint" => 8300,
                        "sheepshank-gjallhuntertowerv3.blueprint" => 3636,
                        "ugm-meadowsbasefor10players.blueprint" => 9185,
                        _ => 0
                    };
                    Require(source.parts.Count == expectedParts, entry.FullName + " author record count changed");
                }
                // Unit prefab bases are an inert probe, not evidence that the real prefab exists or has this scale.
                var prepared = ExternalBlueprintLibrary.PrepareForHost(source,
                    _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, ForeignZdoChecks.Hash, 0);
                if (prepared.importError != null)
                { Console.WriteLine("UNIT-BASE REFUSAL " + entry.FullName + " " + prepared.importError); continue; }
                ++converted;
                string path = Path.Combine(directory, total + ".json");
                var store = new CompositeBlueprintStore(path);
                Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), entry.FullName + " " + error);
                Require(saved.parts.Count == prepared.parts.Count && saved.sourceDocuments.Select(s => s.text)
                    .SequenceEqual(prepared.sourceDocuments.Select(s => s.text)), entry.FullName + " source/parts lost during copy");
                // Copies get new IDs; every other part field must survive the actual save path unchanged.
                var ids = saved.parts.Select(p => p.stableId).ToArray();
                try
                {
                    for (int i = 0; i < ids.Length; ++i) saved.parts[i].stableId = prepared.parts[i].stableId;
                    Require(Parts(prepared).SequenceEqual(Parts(saved)), entry.FullName + " part fields changed during copy");
                }
                finally { for (int i = 0; i < ids.Length; ++i) saved.parts[i].stableId = ids[i]; }
                byte[] portable = BlueprintExchangeFormat.Encode(saved);
                var reopened = new CompositeBlueprintStore(path).All().Single();
                Require(portable.SequenceEqual(BlueprintExchangeFormat.Encode(reopened)), entry.FullName + " native reopen changed data");
                var decoded = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(portable));
                Require(portable.SequenceEqual(BlueprintExchangeFormat.Encode(decoded)), entry.FullName + " portable roundtrip changed data");
                var reimported = ExternalBlueprintLibrary.Parse(Encoding.UTF8.GetString(portable), "copy.buildworks.json");
                Require(reimported.importError == null && reimported.parts.Count == saved.parts.Count &&
                    reimported.sourceDocuments.Select(s => s.text).SequenceEqual(saved.sourceDocuments.Select(s => s.text)),
                    entry.FullName + " portable reimport lost source/parts");
                // Catalog identity changes on import; every portable field and dependency must remain exact.
                reimported.id = saved.id;
                Require(portable.SequenceEqual(BlueprintExchangeFormat.Encode(reimported)),
                    entry.FullName + " portable reimport changed document fields or dependencies");
                ++roundtrips;
                File.Delete(path);
                if (roundtrips % 25 == 0) Console.WriteLine("ROUNDTRIP " + roundtrips + "/" + expected);
            }
            Require(total == expected && parsed == expectedParsed && converted == parsed &&
                roundtrips == converted && Hash(archivePath) == sourceHash, "Corpus changed, regressed or incomplete");
            Console.WriteLine("PASS corpus pipeline: " + total + " files; " + parsed + " parsed; " + converted +
                " unit-base converted; " + roundtrips + " native reopen/portable/full-field reimport; archive SHA unchanged. " +
                "No actual prefab, editor, placement, reload or network proof.");
        }

        private static byte[] Parts(CompositeBlueprintStore.Blueprint blueprint)
        {
            using (var output = new MemoryStream())
            {
                new DataContractJsonSerializer(blueprint.parts.GetType()).WriteObject(output, blueprint.parts);
                return output.ToArray();
            }
        }

        private static string Hash(string path)
        {
            using (var source = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(source)).Replace("-", "");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
