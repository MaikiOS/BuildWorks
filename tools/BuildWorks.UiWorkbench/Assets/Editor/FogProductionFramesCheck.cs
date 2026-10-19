using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Production pool/cadence, procedural geometry: this diagnoses scheduling, not native house appearance.
[InitializeOnLoad]
public static class FogProductionFramesCheck
{
    private const string Pending = "BuildWorks.FogProductionFrames.Pending";
    private static IEnumerator run;
    private static Report currentReport;
    private static string reportPath;
    private static int lastFrame = -1;
    static FogProductionFramesCheck()
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
    public static void RunNativeCadence()
    { SessionState.SetBool(Pending + ".NativeCadence", true); Run(); }
    public static void RunNativeControls()
    { SessionState.SetBool(Pending + ".NativeControls", true); RunNativeCadence(); }
    public static void RunPollingCandidate()
    { SessionState.SetBool(Pending + ".PollingCandidate", true); RunNativeCadence(); }
    public static void RunPostCaptureCandidate()
    { SessionState.SetBool(Pending + ".PostCaptureCandidate", true); RunPollingCandidate(); }
    public static void RunTelemetryMeasurement()
    { SessionState.SetBool(Pending + ".TelemetryMeasurement", true); RunNativeCadence(); }
    private static void Advance()
    {
        try
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (run.MoveNext()) return;
            EditorApplication.update -= Advance; EditorApplication.Exit(currentReport != null && currentReport.passed ? 0 : 1);
        }
        catch (Exception error)
        {
            EditorApplication.update -= Advance; (run as IDisposable)?.Dispose();
            if (currentReport != null && currentReport.cases.Count > 0)
            {
                currentReport.cases[currentReport.cases.Count - 1].failure = error.Message;
                File.WriteAllText(reportPath, JsonUtility.ToJson(currentReport, true));
            }
            Debug.LogException(error); EditorApplication.Exit(1);
        }
    }
    private static IEnumerator Check()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        bool nativeCadence = SessionState.GetBool(Pending + ".NativeCadence", false); SessionState.SetBool(Pending + ".NativeCadence", false);
        bool controls = SessionState.GetBool(Pending + ".NativeControls", false); SessionState.SetBool(Pending + ".NativeControls", false);
        bool polling = SessionState.GetBool(Pending + ".PollingCandidate", false); SessionState.SetBool(Pending + ".PollingCandidate", false);
        bool postCapture = SessionState.GetBool(Pending + ".PostCaptureCandidate", false); SessionState.SetBool(Pending + ".PostCaptureCandidate", false);
        bool profiling = SessionState.GetBool(Pending + ".TelemetryMeasurement", false); SessionState.SetBool(Pending + ".TelemetryMeasurement", false);
        if (nativeCadence) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = 36; }
        string modeName = profiling ? "TelemetryMeasurement" : postCapture ? "PostCaptureCandidate" : polling ? "PollingCandidate" : controls ? "NativeControls" : nativeCadence ? "NativeCadence" : "Controls";
        string output = Argument("-fogEvidenceOutput") ?? Path.Combine(root, "artifacts/checkpoint-234/" + (polling ? "polling-candidate" : controls ? "native-controls" : nativeCadence ? "native-cadence" : "production-frames")); Directory.CreateDirectory(output);
        var report = currentReport = new Report(); reportPath = Path.Combine(output, "verification.json");
        report.requestedFrameRate = nativeCadence ? 36 : -1; report.mode = modeName; report.runId = Argument("-fogEvidenceId"); report.explicitPolling = polling;
        report.postCapturePump = postCapture;
        report.measurementOnly = profiling;
        using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
        {
            for (int mode = controls ? 1 : nativeCadence ? 3 : 0; mode < (controls ? 3 : 4); ++mode)
            {
                var result = new Case { contact = (mode & 1) != 0, capture = (mode & 2) != 0 };
                string folder = Path.Combine(output, "case-" + mode); Directory.CreateDirectory(folder);
                report.cases.Add(result);
                var test = Measure(assets, result, folder, profiling ? 20 : nativeCadence ? 48 : 22, polling, postCapture, profiling);
                try { while (test.MoveNext()) yield return null; }
                finally { (test as IDisposable)?.Dispose(); }
                File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(report, true));
            }
        }
        report.passed = report.cases.Count == (controls ? 2 : nativeCadence ? 1 : 4) && report.cases.All(value => value.passed);
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(report, true));
        Debug.Log("BUILDWORKS_PRODUCTION_FRAMES_" + (report.passed ? "OK" : "FAILED"));
    }
    private static IEnumerator Measure(BlueprintFogAssets assets, Case result, string output, int duration, bool polling, bool postCapture, bool profiling)
    {
        var body = new Vector2(.3f, .95f);
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.localScale = new Vector3(.2f, 2, 2); wall.SetActive(false);
        var paint = new Material(Shader.Find("Standard")); paint.color = new Color(.64f, .31f, .12f); wall.GetComponent<Renderer>().sharedMaterial = paint;
        var cameraObject = new GameObject("Production fog diagnostic camera"); var camera = cameraObject.AddComponent<Camera>();
        var probe = cameraObject.AddComponent<FogRenderProbe>(); camera.enabled = false;
        camera.transform.position = new Vector3(8, 4, -9); camera.transform.LookAt(new Vector3(2.4f, 1, .75f)); camera.fieldOfView = 55;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .2f, .3f);
        camera.allowHDR = true; camera.renderingPath = RenderingPath.DeferredShading; camera.depthTextureMode = DepthTextureMode.Depth;
        var visuals = new BlueprintFogVisual[28];
        var history = Enumerable.Range(0, 28).Select(_ => new List<BlueprintFogInputQueue.Sample>()).ToArray();
        RenderTexture target = null; Texture2D pixels = null;
        try
        {
            using (var pool = BlueprintFogChannels.CreateArena(assets, 32, 32768, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D))
            using (var atlas = new BlueprintFogAtlas(assets, pool))
            using (var trace = new StreamWriter(Path.Combine(output, "frames.jsonl")))
            {
                if (profiling) pool.EnablePerformanceProbe();
                Require(pool.SnapshotCapacity == 32 && pool.SnapshotArena.BlockCapacity == 32768 && pool.SnapshotArena.LedgerCells == 16384 && pool.SnapshotArena.ResidentCapacity == 4,
                    "Diagnostic pool differs from production32/32768/Spatial3D/4resident.");
                Vector3 Actor(float clock)
                {
                    float x = clock < 10 ? -1.8f : clock < 14 ? Mathf.Lerp(-1.8f, .4f, (clock - 10) / 4) : clock < 16 ? .4f : Mathf.Lerp(.4f, -1.8f, (clock - 16) / 3);
                    return new Vector3(x, .95f, result.contact ? .03f : 10);
                }
                for (int i = 0; i < visuals.Length; ++i)
                {
                    Vector3 origin = new Vector3(i % 7 * .8f, 0, i / 7 * .5f);
                    var visual = new BlueprintFogVisual(assets, wall, origin + Vector3.up, Quaternion.identity, Vector3.one, null,
                        origin, Actor(0), body, pool, deferPreparation: true, noiseOrigin: Vector3.zero);
                    visuals[i] = visual;
                    using (var preparing = visual.Prepare().GetEnumerator()) while (preparing.MoveNext()) yield return null;
                    visual.Root.SetActive(true);
                }
                atlas.PreparePublishedLayout(visuals);
                var snapshots = new BlueprintFogChannels.SnapshotChannel[28];
                if (result.capture)
                {
                    target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
                    pixels = new Texture2D(960, 540, TextureFormat.RGB24, false);
                }
                float clock = 0, observed = 0;
                int[] gcBefore = profiling ? new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) } : null;
                if (profiling) result.managedBytesStart = GC.GetTotalMemory(false);
                double started = Time.realtimeSinceStartupAsDouble, nextTrace = 0, nextCapture = 0;
                int[] lastTicks = new int[28]; double[] lastProgress = new double[28]; int firstFrame = Time.frameCount;
                var cursor = typeof(BlueprintFogChannels).GetField("sourceCursor", BindingFlags.Instance | BindingFlags.NonPublic);
                void PollCandidate()
                {
                    if (!polling) return;
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    // Test-only: no retirement here; a runtime change must cover the actual slot array and late replies.
                    foreach (var visual in visuals)
                    {
                        var owner = visual.LogicalSource.Snapshot; if (owner == null) continue;
                        if (owner.nativePending) { ++result.receiptUpdates; owner.request.Update(); }
                        if (owner.evictionNativePending) { ++result.velocityUpdates; owner.evictionRequest.Update(); }
                        if (owner.recoveryNativePending) { ++result.recoveryUpdates; owner.recoveryRequest.Update(); }
                    }
                    double elapsed = watch.Elapsed.TotalMilliseconds;
                    result.pollingMilliseconds += elapsed;
                    result.maxPollingMilliseconds = Math.Max(result.maxPollingMilliseconds, elapsed);
                }
                void Trace(string reason = null)
                {
                    double now = Time.realtimeSinceStartupAsDouble - started;
                    var owners = new Owner[28];
                    for (int i = 0; i < owners.Length; ++i)
                    {
                        var source = visuals[i].LogicalSource; var owner = source.Snapshot; int tick = owner?.State.ConfirmedTick ?? 0;
                        if (tick != lastTicks[i]) { lastTicks[i] = tick; lastProgress[i] = now; }
                        owners[i] = new Owner { slot = i, queued = source.Inputs.Count, pending = source.Inputs.PendingCount, tick = tick,
                            state = owner?.State.State.ToString(), flags = owner?.LastFlags ?? 0, resident = owner != null && owner.Velocity,
                            nativePending = owner?.nativePending ?? false, evictionPending = owner?.evictionPending ?? false,
                            evictionNativePending = owner?.evictionNativePending ?? false, secondsWithoutProgress = now - lastProgress[i],
                            error = source.Error ?? owner?.Error, accepted = history[i].Count };
                    }
                    trace.WriteLine(JsonUtility.ToJson(new Frame { hostFrame = Time.frameCount, seconds = now, clock = clock, reason = reason,
                        cursor = (int)cursor.GetValue(pool), resident = pool.ResidentSnapshotCount, fieldBytes = pool.FieldBytes,
                        velocityRamBytes = pool.VelocityRamBytes, owners = owners })); trace.Flush();
                    result.maxQueue = Math.Max(result.maxQueue, owners.Max(owner => owner.queued));
                }
                while (Time.realtimeSinceStartupAsDouble - started < duration)
                {
                    try
                    {
                        clock += Mathf.Min(Time.deltaTime, BlueprintFogSimulation.Step * 8);
                        if (Tick(clock) > Tick(observed))
                        {
                            Vector3 actor = Actor(clock);
                            for (int i = 0; i < visuals.Length; ++i)
                            {
                                visuals[i].AdvanceTo(clock, actor, body, true);
                                var inputs = visuals[i].LogicalSource.Inputs; history[i].Add(inputs.At(inputs.Count - 1));
                            }
                            observed = clock;
                        }
                        result.maxQueue = Math.Max(result.maxQueue, visuals.Max(value => value.LogicalSource.Inputs.Count));
                        // Building.Pump followed by the caller's shared-pool Pump, as in ProjectFog.Update.
                        PollCandidate();
                        pool.PumpSources(); pool.PumpSources();
                        Require(!pool.RequiresSnapshotTeardown && pool.FieldBytes <= 98304000 && pool.ResidentSnapshotCount <= 4, "Production pool ownership/budget failed.");
                        var failed = visuals.FirstOrDefault(value => value.LogicalSource.Error != null || value.LogicalSource.Inputs.Faulted);
                        if (failed != null) throw new InvalidOperationException(failed.LogicalSource.Error ?? "Input queue faulted.");
                        double elapsed = Time.realtimeSinceStartupAsDouble - started;
                        if (result.capture && clock >= 10 && elapsed >= nextCapture)
                        {
                            for (int i = 0; i < snapshots.Length; ++i) snapshots[i] = visuals[i].Snapshot;
                            atlas.PreparePublished(visuals, snapshots);
                            probe.Render = (image, destination) => atlas.Render(camera, image, destination, visuals, new Vector3(-4, 1, 0), Vector3.right, 0, snapshots);
                            var watch = System.Diagnostics.Stopwatch.StartNew(); Capture(camera, probe, target, pixels, Path.Combine(output, "frame-" + result.captures.ToString("D4") + ".png"), result, profiling);
                            result.captureMilliseconds.Add(watch.Elapsed.TotalMilliseconds); ++result.captures;
                            if (postCapture)
                            {
                                // Test whether synchronous capture leaves completed readbacks idle until the next host frame.
                                PollCandidate(); pool.PumpSources(); ++result.postCapturePumps;
                            }
                            nextCapture = Time.realtimeSinceStartupAsDouble - started + .1;
                        }
                        if (elapsed >= nextTrace) { Trace(); nextTrace = elapsed + .25; }
                    }
                    catch (Exception error) { result.failure = error.Message; Trace(result.failure); break; }
                    yield return null;
                }
                result.inputSeconds = Time.realtimeSinceStartupAsDouble - started; result.clock = clock;
                result.hostFrames = Time.frameCount - firstFrame; result.hostFramesPerSecond = result.hostFrames / result.inputSeconds;
                if (profiling) result.performanceWindow = pool.ReadPerformanceProbe();
                if (profiling) { result.managedBytesEnd = GC.GetTotalMemory(false); result.gcCollections = new[] { GC.CollectionCount(0)-gcBefore[0], GC.CollectionCount(1)-gcBefore[1], GC.CollectionCount(2)-gcBefore[2] }; }
                File.WriteAllText(Path.Combine(output, "accepted-inputs.json"), JsonUtility.ToJson(new Accepted {
                    owners = history.Select(list => new Series { samples = list.Select(sample => new Input { clock = sample.Clock,
                        actor = new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z), radius = sample.Radius, halfHeight = sample.HalfHeight,
                        emit = sample.Emit, possible = sample.EmissionPossible, nearby = sample.Nearby }).ToArray() }).ToArray() }, true));
                if (result.failure != null) yield break;
                double drainStart = Time.realtimeSinceStartupAsDouble;
                while (visuals.Any(value => value.LogicalSource.Inputs.Count != 0))
                {
                    if (Time.realtimeSinceStartupAsDouble - drainStart > 5) { result.failure = "Accepted inputs did not drain within5s."; Trace(result.failure); yield break; }
                    PollCandidate(); pool.PumpSources(); Trace("drain"); yield return null;
                }
                result.drainSeconds = Time.realtimeSinceStartupAsDouble - drainStart;
                if (profiling) result.performanceDrained = pool.ReadPerformanceProbe();
                Trace("drained");
                using (var codec = new FogSparseStorageProbe())
                for (int i = 0; i < visuals.Length; ++i)
                {
                    var visual = visuals[i]; var source = visual.LogicalSource; var owner = source.Snapshot;
                    double promote = Time.realtimeSinceStartupAsDouble;
                    while (!pool.EnsureSnapshotVelocity(owner))
                    { Require(Time.realtimeSinceStartupAsDouble - promote <= 5, "Confirmed velocity promotion timed out."); pool.PollSnapshots(); yield return null; }
                    Vector3 Local(BlueprintFogInputQueue.Sample sample) => new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z);
                    Vector3 initial = Actor(0) - visual.Origin;
                    using (var reference = new BlueprintFogSimulation(assets, visual.Source.Shape, visual.Source.Pigment, visual.Source.Noise,
                        visual.FieldMinimum, visual.FieldMaximum, visual.Source.Minimum, visual.Source.Maximum, initial, 37,
                        noiseOffset: visual.NoiseOffset, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D))
                    {
                        foreach (var sample in history[i]) reference.AdvanceTo(sample.Clock, Local(sample), new Vector2(sample.Radius, sample.HalfHeight), sample.Emit, sample.EmissionPossible);
                        foreach (string name in new[] { "previous", "inputPosition", "inputTime", "tick", "emissionRevision" })
                        { var field = typeof(BlueprintFogSimulation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); Require(Equals(field.GetValue(owner.simulation), field.GetValue(reference)), "CPU integrator differs: " + i + "/" + name); ++result.scalarComparisons; }
                        pool.BindSnapshot(owner); pool.SnapshotArena.Dispatch("BeginStep"); pool.SnapshotArena.Dispatch("RestoreFields", 24, 20, 20);
                        codec.BeginComparison(); var fields = pool.SnapshotArena.Scratch;
                        codec.Compare(fields.ReadColour, reference.Smoke); codec.Compare(fields.ReadOrigin, FogRuntimeSparseCheck.Current(reference, "origin"));
                        codec.Compare(fields.ReadSurface, reference.Surface); codec.Compare(owner.Velocity, reference.Flow);
                        for (int f = 0; f < 3; ++f) codec.Compare(owner.Ledger[f], FogRuntimeSparseCheck.Buffer(reference, new[] { "away", "peak", "state" }[f]));
                        codec.RequireClean(); result.fieldComparisons += 7;
                    }
                    yield return null;
                }
                result.passed = true;
            }
        }
        finally
        {
            foreach (var visual in visuals) visual?.Dispose();
            if (target) RenderTexture.ReleaseTemporary(target); if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
            UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(paint); UnityEngine.Object.DestroyImmediate(cameraObject);
        }
    }
    private static void Capture(Camera camera, FogRenderProbe probe, RenderTexture target, Texture2D pixels, string path, Case result, bool profiling)
    {
        var old = camera.targetTexture; var active = RenderTexture.active; float aspect = camera.aspect; int before = probe.Frames;
        try
        {
            camera.targetTexture = target; camera.aspect = 960f / 540;
            long time = profiling ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            double Lap() { long end = System.Diagnostics.Stopwatch.GetTimestamp(); double ms = (end - time) * 1000d / System.Diagnostics.Stopwatch.Frequency; time = end; return ms; }
            camera.Render(); if (profiling) result.captureRenderMilliseconds += Lap(); Require(probe.Frames == before + 1, "Capture skipped the real atlas callback.");
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); pixels.Apply();
            if (profiling) result.captureReadPixelsMilliseconds += Lap();
            byte[] png = pixels.EncodeToPNG(); result.captureBytes += png.Length;
            if (profiling) result.capturePngMilliseconds += Lap();
            Require(result.captureBytes <= 256L * 1024 * 1024, "Capture byte budget exceeded."); File.WriteAllBytes(path, png);
            if (profiling) { result.captureDiskMilliseconds += Lap(); ++result.captureStageCount; }
        }
        finally { camera.targetTexture = old; camera.aspect = aspect; RenderTexture.active = active; }
    }
    private static int Tick(float clock) => Mathf.FloorToInt(clock / BlueprintFogSimulation.Step + .0001f);
    private static string Argument(string name)
    { var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null; }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report { public bool passed, gameHostTested, nativeHouseGeometryTested, gpuTimingsMeasured, explicitPolling, postCapturePump, measurementOnly; public int requestedFrameRate; public string mode, runId, configuration = "32slots/32768blocks/Spatial3D/4resident;28procedural walls"; public List<Case> cases = new List<Case>(); }
    [Serializable] private sealed class Case { public bool passed, contact, capture; public string failure; public int maxQueue, captures, hostFrames, scalarComparisons, fieldComparisons, receiptUpdates, velocityUpdates, recoveryUpdates, postCapturePumps, captureStageCount; public long captureBytes, managedBytesStart, managedBytesEnd; public int[] gcCollections; public float clock; public double inputSeconds, drainSeconds, hostFramesPerSecond, pollingMilliseconds, maxPollingMilliseconds, captureRenderMilliseconds, captureReadPixelsMilliseconds, capturePngMilliseconds, captureDiskMilliseconds; public FogPerformanceCounters.Snapshot performanceWindow, performanceDrained; public List<double> captureMilliseconds = new List<double>(); }
    [Serializable] private sealed class Frame { public int hostFrame, cursor, resident; public double seconds; public float clock; public string reason; public long fieldBytes, velocityRamBytes; public Owner[] owners; }
    [Serializable] private sealed class Owner { public int slot, queued, pending, tick, accepted; public uint flags; public string state, error; public bool resident, nativePending, evictionPending, evictionNativePending; public double secondsWithoutProgress; }
    [Serializable] private sealed class Accepted { public Series[] owners; }
    [Serializable] private sealed class Series { public Input[] samples; }
    [Serializable] private sealed class Input { public float clock, radius, halfHeight; public Vector3 actor; public bool emit, possible, nearby; }
}
