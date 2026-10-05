using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--benchmark") return BenchmarkExchange(int.Parse(args[1]));
            string directory = Path.Combine(
                Path.GetTempPath(), "BuildWorks.StoreTests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                RoundTripV9(Path.Combine(directory, "roundtrip.json"));
                PrimaryPartRoundTrip(Path.Combine(directory, "primary.json"));
                PrimaryGroupRoundTrip(Path.Combine(directory, "primary-group.json"));
                MigrateV5Scale(Path.Combine(directory, "migration-v5.json"));
                RejectInvalidScale(Path.Combine(directory, "scale-invalid.json"));
                ScaleUpdateAndRollback(Path.Combine(directory, "scale-update.json"));
                MetadataRoundTrip(Path.Combine(directory, "metadata.json"));
                MigrateV3(Path.Combine(directory, "migration.json"));
                RejectGroupCycle(Path.Combine(directory, "cycle.json"));
                FailedWritePreservesOriginal(Path.Combine(directory, "failure.json"));
                ExternalImports(directory);
                ExchangeRoundTrip(Path.Combine(directory, "exchange-library.json"));
                if (args.Length > 0) InspectActualLibrary(args[0]);
                Console.WriteLine("All BuildWorks store tests passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static void ExternalImports(string directory)
        {
            const string rows = "wall;BuildingWorkbench;1;2;3;0;0;0;1;\"\";1;2;1\n" +
                "beam;BuildingWorkbench;-4;5;6;0;.7071068;0;.7071068;\"\";1;1;1\n";
            string source = "#Name:Example\n#Category:Building\n#Terrain\nsquare;NaN;malicious;ignored\n#Pieces\n" + rows;
            var parsed = ExternalBlueprintLibrary.Parse(source, "example.blueprint");
            Require(parsed.importError == null && parsed.parts.Count == 2 && parsed.IsExternal, "Basic blueprint/terrain separation failed");
            Require(parsed.parts[0].position.x == 1 && parsed.parts[0].position.y == 2 && parsed.parts[0].position.z == 3 &&
                parsed.parts[0].scale.y == 2 && Math.Abs(parsed.parts[1].rotation.y - .7071068f) < .000001f, "Position/rotation/scale ordering changed");
            var vbuild = ExternalBlueprintLibrary.Parse("wall;0;0;0;1;1;2;3\nbeam;0;0;0;1;-4;5;6", "sample.vbuild");
            Require(vbuild.importError == null && vbuild.parts[1].position.z == 6 && vbuild.parts[0].rotation.w == 1, "vbuild ordering");
            var whitespace = ExternalBlueprintLibrary.Parse("wall 0 0 0 1 1 2 3\nbeam\t0 0 0 1 -4 5 6", "buildshare.vbuild");
            Require(whitespace.importError == null && whitespace.parts[0].position.y == 2, "BuildShare whitespace dialect");
            Require(ExternalBlueprintLibrary.Parse("#Pieces\n" + rows, "no-headers.blueprint").importError == null, "Header-light source");
            var copyA = ExternalBlueprintLibrary.DetachedCopy(parsed);
            var copyB = ExternalBlueprintLibrary.DetachedCopy(parsed);
            Require(copyA.id != copyB.id && copyA.parts[0].stableId != copyB.parts[0].stableId &&
                copyA.parts[0].stableId != parsed.parts[0].stableId, "Duplicate conversion identities collide");
            foreach (string invalid in new[] {
                source.Replace("1;2;3", "NaN;2;3"), source.Replace("1;2;3", "513;2;3"),
                source.Replace("0;0;0;1;", "0;0;0;0;"), source.Replace("\"\"", "\"inventory\""),
                source.Replace(";1;2;1", ";1;5;1"), source.Replace(";1;2;1", ";1;2;1;zdo"),
                source.Replace(";1;2;1", ";1;2;1;;0.5"), source + "#FutureData\nnew data",
                source.Replace("#Terrain", "#TerrainFuture"),
                "#Pieces\n" + string.Concat(Enumerable.Repeat(rows, CompositeBlueprintStore.MaximumParts / 2 + 1)), "#Pieces\n" + rows.Split('\n')[0]
            })
            {
                var rejected = ExternalBlueprintLibrary.Parse(invalid, "bad.blueprint");
                Require(rejected.importError != null && rejected.parts.Count == 0, "Unsafe/partial source was accepted");
            }
            string folder = Path.Combine(directory, "imports"); Directory.CreateDirectory(folder);
            string original = Path.Combine(folder, "example.blueprint"); File.WriteAllText(original, source);
            string zipPath = Path.Combine(folder, "archive.zip");
            using (var output = File.Create(zipPath))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("../outside.vbuild").Open())) writer.Write("wall;0;0;0;1;1;2;3\nbeam;0;0;0;1;4;5;6");
                using (var writer = new StreamWriter(zip.CreateEntry("never-load.dll").Open())) writer.Write("not code");
            }
            var library = ExternalBlueprintLibrary.Load(folder);
            Require(library.Count == 2 && !File.Exists(Path.Combine(directory, "outside.vbuild")), "Archive paths extracted or DLL treated as blueprint");
            var store = new CompositeBlueprintStore(Path.Combine(directory, "import-copy.json"));
            Require(store.All().Count == 0 && store.CatalogAll().Count == 2 && !File.Exists(Path.Combine(directory, "import-copy.json")), "Browsing sources mutated native store");
            Require(!store.TryDelete(parsed, out _) && File.ReadAllText(original) == source, "Source library is writable");
            Require(store.TrySaveDocument(copyA.name, copyA.category, copyA.parts, copyA.groups, copyA.anchors,
                out CompositeBlueprintStore.Blueprint saved, out string error), error);
            Require(saved.id != parsed.id && !saved.IsExternal, "Imported copy shares source identity");
            var loaded = new CompositeBlueprintStore(Path.Combine(directory, "import-copy.json"));
            Require(loaded.All().Count == 1 && loaded.All()[0].parts[0].scale.y == 2 && loaded.CatalogAll().Count == 3,
                "Native copy cannot be reopened with original sources");
            Require(File.ReadAllText(original) == source, "Import/save rewrote original file");
            Console.WriteLine("PASS read-only .blueprint/.vbuild -> separate native v10 copy, exact poses/scales, terrain exclusion, rejection without partial pieces, inert archive");
        }

        private static int BenchmarkExchange(int count)
        {
            var blueprint = new CompositeBlueprintStore.Blueprint { id = Guid.NewGuid().ToString("N"),
                name = "Benchmark", category = "OTHER", prefabName = "woodwall" };
            for (int i = 0; i < count; ++i)
            {
                var part = Part("part" + i, "woodwall", "Wall", null, true, false, 1);
                part.position = Vector(i % 100, i / 100, 0);
                blueprint.parts.Add(part);
            }
            var clock = System.Diagnostics.Stopwatch.StartNew();
            byte[] bytes = BlueprintExchangeFormat.Encode(blueprint);
            double encode = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            var decoded = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(bytes));
            Require(decoded.parts.Count == count && decoded.parts[count - 1].stableId == "part" + (count - 1), "Large roundtrip truncated");
            Console.WriteLine("parts,json_bytes,encode_ms,strict_decode_ms,managed_live_bytes");
            Console.WriteLine(FormattableString.Invariant($"{count},{bytes.Length},{encode:F3},{clock.Elapsed.TotalMilliseconds:F3},{GC.GetTotalMemory(false)}"));
            return 0;
        }

        private static void InspectActualLibrary(string path)
        {
            int total = 0, supported = 0, blocked = 0;
            using (Stream input = File.OpenRead(path))
            using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
                foreach (ZipArchiveEntry entry in zip.Entries.Where(x => x.FullName.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase)))
                {
                    using (var reader = new StreamReader(entry.Open()))
                    {
                        var parsed = ExternalBlueprintLibrary.Parse(reader.ReadToEnd(), entry.FullName);
                        ++total; if (parsed.importError == null) ++supported; else ++blocked;
                        if (parsed.name == "CoffeeTable") Require(parsed.importError == null && parsed.parts.Count == 4, "Actual CoffeeTable cannot be converted");
                        if (parsed.name == "WoodCouch1") Require(parsed.importError?.Contains("object_data") == true, "Sign text was silently lost");
                    }
                }
            Require(total == 353 && supported > 0 && total == supported + blocked, "Downloaded library coverage");
            Console.WriteLine("PASS actual BiomeBlueprints: " + total + " visible source entries; " + supported + " format-compatible; " + blocked + " explicitly blocked (host prefab availability is separate)");
        }

        private static void ExchangeRoundTrip(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new[] { Part("sign-a", "sign", "Sign", "group-a", false, true, 1),
                Part("wall-b", "woodwall", "Wall", null, true, false, 2) };
            parts[0].signText = "Дом Ostrix\nВход → здесь <color=yellow>текст</color>";
            var groups = new[] { new CompositeBlueprintStore.Group { stableId = "group-a", name = "Group", pivotPartId = "sign-a" } };
            var points = new[] { Vector(2, 3, 4) };
            Require(store.TrySaveDocument("Exchange", "OTHER", parts, groups, Array.Empty<CompositeBlueprintStore.VectorData>(),
                "sign-a", "group-a", out var saved, out string error, points), error);
            var reopened = new CompositeBlueprintStore(path).All()[0];
            Require(reopened.parts[0].signText == parts[0].signText && reopened.customSnapPoints[0].z == 4, "Native v10 fields lost");
            string json = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened));
            var decoded = BlueprintExchangeFormat.Decode(json);
            Require(decoded.parts[0].signText == parts[0].signText && decoded.primaryPartId == "sign-a" &&
                decoded.groups[0].pivotPartId == "sign-a" && decoded.customSnapPoints[0].y == 3, "Portable fields lost");
            var external = ExternalBlueprintLibrary.Parse(json, "roundtrip.buildworks.json");
            Require(external.importError == null && external.IsExternal, "Portable file not available in source catalog");
            Require(external.id != reopened.id && ExternalBlueprintLibrary.Parse(json.Replace("Wall", "Other wall"),
                "roundtrip.buildworks.json").id != external.id && ExternalBlueprintLibrary.Parse(json, "other.buildworks.json").id != external.id,
                "Portable sources collide with native/revised runtime identities");
            var copy = ExternalBlueprintLibrary.DetachedCopy(external);
            Require(copy.groups[0].stableId != "group-a" && copy.parts[0].parentGroupId == copy.groups[0].stableId &&
                copy.groups[0].pivotPartId == copy.parts[0].stableId && copy.primaryPartId == copy.parts[0].stableId &&
                copy.primaryGroupId == copy.groups[0].stableId && copy.parts[0].signText == parts[0].signText &&
                copy.customSnapPoints[0].x == 2 && !copy.parts[0].editorVisible && copy.parts[0].editorLocked, "Detached copy lost hierarchy/data");
            foreach (string invalid in new[] { json.Replace("\"version\":1", "\"version\":999"),
                json.Replace("\"format\":", "\"future\":42,\"format\":"),
                json.Replace("\"version\":1", "\"version\":1,\"version\":1"),
                json.Replace("\"x\":2", "\"x\":null"), json.Replace("\"x\":2", "\"extra\":2,\"x\":2"),
                json.Replace("\"previewZoom\":1,", ""), json.Replace("\"name\":\"Group\"", "\"name\":\" Group \""),
                json.Replace("\"previewZoom\":1", "\"previewZoom\":0") })
                Require(ExternalBlueprintLibrary.Parse(invalid, "bad.buildworks.json").importError != null, "Unknown/invalid portable data accepted");
            var snapSource = ExternalBlueprintLibrary.Parse("#Name:Snap\n#Pieces\nwall;;0;0;0;0;0;0;1;\"\"\nwall;;1;0;0;0;0;0;1;\"\"\n#SnapPoints\n2;3;4", "snap.blueprint");
            Require(snapSource.importError == null && snapSource.customSnapPoints.Count == 1 &&
                ExternalBlueprintLibrary.DetachedCopy(snapSource).customSnapPoints[0].z == 4, "Foreign explicit snap point lost");
            Require(ExternalBlueprintLibrary.Parse("#Pieces\nwall;;0;0;0;0;0;0;1;\"\"\nwall;;1;0;0;0;0;0;0;1;\"\"\n#SnapPoints\nNaN;0;0", "bad.blueprint").importError != null, "Nonfinite snap accepted");
            Require(store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                saved.primaryPartId, saved.primaryGroupId, out error), error);
            Require(new CompositeBlueprintStore(path).All()[0].customSnapPoints.Count == 1, "Legacy save path cleared custom points");
            string original = File.ReadAllText(path);
            File.WriteAllText(path, original.Replace("\"version\":10", "\"future\":123,\"version\":10"));
            var unknown = new CompositeBlueprintStore(path);
            Require(!unknown.TrySave(parts, Array.Empty<CompositeBlueprintStore.VectorData>(), out _, out _) &&
                File.ReadAllText(path).Contains("\"future\":123"), "Unknown native fields silently discarded");
            Console.WriteLine("PASS v10/public v1 roundtrip, sign UTF-8, custom sockets, strict unknown fields, detached hierarchy");
        }

        private static void RoundTripV9(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new List<CompositeBlueprintStore.Part>
            {
                Part("part-a", "woodwall", "Wall A", "group-inner", false, true, 1f),
                Part("part-b", "woodwall", "Wall B", null, true, false, 2f)
            };
            var groups = new List<CompositeBlueprintStore.Group>
            {
                new CompositeBlueprintStore.Group
                {
                    stableId = "group-a",
                    name = "Hidden group",
                    editorVisible = false,
                    editorLocked = true
                },
                new CompositeBlueprintStore.Group
                {
                    stableId = "group-inner",
                    name = "Nested group",
                    parentGroupId = "group-a",
                    pivotPartId = "part-a"
                }
            };
            parts[0].scale = Vector(2f, 2f, 2f);
            var anchors = new List<CompositeBlueprintStore.VectorData>
            {
                Vector(0f, 0f, 0f),
                Vector(2f, 3f, 4f)
            };

            Require(store.TrySaveDocument(
                parts, groups, anchors, out CompositeBlueprintStore.Blueprint saved,
                out string error), error);
            Require(File.ReadAllText(path).Contains("\"version\":10"), "v10 was not written");

            var reopened = new CompositeBlueprintStore(path);
            Equal(1, reopened.All().Count, "blueprint count");
            CompositeBlueprintStore.Blueprint blueprint = reopened.All()[0];
            Equal(saved.id, blueprint.id, "blueprint id");
            Equal(2, blueprint.groups.Count, "group count");
            Equal("group-a", blueprint.groups[0].stableId, "group id");
            Equal(false, blueprint.groups[0].editorVisible, "group visibility");
            Equal(true, blueprint.groups[0].editorLocked, "group lock");
            Equal("group-a", blueprint.groups[1].parentGroupId, "nested group parent");
            Equal("part-a", blueprint.groups[1].pivotPartId, "nested group pivot");
            Equal("part-a", blueprint.parts[0].stableId, "part id");
            Equal("Wall A", blueprint.parts[0].displayName, "part name");
            Equal("group-inner", blueprint.parts[0].parentGroupId, "part parent");
            Equal(false, blueprint.parts[0].editorVisible, "part visibility");
            Equal(true, blueprint.parts[0].editorLocked, "part lock");
            Equal(1f, blueprint.parts[0].position.x, "part position");
            Equal(2f, blueprint.parts[0].scale.x, "part scale x");
            Equal(2f, blueprint.parts[0].scale.y, "part scale y");
            Equal(2f, blueprint.parts[0].scale.z, "part scale z");
            Equal(1f, blueprint.parts[1].scale.x, "default part scale");
            blueprint.groups[1].pivotPartId = "part-b";
            Require(!store.TryUpdateDocument(blueprint, blueprint.parts, blueprint.groups,
                blueprint.anchors, out error), "out-of-group pivot was accepted");
        }

        private static void MigrateV5Scale(string path)
        {
            RoundTripV9(path);
            string original = System.Text.RegularExpressions.Regex.Replace(
                File.ReadAllText(path).Replace("\"version\":10", "\"version\":5"),
                ",\"scale\":\\{[^}]+\\}", string.Empty);
            File.WriteAllText(path, original);
            var migrated = new CompositeBlueprintStore(path);
            Equal(1, migrated.All().Count, "v5 migration count");
            foreach (CompositeBlueprintStore.Part part in migrated.All()[0].parts)
                Equal(1f, part.scale.x, "v5 migration unit scale");
            Equal(original, File.ReadAllText(path), "migration does not modify original until Save");
        }

        private static void PrimaryPartRoundTrip(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new List<CompositeBlueprintStore.Part>
            {
                Part("part-a", "woodwall", "Wall A", null, true, false, 1f),
                Part("part-b", "wood_pole2", "Pole", null, true, false, 1f)
            };
            Require(store.TrySaveDocument("Primary", "ПРОЧЕЕ", parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), "part-b",
                out CompositeBlueprintStore.Blueprint saved, out string error), error);
            Equal("part-b", saved.primaryPartId, "saved primary part");
            Equal("wood_pole2", saved.prefabName, "primary prefab identity");

            var reopened = new CompositeBlueprintStore(path);
            Equal("part-b", reopened.All()[0].primaryPartId, "reopened primary part");
            Require(!reopened.TryUpdateDocument(reopened.All()[0], "Primary", "ПРОЧЕЕ",
                parts, Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), "missing", out error),
                "missing primary part was accepted");
        }

        private static void PrimaryGroupRoundTrip(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var groups = new[]
            {
                new CompositeBlueprintStore.Group { stableId = "frame", name = "Frame" },
                new CompositeBlueprintStore.Group
                    { stableId = "nested", name = "Nested", parentGroupId = "frame" }
            };
            var parts = new[]
            {
                Part("part-a", "woodwall", "A", "nested", true, false, 1f),
                Part("part-b", "wood_pole2", "B", null, true, false, 1f)
            };
            Require(store.TrySaveDocument("Group frame", "ПРОЧЕЕ", parts, groups,
                Array.Empty<CompositeBlueprintStore.VectorData>(), null, "frame",
                out CompositeBlueprintStore.Blueprint saved, out string error), error);
            Equal("frame", saved.primaryGroupId, "saved primary group");
            Equal(0, CompositeBlueprintStore.FramePartIndex(saved), "group frame part");
            Equal(true, CompositeBlueprintStore.IsFramePart(saved, 0), "nested group part included");
            Equal(false, CompositeBlueprintStore.IsFramePart(saved, 1), "root part excluded");

            Require(store.TryUpdateDocument(saved, saved.name, saved.category, parts, groups,
                saved.anchors, "part-b", "frame", out error), error);
            Equal(1, CompositeBlueprintStore.FramePartIndex(saved), "primary part wins over group");
            Equal(false, CompositeBlueprintStore.IsFramePart(saved, 0), "group loses to primary part");
            Equal(true, CompositeBlueprintStore.IsFramePart(saved, 1), "primary part selected");
            Require(!store.TryUpdateDocument(saved, saved.name, saved.category, parts, groups,
                saved.anchors, null, "missing", out error), "missing primary group was accepted");
        }

        private static void RejectInvalidScale(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new[]
            {
                Part("a", "woodwall", "A", null, true, false, 0f),
                Part("b", "woodwall", "B", null, true, false, 1f)
            };
            foreach (float scale in new[] { 0f, -1f, 0.009f, 4.01f, float.NaN, float.PositiveInfinity })
            {
                parts[0].scale = Vector(scale, 1f, 1f);
                Require(!store.TrySaveDocument(parts, Array.Empty<CompositeBlueprintStore.Group>(),
                    Array.Empty<CompositeBlueprintStore.VectorData>(), out _, out _),
                    "invalid scale accepted: " + scale);
                Equal(0, store.All().Count, "invalid scale mutated library");
                Require(!File.Exists(path), "invalid scale created file");
            }
        }

        private static void ScaleUpdateAndRollback(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new[]
            {
                Part("a", "woodwall", "A", null, true, false, 0f),
                Part("b", "woodwall", "B", null, true, false, 1f)
            };
            parts[0].scale = Vector(0.01f, 1.25f, 4f);
            Require(store.TrySaveDocument(parts, Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), out var blueprint, out string error), error);
            parts[0].scale = Vector(2f, 3f, 0.5f);
            Equal(0.01f, blueprint.parts[0].scale.x, "saved scale is detached from caller");
            Require(store.TryUpdateDocument(blueprint, parts, blueprint.groups, blueprint.anchors,
                out error), error);
            var reopened = new CompositeBlueprintStore(path).All()[0];
            Equal(2f, reopened.parts[0].scale.x, "updated scale x");
            Equal(3f, reopened.parts[0].scale.y, "updated scale y");
            Equal(0.5f, reopened.parts[0].scale.z, "updated scale z");

            parts[0].scale = Vector(2.5f, 3.5f, 0.75f);
            Require(store.TryUpdateParts(blueprint, parts, blueprint.anchors, out error), error);
            reopened = new CompositeBlueprintStore(path).All()[0];
            Equal(2.5f, reopened.parts[0].scale.x, "workspace update retains incoming scale");
            Equal(3.5f, reopened.parts[0].scale.y, "workspace update scale y");
            Equal(0.75f, reopened.parts[0].scale.z, "workspace update scale z");

            string original = File.ReadAllText(path);
            Directory.CreateDirectory(path + ".tmp");
            parts[0].scale = Vector(1.5f, 1.5f, 1.5f);
            Require(!store.TryUpdateDocument(blueprint, parts, blueprint.groups, blueprint.anchors,
                out error), "blocked scale write succeeded");
            Equal(2.5f, blueprint.parts[0].scale.x, "failed scale write rolls back memory");
            Equal(3.5f, blueprint.parts[0].scale.y, "failed scale write rolls back all axes");
            Equal(original, File.ReadAllText(path), "failed scale write preserves file");
        }

        private static void MigrateV3(string path)
        {
            string id = Guid.NewGuid().ToString("N");
            File.WriteAllText(path,
                "{\"version\":3,\"blueprints\":[{" +
                "\"id\":\"" + id + "\",\"name\":\"Legacy\"," +
                "\"prefabName\":\"woodwall\",\"category\":\"ПРОЧЕЕ\"," +
                "\"previewYaw\":45,\"previewPitch\":30,\"previewZoom\":1," +
                "\"parts\":[" + LegacyPart(0f) + "," + LegacyPart(1f) + "]," +
                "\"anchors\":[{\"x\":0,\"y\":0,\"z\":0}]}]," +
                "\"categories\":[\"ПРОЧЕЕ\"]}");

            var migrated = new CompositeBlueprintStore(path);
            Equal(1, migrated.All().Count, "migrated blueprint count");
            CompositeBlueprintStore.Blueprint blueprint = migrated.All()[0];
            Equal(0, blueprint.groups.Count, "migrated group count");
            Equal(id + "_part_000", blueprint.parts[0].stableId, "migrated first id");
            Equal(id + "_part_001", blueprint.parts[1].stableId, "migrated second id");
            Equal("woodwall", blueprint.parts[0].displayName, "migrated name");
            Equal(true, blueprint.parts[0].editorVisible, "migrated visibility");

            Require(migrated.TryUpdateDocument(
                blueprint, blueprint.parts, blueprint.groups, blueprint.anchors,
                out string error), error);
            Require(File.ReadAllText(path).Contains("\"version\":10"),
                "migration was not persisted as v10");
            Equal(id + "_part_000",
                new CompositeBlueprintStore(path).All()[0].parts[0].stableId,
                "migrated id after reopen");
        }

        private static void MetadataRoundTrip(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new List<CompositeBlueprintStore.Part>
            {
                Part("part-a", "woodwall", "Wall A", null, true, false, 0f),
                Part("part-b", "woodwall", "Wall B", null, true, false, 1f)
            };
            Require(store.TrySaveDocument(
                "Bridge", "wood",
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out CompositeBlueprintStore.Blueprint blueprint,
                out string error), error);
            Equal("Bridge", blueprint.name, "saved metadata name");
            Equal("WOOD", blueprint.category, "saved metadata category");
            Require(store.Categories().Contains("WOOD"), "saved category missing");

            Require(store.TryUpdateDocument(
                blueprint,
                "Stone bridge",
                "stone",
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out error), error);

            CompositeBlueprintStore.Blueprint reopened =
                new CompositeBlueprintStore(path).All()[0];
            Equal("Stone bridge", reopened.name, "updated metadata name");
            Equal("STONE", reopened.category, "updated metadata category");
        }

        private static void RejectGroupCycle(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var groups = new[]
            {
                new CompositeBlueprintStore.Group
                    { stableId = "a", name = "A", parentGroupId = "b" },
                new CompositeBlueprintStore.Group
                    { stableId = "b", name = "B", parentGroupId = "a" }
            };
            Require(!store.TrySaveDocument(
                new[]
                {
                    Part("part-a", "woodwall", "Wall A", "a", true, false, 0f),
                    Part("part-b", "woodwall", "Wall B", "b", true, false, 1f)
                },
                groups,
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out _, out _), "cyclic groups were accepted");
            Require(!File.Exists(path), "invalid group cycle was written");
        }

        private static void FailedWritePreservesOriginal(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new List<CompositeBlueprintStore.Part>
            {
                Part("part-a", "woodwall", "Wall A", null, true, false, 0f),
                Part("part-b", "woodwall", "Wall B", null, true, false, 1f)
            };
            Require(store.TrySaveDocument(
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out CompositeBlueprintStore.Blueprint blueprint,
                out string error), error);
            string original = File.ReadAllText(path);
            Directory.CreateDirectory(path + ".tmp");

            Require(!store.TryRename(blueprint, "Must roll back", out error),
                "blocked write unexpectedly succeeded");
            Equal("Group 1", blueprint.name, "failed rename rollback");
            Require(!store.TryUpdateDocument(
                blueprint,
                "Must roll back",
                "NEW CATEGORY",
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out error), "blocked metadata write unexpectedly succeeded");
            Equal("Group 1", blueprint.name, "failed metadata name rollback");
            Equal(CompositeBlueprintStore.DefaultCategory, blueprint.category,
                "failed metadata category rollback");
            Require(!store.Categories().Contains("NEW CATEGORY"),
                "failed metadata category remained in memory");
            Require(!store.TrySaveDocument(
                "Failed new blueprint",
                "FAILED CREATE CATEGORY",
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out CompositeBlueprintStore.Blueprint failedBlueprint,
                out error), "blocked initial save unexpectedly succeeded");
            Equal<CompositeBlueprintStore.Blueprint>(
                null, failedBlueprint, "failed initial save result");
            Equal(1, store.All().Count, "failed initial save blueprint rollback");
            Require(!store.Categories().Contains("FAILED CREATE CATEGORY"),
                "failed initial save category remained in memory");
            Equal(original, File.ReadAllText(path), "original file after failed write");
        }

        private static CompositeBlueprintStore.Part Part(
            string id,
            string prefab,
            string name,
            string group,
            bool visible,
            bool locked,
            float x) => new CompositeBlueprintStore.Part
            {
                stableId = id,
                prefabName = prefab,
                displayName = name,
                parentGroupId = group,
                editorVisible = visible,
                editorLocked = locked,
                position = Vector(x, 0f, 0f),
                rotation = new CompositeBlueprintStore.QuaternionData
                {
                    x = 0f,
                    y = 0f,
                    z = 0f,
                    w = 1f
                }
            };

        private static string LegacyPart(float x) =>
            "{\"prefabName\":\"woodwall\",\"position\":{\"x\":" +
            x.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1}}";

        private static CompositeBlueprintStore.VectorData Vector(float x, float y, float z) =>
            new CompositeBlueprintStore.VectorData { x = x, y = y, z = z };

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message ?? "operation failed");
        }

        private static void Equal<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    name + ": expected " + expected + ", actual " + actual);
        }
    }
}
