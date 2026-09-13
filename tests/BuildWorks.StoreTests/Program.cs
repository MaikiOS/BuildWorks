using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class Program
    {
        private static int Main()
        {
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
            Require(File.ReadAllText(path).Contains("\"version\":9"), "v9 was not written");

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
                File.ReadAllText(path).Replace("\"version\":9", "\"version\":5"),
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
            Require(File.ReadAllText(path).Contains("\"version\":9"),
                "migration was not persisted as v9");
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
            Equal("Группа 1", blueprint.name, "failed rename rollback");
            Require(!store.TryUpdateDocument(
                blueprint,
                "Must roll back",
                "NEW CATEGORY",
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out error), "blocked metadata write unexpectedly succeeded");
            Equal("Группа 1", blueprint.name, "failed metadata name rollback");
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
