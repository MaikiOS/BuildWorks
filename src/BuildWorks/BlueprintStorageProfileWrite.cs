using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;

namespace OstrixMods.BuildWorks
{
    // Observe this request's successful native writer and replacement. Never change player data or file contents.
    internal sealed class BlueprintStorageProfileWrite : IDisposable
    {
        private static BlueprintStorageProfileWrite active;
        private static readonly FieldInfo WriterPath = AccessTools.Field(typeof(FileWriter), "m_path");
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly string path;
        private readonly Func<string, string> verify;
        private FileWriter writer;
        private int finished, replacing, replaced;
        internal string ProfileHash { get; private set; }
        internal string Reason { get; private set; }
        internal bool Completed => Reason == null && finished == 1 && replacing == 1 && replaced == 1 && ProfileHash != null;

        private BlueprintStorageProfileWrite(string path, Func<string, string> verify) { this.path = Path.GetFullPath(path); this.verify = verify; }

        internal static BlueprintStorageProfileWrite Begin(string path, Func<string, string> verify)
        {
            if (active != null || verify == null || WriterPath == null) throw new InvalidOperationException("Profile write observer unavailable or already in use.");
            var harmony = new Harmony("ostrmod.buildworks");
            Install(harmony, AccessTools.Method(typeof(FileWriter), nameof(FileWriter.Finish)), nameof(BeforeFinish), nameof(AfterFinish));
            var replacement = typeof(FileHelpers).GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method => method.Name == "ReplaceOldFile" &&
                method.GetParameters().Length == 5 && method.GetParameters()[4].ParameterType == typeof(FileHelpers.FileSource));
            Install(harmony, replacement, nameof(BeforeReplace), nameof(AfterReplace));
            return active = new BlueprintStorageProfileWrite(path, verify);
        }

        private static void Install(Harmony harmony, MethodInfo target, string prefix, string postfix)
        {
            if (target == null) throw new MissingMethodException("Native profile write contract changed.");
            var before = AccessTools.Method(typeof(BlueprintStorageProfileWrite), prefix);
            var after = AccessTools.Method(typeof(BlueprintStorageProfileWrite), postfix);
            var patches = Harmony.GetPatchInfo(target);
            if (patches?.Prefixes.Any(patch => patch.PatchMethod == before) == true && patches.Postfixes.Any(patch => patch.PatchMethod == after)) return;
            harmony.Patch(target, prefix: new HarmonyMethod(before), postfix: new HarmonyMethod(after));
        }

        private static bool OnThread => active != null && Thread.CurrentThread.ManagedThreadId == active.thread;
        private static void BeforeFinish(FileWriter __instance, out bool __state)
        {
            __state = false;
            if (!OnThread) return;
            try
            {
                if (__instance.m_fileSource != FileHelpers.FileSource.Local ||
                    Path.GetFullPath((string)WriterPath.GetValue(__instance)) != active.path + ".new") return;
                if (active.writer != null || __instance.Status == FileWriter.WriterStatus.CloseSucceeded)
                    throw new InvalidOperationException("Repeated or already-finished profile writer cannot prove this save request.");
                active.writer = __instance; __state = true;
            }
            catch (Exception error) { active.Reason = error.Message; }
        }
        private static void AfterFinish(FileWriter __instance, bool __state)
        {
            if (!__state || !OnThread) return;
            try
            {
                if (__instance.Status != FileWriter.WriterStatus.CloseSucceeded) throw new IOException("The current Local profile writer did not flush successfully.");
                active.ProfileHash = active.verify(active.path + ".new"); active.finished++;
            }
            catch (Exception error) { active.Reason = error.Message; }
        }
        private static bool Matches(object[] args) => OnThread && args.Length == 5 && args[4] is FileHelpers.FileSource source &&
            source == FileHelpers.FileSource.Local && args[0] is string save && args[1] is string temporary && args[2] is string old &&
            Path.GetFullPath(save) == active.path && Path.GetFullPath(temporary) == active.path + ".new" && Path.GetFullPath(old) == active.path + ".old";
        private static void BeforeReplace(object[] __args, out bool __state)
        {
            __state = false;
            try
            {
                if (!Matches(__args)) return;
                if (active.Reason != null || active.finished != 1 || active.replacing != 0) throw new IOException("Profile replacement did not follow this request's successful writer.");
                active.replacing++; __state = true;
            }
            catch (Exception error) { if (OnThread) active.Reason = error.Message; }
        }
        private static void AfterReplace(bool __state)
        {
            if (!__state || !OnThread) return;
            if (File.Exists(active.path + ".new")) active.Reason = "Native profile replacement left the temporary file behind.";
            else active.replaced++;
        }

        public void Dispose() { if (ReferenceEquals(active, this)) active = null; }
    }
}
