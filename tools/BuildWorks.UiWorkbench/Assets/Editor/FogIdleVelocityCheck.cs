using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Private experiment, not a runtime prewarm policy or acceptance of approximate history.
public static class FogIdleVelocityCheck
{
    private static readonly int[] Captures = { 368, 384, 400, 416, 440, 480 };
    private static readonly FieldInfo Simulation = typeof(BlueprintFogVisual).GetField("simulation", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly string[] IntegrationNames = { "previous", "inputPosition", "inputTime", "tick", "emissionRevision" };
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../..")), output = Path.Combine(root, "artifacts/fog-idle-velocity");
        Directory.CreateDirectory(output); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PlayerSettings.colorSpace = ColorSpace.Linear;
        var host = new GameObject("Idle velocity experiment"); var camera = host.AddComponent<Camera>(); var probe = host.AddComponent<FogRenderProbe>();
        camera.fieldOfView = 49; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .2f, .3f);
        camera.allowHDR = true; camera.renderingPath = RenderingPath.DeferredShading; camera.depthTextureMode = DepthTextureMode.Depth;
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.transform.localScale = new Vector3(.2f, 2, 2); prefab.SetActive(false);
        var original = new Material(Shader.Find("Standard")); original.color = new Color(.64f, .31f, .12f); prefab.GetComponent<Renderer>().sharedMaterial = original;
        var rows = new List<Row>();
        try
        {
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            for (int scenario = 0; scenario < 2; ++scenario)
            {
                Quaternion rotation = Quaternion.Euler(0, scenario * 90, 0); Vector3 origin = new Vector3(scenario * 8, 0, scenario * 16);
                camera.transform.position = origin + rotation * new Vector3(4, 2.3f, -5); camera.transform.LookAt(origin + rotation * new Vector3(0, 1, .1f));
                BlueprintFogInputQueue.Sample At(int tick)
                {
                    float t = tick * BlueprintFogSimulation.Step + (tick == 0 ? 0 : .005f), phase = t - 12;
                    float x = phase < 0 ? (scenario == 0 ? -1.8f : -.85f) : phase < .4f ? Mathf.Lerp(scenario == 0 ? -1.8f : -.85f, 0, phase / .4f)
                        : phase < 1.2f ? 0 : phase < 1.6f ? Mathf.Lerp(0, 1.8f, (phase - 1.2f) / .4f) : 1.8f;
                    float z = phase < 0 && scenario == 1 ? Mathf.Sin(t * 2.1f) * .7f : 0;
                    Vector3 actor = rotation * new Vector3(x, .95f, z);
                    // Match Visual's world-to-local float rounding before either solver receives the observation.
                    actor = origin + actor - origin;
                    return new BlueprintFogInputQueue.Sample(t, new OstrixMods.BuildWorks.Geometry.Point3(actor.x, actor.y, actor.z),
                        phase > .8f && phase < 1.2f ? .37f : .3f, phase > .8f && phase < 1.2f ? 1.1f : .95f,
                        scenario == 1 || phase >= 0, true, true);
                }
                var initial = At(0);
                using (var visual = new BlueprintFogVisual(assets, prefab, origin + Vector3.up, rotation, Vector3.one, null, origin, origin + Actor(initial), Body(initial), noiseOrigin: Vector3.zero))
                {
                    var continuous = visual.Simulation; var frames = new Dictionary<int, Frame>(); var checkpoints = new Dictionary<int, Checkpoint>();
                    for (int tick = 8; tick <= 480; tick += 8)
                    {
                        var sample = At(tick); visual.AdvanceTo(sample.Clock, origin + Actor(sample), Body(sample), sample.Emit);
                        if (tick == 360) Require(Fields(continuous).Take(3).All(data => data.All(word => word == 0)), "Idle experiment created matter before contact.");
                        if (Captures.Contains(tick)) frames.Add(tick, Capture(continuous, visual, sample, camera, probe, output, "s" + scenario + "-continuous-" + tick));
                        if (tick == 240 || tick == 400)
                        {
                            var saved = Save(continuous);
                            Require(saved.fields[3].Any(word => word != 0), "Checkpoint control has no accumulated velocity.");
                            if (tick == 400) Require(saved.fields.Take(3).Any(data => data.Any(word => word != 0)), "Active checkpoint control has no matter/deformation.");
                            checkpoints.Add(tick, saved);
                        }
                    }
                    foreach (int variant in new[] { 0, 2, 4, 8, 12, 240, 400 })
                    {
                        int checkpointTick = variant >= 240 ? variant : 0, window = checkpointTick == 240 ? 4 : checkpointTick == 400 ? 0 : variant;
                        int startTick = checkpointTick != 0 ? checkpointTick : window == 0 ? 360 : (360 - window * 30) / 8 * 8;
                        // Exact original observation timestamps, including the fractional initial clock; no resampling.
                        var baseline = At(startTick);
                        var row = new Row { scenario = scenario, windowSeconds = window, initialClock = baseline.Clock, exactReplayControl = window == 12,
                            checkpointTick = checkpointTick, checkpointRestored = checkpointTick != 0 };
                        using (var candidate = new BlueprintFogSimulation(assets, visual.Source.Shape, visual.Source.Pigment, visual.Source.Noise,
                            visual.FieldMinimum, visual.FieldMaximum, visual.Source.Minimum, visual.Source.Maximum, Actor(baseline), 37,
                            initialClock: baseline.Clock, noiseOffset: visual.NoiseOffset))
                        {
                            if (checkpointTick != 0)
                            {
                                var restoreTimer = Stopwatch.StartNew(); Restore(candidate, checkpoints[checkpointTick]);
                                FogRuntimeSparseCheck.Read(candidate.Flow); restoreTimer.Stop(); row.synchronizedRestoreMilliseconds = restoreTimer.Elapsed.TotalMilliseconds;
                                row.checkpointFieldLedgerBytes = checkpoints[checkpointTick].Bytes;
                            }
                            var timer = Stopwatch.StartNew();
                            int nextTick = (startTick / 8 + 1) * 8;
                            for (int tick = nextTick; tick <= 360; tick += 8) Step(candidate, At(tick));
                            FogRuntimeSparseCheck.Read(candidate.Flow); timer.Stop(); row.synchronizedPrewarmMilliseconds = timer.Elapsed.TotalMilliseconds;
                            for (int tick = Math.Max(368, startTick + 8); tick <= 480; tick += 8)
                            {
                                var sample = At(tick); Step(candidate, sample); if (!Captures.Contains(tick)) continue;
                                var actual = Capture(candidate, visual, sample, camera, probe, output, "s" + scenario + "-w" + window + "-checkpoint" + checkpointTick + "-" + tick);
                                var expected = frames[tick];
                                for (int field = 0; field < 4; ++field) for (int i = 0; i < actual.fields[field].Length; ++i)
                                {
                                    ushort a = actual.fields[field][i], b = expected.fields[field][i];
                                    if (a != b) ++row.differentHalfWords;
                                    double delta = Math.Abs(Mathf.HalfToFloat(a) - Mathf.HalfToFloat(b));
                                    Require(!double.IsNaN(delta) && !double.IsInfinity(delta), "Non-finite idle field.");
                                    row.fieldMaxDelta = Math.Max(row.fieldMaxDelta, delta);
                                }
                                for (int ledger = 0; ledger < 3; ++ledger) for (int i = 0; i < actual.ledger[ledger].Length; ++i)
                                    if (actual.ledger[ledger][i] != expected.ledger[ledger][i]) ++row.differentLedgerWords;
                                for (int route = 0; route < 2; ++route)
                                {
                                    row.meanRgbDelta = Math.Max(row.meanRgbDelta, FogRuntimePairCheck.Delta(expected.images[route], actual.images[route]));
                                    for (int i = 0; i < actual.images[route].Length; ++i)
                                    {
                                        Color a = actual.images[route][i], b = expected.images[route][i];
                                        row.localRgbDelta = Math.Max(row.localRgbDelta, Math.Max(Math.Abs(a.r - b.r), Math.Max(Math.Abs(a.g - b.g), Math.Abs(a.b - b.b))));
                                    }
                                    ++row.imageComparisons;
                                }
                            }
                            Simulation.SetValue(visual, continuous);
                        }
                        row.withinExistingImageLimits = row.meanRgbDelta < .0001 && row.localRgbDelta < .03;
                        if (row.exactReplayControl || row.checkpointRestored) Require(row.differentHalfWords == 0 && row.differentLedgerWords == 0 && row.localRgbDelta == 0,
                            "Full replay differs from continuous oracle: " + JsonUtility.ToJson(row));
                        rows.Add(row);
                    }
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(original); UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(host); }
        Require(rows.Count == 14 && rows.Count(row => row.exactReplayControl) == 2 && rows.Count(row => row.checkpointRestored) == 4
            && rows.All(row => row.imageComparisons == (row.checkpointTick == 400 ? 6 : 12)), "Idle sweep coverage incomplete.");
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report { collected = true, exactReplayValidated = true,
            runtimePolicyImplemented = false, wholeHouseSupported = false, gameHostTested = false, scenarios = 2, rows = rows.ToArray() }, true));
    }
    private static void Step(BlueprintFogSimulation simulation, BlueprintFogInputQueue.Sample sample)
        => simulation.AdvanceTo(sample.Clock, Actor(sample), Body(sample), sample.Emit);
    private static Vector3 Actor(BlueprintFogInputQueue.Sample sample) => new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z);
    private static Vector2 Body(BlueprintFogInputQueue.Sample sample) => new Vector2(sample.Radius, sample.HalfHeight);
    private static ushort[][] Fields(BlueprintFogSimulation simulation) => new[] { FogRuntimeSparseCheck.Read(simulation.Smoke),
        FogRuntimeSparseCheck.Read(FogRuntimeSparseCheck.Current(simulation, "origin")), FogRuntimeSparseCheck.Read(simulation.Surface), FogRuntimeSparseCheck.Read(simulation.Flow) };
    private static Checkpoint Save(BlueprintFogSimulation simulation)
    {
        var result = new Checkpoint { fields = Fields(simulation), ledger = Ledgers(simulation),
            integration = IntegrationNames.Select(name => typeof(BlueprintFogSimulation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulation)).ToArray() };
        return result;
    }
    private static uint[][] Ledgers(BlueprintFogSimulation simulation)
    {
        var result = new uint[3][];
        for (int i = 0; i < 3; ++i)
        {
            result[i] = new uint[i == 2 ? 1 : 1024];
            FogRuntimeSparseCheck.Buffer(simulation, new[] { "away", "peak", "state" }[i]).GetData(result[i]);
        }
        return result;
    }
    private static void Restore(BlueprintFogSimulation simulation, Checkpoint checkpoint)
    {
        var targets = new[] { simulation.Smoke, FogRuntimeSparseCheck.Current(simulation, "origin"), simulation.Surface, simulation.Flow };
        var upload = new Texture3D(96, 80, 80, TextureFormat.RGBAHalf, false);
        try
        {
            for (int i = 0; i < targets.Length; ++i)
            { upload.SetPixelData(checkpoint.fields[i], 0); upload.Apply(false, false); Graphics.CopyTexture(upload, targets[i]); }
        }
        finally { UnityEngine.Object.DestroyImmediate(upload); }
        for (int i = 0; i < 3; ++i) FogRuntimeSparseCheck.Buffer(simulation, new[] { "away", "peak", "state" }[i]).SetData(checkpoint.ledger[i]);
        for (int i = 0; i < IntegrationNames.Length; ++i)
            typeof(BlueprintFogSimulation).GetField(IntegrationNames[i], BindingFlags.Instance | BindingFlags.NonPublic).SetValue(simulation, checkpoint.integration[i]);
    }
    private static Frame Capture(BlueprintFogSimulation simulation, BlueprintFogVisual visual, BlueprintFogInputQueue.Sample sample, Camera camera, FogRenderProbe probe, string output, string name)
    {
        var old = visual.Simulation;
        try
        {
            Simulation.SetValue(visual, simulation);
            // Bind this exact state without asking Visual to move a clock backwards.
            typeof(BlueprintFogVisual).GetField("time", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(visual, sample.Clock);
            visual.AdvanceTo(sample.Clock, visual.Origin + Actor(sample), Body(sample), sample.Emit);
            var images = new Color[2][];
            for (int route = 0; route < 2; ++route)
            {
                int mode = route;
                images[route] = FogRuntimePairCheck.Capture(camera, probe, (image, target) => {
                    if (mode == 0) Graphics.Blit(image, target); else visual.RenderVolume(camera, image, target, visual.Origin, Vector3.right, 0);
                }, output, name + "-" + route);
            }
            return new Frame { fields = Fields(simulation), ledger = Ledgers(simulation), images = images };
        }
        finally { Simulation.SetValue(visual, old); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Frame { internal ushort[][] fields; internal uint[][] ledger; internal Color[][] images; }
    private sealed class Checkpoint
    {
        internal ushort[][] fields; internal uint[][] ledger; internal object[] integration;
        internal long Bytes => fields.Sum(data => (long)data.Length * 2) + ledger.Sum(data => (long)data.Length * 4);
    }
    [Serializable] private sealed class Row
    {
        public int scenario, windowSeconds, imageComparisons, checkpointTick;
        public float initialClock;
        public bool exactReplayControl, withinExistingImageLimits, checkpointRestored;
        public long differentHalfWords, differentLedgerWords, checkpointFieldLedgerBytes;
        public double fieldMaxDelta, meanRgbDelta, localRgbDelta, synchronizedPrewarmMilliseconds, synchronizedRestoreMilliseconds;
    }
    [Serializable] private sealed class Report
    {
        public bool collected, exactReplayValidated, runtimePolicyImplemented, wholeHouseSupported, gameHostTested;
        public int scenarios; public Row[] rows;
    }
}
