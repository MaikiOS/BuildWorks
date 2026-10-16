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

public static class FogRuntimeVisualCheck
{
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);

    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-visual"); Directory.CreateDirectory(output);
        string scene = "Assets/FogGhostLab/FogWallStudy.unity";
        if (!File.Exists(scene)) throw new InvalidOperationException("Local approved wall scene required; do not download or publish its game assets.");
        EditorSceneManager.OpenScene(scene);
        Type labType = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType("BuildWorks.FogStudy.FogGhostLab")).FirstOrDefault(type => type != null);
        if (labType == null) throw new InvalidOperationException("Local approved v11 controller unavailable.");
        var lab = (Behaviour)UnityEngine.Object.FindFirstObjectByType(labType);
        Camera camera = lab.GetComponent<Camera>();
        Vector3 baseCameraPosition = camera.transform.position;
        Set(lab, "paused", true); Set(lab, "automatic", true); Set(lab, "scenario", 0); Set(lab, "showControls", false);
        Set(lab, "solid", false); labType.GetMethod("SetTime").Invoke(lab, new object[] { 0f });
        Transform actor = Field<Transform>(lab, "actor"); GameObject originalGhost = Field<GameObject>(lab, "ghostBuilding");
        GameObject originalSolid = Field<GameObject>(lab, "solidBuilding");
        Vector3 beamOrigin = Field<Vector3>(lab, "beamOrigin"), beamDirection = Field<Vector3>(lab, "beamDirection");
        float beamStrength = Field<float>(lab, "beamStrength");
        var bodyBounds = actor.GetComponent<Collider>().bounds;
        Vector2 body = new Vector2(Mathf.Max(bodyBounds.extents.x, bodyBounds.extents.z), bodyBounds.extents.y);
        FogRenderProbe probe = camera.gameObject.AddComponent<FogRenderProbe>(); probe.enabled = false;
        camera.depthTextureMode |= DepthTextureMode.Depth;
        BlueprintFogVisual visual = null;
        var frames = new List<Frame>();
        var untranslated = new Dictionary<int, Color32[]>();
        var translated = new List<Frame>();
        try
        {
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            {
                visual = new BlueprintFogVisual(assets, originalSolid, Vector3.zero, Quaternion.identity, Vector3.one, null,
                    Vector3.zero, actor.position, body);
                Require(visual.Root.GetComponentsInChildren<Collider>(true).Length == 0, "Fog visual has collision");
                Require(visual.Root.GetComponentsInChildren<Piece>(true).Length == 0, "Fog visual has native pieces");
                Require(visual.Root.GetComponentsInChildren<ZNetView>(true).Length == 0, "Fog visual has network objects");
                var oldSimulation = Field<object>(lab, "simulation");
                Texture3D oldShape = Field<Texture3D>(oldSimulation, "shape"), oldPigment = Field<Texture3D>(oldSimulation, "pigment"), oldNoise = Field<Texture3D>(oldSimulation, "noise");
                bool shapeEqual = oldShape.GetPixelData<byte>(0).ToArray().SequenceEqual(visual.Source.Shape.GetPixelData<byte>(0).ToArray());
                bool pigmentEqual = oldPigment.GetPixelData<byte>(0).ToArray().SequenceEqual(visual.Source.Pigment.GetPixelData<byte>(0).ToArray());
                bool noiseEqual = oldNoise.GetPixelData<byte>(0).ToArray().SequenceEqual(visual.Source.Noise.GetPixelData<byte>(0).ToArray());
                File.WriteAllText(Path.Combine(output, "source-comparison.json"), JsonUtility.ToJson(new Sources { shapeEqual = shapeEqual, pigmentEqual = pigmentEqual, noiseEqual = noiseEqual }, true));
                Require(shapeEqual && pigmentEqual && noiseEqual, "Runtime source differs from the approved one-wall source");
                probe.Render = (image, target) => visual.RenderVolume(camera, image, target, beamOrigin, beamDirection, beamStrength);
                foreach (int tick in Enumerable.Range(0, 271))
                {
                    float time = tick * BlueprintFogSimulation.Step;
                    labType.GetMethod("SetTime").Invoke(lab, new object[] { time });
                    visual.AdvanceTo(time, actor.position, body, true);
                    if (tick != 0 && tick != 78 && tick != 96 && tick != 120 && tick != 180 && tick != 270) continue;
                    visual.Root.SetActive(false); originalGhost.SetActive(true); lab.enabled = true; probe.enabled = false;
                    Color32[] reference = Render(camera, Path.Combine(output, tick.ToString("D3") + "-reference.png"));
                    originalGhost.SetActive(false); lab.enabled = false; visual.Root.SetActive(true); probe.enabled = true;
                    int beforeFrame = probe.Frames;
                    Color32[] candidate = Render(camera, Path.Combine(output, tick.ToString("D3") + "-runtime.png"));
                    Require(probe.Frames > beforeFrame, "Runtime image-effect callback did not render");
                    if (tick == 0 || tick == 78) untranslated.Add(tick, candidate);
                    double delta = 0; int changed = 0;
                    for (int i = 0; i < reference.Length; ++i)
                    {
                        int d = Math.Abs(reference[i].r - candidate[i].r) + Math.Abs(reference[i].g - candidate[i].g) + Math.Abs(reference[i].b - candidate[i].b);
                        delta += d; if (d != 0) ++changed;
                    }
                    frames.Add(new Frame { tick = tick, meanRgbDelta = delta / (reference.Length * 3 * 255d), changedPixels = changed });
                }
                Require(frames.All(frame => frame.meanRgbDelta < .005), "Render differs beyond the v11 reference tolerance");
                visual.Dispose();
                Vector3 shift = new Vector3(2500, 45, -1900);
                foreach (GameObject node in camera.gameObject.scene.GetRootGameObjects()) node.transform.position += shift;
                actor.position = shift + Position(0);
                visual = new BlueprintFogVisual(assets, originalSolid, shift, Quaternion.identity, Vector3.one, null, shift, actor.position, body);
                for (int tick = 0; tick <= 78; ++tick)
                {
                    float time = tick * BlueprintFogSimulation.Step; actor.position = shift + Position(time);
                    visual.AdvanceTo(time, actor.position, body, true);
                    if (tick != 0 && tick != 78) continue;
                    int beforeFrame = probe.Frames;
                    probe.Render = (image, target) => visual.RenderVolume(camera, image, target, beamOrigin + shift, beamDirection, beamStrength);
                    Color32[] image = Render(camera, Path.Combine(output, tick.ToString("D3") + "-translated-runtime.png"));
                    Require(probe.Frames > beforeFrame, "Translated runtime callback did not render");
                    double delta = 0; int changed = 0; Color32[] before = untranslated[tick];
                    for (int i = 0; i < before.Length; ++i)
                    {
                        int d = Math.Abs(before[i].r - image[i].r) + Math.Abs(before[i].g - image[i].g) + Math.Abs(before[i].b - image[i].b);
                        delta += d; if (d != 0) ++changed;
                    }
                    translated.Add(new Frame { tick = tick, changedPixels = changed, meanRgbDelta = delta / (before.Length * 3 * 255d) });
                }
                Require(translated.All(frame => frame.meanRgbDelta < .005), "Distant world coordinates broke origin invariance");
                var report = new Report { passed = true, gameHostTested = false, ownerVisualAcceptance = false,
                    unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(), frames = frames.ToArray(),
                    shapeEqual = shapeEqual, pigmentEqual = pigmentEqual, noiseEqual = noiseEqual,
                    cameraPosition = baseCameraPosition, translatedCameraPosition = camera.transform.position,
                    cameraRotation = camera.transform.rotation, fov = camera.fieldOfView,
                    width = 960, height = 540, translatedFrames = translated.ToArray(), translation = shift };
                File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(report, true));
            }
        }
        finally
        {
            probe.Render = null; UnityEngine.Object.DestroyImmediate(probe); visual?.Dispose();
            lab.enabled = false; originalGhost.SetActive(false);
        }
        Debug.Log("BuildWorks v11 runtime rendering captured; inspect source parity and frame comparison before visual acceptance.");
    }

    private static Vector3 Position(float time)
    { return new Vector3(Mathf.Lerp(-1.8f, 1.8f, Mathf.Clamp01((time - 2) / 1.2f)), .95f, 0); }

    private static Color32[] Render(Camera camera, string path)
    {
        RenderTexture target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGB32);
        RenderTexture before = camera.targetTexture, active = RenderTexture.active; Texture2D copy = null;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            copy = new Texture2D(960, 540, TextureFormat.RGB24, false);
            copy.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); copy.Apply();
            File.WriteAllBytes(path, copy.EncodeToPNG()); return copy.GetPixels32();
        }
        finally { camera.targetTexture = before; RenderTexture.active = active; if (copy) UnityEngine.Object.DestroyImmediate(copy); RenderTexture.ReleaseTemporary(target); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Sources { public bool shapeEqual, pigmentEqual, noiseEqual; }
    [Serializable] private sealed class Frame { public int tick, changedPixels; public double meanRgbDelta; }
    [Serializable] private sealed class Report
    {
        public bool passed, gameHostTested, ownerVisualAcceptance, shapeEqual, pigmentEqual, noiseEqual;
        public string unity, graphics; public Frame[] frames; public Vector3 cameraPosition; public Quaternion cameraRotation;
        public Frame[] translatedFrames; public Vector3 translation, translatedCameraPosition;
        public float fov; public int width, height;
    }
}
