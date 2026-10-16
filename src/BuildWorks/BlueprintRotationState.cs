using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintRotationState
    {
        private static readonly int ExactKey = "BuildWorks_ExactRotation_v1".GetStableHashCode();
        private static readonly int WitnessKey = "BuildWorks_ExactRotationWitness_v1".GetStableHashCode();
        private static readonly FieldInfo NativeRotation = AccessTools.Field(typeof(ZDO), "m_rotation");

        internal static void RequireFreshCreation()
        {
            if (ZNetView.m_useInitZDO || ZNetView.m_initZDO != null) throw Unsupported();
        }

        internal static void ApplyCreated(GameObject placed, GameObject source, GameObject ghost)
        {
            if (!placed || !source || !ghost || placed == source || placed == ghost ||
                !placed.GetComponent<Piece>() || !source.GetComponent<Piece>()) throw Unsupported();
            ZNetView view = placed.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || !view.IsOwner()) throw Unsupported();
            ZDO data = view.GetZDO();
            if (data == null || data.m_uid.IsNone() || BlueprintMaterialState.SharesData(data, source) ||
                BlueprintMaterialState.SharesData(data, ghost) || NativeRotation == null ||
                data.GetQuaternion(ExactKey, out _) || data.GetQuaternion(WitnessKey, out _)) throw Unsupported();
            Quaternion exact = placed.transform.rotation;
            if (!Valid(exact)) throw Unsupported();
            // Use the host's disk encoder: its half-degree bins can truncate an almost exact angle.
            var packet = new ZPackage();
            packet.WriteSmallRotation((Vector3)NativeRotation.GetValue(data));
            Quaternion witness = Quaternion.Euler(new ZPackage(packet.GetArray()).ReadSmallRotation());
            if (!Valid(witness)) throw Unsupported();
            data.Set(WitnessKey, witness);
            data.Set(ExactKey, exact);
            if (!view || !view.IsValid() || !view.IsOwner() || !ReferenceEquals(data, view.GetZDO()) ||
                !data.GetQuaternion(ExactKey, out Quaternion saved) || !Close(saved, exact) ||
                !data.GetQuaternion(WitnessKey, out saved) || !Close(saved, witness)) throw Unsupported();
        }

        internal static bool TryRestore(ZDO data, Quaternion native, out Quaternion rotation)
        {
            rotation = native;
            if (data == null || data.m_uid.IsNone() || !Valid(native) ||
                !data.GetQuaternion(ExactKey, out Quaternion exact) || !Valid(exact) ||
                !data.GetQuaternion(WitnessKey, out Quaternion witness) || !Valid(witness) ||
                (!Close(native, exact) && !Close(native, witness))) return false;
            rotation = exact;
            return true;
        }

        internal static void Invalidate(ZDO data, Quaternion requested)
        {
            if (data != null && data.GetQuaternion(ExactKey, out Quaternion exact) && !Close(requested, exact))
            {
                data.RemoveQuaternion(ExactKey);
                data.RemoveQuaternion(WitnessKey);
            }
        }

        private static bool Valid(Quaternion q)
        {
            float norm = Quaternion.Dot(q, q);
            return !float.IsNaN(norm) && !float.IsInfinity(norm) && Math.Abs(norm - 1f) <= 1e-5f;
        }

        private static bool Close(Quaternion a, Quaternion b)
        {
            float sign = Quaternion.Dot(a, b) < 0 ? -1 : 1;
            float x = a.x - sign * b.x, y = a.y - sign * b.y;
            float z = a.z - sign * b.z, w = a.w - sign * b.w;
            return x * x + y * y + z * z + w * w <= 1e-10f;
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.GetRotation))]
        internal static class ReadPatch
        {
            private static void Postfix(ZDO __instance, ref Quaternion __result)
            { if (TryRestore(__instance, __result, out Quaternion exact)) __result = exact; }
        }

        [HarmonyPatch(typeof(ZDO), nameof(ZDO.SetRotation))]
        internal static class ChangePatch
        {
            private static void Prefix(ZDO __instance, Quaternion __0) => Invalidate(__instance, __0);
        }

        private static InvalidOperationException Unsupported() => new InvalidOperationException(
            BuildWorksLocalization.Text("placement.rotation_persistence_unsupported"));
    }
}
