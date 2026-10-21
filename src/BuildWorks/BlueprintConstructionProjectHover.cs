using HarmonyLib;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    [HarmonyPatch(typeof(Player), "UpdateHover")]
    internal static class BlueprintConstructionProjectHover
    {
        private delegate void NativeSearch(Player player, out GameObject hover, out Character creature);
        private static readonly NativeSearch Find = AccessTools.MethodDelegate<NativeSearch>(
            AccessTools.Method(typeof(Player), "FindHoverObject", new[] {
                typeof(GameObject).MakeByRefType(), typeof(Character).MakeByRefType() }));

        // Native placement clears hover with a hammer. Restore only our nearest board, using native occlusion/reach.
        private static void Postfix(Player __instance, IDoodadController ___m_doodadController,
            ref GameObject ___m_hovering)
        {
            var runtime = BuildWorksPlugin.ConstructionProjects;
            if (__instance != Player.m_localPlayer || !__instance || __instance.IsDead() ||
                ___m_hovering || runtime == null || runtime.BoardCount == 0 ||
                ___m_doodadController != null || !__instance.InPlaceMode() ||
                !BlueprintConstructionProjectRuntime.HasHammer(__instance) || !BuildWorksPlugin.CanOpenConstructionProject ||
                !GameCamera.instance) return;
            Find(__instance, out GameObject hit, out _);
            var board = hit ? hit.GetComponentInParent<BlueprintConstructionProjectBoard>() : null;
            if (!board || runtime.Board(board.Id) != board) return;
            ___m_hovering = hit;
        }
    }
}
