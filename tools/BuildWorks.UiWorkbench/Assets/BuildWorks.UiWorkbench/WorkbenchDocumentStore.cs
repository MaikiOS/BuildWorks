using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks.UiWorkbench
{
    internal static class WorkbenchDocumentStore
    {
        [Serializable]
        private sealed class Snapshot
        {
            public int version = 1;
            public string sourceBlueprintId;
            public string name;
            public string category;
            public PartData[] parts;
            public GroupData[] groups;
        }

        [Serializable]
        private sealed class PartData
        {
            public string stableId;
            public string prefabName;
            public string displayName;
            public string parentGroupId;
            public bool visible;
            public bool locked;
            public float px;
            public float py;
            public float pz;
            public float rx;
            public float ry;
            public float rz;
            public float rw;
            public float sx;
            public float sy;
            public float sz;
        }

        [Serializable]
        private sealed class GroupData
        {
            public string stableId;
            public string name;
            public string parentGroupId;
            public bool visible;
            public bool locked;
        }

        internal static void Save(BlueprintEditorDocument document, string path)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(
                "Path is required.", nameof(path));
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporary = fullPath + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(Capture(document), true),
                new UTF8Encoding(false));
            BlueprintEditorDocument verified = Load(temporary);
            if (verified.Parts.Count != document.Parts.Count ||
                verified.Groups.Count != document.Groups.Count)
                throw new InvalidDataException("Saved workbench document did not round-trip.");
            if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null, true);
            else File.Move(temporary, fullPath);
        }

        internal static BlueprintEditorDocument Load(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException(
                "Path is required.", nameof(path));
            Snapshot snapshot = JsonUtility.FromJson<Snapshot>(File.ReadAllText(path));
            if (snapshot == null || snapshot.version != 1 || snapshot.parts == null ||
                snapshot.groups == null)
                throw new InvalidDataException("Unsupported workbench document.");
            var groups = new List<BlueprintEditorGroup>(snapshot.groups.Length);
            foreach (GroupData group in snapshot.groups)
                groups.Add(new BlueprintEditorGroup(
                    group.stableId, group.name, group.visible, group.locked,
                    string.IsNullOrEmpty(group.parentGroupId) ? null : group.parentGroupId));
            var parts = new List<BlueprintEditorPart>(snapshot.parts.Length);
            foreach (PartData part in snapshot.parts)
                parts.Add(new BlueprintEditorPart(
                    part.stableId,
                    part.prefabName,
                    part.displayName,
                    new Point3(part.px, part.py, part.pz),
                    new Rotation3(part.rx, part.ry, part.rz, part.rw),
                    string.IsNullOrEmpty(part.parentGroupId) ? null : part.parentGroupId,
                    part.visible,
                    part.locked,
                    new Point3(part.sx, part.sy, part.sz)));
            var document = new BlueprintEditorDocument(
                snapshot.sourceBlueprintId, snapshot.name, snapshot.category, parts, groups);
            document.MarkClean();
            return document;
        }

        private static Snapshot Capture(BlueprintEditorDocument document)
        {
            var snapshot = new Snapshot
            {
                sourceBlueprintId = document.SourceBlueprintId,
                name = document.Name,
                category = document.Category,
                parts = new PartData[document.Parts.Count],
                groups = new GroupData[document.Groups.Count]
            };
            for (int index = 0; index < document.Parts.Count; ++index)
            {
                BlueprintEditorPart part = document.Parts[index];
                snapshot.parts[index] = new PartData
                {
                    stableId = part.StableId,
                    prefabName = part.PrefabName,
                    displayName = part.DisplayName,
                    parentGroupId = part.ParentGroupId,
                    visible = part.Visible,
                    locked = part.Locked,
                    px = (float)part.Position.X,
                    py = (float)part.Position.Y,
                    pz = (float)part.Position.Z,
                    rx = (float)part.Rotation.X,
                    ry = (float)part.Rotation.Y,
                    rz = (float)part.Rotation.Z,
                    rw = (float)part.Rotation.W,
                    sx = (float)part.Scale.X,
                    sy = (float)part.Scale.Y,
                    sz = (float)part.Scale.Z
                };
            }
            for (int index = 0; index < document.Groups.Count; ++index)
            {
                BlueprintEditorGroup group = document.Groups[index];
                snapshot.groups[index] = new GroupData
                {
                    stableId = group.StableId,
                    name = group.Name,
                    parentGroupId = group.ParentGroupId,
                    visible = group.Visible,
                    locked = group.Locked
                };
            }
            return snapshot;
        }
    }
}
