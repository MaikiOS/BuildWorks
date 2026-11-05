using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintUnboundImport
    {
        // Unity thread only: workers receive immutable values, never prefab objects or live callbacks.
        internal static UnboundImportContext Capture(GameObject prefab)
        {
            if (!prefab || !prefab.GetComponent<Piece>()) return null;
            var view = prefab.GetComponent<ZNetView>();
            if (!view) return null;
            var metadata = BlueprintPrefabMetadataCapture.Capture(prefab)?.ToGeometry();
            if (metadata == null) return null;
            var sign = prefab.GetComponent<Sign>();
            return new UnboundImportContext(metadata, view.m_syncInitialScale,
                BlueprintStorageCapture.SupportsKgStateProjection(prefab) == true, sign && sign.GetType() == typeof(Sign),
                BlueprintMaterialState.Supports(prefab), BlueprintStorageCapture.PrefabGrid(prefab));
        }
    }
}
