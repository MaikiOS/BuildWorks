using System;
using System.Collections;
using System.IO;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class FogRuntimeRecoveryCheck
{
    private const string Pending = "BuildWorks.FogRecovery.Pending";
    private static IEnumerator run;
    private static int lastFrame = -1;
    static FogRuntimeRecoveryCheck()
    {
        EditorApplication.playModeStateChanged += state => {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false); run = Check(); EditorApplication.update += Advance;
        };
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PlayerSettings.colorSpace = ColorSpace.Linear;
        SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode();
    }
    private static void Advance()
    {
        try
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (run.MoveNext()) return;
            EditorApplication.update -= Advance; EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            EditorApplication.update -= Advance;
            (run as IDisposable)?.Dispose(); Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    private static IEnumerator Check()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-recovery"); Directory.CreateDirectory(output);
        var camera = new GameObject("Recovery frame camera"); camera.AddComponent<Camera>();
        var shape = new Texture3D(2, 2, 2, TextureFormat.R8, false);
        var pigment = new Texture3D(2, 2, 2, TextureFormat.RGBA32, false);
        var noise = new Texture3D(2, 2, 2, TextureFormat.R8, false);
        int firstFrame = Time.frameCount;
        try
        {
            shape.SetPixelData(new byte[8], 0); shape.Apply();
            pigment.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white, Color.white }); pigment.Apply();
            noise.SetPixelData(new byte[8], 0); noise.Apply();
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using (var simulation = new BlueprintFogSimulation(assets, shape, pigment, noise,
                new Vector3(-1.2f, -.2f, -1.215f), new Vector3(1.2f, 2.35f, 1.215f),
                new Vector3(-1, 0, -1), new Vector3(1, 2, 1), Vector3.zero, 37, new Vector3Int(32, 40, 36)))
            {
                Require(!simulation.TryGetRecovered(), "Recovery must await its GPU result");
                var expired = (AsyncGPUReadbackRequest)typeof(BlueprintFogSimulation).GetField("recoveryRequest", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulation);
                AsyncGPUReadback.WaitAllRequests();
                for (int frame = 0; frame < 6; ++frame) yield return null;
                Require(expired.done && expired.hasError, "Negative control did not cross the native request lifetime");
                Require(simulation.TryGetRecovered(), "Completed recovery did not survive six real frames without polling");

                var body = new Vector2(.3f, .95f);
                simulation.AdvanceTo(BlueprintFogSimulation.Step, Vector3.zero, body, true);
                Require(!simulation.TryGetRecovered(), "New emission reused a previous recovery result");
                simulation.AdvanceTo(2 * BlueprintFogSimulation.Step, Vector3.zero, body, true);
                AsyncGPUReadback.WaitAllRequests();
                for (int frame = 0; frame < 6; ++frame) yield return null;
                Require(!simulation.TryGetRecovered(), "Stale empty reply authorized release after another emission");
                AsyncGPUReadback.WaitAllRequests();
                for (int frame = 0; frame < 6; ++frame) yield return null;
                Require(simulation.TryGetRecovered(), "Fresh recovery was not retained after request expiry");

                simulation.AdvanceTo(3 * BlueprintFogSimulation.Step, Vector3.zero, body, true);
                Require(!simulation.TryGetRecovered(), "Pending disposal case did not issue a new request");
                simulation.Dispose(); AsyncGPUReadback.WaitAllRequests();
                for (int frame = 0; frame < 6; ++frame) yield return null;
                bool rejected = false;
                try { simulation.TryGetRecovered(); } catch (ObjectDisposedException) { rejected = true; }
                Require(rejected, "Pending callback resurrected a disposed simulation");
            }
            int frames = Time.frameCount - firstFrame; Require(frames >= 24, "Recovery check did not advance real player frames");
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, expiredHandleObserved = true, delayedReadbackRetained = true,
                staleRecoveryRejected = true, pendingDisposeSafe = true, frames = frames, gameHostTested = false }, true));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(camera); UnityEngine.Object.DestroyImmediate(shape);
            UnityEngine.Object.DestroyImmediate(pigment); UnityEngine.Object.DestroyImmediate(noise);
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, expiredHandleObserved, delayedReadbackRetained, staleRecoveryRejected, pendingDisposeSafe, gameHostTested;
        public int frames;
    }
}
