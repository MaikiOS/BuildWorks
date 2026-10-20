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
using Object = UnityEngine.Object;

// Diagnostic extension of the approved single-wall scene, not a newly approved multi-owner reference.
public static class FogLongWallMatchedCheck
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static long writtenBytes;
    public static void Run() => Capture(false, 4, 4);
    public static void RunProbe() => Capture(true, 4, 1);
    public static void RunSingleProbe() => Capture(true, 1, 1);
    public static void RunAllDirectionsProbe() => Capture(true, 4, 4);
    public static void RunJointProbe()
    {
        CheckRenderFailure();
        Capture(true, 2, 4, true);
    }
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Instance).GetValue(value);
    private static void Set(object value, string name, object data) => value.GetType().GetField(name, Instance).SetValue(value, data);
    private static void Call(object value, string name, params object[] args) => value.GetType().GetMethod(name, Instance).Invoke(value, args);
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }

    private static void Capture(bool probeOnly, int sections, int directions, bool jointControl = false)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/" + (jointControl ? "fog-pair-matched-260-joint-v2" : sections == 1 ? "fog-singlewall-matched-259-probe"
            : probeOnly && directions == 4 ? "fog-longwall-matched-259-directions"
            : "fog-longwall-matched-257" + (probeOnly ? "-probe" : "")));
        Require(!Directory.Exists(output), "Comparison output already exists; preserve previous evidence.");
        Require(new DriveInfo(Path.GetPathRoot(root)).AvailableFreeSpace >= 4L * 1024 * 1024 * 1024, "Matched recording requires 4GiB free disk.");
        Directory.CreateDirectory(output);
        writtenBytes = 0;
        ColorSpace previous = PlayerSettings.colorSpace;
        string middle = jointControl ? "current-dense-joint" : "current-dense-sequential";
        var report = new Report { probeOnly = probeOnly, sections = sections, directions = directions, denseComposition = middle };
        report.referenceScope = sections == 1 ? "Approved single-wall laboratory scene; candidate appearance still requires owner acceptance"
            : "Diagnostic " + sections + "-owner sequential reference; order dependent and not an approved joint-volume baseline";
        try
        {
            PlayerSettings.colorSpace = ColorSpace.Gamma;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Require(QualitySettings.activeColorSpace == ColorSpace.Gamma, "Gamma comparison unavailable.");
            EditorSceneManager.OpenScene("Assets/FogGhostLab/FogWallStudy.unity");
            Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BuildWorks.FogStudy.FogGhostLab")).First(t => t != null);
            var original = (Behaviour)Object.FindFirstObjectByType(type);
            original.enabled = false;
            Camera camera = original.GetComponent<Camera>(); camera.depthTextureMode |= DepthTextureMode.Depth;
            GameObject ghost = Field<GameObject>(original, "ghostBuilding"), solid = Field<GameObject>(original, "solidBuilding");
            Transform actor = Field<Transform>(original, "actor");
            var frozen = Field<Component>(original, "simulation");
            Bounds capsule = actor.GetComponent<Collider>().bounds;
            var body = new Vector2(Mathf.Max(capsule.extents.x, capsule.extents.z), capsule.extents.y);
            Bounds geometry = ghost.GetComponentsInChildren<Renderer>(true).First().bounds;
            foreach (var renderer in ghost.GetComponentsInChildren<Renderer>(true)) geometry.Encapsulate(renderer.bounds);
            float width = geometry.size.z;
            Require(width > .5f && width < 5, "Long-wall section width is unexpected.");
            ghost.SetActive(false); solid.SetActive(false);
            if (sections > 1)
            {
                camera.transform.position = new Vector3(-10, 5.5f, -width * 3);
                camera.transform.LookAt(new Vector3(0, 1, 0)); camera.fieldOfView = 52;
            }
            camera.aspect = 960f / 540;
            report.cameraPosition = camera.transform.position; report.cameraRotation = camera.transform.rotation;
            report.fov = camera.fieldOfView; report.body = body; report.sectionWidth = width;
            Vector3 beam = Field<Vector3>(original, "beamOrigin"), direction = Field<Vector3>(original, "beamDirection");
            float strength = Field<float>(original, "beamStrength");
            var render = camera.gameObject.AddComponent<FogRenderProbe>();
            try
            {
                using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-gamma/buildworks-fog-v11"))))
                for (int side = 0; side < directions; side++)
                {
                    var labs = new List<Behaviour>(); var resources = new List<Object>();
                    var dense = new List<BlueprintFogVisual>(); var current = new List<BlueprintFogVisual>();
                    using (var pool = BlueprintFogChannels.CreateArena(assets, sections, sections * 8192, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D))
                    using (var atlas = new BlueprintFogAtlas(assets, pool))
                    using (var denseAtlas = jointControl ? new BlueprintFogAtlas(assets) : null)
                    try
                    {
                        for (int section = 0; section < sections; section++)
                        {
                            Vector3 offset = Vector3.forward * (section - (sections - 1) * .5f) * width;
                            Behaviour lab = CopyReference(original, frozen, ghost, solid, camera, offset, resources);
                            labs.Add(lab);
                            dense.Add(new BlueprintFogVisual(assets, solid, solid.transform.position + offset, solid.transform.rotation, Vector3.one,
                                null, offset, Position(0, side, width), body, noiseOrigin: Vector3.zero, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D));
                            current.Add(new BlueprintFogVisual(assets, solid, solid.transform.position + offset, solid.transform.rotation, Vector3.one,
                                null, offset, Position(0, side, width), body, pool, noiseOrigin: Vector3.zero, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D));
                            var actual = dense.Last().Source;
                            var referenceSimulation = Field<Component>(lab, "simulation");
                            Require(Vector3.Distance(actual.Minimum + offset, Field<Vector3>(referenceSimulation, "restMinimum")) < .00001f &&
                                Vector3.Distance(actual.Maximum + offset, Field<Vector3>(referenceSimulation, "restMaximum")) < .00001f &&
                                Vector3.Distance(dense.Last().FieldMinimum + offset, Field<Vector3>(referenceSimulation, "minimum")) < .00001f &&
                                Vector3.Distance(dense.Last().FieldMaximum + offset, Field<Vector3>(referenceSimulation, "maximum")) < .00001f,
                                "Reference/current physical domains differ.");
                            foreach (string name in new[] { "shape", "pigment", "noise" })
                            {
                                Texture3D reference = Field<Texture3D>(frozen, name);
                                Texture3D candidate = name == "shape" ? actual.Shape : name == "pigment" ? actual.Pigment : actual.Noise;
                                Require(reference.GetPixelData<byte>(0).ToArray().SequenceEqual(candidate.GetPixelData<byte>(0).ToArray()), "Section source differs: " + section + "/" + name);
                                report.sourceComparisons++;
                            }
                        }
                        foreach (string stream in new[] { "reference-sequential", middle, "current-published" })
                            Directory.CreateDirectory(Path.Combine(output, "side" + side, stream));
                        if (jointControl)
                        {
                            Require(dense.Count == 2 && dense[0].VolumeBounds.Intersects(dense[1].VolumeBounds), "Joint control requires overlapping owners.");
                            report.overlapChecks++;
                        }
                        for (int tick = 0; tick <= 360; tick++)
                        {
                            float time = tick * BlueprintFogSimulation.Step;
                            actor.position = Position(time, side, width);
                            foreach (Behaviour lab in labs)
                            {
                                Set(lab, "clock", time);
                                var trail = Field<Vector4[]>(lab, "trail");
                                for (int sample = 0; sample < trail.Length; sample++)
                                {
                                    float stamp = sample == 0 ? time : Mathf.Floor(time / .06f) * .06f - (sample - 1) * .06f;
                                    Vector3 point = Position(Mathf.Max(0, stamp), side, width);
                                    trail[sample] = new Vector4(point.x, point.y, point.z, stamp < 0 ? 0 : Mathf.Clamp01(1 - (time - stamp) / 1.15f));
                                }
                                Call(lab, "OnPreCull");
                            }
                            foreach (var owner in dense.Concat(current)) owner.AdvanceTo(time, actor.position, body, true);
                            pool.PumpSources(); AsyncGPUReadback.WaitAllRequests(); pool.PumpSources();
                            var snapshots = current.Select(v => v.Snapshot).ToArray();
                            Require(snapshots.All(s => s != null && s.State.State == BlueprintFogSnapshotState.Phase.Ready) &&
                                current.All(v => v.LogicalSource.Inputs.Count == 0), "Comparison tick not committed for every owner.");
                            if (probeOnly && !new[] { 0, 66, 96, 150, 360 }.Contains(tick)) continue;
                            SetVisible(labs, dense, current, 0);
                            render.Render = (image, target) => Sequential(image, target, labs.Count, (n, input, result) => Call(labs[n], "OnRenderImage", input, result));
                            var a = Render(camera, output, side, "reference-sequential", tick);
                            SetVisible(labs, dense, current, 1);
                            render.Render = (image, target) =>
                            {
                                if (denseAtlas != null) denseAtlas.Render(camera, image, target, dense, beam, direction, strength);
                                else Sequential(image, target, dense.Count, (n, input, result) => dense[n].RenderVolume(camera, input, result, beam, direction, strength));
                            };
                            var b = Render(camera, output, side, middle, tick);
                            if (denseAtlas != null) report.denseAtlasBytes = Math.Max(report.denseAtlasBytes, denseAtlas.Bytes);
                            SetVisible(labs, dense, current, 2);
                            atlas.PreparePublished(current, snapshots);
                            render.Render = (image, target) => atlas.Render(camera, image, target, current, beam, direction, strength, snapshots);
                            var c = Render(camera, output, side, "current-published", tick);
                            report.frames.Add(new Frame { side = side, tick = tick, time = time, actor = actor.position,
                                phase = time < 2 ? "approach" : time < 3 ? "enter" : time < 4 ? "hold" : time < 5 ? "exit" : "recovery",
                                referenceDenseMean = Delta(a, b), densePublishedMean = Delta(b, c) });
                        }
                    }
                    finally
                    {
                        render.Render = null;
                        // Manually evaluated edit-mode reference components may never receive OnDisable.
                        foreach (var lab in labs) Call(Field<Component>(lab, "simulation"), "OnDisable");
                        foreach (var owner in current.Concat(dense)) owner.Dispose();
                        foreach (var item in resources.AsEnumerable().Reverse()) if (item) Object.DestroyImmediate(item);
                    }
                }
                Require(report.frames.Count == (probeOnly ? 5 : 361) * directions && report.sourceComparisons == sections * directions * 3, "Incomplete matched recording.");
                Require(!jointControl || report.overlapChecks == directions && report.denseAtlasBytes > 0 && report.denseAtlasBytes <= BlueprintFogAtlas.MaximumBytes,
                    "Incomplete overlap or memory proof for the joint control.");
                report.captureComplete = true;
                File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(report, true));
                Debug.Log("FOG_LONGWALL_MATCHED_CAPTURE_COMPLETE frames=" + report.frames.Count);
            }
            finally { render.Render = null; Object.DestroyImmediate(render); }
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Debug.LogException(error); EditorApplication.Exit(1); }
        finally { PlayerSettings.colorSpace = previous; AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); }
    }

    private static Behaviour CopyReference(Behaviour template, Component simulation, GameObject ghost, GameObject solid,
        Camera camera, Vector3 offset, List<Object> resources)
    {
        var lab = (Behaviour)camera.gameObject.AddComponent(template.GetType()); lab.enabled = false; resources.Add(lab);
        foreach (var field in template.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)) field.SetValue(lab, field.GetValue(template));
        var clone = Object.Instantiate(ghost); clone.transform.position += offset; resources.Add(clone);
        var materials = new Dictionary<Material, Material>();
        foreach (var renderer in clone.GetComponentsInChildren<Renderer>(true))
            renderer.sharedMaterials = renderer.sharedMaterials.Select(m =>
            { if (!materials.TryGetValue(m, out var copy)) { copy = new Material(m); materials.Add(m, copy); resources.Add(copy); } return copy; }).ToArray();
        Set(lab, "ghostBuilding", clone); Set(lab, "solidBuilding", solid);
        Set(lab, "ghostMaterials", Field<Material[]>(template, "ghostMaterials").Select(m => materials[m]).ToArray());
        var fog = new Material(Field<Material>(template, "fog")); resources.Add(fog); Set(lab, "fog", fog);
        Array old = Field<Array>(template, "contactSurfaces"), surfaces = Array.CreateInstance(old.GetType().GetElementType(), old.Length);
        for (int i = 0; i < old.Length; i++)
        {
            object value = old.GetValue(i);
            var filter = Field<MeshFilter>(value, "filter");
            string path = AnimationUtility.CalculateTransformPath(filter.transform, ghost.transform);
            Transform target = path.Length == 0 ? clone.transform : clone.transform.Find(path);
            Require(target, "Reference contact mesh path missing.");
            Set(value, "filter", target.GetComponent<MeshFilter>()); Set(value, "renderer", target.GetComponent<Renderer>());
            Bounds bounds = Field<Bounds>(value, "restBounds"); bounds.center += offset; Set(value, "restBounds", bounds); surfaces.SetValue(value, i);
        }
        Set(lab, "contactSurfaces", surfaces);
        var host = new GameObject("Frozen section simulation"); resources.Add(host);
        Component sim = host.AddComponent(simulation.GetType());
        foreach (var field in simulation.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)) field.SetValue(sim, field.GetValue(simulation));
        var solver = Object.Instantiate(Field<ComputeShader>(simulation, "solver")); resources.Add(solver); Set(sim, "solver", solver);
        foreach (string name in new[] { "minimum", "maximum", "restMinimum", "restMaximum" }) Set(sim, name, Field<Vector3>(simulation, name) + offset);
        Set(lab, "simulation", sim);
        foreach (string name in new[] { "volumeMin", "volumeMax" }) Set(lab, name, Field<Vector3>(template, name) + offset);
        Set(lab, "automatic", false); Set(lab, "paused", true); Set(lab, "solid", false); Set(lab, "showControls", false); Set(lab, "smokeSeed", 37f);
        return lab;
    }
    private static void SetVisible(List<Behaviour> labs, List<BlueprintFogVisual> dense, List<BlueprintFogVisual> current, int stream)
    { for (int i = 0; i < labs.Count; i++) { Field<GameObject>(labs[i], "ghostBuilding").SetActive(stream == 0); dense[i].Root.SetActive(stream == 1); current[i].Root.SetActive(stream == 2); } }
    private static Vector3 Position(float time, int side, float width)
    {
        float p = time < 2 ? -1 : time < 3 ? Mathf.Lerp(-1, 0, time - 2) : time < 4 ? 0 : Mathf.Clamp01(time - 4);
        return side < 2 ? new Vector3(p * (side == 0 ? 1.8f : -1.8f), .95f, 0)
            : new Vector3(-.12f, .95f, p * (side == 2 ? 1 : -1) * (width * 2 + 1.8f));
    }
    private static void Sequential(RenderTexture image, RenderTexture target, int count, Action<int, RenderTexture, RenderTexture> pass)
    {
        var a = RenderTexture.GetTemporary(image.descriptor); var b = RenderTexture.GetTemporary(image.descriptor);
        try { Graphics.Blit(image, a); for (int i = 0; i < count; i++) { pass(i, a, b); var swap = a; a = b; b = swap; } Graphics.Blit(a, target); }
        finally { RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b); }
    }
    private static Color32[] Render(Camera camera, string output, int side, string stream, int tick)
    {
        var probe = camera.GetComponent<FogRenderProbe>();
        Require(probe && probe.Render != null, "Matched camera callback missing.");
        var target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
        var before = camera.targetTexture; var active = RenderTexture.active; Texture2D copy = null;
        var draw = probe.Render; Exception failure = null; int calls = 0;
        try
        {
            probe.Render = (source, destination) =>
            {
                ++calls;
                try { draw(source, destination); }
                catch (Exception error) { failure = error; Graphics.Blit(source, destination); }
            };
            camera.targetTexture = target; camera.Render();
            if (failure != null) throw new InvalidOperationException("Matched volume callback failed.", failure);
            Require(calls == 1, "Matched camera callback did not run exactly once.");
            RenderTexture.active = target;
            copy = new Texture2D(960, 540, TextureFormat.RGB24, false); copy.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); copy.Apply();
            byte[] png = copy.EncodeToPNG(); writtenBytes = checked(writtenBytes + png.Length);
            Require(writtenBytes <= 2L * 1024 * 1024 * 1024, "Matched PNG output budget exceeded.");
            File.WriteAllBytes(Path.Combine(output, "side" + side, stream, tick.ToString("D5") + ".png"), png); return copy.GetPixels32();
        }
        finally { probe.Render = draw; camera.targetTexture = before; RenderTexture.active = active; if (copy) Object.DestroyImmediate(copy); RenderTexture.ReleaseTemporary(target); }
    }
    private static void CheckRenderFailure()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-render-failure-260-v2");
        Require(!Directory.Exists(output), "Preserve previous render failure evidence.");
        Directory.CreateDirectory(output);
        var host = new GameObject("Matched callback failure check");
        try
        {
            Camera camera = host.AddComponent<Camera>(); var probe = host.AddComponent<FogRenderProbe>();
            const string marker = "Expected matched callback rejection";
            probe.Render = (source, target) => throw new InvalidOperationException(marker);
            bool rejected = false;
            try { Render(camera, output, 0, "rejected", 0); }
            catch (InvalidOperationException error) when (error.InnerException?.Message == marker) { rejected = true; }
            Require(rejected && probe.Frames == 1 && Directory.GetFiles(output, "*.png", SearchOption.AllDirectories).Length == 0,
                "Failed camera callback produced a frame or lost its exception.");
            File.WriteAllText(Path.Combine(output, "verification.json"), "{\"callbackExceptionPropagated\":true,\"pngFiles\":0}");
        }
        finally { Object.DestroyImmediate(host); }
    }
    private static double Delta(Color32[] a, Color32[] b)
    { Require(a.Length == b.Length, "Frame size mismatch."); long sum = 0; for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b); return sum / (a.Length * 3d * 255); }
    [Serializable] private sealed class Frame { public int side, tick; public float time; public Vector3 actor; public string phase; public double referenceDenseMean, densePublishedMean; }
    [Serializable] private sealed class Report
    {
        public bool captureComplete, probeOnly, nativeGameTested = false, ownerVisualAcceptance = false;
        public string referenceScope;
        public string timing = "Fixed simulation30Hz, not runtime FPS";
        public string denseComposition;
        public string densePublishedMetric = "Normalized whole-frame RGB MAE: denseComposition versus current-published";
        public int overlapChecks; public long denseAtlasBytes;
        public int sourceComparisons, sections, directions, width = 960, height = 540; public float sectionWidth, fov;
        public Vector3 cameraPosition; public Quaternion cameraRotation; public Vector2 body; public List<Frame> frames = new List<Frame>();
    }
}
