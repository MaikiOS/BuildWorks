using System;
using System.Collections;
using System.IO;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Real Editor player frames. Blocking comparisons are verification barriers, never the polling path.
[InitializeOnLoad]
public static class FogArenaFramesCheck
{
    private const string Pending = "BuildWorks.FogArenaFrames.Pending";
    private static IEnumerator run;
    private static int lastFrame = -1;
    static FogArenaFramesCheck()
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
            EditorApplication.update -= Advance; (run as IDisposable)?.Dispose();
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    private static IEnumerator Check()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-arena-frames"); Directory.CreateDirectory(output);
        string trace = Path.Combine(output, "frames.jsonl"); File.WriteAllText(trace, "");
        var camera = new GameObject("Arena frame clock"); camera.AddComponent<Camera>().cullingMask = 0;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Owned procedural test wall"; wall.transform.position = Vector3.up;
        wall.transform.localScale = new Vector3(.2f, 2, 2); wall.SetActive(false);
        var body = new Vector2(.3f, .95f); int firstFrame = Time.frameCount;
        try
        {
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using (var source = new BlueprintFogVisual(assets, wall, Vector3.up, Quaternion.identity, Vector3.one,
                null, Vector3.zero, new Vector3(-1.8f, .95f, 0), body))
            using (var codec = new FogSparseStorageProbe())
            using (var test = new FogArenaPhysicsProbe.Probe(assets, source, body, 8192, codec))
            {
                source.Root.SetActive(false); test.CheckAll(true);
                var inputs = new[] { test.CreateInputQueue(0), test.CreateInputQueue(1) };
                for (int slot = 0; slot < 2; ++slot)
                {
                    inputs[slot].Capture(test.Observation(slot, .005f)); test.Send(slot, 0, input: inputs[slot]);
                    Require(inputs[slot].PendingMetadataOnly && !inputs[slot].CanRecover(0), "Metadata-only work escaped pending ownership.");
                }
                yield return null;
                int metadataDeadline = Time.frameCount + 180;
                while (!test.Poll()) { Require(Time.frameCount < metadataDeadline, "Metadata-only callback timed out."); yield return null; }
                test.CheckAll(true);
                Require(inputs[0].Count == 0 && inputs[1].Count == 0 && inputs[0].Owner.ConfirmedTick == 0 && inputs[1].Owner.ConfirmedTick == 0, "Metadata-only publication changed fixed state or lost confirmation.");
                int tick = 0;
                for (int batch = 0; batch < 24; ++batch)
                {
                    tick += batch % 8 + 1;
                    for (int visit = 0; visit < 2; ++visit)
                    {
                        int slot = batch % 2 == 0 ? visit : 1 - visit;
                        inputs[slot].Capture(test.Observation(slot, inputs[slot].Committed.Clock + .009f));
                        inputs[slot].Capture(test.Observation(slot, tick * BlueprintFogSimulation.Step + .005f));
                        test.Send(slot, tick, reverseCallbacks: batch % 2 == 1, input: inputs[slot]);
                    }
                    if (batch == 0)
                    {
                        bool blocked = false; try { test.Send(0, tick + 1); } catch (InvalidOperationException) { blocked = true; }
                        Require(blocked, "A pending owner accepted a second physics batch.");
                        inputs[0].Capture(test.Observation(0, tick * BlueprintFogSimulation.Step + .010f));
                        Require(inputs[0].PendingCount == 2 && inputs[0].Count == 3, "Pending capture changed the submitted prefix.");
                    }
                    yield return null;
                    int deadline = Time.frameCount + 180;
                    if (batch == 0)
                    {
                        while (!test.RepliesComplete) { Require(Time.frameCount < deadline, "Deferred replies timed out."); yield return null; }
                        for (int delay = 0; delay < 6; ++delay) yield return null;
                        Require(test.HasExpiredRequest, "No expired native handle was observed after the deferred poll.");
                    }
                    while (!test.Poll()) { Require(Time.frameCount < deadline, "Frame polling timed out."); yield return null; }
                    test.CheckAll(false);
                    if (batch > 0) Require(inputs[0].Count == 0 && inputs[1].Count == 0, "Confirmed input prefix retained or dropped its tail.");
                    File.AppendAllText(trace, JsonUtility.ToJson(new Event { frame = Time.frameCount, tick = tick, committed = test.Committed }) + "\n");
                }
                Require(tick == 108, "Frame fixture did not cover three complete1..8 step cycles.");
                var old = test.Send(0, 116); test.States[0].Retire(); test.Send(1, 116);
                Require(!test.States[0].CanReleaseResources, "Retirement freed pending GPU resources.");
                bool resetBlocked = false; try { test.ResetOwner(0); } catch (InvalidOperationException) { resetBlocked = true; }
                Require(resetBlocked, "Pending retirement reused its slot.");
                yield return null;
                int retireDeadline = Time.frameCount + 180;
                while (!test.Poll()) { Require(Time.frameCount < retireDeadline, "Retirement callback timed out."); yield return null; }
                Require(test.States[0].CanReleaseResources && test.States[0].ConfirmedTick == 116, "Retirement callback resurrected or lost the owner.");
                test.CheckAll(true); test.ResetOwner(0); test.CheckAll(true);
                Require(test.States[0].ConfirmedTick == 0 && test.States[1].ConfirmedTick == 116, "Slot reset changed its neighbour or inherited the old clock.");
                var fresh = test.Send(0, 8); test.Send(1, 124);
                Require(fresh.Generation != old.Generation && !test.States[0].Complete(old, BlueprintFogSnapshotState.Completion.Committed), "Reused slot accepted a stale receipt.");
                yield return null;
                int reuseDeadline = Time.frameCount + 180;
                while (!test.Poll()) { Require(Time.frameCount < reuseDeadline, "Slot reuse callback timed out."); yield return null; }
                test.CheckAll(true);
                int frames = Time.frameCount - firstFrame;
                Require(frames >= 33 && test.ReceiptFirst > 0 && test.Committed == 54 && test.Rejected == 0 && test.MetadataChecks == 54 &&
                    test.Fields == 232 && test.Ledgers == 174 && test.Atlases == 20, "Frame fixture coverage incomplete.");
                File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                    passed = true, frames = frames, owners = 2, committed = test.Committed, rejected = test.Rejected,
                    fieldComparisons = test.Fields, ledgerComparisons = test.Ledgers, atlasComparisons = test.Atlases, metadataComparisons = test.MetadataChecks,
                    cachedRepliesAfterExpiry = true, queuedInputsReplayed = true, capturedWhilePending = true, metadataOnlyPending = true,
                    receiptFirstHandled = true, pendingResubmitRejected = true, pendingRetireRetained = true,
                    generationReuse = true, lateReplyRejected = true, pollingHasNoWait = true,
                    verificationBarriersBetweenBatches = true, productionAllocatorTested = false, rendererTested = false,
                    wholeHouseSupported = false, gameHostTested = false, gpuTimingsMeasured = false,
                    note = "Real Unity player frames; submission/polling do not wait. Exact oracle comparisons and teardown may block. " +
                        "Procedural wall/two full grids; ordered queued samples replayed, but no production scheduler throughput, renderer, house memory or FPS proof." }, true));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(camera); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Event { public int frame, tick, committed; }
    [Serializable] private sealed class Report
    {
        public bool passed, cachedRepliesAfterExpiry, queuedInputsReplayed, capturedWhilePending, metadataOnlyPending, receiptFirstHandled, pendingResubmitRejected, pendingRetireRetained, generationReuse, lateReplyRejected,
            pollingHasNoWait, verificationBarriersBetweenBatches, productionAllocatorTested, rendererTested, wholeHouseSupported, gameHostTested, gpuTimingsMeasured;
        public int frames, owners, committed, rejected, fieldComparisons, ledgerComparisons, atlasComparisons, metadataComparisons;
        public string note;
    }
}
