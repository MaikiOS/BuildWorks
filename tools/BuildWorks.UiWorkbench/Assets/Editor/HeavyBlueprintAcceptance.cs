using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Object = UnityEngine.Object;

public static class HeavyBlueprintAcceptance
{
    public static void Run(string output)
    {
        var holder = new GameObject("ScaledPrefabHolder");
        holder.transform.localScale = new Vector3(100, 30, 5);
        holder.transform.rotation = Quaternion.Euler(10, 40, 25);
        var sources = new List<GameObject>();
        var materials = new List<Material>();
        var preview = new BlueprintBatchedPreview();
        try
        {
            for (int index = 0; index < 3; ++index)
            {
                var source = new GameObject("BatchSource" + index);
                source.transform.SetParent(holder.transform, false);
                source.transform.localScale = new Vector3(0.01f, 0.02f, 0.03f);
                source.AddComponent<Piece>();
                GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(mesh.GetComponent<Collider>());
                mesh.transform.SetParent(source.transform, false);
                mesh.transform.localPosition = new Vector3(1, 2, 3);
                mesh.transform.localRotation = Quaternion.Euler(15, 20, 30);
                mesh.transform.localScale = new Vector3(2, 0.5f, 1);
                var material = new Material(Shader.Find("Standard")) { enableInstancing = true };
                mesh.GetComponent<Renderer>().sharedMaterial = material;
                materials.Add(material);
                sources.Add(source);
                // This is the same parentless Instantiate overload as Player.PlacePiece.
                GameObject native = Object.Instantiate(source, new Vector3(10, 20, 30), Quaternion.Euler(0, 70, 0));
                Require((native.transform.localScale - source.transform.localScale).sqrMagnitude < 1e-10f,
                    "Native Instantiate scale differs from intrinsic prefab local scale");
                Object.DestroyImmediate(native);
                GameObject disabled = GameObject.CreatePrimitive(PrimitiveType.Cube);
                disabled.transform.SetParent(source.transform, false);
                disabled.SetActive(false);
            }
            holder.SetActive(false);
            VerifySmallAndEditorScale(sources[0]);
            foreach (int count in new[] { 513, 5000, 39487, 50000 })
            {
                var plan = new List<PrecisionPlacementSession.TransformSnapshot>(count);
                var pieces = new List<Piece>(count);
                for (int index = 0; index < count; ++index)
                {
                    plan.Add(new PrecisionPlacementSession.TransformSnapshot(new Vector3(index % 250, index / 250, 0),
                        Quaternion.Euler(20, 30, 40), new Vector3(2, 0.5f, 3)));
                    pieces.Add(sources[index % 3].GetComponent<Piece>());
                }
                var watch = System.Diagnostics.Stopwatch.StartNew();
                preview.Prepare(plan, pieces, 0);
                if (count == 513)
                {
                    Require(!preview.Ready && preview.TemplateCount == 2, "Unique templates were not prepared in bounded steps");
                    preview.Prepare(plan, pieces, 0);
                }
                Require(preview.Ready && preview.PreparedCount == count && preview.TemplateCount == 3,
                    "Heavy preview lost parts or allocated per-part templates");
                Require(sources.TrueForAll(source => source.transform.childCount == 2), "Source prefab was modified");
                File.AppendAllText(Path.Combine(output, "heavy-blueprint-check.txt"),
                    "PASS " + count + " parts, 3 templates, prepare " + watch.ElapsedMilliseconds + " ms\n");
                if (count == 513)
                {
                    var root = Matrix4x4.TRS(new Vector3(4, 5, 6), Quaternion.Euler(5, 60, 10), Vector3.one * 1.5f);
                    preview.Draw(plan, new MaterialPropertyBlock(), 0, -1, root);
                    VerifyFirstDrawMatrix(preview, root * Matrix4x4.TRS(plan[0].Position, plan[0].Rotation,
                        Vector3.Scale(sources[0].transform.localScale, plan[0].Scale)) *
                        Matrix4x4.TRS(new Vector3(1, 2, 3), Quaternion.Euler(15, 20, 30), new Vector3(2, 0.5f, 1)));
                    foreach (Material material in materials) material.enableInstancing = false;
                    preview.Draw(plan, new MaterialPropertyBlock(), 0, 0, root);
                    foreach (Material material in materials) material.enableInstancing = true;
                    Require(preview.TemplateCount == 3, "Changing hidden/native index recreated templates");
                    RenderProof(preview, pieces, materials, output);
                }
            }
            preview.Dispose();
            Require(!preview.Ready && preview.TemplateCount == 0, "Heavy preview cleanup left templates");
            File.AppendAllText(Path.Combine(output, "heavy-blueprint-check.txt"),
                "PASS intrinsic native Instantiate; nested matrix/XYZ/root/uniform scale; inactive child; instanced/fallback submissions; hidden index; cleanup\n");
        }
        finally
        {
            preview.Dispose();
            Object.DestroyImmediate(holder);
            foreach (Material material in materials) Object.DestroyImmediate(material);
        }
    }

    private static void VerifySmallAndEditorScale(GameObject source)
    {
        Vector3 multiplier = new Vector3(2, 0.5f, 3);
        Vector3 expected = Vector3.Scale(source.transform.localScale, multiplier);
        using (var small = new PlacementGhostPreviewView())
        {
            small.Show(source, new[] {
                new PrecisionPlacementSession.TransformSnapshot(Vector3.zero, Quaternion.identity, multiplier),
                new PrecisionPlacementSession.TransformSnapshot(Vector3.right, Quaternion.identity, multiplier)
            }, new[] { source.GetComponent<Piece>(), source.GetComponent<Piece>() }, -1);
            var instances = (List<GameObject>)typeof(PlacementGhostPreviewView)
                .GetField("instances", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(small);
            foreach (GameObject instance in instances)
                Require((instance.transform.localScale - expected).sqrMagnitude < 1e-10f,
                    "Small world preview includes the prefab ancestor scale");
        }
        var cameraObject = new GameObject("IntrinsicEditorCamera");
        try
        {
            using (var scene = new BlueprintEditorScene(cameraObject.AddComponent<Camera>(), _ => source))
            {
                var document = new BlueprintEditorDocument(null, "Intrinsic scale", "Test", new[] {
                    new BlueprintEditorPart("part", "wall", "Wall", new Point3(0,0,0),
                        new Rotation3(0,0,0,1), scale: new Point3(2,0.5,3))
                });
                Require(scene.TrySync(document, out string error), error);
                var visuals = (System.Collections.IDictionary)typeof(BlueprintEditorScene)
                    .GetField("visuals", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(scene);
                object visual = visuals["part"];
                var root = (GameObject)visual.GetType().GetField("Root",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(visual);
                Require((root.transform.localScale - expected).sqrMagnitude < 1e-10f,
                    "Editor preview includes the prefab ancestor scale");
            }
        }
        finally { Object.DestroyImmediate(cameraObject); }
    }

    private static void RenderProof(BlueprintBatchedPreview preview, IReadOnlyList<Piece> pieces,
        IReadOnlyList<Material> materials, string output)
    {
        var cameraObject = new GameObject("HeavyPreviewCameras");
        var lightObject = new GameObject("HeavyPreviewLight");
        var target = new RenderTexture(512, 512, 24);
        Color ambient = RenderSettings.ambientLight;
        var ambientMode = RenderSettings.ambientMode;
        RenderTexture previous = RenderTexture.active;
        try
        {
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.cullingMask = 1 << 30;
            light.transform.rotation = Quaternion.Euler(40, 20, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.white;
            var plan = new List<PrecisionPlacementSession.TransformSnapshot>();
            for (int index = 0; index < pieces.Count; ++index)
                plan.Add(new PrecisionPlacementSession.TransformSnapshot(new Vector3(index % 27, index / 27, 0),
                    Quaternion.identity, Vector3.one * 20));
            int firstPixels = 0;
            foreach (bool instanced in new[] { true, false })
            {
                var view = new GameObject(instanced ? "InstancedCamera" : "FallbackCamera");
                view.transform.SetParent(cameraObject.transform, false);
                Camera camera = view.AddComponent<Camera>();
                camera.enabled = false;
                camera.cullingMask = 1 << 30;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = true;
                camera.orthographicSize = 17;
                camera.transform.position = new Vector3(13, 9, -35);
                camera.transform.rotation = Quaternion.identity;
                camera.targetTexture = target;
                foreach (Material material in materials)
                { material.enableInstancing = instanced; material.color = Color.red; }
                preview.Prepare(plan, pieces, 30);
                preview.Draw(plan, new MaterialPropertyBlock(), 30, -1, Matrix4x4.identity, camera);
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(512, 512, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                    int pixels = 0;
                    foreach (Color32 pixel in image.GetPixels32())
                        if (pixel.r > 30 && pixel.r > pixel.g * 1.5f) ++pixels;
                    Require(pixels > 1000, "Heavy preview render is missing visible red geometry");
                    if (instanced) firstPixels = pixels;
                    else Require(Math.Abs(pixels - firstPixels) < firstPixels * 0.1f,
                        "Instanced and fallback rendering disagree");
                    File.WriteAllBytes(Path.Combine(output, instanced ? "heavy-instanced.png" : "heavy-fallback.png"), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
            }
        }
        finally
        {
            RenderTexture.active = previous;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientMode = ambientMode;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lightObject);
            foreach (Material material in materials) material.enableInstancing = true;
        }
    }

    private static void VerifyFirstDrawMatrix(BlueprintBatchedPreview preview, Matrix4x4 expected)
    {
        var batches = (System.Collections.IDictionary)typeof(BlueprintBatchedPreview)
            .GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
        object batch = null;
        foreach (System.Collections.DictionaryEntry entry in batches) { batch = entry.Value; break; }
        var matrices = (Matrix4x4[])batch.GetType().GetField("Matrices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(batch);
        for (int index = 0; index < 16; ++index)
            Require(Mathf.Abs(matrices[0][index] - expected[index]) < 0.0001f, "Submitted heavy draw matrix differs from exact clone hierarchy");
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
