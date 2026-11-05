using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

public static class NativePlacementInteropAcceptance
{
    const string ForeignOwner = "augusdogus.Buildheim.hammer";
    static readonly MethodInfo Rotation = AccessTools.PropertySetter(typeof(Transform), "rotation");
    static readonly MethodInfo Position = AccessTools.PropertySetter(typeof(Transform), "position");
    static readonly MethodInfo WrappedRotation = AccessTools.Method(typeof(NativePlacementInteropAcceptance), nameof(SetRotation));
    static readonly MethodInfo WrappedPosition = AccessTools.Method(typeof(NativePlacementInteropAcceptance), nameof(SetPosition));
    static MethodInfo transpiler;
    static MethodBase original;
    static Type hostPlayer, hostLocation;
    static int checks;

    // Only the IL shape of an independent setter wrapper is needed; never execute a host method.
    static void SetRotation(Transform transform, Quaternion value) => throw new NotSupportedException();
    static void SetPosition(Transform transform, Vector3 value) => throw new NotSupportedException();
    static IEnumerable<CodeInstruction> WrapSetters(IEnumerable<CodeInstruction> input)
    {
        foreach (var instruction in input)
        {
            var copy = new CodeInstruction(instruction);
            if (copy.Calls(Rotation) || copy.Calls(Position))
            {
                copy.operand = copy.Calls(Rotation) ? WrappedRotation : WrappedPosition;
                copy.opcode = OpCodes.Call;
            }
            yield return copy;
        }
    }

    static List<CodeInstruction> Native() => PatchProcessor.GetOriginalInstructions(original, out ILGenerator _);
    static List<CodeInstruction> Apply(MethodInfo method, IEnumerable<CodeInstruction> input) =>
        ((IEnumerable<CodeInstruction>)method.Invoke(null, new object[] { input })).ToList();
    static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
        ++checks;
    }

    static string Fingerprint(List<CodeInstruction> code)
    {
        var targets = new Dictionary<Label, int>();
        for (int i = 0; i < code.Count; ++i)
            foreach (var label in code[i].labels) targets[label] = i;
        string Operand(object value)
        {
            if (Equals(value, WrappedRotation)) value = Rotation;
            if (Equals(value, WrappedPosition)) value = Position;
            if (Equals(value, ForeignInstantiate)) value = nativeInstantiate;
            if (value is Label label) return "branch:" + targets[label];
            if (value is Label[] labels) return string.Join(",", labels.Select(l => targets[l]));
            if (value is Type type) return "type:" + type.FullName;
            if (value is MemberInfo member) return (member.DeclaringType?.FullName ?? "<global>") + ":" + member;
            return value?.ToString() ?? "";
        }
        return string.Join("\n", code.Select(i =>
            ((i.Calls(Rotation) || i.Calls(Position) || i.Calls(WrappedRotation) || i.Calls(WrappedPosition))
                ? "setter" : i.opcode.Name) + ":" + Operand(i.operand) + ":" +
            string.Join(",", i.blocks.Select(b => b.blockType + ":" + b.catchType))));
    }


    static GameObject ForeignFactory(GameObject source, Vector3 position, Quaternion rotation) => throw new NotSupportedException();
    static readonly MethodInfo ForeignInstantiate = AccessTools.Method(typeof(NativePlacementInteropAcceptance), nameof(ForeignFactory));
    static MethodInfo nativeInstantiate;
    static bool IsInstantiate(CodeInstruction instruction) => instruction.opcode == OpCodes.Call && instruction.operand is MethodInfo m &&
        m.DeclaringType == typeof(UnityEngine.Object) && m.Name == "Instantiate" && m.IsGenericMethod &&
        m.GetGenericArguments()[0] == typeof(GameObject) && m.GetParameters().Length == 3 &&
        m.GetParameters()[1].ParameterType == typeof(Vector3) && m.GetParameters()[2].ParameterType == typeof(Quaternion);
    static IEnumerable<CodeInstruction> WrapFactory(IEnumerable<CodeInstruction> input)
    {
        foreach (var instruction in input)
        {
            var copy = new CodeInstruction(instruction);
            if (IsInstantiate(copy)) { copy.opcode = OpCodes.Call; copy.operand = ForeignInstantiate; }
            yield return copy;
        }
    }
    static List<CodeInstruction> ApplyFactory(MethodInfo method, IEnumerable<CodeInstruction> input, ILGenerator generator) =>
        ((IEnumerable<CodeInstruction>)method.Invoke(null, method.GetParameters().Length == 2
            ? new object[] { input, generator } : new object[] { input })).ToList();
    static void FactoryChecks(Assembly plugin, bool legacy)
    {
        var patchType = plugin.GetType("OstrixMods.BuildWorks.BuildWorksPlugin", true)
            .GetNestedType("NativePieceScalePlacementPatch", BindingFlags.NonPublic);
        MethodInfo placement = AccessTools.Method(hostPlayer, "PlacePiece");
        MethodInfo own = AccessTools.Method(patchType, "Transpiler");
        MethodInfo foreignMethod = AccessTools.Method(typeof(NativePlacementInteropAcceptance), nameof(WrapFactory));
        var initial = PatchProcessor.GetOriginalInstructions(placement, out ILGenerator initialGenerator);
        nativeInstantiate = (MethodInfo)initial.Single(IsInstantiate).operand;
        bool rejected = false;
        try { ApplyFactory(own, WrapFactory(initial), initialGenerator); }
        catch (Exception e) { rejected = e.GetBaseException().Message == "PlacePiece native Instantiate hook count: 0"; }
        Require(rejected, "Earlier unsupported factory replacement must reproduce/refuse count0");
        if (legacy) return;
        foreach (bool foreignFirst in new[] { false, true })
        {
            var bw = new Patch(new HarmonyMethod(own), foreignFirst ? 1 : 0, "ostrmod.buildworks");
            var foreign = new Patch(new HarmonyMethod(foreignMethod), foreignFirst ? 0 : 1, "fixture.factory");
            var sorted = PatchProcessor.GetSortedPatchMethods(placement, foreignFirst ? new[] { foreign, bw } : new[] { bw, foreign });
            Require(sorted.SequenceEqual(new[] { own, foreignMethod }), "Factory guards must sort before a normal-priority foreign transpiler");
            var originalCode = PatchProcessor.GetOriginalInstructions(placement, out ILGenerator generator);
            string nativeFingerprint = Fingerprint(originalCode);
            var composed = originalCode;
            foreach (var method in sorted) composed = ApplyFactory(method, composed, generator);
            int pre = composed.FindIndex(i => (i.operand as MethodInfo)?.Name == "PrepareNativeInstantiation");
            int factory = composed.FindIndex(i => i.Calls(ForeignInstantiate));
            int post = composed.FindIndex(i => (i.operand as MethodInfo)?.Name == "CompleteNativeInstantiation");
            Require(pre >= 0 && pre < factory && factory < post &&
                composed.Count(i => i.Calls(ForeignInstantiate)) == 1 && !composed.Any(IsInstantiate),
                "Precheck -> original/foreign factory -> postcheck composition changed");
            Require(factory >= 9 && post == factory + 3, "Factory argument and result stack layout changed");
            Require(composed[factory - 9].opcode == OpCodes.Stloc && composed[factory - 8].opcode == OpCodes.Stloc &&
                composed[factory - 7].opcode == OpCodes.Stloc && composed[factory - 6].opcode == OpCodes.Ldloc &&
                composed[factory - 5].opcode == OpCodes.Call && composed[factory - 4].opcode == OpCodes.Stloc &&
                composed[factory - 3].opcode == OpCodes.Ldloc && composed[factory - 2].opcode == OpCodes.Ldloc &&
                composed[factory - 1].opcode == OpCodes.Ldloc, "Factory source/position/rotation spills changed");
            var reduced = composed.Select(i => new CodeInstruction(i)).ToList();
            reduced[factory].labels.AddRange(reduced[factory - 9].labels);
            reduced.RemoveRange(factory + 1, 3); reduced.RemoveRange(factory - 9, 9);
            Require(Fingerprint(reduced) == nativeFingerprint, "Factory composition changed native guards, branches or exception regions");
        }
        var missing = PatchProcessor.GetOriginalInstructions(placement, out ILGenerator badGenerator);
        missing.RemoveAll(IsInstantiate);
        rejected = false;
        try { ApplyFactory(own, missing, badGenerator); }
        catch (Exception e) { rejected = e.GetBaseException().Message == "PlacePiece native Instantiate hook count: 0"; }
        Require(rejected, "A missing native factory must remain fail closed");
        var duplicate = PatchProcessor.GetOriginalInstructions(placement, out badGenerator);
        duplicate.Add(new CodeInstruction(duplicate.Single(IsInstantiate)));
        rejected = false;
        try { ApplyFactory(own, duplicate, badGenerator); }
        catch (Exception e) { rejected = e.GetBaseException().Message == "PlacePiece native Instantiate hook count: 2"; }
        Require(rejected, "An ambiguous factory must remain fail closed");
        var pointer = PatchProcessor.GetOriginalInstructions(placement, out badGenerator);
        pointer.Single(IsInstantiate).opcode = OpCodes.Ldftn;
        rejected = false;
        try { ApplyFactory(own, pointer, badGenerator); }
        catch (Exception e) { rejected = e.GetBaseException().Message == "PlacePiece native Instantiate hook count: 0"; }
        Require(rejected, "A factory method pointer must not be treated as a call with arguments");
        var finalizer = AccessTools.Method(patchType, "Finalizer");
        Require(finalizer != null && finalizer.ReturnType == typeof(Exception) && finalizer.GetParameters().Length == 1,
            "Factory failure cleanup finalizer is missing");
        System.Console.WriteLine("PASS preserved factory / foreign composition, both registration orders, strict invalid shapes; no native/foreign method executed");
    }

    public static void Verify() => RunInWorkbench(false);
    public static void VerifyLegacyFactoryFailure() => RunInWorkbench(true);
    private static void RunInWorkbench(bool legacy)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string dll = legacy ? Path.Combine(root, "artifacts/owner-bugs-20261029/native-baseline326.dll")
            : Path.Combine(root, "artifacts/bin/BuildWorks.dll");
        var args = new List<string> { dll,
            "C:/Program Files (x86)/Steam/steamapps/common/Valheim/valheim_Data/Managed",
            Path.GetFullPath(Path.Combine(root,"../BepInEx/core")) };
        if (legacy) args.Add("--legacy-factory-red");
        int result = Main(args.ToArray());
        if (result == 0) Debug.Log(legacy ? "BUILDWORKS_FACTORY_RED_REPRODUCED" : "BUILDWORKS_NATIVE_INTEROP_OK checks=" + checks);
        UnityEditor.EditorApplication.Exit(result);
    }

    static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3 && args.Length != 4) throw new ArgumentException("plugin DLL, host Managed directory, BepInEx core directory required");
            var roots = new[] { Path.GetDirectoryName(Path.GetFullPath(args[0])), args[1], args[2] };
            AppDomain.CurrentDomain.AssemblyResolve += (_, e) =>
            {
                string name = new AssemblyName(e.Name).Name + ".dll";
                string file = roots.Select(r => Path.Combine(r, name)).FirstOrDefault(File.Exists);
                return file == null ? null : Assembly.LoadFrom(file);
            };
            var host = Assembly.LoadFrom(Path.Combine(args[1], "assembly_valheim.dll"));
            hostPlayer = host.GetType("Player", true); hostLocation = host.GetType("Location", true);
            var plugin = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            if (args.Length == 4 && args[3] == "--legacy-factory-red")
            {
                FactoryChecks(plugin, legacy: true);
                System.Console.WriteLine("RED reproduced: actual legacy factory transpiler rejects a preceding foreign replacement; no game method executed");
                return 0;
            }
            FactoryChecks(plugin, legacy: false);
            var type = plugin.GetType("OstrixMods.BuildWorks.BuildWorksPlugin", true)
                .GetNestedType("TransformGhostBeforeNativeValidationPatch", BindingFlags.NonPublic);
            transpiler = AccessTools.Method(type, "Transpiler");
            original = AccessTools.Method(hostPlayer, "UpdatePlacementGhost");
            var stock = Apply(transpiler, Native());
            var hook = stock.Select((i, index) => new { i, index })
                .Where(x => (x.i.operand as MethodInfo)?.Name == "ApplyPrecisionTransformBeforeValidation").ToArray();
            var ray = AccessTools.Method(hostPlayer, "PieceRayTest");
            int rayCall = stock.FindIndex(i => i.Calls(ray));
            Require(rayCall >= 2 && stock[rayCall - 2].opcode == OpCodes.Ldarg_0 &&
                (stock[rayCall - 1].operand as MethodInfo)?.Name == "BeginNativePlacementRay",
                "Scoped distance must be cleared immediately before the exact native ray call");
            Require((stock[rayCall + 8].operand as MethodInfo)?.Name == "ApplyBlueprintValidationContact",
                "Fresh contact must consume the scoped distance before the native ray result branch");
            var rayPatch = plugin.GetType("OstrixMods.BuildWorks.BuildWorksPlugin", true)
                .GetNestedType("NativePlacementRayDistancePatch", BindingFlags.NonPublic);
            var rayOriginal = PatchProcessor.GetOriginalInstructions(ray, out ILGenerator rayGenerator);
            string rayFingerprint = Fingerprint(rayOriginal);
            var rayCode = Apply(AccessTools.Method(rayPatch, "Transpiler"), rayOriginal);
            Require(rayCode.Count == rayOriginal.Count + 2 && rayCode[0].opcode == OpCodes.Ldarg_0 &&
                (rayCode[1].operand as MethodInfo)?.Name == "CaptureNativePlacementRayDistance",
                "Scoped distance must be captured inside the original ray body after native/foreign prefixes");
            Require(Fingerprint(rayCode.Skip(2).ToList()) == rayFingerprint,
                "Distance observation must preserve all original ray guards/branches/exception regions");
            int validation = stock.FindIndex(i => i.Calls(AccessTools.Method(hostLocation, "IsInsideNoBuildLocation", new[] { typeof(Vector3) })));
            Require(hook.Length == 1 && hook[0].index < validation, "Stock precision hook must precede native validation exactly once");
            bool refused = false;
            try { Apply(transpiler, WrapSetters(Native())); }
            catch (TargetInvocationException e)
            {
                refused = e.GetBaseException().Message == "Current Valheim UpdatePlacementGhost validation layout is unsupported.";
            }
            Require(refused, "Unknown pre-wrapped layout must still fail closed");
            System.Console.WriteLine("PASS stock native IL and reproduced pre-wrapped setter rejection");
            var wrapper = AccessTools.Method(typeof(NativePlacementInteropAcceptance), nameof(WrapSetters));
            foreach (bool foreignFirst in new[] { false, true })
            {
                var bw = new Patch(new HarmonyMethod(transpiler), foreignFirst ? 1 : 0, "ostrmod.buildworks");
                var foreign = new Patch(new HarmonyMethod(wrapper), foreignFirst ? 0 : 1, ForeignOwner);
                var patches = foreignFirst ? new[] { foreign, bw } : new[] { bw, foreign };
                var sorted = PatchProcessor.GetSortedPatchMethods(original, patches);
                Require(sorted.SequenceEqual(new[] { transpiler, wrapper }), "Harmony must order BuildWorks before setter wrapper for registration order foreignFirst=" + foreignFirst);
                var composed = Native();
                foreach (var method in sorted) composed = Apply(method, composed);
                Require(composed.Any(i => i.Calls(WrappedRotation)) && !composed.Any(i => i.Calls(Rotation)), "Foreign setter wrappers must remain installed");
                Require(Fingerprint(composed) == Fingerprint(stock), "Wrapper composition changed native guards, branch targets or precision hooks");
            }
            System.Console.WriteLine("PASS " + checks + " actual native IL/Harmony ordering checks; no host method or foreign assembly executed");
            return 0;
        }
        catch (Exception e) { System.Console.Error.WriteLine(e); return 1; }
    }
}
