using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEngine;
using Object = UnityEngine.Object;

public static class LargeGizmoAcceptance
{
    public static void Run(Camera template, string output)
    {
        Camera camera = Object.Instantiate(template);
        camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
        camera.orthographic = true; camera.orthographicSize = 10;
        camera.pixelRect = new Rect(0, 0, 1280, 720);
        camera.cullingMask = 1 << 2;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.035f, .045f, .06f, 1);
        try
        {
            using (var gizmo = new TransformGizmoView(true) { Family = GizmoFamily.Points })
            {
                // A small before-fix repro avoids allocating thousands of renderers just to fail.
                var repro = new Vector3[600];
                for (int i = 0; i < repro.Length; ++i) repro[i] = new Vector3(i % 30 - 15, i / 30 - 10, 0);
                gizmo.EditorMouse = camera.WorldToScreenPoint(Vector3.zero);
                Show(repro, 599, 598);
                Require(Handles().Count <= 512, "Unbounded anchor renderer allocation: " + Handles().Count);
                Capture(camera, Field<GameObject>(gizmo, "root"), Path.Combine(output, "large-gizmo-pool.png"));
                var points = new Vector3[17125];
                for (int i = 0; i < points.Length; ++i) points[i] = new Vector3(100 + i % 100, 100 + i / 100, 0);
                points[16000] = new Vector3(-4, 0, 0);
                points[17000] = new Vector3(4, 0, 0);
                Vector3[] original = (Vector3[])points.Clone();
                gizmo.EditorMouse = camera.WorldToScreenPoint(points[16000]);
                Show(points, 17000, 16000);
                Require(Handles().Count <= 512, "Large set grew renderer pool");
                Require(gizmo.HitTestAnchor(camera, points, gizmo.EditorMouse.Value) == 16000, "Tail point lost original hit index");
                List<int> indices = Field<List<int>>(gizmo, "anchorHandleIndices");
                Require(indices.Contains(16000) && indices.Contains(17000), "Selected/pinned tail point omitted");
                Require(Handles()[indices.IndexOf(16000)].sortingOrder == short.MaxValue &&
                    Handles()[indices.IndexOf(17000)].sortingOrder == short.MaxValue - 1, "Pin/selection feedback used renderer index");
                gizmo.EditorMouse = camera.WorldToScreenPoint(points[17000]);
                Show(points, -1, -1);
                Require(gizmo.HitTestAnchor(camera, points, gizmo.EditorMouse.Value) == 17000, "Cursor remapping lost other tail point");
                Require(original.SequenceEqual(points), "Rendering changed source coordinates/order");

                // More than one pool of coincident native clasps must not hide a tiny helper.
                var coincident = new Vector3[600];
                gizmo.EditorMouse = camera.WorldToScreenPoint(Vector3.zero);
                Show(coincident, -1, -1, geometryStart: 0, geometryCount: 1);
                Require(gizmo.HitTestAnchor(camera, coincident, gizmo.EditorMouse.Value) == 0,
                    "Coincident geometry helper lost smaller-target priority");
                Show(coincident, -1, -1);
                Require(gizmo.HitTestAnchor(camera, coincident, gizmo.EditorMouse.Value) == 599,
                    "Coincident equal points lost reverse source-order priority");
                var depthTie = Enumerable.Repeat(new Vector3(0, 0, 20), 600).ToArray();
                depthTie[0] = new Vector3(.006f, 0, 0);
                Show(depthTie, -1, -1);
                Require(gizmo.HitTestAnchor(camera, depthTie, gizmo.EditorMouse.Value) == 0,
                    "Full-set near-depth hit winner was replaced by closest-screen shortlist");

                var small = new[] { new Vector3(-2, 0, 0), new Vector3(2, 0, 0) };
                gizmo.EditorMouse = camera.WorldToScreenPoint(small[1]);
                Show(small, -1, -1);
                Require(gizmo.HitTestAnchor(camera, small, gizmo.EditorMouse.Value) == 1 &&
                    Handles().Count(line => line.gameObject.activeSelf) == 2, "Large-to-small leaked handles/hit indices");
                gizmo.NativePointsOnly = true;
                Show(small, -1, -1, nativeStart: 1);
                Require(!Handles()[0].gameObject.activeSelf && Handles()[1].gameObject.activeSelf, "Small-set slot identity changed");
                gizmo.NativePointsOnly = false;
                Show(Array.Empty<Vector3>(), -1, -1);
                Require(Handles().All(line => !line.gameObject.activeSelf) &&
                    gizmo.HitTestAnchor(camera, small, gizmo.EditorMouse.Value) == -1, "Empty set left stale selectable handles");
                Show(small, -1, -1, mode: GizmoMode.Plane);
                Require(Handles().All(line => !line.gameObject.activeSelf), "Plane mode left active anchors");
                Show(new[] { new Vector3(0, 0, -20) }, -1, -1);
                Require(Handles().All(line => !line.gameObject.activeSelf), "Behind-camera point consumed visible slot");
                gizmo.Hide();
                Require(gizmo.HitTestAnchor(camera, small, gizmo.EditorMouse.Value) == -1, "Hidden gizmo still selectable");
                List<LineRenderer> Handles() => Field<List<LineRenderer>>(gizmo, "anchorHandles");
                void Show(Vector3[] values, int selected, int pinned, int nativeStart = 0,
                    int geometryStart = -1, int geometryCount = 0, GizmoMode mode = GizmoMode.Move)
                    => gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, mode,
                        GizmoHandleKind.None, GizmoAxis.None, values, selected, pinned, nativeStart, true, 1,
                        false, Vector3.zero, false, GizmoAxis.None, false, default, default, 0,
                        pointVisibility: _ => true, geometryAnchorStart: geometryStart, geometryPoints: geometryCount);
            }
            using (var world = new TransformGizmoView() { Family = GizmoFamily.Points })
            {
                var points = Enumerable.Range(0, 600).Select(i => new Vector3(i < 300 ? -1 : 1, 0, 0)).ToArray();
                world.EditorMouse = camera.WorldToScreenPoint(Vector3.right);
                ShowWorld(point => point.x > 0);
                var handles = Field<List<LineRenderer>>(world, "anchorHandles");
                Require(handles.Count <= 512 && handles.Count(line => line.gameObject.activeSelf) == 300 &&
                    Field<List<int>>(world, "anchorHandleIndices").All(index => index >= 300),
                    "Occluded world candidates consumed renderer slots");
                Require(world.HitTestAnchor(camera, points, world.EditorMouse.Value) == 599, "World source-order hit changed");
                ShowWorld(_ => false);
                Require(handles.All(line => !line.gameObject.activeSelf) &&
                    world.HitTestAnchor(camera, points, world.EditorMouse.Value) == -1, "World occlusion retained stale hits");
                void ShowWorld(Func<Vector3, bool> visibility) => world.Show(camera, Vector3.zero, Quaternion.identity,
                    false, GizmoMode.Move, GizmoHandleKind.None, GizmoAxis.None, points, -1, -1, 0, true, 1,
                    false, Vector3.zero, false, GizmoAxis.None, false, default, default, 0, pointVisibility: visibility);
            }
            File.WriteAllText(Path.Combine(output, "large-gizmo-check.txt"),
                "PASS: 600 regression/17125 points; <=512 anchor renderer slots; original tail hit/pin/selection; cursor remap; coincident smaller-target/source-order and near-depth winner; source unchanged; large-small-empty/plane/behind/hidden and pooled world-occlusion cleanup. Projection/visibility still scan source candidates; not Valheim FPS proof.");
        }
        finally { Object.DestroyImmediate(camera.gameObject); }
    }
    private static void Capture(Camera camera, GameObject root, string path)
    {
        var texture = new RenderTexture(1280, 720, 24);
        var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
        Renderer[] owned = root.GetComponentsInChildren<Renderer>(true);
        int[] layers = owned.Select(renderer => renderer.gameObject.layer).ToArray();
        Renderer[] all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        int layer = 31;
        while (layer >= 8 && all.Any(renderer => renderer.gameObject.layer == layer)) --layer;
        Require(layer >= 8, "No isolated capture layer available");
        int mask = camera.cullingMask;
        try
        {
            foreach (Renderer renderer in owned) renderer.gameObject.layer = layer;
            camera.cullingMask = 1 << layer;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; RenderTexture.active = active;
            camera.cullingMask = mask;
            for (int index = 0; index < owned.Length; ++index) owned[index].gameObject.layer = layers[index];
            Object.DestroyImmediate(pixels); Object.DestroyImmediate(texture);
        }
    }
    private static T Field<T>(object instance, string name) => (T)instance.GetType()
        .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
