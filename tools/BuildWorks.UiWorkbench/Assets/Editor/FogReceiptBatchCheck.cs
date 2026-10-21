using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogReceiptBatchCheck
{
    private static int checks;
    private static void Check(bool ok, string message) { ++checks; if (!ok) throw new InvalidOperationException(message); }
    private static void Reject(Action action) { bool refused = false; try { action(); } catch (InvalidOperationException) { refused = true; } Check(refused, "Expected bounded failure."); }
    public static void Run()
    {
        var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-fogEvidenceOutput");
        if (index < 0 || index + 1 >= args.Length) throw new InvalidOperationException("Fresh evidence output required.");
        string output = args[index + 1]; var report = new Report();
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.transform.localScale = new Vector3(.2f, 2, 2); prefab.SetActive(false);
        var material = new Material(Shader.Find("Standard")); prefab.GetComponent<Renderer>().sharedMaterial = material;
        try
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
            using (var visual = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.identity, Vector3.one, null, Vector3.zero, new Vector3(-1.8f, .95f, 0), new Vector2(.3f, .95f)))
            {
                BlueprintFogInputQueue.Sample Sample(float clock) => new BlueprintFogInputQueue.Sample(clock, new Point3(-1.8, .95, 0), .3f, .95f, false, true, false);
                BlueprintFogSimulation Make(BlueprintFogScratch scratch, BlueprintFogSimulation.StagedState staged)
                    => new BlueprintFogSimulation(assets, visual.Source.Shape, visual.Source.Pigment, visual.Source.Noise,
                        visual.FieldMinimum, visual.FieldMaximum, visual.Source.Minimum, visual.Source.Maximum, new Vector3(-1.8f, .95f, 0), 37, scratch: scratch, stagedState: staged);
                BlueprintFogChannels Pool(int count) => BlueprintFogChannels.CreateArena(assets, count, 32768, residentOwners: count);
                BlueprintFogChannels.SnapshotSource Register(BlueprintFogChannels pool) => pool.RegisterSnapshotSource(Sample(0), (scratch, staged, initial) => Make(scratch, staged));
                void Drain(BlueprintFogChannels pool) { AsyncGPUReadback.WaitAllRequests(); Check(pool.PollSnapshots(), "Receipt failed to drain."); }
                using (var pool = Pool(32))
                {
                    Check(BlueprintFogArena.MaximumFieldBytes == 256L * 1024 * 1024 &&
                        pool.SnapshotArena.ResidentCapacity == 32 && !pool.SnapshotArena.VelocityUpload && !pool.QaImmediateReceiptReads,
                        "Default field cap/residency/receipt mode differs from the house candidate.");
                    pool.EnablePerformanceProbe(); pool.PumpSources();
                    Check(pool.ReadPerformanceProbe().physicalReceiptRequests == 0, "Empty group requested GPU data.");
                    // Reserve four slots so the source group exercises slot31 as well as packed lower slots.
                    for (int i = 0; i < 4; ++i) pool.AcquireSnapshot(i, Sample(0), (scratch, staged) => Make(scratch, staged));
                    var sources = Enumerable.Range(0, 28).Select(i => Register(pool)).ToArray();
                    Reject(() => pool.QaImmediateReceiptReads = true);
                    foreach (var source in sources) pool.CaptureSource(source, Sample(.005f));
                    pool.PumpSources();
                    Check(sources.Last().Snapshot.Slot == 31 && pool.ReadPerformanceProbe().physicalReceiptRequests == 1, "28 owners did not share one physical request.");
                    AsyncGPUReadback.WaitAllRequests();
                    Check(sources.All(s => s.Snapshot.HasReceipt && s.Snapshot.State.State == BlueprintFogSnapshotState.Phase.Pending && s.Inputs.Count == 1), "Callback committed before PollSnapshots.");
                    pool.PumpSources(); var perf = pool.ReadPerformanceProbe();
                    Check(perf.valid && perf.physicalReceiptRequests == 1 && perf.physicalReceiptBytes == 2048 && perf.flows[0].accepted == 28, "Physical and logical receipt accounting differ.");
                    Check(sources.All(s => s.Inputs.Count == 0 && s.Snapshot.Bank == 0 && s.Snapshot.State.ConfirmedTick == 0), "Metadata group changed a bank or lost a receipt.");
                    report.groupOwners = 28; report.groupPhysicalReads = 1;
                    var old = sources.Select(s => new BlueprintFogChannels.ReceiptRead(s.Snapshot, s.Snapshot.pending)).ToArray();
                    var poison = new NativeArray<uint>(32 * 16, Allocator.Temp);
                    try
                    {
                        for (int i = 0; i < 32; ++i) poison[i * 16 + 8] = 4;
                        pool.ReceiveReceiptBatch(old, poison);
                        Check(!pool.RequiresSnapshotTeardown && pool.ReadPerformanceProbe().flows[0].callbacks == 28, "Duplicate callback affected state or metrics.");
                        foreach (var source in sources) pool.CaptureSource(source, Sample(source == sources[0] ? .105f : .006f));
                        pool.PumpSources(); pool.ReceiveReceiptBatch(old, poison);
                        Check(!pool.RequiresSnapshotTeardown && sources.All(s => s.Snapshot.nativePending && !s.Snapshot.HasReceipt), "Stale callback cleared a new pending token.");
                    }
                    finally { poison.Dispose(); }
                    Drain(pool);
                    Check(sources[0].Snapshot.Bank == 1 && sources[0].Snapshot.State.ConfirmedTick == 3 && sources.Skip(1).All(s => s.Snapshot.Bank == 0), "Mixed physical/metadata group did not retain separate commits.");
                }
                foreach (bool shortRead in new[] { false, true }) using (var pool = Pool(2))
                {
                    var sources = new[] { Register(pool), Register(pool) };
                    foreach (var source in sources) pool.CaptureSource(source, Sample(.005f));
                    pool.PumpSources(); var entries = sources.Select(s => new BlueprintFogChannels.ReceiptRead(s.Snapshot, s.Snapshot.pending)).ToArray();
                    var words = shortRead ? new NativeArray<uint>(1, Allocator.Temp) : default;
                    try { pool.ReceiveReceiptBatch(entries, words); }
                    finally { if (words.IsCreated) words.Dispose(); }
                    AsyncGPUReadback.WaitAllRequests(); pool.PollSnapshots();
                    Check(pool.RequiresSnapshotTeardown && sources.All(s => s.Inputs.Count == 1 && s.Snapshot.State.State == BlueprintFogSnapshotState.Phase.Faulted), "Failed/short shared read committed or lost an owner's history.");
                    ++report.failedReadCases;
                }
                using (var pool = Pool(2))
                {
                    pool.QaImmediateReceiptReads = true; pool.EnablePerformanceProbe();
                    var sources = new[] { Register(pool), Register(pool) };
                    foreach (var source in sources) pool.CaptureSource(source, Sample(.005f));
                    pool.PumpSources(); Drain(pool);
                    Check(pool.ReadPerformanceProbe().physicalReceiptRequests == 2 && sources.All(s => s.Inputs.Count == 0),
                        "Explicit immediate baseline did not keep two physical requests.");
                }
                using (var pool = Pool(2))
                {
                    pool.EnablePerformanceProbe(); var sources = new[] { Register(pool), Register(pool) };
                    foreach (var source in sources) pool.CaptureSource(source, Sample(.005f));
                    pool.QaFailNextReceiptRequest = true; Reject(pool.PumpSources);
                    Check(pool.RequiresSnapshotTeardown && sources.All(s => s.Inputs.Count == 1 && !s.Snapshot.nativePending && s.Snapshot.State.State == BlueprintFogSnapshotState.Phase.Faulted), "Request creation failure orphaned a pending owner.");
                    Check(pool.ReadPerformanceProbe().physicalReceiptRequests == 0, "Failed request was counted as an accepted physical request.");
                }
                using (var pool = Pool(2))
                {
                    var first = Register(pool);
                    var failed = pool.RegisterSnapshotSource(Sample(0), (scratch, staged, initial) => throw new InvalidOperationException("Second source factory failure."));
                    pool.CaptureSource(first, Sample(.005f)); pool.CaptureSource(failed, Sample(.005f));
                    Reject(pool.PumpSources); AsyncGPUReadback.WaitAllRequests();
                    Check(first.Snapshot.HasReceipt && first.Snapshot.State.State == BlueprintFogSnapshotState.Phase.Pending, "Partial throw did not flush the submitted prefix.");
                    pool.RemoveSource(failed); pool.PumpSources();
                    Check(first.Inputs.Count == 0 && !pool.RequiresSnapshotTeardown, "Partial throw left PumpSources locked or lost its good receipt.");
                }
                using (var pool = BlueprintFogChannels.CreateArena(assets, 3, 32768, residentOwners: 2))
                {
                    pool.EnablePerformanceProbe(); var sources = new[] { Register(pool), Register(pool) };
                    foreach (var source in sources) pool.CaptureSource(source, Sample(.105f));
                    pool.PumpSources(); var retiring = sources[0].Snapshot; var neighbor = sources[1].Snapshot;
                    Check(pool.ReadPerformanceProbe().physicalReceiptRequests == 1 && sources.All(s => s.Snapshot.nativePending && s.Snapshot.evictionPending),
                        "Paging-enabled shared group did not retain both pending velocity reads.");
                    pool.RemoveSource(sources[0]); pool.RetireSnapshot(retiring);
                    Check(!pool.TryReleaseSnapshot(retiring) && !sources[0].Released, "Retirement released a pending shared receipt/velocity owner.");
                    Drain(pool); pool.PumpSources();
                    Check(!sources[0].Released && neighbor.Inputs.Count == 0 && neighbor.State.ConfirmedTick == 3 && pool.Count == 2,
                        "Shared retirement skipped map confirmation or lost its neighbor's commit.");
                    AsyncGPUReadback.WaitAllRequests(); pool.PumpSources();
                    Check(sources[0].Released && pool.Count == 1 && neighbor.State.State == BlueprintFogSnapshotState.Phase.Ready && !pool.RequiresSnapshotTeardown,
                        "Shared receipt/velocity retirement leaked its slot or faulted the neighbor.");
                }
                using (var pool = Pool(1))
                {
                    pool.EnablePerformanceProbe(); var owner = pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged));
                    pool.CaptureSnapshot(owner, Sample(.005f)); Check(pool.TrySubmitSnapshot(owner, out var stamp), "Direct submission did not start.");
                    var old = new[] { new BlueprintFogChannels.ReceiptRead(owner, stamp) }; Drain(pool);
                    Check(pool.ReadPerformanceProbe().physicalReceiptRequests == 1, "Direct submission stopped requesting immediately.");
                    pool.RetireSnapshot(owner); Check(!pool.TryReleaseSnapshot(owner), "Retirement skipped its receipt.");
                    AsyncGPUReadback.WaitAllRequests(); Check(pool.TryReleaseSnapshot(owner), "Retirement did not drain.");
                    var next = pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged));
                    pool.CaptureSnapshot(next, Sample(.005f)); pool.TrySubmitSnapshot(next, out var nextStamp);
                    var poison = new NativeArray<uint>(16, Allocator.Temp);
                    try { poison[8] = 4; pool.ReceiveReceiptBatch(old, poison); }
                    finally { poison.Dispose(); }
                    Check(next.nativePending && !next.HasReceipt && !pool.RequiresSnapshotTeardown, "Slot ABA poisoned a new owner.");
                    Drain(pool); Check(next.LastSubmission.Sequence == nextStamp.Sequence, "New slot receipt was lost.");
                }
                using (var pool = Pool(2))
                {
                    var sources = new[] { Register(pool), Register(pool) };
                    foreach (var source in sources) pool.CaptureSource(source, Sample(.005f));
                    pool.PumpSources(); var owners = sources.Select(s => s.Snapshot).ToArray(); pool.Dispose();
                    Check(pool.Count == 0 && pool.FieldBytes == 0 && owners.All(o => !o.nativePending && o.State.CanReleaseResources), "Grouped pending Dispose released before drain or leaked resources.");
                }
                report.passed = true;
            }
        }
        catch (Exception error) { report.passed = false; report.failure = error.ToString(); Debug.LogException(error); }
        finally { UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(prefab); }
        report.checks = checks; File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(report, true));
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
    [Serializable] private sealed class Report
    {
        public bool passed, gameHostTested = false, gpuTimingsMeasured = false;
        public int checks, groupOwners, groupPhysicalReads, failedReadCases;
        public string failure = "", scope = "Actual shared receipt callback/scheduler GPU guards; not native house cadence or appearance";
    }
}
