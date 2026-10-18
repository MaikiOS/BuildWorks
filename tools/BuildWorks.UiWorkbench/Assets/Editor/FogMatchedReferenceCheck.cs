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

// Same scene/camera/input: isolate the renderer from differences in native fixtures or host lighting.
public static class FogMatchedReferenceCheck
{
    public static void RunGamma() => Run(ColorSpace.Gamma);
    public static void RunLinear() => Run(ColorSpace.Linear);
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Run(ColorSpace space)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-matched-reference-225/" + space); Directory.CreateDirectory(output);
        ColorSpace previous = PlayerSettings.colorSpace;
        var report = new Report { colorSpace = space.ToString() };
        try
        {
            PlayerSettings.colorSpace = space; AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Require(QualitySettings.activeColorSpace == space, "Requested comparison color space unavailable.");
            EditorSceneManager.OpenScene("Assets/FogGhostLab/FogWallStudy.unity");
            Type labType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BuildWorks.FogStudy.FogGhostLab")).First(t => t != null);
            var lab = (Behaviour)UnityEngine.Object.FindFirstObjectByType(labType); Camera camera = lab.GetComponent<Camera>();
            Set(lab, "paused", true); Set(lab, "automatic", true); Set(lab, "showControls", false); Set(lab, "solid", false);
            GameObject ghost = Field<GameObject>(lab, "ghostBuilding"), solid = Field<GameObject>(lab, "solidBuilding");
            Transform actor = Field<Transform>(lab, "actor"); Bounds bodyBounds = actor.GetComponent<Collider>().bounds;
            var body = new Vector2(Mathf.Max(bodyBounds.extents.x, bodyBounds.extents.z), bodyBounds.extents.y);
            Vector3 beam = Field<Vector3>(lab, "beamOrigin"), direction = Field<Vector3>(lab, "beamDirection");
            float strength = Field<float>(lab, "beamStrength");
            camera.depthTextureMode |= DepthTextureMode.Depth;
            var probe = camera.gameObject.AddComponent<FogRenderProbe>(); probe.enabled = false;
            string bundle = space == ColorSpace.Gamma ? "artifacts/fog-window-gamma-225/buildworks-fog-v11" : "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11";
            try
            {
                using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, bundle))))
                    for (int scenario = 0; scenario < 4; ++scenario)
                    {
                        Set(lab, "scenario", scenario); labType.GetMethod("SetTime").Invoke(lab, new object[] { 0f });
                        using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D))
                        using (var actual = new BlueprintFogVisual(assets, solid, Vector3.zero, Quaternion.identity, Vector3.one, null, Vector3.zero, actor.position, body, pool))
                        using (var dense = new BlueprintFogVisual(assets, solid, Vector3.zero, Quaternion.identity, Vector3.one, null, Vector3.zero, actor.position, body, ledgerLayout: BlueprintFogLedgerLayout.Spatial3D))
                        using (var atlas = new BlueprintFogAtlas(assets, pool))
                        using (var codec = new FogSparseStorageProbe())
                        {
                            var originals = new[] { Field<Texture3D>(Field<object>(lab, "simulation"), "shape"), Field<Texture3D>(Field<object>(lab, "simulation"), "pigment"), Field<Texture3D>(Field<object>(lab, "simulation"), "noise") };
                            var sources = new[] { actual.Source.Shape, actual.Source.Pigment, actual.Source.Noise };
                            for (int i = 0; i < 3; ++i) Require(originals[i].GetPixelData<byte>(0).ToArray().SequenceEqual(sources[i].GetPixelData<byte>(0).ToArray()), "Source bake differs before renderer comparison: " + i);
                            for (int tick = 0; tick <= 270; ++tick)
                            {
                                float time = tick * BlueprintFogSimulation.Step; labType.GetMethod("SetTime").Invoke(lab, new object[] { time });
                                actual.AdvanceTo(time, actor.position, body, true); dense.AdvanceTo(time, actor.position, body, true);
                                pool.PumpSources(); AsyncGPUReadback.WaitAllRequests(); pool.PumpSources();
                                if (!new[] { 0, 66, 78, 96, 120, 150, 180, 210, 270 }.Contains(tick)) continue;
                                var snapshot = actual.Snapshot;
                                Require(snapshot != null && snapshot.State.State == BlueprintFogSnapshotState.Phase.Ready && actual.LogicalSource.Inputs.Count == 0, "Unconfirmed published tick.");
                                actual.Root.SetActive(false); dense.Root.SetActive(false); ghost.SetActive(true); lab.enabled = true; probe.enabled = false;
                                string label = "case" + scenario + "-" + tick.ToString("D3");
                                Color32[] reference = Render(camera, Path.Combine(output, label + "-reference.png"));
                                var frozen = Field<object>(lab, "simulation"); codec.BeginComparison();
                                codec.Compare(dense.Simulation.Smoke, (RenderTexture)frozen.GetType().GetProperty("Smoke").GetValue(frozen));
                                codec.Compare(dense.Simulation.Flow, (RenderTexture)frozen.GetType().GetProperty("Flow").GetValue(frozen));
                                codec.Compare(dense.Simulation.Surface, (RenderTexture)frozen.GetType().GetProperty("Surface").GetValue(frozen));
                                codec.RequireClean(); report.frozenFieldComparisons += 3;
                                ghost.SetActive(false); lab.enabled = false; dense.Root.SetActive(true); probe.enabled = true;
                                probe.Render = (image, target) => dense.RenderVolume(camera, image, target, beam, direction, strength);
                                var denseImage = Render(camera, Path.Combine(output, label + "-dense.png"));
                                dense.Root.SetActive(false); actual.Root.SetActive(true); var mapping = new[] { snapshot };
                                atlas.PreparePublished(new[] { actual }, mapping);
                                probe.Render = (image, target) => atlas.Render(camera, image, target, new[] { actual }, beam, direction, strength, mapping);
                                var published = Render(camera, Path.Combine(output, label + "-published.png"));
                                double local = 0, publishedDelta = Delta(denseImage, published, out local), referenceDelta = Delta(reference, denseImage, out double referenceLocal);
                                report.frames.Add(new Frame { scenario = scenario, tick = tick, referenceMean = referenceDelta, referenceLocal = referenceLocal, publishedMean = publishedDelta, publishedLocal = local });
                                Require(referenceDelta < .005, "Frozen scene differs beyond existing v11 reference tolerance: " + label + "/" + referenceDelta);
                                Require(publishedDelta < .0001 && local < .03, "Published renderer differs beyond existing dense/published limits: " + label + "/" + publishedDelta + "/" + local);
                            }
                        }
                    }
                report.cameraPosition = camera.transform.position; report.cameraRotation = camera.transform.rotation; report.fov = camera.fieldOfView;
                report.passed = true; File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(report, true));
                Debug.Log("FOG_MATCHED_REFERENCE_PASSED " + space + " frames=" + report.frames.Count);
            }
            finally { probe.Render = null; UnityEngine.Object.DestroyImmediate(probe); lab.enabled = false; ghost.SetActive(false); }
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Debug.LogException(error); EditorApplication.Exit(1); }
        finally { PlayerSettings.colorSpace = previous; AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); }
    }
    private static Color32[] Render(Camera camera, string path)
    {
        var target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
        RenderTexture before = camera.targetTexture, active = RenderTexture.active; Texture2D copy = null;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target; copy = new Texture2D(960, 540, TextureFormat.RGB24, false);
            copy.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); copy.Apply(); File.WriteAllBytes(path, copy.EncodeToPNG()); return copy.GetPixels32();
        }
        finally { camera.targetTexture = before; RenderTexture.active = active; if (copy) UnityEngine.Object.DestroyImmediate(copy); RenderTexture.ReleaseTemporary(target); }
    }
    private static double Delta(Color32[] a, Color32[] b, out double local)
    {
        Require(a.Length == b.Length, "Frame size mismatch."); double sum = 0; local = 0;
        for (int i = 0; i < a.Length; ++i)
        {
            int r = Math.Abs(a[i].r-b[i].r), g = Math.Abs(a[i].g-b[i].g), bDelta = Math.Abs(a[i].b-b[i].b);
            sum += r + g + bDelta; local = Math.Max(local, Math.Max(r, Math.Max(g, bDelta)) / 255d);
        }
        return sum / (a.Length * 3 * 255d);
    }
    [Serializable] private sealed class Frame { public int scenario, tick; public double referenceMean, referenceLocal, publishedMean, publishedLocal; }
    [Serializable] private sealed class Report
    {
        public bool passed, nativeGameTested = false, ownerVisualAcceptance = false;
        public string colorSpace; public int frozenFieldComparisons; public int width = 960, height = 540;
        public Vector3 cameraPosition; public Quaternion cameraRotation; public float fov; public List<Frame> frames = new List<Frame>();
    }
}
