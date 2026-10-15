using System.Linq;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // One explicit native replacement. Compare current prototypes; never instantiate them.
    internal static class BlueprintNativePrefabMigration
    {
        internal static bool CanMigrateRoof()
        {
            var scene = ZNetScene.instance;
            if (!scene) return false;
            GameObject old = scene.GetPrefab(Geometry.BlueprintPrefabMigration.OldRoof);
            GameObject current = scene.GetPrefab(Geometry.BlueprintPrefabMigration.CurrentRoof);
            if (!old || !current) return false;
            Piece piece = current.GetComponent<Piece>();
            // This rule never creates a free recipe or bypasses the native target's rules.
            if (!piece || !current.GetComponent<WearNTear>() || piece.m_resources == null || piece.m_resources.Length == 0 ||
                piece.m_resources.Any(resource => resource == null || !resource.m_resItem || resource.m_amount <= 0)) return false;
            return BlueprintPrefabMigrationGeometry.Matches(old, current);
        }
    }
}
