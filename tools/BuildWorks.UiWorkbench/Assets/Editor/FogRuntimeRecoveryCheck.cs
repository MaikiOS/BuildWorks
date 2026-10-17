using System;
using System.Collections;
using System.IO;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
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
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
            using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
            {
                BlueprintFogInputQueue.Sample Sample(float clock) => new BlueprintFogInputQueue.Sample(clock, new Point3(0, 0, 0), .3f, .95f, true, true, true);
                BlueprintFogChannels.SnapshotChannel Acquire() => pool.AcquireSnapshot(0, Sample(0), (scratch, staged) =>
                    new BlueprintFogSimulation(assets, shape, pigment, noise, new Vector3(-1.2f, -.2f, -1.215f), new Vector3(1.2f, 2.35f, 1.215f),
                        new Vector3(-1, 0, -1), new Vector3(1, 2, 1), Vector3.zero, 37, scratch: scratch, stagedState: staged));
                var owner = Acquire();
                pool.SnapshotArena.Buffers["_StageState"].SetData(new[] { 3u });
                Require(!pool.TryGetRecoveredSnapshot(owner), "Snapshot recovery skipped GPU confirmation");
                var expired = owner.recoveryRequest;
                AsyncGPUReadback.WaitAllRequests();
                for (int frame = 0; frame < 6; ++frame) yield return null;
                Require(expired.done && expired.hasError && pool.TryGetRecoveredSnapshot(owner), "Committed recovery used expired handle or neighbour workspace");

                pool.CaptureSnapshot(owner, Sample(.105f));
                Require(!pool.TryGetRecoveredSnapshot(owner), "Accepted input reused old recovery");
                pool.TrySubmitSnapshot(owner, out var token);
                Require(!pool.TryGetRecoveredSnapshot(owner), "Pending commit reused old recovery");
                AsyncGPUReadback.WaitAllRequests(); pool.PollSnapshots();
                Require(!pool.TryGetRecoveredSnapshot(owner), "New commit reused old recovery");
                var old = owner.recoveryStamp;
                pool.CaptureSnapshot(owner, Sample(.21f)); pool.TrySubmitSnapshot(owner, out token);
                AsyncGPUReadback.WaitAllRequests(); pool.PollSnapshots();
                for (int frame = 0; frame < 6; ++frame) yield return null;
                Require(!pool.TryGetRecoveredSnapshot(owner), "In-flight recovery authorized a newer committed sequence");
                owner.ReceiveRecovery(old, 0);
                Require(!owner.recoveryReceived, "Stale recovery callback changed current request");
                AsyncGPUReadback.WaitAllRequests();
                for (int frame = 0; frame < 6; ++frame) yield return null;
                Require(pool.TryGetRecoveredSnapshot(owner), "Fresh snapshot recovery lost its copied result");

                pool.CaptureSnapshot(owner, Sample(.315f)); pool.TrySubmitSnapshot(owner, out token);
                AsyncGPUReadback.WaitAllRequests(); pool.PollSnapshots();
                Require(!pool.TryGetRecoveredSnapshot(owner), "Retirement case did not start readback");
                pool.RetireSnapshot(owner);
                Require(!pool.TryReleaseSnapshot(owner) && pool.Count == 1, "Retirement freed a ledger referenced by readback");
                AsyncGPUReadback.WaitAllRequests();
                Require(!pool.TryReleaseSnapshot(owner), "GPU release skipped confirmation after recovery");
                AsyncGPUReadback.WaitAllRequests(); Require(pool.TryReleaseSnapshot(owner), "Recovered owner release failed");
                var fresh = Acquire(); owner.ReceiveRecovery(old, 0);
                Require(fresh.Generation != owner.Generation && !pool.TryGetRecoveredSnapshot(fresh), "Reused generation inherited recovery");
                AsyncGPUReadback.WaitAllRequests();
                foreach (uint flags in new[] { 1u, 2u })
                {
                    fresh.Ledger[2].SetData(new[] { flags });
                    pool.CaptureSnapshot(fresh, Sample(flags * .001f)); pool.TrySubmitSnapshot(fresh, out token);
                    AsyncGPUReadback.WaitAllRequests(); pool.PollSnapshots();
                    Require(!pool.TryGetRecoveredSnapshot(fresh), "New sequence reused empty recovery");
                    AsyncGPUReadback.WaitAllRequests();
                    Require(!pool.TryGetRecoveredSnapshot(fresh) && fresh.Velocity, "Smoke-only or surface-only owner was released");
                }
                fresh.Ledger[2].SetData(new[] { 0u });
                pool.CaptureSnapshot(fresh, Sample(.003f)); pool.TrySubmitSnapshot(fresh, out token);
                AsyncGPUReadback.WaitAllRequests(); pool.PollSnapshots();
                Require(!pool.TryGetRecoveredSnapshot(fresh), "Failure case did not request recovery");
                fresh.ReceiveRecovery(fresh.recoveryStamp, null); AsyncGPUReadback.WaitAllRequests();
                Require(!pool.TryGetRecoveredSnapshot(fresh) && !pool.RequiresSnapshotTeardown && fresh.Velocity,
                    "Failed recovery inferred success or damaged neighbour allocator");
                pool.CaptureSnapshot(fresh, Sample(.004f)); pool.TrySubmitSnapshot(fresh, out token);
                AsyncGPUReadback.WaitAllRequests(); pool.PollSnapshots();
                Require(!pool.TryGetRecoveredSnapshot(fresh), "Dispose case did not request recovery");
                pool.Dispose();
                for (int frame = 0; frame < 6; ++frame) yield return null;
                Require(!fresh.Velocity && pool.Count == 0, "Pending recovery disposal leaked resources");
            }
            int frames = Time.frameCount - firstFrame; Require(frames >= 48, "Recovery check did not advance real player frames");
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, expiredHandleObserved = true, delayedReadbackRetained = true,
                staleRecoveryRejected = true, pendingDisposeSafe = true, snapshotRecoveryValidated = true,
                snapshotWorkspaceIsolated = true, snapshotGenerationReuse = true, snapshotPendingRetirement = true,
                snapshotSmokeAndSurfaceRetained = true, snapshotReadbackFailureRetained = true,
                frames = frames, gameHostTested = false }, true));
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
        public bool snapshotRecoveryValidated, snapshotWorkspaceIsolated, snapshotGenerationReuse, snapshotPendingRetirement;
        public bool snapshotSmokeAndSurfaceRetained, snapshotReadbackFailureRetained;
        public int frames;
    }
}
