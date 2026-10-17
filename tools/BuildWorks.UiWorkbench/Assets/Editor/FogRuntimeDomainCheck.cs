using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Diagnostic comparison; collecting these measurements does not approve the house effect.
public static class FogRuntimeDomainCheck
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-domain"); Directory.CreateDirectory(output);
        EditorSceneManager.OpenScene("Assets/FogGhostLab/FogWallStudy.unity");
        PlayerSettings.colorSpace = ColorSpace.Linear;
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BuildWorks.FogStudy.FogGhostLab")).First(t => t != null);
        var lab = (Behaviour)UnityEngine.Object.FindFirstObjectByType(type);
        object Read(string name) => type.GetField(name, BindingFlags.Public | BindingFlags.Instance).GetValue(lab);
        var original = (GameObject)Read("solidBuilding");
        var ghost = (GameObject)Read("ghostBuilding");
        var actor = (Transform)Read("actor"); var camera = lab.GetComponent<Camera>();
        Bounds bounds = actor.GetComponent<Collider>().bounds;
        var body = new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y);
        Vector3 beam = (Vector3)Read("beamOrigin"), direction = (Vector3)Read("beamDirection");
        float strength = (float)Read("beamStrength");
        lab.enabled = false; original.SetActive(false); ghost.SetActive(false);
        camera.depthTextureMode |= DepthTextureMode.Depth;
        var probe = camera.gameObject.AddComponent<FogRenderProbe>();
        var frames = new List<Frame>();
        double controlDelta = 0;
        string probeSource = BoundaryProbe(root, out ComputeShader boundaryProbe);
        int[] capture = { 0, 78, 90, 120, 135, 150, 165, 180, 210, 240, 300, 360 };
        using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
        using (var pool = new BlueprintFogChannels())
        using (var full = new BlueprintFogVisual(assets, original, Vector3.zero, Quaternion.identity, Vector3.one, null,
            Vector3.zero, Position(0), body))
        using (var compact = new BlueprintFogVisual(assets, original, Vector3.zero, Quaternion.identity, Vector3.one, null,
            Vector3.zero, Position(0), body, pool, "domain-compact", true))
        {
            Vector3 spacing = full.FieldMaximum - full.FieldMinimum;
            spacing = new Vector3(spacing.x / BlueprintFogSimulation.Nx, spacing.y / BlueprintFogSimulation.Ny, spacing.z / BlueprintFogSimulation.Nz);
            Vector3 cropMin = Vector3.zero; Vector3Int cropGrid = Vector3Int.zero;
            for (int axis = 0; axis < 3; ++axis)
            {
                int group = axis == 2 ? 4 : 8;
                cropMin[axis] = full.FieldMinimum[axis] + Mathf.FloorToInt((compact.FieldMinimum[axis] - full.FieldMinimum[axis]) / spacing[axis] / group) * spacing[axis] * group;
                cropGrid[axis] = Mathf.CeilToInt((compact.FieldMaximum[axis] - cropMin[axis]) / spacing[axis] / group) * group;
            }
            Vector3 cropMax = cropMin + Vector3.Scale(spacing, (Vector3)cropGrid);
            Vector3 expandedMin = full.FieldMinimum + Vector3.Scale(spacing, new Vector3(16, 0, 0));
            var expandedGrid = new Vector3Int(64, 72, 80);
            Vector3 expandedMax = expandedMin + Vector3.Scale(spacing, (Vector3)expandedGrid);
            Vector3 broadMin = full.FieldMinimum + Vector3.Scale(spacing, new Vector3(8, 0, 0));
            var broadGrid = new Vector3Int(80, 80, 80);
            Vector3 broadMax = broadMin + Vector3.Scale(spacing, (Vector3)broadGrid);
            using (var wide = new BlueprintFogSimulation(assets, compact.Source.Shape, compact.Source.Pigment, compact.Source.Noise,
                full.FieldMinimum, full.FieldMaximum, compact.Source.Minimum, compact.Source.Maximum, Position(0), 37))
            using (var cropped = new BlueprintFogSimulation(assets, compact.Source.Shape, compact.Source.Pigment, compact.Source.Noise,
                cropMin, cropMax, compact.Source.Minimum, compact.Source.Maximum, Position(0), 37, cropGrid))
            using (var control = CreateBoundarySimulation(assets, boundaryProbe, compact.Source,
                full.FieldMinimum, full.FieldMaximum, null, full.FieldMinimum, full.FieldMaximum))
            using (var wideCroppedAbsorption = CreateBoundarySimulation(assets, boundaryProbe, compact.Source,
                full.FieldMinimum, full.FieldMaximum, null, cropMin, cropMax))
            using (var croppedWideAbsorption = CreateBoundarySimulation(assets, boundaryProbe, compact.Source,
                cropMin, cropMax, cropGrid, full.FieldMinimum, full.FieldMaximum))
            using (var expanded = CreateBoundarySimulation(assets, boundaryProbe, compact.Source,
                expandedMin, expandedMax, expandedGrid, full.FieldMinimum, full.FieldMaximum))
            using (var broad = CreateBoundarySimulation(assets, boundaryProbe, compact.Source,
                broadMin, broadMax, broadGrid, full.FieldMinimum, full.FieldMaximum))
            {
            var sweep = new[] { croppedWideAbsorption, expanded, broad, control };
            var starts = new[] { new Vector3Int(24, 8, 4), new Vector3Int(16, 0, 0), new Vector3Int(8, 0, 0), Vector3Int.zero };
            if ((cropMin - full.FieldMinimum - Vector3.Scale(spacing, (Vector3)starts[0])).magnitude > .000001f)
                throw new InvalidOperationException("Padding sweep no longer aligns with its frozen voxel grid.");
            for (int tick = 0; tick <= 360; ++tick)
            {
                float time = tick * BlueprintFogSimulation.Step; actor.position = Position(time);
                full.AdvanceTo(time, actor.position, body, true); compact.AdvanceTo(time, actor.position, body, true);
                wide.AdvanceTo(time, actor.position, body, true); cropped.AdvanceTo(time, actor.position, body, true);
                control.AdvanceTo(time, actor.position, body, true);
                wideCroppedAbsorption.AdvanceTo(time, actor.position, body, true);
                croppedWideAbsorption.AdvanceTo(time, actor.position, body, true);
                expanded.AdvanceTo(time, actor.position, body, true); broad.AdvanceTo(time, actor.position, body, true);
                if (!capture.Contains(tick)) continue;
                StateComparison[] states = sweep.Select((candidate, index) => CompareState(wide, candidate, starts[index], spacing)).ToArray();
                StateComparison repeated = states[3];
                controlDelta = Math.Max(controlDelta, Math.Max(Math.Max(repeated.smokeMaxDelta, repeated.velocityMaxDelta),
                    Math.Max(repeated.originMaxDelta, repeated.surfaceMaxDelta)));
                compact.Root.SetActive(false); full.Root.SetActive(true);
                Color[] reference = FogRuntimePairCheck.Capture(camera, probe,
                    (image, target) => full.RenderVolume(camera, image, target, beam, direction, strength), output, tick + "-full");
                full.Root.SetActive(false); compact.Root.SetActive(true);
                Color[] candidate = FogRuntimePairCheck.Capture(camera, probe,
                    (image, target) => compact.RenderVolume(camera, image, target, beam, direction, strength), output, tick + "-compact");
                frames.Add(new Frame { tick = tick, seconds = time,
                    fullSmoke = Mass(full.Simulation.Smoke, full.FieldMaximum - full.FieldMinimum),
                    compactSmoke = Mass(compact.Simulation.Smoke, compact.FieldMaximum - compact.FieldMinimum),
                    sameSourceWideSmoke = Mass(wide.Smoke, full.FieldMaximum - full.FieldMinimum),
                    sameSourceCroppedSmoke = Mass(cropped.Smoke, cropMax - cropMin),
                    probeControlSmoke = Mass(control.Smoke, full.FieldMaximum - full.FieldMinimum),
                    wideCroppedAbsorptionSmoke = Mass(wideCroppedAbsorption.Smoke, full.FieldMaximum - full.FieldMinimum),
                    croppedWideAbsorptionSmoke = Mass(croppedWideAbsorption.Smoke, cropMax - cropMin),
                    paddingSweep = states,
                    meanRgbDelta = FogRuntimePairCheck.Delta(reference, candidate) });
            }
            if (frames.Count != capture.Length || !frames.Any(frame => frame.fullSmoke > 0 && frame.compactSmoke > 0))
                throw new InvalidOperationException("Domain comparison did not include both active smoke fields.");
            File.WriteAllText(Path.Combine(output, "comparison.json"), JsonUtility.ToJson(new Report {
                collected = true, visualAccepted = false, boundaryIsolated = true, probeControlMaxFieldDelta = controlDelta,
                boundaryProbeSource = probeSource, colorSpace = QualitySettings.activeColorSpace.ToString(),
                cameraPosition = camera.transform.position, cameraRotation = camera.transform.eulerAngles, fov = camera.fieldOfView,
                body = body, fullMin = full.FieldMinimum, fullMax = full.FieldMaximum,
                compactMin = compact.FieldMinimum, compactMax = compact.FieldMaximum,
                sameSourceCropMin = cropMin, sameSourceCropMax = cropMax, sameSourceCropGrid = cropGrid, frames = frames.ToArray()
            }, true));
            }
        }
    }

    private static StateComparison CompareState(BlueprintFogSimulation reference, BlueprintFogSimulation candidate,
        Vector3Int start, Vector3 spacing)
    {
        var result = new StateComparison { grid = candidate.GridSize, referenceStart = start };
        foreach (string field in new[] { "colour", "velocity", "origin", "surface" })
        {
            RenderTexture a = Current(reference, field), b = Current(candidate, field);
            var ar = AsyncGPUReadback.Request(a, 0, TextureFormat.RGBAFloat);
            var br = AsyncGPUReadback.Request(b, 0, TextureFormat.RGBAFloat);
            ar.WaitForCompletion(); br.WaitForCompletion();
            if (ar.hasError || br.hasError || start.x < 0 || start.y < 0 || start.z < 0 ||
                start.x + b.width > a.width || start.y + b.height > a.height || start.z + b.volumeDepth > a.volumeDepth)
                throw new InvalidOperationException("Padding sweep readback or reference range failed.");
            long samples = 0; double maximum = 0;
            for (int z = 0; z < b.volumeDepth; ++z)
            {
                var av = ar.GetData<float>(z + start.z); var bv = br.GetData<float>(z);
                if (av.Length != a.width * a.height * 4 || bv.Length != b.width * b.height * 4)
                    throw new InvalidOperationException("Incomplete padding sweep layer.");
                samples += bv.Length;
                for (int y = 0; y < b.height; ++y) for (int x = 0; x < b.width; ++x) for (int c = 0; c < 4; ++c)
                {
                    float expected = av[((y + start.y) * a.width + x + start.x) * 4 + c];
                    float actual = bv[(y * b.width + x) * 4 + c];
                    if (float.IsNaN(expected) || float.IsInfinity(expected) || float.IsNaN(actual) || float.IsInfinity(actual))
                        throw new InvalidOperationException("Nonfinite padding sweep state.");
                    maximum = Math.Max(maximum, Math.Abs((double)expected - actual));
                }
            }
            if (samples != (long)b.width * b.height * b.volumeDepth * 4)
                throw new InvalidOperationException("Incomplete padding sweep field.");
            if (field == "colour") result.smokeMaxDelta = maximum;
            else if (field == "velocity") result.velocityMaxDelta = maximum;
            else if (field == "origin") result.originMaxDelta = maximum;
            else result.surfaceMaxDelta = maximum;
        }
        result.awayMaxDelta = BufferDelta(reference, candidate, "away", 1024);
        result.peakMaxDelta = BufferDelta(reference, candidate, "peak", 1024);
        result.recoveryFlagsExact = BufferDelta(reference, candidate, "state", 1) == 0;
        result.allFieldsExact = result.smokeMaxDelta == 0 && result.velocityMaxDelta == 0 &&
            result.originMaxDelta == 0 && result.surfaceMaxDelta == 0;
        Vector3 size = Vector3.Scale((Vector3)candidate.GridSize, spacing);
        result.smoke = Mass(candidate.Smoke, size);
        long voxels = BlueprintFogSimulation.FieldVoxels(candidate.GridSize);
        // Optimistic lower bound: every active owner shares one exact-grid scratch.
        result.sixOwnerSharedBytes = voxels * (6 * 32 + 48);
        result.eightOwnerSharedBytes = voxels * (8 * 32 + 48);
        return result;
    }

    private static long BufferDelta(BlueprintFogSimulation first, BlueprintFogSimulation second, string name, int length)
    {
        var field = typeof(BlueprintFogSimulation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        var a = AsyncGPUReadback.Request((ComputeBuffer)field.GetValue(first));
        var b = AsyncGPUReadback.Request((ComputeBuffer)field.GetValue(second));
        a.WaitForCompletion(); b.WaitForCompletion();
        if (a.hasError || b.hasError) throw new InvalidOperationException("Padding sweep ledger readback failed.");
        var x = a.GetData<uint>(); var y = b.GetData<uint>();
        if (x.Length != length || y.Length != length) throw new InvalidOperationException("Incomplete padding sweep ledger.");
        long maximum = 0;
        for (int i = 0; i < length; ++i) maximum = Math.Max(maximum, Math.Abs((long)x[i] - y[i]));
        return maximum;
    }

    private static string BoundaryProbe(string root, out ComputeShader probe)
    {
        const string edge = "float3 edge = min(p-_Minimum,_Maximum-p);";
        string source = File.ReadAllText(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/AccumulatedSmoke.compute"));
        if (source.Split(new[] { edge }, StringSplitOptions.None).Length != 2)
            throw new InvalidOperationException("Boundary probe requires exactly one runtime absorption expression.");
        source = "// Generated BuildWorks boundary diagnostic; not a runtime shader.\nfloat3 _AbsorptionMinimum, _AbsorptionMaximum;\n" +
            source.Replace(edge, "float3 edge = min(p-_AbsorptionMinimum,_AbsorptionMaximum-p);");
        const string path = "Assets/LocalVanillaPreview/BuildWorksFogBoundaryProbe.compute";
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "LocalVanillaPreview"));
        string absolute = Path.Combine(Application.dataPath, "LocalVanillaPreview/BuildWorksFogBoundaryProbe.compute");
        if (File.Exists(absolute) && !File.ReadAllText(absolute).StartsWith("// Generated BuildWorks boundary diagnostic;"))
            throw new InvalidOperationException("Boundary probe refuses to overwrite an unrelated asset.");
        File.WriteAllText(absolute, source);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        probe = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
        if (!probe) throw new InvalidOperationException("Boundary diagnostic shader did not import.");
        return source;
    }

    private static BlueprintFogSimulation CreateBoundarySimulation(BlueprintFogAssets assets, ComputeShader probe,
        BlueprintFogSource source, Vector3 minimum, Vector3 maximum, Vector3Int? grid, Vector3 absorptionMin, Vector3 absorptionMax)
    {
        var property = typeof(BlueprintFogAssets).GetProperty("Solver", BindingFlags.Instance | BindingFlags.NonPublic);
        ComputeShader original = assets.Solver;
        BlueprintFogSimulation simulation = null;
        try
        {
            property.SetValue(assets, probe);
            simulation = new BlueprintFogSimulation(assets, source.Shape, source.Pigment, source.Noise,
                minimum, maximum, source.Minimum, source.Maximum, Position(0), 37, grid);
            var solver = (ComputeShader)typeof(BlueprintFogSimulation).GetField("solver", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulation);
            solver.SetVector("_AbsorptionMinimum", absorptionMin); solver.SetVector("_AbsorptionMaximum", absorptionMax);
            return simulation;
        }
        catch { simulation?.Dispose(); throw; }
        finally { property.SetValue(assets, original); }
    }

    private static RenderTexture Current(BlueprintFogSimulation simulation, string name)
    {
        var type = typeof(BlueprintFogSimulation);
        int index = (int)type.GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulation);
        return ((RenderTexture[])type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulation))[index];
    }

    private static Vector3 Position(float time)
    {
        float x = time < 3 ? Mathf.Lerp(-1.8f, 0, Mathf.Clamp01(time - 2))
            : time <= 4.5f ? 0 : Mathf.Lerp(0, 1.8f, Mathf.Clamp01(time - 4.5f));
        return new Vector3(x, .95f, 0);
    }
    private static double Mass(RenderTexture texture, Vector3 size)
    {
        var request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBAFloat); request.WaitForCompletion();
        if (request.hasError) throw new InvalidOperationException("Domain diagnostic readback failed.");
        double sum = 0; int count = 0;
        for (int layer = 0; layer < request.layerCount; ++layer)
        {
            var values = request.GetData<float>(layer);
            count = checked(count + values.Length);
            for (int i = 3; i < values.Length; i += 4)
            {
                if (float.IsNaN(values[i]) || float.IsInfinity(values[i]) || values[i] < 0)
                    throw new InvalidOperationException("Invalid smoke mass.");
                sum += values[i];
            }
        }
        if (count != texture.width * texture.height * texture.volumeDepth * 4)
            throw new InvalidOperationException("Incomplete domain diagnostic readback.");
        return sum * size.x * size.y * size.z / (texture.width * texture.height * texture.volumeDepth);
    }
    [Serializable] private sealed class StateComparison
    {
        public Vector3Int grid, referenceStart;
        public double smoke, smokeMaxDelta, velocityMaxDelta, originMaxDelta, surfaceMaxDelta;
        public long awayMaxDelta, peakMaxDelta, sixOwnerSharedBytes, eightOwnerSharedBytes;
        public bool recoveryFlagsExact, allFieldsExact;
    }
    [Serializable] private sealed class Frame { public int tick; public float seconds; public double fullSmoke, compactSmoke, sameSourceWideSmoke, sameSourceCroppedSmoke, probeControlSmoke, wideCroppedAbsorptionSmoke, croppedWideAbsorptionSmoke, meanRgbDelta; public StateComparison[] paddingSweep; }
    [Serializable] private sealed class Report
    {
        public bool collected, visualAccepted, boundaryIsolated;
        public double probeControlMaxFieldDelta;
        public string colorSpace, boundaryProbeSource;
        public Vector3 cameraPosition, cameraRotation, fullMin, fullMax, compactMin, compactMax, sameSourceCropMin, sameSourceCropMax;
        public Vector3Int sameSourceCropGrid;
        public Vector2 body; public float fov; public Frame[] frames;
    }
}
