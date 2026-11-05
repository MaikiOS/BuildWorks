#if BUILDWORKS_TEST_STAND
using System;
using System.Linq;
using UnityEngine;
using G = OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // Read-only, no producer execution, editor/catalog writes, movement or construction.
    internal static class BuildWorksTestBbpBlueprint
    {
        public static string Check(string path)
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksPlugin.RequireQaStorageIdle();
            var before = new ZPackage(); player.GetInventory().Save(before);
            var document = ExternalBlueprintLibrary.LoadFile(path).Single();
            if (document.importError != null) throw new InvalidOperationException(document.importError);
            var evidence = document.sourceDocuments.Single().ToGeometry();
            byte[] bytes = evidence.OriginalBytes();
            var model = evidence.BetterBuilding;
            if (model == null || model.Terrain != null || model.Pieces.Any(p => p.DisplayItem != null || p.Scale.HasValue))
                throw new InvalidOperationException("This native smoke checks BetterBuilding geometry/sign data only; optional data is separately tested.");
            float positionError = 0, axisError = 0;
            foreach (var part in document.parts)
            {
                var row = model.Pieces[part.sourceRecord - 1];
                var prefab = ZNetScene.instance.GetPrefab(part.prefabName);
                if (!prefab || !evidence.HasPartAddress(part.sourceLine, part.sourceRecord, prefab.name) || !part.sourceFrameApplied)
                    throw new InvalidOperationException("BBP native prefab/source binding differs.");
                var q = new Quaternion((float)row.Rotation.X, (float)row.Rotation.Y, (float)row.Rotation.Z, (float)row.Rotation.W).normalized;
                var actual = new Quaternion(part.rotation.x, part.rotation.y, part.rotation.z, part.rotation.w);
                positionError = Mathf.Max(positionError, Vector3.Distance(part.position.ToVector3(), new Vector3((float)row.Position.X, (float)row.Position.Y, (float)row.Position.Z)));
                foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward }) axisError = Mathf.Max(axisError, Vector3.Distance(q * axis, actual * axis));
                if (part.signText != row.SignText) throw new InvalidOperationException("BBP sign data differs.");
            }
            bool legacyJson = evidence.Format == G.BetterBuildingBlueprint.JsonFormat;
            var review = BetterBuildingExport.Review(document, name => {
                var prefab = ZNetScene.instance.GetPrefab(name);
                return prefab ? (CompositeBlueprintStore.VectorData?)new CompositeBlueprintStore.VectorData(prefab.transform.localScale) : null;
            }, legacyJson ? BetterBuildingExport.Target.LegacyJson : BetterBuildingExport.Target.Bbp);
            if (positionError > .0001f || axisError > .0001f || review.Blockers.Count != 0 || !bytes.SequenceEqual(evidence.OriginalBytes()))
                throw new InvalidOperationException("BBP native frame/review/original differs.");
            if (string.IsNullOrEmpty(BlueprintSourcePresentation.Metadata(document)) || BlueprintSourcePresentation.Images(document).Any())
                throw new InvalidOperationException("BBP source metadata/image guard differs.");
            var reopened = ExternalBlueprintLibrary.LoadFile(path).Single();
            var after = new ZPackage(); player.GetInventory().Save(after);
            if (!before.GetArray().SequenceEqual(after.GetArray()) || reopened.importError != null ||
                !reopened.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes))
                throw new InvalidOperationException("BBP smoke changed inventory/source.");
            return JsonUtility.ToJson(new Result { parts = document.parts.Count, signs = review.Signs, positionError = positionError,
                axisError = axisError, sourceBytes = bytes.Length, sourceFormat = evidence.Format, version = BuildWorksPlugin.PluginVersion,
                inventoryUnchanged = true, originalUnchanged = true, exportReviewed = true, legacyJsonReviewed = legacyJson });
        }
        [Serializable] private sealed class Result
        {
            public int parts, signs, sourceBytes;
            public float positionError, axisError;
            public string sourceFormat, version;
            public bool inventoryUnchanged, originalUnchanged, exportReviewed, legacyJsonReviewed;
        }
    }
}
#endif
