using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEngine;
using Object = UnityEngine.Object;

public static class GizmoQueryAcceptance
{
    public static void Run(Camera template, string output)
    {
        Camera camera = Object.Instantiate(template);
        camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
        camera.orthographic = true; camera.orthographicSize = 10;
        camera.pixelRect = new Rect(0, 0, 1280, 720);
        try
        {
            using (var gizmo = new TransformGizmoView(true) { Family = GizmoFamily.Points, ModelSpaceSizing = true })
            {
                var points = Enumerable.Range(0, 17125).Select(i => new Vector3((i % 200) * .08f - 8, (i / 200) * .08f - 3, 0)).ToArray();
                int visibilityCalls = 0;
                bool clear = true;
                Func<Vector3, bool> visibility = _ => { ++visibilityCalls; return clear; };
                gizmo.EditorMouse = camera.WorldToScreenPoint(Vector3.zero);
                Show(points);
                Require(visibilityCalls <= 512, "Editor queried visibility for unrendered anchors: " + visibilityCalls);
                int firstProjections = Field<int>(gizmo, "anchorProjectionCalls");
                Require(firstProjections <= points.Length * 2 && firstProjections >= points.Length,
                    "Unexpected first projection work");
                clear = false; visibilityCalls = 0;
                Show(points);
                Require(Field<int>(gizmo, "anchorProjectionCalls") == 0, "Static anchors were projected again");
                Require(visibilityCalls == 512 && Indices().All(i => Field<List<bool>>(gizmo, "anchorOccluded")[i]),
                    "Static camera cached stale editor visibility");
                clear = true; visibilityCalls = 0;
                gizmo.EditorMouse = camera.WorldToScreenPoint(points[16000]);
                Show((Vector3[])points.Clone());
                Require(Field<int>(gizmo, "anchorProjectionCalls") == 0 && visibilityCalls == 512,
                    "Cloned identical array missed cache or visibility refresh");
                Require(gizmo.HitTestAnchor(camera, points, gizmo.EditorMouse.Value) == 16000,
                    "Cursor movement lost original source ID");
                points[16000] += new Vector3(.2f, 0, 0);
                gizmo.EditorMouse = camera.WorldToScreenPoint(points[16000]);
                Show(points);
                Require(Field<int>(gizmo, "anchorProjectionCalls") == 2,
                    "In-place point mutation did not update only center/edge projection");
                camera.transform.position += new Vector3(.5f, 0, 0);
                Show(points);
                Require(Field<int>(gizmo, "anchorProjectionCalls") >= points.Length, "Camera view cache was stale");
                camera.orthographicSize = 9;
                Show(points);
                Require(Field<int>(gizmo, "anchorProjectionCalls") >= points.Length, "Projection matrix cache was stale");
                camera.pixelRect = new Rect(40, 20, 1000, 600);
                Show(points);
                Require(Field<int>(gizmo, "anchorProjectionCalls") >= points.Length, "Pixel rectangle cache was stale");
                gizmo.ModelSize = 2;
                Show(points);
                Require(Field<int>(gizmo, "anchorProjectionCalls") == points.Length, "Model size did not update edge projections only");
                camera.pixelRect = new Rect(0, 0, 1280, 720);
                camera.orthographicSize = 10;
                camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
                gizmo.ModelSize = 1;
                Show(Array.Empty<Vector3>());
                Require(Indices().Count == 0 && gizmo.HitTestAnchor(camera, points, gizmo.EditorMouse.Value) == -1,
                    "Empty set retained cached hits");
                void Show(Vector3[] values) => gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                    GizmoHandleKind.None, GizmoAxis.None, values, -1, -1, 0, true, 1, false, Vector3.zero, false,
                    GizmoAxis.None, false, default, default, 0, pointVisibility: visibility);
                List<int> Indices() => Field<List<int>>(gizmo, "anchorHandleIndices");
            }
            TestSelectionParity(camera);
            TestWorldVisibility(camera);
            File.WriteAllText(Path.Combine(output, "gizmo-query-check.txt"),
                "PASS:17125 anchors; <=512 fresh editor visibility checks vs17125 before; zero repeated center/edge projections for identical arrays; in-place/camera/projection/pixelRect/model-size invalidation; cursor source IDs; empty cleanup; exact legacy shortlist/hit parity for random/dense world+editor, geometry/occlusion, shifted input and fresh dynamic visibility. World17125 coincident:512 checks when clear,17125 once each when all occluded; no stale collider/point/camera hits. Full source scan/sort and worst-case world visibility remain; not Valheim/FPS proof.");
        }
        finally { Object.DestroyImmediate(camera.gameObject); }
    }

    private static void TestWorldVisibility(Camera camera)
    {
        using (var gizmo = new TransformGizmoView() { Family = GizmoFamily.Points })
        {
            var points = new Vector3[17125];
            bool clear = true;
            int queries = 0;
            gizmo.EditorMouse = camera.WorldToScreenPoint(Vector3.zero);
            Show();
            Require(queries == 512 && Field<int>(gizmo, "anchorProjectionCalls") == points.Length,
                "Clear world still queried unrendered anchors");
            Require(gizmo.HitTestAnchor(camera, points, gizmo.EditorMouse.Value) == 17124, "World coincident tie changed");
            points[17124] = Vector3.right * 2;
            Require(gizmo.HitTestAnchor(camera, points, gizmo.EditorMouse.Value) == 17123,
                "In-place point change before Show used stale projection");
            points[17124] = Vector3.zero;
            Vector3 position = camera.transform.position;
            camera.transform.position += Vector3.right;
            Require(gizmo.HitTestAnchor(camera, points, gizmo.EditorMouse.Value) == -1,
                "Camera change before Show used stale projection");
            camera.transform.position = position;
            clear = false; queries = 0;
            Show();
            Require(queries == points.Length && Field<List<int>>(gizmo, "anchorHandleIndices").Count == 0 &&
                gizmo.HitTestAnchor(camera, points, gizmo.EditorMouse.Value) == -1,
                "All-occluded world lost fresh visibility or repeated per-source queries");
            clear = true; queries = 0;
            Show();
            Require(queries == 512 && Field<int>(gizmo, "anchorProjectionCalls") == 0 &&
                gizmo.HitTestAnchor(camera, points, gizmo.EditorMouse.Value) == 17124,
                "World obstacle removal was cached across Show calls");
            void Show() => gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                GizmoHandleKind.None, GizmoAxis.None, points, -1, -1, 0, true, 1, false, Vector3.zero, false,
                GizmoAxis.None, false, default, default, 0, pointVisibility: _ => { ++queries; return clear; });
        }
    }

    private static void TestSelectionParity(Camera camera)
    {
        var random = new System.Random(121);
        foreach (bool editor in new[] { false, true })
        using (var gizmo = new TransformGizmoView(editor) { Family = GizmoFamily.Points, ModelSpaceSizing = editor })
        {
            foreach (int count in new[] { 128, 513, 1200 })
            foreach (bool dense in new[] { false, true })
            foreach (bool showAll in new[] { false, true })
            {
                float spread = dense ? .04f : 7f;
                var points = Enumerable.Range(0, count).Select(_ => new Vector3(
                    (float)(random.NextDouble() * spread * 2 - spread),
                    (float)(random.NextDouble() * spread * 2 - spread), (float)(random.NextDouble() * 4))).ToArray();
                points[0] = new Vector3(.006f, 0, 0); points[1] = new Vector3(5, 0, 0);
                Vector2 mouse = camera.WorldToScreenPoint(Vector3.zero);
                foreach (bool clear in new[] { true, false })
                {
                    Func<Vector3, bool> visibility = point => clear && point.x >= 0;
                    gizmo.EditorMouse = mouse;
                    gizmo.Show(camera, Vector3.zero, Quaternion.identity, false, GizmoMode.Move,
                        GizmoHandleKind.None, GizmoAxis.None, points, 0, 1, 100, showAll, 1,
                        false, Vector3.zero, false, GizmoAxis.None, false, default, default, 0,
                        pointVisibility: visibility, geometryAnchorStart: 0, geometryPoints: 5);
                    List<int> actual = Field<List<int>>(gizmo, "anchorHandleIndices");
                    var eligible = new bool[count]; var radii = new float[count];
                    var distances = new float[count]; var priorities = new int[count];
                    for (int i = 0; i < count; ++i)
                    {
                        Vector3 screen = camera.WorldToScreenPoint(points[i]);
                        distances[i] = Vector2.Distance(mouse, screen);
                        bool important = i == 0 || i == 1, geometry = editor && i < 5, native = i >= 100;
                        eligible[i] = screen.z > 0 && (showAll || important || distances[i] <= 120) && (editor || visibility(points[i]));
                        priorities[i] = important ? 0 : 1;
                        Vector3 edge = camera.WorldToScreenPoint(points[i] + camera.transform.right *
                            (geometry ? .035f : native ? .128f : .072f));
                        radii[i] = editor ? Vector2.Distance(screen, edge) + 2 : 16;
                    }
                    int fullWinner = LegacyHit(points, eligible, radii, mouse, editor);
                    if (fullWinner >= 0) priorities[fullWinner] = 0;
                    int[] expected = count <= 512 ? Enumerable.Range(0, count).ToArray() :
                        Enumerable.Range(0, count).Where(i => eligible[i]).OrderBy(i => priorities[i])
                            .ThenBy(i => distances[i]).ThenByDescending(i => i).Take(512).OrderBy(i => i).ToArray();
                    Require(actual.SequenceEqual(expected), "Legacy renderer shortlist/source IDs changed");
                    var visible = new bool[count];
                    foreach (int i in expected) visible[i] = eligible[i];
                    Require(gizmo.HitTestAnchor(camera, points, mouse) == LegacyHit(points, visible, radii, mouse, editor),
                        "Legacy public hit changed");
                    Vector2 shifted = mouse + new Vector2(3, 2);
                    Require(gizmo.HitTestAnchor(camera, points, shifted) == LegacyHit(points, visible, radii, shifted, editor),
                        "Input between Show calls used stale cursor coordinates");
                }
            }
        }

        int LegacyHit(Vector3[] points, bool[] visible, float[] radii, Vector2 mouse, bool editor)
        {
            int best = -1; float bestDistance = float.PositiveInfinity, bestDepth = float.PositiveInfinity;
            for (int i = points.Length - 1; i >= 0; --i)
            {
                if (!visible[i]) continue;
                Vector3 screen = camera.WorldToScreenPoint(points[i]);
                if (screen.z <= 0) continue;
                float distance = Vector2.Distance(mouse, screen);
                if (distance > radii[i]) continue;
                bool coincident = editor && best >= 0 && Vector3.Distance(points[i], points[best]) < .0001f;
                bool smaller = coincident && radii[i] < radii[best];
                if (smaller || !coincident && (distance < bestDistance - .5f || distance <= bestDistance + .5f && screen.z < bestDepth))
                { best = i; bestDistance = distance; bestDepth = screen.z; }
            }
            return best;
        }
    }
    private static T Field<T>(object value, string name) => (T)value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
