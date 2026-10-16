using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogRuntimePairCheck
{
    private static readonly Vector3 Beam = new Vector3(-4, 1, 0);
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-pair"); Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var cameraObject = new GameObject("Fog pair camera"); var camera = cameraObject.AddComponent<Camera>();
        camera.transform.position = new Vector3(4, 2.5f, -5); camera.transform.LookAt(new Vector3(0, 1, 0));
        camera.fieldOfView = 49; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.15f, .2f, .3f);
        camera.allowHDR = true; camera.renderingPath = RenderingPath.DeferredShading; camera.depthTextureMode |= DepthTextureMode.Depth;
        var lightObject = new GameObject("Fog pair light"); var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(35, -25, 0); light.intensity = 1.3f;
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.name = "Owned pair test wall";
        prefab.transform.localScale = new Vector3(.18f, 2, 1.8f); prefab.SetActive(false);
        var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
        texture.SetPixels(Enumerable.Range(0, 64).Select(i => (i / 8 + i % 8) % 2 == 0 ? new Color(.9f, .25f, .12f) : new Color(.2f, .45f, .9f)).ToArray()); texture.Apply();
        var original = new Material(Shader.Find("Standard")); original.mainTexture = texture; prefab.GetComponent<Renderer>().sharedMaterial = original;
        var probe = cameraObject.AddComponent<FogRenderProbe>();
        BlueprintFogVisual a = null, b = null; int frames = 0; double worstOrder = 0, sequentialOrder = 0, sequentialDifference = 0;
        double singleDelta = 0, removalDelta = 0, translatedDelta = 0, occlusionDelta = 0; Color[] untranslated = null;
        Vector2 body = new Vector2(.3f, .95f); Vector3 bOrigin = new Vector3(.35f, 0, .2f);
        ColorSpace previousSpace = PlayerSettings.colorSpace;
        try
        {
            PlayerSettings.colorSpace = ColorSpace.Linear; AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Require(QualitySettings.activeColorSpace == ColorSpace.Linear, "Pair must render in native Linear color space");
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using (var pool = new BlueprintFogChannels())
            using (var volume = new BlueprintFogVolume(assets))
            {
                a = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.identity, Vector3.one, null, Vector3.zero, Actor(0), body, pool, "A");
                b = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.Euler(0, 90, 0), new Vector3(1.2f, .8f, .9f), null, bOrigin, Actor(0), body, pool, "B");
                bool duplicateRejected = false, fullRejected = false;
                try { using (var duplicate = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.identity, Vector3.one, null, Vector3.zero, Actor(0), body, pool, "A")) { } }
                catch (InvalidOperationException) { duplicateRejected = true; }
                try { using (var extra = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.identity, Vector3.one, null, Vector3.zero, Actor(0), body, pool, "C")) { } }
                catch (InvalidOperationException) { fullRejected = true; }
                Require(duplicateRejected && fullRejected && pool.Count == 2 && a.Simulation.Smoke && b.Simulation.Smoke, "Visual acquisition damaged existing owners");
                b.Dispose(); bool spanRejected = false;
                using (var far = new BlueprintFogVisual(assets, prefab, new Vector3(100, 1, 0), Quaternion.identity, Vector3.one, null, new Vector3(100, 0, 0), Actor(0), body, pool, "far"))
                {
                    try { volume.Render(camera, null, null, a, far, Beam, Vector3.right, 2); }
                    catch (InvalidOperationException error) { spanRejected = error.Message.Contains("span"); }
                }
                Require(spanRejected && pool.Count == 1 && a.Simulation.Smoke, "Oversized pair not rejected safely");
                b = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.Euler(0, 90, 0), new Vector3(1.2f, .8f, .9f), null, bOrigin, Actor(0), body, pool, "B");
                Require(a.Root.GetComponentsInChildren<Collider>(true).Length == 0 && b.Root.GetComponentsInChildren<Collider>(true).Length == 0, "Pair has colliders");
                b.Root.SetActive(false);
                Color[] direct = Capture(camera, probe, (image, target) => a.RenderVolume(camera, image, target, Beam, Vector3.right, 2), output, "single-direct");
                Color[] wrapped = Capture(camera, probe, (image, target) => volume.Render(camera, image, target, a, null, Beam, Vector3.right, 2), output, "single-wrapper");
                singleDelta = Delta(direct, wrapped); Require(singleDelta == 0, "One-owner path changed the approved integration"); b.Root.SetActive(true);
                for (int tick = 0; tick <= 420; ++tick)
                {
                    Vector3 actor = Actor(tick); float time = tick * BlueprintFogSimulation.Step;
                    a.AdvanceTo(time, actor, body, true); b.AdvanceTo(time, actor, body, true);
                    if (tick == 45) Require(MaxAlpha(a.Simulation.Surface) > .5f && MaxAlpha(b.Simulation.Surface) == 0, "Perpendicular A-only contact wrong");
                    if (tick != 0 && tick != 45 && tick != 96 && tick != 420) continue;
                    Color[] ab = Capture(camera, probe, (image, target) => volume.Render(camera, image, target, a, b, Beam, Vector3.right, 2), output, tick + "-AB");
                    Color[] ba = Capture(camera, probe, (image, target) => volume.Render(camera, image, target, b, a, Beam, Vector3.right, 2), output, tick + "-BA");
                    worstOrder = Math.Max(worstOrder, Delta(ab, ba)); ++frames;
                    if (tick == 96)
                    {
                        Require(MaxAlpha(a.Simulation.Surface) > .5f && MaxAlpha(b.Simulation.Surface) > .5f, "Both contacts absent");
                        untranslated = ab;
                        Color[] wrongAB = Capture(camera, probe, (image, target) => Sequential(camera, image, target, a, b), output, "96-sequential-AB");
                        Color[] wrongBA = Capture(camera, probe, (image, target) => Sequential(camera, image, target, b, a), output, "96-sequential-BA");
                        sequentialOrder = Delta(wrongAB, wrongBA); sequentialDifference = Delta(ab, wrongAB);
                        var occluder = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        try
                        {
                            occluder.transform.SetPositionAndRotation(camera.transform.position + camera.transform.forward, camera.transform.rotation);
                            occluder.transform.localScale = new Vector3(12, 12, .1f); occluder.GetComponent<Renderer>().sharedMaterial = original;
                            Color[] opaque = Capture(camera, probe, (image, target) => Graphics.Blit(image, target), output, "96-opaque-reference");
                            Color[] hidden = Capture(camera, probe, (image, target) => volume.Render(camera, image, target, a, b, Beam, Vector3.right, 2), output, "96-opaque-pair");
                            occlusionDelta = Delta(opaque, hidden); Require(occlusionDelta == 0, "Fog drew through opaque depth");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(occluder); }
                    }
                }
                Require(worstOrder < .000001 && sequentialOrder > .0000001 && sequentialDifference > .00001, "Pair order/negative control failed");
                Require(MaxAlpha(a.Simulation.Surface) == 0 && MaxAlpha(b.Simulation.Surface) == 0, "Pair did not recover");
                var retired = a.Root; a.Dispose();
                Require(!retired && pool.Count == 1 && b.Simulation.Smoke, "A cleanup removed B");
                direct = Capture(camera, probe, (image, target) => b.RenderVolume(camera, image, target, Beam, Vector3.right, 2), output, "remaining-direct");
                wrapped = Capture(camera, probe, (image, target) => volume.Render(camera, image, target, b, null, Beam, Vector3.right, 2), output, "remaining-wrapper");
                removalDelta = Delta(direct, wrapped); Require(removalDelta == 0, "Remaining owner render changed"); b.Dispose();
                Vector3 shift = new Vector3(2500, 45, -1900); camera.transform.position += shift; light.transform.position += shift;
                a = new BlueprintFogVisual(assets, prefab, shift + Vector3.up, Quaternion.identity, Vector3.one, null, shift, shift + Actor(0), body, pool, "A");
                b = new BlueprintFogVisual(assets, prefab, shift + Vector3.up, Quaternion.Euler(0, 90, 0), new Vector3(1.2f, .8f, .9f), null, shift + bOrigin, shift + Actor(0), body, pool, "B");
                for (int tick = 0; tick <= 96; ++tick)
                { a.AdvanceTo(tick * BlueprintFogSimulation.Step, shift + Actor(tick), body, true); b.AdvanceTo(tick * BlueprintFogSimulation.Step, shift + Actor(tick), body, true); }
                Color[] translated = Capture(camera, probe, (image, target) => volume.Render(camera, image, target, a, b, shift + Beam, Vector3.right, 2), output, "96-translated");
                translatedDelta = Delta(untranslated, translated); Require(translatedDelta < .005, "World translation broke pair rendering");
                BlueprintFogVisual old = a; var oldRoot = a.Root; pool.Remove("A");
                a = new BlueprintFogVisual(assets, prefab, shift + Vector3.up, Quaternion.identity, Vector3.one, null, shift, shift + Actor(96), body, pool, "A");
                old.Dispose();
                Require(!oldRoot && pool.Count == 2 && a.Simulation.Smoke && b.Simulation.Smoke, "Late old visual disposal destroyed reused owner");
                a.Dispose(); b.Dispose(); Require(pool.Count == 0, "Pair cleanup retained owners");
            }
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, frames = frames, orderIndependent = true, singleExact = true, sequentialNegative = true, removalIsolated = true,
                pooledVisualGuards = true, translated = true, spanRejected = true, opaqueDepthExact = true, reusedOwnerRetained = true, colorSpace = QualitySettings.activeColorSpace.ToString(),
                occlusionDelta = occlusionDelta, maxOrderDelta = worstOrder, sequentialOrderDelta = sequentialOrder,
                sequentialVsPairDelta = sequentialDifference, singleDelta = singleDelta, removalDelta = removalDelta, translatedDelta = translatedDelta,
                gameHostTested = false, wholeHouseSupported = false, nativeAssetsTested = false, width = 384, height = 216 }, true));
        }
        finally
        {
            a?.Dispose(); b?.Dispose(); probe.Render = null;
            UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(original); UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(lightObject);
            PlayerSettings.colorSpace = previousSpace; AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
    }
    private static Vector3 Actor(int tick) => tick < 30 ? Vector3.Lerp(new Vector3(-1.4f, .95f, .75f), new Vector3(0, .95f, .75f), tick / 30f)
        : tick <= 45 ? new Vector3(0, .95f, .75f) : tick < 80 ? Vector3.Lerp(new Vector3(0, .95f, .75f), new Vector3(0, .95f, 0), (tick - 45) / 35f)
        : tick <= 120 ? new Vector3(0, .95f, 0) : new Vector3(1.8f, .95f, 1.8f);
    private static void Sequential(Camera camera, RenderTexture image, RenderTexture target, BlueprintFogVisual a, BlueprintFogVisual b)
    {
        var intermediate = RenderTexture.GetTemporary(image.width, image.height, 0, RenderTextureFormat.ARGBHalf);
        try { a.RenderVolume(camera, image, intermediate, Beam, Vector3.right, 2); b.RenderVolume(camera, intermediate, target, Beam, Vector3.right, 2); }
        finally { RenderTexture.ReleaseTemporary(intermediate); }
    }
    private static Color[] Capture(Camera camera, FogRenderProbe probe, Action<RenderTexture, RenderTexture> render, string output, string name)
    {
        var target = RenderTexture.GetTemporary(384, 216, 24, RenderTextureFormat.ARGBHalf); var old = camera.targetTexture; var active = RenderTexture.active;
        Texture2D read = null, preview = null; int before = probe.Frames;
        try
        {
            probe.Render = render; camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            Require(probe.Frames == before + 1, "Pair callback not executed exactly once");
            read = new Texture2D(384, 216, TextureFormat.RGBAFloat, false); read.ReadPixels(new Rect(0, 0, 384, 216), 0, 0); read.Apply();
            Color[] values = read.GetPixels(); Require(values.All(c => Finite(c.r) && Finite(c.g) && Finite(c.b) && Finite(c.a)), "Nonfinite pair image");
            preview = new Texture2D(384, 216, TextureFormat.RGB24, false); preview.SetPixels(values); preview.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), preview.EncodeToPNG()); return values;
        }
        finally { camera.targetTexture = old; RenderTexture.active = active; if (read) UnityEngine.Object.DestroyImmediate(read); if (preview) UnityEngine.Object.DestroyImmediate(preview); RenderTexture.ReleaseTemporary(target); }
    }
    private static float MaxAlpha(RenderTexture field)
    {
        var request = AsyncGPUReadback.Request(field, 0, TextureFormat.RGBAFloat); request.WaitForCompletion(); Require(!request.hasError, "Pair readback failed");
        float max = 0; int count = 0;
        for (int layer = 0; layer < request.layerCount; ++layer) { var data = request.GetData<float>(layer); count += data.Length; for (int i = 3; i < data.Length; i += 4) { Require(Finite(data[i]), "Nonfinite field"); max = Mathf.Max(max, data[i]); } }
        Require(count == field.width * field.height * field.volumeDepth * 4, "Incomplete pair field"); return max;
    }
    private static double Delta(Color[] a, Color[] b)
    { Require(a.Length == b.Length, "Image dimensions differ"); double sum = 0; for (int i = 0; i < a.Length; ++i) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b); return sum / (a.Length * 3); }
    private static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, orderIndependent, singleExact, sequentialNegative, removalIsolated, pooledVisualGuards, translated, spanRejected, opaqueDepthExact, reusedOwnerRetained,
            gameHostTested, wholeHouseSupported, nativeAssetsTested;
        public int frames, width, height;
        public string colorSpace;
        public double maxOrderDelta, sequentialOrderDelta, sequentialVsPairDelta, singleDelta, removalDelta, translatedDelta, occlusionDelta;
    }
}
