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
            using (var wide = new BlueprintFogSimulation(assets, compact.Source.Shape, compact.Source.Pigment, compact.Source.Noise,
                full.FieldMinimum, full.FieldMaximum, compact.Source.Minimum, compact.Source.Maximum, Position(0), 37))
            using (var cropped = new BlueprintFogSimulation(assets, compact.Source.Shape, compact.Source.Pigment, compact.Source.Noise,
                cropMin, cropMax, compact.Source.Minimum, compact.Source.Maximum, Position(0), 37, cropGrid))
            {
            for (int tick = 0; tick <= 360; ++tick)
            {
                float time = tick * BlueprintFogSimulation.Step; actor.position = Position(time);
                full.AdvanceTo(time, actor.position, body, true); compact.AdvanceTo(time, actor.position, body, true);
                wide.AdvanceTo(time, actor.position, body, true); cropped.AdvanceTo(time, actor.position, body, true);
                if (!capture.Contains(tick)) continue;
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
                    meanRgbDelta = FogRuntimePairCheck.Delta(reference, candidate) });
            }
            if (frames.Count != capture.Length || !frames.Any(frame => frame.fullSmoke > 0 && frame.compactSmoke > 0))
                throw new InvalidOperationException("Domain comparison did not include both active smoke fields.");
            File.WriteAllText(Path.Combine(output, "comparison.json"), JsonUtility.ToJson(new Report {
                collected = true, visualAccepted = false, colorSpace = QualitySettings.activeColorSpace.ToString(),
                cameraPosition = camera.transform.position, cameraRotation = camera.transform.eulerAngles, fov = camera.fieldOfView,
                body = body, fullMin = full.FieldMinimum, fullMax = full.FieldMaximum,
                compactMin = compact.FieldMinimum, compactMax = compact.FieldMaximum,
                sameSourceCropMin = cropMin, sameSourceCropMax = cropMax, sameSourceCropGrid = cropGrid, frames = frames.ToArray()
            }, true));
            }
        }
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
    [Serializable] private sealed class Frame { public int tick; public float seconds; public double fullSmoke, compactSmoke, sameSourceWideSmoke, sameSourceCroppedSmoke, meanRgbDelta; }
    [Serializable] private sealed class Report
    {
        public bool collected, visualAccepted;
        public string colorSpace;
        public Vector3 cameraPosition, cameraRotation, fullMin, fullMax, compactMin, compactMax, sameSourceCropMin, sameSourceCropMax;
        public Vector3Int sameSourceCropGrid;
        public Vector2 body; public float fov; public Frame[] frames;
    }
}
