using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class UnboundSourceChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            const string simple = "AAHqcqjB";
            string rich;
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, new UTF8Encoding(false, true), true))
                { writer.Write((ushort)64); writer.Write(-1045925142); writer.Write((byte)1); writer.Write(17); writer.Write("second row ♞" + new string('x', 40000)); }
                rich = Convert.ToBase64String(bytes.ToArray());
            }
            Check(rich.Length > 32768, "Large native packet control missing");
            string row(string packet) => "wood_floor|1|0|2|0|0|0|1|" + packet;
            string raw = "\uFEFFvtbp 2\r\n#terrain|1|-1.000\r\n\r\n" + row(simple) + "\n\n" + row(rich);
            var evidence = new BlueprintSourceDocument(UnboundBlueprint.Format, "sample.vtbp", raw);
            Check(evidence.Unbound.Pieces.Count == 2 && evidence.HasPartAt(4, "wood_floor") && evidence.HasPartAt(6, "wood_floor") &&
                evidence.PartRow(4) == row(simple) && evidence.HasPlacementFrame && evidence.HasUnsupportedSections(true) &&
                evidence.OriginalBytes().SequenceEqual(new UTF8Encoding(false, true).GetBytes(raw)), "Source rows/terrain/original bytes");
            var source = new CompositeBlueprintStore.Blueprint { id = Guid.NewGuid().ToString("N"), name = "Unbound source", category = "Default", prefabName = "wood_floor", importSource = "synthetic.vtbp", importPartCount = 2 };
            source.sourceDocuments = new List<CompositeBlueprintStore.SourceDocument> { CompositeBlueprintStore.SourceDocument.FromGeometry(evidence) };
            foreach (var piece in evidence.Unbound.Pieces)
                source.parts.Add(new CompositeBlueprintStore.Part {
                    stableId = "part" + piece.Line, prefabName = piece.PrefabName, displayName = piece.PrefabName,
                    position = new CompositeBlueprintStore.VectorData { x = (float)piece.Position.X, y = (float)piece.Position.Y, z = (float)piece.Position.Z },
                    rotation = new CompositeBlueprintStore.QuaternionData { x = 0, y = 0, z = 0, w = 1 },
                    sourceDocumentId = evidence.Id, sourceLine = piece.Line, sourceFrameApplied = true,
                    foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(new BlueprintForeignPartData(UnboundBlueprint.Format, piece.PrefabName, piece.ZdoData, null)) });
            Check(CompositeBlueprintStore.Valid(source), "Initial source document invalid");
            Editor(source);
            string path = Path.Combine(directory, "library.json"); var store = new CompositeBlueprintStore(path);
            Check(store.TrySaveDocument(source.name, source.category, source.parts, source.groups, source.anchors,
                null, null, source.supports, null, out var saved, out string error, sourceDocuments: source.sourceDocuments), error);
            byte[] portable = BlueprintExchangeFormat.Encode(saved);
            var decoded = BlueprintExchangeFormat.DecodeWithVersion(Encoding.UTF8.GetString(portable), out int version);
            var reopened = new CompositeBlueprintStore(path).All().Single();
            Check(version == 24 && File.ReadAllText(path).Contains("\"version\":36") &&
                portable.SequenceEqual(BlueprintExchangeFormat.Encode(decoded)) && portable.SequenceEqual(BlueprintExchangeFormat.Encode(reopened)),
                "Native36/portable24 complete fields differ");
            foreach (var copy in new[] { decoded, reopened, source.ImportCopy(), ExternalBlueprintLibrary.DetachedCopy(source) })
            {
                Check(copy.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(evidence.OriginalBytes()) &&
                    copy.parts.Select(p => p.sourceLine).SequenceEqual(new[] { 4, 6 }) && copy.parts.All(p => p.sourceRecord == 0 && p.sourceFrameApplied) &&
                    copy.parts[0].foreignData.zdo == simple && copy.parts[1].foreignData.zdo == rich &&
                    CompositeBlueprintStore.HasForeignData(copy), "Raw source/state/addresses/blocker lost on copy/reload");
                Editor(copy);
            }
            var edited = source.ImportCopy(); edited.parts[0].position.x = 12;
            Check(CompositeBlueprintStore.Valid(edited), "Source binding wrongly forbids geometry editing"); Editor(edited);
            int refused = 0;
            void Bad(Action<CompositeBlueprintStore.Blueprint> mutate)
            {
                var copy = source.ImportCopy(); Check(CompositeBlueprintStore.Valid(copy), "Warm source setup failed"); mutate(copy);
                Check(!CompositeBlueprintStore.Valid(copy), "Invalid store binding accepted");
                bool rejected = false; try { Editor(copy); } catch (ArgumentException) { rejected = true; }
                Check(rejected, "Invalid editor binding accepted"); ++refused;
            }
            Bad(b => b.parts[0].foreignData.zdo = rich);
            Bad(b => b.parts[0].foreignData = null);
            Bad(b => b.parts[0].foreignData.format = "blueprint");
            Bad(b => b.parts[0].foreignData.info = "wood_wall");
            Bad(b => b.parts[0].foreignData.info = null);
            Bad(b => b.parts[0].foreignData.chance = "1");
            Bad(b => b.parts[0].foreignData.contentsCleared = true);
            Bad(b => b.parts[0].foreignData.zdo = null);
            Bad(b => b.parts[0].sourceLine = 6);
            Bad(b => b.parts[0].sourceLine = 3);
            Bad(b => b.parts[0].sourceRecord = 1);
            Bad(b => b.parts[0].sourceDocumentId = "missing");
            Bad(b => { b.parts[0].sourceDocumentId = null; b.parts[0].sourceLine = 0; b.parts[0].sourceFrameApplied = false; });
            Bad(b => b.sourceDocuments[0].id = "missing");
            Bad(b => b.sourceDocuments[0].name = "other.vtbp");
            Bad(b => b.sourceDocuments[0].text += "\n");
            Bad(b => b.sourceDocuments[0].format = "blueprint");
            string json = Encoding.UTF8.GetString(portable), native = File.ReadAllText(path);
            for (int old = 1; old < 24; ++old)
            {
                bool rejected = false;
                try { BlueprintExchangeFormat.Decode(json.Replace("\"version\":24", "\"version\":" + old)); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Unbound entered old portable " + old); ++refused;
            }
            for (int old = 1; old < 36; ++old)
            {
                string oldPath = Path.Combine(directory, "old" + old + ".json"), text = native.Replace("\"version\":36", "\"version\":" + old);
                File.WriteAllText(oldPath, text); var blocked = new CompositeBlueprintStore(oldPath);
                Check(blocked.All().Count == 0 && !blocked.TryAddCategory("blocked", out _) && File.ReadAllText(oldPath) == text,
                    "Unbound entered/overwrote old native " + old); ++refused;
            }
            Check(BlueprintExchangeFormat.HasUnboundData(new CompositeBlueprintStore.Blueprint { sourceDocuments = source.sourceDocuments }) &&
                BlueprintExchangeFormat.HasUnboundData(new CompositeBlueprintStore.Blueprint { parts = source.parts }) &&
                !BlueprintExchangeFormat.HasUnboundData(new CompositeBlueprintStore.Blueprint()), "Source-only/detached dialect version detection");
            // Source-only and foreign-only raw controls reach the content version gate before model validation.
            string sourceOnly = json.Replace("\"foreignData\":{\"format\":\"" + UnboundBlueprint.Format + "\"", "\"foreignData\":{\"format\":\"blueprint\"");
            string foreignOnly = json.Replace("\"id\":\"" + evidence.Id + "\",\"format\":\"" + UnboundBlueprint.Format + "\"", "\"id\":\"" + evidence.Id + "\",\"format\":\"blueprint\"");
            foreach (string control in new[] { sourceOnly, foreignOnly })
            {
                Check(control != json, "Dialect control did not change JSON");
                bool rejected = false; try { BlueprintExchangeFormat.Decode(control.Replace("\"version\":24", "\"version\":23")); }
                catch (InvalidDataException ex) { rejected = ex.Message == "$buildworks_store.unknown_version"; }
                Check(rejected, "Old portable content gate missed source or detached state"); ++refused;
            }
            foreach (string control in new[] {
                json.Replace("\"sourceLine\":4", "\"sourceLine\":null"),
                json.Replace("\"sourceLine\":4", "\"sourceLine\":4,\"sourceRecord\":null"),
                json.Replace("\"sourceLine\":4", "\"sourceLine\":4,\"sourceRecord\":-1"),
                json.Replace("\"info\":\"wood_floor\"", "\"info\":null"),
                json.Replace("\"format\":\"" + UnboundBlueprint.Format + "\"", "\"format\":null"),
                json.Replace("\"sourceFrameApplied\":true", "\"sourceFrameApplied\":null") })
            {
                Check(control != json, "Malformed JSON control did not change data");
                bool rejected = false; try { BlueprintExchangeFormat.Decode(control); } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Explicit null or invalid state binding accepted"); ++refused;
            }
            Console.WriteLine("PASS Unbound source/native36/portable24: full raw/state/terrain/copy/reopen/editor binding;" + refused +
                " malformed/downgrade controls. Synthetic persisted source only; library file import/semantic conversion/game NOT RUN.");
        }
        internal static void Editor(CompositeBlueprintStore.Blueprint source) => new BlueprintEditorDocument(null, source.name, source.category,
            source.parts.Select(p => new BlueprintEditorPart(p.stableId, p.prefabName, p.displayName,
                new Point3(p.position.x, p.position.y, p.position.z), new Rotation3(p.rotation.x, p.rotation.y, p.rotation.z, p.rotation.w),
                sourceDocumentId: p.sourceDocumentId, sourceLine: p.sourceLine, sourceRecord: p.sourceRecord,
                sourceFrameApplied: p.sourceFrameApplied, foreignData: p.foreignData?.ToGeometry(),
                scale: new Point3(p.scale.x, p.scale.y, p.scale.z), signText: p.signText, materialSeed: p.materialSeed,
                scaleIsAbsolute: p.scaleIsAbsolute, baseScale: p.baseScale.HasValue
                    ? new Point3(p.baseScale.Value.x, p.baseScale.Value.y, p.baseScale.Value.z) : (Point3?)null,
                metadata: p.metadata?.ToGeometry(), originalPrefabName: p.originalPrefabName)),
            sourceDocuments: source.sourceDocuments.Select(s => s.ToGeometry()));
        private static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
    }
}
