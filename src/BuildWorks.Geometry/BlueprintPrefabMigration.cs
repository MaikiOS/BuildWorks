namespace OstrixMods.BuildWorks.Geometry
{
    // Explicit native replacement, measured on Valheim 1.0.20. Never a name similarity rule.
    public static class BlueprintPrefabMigration
    {
        public const string OldRoof = "wood_wall_roof";
        public const string CurrentRoof = "wood_wall_roof_a";

        public static bool IsRecordedPair(string original, string effective) =>
            original == null || original == OldRoof && effective == CurrentRoof;
    }
}
