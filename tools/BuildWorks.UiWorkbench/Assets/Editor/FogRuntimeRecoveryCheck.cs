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
            int sourceStartFrame = Time.frameCount, releasedSources = 0;
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
            using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
            {
                BlueprintFogInputQueue.Sample Sample(float clock, float x, bool contact = true)
                    => new BlueprintFogInputQueue.Sample(clock, new Point3(x, 1, 0), .3f, .95f, true, contact, true);
                BlueprintFogSimulation Make(BlueprintFogScratch scratch, BlueprintFogSimulation.StagedState staged, BlueprintFogInputQueue.Sample initial)
                    => new BlueprintFogSimulation(assets, shape, pigment, noise, new Vector3(-1.2f, -.2f, -1.215f), new Vector3(1.2f, 2.35f, 1.215f),
                        new Vector3(-1, 0, -1), new Vector3(1, 2, 1), new Vector3((float)initial.Actor.X, (float)initial.Actor.Y, (float)initial.Actor.Z), 37,
                        initialClock: initial.Clock, scratch: scratch, stagedState: staged);
                var a = pool.RegisterSnapshotSource(Sample(0, 0, false), Make, () => ++releasedSources);
                var b = pool.RegisterSnapshotSource(Sample(0, 0, false), Make, () => ++releasedSources);
                pool.CaptureSource(a, Sample(.005f, 1)); pool.CaptureSource(b, Sample(.005f, -1)); pool.PumpSources();
                var old = a.Snapshot;
                Require(old != null && b.Snapshot == null && b.Inputs.Count == 1, "Occupied slot dropped or evicted a waiting source");
                pool.CaptureSource(a, Sample(.01f, 2, false)); pool.CaptureSource(b, Sample(.01f, -2));
                for (int frame = 0; frame < 6; ++frame) yield return null;
                pool.PumpSources();
                Require(old.recoveryNativePending && a.Inputs.Count == 1 && a.Inputs.Committed.Clock == .005f, "Recovery barrier did not retain its queued noncontact tail");
                pool.CaptureSource(a, Sample(.02f, 3)); pool.PumpSources();
                Require(old.State.State == BlueprintFogSnapshotState.Phase.Pending && a.Inputs.PendingCount == 2, "Reentry during recovery did not replay the accepted path");
                for (int frame = 0; frame < 6; ++frame) yield return null;
                pool.PumpSources();
                Require(!old.recoveryNativePending && !old.State.CanReleaseResources, "Empty ledger retired a source while the latest observation still had contact");
                pool.CaptureSource(a, Sample(.025f, 4, false)); pool.PumpSources();
                for (int frame = 0; frame < 6; ++frame) yield return null;
                pool.PumpSources();
                Require(old.State.CanReleaseResources, "Recovered source did not retire while retaining noncontact tail");
                pool.CaptureSource(a, Sample(.026f, 5)); pool.PumpSources();
                Require(a.Inputs.Count == 2 && a.Snapshot == old && b.Snapshot == null, "Reentry undid retirement or lost its accepted tail");
                for (int frame = 0; frame < 6; ++frame) yield return null;
                pool.PumpSources();
                Require(b.Snapshot != null && a.Snapshot == null && a.Inputs.Count == 2 && !old.Velocity,
                    "Retiring source seized the slot before the waiting source");
                var retiring = b.Snapshot; pool.RemoveSource(b);
                Require(releasedSources == 0 && retiring.Velocity, "Removing source freed borrowed textures before GPU drain");
                for (int frame = 0; frame < 6; ++frame) yield return null;
                pool.PumpSources();
                for (int frame = 0; frame < 6; ++frame) yield return null;
                pool.PumpSources();
                Require(b.Released && releasedSources == 1 && !retiring.Velocity && a.Snapshot != null && a.Snapshot.Generation > old.Generation &&
                    a.Inputs.Committed.Clock == .02f && a.Inputs.PendingCount == 2 && old.State.CanReleaseResources,
                    "Source removal/reacquisition changed its baseline, old generation or queued replay");
                for (int frame = 0; frame < 6; ++frame) yield return null;
                pool.PumpSources(); pool.CaptureSource(a, Sample(.027f, 6, false));
                bool drained = false;
                for (int frame = 0; frame < 120; ++frame)
                {
                    yield return null; pool.PumpSources();
                    if (a.Snapshot == null && a.Inputs.Count == 0) { drained = true; break; }
                }
                Require(drained && a.Inputs.Committed.Clock == .027f && a.Inputs.Committed.Actor.X == 6,
                    "Noncontact accepted tail was dropped or empty generations retired before replaying it");
                pool.RemoveSource(a); Require(a.Released && releasedSources == 2, "Dormant source cleanup was not exactly once");
                bool closed = false; try { pool.CaptureSource(a, Sample(.028f, 7)); } catch (ArgumentException) { closed = true; }
                Require(closed, "Removed logical source accepted input");
                var failing = pool.RegisterSnapshotSource(Sample(.03f, 0, false), (scratch, staged, initial) => {
                    var simulation = Make(scratch, staged, initial); UnityEngine.Object.DestroyImmediate(pool.SnapshotArena.Program); return simulation;
                }, () => ++releasedSources);
                pool.CaptureSource(failing, Sample(.031f, 1));
                bool failed = false; try { pool.PumpSources(); } catch (Exception) { failed = true; }
                Require(failed && pool.RequiresSnapshotTeardown && pool.Count == 1 && failing.Snapshot == null,
                    "Failed source initialization did not retain its orphaned GPU owner");
                pool.RemoveSource(failing);
                Require(!failing.Released && releasedSources == 2, "Failed initialization released borrowed source before whole-pool drain");
                pool.Dispose(); pool.Dispose();
                Require(failing.Released && releasedSources == 3 && pool.Count == 0, "Failed initialization cleanup was early, duplicated or omitted");
            }
            Require(releasedSources == 3 && Time.frameCount - sourceStartFrame >= 42, "Source teardown duplicated release or did not run real frames");
            int frames = Time.frameCount - firstFrame; Require(frames >= 48, "Recovery check did not advance real player frames");
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, expiredHandleObserved = true, delayedReadbackRetained = true,
                staleRecoveryRejected = true, pendingDisposeSafe = true, snapshotRecoveryValidated = true,
                snapshotWorkspaceIsolated = true, snapshotGenerationReuse = true, snapshotPendingRetirement = true,
                snapshotSmokeAndSurfaceRetained = true, snapshotReadbackFailureRetained = true,
                logicalSourcesValidated = true, sourceFairness = true, sourceRecoveryReentry = true, sourceRetirementReentry = true,
                sourceRemovalAfterDrain = true, sourceAcceptedTailReplayed = true, sourceFrames = Time.frameCount - sourceStartFrame,
                sourceFailedAcquireRetained = true,
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
        public bool logicalSourcesValidated, sourceFairness, sourceRecoveryReentry, sourceRetirementReentry, sourceRemovalAfterDrain, sourceAcceptedTailReplayed;
        public bool sourceFailedAcquireRetained;
        public int sourceFrames;
        public int frames;
    }
}
