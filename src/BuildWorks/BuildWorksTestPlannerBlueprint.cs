#if BUILDWORKS_TEST_STAND
using System;
using System.Linq;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Scalar read-only probe: no editor/catalog/world writes or placement.
    internal static class BuildWorksTestPlannerBlueprint
    {
        public static string Check(string path)
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksPlugin.RequireQaStorageIdle();
            var before = new ZPackage(); player.GetInventory().Save(before);
            var raw = ExternalBlueprintLibrary.LoadFile(path).Single();
            if (raw.importError != null) throw new InvalidOperationException(raw.importError);
            var evidence = raw.sourceDocuments.Single().ToGeometry(); var model = evidence.Planner;
            if (model == null) throw new InvalidOperationException("Planner source required.");
            byte[] bytes = evidence.OriginalBytes();
            CompositeBlueprintStore.VectorData? PrefabScale(string name)
            {
                var prefab = ZNetScene.instance.GetPrefab(name);
                return prefab ? (CompositeBlueprintStore.VectorData?)new CompositeBlueprintStore.VectorData(prefab.transform.localScale) : null;
            }
            var document = ExternalBlueprintLibrary.PrepareForHost(raw, PrefabScale);
            if (document.importError != null || document.importAbsoluteScale || document.parts.Any(p => p.scaleIsAbsolute))
                throw new InvalidOperationException("Planner native scale is unresolved.");
            if (document.groups.Count != model.Layers.Count || document.groups.Any(g => g.sourceDocumentId != evidence.Id ||
                !model.Layers.Any(l => l.Id == g.sourceLayerId && l.Name == g.name)))
                throw new InvalidOperationException("Planner layer provenance differs.");
            float positionError = 0, axisError = 0, rootScaleError = 0;
            foreach (var part in document.parts)
            {
                var row = model.Pieces[part.sourceRecord - 1]; var prefab = ZNetScene.instance.GetPrefab(part.prefabName);
                if (!prefab || !evidence.HasPartAddress(part.sourceLine, part.sourceRecord, part.originalPrefabName ?? prefab.name) || !part.sourceFrameApplied ||
                    document.groups.Single(g => g.stableId == part.parentGroupId).sourceLayerId != row.LayerId)
                    throw new InvalidOperationException("Planner prefab/source/layer binding differs.");
                var expected = new Quaternion((float)row.Rotation.X, (float)row.Rotation.Y, (float)row.Rotation.Z, (float)row.Rotation.W).normalized;
                var actual = new Quaternion(part.rotation.x, part.rotation.y, part.rotation.z, part.rotation.w);
                positionError = Mathf.Max(positionError, Vector3.Distance(part.position.ToVector3(),
                    new Vector3((float)row.Position.X, (float)row.Position.Y, (float)row.Position.Z)));
                foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                    axisError = Mathf.Max(axisError, Vector3.Distance(expected * axis, actual * axis));
                rootScaleError = Mathf.Max(rootScaleError, Vector3.Distance(part.RootScale(prefab.transform.localScale), Vector3.one));
            }
            var review = PlannerExport.Review(document, PrefabScale);
            var snapshot = BlueprintExportPair.Snapshot(document).Document;
            if (positionError > .0001f || axisError > .0001f || rootScaleError > .0001f || review.Blockers.Count != 0 ||
                snapshot.groups.Count != model.Layers.Count || !snapshot.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes) ||
                snapshot.groups.Any(g => g.sourceDocumentId != evidence.Id || !evidence.HasLayerAddress(g.sourceLayerId)))
                throw new InvalidOperationException("Planner native frame/review/snapshot differs.");
            if (string.IsNullOrEmpty(BlueprintSourcePresentation.Metadata(document)))
                throw new InvalidOperationException("Planner source metadata absent.");
            var reopened = ExternalBlueprintLibrary.LoadFile(path).Single();
            var after = new ZPackage(); player.GetInventory().Save(after);
            if (!before.GetArray().SequenceEqual(after.GetArray()) || reopened.importError != null ||
                !reopened.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes))
                throw new InvalidOperationException("Planner smoke changed inventory/source.");
            return JsonUtility.ToJson(new Result { parts = document.parts.Count, layers = document.groups.Count,
                emptyLayers = document.groups.Count(g => !document.parts.Any(p => p.parentGroupId == g.stableId)),
                sourceBytes = bytes.Length, positionError = positionError, axisError = axisError, rootScaleError = rootScaleError,
                sourceFormat = evidence.Format, version = BuildWorksPlugin.PluginVersion,
                inventoryUnchanged = true, originalUnchanged = true, exportReviewed = true, snapshotValidated = true });
        }
        public static string CheckNativeExport(string path, string plannerPath)
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksPlugin.RequireQaStorageIdle();
            var before = new ZPackage(); player.GetInventory().Save(before);
            Check(plannerPath);
            var raw = ExternalBlueprintLibrary.LoadFile(path).Single();
            var reference = ExternalBlueprintLibrary.LoadFile(plannerPath).Single();
            if (raw.importError != null || reference.importError != null) throw new InvalidOperationException("Producer pair load failed.");
            var evidence = raw.sourceDocuments.Single().ToGeometry();
            if (evidence.Format != "blueprint" && evidence.Format != "vbuild")
                throw new InvalidOperationException("Native producer export required.");
            byte[] bytes = evidence.OriginalBytes();
            CompositeBlueprintStore.VectorData? Scale(string name)
            {
                var prefab = ZNetScene.instance.GetPrefab(name);
                return prefab ? (CompositeBlueprintStore.VectorData?)new CompositeBlueprintStore.VectorData(prefab.transform.localScale) : null;
            }
            var document = ExternalBlueprintLibrary.PrepareForHost(raw, Scale);
            var expected = ExternalBlueprintLibrary.PrepareForHost(reference, Scale);
            if (document.importError != null || document.importAbsoluteScale || document.parts.Any(p => p.scaleIsAbsolute) ||
                document.parts.Count != expected.parts.Count) throw new InvalidOperationException("Producer native projection unresolved.");
            float positionError = 0, axisError = 0, rootScaleError = 0;
            for (int index = 0; index < document.parts.Count; ++index)
            {
                var part = document.parts[index]; var other = expected.parts[index];
                var prefab = ZNetScene.instance.GetPrefab(part.prefabName);
                if (!prefab || part.prefabName != other.prefabName ||
                    !evidence.HasPartAddress(part.sourceLine, part.sourceRecord, part.originalPrefabName ?? part.prefabName))
                    throw new InvalidOperationException("Producer native identity/source mismatch.");
                positionError = Mathf.Max(positionError, Vector3.Distance(part.position.ToVector3(), other.position.ToVector3()));
                var actual = new Quaternion(part.rotation.x, part.rotation.y, part.rotation.z, part.rotation.w);
                var wanted = new Quaternion(other.rotation.x, other.rotation.y, other.rotation.z, other.rotation.w);
                foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                    axisError = Mathf.Max(axisError, Vector3.Distance(actual * axis, wanted * axis));
                rootScaleError = Mathf.Max(rootScaleError, Vector3.Distance(part.RootScale(prefab.transform.localScale), other.RootScale(prefab.transform.localScale)));
            }
            var snapshot = BlueprintExportPair.Snapshot(document).Document;
            var reopened = ExternalBlueprintLibrary.LoadFile(path).Single();
            var after = new ZPackage(); player.GetInventory().Save(after);
            if (positionError > .0001f || axisError > .0001f || rootScaleError > .0001f ||
                !before.GetArray().SequenceEqual(after.GetArray()) || reopened.importError != null ||
                !reopened.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes) ||
                !snapshot.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes))
                throw new InvalidOperationException("Producer native pair frame/source/inventory differs.");
            return JsonUtility.ToJson(new NativeExportResult { parts = document.parts.Count, sourceBytes = bytes.Length,
                sourceFormat = evidence.Format, version = BuildWorksPlugin.PluginVersion,
                positionError = positionError, axisError = axisError, rootScaleError = rootScaleError,
                nativeExportPaired = true, inventoryUnchanged = true, originalUnchanged = true, snapshotValidated = true });
        }

        public static string CheckNativeSource(string path)
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksPlugin.RequireQaStorageIdle();
            var before = new ZPackage(); player.GetInventory().Save(before);
            var raw = ExternalBlueprintLibrary.LoadFile(path).Single();
            if (raw.importError != null) throw new InvalidOperationException(raw.importError);
            var evidence = raw.sourceDocuments.Single().ToGeometry();
            if ((evidence.Format != "blueprint" && evidence.Format != "vbuild") || !raw.importAbsoluteScale)
                throw new InvalidOperationException("Absolute-scale native source required.");
            byte[] bytes = evidence.OriginalBytes();
            CompositeBlueprintStore.VectorData? Scale(string name)
            {
                var prefab = ZNetScene.instance.GetPrefab(name);
                return prefab ? (CompositeBlueprintStore.VectorData?)new CompositeBlueprintStore.VectorData(prefab.transform.localScale) : null;
            }
            var document = ExternalBlueprintLibrary.PrepareForHost(raw, Scale);
            if (document.importError != null || document.importAbsoluteScale || document.parts.Any(p => p.scaleIsAbsolute) ||
                document.parts.Count != raw.parts.Count) throw new InvalidOperationException("Native source projection unresolved.");
            float positionError = 0, axisError = 0, rootScaleError = 0;
            for (int index = 0; index < document.parts.Count; ++index)
            {
                var part = document.parts[index]; var original = raw.parts[index];
                var prefab = ZNetScene.instance.GetPrefab(part.prefabName);
                if (!prefab || prefab.name != part.prefabName ||
                    (part.originalPrefabName ?? part.prefabName) != original.prefabName ||
                    part.sourceLine != original.sourceLine ||
                    !evidence.HasPartAddress(part.sourceLine, part.sourceRecord, part.originalPrefabName ?? part.prefabName))
                    throw new InvalidOperationException("Native source prefab/address mismatch.");
                positionError = Mathf.Max(positionError, Vector3.Distance(part.position.ToVector3(), original.position.ToVector3()));
                var actual = new Quaternion(part.rotation.x, part.rotation.y, part.rotation.z, part.rotation.w);
                var expected = new Quaternion(original.rotation.x, original.rotation.y, original.rotation.z, original.rotation.w);
                foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                    axisError = Mathf.Max(axisError, Vector3.Distance(actual * axis, expected * axis));
                rootScaleError = Mathf.Max(rootScaleError,
                    Vector3.Distance(part.RootScale(prefab.transform.localScale), original.scale.ToVector3()));
            }
            var snapshot = BlueprintExportPair.Snapshot(document).Document;
            var reopened = ExternalBlueprintLibrary.LoadFile(path).Single();
            var after = new ZPackage(); player.GetInventory().Save(after);
            if (new[] { positionError, axisError, rootScaleError }.Any(value => float.IsNaN(value) || float.IsInfinity(value) || value > .0001f) ||
                !before.GetArray().SequenceEqual(after.GetArray()) || reopened.importError != null ||
                !reopened.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes) ||
                !snapshot.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes))
                throw new InvalidOperationException("Native source frame/scale/inventory/snapshot differs.");
            return JsonUtility.ToJson(new NativeSourceResult { parts = document.parts.Count, sourceBytes = bytes.Length,
                sourceFormat = evidence.Format, version = BuildWorksPlugin.PluginVersion,
                positionError = positionError, axisError = axisError, rootScaleError = rootScaleError,
                nativeSourceResolved = true, inventoryUnchanged = true, originalUnchanged = true, snapshotValidated = true });
        }

        [Serializable] private sealed class NativeSourceResult
        {
            public int parts, sourceBytes;
            public string sourceFormat, version;
            public float positionError, axisError, rootScaleError;
            public bool nativeSourceResolved, inventoryUnchanged, originalUnchanged, snapshotValidated;
        }

        [Serializable] private sealed class NativeExportResult
        {
            public int parts, sourceBytes;
            public string sourceFormat, version;
            public float positionError, axisError, rootScaleError;
            public bool nativeExportPaired, inventoryUnchanged, originalUnchanged, snapshotValidated;
        }

        [Serializable] private sealed class Result
        {
            public int parts, layers, emptyLayers, sourceBytes;
            public float positionError, axisError, rootScaleError;
            public string sourceFormat, version;
            public bool inventoryUnchanged, originalUnchanged, exportReviewed, snapshotValidated;
        }
    }
}
#endif
