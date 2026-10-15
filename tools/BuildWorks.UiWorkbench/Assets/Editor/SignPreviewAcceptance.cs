using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

// Actual preview code with an inert sign host. Native font/privilege/render parity still needs Valheim.
internal static class SignPreviewAcceptance
{
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException("Sign preview: " + reason); }
    private static T Field<T>(object value, string name) => (T)value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(value);
    private static BlueprintSignGlyphs Glyphs(GameObject root) => root.GetComponentInChildren<BlueprintSignGlyphs>(true);
    private static GameObject CurrentSignVisual(BlueprintEditorScene scene) => Field<GameObject>(
        ((IDictionary)Field<object>(scene, "visuals"))["sign"], "Root");

    private static void RenderChecks(GameObject source, GameObject visual, TextMeshProUGUI sample, string text)
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/sign-preview"));
        var cameraObject = new GameObject("SignProofCamera", typeof(Camera));
        var camera = cameraObject.GetComponent<Camera>();
        var target = new RenderTexture(512, 256, 24, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1.3f;
            camera.transform.position = new Vector3(0, .7f, -10);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.targetTexture = target;
            foreach (Transform node in source.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 20;
            foreach (Transform node in visual.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 21;
            visual.transform.localScale = source.transform.localScale;
            sample.text = text; sample.ForceMeshUpdate(true, true);
            Glyphs(visual).Refresh();
            Color32[] Capture(int layer, string name)
            {
                camera.cullingMask = 1 << layer; camera.Render(); RenderTexture.active = target;
                var image = new Texture2D(512, 256, TextureFormat.RGBA32, false);
                try
                {
                    image.ReadPixels(new Rect(0, 0, 512, 256), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(output, name + ".png"), image.EncodeToPNG());
                    return image.GetPixels32();
                }
                finally { Object.Destroy(image); }
            }
            Directory.CreateDirectory(output);
            Color32[] baseline = Capture(20, "fixture-native-tmp");
            Color32[] preview = Capture(21, "fixture-baked-tmp");
            Glyphs(visual).gameObject.SetActive(false);
            Color32[] absent = Capture(21, "fixture-without-text");
            Glyphs(visual).gameObject.SetActive(true);
            long difference = 0, absentDifference = 0;
            for (int index = 0; index < baseline.Length; ++index)
            {
                difference += Math.Abs(baseline[index].r - preview[index].r) + Math.Abs(baseline[index].g - preview[index].g) + Math.Abs(baseline[index].b - preview[index].b);
                absentDifference += Math.Abs(baseline[index].r - absent[index].r) + Math.Abs(baseline[index].g - absent[index].g) + Math.Abs(baseline[index].b - absent[index].b);
            }
            Require(absentDifference > 10000, "render comparison has no readable native text");
            Require(difference < absentDifference * .03 + 1024, "baked text pixels differ from TMP baseline: " + difference + "/" + absentDifference);

            var part = new BlueprintEditorPart("sign", "SignFixture", "Sign", new Point3(0, 0, 0), new Rotation3(0, 0, 0, 1), signText: text);
            using (var scene = new BlueprintEditorScene(camera, _ => source))
            {
                var document = new BlueprintEditorDocument(null, "Signs", "QA", new[] { part });
                Require(scene.TrySync(document, out string warning) && warning == null && Glyphs(Field<GameObject>(scene, "root")),
                    "editor scene omitted text: " + warning);
                GameObject first = Glyphs(CurrentSignVisual(scene)).gameObject;
                document = new BlueprintEditorDocument(null, "Signs", "QA", new[] {
                    new BlueprintEditorPart("sign", "SignFixture", "Sign", new Point3(0,0,0), new Rotation3(0,0,0,1), signText: "Changed") });
                Require(scene.TrySync(document, out warning) && Glyphs(CurrentSignVisual(scene)).gameObject != first,
                    "editor scene retains old text after change");
            }
            source.GetComponent<Sign>().m_textWidget = null;
            using (var scene = new BlueprintEditorScene(camera, _ => source))
            {
                Require(scene.TrySync(new BlueprintEditorDocument(null, "Signs", "QA", new[] { part }), out string warning) &&
                    !string.IsNullOrEmpty(warning) && Field<GameObject>(scene, "root").GetComponentInChildren<MeshRenderer>(true),
                    "missing text widget removed the board or failed to warn");
            }
            source.GetComponent<Sign>().m_textWidget = sample;
            File.WriteAllText(Path.Combine(output, "render-acceptance.txt"), "PASS isolated TMP fixture pixels; diff=" + difference +
                ", missing-text control=" + absentDifference + ". Actual editor scene text/change/missing-widget checks passed. Native Valheim font not tested.\n" + DateTime.UtcNow.ToString("O"));
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null; target.Release();
            Object.Destroy(target); Object.Destroy(cameraObject);
        }
    }

    internal static IEnumerable Run()
    {
        var provider = Splatform.PlatformManager.DistributionPlatform.PrivilegeProvider;
        GameObject source = null, visual = null, thumbnail = null;
        var ownedMeshes = new List<Mesh>();
        var ownedMaterials = new List<Material>();
        try
        {
            Require(Application.isPlaying, "must execute in Play Mode");
            provider.Granted = true;
            source = new GameObject("SignFixture", typeof(Piece), typeof(ZNetView), typeof(Sign));
            source.transform.localScale = new Vector3(2, .5f, 1.3f);
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.transform.SetParent(source.transform, false);
            Object.Destroy(board.GetComponent<Collider>());
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(source.transform, false);
            canvas.transform.localPosition = new Vector3(.1f, 1, -.52f);
            canvas.transform.localRotation = Quaternion.Euler(7, 19, 3);
            canvas.transform.localScale = new Vector3(.013f, .017f, .01f);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var text = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            text.transform.SetParent(canvas.transform, false);
            var sample = text.GetComponent<TextMeshProUGUI>();
            sample.rectTransform.sizeDelta = new Vector2(100, 50);
            sample.rectTransform.pivot = new Vector2(.2f, .1f);
            sample.font = TMP_Settings.defaultFontAsset;
            Require(sample.font, "Workbench TMP font missing");
            sample.fontSize = 12;
            sample.alignment = TextAlignmentOptions.Center;
            sample.richText = true;
            const string wanted = "Дом <b>Ostrix</b>\nQA";
            sample.text = wanted;
            sample.ForceMeshUpdate(true, true);
            Vector3[] expected = sample.mesh.vertices;
            var expectedUv = new List<Vector4>();
            sample.mesh.GetUVs(0, expectedUv);
            Require(expected.Length > 0, "baseline TMP did not generate glyphs");
            sample.text = "DEFAULT";
            sample.ForceMeshUpdate(true, true);
            source.GetComponent<Sign>().m_textWidget = sample;
            source.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_text, "UNCHANGED");
            int writes = source.GetComponent<ZNetView>().GetZDO().Writes;

            visual = PlacementGhostPreviewView.CreateVisualClone(source, "SignVisual", 0);
            var glyphRoot = BlueprintSignPreview.Add(source, visual, wanted);
            Require(glyphRoot && Glyphs(visual), "no renderer-only text");
            MeshFilter glyph = glyphRoot.GetComponentInChildren<MeshFilter>();
            Require(glyph && glyph.sharedMesh.vertexCount == expected.Length, "glyph topology differs from TMP");
            Vector3[] actual = glyph.sharedMesh.vertices;
            var actualUv = new List<Vector4>();
            glyph.sharedMesh.GetUVs(0, actualUv);
            for (int index = 0; index < actual.Length; ++index)
            {
                Vector3 want = source.transform.worldToLocalMatrix.MultiplyPoint3x4(sample.transform.TransformPoint(expected[index]));
                Vector3 got = visual.transform.worldToLocalMatrix.MultiplyPoint3x4(glyph.transform.TransformPoint(actual[index]));
                Require(Vector3.Distance(want, got) < 1e-5f, "nested transform/pivot/text geometry mismatch");
                Require(Mathf.Abs(actualUv[index].w * Mathf.Abs(sample.transform.lossyScale.y) - expectedUv[index].w) < 1e-5f,
                    "unit-scale SDF data differs from native TMP glyphs");
            }
            var sdf = new MaterialPropertyBlock();
            glyph.GetComponent<Renderer>().GetPropertyBlock(sdf);
            Require(Mathf.Abs(sdf.GetFloat("_ScaleY") - Mathf.Abs(glyph.transform.lossyScale.y)) < 1e-5f,
                "small/editor glyph shader omits world scale");
            visual.transform.localScale = new Vector3(.7f, 3, 1.1f);
            Glyphs(visual).Refresh();
            glyph.GetComponent<Renderer>().GetPropertyBlock(sdf);
            Require(Mathf.Abs(sdf.GetFloat("_ScaleY") - Mathf.Abs(glyph.transform.lossyScale.y)) < 1e-5f,
                "glyph scale change retains stale SDF factors");
            Require(sample.text == "DEFAULT" && sample.enabled && source.GetComponent<Sign>().Calls == 0 &&
                source.GetComponent<ZNetView>().GetZDO().Writes == writes, "preview mutated native source or ZDO");
            Require(!visual.GetComponentInChildren<Sign>(true) && !visual.GetComponentInChildren<ZNetView>(true) &&
                !visual.GetComponentInChildren<Canvas>(true) && !visual.GetComponentInChildren<TMP_Text>(true), "preview cloned gameplay or UI components");
            Require(!glyph.GetComponent<Renderer>().receiveShadows, "text casts/receives world shadows");
            ownedMeshes.Add(glyph.sharedMesh);
            ownedMaterials.Add(glyph.GetComponent<Renderer>().sharedMaterial);
            provider.Granted = false;
            Glyphs(visual).Refresh();
            Require(!glyph.GetComponent<Renderer>().enabled && !BlueprintSignPreview.Add(source, visual, wanted), "UGC denial exposed text");
            provider.Granted = true;
            Glyphs(visual).Refresh();
            Require(glyph.GetComponent<Renderer>().enabled, "permission restoration did not restore visibility");
            Require(!BlueprintSignPreview.Add(source, visual, ""), "explicit blank created text");
            GameObject filtered = BlueprintSignPreview.Add(source, visual, "QA_FILTER_WORD");
            GameObject filterControl = BlueprintSignPreview.Bake(source.transform, sample, visual, "***");
            Require(filtered.GetComponentInChildren<MeshFilter>().sharedMesh.vertices.SequenceEqual(
                    filterControl.GetComponentInChildren<MeshFilter>().sharedMesh.vertices),
                "preview did not use the host filter output");
            filtered.SetActive(false); filterControl.SetActive(false);
            Object.Destroy(filtered);
            Object.Destroy(filterControl);
            if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                RenderChecks(source, visual, sample, wanted);

            var plan = new List<PrecisionPlacementSession.TransformSnapshot> {
                new PrecisionPlacementSession.TransformSnapshot(Vector3.zero, Quaternion.identity, Vector3.one, signText: wanted) };
            var pieces = new List<Piece> { source.GetComponent<Piece>() };
            using (var small = new PlacementGhostPreviewView())
            {
                small.Show(source, plan, pieces);
                var first = Field<List<GameObject>>(small, "instances")[0];
                Require(!sample.enabled && Glyphs(first) && Glyphs(first).GetComponentInChildren<Renderer>().enabled,
                    "active native ghost did not show saved text or retained default text");
                plan[0] = new PrecisionPlacementSession.TransformSnapshot(Vector3.zero, Quaternion.identity, Vector3.one, signText: "New");
                small.Show(source, plan, pieces);
                Require(Field<List<GameObject>>(small, "instances")[0] != first, "small preview reused stale text");
                small.Hide();
                Require(sample.enabled, "closing preview failed to restore native widget");
            }
            using (var heavy = new BlueprintBatchedPreview())
            {
                plan.Clear(); pieces.Clear();
                for (int index = 0; index < 513; ++index)
                {
                    plan.Add(new PrecisionPlacementSession.TransformSnapshot(Vector3.right * index, Quaternion.identity, Vector3.one,
                        signText: index % 2 == 0 ? wanted : "Other"));
                    pieces.Add(source.GetComponent<Piece>());
                }
                heavy.Prepare(plan, pieces, 0);
                Require(heavy.Ready && heavy.TemplateCount == 2, "heavy text key lost text or created per-instance templates");
                var templates = Field<Array>(heavy, "parts");
                var drawParts = (IEnumerable)Field<object>(templates.GetValue(0), "Parts");
                bool attachment = false;
                foreach (var draw in drawParts)
                    if (Field<bool>(draw, "SignText")) attachment |= Field<bool>(draw, "Attachment");
                Require(attachment, "active heavy ghost drops text with native body");
                provider.Granted = false;
                heavy.Prepare(plan, pieces, 0);
                Require(heavy.Ready && !Glyphs(Field<GameObject>(Field<Array>(heavy, "parts").GetValue(0), "Visual")),
                    "heavy permission change reused allowed template");
                provider.Granted = true;
                for (int change = 0; change < 12; ++change)
                {
                    for (int index = 0; index < plan.Count; ++index)
                        plan[index] = new PrecisionPlacementSession.TransformSnapshot(Vector3.right * index,
                            Quaternion.identity, Vector3.one, signText: "Change " + change);
                    heavy.Prepare(plan, pieces, 0);
                    Require(heavy.Ready && heavy.TemplateCount == 1, "retired sign templates accumulate during text edits");
                    yield return null;
                }
                var caller = new MaterialPropertyBlock();
                Matrix4x4 rootMatrix = Matrix4x4.TRS(new Vector3(2, 3, 4), Quaternion.Euler(17, 31, 7), new Vector3(2, .7f, 1.3f));
                heavy.Draw(plan, caller, 0, 0, rootMatrix, attachmentsOnly: true);
                object currentTemplate = Field<Array>(heavy, "parts").GetValue(512);
                foreach (object draw in (IEnumerable)Field<object>(currentTemplate, "Parts"))
                {
                    if (!Field<bool>(draw, "SignText")) continue;
                    Matrix4x4 expectedMatrix = rootMatrix * Matrix4x4.TRS(plan[512].Position, plan[512].Rotation,
                        Vector3.Scale(source.transform.localScale, plan[512].Scale)) * Field<Matrix4x4>(draw, "Local");
                    Require(Mathf.Abs(Field<MaterialPropertyBlock>(heavy, "signProperties").GetFloat("_ScaleY") -
                        expectedMatrix.GetColumn(1).magnitude) < 1e-5f && caller.GetFloat("_ScaleY") == 0,
                        "heavy SDF scale differs from the effective world matrix or mutates caller properties");
                }
            }

            thumbnail = new GameObject("ThumbnailVisual");
            var blueprint = new CompositeBlueprintStore.Blueprint { parts = new List<CompositeBlueprintStore.Part> {
                new CompositeBlueprintStore.Part { prefabName = "SignFixture", signText = wanted,
                    position = new CompositeBlueprintStore.VectorData(Vector3.zero),
                    rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity),
                    scale = new CompositeBlueprintStore.VectorData(Vector3.one) } } };
            typeof(BlueprintThumbnailRenderer).GetMethod("CreateVisuals", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { thumbnail, blueprint, new[] { source.GetComponent<Piece>() } });
            Require(Glyphs(thumbnail), "thumbnail visual omitted saved text");
            using (var thumbnails = new BlueprintThumbnailRenderer())
            {
                var path = typeof(BlueprintThumbnailRenderer).GetMethod("ThumbnailPath", BindingFlags.Instance | BindingFlags.NonPublic);
                string cacheId = "sign-preview-qa-" + Guid.NewGuid().ToString("N");
                string allowedPath = (string)path.Invoke(thumbnails, new object[] { cacheId });
                provider.Granted = false;
                thumbnails.GetCached(new CompositeBlueprintStore.Blueprint { id = cacheId, parts = blueprint.parts });
                string deniedPath = (string)path.Invoke(thumbnails, new object[] { cacheId });
                Require(deniedPath != allowedPath, "thumbnail cache ignores UGC permission");
                Directory.CreateDirectory(Path.GetDirectoryName(allowedPath));
                File.WriteAllText(allowedPath, "QA cache fixture"); File.WriteAllText(deniedPath, "QA cache fixture");
                thumbnails.Delete(cacheId);
                Require(!File.Exists(allowedPath) && !File.Exists(deniedPath), "editing under denied UGC leaves stale allowed thumbnail");
                provider.Granted = true;
            }
        }
        finally
        {
            provider.Granted = true;
            if (visual) Object.Destroy(visual);
            if (thumbnail) Object.Destroy(thumbnail);
            if (source) Object.Destroy(source);
        }
        yield return null; yield return null;
        Require(ownedMeshes.All(mesh => !mesh) && ownedMaterials.All(material => !material), "owned glyph assets leaked after cleanup");
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/sign-preview"));
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "mesh-acceptance.txt"),
            "PASS renderer-only TMP geometry, Cyrillic/rich text fixture, nested TRS/pivot, source/ZDO immutability, blank, UGC deny/restore, small/active ghost text invalidation and widget restoration, heavy 513-part text keys/attachment semantics, thumbnail text/cache isolation, owned mesh/material cleanup.\n" +
            "LIMIT: inert host/font fixture; no rendered-pixel, installed-native font, live permission, gameplay, reload or network proof.\n" + DateTime.UtcNow.ToString("O"));
        Debug.Log("PASS SIGN PREVIEW MESH ACCEPTANCE");
    }
}
