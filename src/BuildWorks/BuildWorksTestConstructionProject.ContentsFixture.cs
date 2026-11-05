#if BUILDWORKS_TEST_STAND || BUILDWORKS_CONTENTS_FIXTURE_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static partial class BuildWorksTestConstructionProject
    {
        internal const string ContentsProjectName = "QA paid contents construction";

        internal static CompositeBlueprintStore.Blueprint ContentsBlueprint(CompositeBlueprintStore.ImportReview review, Guid id)
        {
            if (id == Guid.Empty || review?.Documents?.Count != 1 || review.Documents[0] == null || review.Documents[0].importError != null)
                throw new InvalidDataException("One valid owned contents fixture is required.");
            var source = review.Documents[0].ImportCopy();
            var parts = source.parts?.Where(part => part != null && part.prefabName == "piece_chest_wood" && part.stableId == "chest").ToArray();
            if (parts == null || parts.Length != 1) throw new InvalidDataException("Contents fixture needs its unique chest part.");
            var chest = parts[0]; var data = chest.foreignData?.ToGeometry();
            if (data == null || data.Format != BlueprintForeignPartData.NativeContainerFormat || data.ContentsCleared ||
                !BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out var items) ||
                items.Count != 1 || items[0].Prefab != "Wood" || items[0].Stack != 3)
                throw new InvalidDataException("Contents fixture must request exactly three existing Wood.");
            chest.position = new CompositeBlueprintStore.VectorData(Vector3.zero);
            chest.rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity);
            chest.scale = new CompositeBlueprintStore.VectorData(Vector3.one);
            chest.baseScale = new CompositeBlueprintStore.VectorData(Vector3.one);
            chest.editorVisible = true; chest.editorLocked = false;
            return new CompositeBlueprintStore.Blueprint { id = id.ToString("N"), name = ContentsProjectName,
                category = "QA", prefabName = "piece_chest_wood", primaryPartId = "chest",
                parts = new List<CompositeBlueprintStore.Part> { chest }, sourceDocuments = source.sourceDocuments };
        }
    }
}
#endif
