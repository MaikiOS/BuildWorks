using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

public static partial class FogProductionFramesCheck
{
    private static IEnumerator CheckFogGpuTiming()
    {
        string output = Argument("-fogEvidenceOutput");
        Require(!string.IsNullOrEmpty(output), "Fog GPU timing output required.");
        currentReport = new Report(); reportPath = Path.Combine(output, "verification.json");
        var proof = new FogGpuTimingReport();
        var host = new GameObject("Frozen GPU timing camera"); var camera = host.AddComponent<Camera>();
        var probe = host.AddComponent<FogRenderProbe>(); camera.enabled = false;
        camera.transform.position = new Vector3(8, 4, -9); camera.transform.LookAt(new Vector3(2.4f, 1, .75f));
        camera.fieldOfView = 55; camera.aspect = 960f / 540;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .2f, .3f);
        camera.allowHDR = true; camera.renderingPath = RenderingPath.DeferredShading; camera.depthTextureMode = DepthTextureMode.Depth;
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.transform.localScale = new Vector3(.2f, 2, 2); prefab.SetActive(false);
        var paint = new Material(Shader.Find("Standard")); paint.color = new Color(.64f, .31f, .12f); prefab.GetComponent<Renderer>().sharedMaterial = paint;
        var visuals = new BlueprintFogVisual[28]; var previous = RenderTexture.active; RenderTexture target = null;
        try
        {
            Require(SystemInfo.supportsGpuRecorder, "GPU recorder unavailable.");
            Require(BlueprintFogArena.MaximumFieldBytes == 256L * 1024 * 1024, "GPU timing requires32resident define.");
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
            byte[] bundle = File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"));
            using (var hash = System.Security.Cryptography.SHA256.Create()) proof.bundleSha256 = BitConverter.ToString(hash.ComputeHash(bundle)).Replace("-", "");
            using (var assets = new BlueprintFogAssets(bundle))
            using (var pool = BlueprintFogChannels.CreateArena(assets, 32, 32768, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D))
            using (var atlas = new BlueprintFogAtlas(assets, pool))
            {
                Require(pool.SnapshotArena.ResidentCapacity == 32, "GPU timing residency differs.");
                var body = new Vector2(.3f, .95f);
                for (int i = 0; i < visuals.Length; ++i)
                {
                    var origin = new Vector3(i % 7 * .8f, 0, i / 7 * .5f);
                    var visual = visuals[i] = new BlueprintFogVisual(assets, prefab, origin + Vector3.up, Quaternion.identity, Vector3.one,
                        null, origin, origin + new Vector3(-1.8f, .95f, 0), body, pool, deferPreparation: true, noiseOrigin: Vector3.zero);
                    using (var preparation = visual.Prepare().GetEnumerator()) while (preparation.MoveNext()) { }
                    visual.Root.SetActive(true);
                    visual.AdvanceTo(8 * BlueprintFogSimulation.Step + .005f, origin + new Vector3(0, .95f, 0), body, true);
                }
                for (int attempt = 0; attempt < 128 && visuals.Any(v => v.Snapshot == null || v.Snapshot.State.ConfirmedTick != 8 || v.LogicalSource.Inputs.Count != 0); ++attempt)
                { pool.PumpSources(); AsyncGPUReadback.WaitAllRequests(); pool.PumpSources(); }
                var handles = visuals.Select(v => v.Snapshot).ToArray();
                void Frozen() => Require(!pool.RequiresSnapshotTeardown && handles.All(h => h != null && h.State.ConfirmedTick == 8 && h.LastFlags == 0 && !h.nativePending) &&
                    visuals.All(v => v.LogicalSource.Error == null && v.LogicalSource.Inputs.Count == 0), "Frozen GPU timing state differs.");
                Frozen(); atlas.PreparePublishedLayout(visuals); atlas.PreparePublished(visuals, handles);
                target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target;
                foreach (float light in new[] { 0f, 1.6f, 1.6f, 0f })
                {
                    var row = new FogGpuTimingRow { beamStrength = light }; proof.rows.Add(row);
                    var sampler = CustomSampler.Create("BuildWorks.FogGpuTiming." + proof.rows.Count, true);
                    var recorder = sampler.GetRecorder(); Require(recorder.isValid, "Fog GPU recorder invalid."); recorder.enabled = true;
                    double atlasSubmit = 0;
                    probe.Render = (image, destination) => {
                        var submit = System.Diagnostics.Stopwatch.StartNew(); sampler.Begin();
                        try { atlas.Render(camera, image, destination, visuals, new Vector3(-4, 1, 0), Vector3.right, light, handles); }
                        finally { sampler.End(); atlasSubmit = submit.Elapsed.TotalMilliseconds; }
                    };
                    try
                    {
                        for (int frame = 0; frame < 26; ++frame)
                        {
                            int before = probe.Frames;
                            var watch = System.Diagnostics.Stopwatch.StartNew(); camera.Render(); double renderMs = watch.Elapsed.TotalMilliseconds;
                            Require(probe.Frames == before + 1, "Fog GPU probe skipped atlas callback."); ++row.renderCalls;
                            watch.Restart(); var drain = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); drain.WaitForCompletion();
                            Require(!drain.hasError && drain.GetData<byte>().Length == 960 * 540 * 4, "Fog GPU drain failed.");
                            double drainMs = watch.Elapsed.TotalMilliseconds;
                            yield return null;
                            // Recorder is delayed; six identical warmup frames keep every retained sample within this frozen arm.
                            if (frame >= 6)
                            {
                                Require(recorder.gpuSampleBlockCount == 1 && recorder.gpuElapsedNanoseconds > 0, "Fog GPU timing unavailable/ambiguous.");
                                row.samples.Add(new GpuTimingSample { frame = Time.frameCount, blocks = recorder.gpuSampleBlockCount, gpuNanoseconds = recorder.gpuElapsedNanoseconds });
                                row.renderSubmitMilliseconds += renderMs; row.atlasSubmitMilliseconds += atlasSubmit; row.readbackWaitMilliseconds += drainMs;
                            }
                            Frozen();
                        }
                    }
                    finally { recorder.enabled = false; probe.Render = null; }
                    Require(row.renderCalls == 26 && row.samples.Count == 20, "Fog GPU timing coverage differs.");
                    File.WriteAllText(reportPath, JsonUtility.ToJson(proof, true));
                }
                proof.passed = proof.rows.Count == 4 && proof.rows.All(row => row.samples.Count == 20);
            }
        }
        finally
        {
            probe.Render = null; camera.targetTexture = null; RenderTexture.active = previous;
            foreach (var visual in visuals) visual?.Dispose();
            if (target) RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(paint); UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(host);
            proof.resourcesReleased = true; currentReport.passed = proof.passed;
            File.WriteAllText(reportPath, JsonUtility.ToJson(proof, true));
        }
    }
    private static IEnumerator CheckGpuTimingCapability()
    {
        string output = Argument("-fogEvidenceOutput");
        Require(!string.IsNullOrEmpty(output), "GPU capability output required.");
        var proof = new GpuTimingCapability { supportsGpuRecorder = SystemInfo.supportsGpuRecorder,
            graphicsDevice = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(), batchMode = Application.isBatchMode };
        currentReport = new Report(); reportPath = Path.Combine(output, "verification.json");
        var sampler = CustomSampler.Create("BuildWorks.GpuTimingCapability", true);
        var recorder = sampler.GetRecorder(); proof.recorderValid = recorder.isValid;
        RenderTexture target = null; var previous = RenderTexture.active;
        try
        {
            if (!proof.supportsGpuRecorder || !proof.recorderValid) { proof.failure = "GPU recorder unavailable in this mode."; yield break; }
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            target = RenderTexture.GetTemporary(960, 540, 0, RenderTextureFormat.ARGB32);
            recorder.enabled = true;
            for (int i = 0; i < 36; ++i)
            {
                sampler.Begin();
                try { Graphics.Blit(Texture2D.whiteTexture, target); ++proof.submissions; }
                finally { sampler.End(); }
                yield return null;
                if (i >= 6) proof.rows.Add(new GpuTimingSample { frame = Time.frameCount,
                    gpuNanoseconds = recorder.gpuElapsedNanoseconds, blocks = recorder.gpuSampleBlockCount });
            }
            foreach (var row in proof.rows) if (row.blocks > 0 && row.gpuNanoseconds > 0) ++proof.nonzeroSamples;
            proof.passed = proof.submissions == 36 && proof.rows.Count == 30 && proof.nonzeroSamples >= 8;
            if (!proof.passed) proof.failure = "No sufficient positive GPU samples; no timing inferred.";
        }
        finally
        {
            recorder.enabled = false; RenderTexture.active = previous;
            if (target) RenderTexture.ReleaseTemporary(target);
            proof.resourcesReleased = true; currentReport.passed = proof.passed;
            File.WriteAllText(reportPath, JsonUtility.ToJson(proof, true));
        }
    }
    [Serializable] private sealed class GpuTimingCapability
    {
        public bool passed, supportsGpuRecorder, recorderValid, batchMode, resourcesReleased;
        public bool gameHostTested, fogGpuTimeMeasured;
        public string graphicsDevice, graphicsApi, failure = "";
        public int submissions, nonzeroSamples;
        public List<GpuTimingSample> rows = new List<GpuTimingSample>();
    }
    [Serializable] private sealed class GpuTimingSample
    { public int frame, blocks; public long gpuNanoseconds; }
    [Serializable] private sealed class FogGpuTimingReport
    {
        public bool passed, resourcesReleased;
        public bool gameHostTested, nativeServiceHeadroomProved;
        public string scope = "Frozen28procedural/32resident;GPU marker around atlas.Render only;not solver GPU,whole frame or native FPS", bundleSha256;
        public List<FogGpuTimingRow> rows = new List<FogGpuTimingRow>();
    }
    [Serializable] private sealed class FogGpuTimingRow
    {
        public float beamStrength;
        public int renderCalls;
        public double renderSubmitMilliseconds, atlasSubmitMilliseconds, readbackWaitMilliseconds;
        public List<GpuTimingSample> samples = new List<GpuTimingSample>();
    }
}
