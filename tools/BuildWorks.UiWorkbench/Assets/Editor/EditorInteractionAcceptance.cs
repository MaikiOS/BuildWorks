using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Object = UnityEngine.Object;

// Receives already loaded, read-only installed assets from ActualDividerAcceptance.
// Native prefab components are never instantiated or executed.
public static class EditorInteractionAcceptance
{
    public static void Run(string outputDirectory, GameObject divider, GameObject wall)
    {
        var evidence = new List<string> { "0.19.11 exact native editor interaction", DateTime.UtcNow.ToString("O") };
        GameObject templateObject = new GameObject("NativeInteractionTemplate", typeof(Camera));
        Camera template = templateObject.GetComponent<Camera>();
        template.enabled = false;
        var captureTarget = new RenderTexture(1024,1024,24);
        int dividerChildren = divider.transform.childCount, wallChildren = wall.transform.childCount;
        try
        {
            foreach (GameObject source in new[] { divider, wall })
            {
                int native = 0;
                foreach (Transform child in source.transform)
                {
                    evidence.Add(source.name + "/" + child.name + ": tag=" + child.tag +
                        "; local=" + child.localPosition);
                    if (child.tag == "snappoint" && !child.name.StartsWith("BuildWorks_MidSnap_")) ++native;
                }
                Require(native >= 2, "Exact native snap metadata missing: " + source.name);
            }
            var doc = new BlueprintEditorDocument(null, "Actual native group", "Test", new[] {
                Part("left", "ashwood_decowall_divider", new Vector3(-0.5445061f,0.004989624f,-0.1852417f)),
                Part("wall", "woodwall", new Vector3(-0.044506073f,0.004989624f,0.014755249f)),
                Part("right", "ashwood_decowall_divider", new Vector3(0.455493927f,0.004989624f,-0.1852417f)) },
                new[] { new BlueprintEditorGroup("group", "Группа 1") });
            doc.SelectOnly("group");
            using (var scene = new BlueprintEditorScene(template,
                name => name == "woodwall" ? wall : name == "ashwood_decowall_divider" ? divider : null))
            {
                Require(scene.TrySync(doc, out string error), "Actual native Scene: " + error);
                var ids = new[] { "left", "wall", "right" };
                Require(scene.TryGetContentBounds(out Bounds bounds), "Actual native bounds missing");
                // Keep one target bound for both projection and rendering; swapping
                // between display and RT changes Camera.pixelRect and pixel metrics.
                scene.Camera.targetTexture = captureTarget;
                scene.SetViewport(new Rect(0,0,1024,1024));
                scene.Camera.orthographic = true;
                scene.Camera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.y) * 1.7f;
                scene.SetCameraPose(bounds.center - Vector3.forward * 6f, Quaternion.identity);
                Require(scene.TryGetGizmoAnchors(ids, out Vector3[] anchors, out int nativeStart) &&
                    anchors.Length > nativeStart, "Exact group produced no native/midpoint anchors");
                object cache = Field<object>(scene, "cachedGizmoAnchors");
                var visuals = Field<IDictionary>(scene, "visuals");
                foreach (string id in ids)
                    evidence.Add(id + " snapLocal=" + Field<List<Vector3>>(visuals[id], "SnapLocal").Count);
                evidence.Add("Group nativeStart=" + nativeStart + "; exterior=" + (anchors.Length - nativeStart));
                Vector3 pivot = bounds.center;
                Color32[] clean = Render(scene.Camera);
                Show(scene, pivot, anchors, nativeStart);
                TransformGizmoView gizmo = Field<TransformGizmoView>(scene, "gizmo");
                var handles = Field<List<LineRenderer>>(gizmo, "anchorHandles");
                int layer = Field<GameObject>(gizmo, "root").layer;
                for (int index = nativeStart; index < anchors.Length; ++index)
                    Require(handles[index].gameObject.activeInHierarchy && handles[index].gameObject.layer == layer &&
                        (scene.Camera.cullingMask & (1 << handles[index].gameObject.layer)) != 0,
                        "Dynamic native marker is outside editor camera: " + index);
                int nativeIndex = Enumerable.Range(nativeStart, anchors.Length - nativeStart)
                    .Where(index => InFrame(scene.Camera.WorldToScreenPoint(anchors[index])))
                    .OrderByDescending(index => (anchors[index] - pivot).sqrMagnitude).First();
                Vector2 nativePixel = scene.Camera.WorldToScreenPoint(anchors[nativeIndex]);
                evidence.Add("Native sample=" + nativeIndex + "; pixel=" + nativePixel +
                    "; pixelRect=" + scene.Camera.pixelRect);
                Color32[] gold = Render(scene.Camera, Path.Combine(outputDirectory, "actual-native-gold.png"));
                int goldPixels = ChangedPixels(clean, gold, nativePixel, pixel =>
                    pixel.r > 150 && pixel.g > 85 && pixel.b < 190 && pixel.r > pixel.g + 15);
                Require(goldPixels >= 8, "Native marker has metadata but no gold pixels: " + goldPixels);
                var pinned = new Vector3[anchors.Length + 1];
                Array.Copy(anchors, pinned, anchors.Length);
                pinned[pinned.Length - 1] = anchors[nativeIndex];
                Show(scene, pivot, pinned, nativeStart, pinned.Length - 1);
                Require(!handles[nativeIndex].gameObject.activeSelf && handles[pinned.Length - 1].gameObject.activeInHierarchy,
                    "Coincident native glyph overrides appended pin");
                Color32[] purple = Render(scene.Camera, Path.Combine(outputDirectory, "actual-native-pin-purple.png"));
                int purplePixels = ChangedPixels(clean, purple, nativePixel, pixel =>
                    pixel.r > 120 && pixel.b > 150 && pixel.b > pixel.g + 35 && pixel.r > pixel.g + 35);
                Require(purplePixels >= 8, "Appended pin has metadata but no purple pixels: " + purplePixels);
                evidence.Add("PASS actual rendered gold=" + goldPixels + "; purple=" + purplePixels +
                    "; editorLayer=" + layer + "; cameraMask=" + scene.Camera.cullingMask);

                scene.ShowSnapCandidates(new[] { pivot + Vector3.up }, new[] { true });
                foreach (LineRenderer line in Field<List<LineRenderer>>(gizmo, "snapCandidateHandles"))
                    Require(line.gameObject.layer == layer, "Dynamic snap candidate lost editor layer");
                scene.ShowContourGuide(new[] { pivot, pivot + Vector3.right }, false);
                foreach (LineRenderer line in Field<List<LineRenderer>>(gizmo, "layoutHandles"))
                    Require(line.gameObject.layer == layer, "Dynamic contour marker lost editor layer");

                Show(scene, pivot, anchors, nativeStart);
                TestIndependentSizes(scene, gizmo, pivot, anchors, nativeStart);
                scene.SetTemporarySelectionHighlight(true, doc);
                foreach (string id in ids)
                    foreach (Renderer renderer in Field<Renderer[]>(visuals[id], "Renderers"))
                        Require(renderer.HasPropertyBlock(), "Shift selection tint omitted descendant: " + id);
                scene.SetTemporarySelectionHighlight(false, doc);
                foreach (string id in ids)
                    foreach (Renderer renderer in Field<Renderer[]>(visuals[id], "Renderers"))
                        Require(!renderer.HasPropertyBlock(), "Shift release retained selection tint: " + id);
                Require(ReferenceEquals(cache, Field<object>(scene, "cachedGizmoAnchors")),
                    "Camera/settings/Shift invalidated geometry-only anchor cache");
                anchors[0] = Vector3.one * 999f;
                scene.TryGetGizmoAnchors(ids, out Vector3[] original, out _);
                Require(original[0] != anchors[0], "Returned anchor array corrupts cached data");
                scene.PreviewTransform(doc, ids, Vector3.up, Quaternion.identity, pivot);
                scene.TryGetGizmoAnchors(ids, out Vector3[] moved, out _);
                Require(Vector3.Distance(moved[0], original[0] + Vector3.up) < 0.001f,
                    "Actual group preview kept stale cached anchors");
                Require(scene.TrySync(doc, out error), "Restore native group: " + error);
                scene.TryGetGizmoAnchors(ids, out Vector3[] restored, out _);
                Require(Vector3.Distance(restored[0], original[0]) < 0.001f, "TrySync retained preview anchor positions");

                scene.ShowContourPreview(new[] { Part("array", "woodwall", pivot + Vector3.right * 3f) });
                scene.ShowDuplicatePreview(new[] { Part("duplicate", "woodwall", pivot + Vector3.left * 3f) });
                var duplicates = Field<IList>(scene, "duplicatePreviews");
                GameObject duplicateRoot = Field<GameObject>(duplicates[0], "Root");
                Material duplicateMaterial = Field<Renderer[]>(duplicates[0], "Renderers")[0].sharedMaterial;
                scene.ShowDuplicatePreview(new[] { Part("new-id-each-frame", "woodwall", pivot + Vector3.left * 4f) });
                Require(duplicateRoot.GetInstanceID() == Field<GameObject>(duplicates[0], "Root").GetInstanceID() &&
                    duplicateMaterial.GetInstanceID() == Field<Renderer[]>(duplicates[0], "Renderers")[0].sharedMaterial.GetInstanceID(),
                    "Same prefab preview reallocates its visual or material each frame");
                Quaternion previewRotation = Quaternion.Euler(0,45f,0);
                scene.ShowDuplicatePreview(new[] { new BlueprintEditorPart("posed", "woodwall", "wall",
                    new Point3(2,3,4), new Rotation3(previewRotation.x,previewRotation.y,previewRotation.z,previewRotation.w),
                    scale: new Point3(2,2,2)) });
                Require(duplicateRoot.GetInstanceID() == Field<GameObject>(duplicates[0], "Root").GetInstanceID() &&
                    Vector3.Distance(duplicateRoot.transform.position, new Vector3(2,3,4)) < 0.0001f &&
                    Quaternion.Angle(duplicateRoot.transform.rotation, previewRotation) < 0.001f &&
                    Vector3.Distance(duplicateRoot.transform.localScale, wall.transform.lossyScale * 2f) < 0.0001f,
                    "Reused preview kept stale pose or scale");
                scene.ShowDuplicatePreview(new[] { Part("replacement", "ashwood_decowall_divider", pivot),
                    Part("tail", "woodwall", pivot + Vector3.up) });
                GameObject replacementRoot = Field<GameObject>(duplicates[0], "Root");
                GameObject tailRoot = Field<GameObject>(duplicates[1], "Root");
                Require(!duplicateRoot.activeSelf, "Prefab replacement leaves previous visual active");
                scene.ShowDuplicatePreview(new[] { Part("shortened", "ashwood_decowall_divider", pivot) });
                Require(duplicates.Count == 1 && !tailRoot.activeSelf &&
                    replacementRoot.GetInstanceID() == Field<GameObject>(duplicates[0], "Root").GetInstanceID(),
                    "Shrinking preview failed to remove only its trailing visual");
                bool missingRejected = false;
                try { scene.ShowDuplicatePreview(new[] { Part("missing", "missing-prefab", pivot) }); }
                catch (InvalidOperationException) { missingRejected = true; }
                Require(missingRejected && duplicates.Count == 0 && !replacementRoot.activeSelf,
                    "Missing prefab failure leaves the old preview active");
                scene.ClearDuplicatePreview();
                Require(!duplicateRoot.activeSelf && Field<IList>(scene, "contourPreviews").Count == 1,
                    "Duplicate preview teardown changed the independent array preview");
                scene.ClearContourPreview();
                scene.ShowBlueprintPlacementPreview(new[] { Part("catalog", "woodwall", pivot) },
                    new Vector3(0,2f,0), out Vector3 offset);
                duplicates = Field<IList>(scene, "duplicatePreviews");
                Renderer[] previewRenderers = Field<Renderer[]>(duplicates[0], "Renderers");
                float bottom = previewRenderers.Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                    .Min(renderer => renderer.bounds.min.y);
                Require(Mathf.Abs(bottom - 2f) < 0.001f, "Compound catalog preview is not grounded: " + bottom);
                float nativeBottom = Field<Renderer[]>(visuals["wall"], "Renderers")
                    .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy)
                    .Min(renderer => renderer.bounds.min.y);
                var snapPreview = new[] {
                    Part("snap-wall", "woodwall", new Vector3(0.155493927f,0.004989624f,0.014755249f)),
                    Part("snap-divider", "ashwood_decowall_divider", new Vector3(-0.3445061f,0.004989624f,-0.1852417f)) };
                scene.ShowBlueprintPlacementPreview(snapPreview, new Vector3(0,nativeBottom,0), out offset, snap: true);
                Require(scene.PlacementSnapTarget.HasValue, "Compound preview omitted native snap target");
                bool aligned = false;
                duplicates = Field<IList>(scene, "duplicatePreviews");
                for (int index = 0; index < duplicates.Count; ++index)
                {
                    object visual = duplicates[index];
                    Transform transform = Field<GameObject>(visual, "Root").transform;
                    Point3 position = snapPreview[index].Position;
                    Require(Vector3.Distance(transform.position,
                        new Vector3((float)position.X,(float)position.Y,(float)position.Z) + offset) < 0.0001f,
                        "Compound source received a different offset than final commit");
                    foreach (Vector3 point in Field<List<Vector3>>(visual, "SnapLocal"))
                        aligned |= Vector3.Distance(transform.TransformPoint(point), scene.PlacementSnapTarget.Value) < 0.0001f;
                }
                Require(aligned, "Compound preview target and transformed native source do not coincide");
                scene.ClearBlueprintPlacementPreview();
                Require(!scene.PlacementSnapTarget.HasValue, "Compound clear retained snap target");
                scene.ShowPlacementTarget(pivot, true);
                Require(handles[0].gameObject.activeInHierarchy && handles[0].startColor.r > 0.9f,
                    "Placement snap target marker missing");
                scene.HidePlacementTarget();
                Require(!Field<GameObject>(gizmo, "root").activeSelf, "Placement target survives hide");
                evidence.Add("PASS independent five sizes, square scale hit priority, Shift restore, native cache and preview lifecycle");
            }
            Require(divider.transform.childCount == dividerChildren && wall.transform.childCount == wallChildren,
                "Editor created midpoint GameObjects on installed prefabs");
            evidence.Add("PASS installed prefab hierarchy unchanged");
            Debug.Log("BUILDWORKS_NATIVE_INTERACTION_OK");
        }
        catch (Exception exception)
        {
            evidence.Add("FAIL " + exception);
            throw;
        }
        finally
        {
            templateObject.SetActive(false);
            Object.Destroy(templateObject);
            captureTarget.Release();
            Object.Destroy(captureTarget);
            File.WriteAllLines(Path.Combine(outputDirectory, "native-interaction-check.txt"), evidence);
        }
    }

    private static void TestIndependentSizes(BlueprintEditorScene scene, TransformGizmoView gizmo,
        Vector3 pivot, Vector3[] anchors, int nativeStart)
    {
        LineRenderer move = Field<LineRenderer[]>(gizmo, "moveLines")[0];
        LineRenderer rotation = Field<LineRenderer[]>(gizmo, "rotationRings")[2];
        LineRenderer array = Field<LineRenderer[]>(gizmo, "layoutAxisLines")[0];
        LineRenderer square = Field<LineRenderer>(gizmo, "scaleHandle");
        LineRenderer point = Field<List<LineRenderer>>(gizmo, "anchorHandles")[nativeStart];
        scene.SetViewportSettings(1,1,1,1,1,true);
        Show(scene, pivot, anchors, nativeStart);
        float moveLength = Vector3.Distance(move.GetPosition(1), pivot);
        float rotationRadius = Vector3.Distance(rotation.GetPosition(0), pivot);
        float arrayLength = Vector3.Distance(array.GetPosition(1), array.GetPosition(0));
        float squareWidth = Vector3.Distance(square.GetPosition(0), square.GetPosition(1));
        float pointRadius = Vector3.Distance(point.GetPosition(0), anchors[nativeStart]);
        Vector3 squareCenter = (square.GetPosition(0) + square.GetPosition(2)) * 0.5f;
        Vector2 pixel = scene.Camera.WorldToScreenPoint(squareCenter);
        Require(scene.HitTestScale(pixel) && scene.HitTestGizmo(BlueprintEditorTool.Array, pivot,
            Quaternion.identity, false, pixel, out _) == GizmoHandleKind.Scale,
            "Uniform scale square loses hit priority to array/planes");
        Require(Mathf.Abs(Vector3.Dot((square.GetPosition(1)-square.GetPosition(0)).normalized,
            scene.Camera.transform.right)) > 0.999f, "Scale glyph is still an anchor diamond");
        scene.SetViewportSettings(2,0.5f,1.5f,2,2.5f,false);
        Show(scene, pivot, anchors, nativeStart);
        Require(Near(Vector3.Distance(move.GetPosition(1), pivot), moveLength * 2f) &&
            Near(Vector3.Distance(rotation.GetPosition(0), pivot), rotationRadius * 0.5f) &&
            Near(Vector3.Distance(array.GetPosition(1), array.GetPosition(0)), arrayLength * 1.5f) &&
            Near(Vector3.Distance(point.GetPosition(0), anchors[nativeStart]), pointRadius * 2f) &&
            Near(Vector3.Distance(square.GetPosition(0), square.GetPosition(1)), squareWidth * 2.5f),
            "Independent editor size settings cross-coupled");
        Require(!Field<Transform>(scene, "minorGrid").gameObject.activeSelf &&
            !Field<Transform>(scene, "majorGrid").gameObject.activeSelf, "Grid toggle leaves a grid visible");
        squareCenter = (square.GetPosition(0) + square.GetPosition(2)) * 0.5f;
        pixel = scene.Camera.WorldToScreenPoint(squareCenter);
        Require(scene.HitTestScale(pixel + Vector2.right * 24f), "Scaled square hit bounds do not match its glyph");
        scene.SetViewportSettings(1,1,1,1,1,true);
        Show(scene, pivot, anchors, nativeStart);
        Require(Near(Vector3.Distance(move.GetPosition(1), pivot), moveLength), "Settings reset did not restore baseline");
        using (var world = new TransformGizmoView())
        {
            world.SetEditorSizes(2,0.5f,1.5f,2,2.5f);
            Require(Field<float>(world, "moveSize") == 1f && Field<float>(world, "pointSize") == 1f,
                "Editor settings modified legacy world F9 sizes");
        }
    }

    private static void Show(BlueprintEditorScene scene, Vector3 pivot, Vector3[] anchors, int nativeStart, int pin = -1) =>
        scene.ShowGizmo(BlueprintEditorTool.Array, pivot, Quaternion.identity, false,
            GizmoHandleKind.None, GizmoAxis.None, anchors, -1, nativeStart, true, false,
            Vector3.zero, false, pinnedAnchor: pin);

    private static BlueprintEditorPart Part(string id, string prefab, Vector3 point) =>
        new BlueprintEditorPart(id, prefab, prefab, new Point3(point.x,point.y,point.z),
            new Rotation3(0,0,0,1), "group");
    private static bool Near(float value, float expected) => Mathf.Abs(value - expected) < 0.0001f;
    private static bool InFrame(Vector3 point) => point.z > 0 && point.x > 40 && point.x < 984 && point.y > 40 && point.y < 984;
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private static int ChangedPixels(Color32[] before, Color32[] after, Vector2 center, Func<Color32,bool> color)
    {
        int count = 0;
        for (int y = Mathf.Max(0,(int)center.y-40); y <= Mathf.Min(1023,(int)center.y+40); ++y)
            for (int x = Mathf.Max(0,(int)center.x-40); x <= Mathf.Min(1023,(int)center.x+40); ++x)
            {
                Color32 a = after[y*1024+x], b = before[y*1024+x];
                if (color(a) && Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b) > 30) ++count;
            }
        return count;
    }

    private static Color32[] Render(Camera camera, string path = null)
    {
        var texture = new Texture2D(1024,1024,TextureFormat.RGB24,false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.Render();
            RenderTexture.active = camera.targetTexture;
            texture.ReadPixels(new Rect(0,0,1024,1024),0,0);
            texture.Apply();
            if (path != null) File.WriteAllBytes(path, texture.EncodeToPNG());
            return texture.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            Object.Destroy(texture);
        }
    }
}
