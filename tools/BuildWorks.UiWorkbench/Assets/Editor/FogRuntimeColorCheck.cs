using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogRuntimeColorCheck
{
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-color-check"); Directory.CreateDirectory(output);
        ColorSpace before = PlayerSettings.colorSpace;
        try
        {
            Require(QualitySettings.activeColorSpace == ColorSpace.Gamma, "Color regression must start in the approved Gamma Workbench");
            bool gammaHostRejected = false;
            try { using (var invalid = BlueprintFogAssets.LoadEmbedded()) { } }
            catch (NotSupportedException) { gammaHostRejected = true; }
            Require(gammaHostRejected, "Embedded Linear shader load accepted a Gamma host");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Require(QualitySettings.activeColorSpace == ColorSpace.Linear, "Editor is not rendering Linear");
            EditorSceneManager.OpenScene("Assets/FogGhostLab/FogWallStudy.unity");
            Type labType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BuildWorks.FogStudy.FogGhostLab")).First(t => t != null);
            var lab = (Behaviour)UnityEngine.Object.FindFirstObjectByType(labType);
            lab.enabled = false;
            Camera camera = lab.GetComponent<Camera>(); camera.allowHDR = true; camera.renderingPath = RenderingPath.DeferredShading;
            GameObject solid = Field<GameObject>(lab, "solidBuilding");
            Field<GameObject>(lab, "ghostBuilding").SetActive(false); solid.SetActive(false);
            Transform actor = Field<Transform>(lab, "actor");
            Bounds body = actor.GetComponent<Collider>().bounds;
            var capsule = new Vector2(Mathf.Max(body.extents.x, body.extents.z), body.extents.y);
            Color32[] gamma, direct, linear;
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-gamma/buildworks-fog-v11"))))
            using (var visual = new BlueprintFogVisual(assets, solid, Vector3.zero, Quaternion.identity, Vector3.one, null, Vector3.zero, actor.position, capsule))
            {
                gamma = Render(camera, Path.Combine(output, "linear-host-gamma-bundle.png"));
                Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/BuildWorksFogRuntime/TexturedGhost.shader");
                Require(shader && shader != assets.Surface, "Direct source shader not distinct from bundle");
                foreach (Renderer renderer in visual.Root.GetComponentsInChildren<Renderer>())
                    foreach (Material material in renderer.sharedMaterials) material.shader = shader;
                visual.AdvanceTo(0, actor.position, capsule, true);
                direct = Render(camera, Path.Combine(output, "linear-host-direct-source.png"));
            }
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using (var visual = new BlueprintFogVisual(assets, solid, Vector3.zero, Quaternion.identity, Vector3.one, null, Vector3.zero, actor.position, capsule))
                linear = Render(camera, Path.Combine(output, "linear-host-linear-bundle.png"));
            double gammaDelta = Delta(direct, gamma), linearDelta = Delta(direct, linear);
            Require(linearDelta < .005, "Linear compiled bundle differs from direct Linear shader source");
            Require(gammaDelta >= .005, "Wrong-space negative control did not reproduce the mismatch");
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, hostColorSpace = QualitySettings.activeColorSpace.ToString(), hdr = camera.allowHDR,
                renderingPath = camera.actualRenderingPath.ToString(), gammaBundleDelta = gammaDelta, linearBundleDelta = linearDelta,
                gammaMismatchReproduced = gammaDelta >= .005, gammaHostRejected = gammaHostRejected, gameHostTested = false }, true));
        }
        finally { PlayerSettings.colorSpace = before; AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); }
    }
    private static Color32[] Render(Camera camera, string path)
    {
        var target = RenderTexture.GetTemporary(960, 540, 24, RenderTextureFormat.ARGBHalf);
        var previous = camera.targetTexture; var active = RenderTexture.active; Texture2D copy = null;
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            copy = new Texture2D(960, 540, TextureFormat.RGB24, false); copy.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); copy.Apply();
            File.WriteAllBytes(path, copy.EncodeToPNG()); return copy.GetPixels32();
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; if (copy) UnityEngine.Object.DestroyImmediate(copy); RenderTexture.ReleaseTemporary(target); }
    }
    private static double Delta(Color32[] a, Color32[] b)
    {
        Require(a.Length == b.Length, "Frame dimensions differ"); double sum = 0;
        for (int i = 0; i < a.Length; ++i) sum += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
        return sum / (a.Length * 3 * 255d);
    }
    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, hdr, gammaMismatchReproduced, gammaHostRejected, gameHostTested;
        public string hostColorSpace, renderingPath; public double gammaBundleDelta, linearBundleDelta;
    }
}
