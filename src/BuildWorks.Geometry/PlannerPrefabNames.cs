using System;
using System.Collections.Generic;
using System.Linq;

namespace OstrixMods.BuildWorks.Geometry
{
    // Exact producer/native pairs measured against the Valheim registry; never fuzzy lookup.
    public static class PlannerPrefabNames
    {
        private static readonly Dictionary<string, string> native = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "armorstand", "ArmorStand" },
            { "candle_resin", "Candle_resin" },
            { "castlekit_braided_box01", "CastleKit_braided_box01" },
            { "firtree_small", "FirTree_small" },
            { "piece_flametal_beam", "Piece_flametal_beam" },
            { "piece_flametal_pillar", "Piece_flametal_pillar" },
            { "piece_grausten_floor_1x1", "Piece_grausten_floor_1x1" },
            { "piece_grausten_floor_2x2", "Piece_grausten_floor_2x2" },
            { "piece_grausten_floor_4x4", "Piece_grausten_floor_4x4" },
            { "piece_grausten_pillar_arch", "Piece_grausten_pillar_arch" },
            { "piece_grausten_pillar_arch_small", "Piece_grausten_pillar_arch_small" },
            { "piece_grausten_pillarbase_medium", "Piece_grausten_pillarbase_medium" },
            { "piece_grausten_pillarbase_small", "Piece_grausten_pillarbase_small" },
            { "piece_grausten_pillarbase_tapered", "Piece_grausten_pillarbase_tapered" },
            { "piece_grausten_pillarbase_tapered_inverted", "Piece_grausten_pillarbase_tapered_inverted" },
            { "piece_grausten_pillarbeam_medium", "Piece_grausten_pillarbeam_medium" },
            { "piece_grausten_pillarbeam_small", "Piece_grausten_pillarbeam_small" },
            { "piece_grausten_stone_ladder", "Piece_grausten_stone_ladder" },
            { "piece_grausten_wall_1x2", "Piece_grausten_wall_1x2" },
            { "piece_grausten_wall_2x2", "Piece_grausten_wall_2x2" },
            { "piece_grausten_wall_4x2", "Piece_grausten_wall_4x2" },
            { "piece_grausten_wall_arch", "Piece_grausten_wall_arch" },
            { "piece_grausten_wall_arch_inverted", "Piece_grausten_wall_arch_inverted" },
            { "piece_grausten_window_2x2", "Piece_grausten_window_2x2" },
            { "piece_grausten_window_4x2", "Piece_grausten_window_4x2" },
            { "piece_meadcauldron", "piece_MeadCauldron" },
            { "placeable_stone", "Placeable_Stone" },
        };
        private static readonly Dictionary<string, string> producer = native.ToDictionary(p => p.Value, p => p.Key, StringComparer.Ordinal);
        public static string Native(string name) => name != null && native.TryGetValue(name, out string value) ? value : name;
        public static string Producer(string name) => name != null && producer.TryGetValue(name, out string value) ? value : name;
        public static bool IsPair(string original, string effective) => original != null && native.TryGetValue(original, out string value) && value == effective;
        public static bool SupportsSource(BlueprintSourceDocument source) => source != null &&
            (source.Planner != null || source.Format == "blueprint" || source.Format == "vbuild");
    }
}
