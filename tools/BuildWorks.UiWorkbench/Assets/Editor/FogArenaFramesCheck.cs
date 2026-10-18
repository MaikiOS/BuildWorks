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
    public static void DiagnosePaging()
    { SessionState.SetBool("BuildWorks.FogPaging.Diagnostic", true); Run(); }
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
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
            using (var source = new BlueprintFogVisual(assets, wall, Vector3.up, Quaternion.identity, Vector3.one,
                null, Vector3.zero, new Vector3(-1.8f, .95f, 0), body))
            using (var codec = new FogSparseStorageProbe())
            using (var test = new FogArenaPhysicsProbe.Probe(assets, source, body, 8192, codec))
            {
                if (SessionState.GetBool("BuildWorks.FogPaging.Diagnostic", false))
                {
                    SessionState.SetBool("BuildWorks.FogPaging.Diagnostic", false);
                    string path = Path.Combine(root, "artifacts/checkpoint-217/paging-diagnostic.txt"); File.WriteAllText(path, "");
                    var diagnostic = FogVelocityPagingCheck.Frames(assets, source, body, codec, path);
                    try { while (diagnostic.MoveNext()) yield return null; }
                    finally { (diagnostic as IDisposable)?.Dispose(); }
                    yield break;
                }
                source.Root.SetActive(false); test.CheckAll(true);
                var inputs = new[] { test.InputQueue(0), test.InputQueue(1) };
                for (int slot = 0; slot < 2; ++slot)
                {
                    test.Capture(slot, test.Observation(slot, .005f)); test.Send(slot, 0, input: inputs[slot]);
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
                        test.Capture(slot, test.Observation(slot, inputs[slot].Committed.Clock + .009f));
                        test.Capture(slot, test.Observation(slot, tick * BlueprintFogSimulation.Step + .005f));
                        test.Send(slot, tick, input: inputs[slot]);
                    }
                    if (batch == 0)
                    {
                        bool blocked = false; try { test.Send(0, tick + 1); } catch (InvalidOperationException) { blocked = true; }
                        Require(blocked, "A pending owner accepted a second physics batch.");
                        test.Capture(0, test.Observation(0, tick * BlueprintFogSimulation.Step + .010f));
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
                var old = test.Send(0, 116); test.Retire(0); test.Send(1, 116);
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
                test.DeliverStale(0, old);
                Require(fresh.Generation != old.Generation && test.States[0].State == BlueprintFogSnapshotState.Phase.Pending, "Reused slot accepted a stale receipt.");
                yield return null;
                int reuseDeadline = Time.frameCount + 180;
                while (!test.Poll()) { Require(Time.frameCount < reuseDeadline, "Slot reuse callback timed out."); yield return null; }
                test.CheckAll(true);
                int frames = Time.frameCount - firstFrame, pagingStart = Time.frameCount;
                double pagingSeconds = 0;
                var paging = FogVelocityPagingCheck.Frames(assets, source, body, codec, measured: value => pagingSeconds = value);
                try { while (paging.MoveNext()) yield return null; }
                finally { (paging as IDisposable)?.Dispose(); }
                int pagingFrames = Time.frameCount - pagingStart;
                double houseSeconds = 0;
                int houseContacts = 0;
                var house = FogVelocityPagingCheck.Frames(assets, source, body, codec, measured: value => houseSeconds = value, sourceCount: 28, residentLimit: 0, contacts: value => houseContacts = value);
                try { while (house.MoveNext()) yield return null; }
                finally { (house as IDisposable)?.Dispose(); }
                var overlap = FogVelocityPagingCheck.Frames(assets, source, body, codec, sourceCount: 28, residentLimit: 0, expectOverlapShortage: true);
                try { while (overlap.MoveNext()) yield return null; }
                finally { (overlap as IDisposable)?.Dispose(); }
                Require(frames >= 33 && test.ReceiptFirst > 0 && test.Committed == 54 && test.Rejected == 0 && test.MetadataChecks == 54 &&
                    test.Fields == 232 && test.Ledgers == 174 && test.Atlases == 20, "Frame fixture coverage incomplete.");
                File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                    passed = true, runtimeChannelsValidated = true, frames = frames, owners = 2, committed = test.Committed, rejected = test.Rejected,
                    fieldComparisons = test.Fields, ledgerComparisons = test.Ledgers, atlasComparisons = test.Atlases, metadataComparisons = test.MetadataChecks,
                    cachedRepliesAfterExpiry = true, queuedInputsReplayed = true, capturedWhilePending = true, metadataOnlyPending = true,
                    receiptFirstHandled = true, pendingResubmitRejected = true, pendingRetireRetained = true,
                    generationReuse = true, lateReplyRejected = true, pollingHasNoWait = true,
                    verificationBarriersBetweenBatches = true, productionAllocatorTested = false, rendererTested = false,
                    wholeHouseSupported = false, gameHostTested = false, gpuTimingsMeasured = false,
                    velocityPagingFrames = pagingFrames, velocityPagingSamples = 288, velocityPagingComparisons = 21,
                    velocityPagingSeconds = pagingSeconds,
                    velocityHouseSamples = 2688, velocityHouseComparisons = 196, velocityHouseSeconds = houseSeconds,
                    velocityHouseContacts = houseContacts,
                    velocityOverlapRejected = true,
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
        public bool runtimeChannelsValidated, passed, cachedRepliesAfterExpiry, queuedInputsReplayed, capturedWhilePending, metadataOnlyPending, receiptFirstHandled, pendingResubmitRejected, pendingRetireRetained, generationReuse, lateReplyRejected,
            pollingHasNoWait, verificationBarriersBetweenBatches, productionAllocatorTested, rendererTested, wholeHouseSupported, gameHostTested, gpuTimingsMeasured;
        public int frames, owners, committed, rejected, fieldComparisons, ledgerComparisons, atlasComparisons, metadataComparisons;
        public int velocityPagingFrames, velocityPagingSamples, velocityPagingComparisons;
        public double velocityPagingSeconds;
        public int velocityHouseSamples, velocityHouseComparisons;
        public int velocityHouseContacts;
        public double velocityHouseSeconds;
        public bool velocityOverlapRejected;
        public string note;
    }
}
