using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    [HarmonyPatch(typeof(Player), "FindClosestSnapPoints")]
    internal static class BlueprintHammerSnapSearch
    {
        internal const string PrefabPrefix = "BuildWorks_Blueprint_";
        private delegate bool NativeSearch(Player player, Vector3 point, List<Transform> points,
            float maximumDistance, out Transform closest, out float distance);
        private static readonly MethodInfo InnerMethod = AccessTools.Method(typeof(Player), "FindClosestSnappoint",
            new[] { typeof(Vector3), typeof(List<Transform>), typeof(float),
                typeof(Transform).MakeByRefType(), typeof(float).MakeByRefType() });
        private static readonly NativeSearch Native = AccessTools.MethodDelegate<NativeSearch>(InnerMethod);
        private static readonly MethodInfo OuterMethod = AccessTools.Method(typeof(Player), "FindClosestSnapPoints");
        [ThreadStatic] private static Context current;

        private sealed class Context
        {
            internal Player Player;
            internal List<Transform> List;
            internal Transform[] Targets;
            internal BlueprintNearestPartIndex Index;
            internal bool Attempted, Disabled;
        }

        private static void Prefix(Player __instance, Transform __0, List<Transform> ___m_tempSnapPoints2,
            int ___m_manualSnapPoint, out Context __state)
        {
            __state = current;
            current = __instance == Player.m_localPlayer && __0 && __0.childCount > 512 &&
                ___m_manualSnapPoint < 0 && __0.name.StartsWith(PrefabPrefix, StringComparison.Ordinal) &&
                !HasForeignPatches(InnerMethod) && !HasForeignPatches(OuterMethod)
                ? new Context { Player = __instance, List = ___m_tempSnapPoints2 } : null;
        }

        private static void Finalizer(Context __state) { current = __state; }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            if (InnerMethod == null || InnerMethod.IsStatic || InnerMethod.ReturnType != typeof(bool) ||
                code.FindAll(instruction => instruction.Calls(InnerMethod)).Count != 2)
                throw new InvalidOperationException("Current Valheim snap search layout is unsupported.");
            MethodInfo replacement = AccessTools.Method(typeof(BlueprintHammerSnapSearch), nameof(FindClosest));
            foreach (CodeInstruction instruction in code)
                if (instruction.Calls(InnerMethod))
                { instruction.opcode = OpCodes.Call; instruction.operand = replacement; }
            return code;
        }

        private static bool FindClosest(Player player, Vector3 point, List<Transform> points,
            float maximumDistance, out Transform closest, out float distance)
        {
            Context context = current;
            if (context == null || context.Player != player || !ReferenceEquals(context.List, points) ||
                !Finite(point) || float.IsNaN(maximumDistance) || float.IsInfinity(maximumDistance) || maximumDistance < 0)
                return Native(player, point, points, maximumDistance, out closest, out distance);
            if (!context.Attempted)
            {
                context.Attempted = true;
                if (points.Count > BlueprintEditorDocument.MaximumParts) context.Disabled = true;
                else
                {
                    context.Targets = points.ToArray();
                    var positions = new Point3[points.Count];
                    for (int i = 0; i < positions.Length; ++i)
                    {
                        if (!context.Targets[i]) { context.Disabled = true; break; }
                        Vector3 position = context.Targets[i].position;
                        if (!Finite(position)) { context.Disabled = true; break; }
                        positions[i] = new Point3(position.x, position.y, position.z);
                    }
                    if (!context.Disabled && positions.Length > 0)
                        context.Index = new BlueprintNearestPartIndex(positions);
                }
            }
            if (context.Disabled || points.Count != context.Targets.Length)
                return Native(player, point, points, maximumDistance, out closest, out distance);
            closest = null;
            distance = 999999f;
            float foundDistance = float.PositiveInfinity;
            int target = context.Index == null ? -1 : context.Index.FindNearestByDistance(
                new Point3(point.x, point.y, point.z), maximumDistance, out foundDistance);
            if (target < 0) return false;
            // Native ignores a target at or beyond its initial distance sentinel.
            float candidate = Vector3.Distance(context.Targets[target].position, point);
            if (candidate != foundDistance || !(candidate <= maximumDistance) || !(candidate < distance))
                return Native(player, point, points, maximumDistance, out closest, out distance);
            closest = context.Targets[target];
            distance = candidate;
            return true;
        }

        private static bool Finite(Vector3 point) =>
            !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z) &&
            !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z);

        private static bool HasForeignPatches(MethodBase method)
        {
            var patches = Harmony.GetPatchInfo(method);
            if (patches != null)
                foreach (string owner in patches.Owners)
                    if (owner != BuildWorksPlugin.PluginGuid) return true;
            return false;
        }
    }
}
