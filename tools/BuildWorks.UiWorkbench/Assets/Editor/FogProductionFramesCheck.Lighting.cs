using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class FogProductionFramesCheck
{
    // Frozen procedural owners isolate render cost; no scheduler cadence or native-house claim.
    public static void RunLightingMeasurement()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Argument("-fogEvidenceOutput");
        Require(!string.IsNullOrEmpty(output), "Lighting evidence output required.");
        var report = new LightingReport { runId = Argument("-fogEvidenceId") };
        string path = Path.Combine(output, "verification.json");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        PlayerSettings.colorSpace = ColorSpace.Linear;
        var host = new GameObject("Frozen lighting diagnostic camera"); var camera = host.AddComponent<Camera>();
        var probe = host.AddComponent<FogRenderProbe>(); camera.enabled = false;
        camera.transform.position = new Vector3(8, 4, -9); camera.transform.LookAt(new Vector3(2.4f, 1, .75f));
        camera.fieldOfView = 55; camera.aspect = 960f / 540;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .2f, .3f);
        camera.allowHDR = true; camera.renderingPath = RenderingPath.DeferredShading; camera.depthTextureMode = DepthTextureMode.Depth;
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.transform.localScale = new Vector3(.2f, 2, 2); prefab.SetActive(false);
        var paint = new Material(Shader.Find("Standard")); paint.color = new Color(.64f, .31f, .12f); prefab.GetComponent<Renderer>().sharedMaterial = paint;
        var visuals = new BlueprintFogVisual[28];
        var oldActive = RenderTexture.active; RenderTexture target = null;
        try
        {
            Require(BlueprintFogArena.MaximumFieldBytes == 256L * 1024 * 1024, "Lighting requires the explicit Editor GPU-residency define.");
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
            using (var pool = BlueprintFogChannels.CreateArena(assets, 32, 32768, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D))
            using (var atlas = new BlueprintFogAtlas(assets, pool))
            {
                report.residentCapacity = pool.SnapshotArena.ResidentCapacity;
                Require(report.residentCapacity == 32 && pool.SnapshotCapacity == 32 && pool.SnapshotArena.BlockCapacity == 32768, "Lighting pool differs from native QA capacity.");
                var body = new Vector2(.3f, .95f);
                for (int i = 0; i < visuals.Length; ++i)
                {
                    var origin = new Vector3(i % 7 * .8f, 0, i / 7 * .5f);
                    var visual = visuals[i] = new BlueprintFogVisual(assets, prefab, origin + Vector3.up, Quaternion.identity, Vector3.one,
                        null, origin, origin + new Vector3(-1.8f, .95f, 0), body, pool, deferPreparation: true, noiseOrigin: Vector3.zero);
                    using (var preparation = visual.Prepare().GetEnumerator()) while (preparation.MoveNext()) { }
                    visual.Root.SetActive(true);
                    // Each procedural owner has the same local contact state; this is not a real player's house route.
                    visual.AdvanceTo(8 * BlueprintFogSimulation.Step + .005f, origin + new Vector3(0, .95f, 0), body, true);
                }
                bool drained = false;
                for (int attempt = 0; attempt < 128; ++attempt)
                {
                    pool.PumpSources(); AsyncGPUReadback.WaitAllRequests(); pool.PumpSources();
                    Require(!pool.RequiresSnapshotTeardown && visuals.All(v => v.LogicalSource.Error == null), "Lighting preparation faulted.");
                    if (visuals.All(v => v.Snapshot != null && v.Snapshot.State.ConfirmedTick == 8 && v.LogicalSource.Inputs.Count == 0)) { drained = true; break; }
                }
                Require(drained, "Lighting preparation did not drain all28owners.");
                var handles = visuals.Select(v => v.Snapshot).ToArray();
                void Frozen()
                {
                    Require(handles.All(h => h.State.ConfirmedTick == 8 && h.LastFlags == 0 && !h.nativePending) &&
                        visuals.All(v => v.LogicalSource.Inputs.Count == 0), "Frozen lighting state changed.");
                }
                Frozen(); atlas.PreparePublishedLayout(visuals); atlas.PreparePublished(visuals, handles);
                target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32); camera.targetTexture = target;
                float strength = 0;
                probe.Render = (image, destination) => atlas.Render(camera, image, destination, visuals, new Vector3(-4, 1, 0), Vector3.right, strength, handles);
                byte[] Drain()
                {
                    var request = AsyncGPUReadback.Request(target, 0, TextureFormat.RGBA32); request.WaitForCompletion();
                    Require(!request.hasError && request.GetData<byte>().Length == 960 * 540 * 4, "Lighting target drain failed.");
                    return request.GetData<byte>().ToArray();
                }
                void Draw(int frames)
                {
                    int before = probe.Frames;
                    for (int i = 0; i < frames; ++i) camera.Render();
                    Require(probe.Frames == before + frames, "Lighting skipped atlas callbacks.");
                    report.renderCalls += frames;
                }
                strength = 0; Draw(4); byte[] dark = Drain();
                strength = 1.6f; Draw(4); byte[] lit = Drain();
                long difference = 0;
                for (int i = 0; i < lit.Length; ++i) if (i % 4 != 3) difference += Math.Abs(lit[i] - dark[i]);
                report.meanLightingByteDelta = difference / (960d * 540 * 3);
                Require(report.meanLightingByteDelta > .0001, "Lighting control is insensitive.");
                foreach (float light in new[] { 0f, 1.6f, 1.6f, 0f, 0f, 1.6f })
                {
                    strength = light; Draw(2); Drain(); Frozen();
                    var watch = System.Diagnostics.Stopwatch.StartNew(); Draw(8); Drain(); watch.Stop(); Frozen();
                    report.rows.Add(new LightingRow { beamStrength = light, frames = 8, elapsedMilliseconds = watch.Elapsed.TotalMilliseconds });
                    Require(report.rows.Last().elapsedMilliseconds > 0 && report.rows.Last().elapsedMilliseconds < 30000, "Lighting series outside bounds.");
                }
                report.fieldBytes = pool.FieldBytes; report.renderBytes = atlas.Bytes + pool.SnapshotArena.RenderBytes;
                report.passed = report.rows.Count == 6 && report.renderCalls == 68;
                Require(report.passed, "Lighting coverage incomplete.");
            }
        }
        catch (Exception error) { report.failure = error.ToString(); report.passed = false; Debug.LogException(error); }
        finally
        {
            camera.targetTexture = null; probe.Render = null;
            foreach (var visual in visuals) visual?.Dispose();
            RenderTexture.active = oldActive; if (target) RenderTexture.ReleaseTemporary(target);
            UnityEngine.Object.DestroyImmediate(paint); UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(host);
            report.resourcesReleased = true;
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
        EditorApplication.Exit(report.passed ? 0 : 1);
    }
    [Serializable] private sealed class LightingReport
    {
        public string mode = "LightingMeasurement", runId, failure;
        public string scope = "Frozen28procedural published owners; CPU+GPU+target-readback drain elapsed, not GPU execution timing";
        public bool passed, resourcesReleased, gameHostTested, nativeHouseGeometryTested, gpuTimingsMeasured;
        public int owners = 28, residentCapacity, renderCalls, width = 960, height = 540;
        public long fieldBytes, renderBytes;
        public double meanLightingByteDelta;
        public List<LightingRow> rows = new List<LightingRow>();
    }
    [Serializable] private sealed class LightingRow { public float beamStrength; public int frames; public double elapsedMilliseconds; }
}
