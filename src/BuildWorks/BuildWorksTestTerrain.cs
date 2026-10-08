#if BUILDWORKS_TEST_STAND
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Native QA terrain needs the same bound during preparation and subsequent heightmap reloads.
    [HarmonyPatch]
    internal static class BuildWorksTestTerrain
    {
        internal const string Marker = "BuildWorks.QaYard";

        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TerrainComp), "LevelTerrain");
            yield return AccessTools.Method(typeof(TerrainComp), "ApplyToHeightmap");
        }

        private static float Limit(TerrainComp compiler)
        {
            ZNetView view = compiler ? compiler.GetComponent<ZNetView>() : null;
            if (!view || !view.IsValid() || string.IsNullOrEmpty(view.GetZDO().GetString(Marker, ""))) return 8f;
            try
            {
                BuildWorksTestIsolation.AssertPinned();
                string[] identity = BuildWorksTestIsolation.ReadIdentityReceipt();
                ZNet network = ZNet.instance;
                World world = network ? network.GetWorld() : null;
                return world != null && world.m_fileSource == FileHelpers.FileSource.Local &&
                    world.m_worldName == identity[2] && world.m_uid == long.Parse(identity[3], CultureInfo.InvariantCulture) &&
                    view.GetZDO().GetString(Marker, "") == identity[1] && network.IsServer() && !network.IsDedicated() &&
                    !ZNet.IsOpenServer() && network.GetPeers().Count == 0 ? 16f : 8f;
            }
            catch (Exception exception)
            {
                Debug.LogError("BuildWorks QA terrain bound refused: " + exception);
                return 8f;
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var code = instructions.ToList();
            var bounds = code.Where(c => c.opcode == OpCodes.Ldc_R4 && Math.Abs((float)c.operand) == 8f).ToArray();
            if (bounds.Length != 2) throw new MissingMethodException("QA terrain clamp contract changed.");
            LocalBuilder limit = generator.DeclareLocal(typeof(float));
            yield return new CodeInstruction(OpCodes.Ldarg_0);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BuildWorksTestTerrain), nameof(Limit)));
            yield return new CodeInstruction(OpCodes.Stloc, limit);
            foreach (CodeInstruction instruction in code)
            {
                bool bound = bounds.Contains(instruction);
                bool negative = bound && (float)instruction.operand < 0;
                if (bound) { instruction.opcode = OpCodes.Ldloc; instruction.operand = limit; }
                yield return instruction;
                if (negative) yield return new CodeInstruction(OpCodes.Neg);
            }
        }
    }
}
#endif
