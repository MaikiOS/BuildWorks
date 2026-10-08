using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

// Loads the actual plugin without adding game DLLs to Assets or starting host lifecycle.
public static class WorldLayoutRunner
{
    public static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        string root = args[Array.IndexOf(args, "-buildWorksRoot") + 1];
        string expected = args[Array.IndexOf(args, "-buildWorksSha") + 1];
        string output = Path.Combine(root, "artifacts", "fix-0.19.22", "world-layout-checks");
        var captured = new StringWriter();
        TextWriter previous = Console.Out;
        int result = 1;
        string hostError = null;
        Application.LogCallback onLog = (message, trace, type) =>
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                hostError = message + "\n" + trace;
        };
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("PlayMode is forbidden.");
            string plugin = Path.Combine(root, "artifacts", "bin", "BuildWorks.dll");
            using (var stream = File.OpenRead(plugin))
            using (var sha = SHA256.Create())
                if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "") != expected)
                    throw new InvalidOperationException("BuildWorks DLL changed after test compilation.");
            Console.SetOut(captured);
            Application.logMessageReceived += onLog;
            Assembly checks = Assembly.LoadFrom(Path.Combine(output, "WorldLayoutChecks.exe"));
            result = (int)checks.EntryPoint.Invoke(null, new object[] { new[] { root } });
            Assembly actual = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "BuildWorks");
            if (Path.GetFullPath(actual.Location) != Path.GetFullPath(plugin))
                throw new InvalidOperationException("Wrong plugin assembly loaded.");
            Type piece = actual.GetType("OstrixMods.BuildWorks.PrecisionPlacementSession", true)
                .GetField("selectedPiece", BindingFlags.Instance | BindingFlags.NonPublic).FieldType;
            if (piece.Assembly.GetName().Name != "assembly_valheim")
                throw new InvalidOperationException("Workbench Piece stub used instead of the real host type.");
            if (hostError != null) throw new InvalidOperationException("Unexpected host lifecycle: " + hostError);
            if (result != 0) throw new InvalidOperationException("World layout checks failed.");
            captured.WriteLine("BUILDWORKS_WORLD_LAYOUT_OK " + expected);
        }
        catch (Exception exception)
        {
            result = 1;
            captured.WriteLine(exception);
        }
        finally
        {
            Application.logMessageReceived -= onLog;
            Console.SetOut(previous);
            File.WriteAllText(Path.Combine(output, "result.txt"), captured.ToString());
            Debug.Log(captured.ToString());
            EditorApplication.Exit(result);
        }
    }
}
