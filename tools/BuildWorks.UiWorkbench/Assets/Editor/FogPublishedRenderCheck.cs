using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Private render proof. CPU confirmation is deliberately held after GPU publication, never production scheduling.
public static class FogPublishedRenderCheck
{
    private const string Keyword = "BUILDWORKS_FOG_PUBLISHED";
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../..")), output = Path.Combine(root, "artifacts/fog-published-render");
        Directory.CreateDirectory(output); EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PlayerSettings.colorSpace = ColorSpace.Linear;
        var host = new GameObject("Published fog camera"); var camera = host.AddComponent<Camera>(); var probe = host.AddComponent<FogRenderProbe>();
        camera.transform.position = new Vector3(4, 2.3f, -5); camera.transform.LookAt(new Vector3(0, 1, 1.1f)); camera.fieldOfView = 49;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .2f, .3f);
        camera.allowHDR = true; camera.renderingPath = RenderingPath.DeferredShading; camera.depthTextureMode = DepthTextureMode.Depth;
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.transform.position = Vector3.up; prefab.transform.localScale = new Vector3(.2f, 2, 2); prefab.SetActive(false);
        var original = new Material(Shader.Find("Standard")); original.color = new Color(.64f, .31f, .12f); prefab.GetComponent<Renderer>().sharedMaterial = original;
        var visuals = new BlueprintFogVisual[2]; var oracle = new BlueprintFogSimulation[2];
        var body = new Vector2(.3f, .95f); var origins = new[] { Vector3.zero, new Vector3(0, 0, 2.5f) };
        var last = new BlueprintFogInputQueue.Sample[2];
        Material volume = null; ComputeShader pack = null; RenderTexture shape = null, smoke = null, surface = null;
        Report report = null; int captures = 0, comparisons = 0, negativeControls = 0; double maxMean = 0, maxLocal = 0, poisonDelta = 0, disabledDelta = 0, staleDelta = 0;
        try
        {
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
            {
                for (int slot = 0; slot < 2; ++slot)
                    visuals[slot] = new BlueprintFogVisual(assets, prefab, origins[slot] + Vector3.up, Quaternion.identity, Vector3.one,
                        null, origins[slot], origins[slot] + new Vector3(-1.8f, .95f, 0), body);
                var source = visuals[0];
                using (var codec = new FogSparseStorageProbe())
                using (var test = new FogArenaPhysicsProbe.Probe(assets, source, body, 8192, codec))
                using (var bindings = new ComputeBuffer(10, 4))
                using (var shortBindings = new ComputeBuffer(4, 4))
                using (var shortMetadata = new ComputeBuffer(1, 4))
                {
                    var words = test.Binding(0).Concat(test.Binding(1)).ToArray(); bindings.SetData(words);
                    var inputs = new[] { test.InputQueue(0), test.InputQueue(1) };
                    for (int slot = 0; slot < 2; ++slot)
                    {
                        last[slot] = test.Observation(slot, 0);
                        oracle[slot] = new BlueprintFogSimulation(assets, source.Source.Shape, source.Source.Pigment, source.Source.Noise,
                            source.FieldMinimum, source.FieldMaximum, source.Source.Minimum, source.Source.Maximum,
                            Actor(last[slot]), 37 + slot);
                    }
                    volume = new Material(assets.Volume); volume.EnableKeyword("BUILDWORKS_FOG_ATLAS"); volume.EnableKeyword("BUILDWORKS_FOG_DORMANT_FAST");
                    pack = UnityEngine.Object.Instantiate(assets.Solver);
                    Vector3Int block = new Vector3Int(146, 114, 122), size = new Vector3Int(146, 114, 244);
                    shape = Atlas(size, RenderTextureFormat.RHalf); smoke = Atlas(size, RenderTextureFormat.ARGBHalf); surface = Atlas(size, RenderTextureFormat.ARGBHalf);
                    var restMin = new Vector4[32]; var restMax = new Vector4[32]; var fieldMin = new Vector4[32]; var fieldMax = new Vector4[32];
                    var offsets = new Vector4[32]; var dynamicOffsets = new Vector4[32]; var shapes = new Vector4[32]; var fields = new Vector4[32];
                    for (int slot = 0; slot < 2; ++slot)
                    {
                        restMin[slot] = source.Source.Minimum; restMax[slot] = source.Source.Maximum;
                        fieldMin[slot] = source.FieldMinimum; fieldMax[slot] = source.FieldMaximum;
                        offsets[slot] = new Vector4(0, 0, slot * block.z, 0); dynamicOffsets[slot] = new Vector4(0, 0, slot * 82, 0);
                        shapes[slot] = new Vector4(144, 112, 120, 1); fields[slot] = new Vector4(96, 80, 80, body.y);
                    }
                    volume.SetVectorArray("_RegionRestMin", restMin); volume.SetVectorArray("_RegionRestMax", restMax);
                    volume.SetVectorArray("_RegionFieldMin", fieldMin); volume.SetVectorArray("_RegionFieldMax", fieldMax);
                    volume.SetVectorArray("_RegionOffset", offsets); volume.SetVectorArray("_RegionDynamicOffset", dynamicOffsets);
                    volume.SetVectorArray("_RegionNoiseOffset", new Vector4[32]); volume.SetVectorArray("_RegionShapeSize", shapes);
                    volume.SetVector("_AtlasSize", new Vector4(size.x, size.y, size.z, 0)); volume.SetInt("_RegionCount", 2);
                    Bounds bounds = source.VolumeBounds; bounds.Encapsulate(visuals[1].VolumeBounds);
                    volume.SetVector("_FineMin", bounds.min); volume.SetVector("_FineMax", bounds.max); volume.SetInt("_IntegrationSteps", 1024);
                    volume.SetTexture("_Noise", source.Source.Noise); volume.SetTexture("_AtlasShape", shape);
                    volume.SetVector("_BeamOrigin", new Vector3(-4, 1, 0)); volume.SetVector("_BeamDirection", Vector3.right); volume.SetFloat("_BeamStrength", 0);
                    volume.SetVector("_FogCameraOrigin", camera.transform.position); volume.SetVector("_CameraRight", camera.transform.right);
                    volume.SetVector("_CameraUp", camera.transform.up); volume.SetVector("_CameraForward", camera.transform.forward);
                    volume.SetFloat("_Aspect", 384f / 216); volume.SetFloat("_TanHalfFov", Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f));
                    void Published(Material material, int slot)
                    {
                        material.EnableKeyword(Keyword); material.SetBuffer("_FogBindings", bindings); material.SetBuffer("_PublishedMetadata", test.PublishedMetadata);
                        material.SetInt("_PublishedSlots", 2); material.SetInt("_PublishedRegion", slot);
                        material.SetTexture("_PublishedSurface", test.PublishedSurface); material.SetVector("_DynamicAtlasSize", new Vector4(98, 82, 164, 0));
                        material.SetVector("_DynamicFieldSize", new Vector4(96, 80, 80, 0)); material.SetVector("_DynamicOffset", dynamicOffsets[slot]);
                    }
                    void Bind(bool published, bool poison = false, bool fallbackFirst = false, bool fallbackAll = false)
                    {
                        var originClocks = new Vector4[32]; var actors = new Vector4[32];
                        for (int slot = 0; slot < 2; ++slot)
                        {
                            var sample = last[slot]; Vector3 actor = Actor(sample); Vector2 capsule = new Vector2(sample.Radius, sample.HalfHeight);
                            originClocks[slot] = new Vector4(origins[slot].x, origins[slot].y, origins[slot].z, poison ? 777 : sample.Clock);
                            actors[slot] = poison ? new Vector4(32, 32, 32, 7) : new Vector4(actor.x, actor.y, actor.z, capsule.x);
                            fields[slot].w = poison ? 7 : capsule.y; shapes[slot].w = poison ? 0 : 1;
                            bool empty = fallbackAll || (fallbackFirst && slot == 0); if (empty) { originClocks[slot].w = 0; shapes[slot].w = 0; }
                            foreach (var material in visuals[slot].Source.Parts.SelectMany(p => p.Renderer.sharedMaterials))
                            {
                                material.DisableKeyword(Keyword);
                                material.SetTexture("_MatterFlow", (poison || empty) ? (Texture)assets.Empty : oracle[slot].Surface);
                                material.SetFloat("_Clock", empty ? 0 : poison ? 777 : sample.Clock); material.SetVector("_Body", poison ? new Vector2(7, 7) : capsule);
                                var trail = new Vector4[24]; trail[0] = new Vector4(actor.x, actor.y, actor.z, sample.Nearby ? 1 : 0);
                                if (poison || empty) trail[0] = new Vector4(32, 32, 32, 0); material.SetVectorArray("_Trail", trail);
                                material.SetFloat("_MatterEnabled", (poison || empty) ? 0 : 1);
                                if (published) Published(material, slot);
                            }
                        }
                        volume.DisableKeyword(Keyword); volume.SetVectorArray("_RegionOriginClock", originClocks); volume.SetVectorArray("_RegionActorRadius", actors);
                        volume.SetVectorArray("_RegionFieldSize", fields); volume.SetVectorArray("_RegionShapeSize", shapes);
                        volume.SetTexture("_AtlasSmoke", published ? test.PublishedSmoke : smoke); volume.SetTexture("_AtlasSurface", published ? test.PublishedSurface : surface);
                        if (published) Published(volume, 0);
                    }
                    void Pack()
                    {
                        int kernel = pack.FindKernel("PackVolume"); pack.SetVector("_PackSize", new Vector4(block.x, block.y, block.z, 0));
                        pack.SetVector("_PackShapeSize", new Vector4(144, 112, 120, 0)); pack.SetVector("_PackFieldSize", new Vector4(96, 80, 80, 0));
                        pack.SetTexture(kernel, "_PackShape", source.Source.Shape); pack.SetTexture(kernel, "_PackedShape", shape);
                        pack.SetTexture(kernel, "_PackedSmoke", smoke); pack.SetTexture(kernel, "_PackedSurface", surface);
                        for (int slot = 0; slot < 2; ++slot)
                        {
                            pack.SetVector("_PackOffset", offsets[slot]); pack.SetTexture(kernel, "_PackSmoke", oracle[slot].Smoke); pack.SetTexture(kernel, "_PackSurface", oracle[slot].Surface);
                            pack.Dispatch(kernel, 19, 15, 31);
                        }
                    }
                    Color[] Capture(string name, int mode)
                    {
                        foreach (var visual in visuals) visual.Root.SetActive(mode != 1); ++captures;
                        return FogRuntimePairCheck.Capture(camera, probe, (image, target) => { if (mode == 0) Graphics.Blit(image, target); else Graphics.Blit(image, target, volume); }, output, name);
                    }
                    void Compare(Color[] a, Color[] b)
                    {
                        double mean = FogRuntimePairCheck.Delta(a, b), local = 0;
                        for (int p = 0; p < a.Length; ++p) local = Math.Max(local, Math.Max(Math.Abs(a[p].r - b[p].r), Math.Max(Math.Abs(a[p].g - b[p].g), Math.Abs(a[p].b - b[p].b))));
                        maxMean = Math.Max(maxMean, mean); maxLocal = Math.Max(maxLocal, local); ++comparisons;
                        Require(mean < .0001 && local < .03, "Published render differs from the dense oracle: " + mean + "/" + local);
                    }
                    void Check(string name)
                    {
                        Pack();
                        for (int mode = 0; mode < 3; ++mode)
                        {
                            Bind(false); var expected = Capture(name + "-" + mode + "-dense", mode);
                            Bind(true); var actual = Capture(name + "-" + mode + "-published", mode); Compare(expected, actual);
                            Bind(true, true); var poisoned = Capture(name + "-" + mode + "-poison", mode);
                            poisonDelta = Math.Max(poisonDelta, FogRuntimePairCheck.Delta(actual, poisoned)); Compare(actual, poisoned);
                            if (name == "96")
                            {
                                if (mode == 2)
                                {
                                    volume.DisableKeyword(Keyword);
                                    foreach (var material in visuals.SelectMany(v => v.Source.Parts).SelectMany(p => p.Renderer.sharedMaterials)) material.DisableKeyword(Keyword);
                                    disabledDelta = FogRuntimePairCheck.Delta(actual, Capture("disabled-negative", mode));
                                    Require(disabledDelta > .0001, "Disabled keyword control was insensitive.");
                                }
                                Bind(false, fallbackFirst: true); var fallback = Capture("static-first-" + mode, mode);
                                Bind(true, true);
                                for (int word = 0; word < 5; ++word)
                                {
                                    var wrong = (uint[])words.Clone(); wrong[word] = word == 0 ? 2u : wrong[word] ^ 1u; bindings.SetData(wrong);
                                    var invalid = Capture("invalid-binding-" + word + "-" + mode, mode); Compare(fallback, invalid); ++negativeControls;
                                    if (mode == 2 && word == 4) staleDelta = FogRuntimePairCheck.Delta(actual, invalid);
                                }
                                bindings.SetData(words); Require(mode != 2 || staleDelta > .0001, "Generation control was insensitive.");
                            }
                        }
                    }
                    void BindingBounds()
                    {
                        for (int mode = 0; mode < 3; ++mode)
                        {
                            Bind(false, fallbackAll: true); var expected = Capture("bounds-static-" + mode, mode);
                            for (int control = 0; control < 3; ++control)
                            {
                                Bind(true, true);
                                foreach (var material in visuals.SelectMany(v => v.Source.Parts).SelectMany(p => p.Renderer.sharedMaterials).Concat(new[] { volume }))
                                {
                                    if (control == 0) material.SetInt("_PublishedSlots", 0);
                                    else material.SetBuffer(control == 1 ? "_FogBindings" : "_PublishedMetadata", control == 1 ? shortBindings : shortMetadata);
                                }
                                Compare(expected, Capture("bounds-" + control + "-" + mode, mode)); ++negativeControls;
                            }
                            if (mode == 0) foreach (int invalidRegion in new[] { -1, 2 })
                            {
                                Bind(true, true);
                                foreach (var material in visuals.SelectMany(v => v.Source.Parts).SelectMany(p => p.Renderer.sharedMaterials)) material.SetInt("_PublishedRegion", invalidRegion);
                                Compare(expected, Capture("invalid-region-" + invalidRegion, mode)); ++negativeControls;
                            }
                        }
                    }
                    Check("initial");
                    for (int tick = 8; tick <= 96; tick += 8)
                    {
                        for (int slot = 0; slot < 2; ++slot)
                        {
                            last[slot] = test.Observation(slot, tick * BlueprintFogSimulation.Step + .005f);
                            test.Capture(slot, last[slot]); test.Send(slot, tick, input: inputs[slot], holdConfirmation: true);
                            oracle[slot].AdvanceTo(last[slot].Clock, Actor(last[slot]), body, true);
                        }
                        AsyncGPUReadback.WaitAllRequests();
                        Require(test.States.All(s => s.State == BlueprintFogSnapshotState.Phase.Pending && s.ConfirmedTick == tick - 8), "CPU confirmation was not held before geometry.");
                        if (tick == 8 || tick == 64 || tick == 96) Check(tick.ToString());
                        test.ReleaseConfirmations(); test.Wait();
                    }
                    for (int slot = 0; slot < 2; ++slot)
                    {
                        var sample = last[slot]; last[slot] = new BlueprintFogInputQueue.Sample(sample.Clock + .009f, sample.Actor, .43f, 1.15f, false, true, false);
                        test.Capture(slot, last[slot]); test.Send(slot, 96, input: inputs[slot], holdConfirmation: true);
                        oracle[slot].AdvanceTo(last[slot].Clock, Actor(last[slot]), new Vector2(.43f, 1.15f), false);
                    }
                    AsyncGPUReadback.WaitAllRequests(); Check("metadata-only"); test.ReleaseConfirmations(); test.Wait();
                    BindingBounds();
                    test.Send(0, 104, inject: true, holdConfirmation: true);
                    AsyncGPUReadback.WaitAllRequests(); Check("rejected"); test.ReleaseConfirmations(); test.Wait();
                    Require(test.Rejected == 1 && test.States[0].ConfirmedTick == 96, "Rejected fields changed render publication.");
                    report = new Report { passed = true, runtimeChannelsValidated = true, owners = 2, negativeControls = negativeControls,
                        captures = captures, comparisons = comparisons, maxMeanDelta = maxMean, maxLocalDelta = maxLocal, poisonedCpuDelta = poisonDelta,
                        disabledKeywordDelta = disabledDelta, staleGenerationDelta = staleDelta, metadataOnly = true, rejectedRetained = true,
                        geometryBeforeCpuConfirmation = true, meshAndAtlasVolume = true, productionSchedulerTested = false, wholeHouseSupported = false,
                        gameHostTested = false, gpuTimingsMeasured = false };
                }
                report.pageSamplingNegativeCases = FogPageSamplingCheck.Check();
                CheckRuntimeAtlas(assets, visuals, camera, probe, output, report);
                foreach (var visual in visuals) visual.Root.SetActive(false);
                CheckLogicalVisual(assets, prefab, camera, probe, output, report);
                CheckBuilding(assets, prefab, camera, probe, output, report);
            }
        }
        finally
        {
            foreach (var item in oracle) item?.Dispose(); foreach (var item in visuals) item?.Dispose();
            foreach (var item in new[] { shape, smoke, surface }) if (item) { item.Release(); UnityEngine.Object.DestroyImmediate(item); }
            if (pack) UnityEngine.Object.DestroyImmediate(pack); if (volume) UnityEngine.Object.DestroyImmediate(volume);
            UnityEngine.Object.DestroyImmediate(original); UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(host);
        }
        var oldImages = LegacyImages(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"), output, "legacy-installed");
        var newImages = LegacyImages(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"), output, "legacy-candidate");
        report.legacyCaptures = oldImages.Length + newImages.Length;
        for (int i = 0; i < oldImages.Length; ++i)
        {
            double delta = FogRuntimePairCheck.Delta(oldImages[i], newImages[i]); report.legacyMeanDelta = Math.Max(report.legacyMeanDelta, delta);
            Require(delta == 0, "Legacy shader variant changed its pixels: " + delta);
        }
        File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(report, true));
    }
    private static void CheckRuntimeAtlas(BlueprintFogAssets assets, BlueprintFogVisual[] visuals, Camera camera, FogRenderProbe probe, string output, Report report)
    {
        foreach (var visual in visuals) { visual.UnbindPublished(); visual.Root.SetActive(true); }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 2, 8192))
        using (var dense = new BlueprintFogAtlas(assets))
        using (var published = new BlueprintFogAtlas(assets, pool))
        {
            var handles = new BlueprintFogChannels.SnapshotChannel[2];
            var samples = new BlueprintFogInputQueue.Sample[2];
            var body = new Vector2(.3f, .95f);
            for (int slot = 0; slot < 2; ++slot)
            {
                var visual = visuals[slot];
                samples[slot] = new BlueprintFogInputQueue.Sample(0, new OstrixMods.BuildWorks.Geometry.Point3(-1.8, .95, 0), body.x, body.y, true, true, true);
                handles[slot] = pool.AcquireSnapshot(slot, samples[slot], (scratch, staged) =>
                    new BlueprintFogSimulation(assets, visual.Source.Shape, visual.Source.Pigment, visual.Source.Noise,
                        visual.FieldMinimum, visual.FieldMaximum, visual.Source.Minimum, visual.Source.Maximum, Actor(samples[slot]), 37,
                        scratch: scratch, stagedState: staged));
            }
            void Reject(Action action) { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } catch (InvalidOperationException) { rejected = true; } Require(rejected, "Invalid runtime atlas mapping accepted."); ++report.runtimeMappingNegativeCases; }
            Reject(() => published.Render(camera, null, null, visuals, Vector3.zero, Vector3.right, 0, handles));
            Reject(() => published.PreparePublished(visuals, new[] { handles[0], handles[0] }));
            Reject(() => published.PreparePublished(visuals, new[] { handles[0] }));
            void Compare(string name, BlueprintFogVisual[] display, BlueprintFogChannels.SnapshotChannel[] mapping)
            {
                for (int mode = 0; mode < 2; ++mode)
                {
                    foreach (var visual in visuals) visual.UnbindPublished();
                    int route = mode;
                    var expected = FogRuntimePairCheck.Capture(camera, probe, (image, target) => {
                        if (route == 0) Graphics.Blit(image, target);
                        else dense.Render(camera, image, target, display, new Vector3(-4, 1, 0), Vector3.right, 0);
                    }, output, "runtime-" + name + "-" + mode + "-dense");
                    published.PreparePublished(display, mapping);
                    var actual = FogRuntimePairCheck.Capture(camera, probe, (image, target) => {
                        if (route == 0) Graphics.Blit(image, target);
                        else published.Render(camera, image, target, display, new Vector3(-4, 1, 0), Vector3.right, 0, mapping);
                    }, output, "runtime-" + name + "-" + mode + "-published");
                    double delta = FogRuntimePairCheck.Delta(expected, actual);
                    double local = 0;
                    for (int p = 0; p < expected.Length; ++p) local = Math.Max(local, Math.Max(Math.Abs(expected[p].r - actual[p].r), Math.Max(Math.Abs(expected[p].g - actual[p].g), Math.Abs(expected[p].b - actual[p].b))));
                    report.runtimeMaxMeanDelta = Math.Max(report.runtimeMaxMeanDelta, delta);
                    report.runtimeMaxLocalDelta = Math.Max(report.runtimeMaxLocalDelta, local);
                    Require(delta < .0001 && local < .03, "Runtime Visual/Atlas differs from dense rendering: " + name + "/" + mode + "/" + delta + "/" + local);
                    report.runtimeVisualAtlasCaptures += 2; ++report.runtimeVisualAtlasComparisons;
                }
            }
            Compare("dormant", visuals, new BlueprintFogChannels.SnapshotChannel[2]);
            Compare("initial", visuals, handles);
            for (int tick = 8; tick <= 96; tick += 8)
            {
                for (int slot = 0; slot < 2; ++slot)
                {
                    float clock = tick * BlueprintFogSimulation.Step + .005f;
                    var local = new Vector3(Mathf.Lerp(-1.8f, 0, Mathf.Clamp01(clock - 2 - slot * .6f)), .95f, 0);
                    var capsule = new Vector2(body.x + slot * .04f, body.y + slot * .1f);
                    samples[slot] = new BlueprintFogInputQueue.Sample(clock, new OstrixMods.BuildWorks.Geometry.Point3(local.x, local.y, local.z), capsule.x, capsule.y, true, true, true);
                    pool.CaptureSnapshot(handles[slot], samples[slot]); pool.TrySubmitSnapshot(handles[slot], out var token);
                    visuals[slot].AdvanceTo(clock, visuals[slot].Origin + local, capsule, true);
                }
                AsyncGPUReadback.WaitAllRequests();
                if (tick == 64 || tick == 96)
                {
                    Require(handles.All(handle => handle.State.State == BlueprintFogSnapshotState.Phase.Pending), "Runtime render did not hold CPU confirmation.");
                    Compare(tick.ToString(), visuals, handles);
                    if (tick == 96)
                    {
                        Color[] CaptureMapping(BlueprintFogChannels.SnapshotChannel[] mapping, string label)
                        {
                            published.PreparePublished(visuals, mapping);
                            return FogRuntimePairCheck.Capture(camera, probe, (image, target) => published.Render(camera, image, target, visuals,
                                new Vector3(-4, 1, 0), Vector3.right, 0, mapping), output, "runtime-mapping-" + label);
                        }
                        var expected = CaptureMapping(handles, "correct");
                        var wrong = CaptureMapping(new[] { handles[1], handles[0] }, "wrong");
                        report.runtimeWrongMappingDelta = FogRuntimePairCheck.Delta(expected, wrong);
                        Require(report.runtimeWrongMappingDelta > .0001, "Runtime mapping control is insensitive to swapped dynamic owners.");
                        report.runtimeVisualAtlasCaptures += 2; ++report.runtimeMappingNegativeCases;
                        published.PreparePublished(visuals, handles);
                    }
                }
                pool.PollSnapshots();
            }
            long initialPacks = published.PackDispatches;
            for (int slot = 0; slot < 2; ++slot)
            {
                var old = samples[slot]; var capsule = new Vector2(.43f - slot * .09f, 1.15f - slot * .2f);
                samples[slot] = new BlueprintFogInputQueue.Sample(old.Clock + .009f, old.Actor, capsule.x, capsule.y, false, true, true);
                pool.CaptureSnapshot(handles[slot], samples[slot]); pool.TrySubmitSnapshot(handles[slot], out var token);
                visuals[slot].AdvanceTo(samples[slot].Clock, visuals[slot].Origin + Actor(samples[slot]), capsule, false);
            }
            AsyncGPUReadback.WaitAllRequests(); Compare("metadata", visuals, handles); pool.PollSnapshots();
            var before = samples[0];
            pool.CaptureSnapshot(handles[0], new BlueprintFogInputQueue.Sample(before.Clock + 8 * BlueprintFogSimulation.Step, before.Actor, before.Radius, before.HalfHeight, true, true, true));
            pool.SnapshotArena.Program.SetInt("_InjectPackFailure", 1);
            pool.TrySubmitSnapshot(handles[0], out var rejectedToken); pool.SnapshotArena.Program.SetInt("_InjectPackFailure", 0);
            AsyncGPUReadback.WaitAllRequests(); Compare("rejected", visuals, handles); pool.PollSnapshots();
            Require(handles[0].LastFlags == 8 && published.PackDispatches == initialPacks && initialPacks == 2, "Runtime source cache repacked dynamic fields or rejected state committed.");
            Compare("reordered", new[] { visuals[1], visuals[0] }, new[] { handles[1], handles[0] });
            using (var foreignPool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
            {
                var v = visuals[0]; var initial = new BlueprintFogInputQueue.Sample(0, samples[0].Actor, body.x, body.y, true, true, true);
                var foreign = foreignPool.AcquireSnapshot(0, initial, (scratch, staged) => new BlueprintFogSimulation(assets,
                    v.Source.Shape, v.Source.Pigment, v.Source.Noise, v.FieldMinimum, v.FieldMaximum, v.Source.Minimum, v.Source.Maximum,
                    Actor(initial), 37, scratch: scratch, stagedState: staged));
                Reject(() => published.PreparePublished(visuals, new[] { foreign, handles[1] }));
            }
            pool.RetireSnapshot(handles[0]); Require(!pool.TryReleaseSnapshot(handles[0]), "Runtime retirement skipped GPU confirmation.");
            AsyncGPUReadback.WaitAllRequests(); Require(pool.TryReleaseSnapshot(handles[0]), "Runtime retirement did not complete.");
            Reject(() => published.PreparePublished(visuals, handles));
            report.runtimeStaticBytes = published.Bytes;
            report.runtimeCombinedRenderBytes = published.Bytes + pool.SnapshotArena.RenderBytes;
            Require(published.Bytes == 8122272 && report.runtimeCombinedRenderBytes == 22601184, "Split atlas bytes include hidden full dynamic copies.");
            bool allocated = false;
            Reject(() => published.ChangeLayout(new[] { new Vector3Int(512, 512, 256) }, 512, size => { allocated = true; }));
            Require(!allocated && published.Bytes == report.runtimeStaticBytes, "Combined render budget allocated or replaced the retained atlas before rejecting.");
            published.Dispose(); published.Dispose();
            Require(pool.SnapshotArena.Smoke == null && pool.SnapshotArena.Surface == null && pool.SnapshotArena.Bricks.IsCreated(), "Production publication allocated dense field copies.");
            report.runtimeVisualAtlasValidated = true; report.staticSourceSeparated = true; report.hardwareFilteredPages = true;
        }
    }
    private static void CheckLogicalVisual(BlueprintFogAssets assets, GameObject prefab, Camera camera, FogRenderProbe probe, string output, Report report)
    {
        var body = new Vector2(.3f, .95f); var outside = new Vector3(-1.8f, .95f, 0);
        using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
        using (var dense = new BlueprintFogAtlas(assets))
        using (var published = new BlueprintFogAtlas(assets, pool))
        using (var expected = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.identity, Vector3.one, null, Vector3.zero, outside, body))
        using (var actual = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.identity, Vector3.one, null, Vector3.zero, outside, body, pool))
        {
            Require(actual.Simulation == null && actual.Snapshot == null && actual.LogicalSource != null, "Visual eagerly allocated a legacy simulation.");
            void Compare(string name, bool continuousControl = false)
            {
                for (int mode = 0; mode < (continuousControl ? 1 : 2); ++mode)
                {
                    actual.Root.SetActive(false); expected.Root.SetActive(true);
                    int route = mode;
                    var baseline = FogRuntimePairCheck.Capture(camera, probe, (image, target) => {
                        if (route == 0) Graphics.Blit(image, target);
                        else dense.Render(camera, image, target, new[] { expected }, Vector3.zero, Vector3.right, 0);
                    }, output, "logical-" + name + "-" + mode + "-dense");
                    expected.Root.SetActive(false); actual.Root.SetActive(true);
                    var mapping = new[] { actual.Snapshot }; published.PreparePublished(new[] { actual }, mapping);
                    var image = FogRuntimePairCheck.Capture(camera, probe, (input, target) => {
                        if (route == 0) Graphics.Blit(input, target);
                        else published.Render(camera, input, target, new[] { actual }, Vector3.zero, Vector3.right, 0, mapping);
                    }, output, "logical-" + name + "-" + mode + "-published");
                    double delta = FogRuntimePairCheck.Delta(baseline, image), local = 0;
                    for (int i = 0; i < image.Length; ++i) local = Math.Max(local, Math.Max(Math.Abs(baseline[i].r - image[i].r), Math.Max(Math.Abs(baseline[i].g - image[i].g), Math.Abs(baseline[i].b - image[i].b))));
                    if (continuousControl)
                    {
                        report.coldIdleMaxLocalDelta = local;
                        Require(local >= .03, "Cold-start negative control is not sensitive to lost idle velocity.");
                        report.coldIdleControlSensitive = true; continue;
                    }
                    report.logicalVisualMaxMeanDelta = Math.Max(report.logicalVisualMaxMeanDelta, delta);
                    report.logicalVisualMaxLocalDelta = Math.Max(report.logicalVisualMaxLocalDelta, local);
                    Require(delta < .0001 && local < .03, "Logical Visual differs from dense rendering: " + name + "/" + mode + "/" + delta + "/" + local);
                    ++report.logicalVisualComparisons;
                }
            }
            void CompareFields()
            {
                var owner = actual.Snapshot;
                Require(owner != null && owner.State.State == BlueprintFogSnapshotState.Phase.Ready, "Continuous comparison requires confirmed state.");
                pool.BindSnapshot(owner); pool.SnapshotArena.Dispatch("BeginStep"); pool.SnapshotArena.Dispatch("RestoreFields", 24, 20, 20);
                var scratch = pool.SnapshotArena.Scratch;
                using (var codec = new FogSparseStorageProbe())
                {
                    codec.BeginComparison(); codec.Compare(scratch.ReadColour, expected.Simulation.Smoke);
                    codec.Compare(scratch.ReadOrigin, FogRuntimeSparseCheck.Current(expected.Simulation, "origin"));
                    codec.Compare(scratch.ReadSurface, expected.Simulation.Surface); codec.Compare(owner.Velocity, expected.Simulation.Flow);
                    for (int i = 0; i < 3; ++i) codec.Compare(owner.Ledger[i], FogRuntimeSparseCheck.Buffer(expected.Simulation, new[] { "away", "peak", "state" }[i]));
                    codec.RequireClean(); report.continuousStateComparisons += 7;
                }
            }
            for (int i = 1; i <= 16; ++i)
            {
                Vector3 actor = i <= 8 ? Vector3.up * .95f : outside;
                actual.AdvanceTo(i * .2f, actor, body, false); expected.AdvanceTo(i * .2f, actor, body, false); pool.PumpSources();
                AsyncGPUReadback.WaitAllRequests(); pool.PumpSources();
                Require(actual.Snapshot != null && actual.LogicalSource.Inputs.Count == 0, "Idle visual lost its resident solver or accepted input.");
                if (i == 8)
                    foreach (var material in actual.Source.Parts.Concat(expected.Source.Parts).SelectMany(part => part.Renderer.sharedMaterials))
                        material.SetFloat("_Accumulated", 0); // Exercise GhostClear as well as the accumulated geometry variant.
                if (i == 8 || i == 16) { Compare("resident-idle-" + i); CompareFields(); }
            }
            Vector3 inside = Vector3.up * .95f;
            actual.AdvanceTo(3.4f, inside, body, true); expected.AdvanceTo(3.4f, inside, body, true); pool.PumpSources();
            Require(actual.Snapshot != null && actual.Snapshot.State.State == BlueprintFogSnapshotState.Phase.Pending, "Visual contact did not submit its queued path.");
            AsyncGPUReadback.WaitAllRequests(); Compare("continuous-idle-before-confirmation");
            // Deliberately restart only the negative oracle: the real source must retain continuous flow.
            var continuous = expected.Simulation;
            using (var cold = new BlueprintFogSimulation(assets, expected.Source.Shape, expected.Source.Pigment, expected.Source.Noise,
                expected.FieldMinimum, expected.FieldMaximum, expected.Source.Minimum, expected.Source.Maximum, outside, 37, initialClock: 3.2f))
            {
                var field = typeof(BlueprintFogVisual).GetField("simulation", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                try
                {
                    cold.AdvanceTo(3.4f, inside, body, true); field.SetValue(expected, cold);
                    expected.AdvanceTo(3.4f, inside, body, true); Compare("cold-start-negative", true);
                }
                finally { field.SetValue(expected, continuous); expected.AdvanceTo(3.4f, inside, body, true); }
            }
            // Latest CPU input cannot replace the actor/clock of the snapshot already published by the GPU.
            actual.AdvanceTo(3.41f, outside, new Vector2(.5f, 1.3f), false); Compare("queued-metadata");
            Require(actual.LogicalSource.Inputs.Count >= 2, "Visual lost accepted input while its receipt was pending.");
            pool.PumpSources(); AsyncGPUReadback.WaitAllRequests(); pool.PumpSources();
            expected.AdvanceTo(3.41f, outside, new Vector2(.5f, 1.3f), false); CompareFields();
            var resident = actual.Snapshot;
            // Continue all idle steps after contact, including body wind, then re-enter the same generation.
            for (int i = 18; i <= 128; ++i)
            {
                Vector3 actor = outside + new Vector3(0, 0, Mathf.Sin(i * .3f) * .4f);
                actual.AdvanceTo(i * .2f, actor, body, false); expected.AdvanceTo(i * .2f, actor, body, false);
                pool.PumpSources(); AsyncGPUReadback.WaitAllRequests(); pool.PumpSources();
                Require(ReferenceEquals(resident, actual.Snapshot), "Recovery discarded continuous velocity/history.");
                if (i % 32 == 0) { Compare("resident-recovery-" + i); CompareFields(); }
            }
            var flags = new uint[1]; resident.Ledger[2].GetData(flags);
            Require(flags[0] == 0, "Continuous fixture did not actually reach recovery.");
            actual.AdvanceTo(25.8f, inside, body, true); expected.AdvanceTo(25.8f, inside, body, true);
            pool.PumpSources(); AsyncGPUReadback.WaitAllRequests(); pool.PumpSources();
            Compare("resident-reentry"); CompareFields();
            report.continuousV11Parity = true;
            bool rejected = false;
            try { published.PreparePublished(new[] { actual }, new BlueprintFogChannels.SnapshotChannel[1]); }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "Logical visual accepted an unassigned mapping while its own snapshot is live.");
            rejected = false;
            try { using (var invalid = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.identity, Vector3.one, null, Vector3.zero, outside, body, pool, compactRegion: true)) { } }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "Logical visual accepted an unproved compact physical domain.");
            rejected = false;
            var legacy = new Material(assets.Volume);
            try { actual.BindVolume(legacy, ""); } catch (InvalidOperationException) { rejected = true; }
            finally { UnityEngine.Object.DestroyImmediate(legacy); }
            Require(rejected, "Logical visual accepted a legacy volume pass.");
            var retained = actual.Source; var logical = actual.LogicalSource;
            published.Dispose(); actual.Dispose(); actual.Dispose();
            Require(retained.Shape && retained.Pigment && retained.Noise && !logical.Released, "Visual freed source textures before GPU drain.");
            pool.Dispose();
            Require(!retained.Shape && !retained.Pigment && !retained.Noise && logical.Released, "Visual source leaked after confirmed pool teardown.");
            expected.Dispose();
            report.logicalVisualValidated = true; report.logicalVisualDeferredDisposal = true; report.dormantClockAndBodyValidated = true;
        }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
        using (var visual = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.identity, Vector3.one, null, Vector3.zero, outside, body, pool))
        {
            visual.AdvanceTo(.2f, Vector3.up * .95f, body, true); pool.PumpSources();
            var logical = visual.LogicalSource; var retained = visual.Source;
            Require(visual.Snapshot != null && retained.Shape && !logical.Released, "Pool-first fixture did not submit a live visual.");
            pool.Dispose();
            Require(logical.Released && !retained.Shape && !retained.Pigment && !retained.Noise, "Pool-first teardown retained source textures.");
            visual.Dispose(); visual.Dispose();
        }
    }
    private static Color[][] LegacyImages(string bundle, string output, string prefix)
    {
        var host = new GameObject("Legacy shader regression"); var camera = host.AddComponent<Camera>(); var probe = host.AddComponent<FogRenderProbe>();
        camera.transform.position = new Vector3(4, 2.3f, -5); camera.transform.LookAt(new Vector3(0, 1, 1.1f)); camera.fieldOfView = 49;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .2f, .3f);
        camera.allowHDR = true; camera.renderingPath = RenderingPath.DeferredShading; camera.depthTextureMode = DepthTextureMode.Depth;
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.transform.localScale = new Vector3(.2f, 2, 2); prefab.SetActive(false);
        var original = new Material(Shader.Find("Standard")); original.color = new Color(.64f, .31f, .12f); prefab.GetComponent<Renderer>().sharedMaterial = original;
        var images = new System.Collections.Generic.List<Color[]>(); var owners = new BlueprintFogVisual[2]; var body = new Vector2(.3f, .95f);
        try
        {
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(bundle)))
            using (var volume = new BlueprintFogVolume(assets))
            using (var atlas = new BlueprintFogAtlas(assets))
            {
                try
                {
                    for (int slot = 0; slot < 2; ++slot)
                    {
                        Vector3 origin = new Vector3(0, 0, slot * 2.5f);
                        owners[slot] = new BlueprintFogVisual(assets, prefab, origin + Vector3.up, Quaternion.identity, Vector3.one, null,
                            origin, origin + new Vector3(-1.8f, .95f, 0), body);
                    }
                    for (int tick = 0; tick <= 96; tick += 8)
                    {
                        float clock = tick * BlueprintFogSimulation.Step + .005f;
                        foreach (var owner in owners) owner.AdvanceTo(clock, owner.Origin + new Vector3(tick < 64 ? -1.8f : 0, .95f, 0), body, true);
                        if (tick != 0 && tick != 96) continue;
                        for (int mode = 0; mode < 4; ++mode)
                        {
                            owners[1].Root.SetActive(mode != 1);
                            int route = mode;
                            images.Add(FogRuntimePairCheck.Capture(camera, probe, (image, target) => {
                                if (route == 0) Graphics.Blit(image, target);
                                else if (route == 1) owners[0].RenderVolume(camera, image, target, new Vector3(-4, 1, 0), Vector3.right, 0);
                                else if (route == 2) volume.Render(camera, image, target, owners[0], owners[1], new Vector3(-4, 1, 0), Vector3.right, 0);
                                else atlas.Render(camera, image, target, owners, new Vector3(-4, 1, 0), Vector3.right, 0);
                            }, output, prefix + "-" + tick + "-" + mode));
                        }
                    }
                }
                finally { foreach (var owner in owners) owner?.Dispose(); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(original); UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(host); }
        return images.ToArray();
    }
    private static Vector3 Actor(BlueprintFogInputQueue.Sample sample) => new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z);
    private static RenderTexture Atlas(Vector3Int size, RenderTextureFormat format)
    {
        var texture = new RenderTexture(size.x, size.y, 0, format, RenderTextureReadWrite.Linear) { dimension = TextureDimension.Tex3D,
            volumeDepth = size.z, enableRandomWrite = true, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
        if (!texture.Create()) { UnityEngine.Object.DestroyImmediate(texture); throw new InvalidOperationException("Render proof atlas allocation failed."); } return texture;
    }
    private static void CheckBuilding(BlueprintFogAssets assets, GameObject prefab, Camera camera, FogRenderProbe probe, string output, Report report)
    {
        var blueprint = new CompositeBlueprintStore.Blueprint();
        for (int i = 0; i < 2; ++i) blueprint.parts.Add(new CompositeBlueprintStore.Part {
            stableId = "published-building-" + i, prefabName = "wall",
            position = new CompositeBlueprintStore.VectorData(new Vector3(i * .7f, 1, i * .8f)),
            rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(0, i * 90, 0)),
            scale = new CompositeBlueprintStore.VectorData(new Vector3(1.1f, .9f, 1.2f)) });
        var body = new Vector2(.3f, .95f); Vector3 actor = new Vector3(-1.8f, .95f, 0);
        var expected = new BlueprintFogVisual[2];
        using (var pool = BlueprintFogChannels.CreateArena(assets, 2, 8192))
        using (var building = new BlueprintFogBuilding(assets, pool, blueprint, _ => prefab, Vector3.zero, Quaternion.identity, actor, body))
        using (var dense = new BlueprintFogAtlas(assets))
        {
            var actual = (System.Collections.Generic.List<BlueprintFogVisual>)typeof(BlueprintFogBuilding)
                .GetField("parts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(building);
            try
            {
                for (int i = 0; i < 2; ++i)
                {
                    var part = blueprint.parts[i]; var root = actual[i].Root.transform;
                    Require(actual[i].LogicalSource != null && actual[i].Simulation == null &&
                        Vector3.Distance(root.position, part.position.ToVector3()) < .00001f &&
                        Quaternion.Angle(root.rotation, part.rotation.ToQuaternion()) < .001f &&
                        Vector3.Distance(root.localScale, Vector3.Scale(prefab.transform.localScale, part.scale.ToVector3())) < .00001f,
                        "Published building changed part TRS or created a legacy solver.");
                    Require(actual[i].Root.GetComponentsInChildren<Collider>(true).Length == 0, "Fog building retained physical colliders.");
                    expected[i] = new BlueprintFogVisual(assets, prefab, root.position, root.rotation, part.scale.ToVector3(), null,
                        actual[i].Origin, actor, body, noiseOrigin: Vector3.zero);
                    expected[i].Root.SetActive(false);
                }
                void Compare(string label)
                {
                    foreach (var part in actual) part.Root.SetActive(false);
                    foreach (var part in expected) part.Root.SetActive(true);
                    var reference = FogRuntimePairCheck.Capture(camera, probe, (image, target) =>
                        dense.Render(camera, image, target, expected, Vector3.zero, Vector3.right, 0), output, "building-" + label + "-dense");
                    foreach (var part in expected) part.Root.SetActive(false);
                    foreach (var part in actual) part.Root.SetActive(true);
                    building.PrepareRender();
                    var image = FogRuntimePairCheck.Capture(camera, probe, (input, target) =>
                        building.Render(camera, input, target, Vector3.zero, Vector3.right, 0), output, "building-" + label + "-published");
                    double delta = FogRuntimePairCheck.Delta(reference, image), local = 0;
                    for (int i = 0; i < image.Length; ++i) local = Math.Max(local, Math.Max(Math.Abs(reference[i].r-image[i].r),
                        Math.Max(Math.Abs(reference[i].g-image[i].g), Math.Abs(reference[i].b-image[i].b))));
                    Require(delta < .0001 && local < .03, "Building differs from independent dense v11: " + label + "/" + delta + "/" + local);
                    ++report.buildingComparisons;
                }
                Compare("rest");
                for (int tick = 1; tick <= 24; ++tick)
                {
                    actor = tick <= 16 ? new Vector3(.3f, .95f, .4f) : new Vector3(-1.8f, .95f, 0);
                    float clock = tick * BlueprintFogSimulation.Step;
                    building.AdvanceTo(clock, actor, body, true);
                    foreach (var part in expected) part.AdvanceTo(clock, actor, body, true);
                    for (int wait = 0; wait < 16 && actual.Any(part => part.LogicalSource.Inputs.Count != 0); ++wait)
                    { AsyncGPUReadback.WaitAllRequests(); building.Pump(); }
                    Require(actual.All(part => part.LogicalSource.Inputs.Count == 0 && part.Snapshot.State.ConfirmedTick == tick && part.Snapshot.LastFlags == 0),
                        "Building failed to commit all source histories.");
                    if (tick == 8 || tick == 16 || tick == 24) Compare("tick-" + tick);
                }
                Require(building.AtlasBytes > pool.SnapshotArena.RenderBytes, "Building metrics omitted static/dynamic render resources.");
                report.buildingAdapterValidated = true;
            }
            finally { foreach (var part in expected) part?.Dispose(); }
        }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool runtimeChannelsValidated;
        public bool buildingAdapterValidated;
        public int buildingComparisons;
        public bool runtimeVisualAtlasValidated, staticSourceSeparated;
        public bool logicalVisualValidated, logicalVisualDeferredDisposal, dormantClockAndBodyValidated;
        public bool continuousV11Parity, coldIdleControlSensitive;
        public double coldIdleMaxLocalDelta;
        public int continuousStateComparisons;
        public int logicalVisualComparisons;
        public double logicalVisualMaxMeanDelta, logicalVisualMaxLocalDelta;
        public bool passed, metadataOnly, rejectedRetained, geometryBeforeCpuConfirmation, meshAndAtlasVolume,
            productionSchedulerTested, wholeHouseSupported, gameHostTested, gpuTimingsMeasured;
        public int owners, captures, comparisons, negativeControls, legacyCaptures;
        public int runtimeVisualAtlasCaptures, runtimeVisualAtlasComparisons;
        public int runtimeMappingNegativeCases;
        public long runtimeStaticBytes, runtimeCombinedRenderBytes;
        public bool hardwareFilteredPages;
        public int pageSamplingNegativeCases;
        public double runtimeMaxMeanDelta, runtimeMaxLocalDelta, runtimeWrongMappingDelta;
        public double legacyMeanDelta, maxMeanDelta, maxLocalDelta, poisonedCpuDelta, disabledKeywordDelta, staleGenerationDelta;
    }
}
