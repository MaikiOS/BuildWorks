using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// The only substituted boundary is physical input. Every gesture enters actual Update/LateUpdate;
// UI clicks are resolved by the actual GraphicRaycaster before native pointer handlers run.
internal static class ControllerInputAcceptance
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static T Field<T>(object instance, string name) => (T)instance.GetType().GetField(name, Private).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Private).SetValue(instance, value);
    private static void Require(bool value, string reason) { if (!value) throw new InvalidOperationException("Input frames: " + reason); }
    private static Vector3 V(Point3 value) => new Vector3((float)value.X, (float)value.Y, (float)value.Z);

    private sealed class InputFrames : IBlueprintEditorInput
    {
        private HashSet<KeyCode> keys = new HashSet<KeyCode>(), previousKeys = new HashSet<KeyCode>();
        private int buttons, previousButtons;
        public Vector2 MousePosition { get; private set; }
        public Vector2 MouseScrollDelta { get; set; }
        public float UnscaledDeltaTime => 1f / 30f;
        public float MouseX, MouseY;
        public bool GetKey(KeyCode key) => keys.Contains(key);
        public bool GetKeyDown(KeyCode key) => keys.Contains(key) && !previousKeys.Contains(key);
        public bool GetMouseButton(int button) => (buttons & 1 << button) != 0;
        public bool GetMouseButtonDown(int button) => GetMouseButton(button) && (previousButtons & 1 << button) == 0;
        public bool GetMouseButtonUp(int button) => !GetMouseButton(button) && (previousButtons & 1 << button) != 0;
        public float GetAxis(string axis) => axis == "Mouse X" ? MouseX : axis == "Mouse Y" ? MouseY : 0f;
        public void Next(Vector2 mouse, int heldButtons = 0, params KeyCode[] heldKeys)
        {
            previousKeys = keys; keys = new HashSet<KeyCode>(heldKeys);
            previousButtons = buttons; buttons = heldButtons; MousePosition = mouse;
            MouseScrollDelta = Vector2.zero; MouseX = MouseY = 0f;
        }
    }

    private static void Frame(BlueprintEditorController controller, InputFrames input, Vector2 mouse,
        int buttons = 0, params KeyCode[] keys)
    {
        input.Next(mouse, buttons, keys);
        controller.Update(); controller.LateUpdate(); Canvas.ForceUpdateCanvases();
    }

    internal static IEnumerable Run(Camera template, TMP_Text font, string output)
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "InputFrameSource";
        foreach (Vector3 point in new[] { new Vector3(-.5f,0,-.5f), new Vector3(.5f,0,-.5f),
            new Vector3(.5f,0,.5f), new Vector3(-.5f,0,.5f),
            new Vector3(0,-.5f,0), new Vector3(0,.5f,0) })
        {
            var snap = new GameObject("FrameNative"); snap.tag = "snappoint";
            snap.transform.SetParent(source.transform, false); snap.transform.localPosition = point;
        }
        source.SetActive(false);
        var input = new InputFrames();
        var catalog = new List<BlueprintEditorCatalogItem>
        { new BlueprintEditorCatalogItem("fixture", "Новая деталь", null, "Строительство", "Дерево", "Test", 0) };
        var compound = new CompositeBlueprintStore.Blueprint { id = "compound", name = "Двойная деталь", category = "Декор" };
        compound.groups.Add(new CompositeBlueprintStore.Group { stableId = "nested", name = "Вложенная группа" });
        for (int index = 0; index < 2; ++index)
            compound.parts.Add(new CompositeBlueprintStore.Part { stableId = "child" + index, prefabName = "fixture",
                displayName = "Составная " + index, parentGroupId = "nested",
                position = new CompositeBlueprintStore.VectorData(new Vector3(index == 0 ? -.75f : .75f, 0, 0)),
                rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity),
                scale = new CompositeBlueprintStore.VectorData(Vector3.one * (index == 0 ? .5f : 2f)) });
        catalog.Add(new BlueprintEditorCatalogItem("compound", compound.name, null, compound.category, "Чертёж", "Мои чертежи", 1, compound));
        Camera uiCamera = new GameObject("InputUiCamera", typeof(Camera)).GetComponent<Camera>();
        uiCamera.enabled = false; uiCamera.cullingMask = 1 << 5; uiCamera.clearFlags = CameraClearFlags.Depth;
        uiCamera.nearClipPlane = .1f; uiCamera.farClipPlane = 100f;
        var target = new RenderTexture(1920, 1080, 24);
        uiCamera.targetTexture = target;
        var errors = new List<string>();
        using (var controller = new BlueprintEditorController(new CompositeBlueprintStore(Path.Combine(output, "input-fixture.json")),
            template, font, _ => source, name => name, () => catalog, errors.Add, editorInput: input))
        try
        {
            Require(controller.OpenNew(out string error), error);
            BlueprintEditorDocument doc = controller.Document;
            doc.AddPart(new BlueprintEditorPart("left", "fixture", "Левая", new Point3(-1.5,.5,0), new Rotation3(0,0,0,1)));
            doc.AddPart(new BlueprintEditorPart("right", "fixture", "Правая", new Point3(1.5,.5,0), new Rotation3(0,0,0,1)));
            doc.AddPart(new BlueprintEditorPart("locked", "fixture", "Locked", new Point3(0,.5,3), new Rotation3(0,0,0,1), locked:true));
            doc.AddPart(new BlueprintEditorPart("hidden", "fixture", "Hidden", new Point3(0,.5,-3), new Rotation3(0,0,0,1), visible:false));
            doc.SelectOnly("left");
            var scene = Field<BlueprintEditorScene>(controller, "scene");
            var view = Field<BlueprintEditorView>(controller, "view");
            Require(Field<Action>(view, "DuplicateSelectionRequested").GetInvocationList().Length == 1 &&
                Field<Action>(view, "DeleteSelectionRequested").GetInvocationList().Length == 1 &&
                Field<Action<string>>(view, "PrimaryPartRequested").GetInvocationList().Length == 1 &&
                Field<Action<string>>(view, "PrimaryGroupRequested").GetInvocationList().Length == 1,
                "Outliner context actions are subscribed more than once");
            Require(scene.TrySync(doc, out error), error); view.Bind(doc, true, null, null);
            view.RootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            view.RootCanvas.worldCamera = uiCamera; view.RootCanvas.planeDistance = 1f;
            scene.Camera.targetTexture = target;
            Set(controller, "cameraFocus", new Vector3(0,.5f,0)); Set(controller, "cameraDistance", 8f);
            Set(controller, "cameraYaw", 20f); Set(controller, "cameraPitch", 20f);
            yield return null;
            Frame(controller, input, Vector2.zero); yield return null;
            NativeClick(view, uiCamera, "View");
            Frame(controller, input, Vector2.zero); yield return null;
            Require(Field<TMP_Text>(view, "statusHintText").text.Contains("настройки вида"),
                "Viewport settings retain unavailable tool shortcuts in the footer");
            Frame(controller, input, Vector2.zero, 0, KeyCode.Escape); yield return null;
            NativeClick(view, uiCamera, "OutlinerMenuButton");
            Frame(controller, input, Vector2.zero); yield return null;
            Require(Field<TMP_Text>(view, "statusHintText").text.Contains("меню дерева"),
                "Outliner drawer retains unavailable tool shortcuts in the footer");
            Frame(controller, input, Vector2.zero, 0, KeyCode.Escape); yield return null;
            int catalogRequests = 0;
            controller.CatalogRequested += () => ++catalogRequests;
            Frame(controller, input, Vector2.zero, 0, KeyCode.Tab); yield return null;
            Require(view.HasCatalog && catalogRequests == 1,
                "Tab did not open the Blueprint Editor catalog exactly once");
            view.HideCatalog();
            Frame(controller, input, Vector2.zero); yield return null;
            TestPrimaryGroupGizmo(controller, input, scene, view, doc);
            Vector2 left = scene.Camera.WorldToScreenPoint(new Vector3(-1.5f,.5f,0));
            Vector2 right = scene.Camera.WorldToScreenPoint(new Vector3(1.5f,.5f,0));
            Require(view.ViewportScreenRect().Contains(left) && view.ViewportScreenRect().Contains(right), "Fixture outside actual viewport");

            Frame(controller, input, left, 0, KeyCode.LeftShift, KeyCode.H); yield return null;
            Require(doc.Parts[0].Visible && !doc.Parts[1].Visible && !doc.Parts[2].Visible && !doc.Parts[3].Visible,
                "Shift+H did not isolate the selected part");
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, left); yield return null;

            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.H); yield return null;
            Require(!doc.Parts[0].Visible && doc.Parts[1].Visible && doc.Parts[2].Visible && doc.Parts[3].Visible,
                "Ctrl+H did not show everything except the selected part");
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, left); yield return null;

            Frame(controller, input, left, 0, KeyCode.H); yield return null;
            Require(!doc.Parts[0].Visible && doc.Parts[1].Visible && doc.Parts[2].Visible && !doc.Parts[3].Visible,
                "H did not hide only the selected part");
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftAlt, KeyCode.H); yield return null;
            Require(doc.Parts[0].Visible && doc.Parts[1].Visible && doc.Parts[2].Visible && doc.Parts[3].Visible,
                "Alt+H did not show all parts");
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, left); yield return null;
            Require(doc.Parts[0].Visible && doc.Parts[1].Visible && doc.Parts[2].Visible && !doc.Parts[3].Visible,
                "Visibility shortcut undo did not restore the fixture");

            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.A); yield return null;
            Require(doc.Selection.Count == 2 && doc.IsPartSelected("left") && doc.IsPartSelected("right") &&
                !doc.IsPartSelected("locked") && !doc.IsPartSelected("hidden"), "Ctrl+A includes hidden/locked or misses editable parts");
            Frame(controller, input, left); yield return null;
            Require(Field<GameObject>(Field<TransformGizmoView>(scene, "gizmo"), "root").activeSelf, "Ctrl+A release has no shared gizmo");

            // Actual outliner row callbacks use the same Shift/Ctrl source as controller Update.
            Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
            Frame(controller, input, Vector2.zero, 0, KeyCode.LeftShift); NativeClick(view, uiCamera, "Row_right"); yield return null;
            Frame(controller, input, Vector2.zero, 0, KeyCode.LeftShift); yield return null;
            Require(doc.EditablePartSelectionCount == 2, "Shift range row callback did not select both parts");
            foreach (string id in new[] { "left", "right" }) Require(IsPainted(scene, id), "Shift-held selection is not blue: " + id);
            Save(scene, view, uiCamera, target, Path.Combine(output, "input-shift-selection.png"));
            Frame(controller, input, right); yield return null;
            foreach (string id in new[] { "left", "right" }) Require(!IsPainted(scene, id), "Shift release retained selection paint: " + id);
            Require(Field<GameObject>(Field<TransformGizmoView>(scene, "gizmo"), "root").activeSelf, "Shift release hides shared gizmo");

            Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
            NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Select]);
            yield return null;
            Frame(controller, input, right, 1, KeyCode.LeftShift); yield return null;
            Frame(controller, input, right, 0, KeyCode.LeftShift); yield return null;
            Require(doc.EditablePartSelectionCount == 2, "Shift viewport hit was swallowed by gizmo");
            Frame(controller, input, right); yield return null;

            foreach (object tick in TestTransformContract(controller, input, scene, view, uiCamera, target, output))
                yield return tick;

            Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
            NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
            yield return null;
            Frame(controller, input, left); yield return null;
            Vector3[] anchors = Field<Vector3[]>(controller, "gizmoAnchors");
            int native = Field<int>(controller, "gizmoNativeAnchorStart");
            Vector2 anchorMouse = scene.Camera.WorldToScreenPoint(anchors[native]);
            Frame(controller, input, anchorMouse, 1, KeyCode.LeftShift); yield return null;
            Frame(controller, input, anchorMouse, 0, KeyCode.LeftShift); yield return null;
            Require(Field<Vector3?>(controller, "pinnedAnchorWorld").HasValue && doc.EditablePartSelectionCount == 1,
                "Shift native point should pin current selection, not toggle object");
            Frame(controller, input, anchorMouse); yield return null;

            // Alt is latched on pointer-down. Preview is separate; releasing Alt before mouse-up keeps the copy operation.
            Vector3 original = V(doc.Parts[0].Position);
            Vector2 moveStart = MoveHandle(scene, original, GizmoAxis.X);
            Frame(controller, input, moveStart, 1, KeyCode.LeftAlt); yield return null;
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Move &&
                Field<bool>(controller, "dragDuplicate"), "Alt arrow did not start copy drag: handle=" +
                Field<GizmoHandleKind>(controller, "dragHandle") + "; anchor=" + Field<int>(controller, "dragAnchorPoint") +
                "; selected=" + Field<int>(controller, "selectedAnchorPoint") + "; mouse=" + moveStart);
            Vector2 moveEnd = moveStart + Field<Vector2>(controller, "dragScreenDirection") * 80;
            Frame(controller, input, moveEnd, 1); yield return null;
            Require(doc.Parts.Count == 4 && V(doc.Parts[0].Position) == original, "Alt preview changed source document");
            Require(Vector3.Distance(VisualRoot(scene, "left").transform.position, original) < .0001f &&
                Field<IList>(scene, "duplicatePreviews").Count == 1, "Alt preview moved source visual or omitted copy");
            Save(scene, view, uiCamera, target, Path.Combine(output, "input-alt-preview.png"));
            Frame(controller, input, moveEnd); yield return null;
            Require(doc.Parts.Count == 5 && V(doc.Parts[0].Position) == original &&
                Field<IList>(scene, "duplicatePreviews").Count == 0, "Alt release did not atomically commit only the copy");
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(doc.Parts.Count == 4, "Alt copy needs more than one Undo");
            Frame(controller, input, left); yield return null;
            moveStart = MoveHandle(scene, original, GizmoAxis.X);
            Frame(controller, input, moveStart, 1, KeyCode.LeftAlt); yield return null;
            moveEnd = moveStart + Field<Vector2>(controller, "dragScreenDirection") * 60;
            Frame(controller, input, moveEnd, 1, KeyCode.LeftAlt); yield return null;
            Frame(controller, input, moveEnd, 1, KeyCode.LeftAlt, KeyCode.Escape); yield return null;
            Frame(controller, input, moveEnd); yield return null;
            Require(doc.Parts.Count == 4 && Field<IList>(scene, "duplicatePreviews").Count == 0 &&
                V(doc.Parts[0].Position) == original, "Esc copy cancellation changed source or leaked preview");

            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.A); yield return null;
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.G); yield return null;
            Frame(controller, input, left); yield return null;
            Require(doc.Groups.Count == 1 && doc.Selection.Count == 1, "Ctrl+G did not produce selected group");
            string originalGroup = doc.Groups[0].StableId;
            foreach (object tick in TestOutlinerDragCancel(controller, input, scene, view, uiCamera, originalGroup))
                yield return tick;
            moveStart = MoveHandle(scene, new Vector3(0,.5f,0), GizmoAxis.X);
            Frame(controller, input, moveStart, 1, KeyCode.LeftAlt); yield return null;
            Require(Field<bool>(controller, "dragDuplicate"), "Group Alt move did not start: handle=" +
                Field<GizmoHandleKind>(controller, "dragHandle") + "; anchor=" + Field<int>(controller, "dragAnchorPoint") +
                "; selected=" + Field<int>(controller, "selectedAnchorPoint") + "; mouse=" + moveStart +
                "; axisHit=" + scene.HitTestGizmo(BlueprintEditorTool.Transform, new Vector3(0,.5f,0),
                    Quaternion.identity, true, moveStart, out GizmoAxis diagnosticAxis) + "/" + diagnosticAxis);
            moveEnd = moveStart + Field<Vector2>(controller, "dragScreenDirection") * 70;
            Frame(controller, input, moveEnd, 1, KeyCode.LeftAlt); yield return null;
            Require(doc.Parts.Count == 4 && doc.Groups.Count == 1 && Field<IList>(scene, "duplicatePreviews").Count == 2,
                "Group Alt preview omitted descendants or changed group document");
            Frame(controller, input, moveEnd); yield return null;
            Require(doc.Parts.Count == 6 && doc.Groups.Count == 2 && doc.Parts[0].ParentGroupId == originalGroup &&
                doc.Parts[4].ParentGroupId == doc.Parts[5].ParentGroupId && doc.Parts[4].ParentGroupId != originalGroup,
                "Group Alt commit lost hierarchy or changed original group");
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(doc.Parts.Count == 4 && doc.Groups.Count == 1, "Group copy Undo is not atomic");
            Frame(controller, input, left); yield return null;
            Frame(controller, input, left, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, left); yield return null;
            NativeClick(view, uiCamera, "Row_left"); yield return null;
            Frame(controller, input, left); yield return null;

            // Separate uniform scale square wins the real controller hit ordering.
            var gizmo = Field<TransformGizmoView>(scene, "gizmo");
            var scaleLine = Field<LineRenderer>(gizmo, "scaleHandle");
            Vector2 scaleMouse = scene.Camera.WorldToScreenPoint((scaleLine.GetPosition(0) + scaleLine.GetPosition(2)) * .5f);
            Frame(controller, input, scaleMouse, 1); yield return null;
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Scale &&
                Field<int>(controller, "dragAnchorPoint") < 0, "Scale square mistaken for anchor/plane");
            Require(Math.Abs(view.ScaleStepPercent - 10f) < .0001f, "Default scale step is not 10 percentage points");
            Frame(controller, input, scaleMouse + Vector2.up * 26, 1); yield return null;
            Frame(controller, input, scaleMouse + Vector2.up * 26); yield return null;
            Require(Math.Abs(doc.Parts[0].Scale.X - 1.3) < .00001 && doc.Parts[0].Scale.X == doc.Parts[0].Scale.Y &&
                doc.Parts[0].Scale.Y == doc.Parts[0].Scale.Z, "Scale frame gesture did not quantize 126% to uniform 130%");
            scaleMouse = scene.Camera.WorldToScreenPoint((scaleLine.GetPosition(0) + scaleLine.GetPosition(2)) * .5f);
            Frame(controller, input, scaleMouse, 1); yield return null;
            Frame(controller, input, scaleMouse + Vector2.up * 10, 1); yield return null;
            Frame(controller, input, scaleMouse + Vector2.up * 10); yield return null;
            Require(Math.Abs(doc.Parts[0].Scale.X - 1.4) < .00001,
                "Single 130% + one scale step did not match inspector 140%");
            Frame(controller, input, scaleMouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, scaleMouse); yield return null;
            Require(Math.Abs(doc.Parts[0].Scale.X - 1.3) < .00001, "Second scale step did not undo to 130%");
            // Shift held after pointer-down bypasses the step, as with F9 move/rotation.
            scaleMouse = scene.Camera.WorldToScreenPoint((scaleLine.GetPosition(0) + scaleLine.GetPosition(2)) * .5f);
            Frame(controller, input, scaleMouse, 1); yield return null;
            Frame(controller, input, scaleMouse + Vector2.up * 7, 1, KeyCode.LeftShift); yield return null;
            Require(Math.Abs(Field<float>(controller, "dragScale") - (1f + .07f / 1.3f)) < .0001f,
                "Shift held during scale still quantizes the scale delta");
            Frame(controller, input, scaleMouse + Vector2.up * 7, 1, KeyCode.Escape); yield return null;
            Frame(controller, input, scaleMouse); yield return null;
            Require(Math.Abs(doc.Parts[0].Scale.X - 1.3) < .00001, "Cancelled free scale changed committed 130%");

            foreach (object tick in TestViewportToolbar(controller, input, scene, view, uiCamera))
                yield return tick;

            NativeClick(view, uiCamera, "View"); yield return null;
            Require(view.HasViewportSettings, "View options failed to open");
            Vector3 beforeBlocked = V(doc.Parts[0].Position);
            Frame(controller, input, left, 1); yield return null;
            Frame(controller, input, left + Vector2.right * 70, 1); yield return null;
            Frame(controller, input, left); yield return null;
            Require(V(doc.Parts[0].Position) == beforeBlocked && Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.None,
                "Viewport options allow click-through transform");
            NativeClick(view, uiCamera, "ViewportSettingsReset"); yield return null;
            Frame(controller, input, left, 0, KeyCode.Escape); yield return null;
            Require(!view.HasViewportSettings && controller.IsOpen, "Esc closes editor instead of viewport options");
            Frame(controller, input, left); yield return null;

            // Real catalog click enters placement; wheel rotates while camera gestures remain available.
            Frame(controller, input, left, 0, KeyCode.LeftShift, KeyCode.A); yield return null;
            Require(view.HasCatalog, "Shift+A did not open catalog");
            NativeClick(view, uiCamera, "Catalog_fixture"); yield return null;
            Frame(controller, input, left); yield return null;
            Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null, "Catalog card did not start placement");
            Frame(controller, input, left, 2); yield return null;
            Frame(controller, input, left); yield return null;
            Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") == null && view.HasCatalog && doc.Parts.Count == 4,
                "Plain RMB after placing a part did not keep the part and reopen the catalog");
            NativeClick(view, uiCamera, "Catalog_fixture"); yield return null;
            Frame(controller, input, left); yield return null;
            Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null,
                "Reopened catalog did not start the next part placement");
            Require(scene.ShowPlacementPreview("fixture", new Vector3(4f, -1f, 0f), Quaternion.identity,
                    false, out Vector3 freeOrigin, out string originWarning) &&
                string.IsNullOrEmpty(originWarning) && Math.Abs(freeOrigin.y + .5f) < .0001f,
                "Placement preview did not rest its real mesh on the aimed surface");
            Require(scene.ShowPlacementPreview("fixture", new Vector3(-1.5f, -.45f, 0f), Quaternion.identity,
                    true, out Vector3 belowGrid, out string snapWarning, 5) &&
                string.IsNullOrEmpty(snapWarning) && belowGrid.y < scene.GroundHeight - .1f,
                "Native upper source point cannot snap below the editor grid");
            Vector2 deleteDuringPlacement = scene.Camera.WorldToScreenPoint(V(doc.Parts[0].Position));
            Require(scene.TryPick(deleteDuringPlacement, out _),
                "Placement-delete fixture is not pickable");
            Frame(controller, input, deleteDuringPlacement, 4); yield return null;
            Frame(controller, input, deleteDuringPlacement); yield return null;
            Require(doc.Parts.Count == 3 && Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null,
                "MMB click did not delete a part while catalog placement stayed active");
            Frame(controller, input, deleteDuringPlacement, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, deleteDuringPlacement); yield return null;
            Require(doc.Parts.Count == 4 && Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null,
                "Ctrl+Z did not restore an MMB-deleted part while placement stayed active");
            Frame(controller, input, deleteDuringPlacement, 0, KeyCode.LeftControl, KeyCode.Y); yield return null;
            Frame(controller, input, deleteDuringPlacement); yield return null;
            Require(doc.Parts.Count == 3 && Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null,
                "Ctrl+Y did not repeat deletion while placement stayed active");
            Frame(controller, input, deleteDuringPlacement, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, deleteDuringPlacement); yield return null;
            Require(doc.Parts.Count == 4,
                "Second Ctrl+Z did not restore the placement-delete fixture");
            float yaw = Field<float>(controller, "cameraYaw");
            Frame(controller, input, left, 4); yield return null;
            Frame(controller, input, left + new Vector2(40,20), 4); yield return null;
            Frame(controller, input, left + new Vector2(40,20)); yield return null;
            Require(Math.Abs(Field<float>(controller, "cameraYaw") - yaw) > 1, "Placement swallowed MMB orbit");
            Vector3 focus = Field<Vector3>(controller, "cameraFocus");
            Frame(controller, input, left, 4, KeyCode.LeftShift); yield return null;
            Frame(controller, input, left + Vector2.right * 35, 4, KeyCode.LeftShift); yield return null;
            Frame(controller, input, left); yield return null;
            Require(Vector3.Distance(Field<Vector3>(controller, "cameraFocus"), focus) > .01, "Placement swallowed Shift+MMB pan");
            focus = Field<Vector3>(controller, "cameraFocus");
            Frame(controller, input, left, 2, KeyCode.W); yield return null;
            Frame(controller, input, left, 2, KeyCode.W); yield return null;
            Frame(controller, input, left); yield return null;
            Require(Vector3.Distance(Field<Vector3>(controller, "cameraFocus"), focus) > .01 && !view.HasCatalog &&
                Field<BlueprintEditorCatalogItem>(controller, "placementItem") != null, "RMB+W placement navigation cancelled/opened catalog");
            input.Next(left); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); yield return null;
            Require(Math.Abs(Field<float>(controller, "placementYaw") - 22.5f) < .001,
                "Wheel did not rotate placement by the world-style step");
            int placementSnapPoints = scene.PlacementSourceSnapPointCount;
            Require(placementSnapPoints > 0, "Placement fixture has no native source snap points");
            Frame(controller, input, left, 0, KeyCode.E); yield return null;
            Require(Math.Abs(Field<float>(controller, "placementYaw") - 22.5f) < .001 &&
                Field<int>(controller, "placementManualSnapPoint") == 0 &&
                Field<TMP_Text>(view, "statusText").text.Contains("Точка привязки"),
                "E did not select the first source snap point without rotating placement");
            Frame(controller, input, left); yield return null;
            Vector2 placeMouse = view.ViewportScreenRect().center + Vector2.down * 120;
            Frame(controller, input, placeMouse); yield return null;
            Vector3 placedAt = Field<Vector3>(controller, "placementPosition");
            Require(Field<object>(scene, "placementPreview") != null, "Navigation lost placement preview");
            Save(scene, view, uiCamera, target, Path.Combine(output, "input-placement-navigation.png"));
            Frame(controller, input, placeMouse, 1); yield return null;
            Frame(controller, input, placeMouse); yield return null;
            Require(doc.Parts.Count == 5 && Vector3.Distance(V(doc.Parts[4].Position), placedAt) < .001, "Placed transform differs from preview");
            Frame(controller, input, placeMouse, 0, KeyCode.G); yield return null;
            TMP_InputField[] transformInputs = Field<TMP_InputField[]>(view, "inspectorPosition");
            Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") == null &&
                Field<BlueprintEditorTool>(controller, "activeTool") == BlueprintEditorTool.Transform &&
                transformInputs[0].gameObject.activeInHierarchy,
                "G did not leave repeat placement for numeric Transform controls");
            NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Select]); yield return null;
            Frame(controller, input, placeMouse); yield return null;
            Require(Field<BlueprintEditorCatalogItem>(controller, "placementItem") == null, "Select toolbar failed to leave placement");
            Require(Field<GameObject>(view, "inspectorEditPanel").activeSelf,
                "Select mode hides numeric Transform controls for its active gizmo");
            // Select a different part through the row, then hover/click the newly inserted mesh.
            NativeClick(view, uiCamera, "Row_left"); yield return null;
            Vector2 newPartMouse = scene.Camera.WorldToScreenPoint(placedAt);
            Frame(controller, input, newPartMouse); yield return null;
            string newId = doc.Parts[4].StableId;
            Require(scene.TryPick(newPartMouse, out string hit) && hit == newId && IsPainted(scene, newId),
                "Newly inserted part cannot be picked/hovered after placement");
            // Gizmo handles legitimately own their visible area. Click a rendered surface pixel outside them.
            newPartMouse = ClearSurfacePoint(controller, scene, newId, newPartMouse, V(doc.Parts[0].Position));
            Frame(controller, input, newPartMouse, 1); yield return null;
            string clickDiagnostic = "handle=" + Field<GizmoHandleKind>(controller, "dragHandle") +
                "; anchor=" + Field<int>(controller, "dragAnchorPoint") + "; selected=" + Field<int>(controller, "selectedAnchorPoint") +
                "; activeTool=" + Field<BlueprintEditorTool>(controller, "activeTool");
            Frame(controller, input, newPartMouse); yield return null;
            Require(doc.IsPartSelected(newId), "Newly inserted part click did not select it: " + clickDiagnostic + "; position=" + placedAt);
            Frame(controller, input, newPartMouse, 4); yield return null;
            Frame(controller, input, newPartMouse); yield return null;
            Require(doc.Parts.Count == 4, "MMB click did not delete the selected viewport part");
            Frame(controller, input, newPartMouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, newPartMouse); yield return null;
            Require(doc.Parts.Count == 5, "Undo did not restore the MMB-deleted viewport part");

            Frame(controller, input, newPartMouse, 0, KeyCode.LeftShift, KeyCode.A); yield return null;
            NativeClick(view, uiCamera, "CatalogBlueprintsMode"); yield return null;
            NativeClick(view, uiCamera, "Catalog_compound"); yield return null;
            input.Next(placeMouse); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); yield return null;
            Frame(controller, input, placeMouse); yield return null;
            IList compoundVisuals = Field<IList>(scene, "duplicatePreviews");
            Require(compoundVisuals.Count == 2 && doc.Parts.Count == 5, "Compound catalog did not preview both scaled parts without committing");
            var previewPositions = new List<Vector3>();
            foreach (object visual in compoundVisuals) previewPositions.Add(Field<GameObject>(visual, "Root").transform.position);
            int previewInstance = Field<GameObject>(compoundVisuals[0], "Root").GetInstanceID();
            Frame(controller, input, placeMouse); yield return null;
            Require(Field<GameObject>(Field<IList>(scene, "duplicatePreviews")[0], "Root").GetInstanceID() == previewInstance,
                "Compound preview recreated visual instances on an unchanged frame");
            Require(scene.TryPlacementSurface(placeMouse, out Vector3 compoundSurface), "Compound target surface unavailable");
            float minimumY = float.PositiveInfinity;
            foreach (object visual in compoundVisuals)
                foreach (Renderer renderer in Field<Renderer[]>(visual, "Renderers")) minimumY = Mathf.Min(minimumY, renderer.bounds.min.y);
            if (!scene.PlacementSnapTarget.HasValue)
                Require(Mathf.Abs(minimumY - compoundSurface.y) < .001, "Compound preview bottom does not touch actual surface");
            else
            {
                float snapDistance = float.PositiveInfinity;
                foreach (object visual in compoundVisuals)
                    foreach (Vector3 local in Field<List<Vector3>>(visual, "SnapLocal"))
                        snapDistance = Mathf.Min(snapDistance, Vector3.Distance(Field<GameObject>(visual, "Root").transform.TransformPoint(local),
                            scene.PlacementSnapTarget.Value));
                Require(snapDistance < .001, "Compound native target does not coincide with any displayed native point");
            }
            Save(scene, view, uiCamera, target, Path.Combine(output, "input-compound-preview.png"));
            Frame(controller, input, placeMouse, 1); yield return null;
            Frame(controller, input, placeMouse); yield return null;
            Require(doc.Parts.Count == 7 && doc.Groups.Count == 2 && doc.Selection.Count == 1 &&
                doc.Parts[5].ParentGroupId == doc.Parts[6].ParentGroupId &&
                Vector3.Distance(V(doc.Parts[5].Position), previewPositions[0]) < .001 &&
                Vector3.Distance(V(doc.Parts[6].Position), previewPositions[1]) < .001 &&
                Math.Abs(doc.Parts[5].Scale.X - .5) < .0001 && Math.Abs(doc.Parts[6].Scale.X - 2) < .0001,
                "Compound commit loses hierarchy, scale or preview transforms");
            NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Select]); yield return null;
            Frame(controller, input, placeMouse, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Require(doc.Parts.Count == 5 && doc.Groups.Count == 0 && Field<IList>(scene, "duplicatePreviews").Count == 0,
                "Compound import does not cancel preview/undo atomically");
            Frame(controller, input, placeMouse); yield return null;
            foreach (object tick in TestArrayInput(controller, input, scene, view, uiCamera, target, output))
                yield return tick;
            NativeClick(view, uiCamera, "ProjectionToggle"); yield return null;
            Require(scene.Camera.orthographic, "Projection precondition before reopen");
            controller.Close(true);
            Require(controller.OpenNew(out error), "Reopen after projection: " + error);
            scene = Field<BlueprintEditorScene>(controller, "scene"); view = Field<BlueprintEditorView>(controller, "view");
            Require(scene.Camera.orthographic && Field<bool>(view, "orthographic"), "Reopen projection UI disagrees with camera");
            Require(errors.Count == 0, "Controller logged errors: " + string.Join("; ", errors));
            File.WriteAllText(Path.Combine(output, "controller-input-check.txt"),
                "PASS: actual Update/LateUpdate with deterministic key/button edges; actual EventSystem GraphicRaycaster and pointer handlers. " +
                "Ctrl+A editable; Select Shift rows/viewport/blue-release-gizmo; Transform clicks/box do not change selection; " +
                "multi-selection Shift pin behind unselected mesh, X/Y/Z constrained anchor rotation about fixed pin; " +
                "native pin; Outliner native drag blocks Ctrl+A/Delete, Esc preserves selection and late drop cannot reparent; " +
                "Alt Move preview+commit+Undo+cancel; group copy hierarchy; uniform 10% scale step/Shift free/cancel; " +
                "viewport grid/projection icons via native raycast, no camera wheel/gizmo/selection interception; " +
                "viewport options/click-through guard; catalog single placement; " +
                "MMB orbit/Shift pan/RMB fly/QE; Select toolbar exits placement; new-part hover/select; " +
                "compound catalog preview/ground/scale/hierarchy/commit/Undo; projection reopen; " +
                "Array native arrow press/drag/release, wheel last direction/Ctrl zoom, Pack/Fit/Exact including 0.2m second Fit drag on 2m source, all numeric TMP parameters, symmetry, preview/apply/Undo.\n" +
                "Input provider is substituted; this does not inject physical OS keys or prove Valheim host input interception.");
        }
        finally
        {
            uiCamera.targetTexture = null; Object.Destroy(uiCamera.gameObject); Object.Destroy(target); Object.Destroy(source);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }
    }

    private static IEnumerable TestOutlinerDragCancel(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, string originalGroup)
    {
        BlueprintEditorDocument doc = controller.Document;
        NativeClick(view, uiCamera, "Row_left"); yield return null;
        Require(doc.Selection.Count == 1 && doc.IsPartSelected("left") && doc.Parts[0].ParentGroupId == originalGroup,
            "Tree drag cancellation precondition: selected nested child missing");
        RectTransform rows = Field<RectTransform>(view, "outlinerRows");
        Button row = rows.Find("Row_left").GetComponent<Button>();
        var rowRect = (RectTransform)row.transform;
        Vector2 mouse = RectTransformUtility.WorldToScreenPoint(uiCamera, rowRect.TransformPoint(rowRect.rect.center));
        Frame(controller, input, mouse, 1); yield return null;
        GameObject dragHost = ExecuteEvents.GetEventHandler<IDragHandler>(row.gameObject);
        Require(dragHost == rows.gameObject, "Tree drag host is not the persistent Outliner rows object");
        var pointer = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left, eligibleForClick = true,
            pointerPress = row.gameObject, pointerClick = row.gameObject, pointerDrag = dragHost,
            pointerPressRaycast = new RaycastResult { gameObject = row.gameObject },
            pressPosition = mouse, position = mouse + Vector2.right * 20f, delta = Vector2.right * 20f
        };
        var module = EventSystem.current.GetComponent<StandaloneInputModule>();
        typeof(PointerInputModule).GetMethod("ProcessDrag", Private).Invoke(module, new object[] { pointer });
        Require(pointer.dragging && !pointer.eligibleForClick && view.IsOutlinerDragging,
            "Native drag did not enter actual Outliner transfer state");
        Frame(controller, input, mouse, 1, KeyCode.LeftControl, KeyCode.A, KeyCode.Delete); yield return null;
        Require(view.IsOutlinerDragging && doc.Parts.Count == 4 && doc.Groups.Count == 1 &&
            doc.Selection.Count == 1 && doc.IsPartSelected("left") && doc.Parts[0].ParentGroupId == originalGroup,
            "Ctrl+A/Delete changed selection or geometry during an Outliner drag");
        Frame(controller, input, mouse, 1, KeyCode.Escape); yield return null;
        Require(!view.IsOutlinerDragging && doc.Selection.Count == 1 && doc.IsPartSelected("left") &&
            doc.Parts[0].ParentGroupId == originalGroup && controller.IsOpen,
            "Escape cancelled more than the Outliner transfer or left drag state active");
        GameObject rootDrop = Field<TMP_Text>(view, "outlinerTitle").transform.parent.gameObject;
        ExecuteEvents.ExecuteHierarchy(rootDrop, pointer, ExecuteEvents.pointerEnterHandler);
        typeof(StandaloneInputModule).GetMethod("ReleaseMouse", Private).Invoke(module, new object[] { pointer, rootDrop });
        Frame(controller, input, mouse); yield return null;
        Require(!view.IsOutlinerDragging && doc.Parts.Count == 4 && doc.Groups.Count == 1 &&
            doc.Parts[0].ParentGroupId == originalGroup && doc.Selection.Count == 1 && doc.IsPartSelected("left"),
            "Delayed native drop after Escape moved the selected child or changed selection");
        NativeClick(view, uiCamera, "Row_" + originalGroup); yield return null;
        Frame(controller, input, scene.Camera.WorldToScreenPoint(new Vector3(0,.5f,0))); yield return null;
        Require(doc.Selection.Count == 1 && doc.Selection[0] == originalGroup,
            "Tree cancellation fixture did not restore the selected group");
    }

    private static IEnumerable TestViewportToolbar(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera)
    {
        BlueprintEditorDocument doc = controller.Document;
        string selection = string.Join("|", doc.Selection);
        Vector3 position = V(doc.Parts[0].Position);
        double scale = doc.Parts[0].Scale.X;
        float distance = Field<float>(controller, "cameraDistance");
        float yaw = Field<float>(controller, "cameraYaw"), pitch = Field<float>(controller, "cameraPitch");
        foreach (string field in new[] { "gridButton", "projectionButton", "gridButton", "projectionButton" })
        {
            Button button = Field<Button>(view, field);
            var rect = (RectTransform)button.transform;
            Vector2 mouse = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center));
            Require(view.ViewportScreenRect().Contains(mouse) && view.IsViewportControlHit(mouse),
                "Grid/projection icon is not a protected in-viewport control");
            bool gridBefore = Field<Transform>(scene, "majorGrid").gameObject.activeSelf;
            bool projectionBefore = scene.Camera.orthographic;
            input.Next(mouse); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); yield return null;
            Require(Mathf.Abs(Field<float>(controller, "cameraDistance") - distance) < .0001f,
                "Wheel over viewport icon zoomed the editor camera");
            Frame(controller, input, mouse, 1); yield return null;
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.None &&
                Field<int>(controller, "dragAnchorPoint") < 0 && !Field<bool>(controller, "selectionPending"),
                "Viewport icon pointer-down began a gizmo or selection gesture");
            NativeClick(view, uiCamera, button);
            Frame(controller, input, mouse); yield return null;
            Require(field == "gridButton"
                ? Field<Transform>(scene, "majorGrid").gameObject.activeSelf != gridBefore && scene.Camera.orthographic == projectionBefore
                : scene.Camera.orthographic != projectionBefore && Field<Transform>(scene, "majorGrid").gameObject.activeSelf == gridBefore,
                "Native viewport icon did not toggle only its intended state");
            Require(string.Join("|", doc.Selection) == selection && V(doc.Parts[0].Position) == position &&
                doc.Parts[0].Scale.X == scale && Mathf.Abs(Field<float>(controller, "cameraYaw") - yaw) < .0001f &&
                Mathf.Abs(Field<float>(controller, "cameraPitch") - pitch) < .0001f,
                "Viewport icon changed selection, geometry or camera orientation");
        }
        Require(Field<Transform>(scene, "majorGrid").gameObject.activeSelf &&
            Field<Transform>(scene, "minorGrid").gameObject.activeSelf && !scene.Camera.orthographic,
            "Viewport toolbar fixture did not restore grid/perspective");
    }

    private static IEnumerable TestTransformContract(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string output)
    {
        BlueprintEditorDocument doc = controller.Document;
        Frame(controller, input, Vector2.zero); NativeClick(view, uiCamera, "Row_left"); yield return null;
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
        Frame(controller, input, Vector2.zero); yield return null;
        Vector3 originalLeft = V(doc.Parts[0].Position), originalRight = V(doc.Parts[1].Position);
        Vector2 right = scene.Camera.WorldToScreenPoint(originalRight);
        Vector2 otherSurface = ClearSurfacePoint(controller, scene, "right", right, originalLeft);
        Vector2 empty = EmptyTransformPoint(controller, scene, view, originalLeft);
        foreach (KeyCode[] keys in new[] { Array.Empty<KeyCode>(), new[] { KeyCode.LeftShift }, new[] { KeyCode.LeftControl } })
        {
            Frame(controller, input, otherSurface, 1, keys); yield return null;
            Require(!Field<bool>(controller, "selectionPending"), "Transform started a selection on another piece");
            Frame(controller, input, otherSurface, 0, keys); yield return null;
            Frame(controller, input, empty, 1, keys); yield return null;
            Frame(controller, input, empty, 0, keys); yield return null;
            Require(doc.Selection.Count == 1 && doc.IsPartSelected("left") && !doc.IsPartSelected("right"),
                "Transform click changed selection, including modifier click");
            Frame(controller, input, empty); yield return null;
        }
        Frame(controller, input, otherSurface, 1); yield return null;
        Frame(controller, input, empty, 1); yield return null;
        Frame(controller, input, empty); yield return null;
        Require(doc.Selection.Count == 1 && doc.IsPartSelected("left") &&
            V(doc.Parts[0].Position) == originalLeft && V(doc.Parts[1].Position) == originalRight,
            "Transform empty-space box changed selection or source transforms");

        // Select is still additive. Both selected parts must rotate around the same native pin.
        Frame(controller, input, Vector2.zero, 0, KeyCode.LeftShift);
        NativeClick(view, uiCamera, "Row_right"); yield return null;
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
        Frame(controller, input, empty); yield return null;
        Require(doc.EditablePartSelectionCount == 2, "Pin fixture lacks multi-selection");
        Vector3[] anchors = Field<Vector3[]>(controller, "gizmoAnchors");
        Vector3 pin = anchors[Field<int>(controller, "gizmoNativeAnchorStart")];
        Vector3 occluder = pin + (scene.Camera.transform.position - pin).normalized * 0.7f;
        doc.AddPart(new BlueprintEditorPart("pin-occluder", "fixture", "Pin occluder",
            new Point3(occluder.x, occluder.y, occluder.z), new Rotation3(0,0,0,1), scale:new Point3(.45,.45,.45)));
        doc.ClearSelection();
        Require(scene.TrySync(doc, out string error), error); view.Bind(doc, true, null, null);
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Select]);
        Frame(controller, input, empty); yield return null;
        Vector3[] selectablePoints = { originalLeft, originalRight, occluder };
        for (int index = 0; index < selectablePoints.Length; ++index)
        {
            Vector2 point = scene.Camera.WorldToScreenPoint(selectablePoints[index]);
            Frame(controller, input, point, 1, KeyCode.LeftShift); yield return null;
            Frame(controller, input, point, 0, KeyCode.LeftShift); yield return null;
            Require(Field<BlueprintEditorTool>(controller, "activeTool") == BlueprintEditorTool.Select &&
                doc.EditablePartSelectionCount == index + 1,
                "Select did not remain additive across three Shift clicks without re-entering Select");
        }
        Frame(controller, input, empty); yield return null;
        doc.SelectOnly("left"); doc.ToggleSelection("right");
        Require(scene.TrySync(doc, out error), error); view.Bind(doc, true, null, null);
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
        Frame(controller, input, empty); yield return null;
        foreach (GizmoAxis axis in new[] { GizmoAxis.X, GizmoAxis.Y, GizmoAxis.Z })
        {
            Vector2 pinMouse = scene.Camera.WorldToScreenPoint(pin);
            Require(scene.TryPick(pinMouse, out string picked) && picked == "pin-occluder",
                "Pin regression precondition: unselected mesh does not occlude selected native point");
            Frame(controller, input, pinMouse, 1, KeyCode.LeftShift); yield return null;
            Frame(controller, input, pinMouse, 0, KeyCode.LeftShift); yield return null;
            Require(Field<Vector3?>(controller, "pinnedAnchorWorld").HasValue &&
                Vector3.Distance(Field<Vector3?>(controller, "pinnedAnchorWorld").Value, pin) < .0001f &&
                doc.EditablePartSelectionCount == 2 && !doc.IsPartSelected("pin-occluder"),
                "Transform Shift pin was intercepted by an unselected occluding mesh");
            Frame(controller, input, pinMouse); yield return null;
            KeyCode key = axis == GizmoAxis.X ? KeyCode.X : axis == GizmoAxis.Y ? KeyCode.Y : KeyCode.Z;
            Frame(controller, input, pinMouse, 0, key); yield return null;
            Frame(controller, input, pinMouse); yield return null;
            Require(Field<GizmoAxis>(controller, "anchorConstraintAxis") == axis, "Pinned X/Y/Z shortcut lost axis " + axis);
            Require(Field<LineRenderer>(Field<TransformGizmoView>(scene, "gizmo"), "constraintPath").gameObject.activeSelf,
                "Pinned X/Y/Z has no constraint path before dragging " + axis);
            Vector3 axisWorld = axis == GizmoAxis.X ? Vector3.right : axis == GizmoAxis.Y ? Vector3.up : Vector3.forward;
            anchors = Field<Vector3[]>(controller, "gizmoAnchors");
            int moving = -1; float bestRadius = 0f;
            for (int index = Field<int>(controller, "gizmoNativeAnchorStart"); index < anchors.Length; ++index)
            {
                Vector3 delta = anchors[index] - pin;
                float radius = Vector3.Cross(delta, axisWorld).magnitude;
                Vector2 point = scene.Camera.WorldToScreenPoint(anchors[index]);
                int hit = scene.HitTestAnchor(anchors, point);
                if (radius > bestRadius && radius > .25f && hit >= 0 &&
                    Vector3.Distance(anchors[hit], anchors[index]) < .0001f && !scene.HitTestScale(point))
                { moving = index; bestRadius = radius; }
            }
            Require(moving >= 0, "No non-collinear native point for constrained axis " + axis);
            Vector3 movingPoint = anchors[moving];
            Vector2 movingMouse = scene.Camera.WorldToScreenPoint(movingPoint);
            Frame(controller, input, movingMouse, 1); yield return null;
            Require(Field<bool>(controller, "dragConstraintActive") &&
                Field<int>(controller, "dragAnchorPoint") >= 0 &&
                Vector3.Dot(Field<Vector3>(controller, "dragAxisWorld"), axisWorld) > .999f,
                "Pinned native drag did not enter constrained rotation " + axis);
            Quaternion expected = Quaternion.AngleAxis(30f, axisWorld);
            Vector3 rotatedPoint = pin + expected * (movingPoint - pin);
            Vector2 end = scene.Camera.WorldToScreenPoint(rotatedPoint);
            Frame(controller, input, end, 1); yield return null;
            Require(Quaternion.Angle(Field<Quaternion>(controller, "dragRotation"), expected) < .1f &&
                Vector3.Distance(Field<Vector3?>(controller, "pinnedAnchorWorld").Value, pin) < .0001f,
                "Constrained native drag rotates about a different axis or moves the pin " + axis);
            if (axis == GizmoAxis.X)
                Save(scene, view, uiCamera, target, Path.Combine(output, "input-transform-occluded-pin-x.png"));
            Frame(controller, input, end); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Position), pin + expected * (originalLeft - pin)) < .002f &&
                Vector3.Distance(V(doc.Parts[1].Position), pin + expected * (originalRight - pin)) < .002f &&
                doc.EditablePartSelectionCount == 2 && !doc.IsPartSelected("pin-occluder"),
                "Constrained rotation commit differs from the fixed-pin multi-selection preview " + axis);
            Frame(controller, input, empty, 0, KeyCode.LeftControl, KeyCode.Z); yield return null;
            Frame(controller, input, empty); yield return null;
            Require(Vector3.Distance(V(doc.Parts[0].Position), originalLeft) < .0001f &&
                Vector3.Distance(V(doc.Parts[1].Position), originalRight) < .0001f,
                "Constrained multi-selection rotation did not undo atomically");
        }
        // Undo restored the group poses; remove only the explicit occlusion fixture.
        doc.SelectOnly("pin-occluder"); Require(doc.DeleteSelection(true), "Pin fixture cleanup failed");
        doc.SelectOnly("left"); Require(scene.TrySync(doc, out error), error); view.Bind(doc, true, null, null);
        Frame(controller, input, empty, 0, KeyCode.Z); yield return null;
        Frame(controller, input, empty); yield return null;
        Require(doc.Parts.Count == 4 && Field<GizmoAxis>(controller, "anchorConstraintAxis") == GizmoAxis.None,
            "Constrained pin fixture left a part or active axis behind");
    }

    private static Vector2 EmptyTransformPoint(BlueprintEditorController controller, BlueprintEditorScene scene,
        BlueprintEditorView view, Vector3 pivot)
    {
        Rect viewport = view.ViewportScreenRect();
        for (int y = 1; y < 5; ++y)
        for (int x = 1; x < 5; ++x)
        {
            Vector2 point = new Vector2(Mathf.Lerp(viewport.xMin, viewport.xMax, x / 5f),
                Mathf.Lerp(viewport.yMin, viewport.yMax, y / 5f));
            if (!scene.TryPick(point, out _) && !scene.HitTestScale(point) &&
                scene.HitTestAnchor(Field<Vector3[]>(controller, "gizmoAnchors"), point) < 0 &&
                scene.HitTestGizmo(BlueprintEditorTool.Transform, pivot, Quaternion.identity, true, point, out _) == GizmoHandleKind.None)
                return point;
        }
        throw new InvalidOperationException("Input frames: no empty viewport pixel outside transform handles");
    }

    private static IEnumerable TestArrayInput(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string output)
    {
        BlueprintEditorDocument doc = controller.Document;
        NativeClick(view, uiCamera, "Row_left"); yield return null;
        Vector2 mouse = scene.Camera.WorldToScreenPoint(V(doc.Parts[0].Position));
        Frame(controller, input, mouse); yield return null;
        NativeClick(view, uiCamera,
            Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
        yield return null;
        Require(Field<BlueprintEditorTool>(controller, "activeTool") == BlueprintEditorTool.Transform &&
            Field<GameObject>(view, "inspectorEditPanel").activeSelf,
            "Array fixture did not open the Transform inspector");
        TMP_InputField sourceScale = Field<TMP_InputField>(view, "inspectorScale");
        sourceScale.text = "200"; sourceScale.onEndEdit.Invoke(sourceScale.text);
        Frame(controller, input, mouse); yield return null;
        Require(Math.Abs(doc.Parts[0].Scale.X - 2) < .00001, "Array fixture did not establish 2m native span");
        NativeClick(view, uiCamera, Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Array]);
        Frame(controller, input, mouse); yield return null;
        Require(Field<RepeatDistributionMode>(controller, "arrayDistribution") == RepeatDistributionMode.Pack,
            "Fresh Array session did not start in predictable Pack mode");
        NativeClick(view, uiCamera, "ArrayDistribution"); yield return null;
        Require(Field<RepeatDistributionMode>(controller, "arrayDistribution") == RepeatDistributionMode.Fit,
            "Array reset fixture did not enter Fit mode");
        NativeClick(view, uiCamera,
            Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Transform]);
        NativeClick(view, uiCamera,
            Field<Dictionary<BlueprintEditorTool, Button>>(view, "toolButtons")[BlueprintEditorTool.Array]);
        Frame(controller, input, mouse); yield return null;
        Require(Field<RepeatDistributionMode>(controller, "arrayDistribution") == RepeatDistributionMode.Pack &&
            Field<int>(controller, "arrayCountX") == 2 && Field<int>(controller, "arrayCountY") == 1 &&
            !Field<bool>(controller, "arraySymmetric") &&
            Mathf.Abs(Field<float>(controller, "arrayRise")) < .0001f,
            "Fresh Array session inherited the previous distribution or profile");
        Require(Field<int>(controller, "arrayPreviewCount") == 0 &&
            Field<TMP_Text>(view, "arrayInfo").text.Contains("Потяни золотую стрелку"), "Array has no explicit pending-direction state");
        foreach (GizmoAxis axis in new[] { GizmoAxis.X, GizmoAxis.Z })
        {
            Vector2 start = LayoutHandle(controller, scene, axis);
            Frame(controller, input, start, 1); yield return null;
            Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Layout &&
                Field<GizmoAxis>(controller, "dragAxis") == axis, "Golden arrow pointer did not begin layout " + axis);
            int count = axis == GizmoAxis.X ? 3 : 2;
            float pixels = (count - 1) * Field<float>(controller, "dragArrayStepLength") /
                Field<float>(controller, "dragWorldUnitsPerPixel");
            Vector2 end = start + Field<Vector2>(controller, "dragScreenDirection") * pixels;
            Frame(controller, input, end, 1); yield return null;
            Frame(controller, input, end); yield return null;
            mouse = scene.Camera.WorldToScreenPoint(V(doc.Parts[0].Position));
            float distance = Field<float>(controller, "cameraDistance");
            float packStep = Field<Vector3>(controller,
                axis == GizmoAxis.X ? "arrayStepX" : "arrayStepY").magnitude;
            input.Next(mouse); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); yield return null;
            Require(Field<int>(controller, axis == GizmoAxis.X ? "arrayCountX" : "arrayCountY") == count + 1 &&
                Mathf.Abs(distance - Field<float>(controller, "cameraDistance")) < .00001f &&
                Mathf.Abs(packStep - Field<Vector3>(controller,
                    axis == GizmoAxis.X ? "arrayStepX" : "arrayStepY").magnitude) < .0001f,
                "Pack wheel did not extend the row at a fixed natural step " + axis);
            input.Next(mouse, 0, KeyCode.LeftControl); input.MouseScrollDelta = Vector2.up;
            controller.Update(); controller.LateUpdate(); yield return null;
            Require(Field<int>(controller, axis == GizmoAxis.X ? "arrayCountX" : "arrayCountY") == count + 1 &&
                Field<float>(controller, "cameraDistance") < distance, "Ctrl+wheel did not zoom exclusively");
            input.Next(mouse); input.MouseScrollDelta = Vector2.down;
            controller.Update(); controller.LateUpdate(); yield return null;
        }
        Frame(controller, input, mouse); yield return null;
        Require(Field<int>(controller, "arrayCountX") == 3 && Field<int>(controller, "arrayCountY") == 2,
            "Second-direction wheel changed first count");
        NativeClick(view, uiCamera, "ArrayDistribution"); yield return null;
        Vector2 shortStart = LayoutHandle(controller, scene, GizmoAxis.Z);
        Frame(controller, input, shortStart, 1); yield return null;
        Require(Field<GizmoHandleKind>(controller, "dragHandle") == GizmoHandleKind.Layout &&
            Field<float>(controller, "dragArrayStepLength") > 1.99f, "Short Fit precondition: second arrow / 2m span");
        Vector2 shortEnd = shortStart + Field<Vector2>(controller, "dragScreenDirection") *
            (.2f / Field<float>(controller, "dragWorldUnitsPerPixel"));
        Frame(controller, input, shortEnd, 1); yield return null;
        Frame(controller, input, shortEnd); yield return null;
        Require(Mathf.Abs(Field<float>(controller, "arrayDistanceY") - .2f) < .0001f &&
            Mathf.Abs(Field<Vector3>(controller, "arrayStepY").magnitude - .2f) < .0001f &&
            Field<int>(controller, "arrayCountY") == 2,
            "Fit second arrow rejected 0.2m drag because of the 2m source span");
        float fitSpan = Field<Vector3>(controller, "arrayStepY").magnitude * (Field<int>(controller, "arrayCountY") - 1);
        input.Next(mouse); input.MouseScrollDelta = Vector2.up;
        controller.Update(); controller.LateUpdate(); yield return null;
        Require(Mathf.Abs(Field<Vector3>(controller, "arrayStepY").magnitude *
            (Field<int>(controller, "arrayCountY") - 1) - fitSpan) < .0001f, "Fit wheel changed dragged span");
        NativeClick(view, uiCamera, "ArrayDistribution"); yield return null;
        Require(Mathf.Abs(Field<Vector3>(controller, "arrayStepX").magnitude - .25f) < .0001,
            "Exact spacing preset unavailable");
        NativeClick(view, uiCamera, "ArrayStep"); yield return null;
        Require(Mathf.Abs(Field<Vector3>(controller, "arrayStepX").magnitude - .5f) < .0001,
            "Exact spacing button failed");
        NativeClick(view, uiCamera, "ArrayDistribution"); yield return null;
        NativeClick(view, uiCamera, "ArrayStep"); yield return null;
        Require(Field<TMP_Text>(view, "arrayInfo").text.Contains("Шаг:") &&
            (VisibleButtonText(view, "ArrayStep").Contains("0,01") ||
            VisibleButtonText(view, "ArrayStep").Contains("0.01")),
            "Current spacing is hidden on or below the spacing button");
        NativeClick(view, uiCamera, "ArrayMoveStep"); yield return null;
        NativeClick(view, uiCamera, "ArrayAngleStep"); yield return null;
        string moveStepLabel = VisibleButtonText(view, "ArrayMoveStep");
        Require(Mathf.Abs(Field<float>(controller, "translationStep") - .1f) < .0001f &&
            Mathf.Abs(Field<float>(controller, "rotationStep") - 5f) < .0001f &&
            (moveStepLabel.Contains("0,1") || moveStepLabel.Contains("0.1")) &&
            VisibleButtonText(view, "ArrayAngleStep").Contains("5"),
            "Array rise/angle scrub presets do not match the shared F9 values");
        TMP_InputField liveRiseField = Field<TMP_InputField>(view, "arrayRise");
        liveRiseField.SetTextWithoutNotify("0");
        IList liveBefore = Field<IList>(scene, "contourPreviews");
        var liveBeforePositions = new List<Vector3>();
        foreach (object preview in liveBefore)
            liveBeforePositions.Add(Field<GameObject>(preview, "Root").transform.position);
        int partsBeforeLiveScrub = doc.Parts.Count;
        var liveScrubPointer = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left, eligibleForClick = true, position = new Vector2(100,100) };
        BlueprintEditorNumericScrub liveScrub = liveRiseField.GetComponent<BlueprintEditorNumericScrub>();
        liveScrub.OnBeginDrag(liveScrubPointer);
        liveScrubPointer.position += Vector2.right;
        liveScrub.OnDrag(liveScrubPointer);
        controller.GetType().GetMethod("RefreshContextHints", Private).Invoke(controller, null);
        Require(view.IsNumericScrubbing &&
            Field<TMP_Text>(view, "statusHintText").text.Contains("Ctrl — быстрее"),
            "Numeric scrub does not replace unavailable tool shortcuts in the footer");
        IList liveAfter = Field<IList>(scene, "contourPreviews");
        bool movedBeforeRelease = liveAfter.Count > 0 && liveAfter.Count == liveBeforePositions.Count;
        for (int index = 0; movedBeforeRelease && index < liveAfter.Count; ++index)
            if (Vector3.Distance(Field<GameObject>(liveAfter[index], "Root").transform.position,
                liveBeforePositions[index]) > .001f) { movedBeforeRelease = true; break; }
            else if (index == liveAfter.Count - 1) movedBeforeRelease = false;
        Require(Mathf.Abs(Field<float>(controller, "arrayRise") - .1f) < .0001f && movedBeforeRelease &&
            doc.Parts.Count == partsBeforeLiveScrub,
            "Array numeric scrub did not rebuild the visible ghost live before release or mutated the document");
        liveScrub.OnEndDrag(liveScrubPointer);
        Require(!view.IsNumericScrubbing, "Numeric scrub remains active after drag release");
        TMP_InputField fractionalScale = Field<TMP_InputField>(view, "arrayScaleStepX");
        fractionalScale.text = "0.1"; fractionalScale.onEndEdit.Invoke(fractionalScale.text);
        Require(Mathf.Abs(Field<float>(controller, "arrayScaleStepX") - .001f) < .000001f &&
            Field<int>(controller, "arrayPreviewCount") > 0, "Fractional 0.1% Array step is rejected by actual TMP callback");
        string[] names = { "arrayRise", "arrayRotation", "arrayPitch", "arrayRoll", "arrayScaleStepX" };
        string[] values = { "0.05", "20", "15", "-10", "10" };
        string[] controllerNames = { "arrayRise", "arrayRotation", "arrayPitch", "arrayRoll", "arrayScaleStepX" };
        float[] expected = { .05f,20f,15f,-10f,.1f };
        for (int index = 0; index < names.Length; ++index)
        {
            TMP_InputField field = Field<TMP_InputField>(view, names[index]);
            field.text = values[index]; field.onEndEdit.Invoke(field.text);
            Require(Mathf.Abs(Field<float>(controller, controllerNames[index]) - expected[index]) < .00001f,
                "Actual TMP callback lost " + names[index]);
            Require(Field<int>(controller, "arrayPreviewCount") > 0, "Parameter broke preview: " + names[index]);
        }
        NativeClick(view, uiCamera, "ArraySymmetry"); yield return null;
        Require(Field<bool>(controller, "arraySymmetric"), "Symmetry button did not apply");
        EventSystem.current.SetSelectedGameObject(null);
        IList previews = Field<IList>(scene, "contourPreviews");
        var poses = new List<Vector3>(); var rotations = new List<Quaternion>(); var scales = new List<Vector3>();
        foreach (object preview in previews)
        {
            Transform transform = Field<GameObject>(preview, "Root").transform;
            poses.Add(transform.position); rotations.Add(transform.rotation); scales.Add(transform.localScale);
        }
        Require(poses.Count == 8 && scales[0].x > scales[1].x, "Symmetric uniform scale profile did not reach preview");
        Require(Vector3.Distance(poses[3] - poses[0], Field<Vector3>(controller,"arrayStepY")) < .001f &&
            Quaternion.Angle(rotations[3], rotations[0]) < .001f && Vector3.Distance(scales[3],scales[0]) < .001f,
            "Second row changes shape profile instead of translating it");
        Save(scene, view, uiCamera, target, Path.Combine(output, "input-array-profile.png"));
        int before = doc.Parts.Count;
        NativeClick(view, uiCamera, "ApplyArray"); yield return null;
        Require(doc.Parts.Count == before + poses.Count, "Apply lost planned Array copies");
        for (int index = 0; index < poses.Count; ++index)
        {
            BlueprintEditorPart part = doc.Parts[before + index];
            Require(Vector3.Distance(V(part.Position), poses[index]) < .001f &&
                Vector3.Distance(V(part.Scale), scales[index]) < .001f &&
                Quaternion.Angle(new Quaternion((float)part.Rotation.X,(float)part.Rotation.Y,
                    (float)part.Rotation.Z,(float)part.Rotation.W), rotations[index]) < .01f,
                "Array commit differs from visual preview " + index);
        }
        Frame(controller,input,mouse,0,KeyCode.LeftControl,KeyCode.Z); yield return null;
        Require(doc.Parts.Count == before, "Array profile is not one Undo");
        Frame(controller,input,mouse); yield return null;
        Frame(controller,input,mouse,0,KeyCode.LeftControl,KeyCode.Z); yield return null;
        Require(Math.Abs(doc.Parts[0].Scale.X - 1.3) < .00001, "Array fixture source scale did not restore");
        Frame(controller,input,mouse); yield return null;
    }

    private static Vector2 LayoutHandle(BlueprintEditorController controller, BlueprintEditorScene scene, GizmoAxis axis)
    {
        LineRenderer line = Field<LineRenderer[]>(Field<TransformGizmoView>(scene, "gizmo"),
            "layoutAxisLines")[(int)axis - 1];
        Vector3 pivot = V(controller.Document.Parts[0].Position);
        int freeSamples = 0, matchingSamples = 0;
        for (int index = 40; index >= 0; --index)
        {
            Vector2 point = scene.Camera.WorldToScreenPoint(Vector3.Lerp(line.GetPosition(0), line.GetPosition(1), index / 40f));
            bool free = scene.HitTestAnchor(Field<Vector3[]>(controller, "gizmoAnchors"), point) < 0 &&
                !scene.HitTestScale(point);
            if (free) ++freeSamples;
            bool matching = scene.HitTestGizmo(BlueprintEditorTool.Array, pivot, Quaternion.identity,
                Field<bool>(controller,"localSpace"), point, out GizmoAxis hit) == GizmoHandleKind.Layout && hit == axis;
            if (matching) ++matchingSamples;
            if (free && matching) return point;
        }
        throw new InvalidOperationException("Input frames: no golden arrow pixel outside native anchor glyphs: " +
            axis + " (free " + freeSamples + ", matching " + matchingSamples + ")");
    }

    private static void TestPrimaryGroupGizmo(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, BlueprintEditorDocument doc)
    {
        Vector3 left = V(doc.Parts[0].Position), right = V(doc.Parts[1].Position);
        const string outside = "primary-frame-outside";
        doc.AddPart(new BlueprintEditorPart(outside, "fixture", "Outside",
            new Point3(0, .5, 4), new Rotation3(0, 0, 0, 1), scale: new Point3(3, 1, 1)));
        doc.SelectOnly("left"); doc.ToggleSelection(outside);
        Require(scene.TrySync(doc, out string error), error);
        var boundsIds = new List<string> { "left", outside };
        Require(scene.TryGetBounds(boundsIds, out Bounds selectionBounds),
            "Bounds-pivot fixture has no visual bounds");
        object[] boundsArgs = { null, null, new List<string>() };
        Require((bool)controller.GetType().GetMethod("TrySelectionBasis", Private)
            .Invoke(controller, boundsArgs) &&
            Vector3.Distance((Vector3)boundsArgs[0], selectionBounds.center) < .0001f,
            "Fallback selection pivot is not the center of visible bounds");
        doc.SelectOnly("left"); doc.ToggleSelection("right");
        Require(doc.CreateGroup("primary-group", "Primary group") &&
            doc.SetPrimaryGroup("primary-group"), "Primary-group fixture setup failed");
        doc.ToggleSelection(outside);
        Require(scene.TrySync(doc, out error), error);
        view.Bind(doc, true, null, null);
        controller.GetType().GetMethod("SetActiveTool", Private)
            .Invoke(controller, new object[] { BlueprintEditorTool.Transform });
        var ids = new List<string>();
        object[] pivotArgs = { null, null, ids };
        Require((bool)controller.GetType().GetMethod("TrySelectionPivot", Private)
            .Invoke(controller, pivotArgs), "Primary-group pivot was unavailable");
        Require(ids.Contains("left") && ids.Contains("right") && ids.Contains(outside),
            "Primary-group selection did not retain its group and outside transform members");
        controller.GetType().GetMethod("UsePrimaryGizmoFrame", Private)
            .Invoke(controller, new object[] { ids });
        Require(ids.Count == 2 && !ids.Contains(outside),
            "Primary-group gizmo included a selected part outside its declared frame");
        doc.SelectOnly("primary-group");
        Require(doc.SetPrimaryPart(outside) && doc.SetGroupPivot("primary-group", "left"),
            "Independent world/group anchor fixture setup failed");
        ids.Clear();
        pivotArgs = new object[] { null, null, ids };
        Require((bool)controller.GetType().GetMethod("TrySelectionPivot", Private)
            .Invoke(controller, pivotArgs) &&
            Vector3.Distance((Vector3)pivotArgs[0], left) < .0001f && ids.Count == 2,
            "Selected group did not anchor its pivot to its own part or lost transform members");
        controller.GetType().GetMethod("UsePrimaryGizmoFrame", Private)
            .Invoke(controller, new object[] { ids });
        Require(ids.Count == 1 && ids[0] == "left",
            "Group pivot did not isolate its own gizmo frame");
        Require(controller.ApplyTransformDelta(Vector3.up, Quaternion.identity, left),
            "Primary-group transform was rejected");
        Require(Vector3.Distance(V(doc.Parts[0].Position), left + Vector3.up) < .0001f &&
            Vector3.Distance(V(doc.Parts[1].Position), right + Vector3.up) < .0001f,
            "Group-pivot gizmo did not transform the whole selected group");
        Require(doc.PrimaryPartId == outside,
            "Group transform changed the independent world anchor");
        Require(doc.CreateEmptyGroup("secondary-group", "Secondary group"),
            "Secondary group fixture setup failed");
        doc.SelectOnly(outside);
        Require(doc.SetSelectionGroup("secondary-group") &&
            doc.SetGroupPivot("secondary-group", outside) && doc.SetPrimaryPart("left"),
            "Multi-group world-anchor fixture setup failed");
        doc.SelectOnly("primary-group");
        doc.ToggleSelection("secondary-group");
        ids.Clear();
        pivotArgs = new object[] { null, null, ids };
        Require((bool)controller.GetType().GetMethod("TrySelectionPivot", Private)
            .Invoke(controller, pivotArgs) &&
            Vector3.Distance((Vector3)pivotArgs[0], left + Vector3.up) < .0001f && ids.Count == 3,
            "Last selected group pivot overrode the selected world anchor");
        controller.GetType().GetMethod("UsePrimaryGizmoFrame", Private)
            .Invoke(controller, new object[] { ids });
        Require(ids.Count == 1 && ids[0] == "left",
            "Multi-group gizmo frame did not isolate the world anchor");
        input.Next(Vector2.zero, 0, KeyCode.N);
        ids.Clear();
        pivotArgs = new object[] { null, null, ids };
        Require((bool)controller.GetType().GetMethod("TrySelectionPivot", Private)
            .Invoke(controller, pivotArgs) &&
            Vector3.Distance((Vector3)pivotArgs[0], left + Vector3.up) > .1f && ids.Count == 3,
            "N did not temporarily ignore the world anchor for the selected frame");
        controller.GetType().GetMethod("UsePrimaryGizmoFrame", Private)
            .Invoke(controller, new object[] { ids });
        Require(ids.Count == 3, "N still reduced anchors to the declared world frame");
        input.Next(Vector2.zero);
        Require(doc.Undo() && doc.Undo() && doc.Undo() && doc.Undo(),
            "Multi-group world-anchor fixture could not restore document history");
        Require(doc.Undo() && doc.Undo() && doc.Undo() && doc.Undo() && doc.Undo() && doc.Undo(),
            "Primary-group fixture could not restore document history");
        doc.SelectOnly("left");
        Require(scene.TrySync(doc, out error), error);
        view.Bind(doc, true, null, null);
    }

    private static Vector2 MoveHandle(BlueprintEditorScene scene, Vector3 pivot, GizmoAxis axis) =>
        scene.Camera.WorldToScreenPoint(Field<LineRenderer[]>(Field<TransformGizmoView>(scene, "gizmo"), "moveLines")[(int)axis - 1].GetPosition(1));

    private static GameObject VisualRoot(BlueprintEditorScene scene, string id) =>
        Field<GameObject>(Field<IDictionary>(scene, "visuals")[id], "Root");

    private static bool IsPainted(BlueprintEditorScene scene, string id)
    {
        foreach (Renderer renderer in Field<Renderer[]>(Field<IDictionary>(scene, "visuals")[id], "Renderers"))
            if (renderer.HasPropertyBlock()) return true;
        return false;
    }

    private static Vector2 ClearSurfacePoint(BlueprintEditorController controller, BlueprintEditorScene scene,
        string id, Vector2 center, Vector3 selectedPivot)
    {
        for (int y = -40; y <= 40; y += 10)
        for (int x = -40; x <= 40; x += 10)
        {
            Vector2 point = center + new Vector2(x,y);
            if (scene.TryPick(point, out string hit) && hit == id &&
                scene.HitTestAnchor(Field<Vector3[]>(controller, "gizmoAnchors"), point) < 0 &&
                scene.HitTestGizmo(BlueprintEditorTool.Transform, selectedPivot, Quaternion.identity, true,
                    point, out _) == GizmoHandleKind.None) return point;
        }
        throw new InvalidOperationException("Input frames: inserted mesh has no selectable surface pixel outside the visible gizmo");
    }

    internal static void NativeClick(BlueprintEditorView view, Camera uiCamera, string name, bool verifyRaycast = true)
    {
        Button expected = null;
        foreach (Button button in view.RootCanvas.GetComponentsInChildren<Button>())
            if (button.name == name) { expected = button; break; }
        Require(expected, "Visible button missing: " + name);
        NativeClick(view, uiCamera, expected, verifyRaycast);
    }

    private static string VisibleButtonText(BlueprintEditorView view, string name)
    {
        foreach (Button button in view.RootCanvas.GetComponentsInChildren<Button>())
            if (button.name == name) return button.GetComponentInChildren<TMP_Text>().text;
        throw new InvalidOperationException("Visible button missing: " + name);
    }

    private static void NativeClick(BlueprintEditorView view, Camera uiCamera, Button expected, bool verifyRaycast = true)
    {
        Canvas.ForceUpdateCanvases();
        string name = expected.name;
        var rect = (RectTransform)expected.transform;
        Vector2 position = RectTransformUtility.WorldToScreenPoint(uiCamera, rect.TransformPoint(rect.rect.center));
        var pointer = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
        GameObject target = expected.gameObject;
        if (verifyRaycast)
        {
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0, "No actual UI raycast at " + name);
            target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Require(target == expected.gameObject, "UI raycast intercepted " + name + " by " + (target ? target.name : "null") +
                "; first=" + hits[0].gameObject.name + "; position=" + position);
        }
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        EventSystem.current.SetSelectedGameObject(null);
    }

    private static void Save(BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string path)
    {
        foreach (Transform child in view.RootCanvas.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases(); scene.Camera.Render(); uiCamera.Render();
        RenderTexture previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0,0,target.width,target.height), 0,0); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG()); Object.Destroy(texture); RenderTexture.active = previous;
    }
}
