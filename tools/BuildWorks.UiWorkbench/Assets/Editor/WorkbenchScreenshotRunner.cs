using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OstrixMods.BuildWorks.UiWorkbench;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class WorkbenchScreenshotRunner
{
    private static readonly List<string> Expected = new List<string>();

    public static void CaptureAll()
    {
        try
        {
            Expected.Clear();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BlueprintEditorWorkbench app = BlueprintEditorWorkbench.Create();
            int[,] sizes = { { 1920, 1080 }, { 2560, 1440 }, { 3440, 1440 } };
            Require(app.SelectablePartCount == app.TreePartCount,
                "Every selectable viewport part must exist in the object tree");
            Require(app.VanillaPreviewPartCount == app.SelectablePartCount,
                "The sample blueprint must use imported vanilla Valheim prefabs");
            Require(app.TryPickPartAtWorldPoint(new Vector3(-1f, 2f, 0f),
                    out string picked) && picked == "Балка_верхняя",
                "Viewport physics picking did not resolve the expected mesh");

            int initialPartCount = app.DocumentPartCount;
            Vector3 initialPosition;
            Quaternion initialRotation;
            Vector3 initialScale;
            Require(app.SelectPart("Створка_левая"),
                "Initial document part could not be selected");
            Require(app.TryGetPartPose("Створка_левая", out initialPosition,
                    out initialRotation, out initialScale),
                "Initial document pose could not be read");
            Vector3 editedPosition;
            Quaternion editedRotation;
            Vector3 editedScale;
            Require(app.ApplySelectedTransform(
                    new Vector3(0.4f, 0.1f, 0f), Quaternion.Euler(0f, 15f, 0f), 1.25f) &&
                app.DocumentIsDirty,
                "Document transform was not committed");
            Require(app.TryGetPartPose("Створка_левая", out editedPosition,
                    out editedRotation, out editedScale) &&
                Vector3.Distance(initialPosition, editedPosition) > 0.1f &&
                Quaternion.Angle(initialRotation, editedRotation) > 10f &&
                Vector3.Distance(editedScale, Vector3.one * 1.25f) < 0.001f,
                "Committed document pose is incorrect");
            Require(app.UndoDocument() &&
                app.TryGetPartPose("Створка_левая", out Vector3 undonePosition,
                    out Quaternion undoneRotation, out Vector3 undoneScale) &&
                Vector3.Distance(initialPosition, undonePosition) < 0.001f &&
                Quaternion.Angle(initialRotation, undoneRotation) < 0.01f &&
                Vector3.Distance(initialScale, undoneScale) < 0.001f,
                "Undo did not restore the original part pose");
            Require(app.RedoDocument(), "Redo did not restore the edited part pose");

            int initialGroupCount = app.DocumentGroupCount;
            Require(app.SelectPart("Створка_левая") &&
                app.SelectPart("Створка_правая", true) &&
                app.CreateGroupFromSelection(),
                "Nested group could not be created from sibling parts");
            string nestedGroupId = app.ActivePartId;
            Require(app.DocumentGroupCount == initialGroupCount + 1 &&
                app.TryGetGroupParent(nestedGroupId, out string nestedParent) &&
                nestedParent == "group-gate" && app.HasTreeRow(nestedGroupId) &&
                app.HasTransformGizmo,
                "Nested group did not select its descendant parts");
            Require(app.RenameNode(nestedGroupId, "СТВОРКИ") &&
                app.ToggleNodeVisibility(nestedGroupId) && !app.HasTransformGizmo &&
                app.ToggleNodeVisibility(nestedGroupId) && app.HasTransformGizmo &&
                app.ToggleNodeLock(nestedGroupId) && !app.HasTransformGizmo &&
                app.ToggleNodeLock(nestedGroupId) && app.HasTransformGizmo,
                "Nested group visibility, lock, or rename is not operational");
            Require(app.MoveSelectionToGroup(null) &&
                app.TryGetGroupParent(nestedGroupId, out nestedParent) && nestedParent == null &&
                app.UndoDocument() &&
                app.TryGetGroupParent(nestedGroupId, out nestedParent) &&
                nestedParent == "group-gate",
                "Nested group move or Undo is inconsistent");
            Require(app.SelectNode(nestedGroupId) && app.UngroupSelection() &&
                app.DocumentGroupCount == initialGroupCount && app.UndoDocument() &&
                app.DocumentGroupCount == initialGroupCount + 1,
                "Nested group ungroup or Undo is inconsistent");

            Require(app.AddCatalogPart(1) && app.DocumentPartCount == initialPartCount + 1 &&
                app.TreePartCount == app.DocumentPartCount,
                "Catalog add did not update the document and object tree");
            string catalogPartId = app.ActivePartId;
            Require(app.UndoDocument() && app.DocumentPartCount == initialPartCount,
                "Undo did not remove the added catalog part");
            Require(app.RedoDocument() && app.DocumentPartCount == initialPartCount + 1,
                "Redo did not restore the added catalog part");
            Require(app.DuplicateSelection() && app.DocumentPartCount == initialPartCount + 2,
                "Duplicate did not add one document part");
            Require(app.DeleteSelection() && app.DocumentPartCount == initialPartCount + 1,
                "Delete did not remove the duplicated part");
            Require(app.UndoDocument() && app.DocumentPartCount == initialPartCount + 2 &&
                app.UndoDocument() && app.DocumentPartCount == initialPartCount + 1,
                "Undo did not restore delete and duplicate atomically");

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string repositoryRoot = Directory.GetParent(
                Directory.GetParent(projectRoot).FullName).FullName;
            string documentPath = Path.Combine(repositoryRoot, "artifacts", "ui-workbench",
                "workbench-blueprint.json");
            Require(app.SaveDocument(documentPath) && !app.DocumentIsDirty,
                "Workbench document was not marked clean after save");
            Require(app.SelectPart("Створка_левая") && app.DuplicateSelection() &&
                app.DocumentIsDirty, "Post-save mutation did not dirty the document");
            Vector3 reopenedPosition;
            Quaternion reopenedRotation;
            Vector3 reopenedScale;
            Require(app.ReopenDocument(documentPath) && !app.DocumentIsDirty &&
                app.DocumentPartCount == initialPartCount + 1 &&
                app.DocumentGroupCount == initialGroupCount + 1 &&
                app.TryGetGroupParent(nestedGroupId, out nestedParent) &&
                nestedParent == "group-gate" &&
                app.TryGetPartPose("Створка_левая", out reopenedPosition,
                    out reopenedRotation, out reopenedScale) &&
                Vector3.Distance(editedPosition, reopenedPosition) < 0.001f &&
                Quaternion.Angle(editedRotation, reopenedRotation) < 0.01f &&
                Vector3.Distance(editedScale, reopenedScale) < 0.001f,
                "Saved document did not reopen with the committed pose");

            Vector3 faultPosition = default;
            Quaternion faultRotation = default;
            Vector3 faultScale = default;
            Require(app.SelectPart("Створка_левая") &&
                app.TryGetPartPose("Створка_левая", out faultPosition,
                    out faultRotation, out faultScale),
                "Fault test could not read the baseline pose");
            InjectSceneSyncFailure(app);
            Require(!app.ApplySelectedTransform(new Vector3(1f, 0f, 0f),
                    Quaternion.identity, 1f) && !app.DocumentIsDirty &&
                app.TryGetPartPose("Створка_левая", out Vector3 rolledBackPosition,
                    out Quaternion rolledBackRotation, out Vector3 rolledBackScale) &&
                Vector3.Distance(faultPosition, rolledBackPosition) < 0.001f &&
                Quaternion.Angle(faultRotation, rolledBackRotation) < 0.01f &&
                Vector3.Distance(faultScale, rolledBackScale) < 0.001f,
                "A failed scene sync did not roll back its document edit");

            Vector3 historyPosition = default;
            Require(app.ApplySelectedTransform(new Vector3(0.2f, 0f, 0f),
                    Quaternion.identity, 1f) && app.DocumentIsDirty &&
                app.TryGetPartPose("Створка_левая", out historyPosition,
                    out _, out _), "History fault test could not create an edit");
            InjectSceneSyncFailure(app);
            Require(!app.UndoDocument() && app.DocumentIsDirty &&
                app.TryGetPartPose("Створка_левая", out Vector3 restoredHistoryPosition,
                    out _, out _) &&
                Vector3.Distance(historyPosition, restoredHistoryPosition) < 0.001f,
                "A failed history sync did not restore the pre-Undo document");
            Require(app.ReopenDocument(documentPath),
                "Document did not recover after the history fault test");

            Require(app.SelectPart("Створка_левая") && app.DuplicateSelection(),
                "Reopen fault test could not dirty the current document");
            int unsavedPartCount = app.DocumentPartCount;
            InjectSceneSyncFailure(app);
            Require(!app.ReopenDocument(documentPath) && app.DocumentIsDirty &&
                app.DocumentPartCount == unsavedPartCount,
                "A failed reopen replaced the unsaved document");
            Require(app.ReopenDocument(documentPath),
                "Saved document did not reopen after rollback");

            string collisionPath = documentPath + ".collision.json";
            string collisionJson = File.ReadAllText(documentPath).Replace(
                "\"prefabName\": \"woodwall\"",
                "\"prefabName\": \"catalog-piece-002\"");
            Require(collisionJson != File.ReadAllText(documentPath),
                "Cross-document prefab collision fixture was not created");
            File.WriteAllText(collisionPath, collisionJson);
            Require(app.ReopenDocument(collisionPath) &&
                VisualPrefab(app, "Створка_левая") == "catalog-piece-002",
                "A reused stable ID kept the previous visual prefab");
            File.Delete(collisionPath);
            Require(app.ReopenDocument(documentPath),
                "Original document did not reopen after collision test");
            Require(app.SelectNode(catalogPartId) && app.DeleteSelection() &&
                app.DocumentPartCount == initialPartCount && app.SaveDocument(documentPath),
                "Catalog test part could not be removed before visual captures");

            app.ClearSelection();
            app.SetTool(WorkbenchTool.Select);
            Require(app.SetHoveredPart("Балка_верхняя") && app.ActiveTool == WorkbenchTool.Select &&
                app.SelectedCount == 0 && app.HoveredPartName == "Балка_верхняя" &&
                !app.HasTransformGizmo, "Select state is inconsistent");
            CaptureState(app, sizes, "select");

            Require(app.SelectPart("Створка_левая") &&
                app.ActiveTool == WorkbenchTool.Transform && app.SelectedCount == 1 &&
                app.HoveredPartName == null && app.HasTransformGizmo,
                "Single selection must enter transform with an active gizmo");
            app.SetTransformSpace(false);
            Require(!app.UsesLocalSpace && app.HasTransformGizmo,
                "World-space transform did not rebuild the gizmo");
            app.SetTransformSpace(true);
            Require(app.UsesLocalSpace && app.HasTransformGizmo,
                "Local-space transform did not rebuild the gizmo");
            CaptureState(app, sizes, "transform");

            app.SetTool(WorkbenchTool.Array);
            int beforeArray = app.DocumentPartCount;
            Require(app.ActiveTool == WorkbenchTool.Array && app.SelectedCount == 1 &&
                app.ArrayPreviewCount == 5, "Array dimensions include source; preview must contain five copies");
            CaptureState(app, sizes, "array");
            Require(app.ApplyArray() && app.ActiveTool == WorkbenchTool.Transform &&
                app.DocumentPartCount == beforeArray + 5,
                "Array did not commit its preview");
            Require(app.UndoDocument() && app.DocumentPartCount == beforeArray,
                "Array was not one atomic Undo operation");

            Require(app.SelectPart("Балка_верхняя"),
                "Contour source could not be selected");
            app.SetTool(WorkbenchTool.Contour);
            int beforeContour = app.DocumentPartCount;
            Require(app.SelectContourSupport("Створка_правая"),
                "Contour support chain could not be selected: " + app.CurrentStatus);
            int contourCopies = app.ContourPreviewCount;
            Require(app.ActiveTool == WorkbenchTool.Contour && app.SelectedCount == 1 &&
                contourCopies >= 2, "Contour preview is inconsistent: " + app.CurrentStatus);
            CaptureState(app, sizes, "contour");
            Require(app.ApplyContour() && app.ActiveTool == WorkbenchTool.Transform &&
                app.DocumentPartCount == beforeContour + contourCopies,
                "Contour did not commit its preview");
            Require(app.UndoDocument() && app.DocumentPartCount == beforeContour,
                "Contour was not one atomic Undo operation");

            app.OpenCatalog();
            Require(!app.HasTransformGizmo,
                "Catalog overlay must hide the transform gizmo.");
            for (int i = 0; i < sizes.GetLength(0); ++i)
                Capture(app, "catalog-" + sizes[i, 0] + "x" + sizes[i, 1],
                    sizes[i, 0], sizes[i, 1]);

            foreach (string path in Expected)
            {
                if (!File.Exists(path) || new FileInfo(path).Length < 10000)
                    throw new InvalidOperationException("Invalid screenshot: " + path);
            }
            Debug.Log("BUILDWORKS_UI_WORKBENCH_OK " + Expected.Count + " screenshots");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CaptureState(BlueprintEditorWorkbench app, int[,] sizes, string prefix)
    {
        for (int i = 0; i < sizes.GetLength(0); ++i)
            Capture(app, prefix + "-" + sizes[i, 0] + "x" + sizes[i, 1],
                sizes[i, 0], sizes[i, 1]);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void InjectSceneSyncFailure(BlueprintEditorWorkbench app)
    {
        FieldInfo field = typeof(BlueprintEditorWorkbench).GetField(
            "failNextSceneSyncForTests", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, "Scene sync fault hook is unavailable");
        field.SetValue(app, true);
    }

    private static string VisualPrefab(BlueprintEditorWorkbench app, string stableId)
    {
        FieldInfo partsField = typeof(BlueprintEditorWorkbench).GetField(
            "parts", BindingFlags.Instance | BindingFlags.NonPublic);
        var visuals = (Dictionary<string, GameObject>)partsField.GetValue(app);
        Require(visuals.TryGetValue(stableId, out GameObject visual),
            "Document visual is missing: " + stableId);
        Component marker = visual.GetComponent("WorkbenchPartMarker");
        Require(marker != null, "Document visual marker is missing: " + stableId);
        FieldInfo prefabField = marker.GetType().GetField(
            "PrefabName", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(prefabField != null, "Document visual prefab marker is unavailable");
        return (string)prefabField.GetValue(marker);
    }

    private static void Capture(BlueprintEditorWorkbench app, string name, int width, int height)
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string repositoryRoot = Directory.GetParent(Directory.GetParent(projectRoot).FullName).FullName;
        string output = Path.Combine(repositoryRoot, "artifacts", "ui-workbench");
        Directory.CreateDirectory(output);
        string path = Path.Combine(output, name + ".png");

        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            app.ViewCamera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            ValidateLayout(app, width, height);
            app.ViewCamera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply(false);
            ValidatePixels(texture, name);
            byte[] png = texture.EncodeToPNG();
            File.WriteAllBytes(path, png);
            ValidatePng(png, width, height, name);
            Expected.Add(path);
            Debug.Log("Captured " + path);
        }
        finally
        {
            app.ViewCamera.targetTexture = null;
            RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private static void ValidateLayout(BlueprintEditorWorkbench app, int width, int height)
    {
        if (app.ViewCamera.targetTexture == null || app.ViewCamera.targetTexture.width != width ||
            app.ViewCamera.targetTexture.height != height)
            throw new InvalidOperationException("Camera target does not match requested dimensions");

        RectTransform root = Find(app.transform, "Canvas/SafeArea");
        RectTransform top = Find(root, "Top");
        RectTransform rail = Find(root, "ToolRail");
        RectTransform right = Find(root, "RightColumn");
        RectTransform status = Find(root, "Status");
        RectTransform outliner = Find(right, "Outliner");
        RectTransform inspector = Find(right, "Inspector");

        EnsureInside(root, top);
        EnsureInside(root, rail);
        EnsureInside(root, right);
        EnsureInside(root, status);
        EnsureInside(right, outliner);
        EnsureInside(right, inspector);
        EnsureDisjoint(top, rail);
        EnsureDisjoint(top, right);
        EnsureDisjoint(top, status);
        EnsureDisjoint(rail, right);
        EnsureDisjoint(rail, status);
        EnsureDisjoint(right, status);
        EnsureDisjoint(outliner, inspector);

        Rect viewport = Rect.MinMaxRect(BoundsIn(root, rail).xMax, BoundsIn(root, status).yMax,
            BoundsIn(root, right).xMin, BoundsIn(root, top).yMin);
        if (viewport.width < 1200f || viewport.height < 600f)
            throw new InvalidOperationException("Viewport is below the minimum usable size");

        RectTransform inspectorBody = Find(inspector, "InspectorBody");
        for (int i = 0; i < inspectorBody.childCount; ++i)
            EnsureInside(inspectorBody, (RectTransform)inspectorBody.GetChild(i));

        RectTransform overlay = Find(root, "CatalogOverlay");
        RectTransform catalog = Find(overlay, "Catalog");
        EnsureInside(root, overlay);
        EnsureInside(overlay, catalog);
        if (!overlay.gameObject.activeInHierarchy)
            return;

        for (int i = 0; i < catalog.childCount; ++i)
            EnsureInside(catalog, (RectTransform)catalog.GetChild(i));

        RectTransform grid = Find(catalog, "Grid");
        LayoutRebuilder.ForceRebuildLayoutImmediate(grid);
        if (grid.childCount != 48)
            throw new InvalidOperationException("Catalog must contain exactly 48 cells");
        GridLayoutGroup layout = grid.GetComponent<GridLayoutGroup>();
        if (layout == null || layout.constraintCount != 12)
            throw new InvalidOperationException("Catalog must use exactly 12 columns");
        var columns = new HashSet<int>();
        var rows = new HashSet<int>();
        for (int i = 0; i < grid.childCount; ++i)
        {
            RectTransform card = (RectTransform)grid.GetChild(i);
            EnsureInside(grid, card);
            Rect bounds = BoundsIn(grid, card);
            columns.Add(Mathf.RoundToInt(bounds.center.x));
            rows.Add(Mathf.RoundToInt(bounds.center.y));
        }
        if (columns.Count != 12 || rows.Count != 4)
            throw new InvalidOperationException("Catalog cells must form exactly 12 columns and 4 rows");
    }

    private static void ValidatePixels(Texture2D texture, string name)
    {
        Color32[] pixels = texture.GetPixels32();
        int step = Mathf.Max(1, pixels.Length / 4096);
        int darkest = 255;
        int brightest = 0;
        for (int i = 0; i < pixels.Length; i += step)
        {
            int value = (pixels[i].r + pixels[i].g + pixels[i].b) / 3;
            darkest = Mathf.Min(darkest, value);
            brightest = Mathf.Max(brightest, value);
        }
        if (brightest - darkest < 16)
            throw new InvalidOperationException("Screenshot is blank or near-uniform: " + name);
    }

    private static void ValidatePng(byte[] png, int width, int height, string name)
    {
        var decoded = new Texture2D(2, 2, TextureFormat.RGB24, false);
        try
        {
            if (!decoded.LoadImage(png) || decoded.width != width || decoded.height != height)
                throw new InvalidOperationException("PNG decode or dimensions failed: " + name);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(decoded);
        }
    }

    private static RectTransform Find(Transform root, string path)
    {
        Transform child = root.Find(path);
        if (child == null)
            throw new InvalidOperationException("Missing UI node: " + path);
        return (RectTransform)child;
    }

    private static void EnsureInside(RectTransform parent, RectTransform child)
    {
        Rect outer = BoundsIn(parent, parent);
        Rect inner = BoundsIn(parent, child);
        const float tolerance = 0.5f;
        if (inner.xMin < outer.xMin - tolerance || inner.yMin < outer.yMin - tolerance ||
            inner.xMax > outer.xMax + tolerance || inner.yMax > outer.yMax + tolerance)
            throw new InvalidOperationException(child.name + " leaves " + parent.name);
    }

    private static void EnsureDisjoint(RectTransform first, RectTransform second)
    {
        RectTransform root = first.GetComponentInParent<Canvas>().GetComponent<RectTransform>();
        Rect a = BoundsIn(root, first);
        Rect b = BoundsIn(root, second);
        float width = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float height = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        if (width > 0.5f && height > 0.5f)
            throw new InvalidOperationException(first.name + " overlaps " + second.name);
    }

    private static Rect BoundsIn(RectTransform root, RectTransform item)
    {
        var corners = new Vector3[4];
        item.GetWorldCorners(corners);
        Vector3 min = root.InverseTransformPoint(corners[0]);
        Vector3 max = min;
        for (int i = 1; i < corners.Length; ++i)
        {
            Vector3 point = root.InverseTransformPoint(corners[i]);
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
