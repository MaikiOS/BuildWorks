using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ValidationReadChecks
    {
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            const string text = "#Name:Validation\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1";
            string loose = Path.Combine(folder, "loose.blueprint");
            File.WriteAllText(loose, text);
            File.WriteAllText(Path.Combine(folder, "legacy.vbuild"), "woodwall;0;0;0;1;2;0;0");
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==");
            string archive = Path.Combine(folder, "paired.zip");
            void WriteArchive(bool preview, string second = text)
            {
                using (var file = File.Create(archive))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                {
                    using (var writer = new StreamWriter(zip.CreateEntry("same.blueprint").Open())) writer.Write(text);
                    using (var writer = new StreamWriter(zip.CreateEntry("same.blueprint").Open())) writer.Write(second);
                    if (preview)
                        using (var image = zip.CreateEntry("same.png").Open()) image.Write(png, 0, png.Length);
                }
            }
            var reads = new Dictionary<string, CompositeBlueprintStore.Blueprint>(StringComparer.Ordinal);
            WriteArchive(true);
            var first = ExternalBlueprintLibrary.Load(folder, validationReads: reads);
            Equal(ExternalBlueprintLibrary.Load(folder), first);
            var secondRead = ExternalBlueprintLibrary.Load(folder, validationReads: reads);
            Equal(first, secondRead);
            for (int i = 0; i < first.Count; ++i)
                Require(!ReferenceEquals(first[i], secondRead[i]) && ReferenceEquals(first[i].parts, secondRead[i].parts),
                    "Unchanged foreign parsing was not reused with separate result envelopes");
            Require(first.Count(item => item.importSource.StartsWith("paired.zip/")) == 2,
                "Duplicate ZIP member multiplicity changed");
            WriteArchive(false);
            var withoutPreview = ExternalBlueprintLibrary.Load(folder, validationReads: reads);
            Equal(ExternalBlueprintLibrary.Load(folder), withoutPreview);
            Require(first.Where(item => item.importSource.StartsWith("paired.zip/")).All(item => item.importPreview != null) &&
                withoutPreview.All(item => item.importPreview == null), "Preview leaked across read envelopes");
            string previousId = withoutPreview.Single(item => item.importSource == "loose.blueprint").id;
            File.WriteAllText(loose, text.Replace(";;0;0;0;", ";;1;0;0;"));
            var changed = ExternalBlueprintLibrary.Load(folder, validationReads: reads);
            Equal(ExternalBlueprintLibrary.Load(folder), changed);
            Require(changed.Single(item => item.importSource == "loose.blueprint").id != previousId,
                "Changed file reused a stale parse");
            WriteArchive(true, text.Replace(";;0;0;0;", ";;2;0;0;"));
            Equal(ExternalBlueprintLibrary.Load(folder), ExternalBlueprintLibrary.Load(folder, validationReads: reads));

            // Exercise the same private reader with a tiny shared budget, without allocating128MiB.
            List<CompositeBlueprintStore.Blueprint> Limited(Dictionary<string, CompositeBlueprintStore.Blueprint> cache)
            {
                var result = new List<CompositeBlueprintStore.Blueprint>();
                object[] args = { archive, result, (long)Encoding.UTF8.GetByteCount(text) + png.Length, null, cache };
                typeof(ExternalBlueprintLibrary).GetMethod("AppendFile", BindingFlags.NonPublic | BindingFlags.Static)
                    .Invoke(null, args);
                return result;
            }
            WriteArchive(true);
            ExternalBlueprintLibrary.Load(folder, validationReads: reads);
            Equal(Limited(null), Limited(reads));
            Require(Limited(reads).Any(item => item.importError != null), "Shared byte budget bypassed by a cached parse");
            byte[] invalid = { 255, 254, 255 };
            File.WriteAllBytes(loose, invalid);
            Equal(ExternalBlueprintLibrary.Load(folder), ExternalBlueprintLibrary.Load(folder, validationReads: reads));
            Require(ExternalBlueprintLibrary.Load(folder, validationReads: reads)
                .Single(item => item.importSource == "loose.blueprint").importError != null, "Invalid UTF8 hit a cached parse");
            Console.WriteLine("PASS per-worker validation reads: exact-text reuse, fresh result/preview, duplicate ZIP members, changed files/member text, shared budget and strict UTF8 parity");
        }

        private static void Equal(IReadOnlyList<CompositeBlueprintStore.Blueprint> expected,
            IReadOnlyList<CompositeBlueprintStore.Blueprint> actual)
        {
            Require(expected.Count == actual.Count, "Validation read count changed");
            for (int i = 0; i < expected.Count; ++i)
            {
                var a = expected[i]; var b = actual[i];
                byte[] Serialize(CompositeBlueprintStore.Blueprint value)
                {
                    using (var bytes = new MemoryStream())
                    { new DataContractJsonSerializer(typeof(CompositeBlueprintStore.Blueprint)).WriteObject(bytes, value); return bytes.ToArray(); }
                }
                Require(Serialize(a).SequenceEqual(Serialize(b)) && a.importSource == b.importSource &&
                    a.importError == b.importError && a.importPartCount == b.importPartCount &&
                    a.importAbsoluteScale == b.importAbsoluteScale &&
                    (a.importPreview == null ? b.importPreview == null : b.importPreview != null && a.importPreview.SequenceEqual(b.importPreview)),
                    "Validation read differs from uncached reader");
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
