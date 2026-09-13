using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Exercises the actual product UI. The central runtime fixture owns resolution
// changes, screenshots and physical pointer/chord routing through the controller.
internal static class UICatalogAcceptance
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    private sealed class InputState : IBlueprintEditorInput
    {
        internal readonly HashSet<KeyCode> Keys = new HashSet<KeyCode>();
        internal readonly HashSet<int> MouseButtons = new HashSet<int>();
        internal Vector2 Pointer;
        public Vector2 MousePosition => Pointer;
        public Vector2 MouseScrollDelta => Vector2.zero;
        public float UnscaledDeltaTime => 1f / 60f;
        public bool GetKey(KeyCode key) => Keys.Contains(key);
        public bool GetKeyDown(KeyCode key) => Keys.Contains(key);
        public bool GetMouseButton(int button) => MouseButtons.Contains(button);
        public bool GetMouseButtonDown(int button) => false;
        public bool GetMouseButtonUp(int button) => false;
        public float GetAxis(string axis) => 0f;
    }

    internal static string Run(TMP_FontAsset font)
    {
        Require(
            BuildWorksLocalization.Token("editor.view.title") ==
                "$buildworks_editor_view_title",
            "Valheim runtime localization token contains a dotted separator");
        var templateObject = new GameObject("CatalogAcceptanceFont", typeof(RectTransform));
        templateObject.SetActive(false);
        TMP_Text template = templateObject.AddComponent<TextMeshProUGUI>();
        template.font = font;
        var input = new InputState();
        using (var view = new BlueprintEditorView(template, input))
        {
            try
            {
                var document = new BlueprintEditorDocument(null, "UI fixture", "Декор", new[]
                {
                    new BlueprintEditorPart("a", "wall", "Стена", new Point3(0,0,0),
                        new Rotation3(0,0,0,1), "g"),
                    new BlueprintEditorPart("b", "wall", "Перегородка", new Point3(1,0,0), new Rotation3(0,0,0,1))
                }, new[] { new BlueprintEditorGroup("g", "Узел") });
                document.SelectOnly("a");
                view.Bind(document, true, null, Vector3.one);
                view.SetUiScale(1.4f);
                Canvas.ForceUpdateCanvases();
                RectTransform outliner = Field<RectTransform>(view, "outliner");
                RectTransform rows = Field<RectTransform>(view, "outlinerRowViewport");
                Require(rows.offsetMin.x == 4f && rows.offsetMax.x == -4f,
                    "Outliner rows still cover the left/right frame");
                EnsureInside(outliner, rows);
                int newGroups = 0;
                view.CreateGroupRequested += () => ++newGroups;
                Click(Button(view, "NewGroup"));
                Require(newGroups == 1, "Empty group toolbar does not dispatch");
                bool shift = false, control = false;
                view.NodeClicked += (_, ctrl, range) => { control = ctrl; shift = range; };
                input.Keys.Add(KeyCode.LeftShift);
                Click(Button(view, "Row_b"));
                Require(shift && !control, "Outliner Shift click does not use shared editor input");
                input.Keys.Clear();
                input.Keys.Add(KeyCode.LeftControl);
                Click(Button(view, "Row_b"));
                Require(control && !shift, "Outliner Ctrl click does not use shared editor input");
                input.Keys.Clear();
                string primaryPart = null;
                string pivotGroup = null, pivotPart = null;
                int deleteRequests = 0;
                view.PrimaryPartRequested += id => primaryPart = id;
                view.GroupPivotRequested += (group, part) =>
                    { pivotGroup = group; pivotPart = part; };
                view.DeleteSelectionRequested += () => ++deleteRequests;
                Button rowA = Button(view, "Row_a");
                input.MouseButtons.Add(1);
                PointerEventData marking = BeginRightHold(rowA.gameObject);
                RectTransform markingMenu = Field<RectTransform>(view, "outlinerContextMenu");
                Require(markingMenu.gameObject.activeSelf && rows.gameObject.activeSelf &&
                    view.HasOutlinerMenu && view.HasOutlinerContextMenu,
                    "Held RMB menu hides the Outliner or is not reported as active context UI");
                Canvas.ForceUpdateCanvases();
                Require(Vector2.Distance(marking.position,
                    RectTransformUtility.WorldToScreenPoint(null, markingMenu.position)) < 1f,
                    "Outliner marking menu did not open at the RMB press point");
                var contextLabels = new HashSet<string>();
                Button primaryAction = null;
                foreach (Button action in markingMenu.GetComponentsInChildren<Button>(true))
                {
                    string label = action.GetComponentInChildren<TMP_Text>().text;
                    contextLabels.Add(label);
                    if (label == BuildWorksLocalization.Text("editor.view.blueprint_frame"))
                        primaryAction = action;
                }
                foreach (string label in new[] {
                    BuildWorksLocalization.Text("editor.view.rename"),
                    BuildWorksLocalization.Text("editor.view.show_hide"),
                    BuildWorksLocalization.Text("editor.view.lock_unlock"),
                    BuildWorksLocalization.Text("editor.view.group"),
                    BuildWorksLocalization.Text("editor.view.ungroup"),
                    BuildWorksLocalization.Text("editor.view.duplicate"),
                    BuildWorksLocalization.Text("editor.view.delete"),
                    BuildWorksLocalization.Text("editor.view.to_root"),
                    BuildWorksLocalization.Text("editor.view.blueprint_frame"),
                    BuildWorksLocalization.Text("editor.view.group_pivot", "Узел") })
                    Require(contextLabels.Contains(label), "Outliner marking action is missing: " + label);
                Require(primaryAction, "Outliner primary-part marking action is missing");
                marking.position = Center((RectTransform)primaryAction.transform);
                ExecuteEvents.Execute(rowA.gameObject, marking, ExecuteEvents.pointerUpHandler);
                Require(primaryPart == null && markingMenu.gameObject.activeSelf,
                    "Selected-row RMB menu closed while the physical button was still held");
                input.Pointer = marking.position;
                input.MouseButtons.Clear();
                view.Tick();
                Require(primaryPart == "a" && !markingMenu.gameObject.activeSelf && rows.gameObject.activeSelf,
                    "RMB release did not execute the hovered action and restore the tree");
                marking = BeginRightHold(rowA.gameObject);
                Button groupPivotAction = null;
                foreach (Button action in markingMenu.GetComponentsInChildren<Button>(true))
                    if (action.GetComponentInChildren<TMP_Text>().text ==
                        BuildWorksLocalization.Text("editor.view.group_pivot", "Узел"))
                        groupPivotAction = action;
                Require(groupPivotAction, "Outliner group-pivot marking action is missing");
                marking.position = Center((RectTransform)groupPivotAction.transform);
                ExecuteEvents.Execute(rowA.gameObject, marking, ExecuteEvents.pointerUpHandler);
                Require(pivotGroup == "g" && pivotPart == "a" && !markingMenu.gameObject.activeSelf,
                    "RMB release did not dispatch the scoped group pivot");
                marking = BeginRightHold(rowA.gameObject);
                markingMenu.sizeDelta = new Vector2(markingMenu.sizeDelta.x, 96f);
                Canvas.ForceUpdateCanvases();
                Button clippedDelete = null;
                foreach (Button action in markingMenu.GetComponentsInChildren<Button>(true))
                    if (action.GetComponentInChildren<TMP_Text>().text ==
                        BuildWorksLocalization.Text("editor.view.delete")) clippedDelete = action;
                Require(clippedDelete, "Clipped context delete action is missing");
                marking.position = Center((RectTransform)clippedDelete.transform);
                Require(!RectTransformUtility.RectangleContainsScreenPoint(
                    Field<RectTransform>(view, "outlinerContextViewport"), marking.position, null),
                    "Clipped-action fixture did not place release outside the context viewport");
                ExecuteEvents.Execute(rowA.gameObject, marking, ExecuteEvents.pointerUpHandler);
                Require(deleteRequests == 0 && !markingMenu.gameObject.activeSelf,
                    "Release outside the clipped context viewport executed a hidden command");
                marking = BeginRightHold(rowA.gameObject);
                RectTransform safeRoot = Field<RectTransform>(view, "safeRoot");
                markingMenu.sizeDelta = new Vector2(markingMenu.sizeDelta.x,
                    Mathf.Max(96f, safeRoot.rect.height - 32f));
                Vector2 safeCenter = RectTransformUtility.WorldToScreenPoint(null, safeRoot.position);
                typeof(BlueprintEditorView).GetMethod("PositionOutlinerContext", Fields)
                    .Invoke(view, new object[] { safeCenter });
                Canvas.ForceUpdateCanvases();
                EnsureInside(safeRoot, markingMenu);
                view.HideOutlinerMenu();
                input.MouseButtons.Add(1);
                marking = BeginRightHold(rowA.gameObject);
                input.Pointer = marking.position;
                view.Bind(document, true, null, Vector3.one);
                input.MouseButtons.Clear();
                view.Tick();
                Require(!markingMenu.gameObject.activeSelf && rows.gameObject.activeSelf,
                    "RMB release was lost after the pressed Outliner row was rebuilt");
                Click(Button(view, "OutlinerMenuButton"));
                Require(view.HasOutlinerMenu && !rows.gameObject.activeSelf,
                    "Outliner drawer does not replace its own rows");
                EnsureInside(outliner, (RectTransform)Field<GameObject>(view, "outlinerMenu").transform);
                view.HideOutlinerMenu();
                Require(rows.gameObject.activeSelf, "Closing Outliner drawer does not restore rows");
                Click(Button(view, "RenameSelection"));
                foreach (double scale in new[] { 0.5, 2.0 })
                {
                    document.SetPartProperties("a", "Стена", new Point3(0,0,0),
                        new Rotation3(0,0,0,1), new Point3(scale, scale, scale));
                    view.Bind(document, true, null, Vector3.one);
                    string expectedScaleLine = BuildWorksLocalization.Text(
                        "editor.view.part_info", string.Empty,
                        (scale * 100).ToString("0") + "%", string.Empty, string.Empty)
                        .Split('\n')[1];
                    Require(Field<TMP_Text>(view, "detailInfo").text.Contains(expectedScaleLine),
                        "Detail inspector reports constant or incorrect uniform scale");
                }
                view.SetTool(BlueprintEditorTool.Select);

                var catalog = new List<BlueprintEditorCatalogItem>();
                for (int index = 0; index < 96; ++index)
                    catalog.Add(new BlueprintEditorCatalogItem("piece" + index, "Деталь " + index,
                        null, "Стены", "Материал " + (index % 36).ToString("00"),
                        "Источник " + index % 2, index));
                var roof = new CompositeBlueprintStore.Blueprint { id = "roof", name = "Крыша", category = "Крыши" };
                var decor = new CompositeBlueprintStore.Blueprint { id = "decor", name = "Перегородка", category = "Декор" };
                catalog.Add(new BlueprintEditorCatalogItem("blueprint-roof", roof.name, null,
                    roof.category, "Чертёж", "Мои чертежи", 96, roof));
                catalog.Add(new BlueprintEditorCatalogItem("blueprint-decor", decor.name, null,
                    decor.category, "Чертёж", "Мои чертежи", 97, decor));
                BlueprintEditorCatalogItem selected = null;
                view.CatalogPartSelected += (item, _) => selected = item;
                view.ShowCatalog(catalog);
                Canvas.ForceUpdateCanvases();
                Require(Field<RectTransform>(view, "catalogGrid").GetComponentsInChildren<Button>().Length == 48,
                    "Part mode no longer uses a full 48-card page");
                Require(Field<TMP_Text>(view, "catalogBreadcrumb").text.Contains("96"),
                    "Catalog count includes blueprints in part mode");
                Require(Button(view, "Catalog_piece0").GetComponentInChildren<TMP_Text>().text == "#1",
                    "Native part card lost its index label");
                Require(Field<RectTransform>(view, "catalogCategories").GetComponent<GridLayoutGroup>().constraintCount == 2,
                    "Materials remain a slow single-column list");
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
                input.Keys.Add(KeyCode.PageDown); view.Tick(); input.Keys.Clear();
                Require(Field<TMP_Text>(view, "catalogPageText").text == "2 / 2", "PageDown does not advance card page");
                TestCatalogNavigation(view, input);
                TMP_InputField jump = Field<TMP_InputField>(view, "catalogPageInput");
                jump.SetTextWithoutNotify("1"); jump.onEndEdit.Invoke("1");
                Require(Field<TMP_Text>(view, "catalogPageText").text == "1 / 2", "Page number jump does not work");
                Click(Button(view, "MaterialsPageDown"));
                Click(Button(view, "MaterialsPageDown"));
                Require(Field<ScrollRect>(view, "catalogMaterialScroll").verticalNormalizedPosition <= 0.001f,
                    "Material page buttons do not reach the last material");
                Canvas.ForceUpdateCanvases();
                Button lastMaterial = Button(view, "Material_Материал 35");
                EnsureInside(Field<ScrollRect>(view, "catalogMaterialScroll").viewport,
                    (RectTransform)lastMaterial.transform);
                Click(lastMaterial);
                Require(Field<TMP_Text>(view, "catalogBreadcrumb").text.Contains("Материал 35"),
                    "Last material does not filter or update the breadcrumb");
                Require(Field<RectTransform>(view, "catalogGrid").GetComponentsInChildren<Button>().Length == 2,
                    "Material filter does not match actual item metadata");
                Click(Button(view, "CatalogBlueprintsMode"));
                Require(Button(view, "Category_Крыши").gameObject.activeInHierarchy &&
                    Button(view, "Category_Декор").gameObject.activeInHierarchy,
                    "Blueprint user subcategories are missing");
                Click(Button(view, "Category_Декор"));
                Button blueprintCard = Button(view, "Catalog_blueprint-decor");
                TMP_Text blueprintLabel = blueprintCard.GetComponentInChildren<TMP_Text>();
                blueprintLabel.ForceMeshUpdate();
                Require(blueprintLabel.text == decor.name && blueprintLabel.maxVisibleLines == 2 &&
                    !blueprintLabel.isTextOverflowing && blueprintLabel.gameObject.activeInHierarchy,
                    "Blueprint card does not visibly show its name without hovering");
                Require(((RectTransform)blueprintLabel.transform).anchoredPosition.y <= -66f,
                    "Blueprint name overlaps the thumbnail area");
                EnsureInside((RectTransform)blueprintCard.transform, (RectTransform)blueprintLabel.transform);
                Click(blueprintCard);
                Require(selected != null && ReferenceEquals(selected.Blueprint, decor),
                    "Blueprint catalog selection loses the whole blueprint payload");
                view.HideCatalog();
                float points = 0f, uniform = 0f;
                bool grid = false;
                view.ViewportSettingsChanged += (_, __, ___, pointSize, uniformSize, showGrid) =>
                { points = pointSize; uniform = uniformSize; grid = showGrid; };
                Click(Button(view, "View"));
                Require(view.HasViewportSettings, "View button does not open viewport options");
                EnsureInside(view.Viewport, (RectTransform)Field<GameObject>(view, "viewportSettings").transform);
                TMP_InputField[] sizes = Field<TMP_InputField[]>(view, "viewportScaleInputs");
                sizes[3].SetTextWithoutNotify("250"); sizes[3].onEndEdit.Invoke("250");
                sizes[4].SetTextWithoutNotify("50"); sizes[4].onEndEdit.Invoke("50");
                Require(points == 2.5f && uniform == 0.5f && grid,
                    "Point and uniform handle sizes do not have independent settings");
                Click(Button(view, "ViewportSettingsReset"));
                Require(points == 1f && uniform == 1f && grid, "Viewport reset does not restore defaults");
                TestNumericDefaults(view, input);
                view.HideViewportSettings();
                TestOutlinerDragDrop(view);
                return "Actual UI: card-only wheel/QE/typing guard, materials retain native scroll, numeric RMB defaults and uniform step/free scrub, live Array scrub and shared step controls, Outliner row RMB tree-only context with blueprint/group anchor actions, native ProcessDrag/ReleaseMouse multi-selection group/root drops with Undo/cycle/lock/edge-scroll, catalog payload and viewport settings passed";
            }
            finally { Object.Destroy(templateObject); }
        }
    }

    private static void TestCatalogNavigation(BlueprintEditorView view, InputState input)
    {
        string Page() => Field<TMP_Text>(view, "catalogPageText").text;
        input.Keys.Add(KeyCode.Q); view.Tick(); input.Keys.Clear();
        Require(Page() == "1 / 2", "Catalog Q does not page backward");
        Scroll(Button(view, "Catalog_piece0").gameObject, -1f);
        Require(Page() == "2 / 2", "Wheel over a native card does not page forward through its hierarchy");
        Scroll(Button(view, "Catalog_piece48").gameObject, 1f);
        Require(Page() == "1 / 2", "Card wheel does not page backward");
        input.Keys.Add(KeyCode.E); view.Tick(); input.Keys.Clear();
        Require(Page() == "2 / 2", "Catalog E does not page forward");
        ScrollRect materials = Field<ScrollRect>(view, "catalogMaterialScroll");
        Canvas.ForceUpdateCanvases(); materials.verticalNormalizedPosition = 1f;
        Scroll(Button(view, "Material_Материал 00").gameObject, -1f);
        Require(Page() == "2 / 2" && materials.verticalNormalizedPosition < 1f,
            "Material wheel was stolen by card paging or stopped scrolling");
        TMP_InputField search = Field<TMP_InputField>(view, "catalogSearch");
        Focus(search);
        input.Keys.Add(KeyCode.Q); input.Keys.Add(KeyCode.PageUp); view.Tick(); input.Keys.Clear();
        Scroll(Button(view, "Catalog_piece48").gameObject, 1f);
        Require(Page() == "2 / 2" && view.IsTextInputFocused,
            "Catalog paging consumes Q/wheel/PgUp while native TMP is editing");
        search.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
        input.Keys.Add(KeyCode.Q); view.Tick(); input.Keys.Clear();
        Require(Page() == "1 / 2", "Catalog paging did not resume after TMP focus ended");
    }

    private static void TestNumericDefaults(BlueprintEditorView view, InputState input)
    {
        TMP_InputField step = Field<TMP_InputField>(view, "scaleStepInput");
        Require(view.ScaleStepPercent == 10f, "Uniform transform step is not 10 percentage points by default");
        foreach (var pair in new[] { new[] { ".05", ".1" }, new[] { "500", "100" }, new[] { "NaN", "10" } })
        {
            step.SetTextWithoutNotify(pair[0]); step.onEndEdit.Invoke(pair[0]);
            Require(Mathf.Abs(view.ScaleStepPercent - float.Parse(pair[1], CultureInfo.InvariantCulture)) < .001f,
                "Uniform step has no finite clamp: " + pair[0]);
        }
        step.SetTextWithoutNotify("2.5"); step.onEndEdit.Invoke("2.5");
        Reset(step, "10", focused: true);
        Require(view.ScaleStepPercent == 10f, "RMB does not apply step10 to the actual View property");
        foreach (TMP_InputField size in Field<TMP_InputField[]>(view, "viewportScaleInputs")) Reset(size, "100");
        Button grid = Button(view, "GridToggle"), projection = Button(view, "ProjectionToggle");
        Require(grid.transform.parent == Field<RectTransform>(view, "viewportControls") &&
            projection.transform.parent == grid.transform.parent && grid.GetComponent<BlueprintEditorHoverTarget>() &&
            projection.GetComponent<BlueprintEditorHoverTarget>(), "Viewport icons are hidden inside settings or have no tooltip");
        Require(Button(view, "View").GetComponentInChildren<TMP_Text>().text ==
            BuildWorksLocalization.Text("editor.view.settings"),
            "Direct header settings control is not labelled clearly");
        float uiScale = 0f;
        view.UiScaleChanged += value => uiScale = value;
        Click(Button(view, "InterfaceScale"));
        Require(Mathf.Abs(uiScale - .6f) < .0001f &&
            Mathf.Abs(view.RootCanvas.GetComponent<CanvasScaler>().referenceResolution.x - 3200f) < .1f,
            "Interface scale control did not wrap 140% to 60% and update the real canvas");
        view.SetUiScale(1.4f);
        view.HideViewportSettings();
        view.SetTool(BlueprintEditorTool.Transform);
        foreach (TMP_InputField field in Field<TMP_InputField[]>(view, "inspectorPosition")) Reset(field, "0");
        foreach (TMP_InputField field in Field<TMP_InputField[]>(view, "inspectorRotation")) Reset(field, "0");
        TMP_InputField scale = Field<TMP_InputField>(view, "inspectorScale");
        Reset(scale, "100");
        Reset(Field<TMP_InputField>(view, "inspectorMoveStep"), "0.05");
        Reset(Field<TMP_InputField>(view, "inspectorAngleStep"), "1");
        Scrub(scale, 8f); Require(scale.text == "110", "Uniform scale numeric scrub ignores the10-point step");
        scale.SetTextWithoutNotify("100"); input.Keys.Add(KeyCode.LeftShift); Scrub(scale, 8f); input.Keys.Clear();
        Require(scale.text == "100.8", "Shift scale scrub is still quantized to whole/10 percent");
        scale.SetTextWithoutNotify("123.45"); scale.onEndEdit.Invoke("123.45");
        Require(scale.text == "123.45", "Direct numeric scale entry was forced onto the scrub step");
        TMP_InputField name = Field<TMP_InputField>(view, "inspectorName");
        string oldName = name.text;
        RightClick(name);
        Require(name.text == oldName && !name.GetComponent<BlueprintEditorNumericScrub>(),
            "RMB numeric reset also resets object names");
        view.SetTool(BlueprintEditorTool.Array);
        Button spacing = Button(view, "ArrayStep");
        spacing.GetComponent<BlueprintEditorHoverTarget>().OnPointerEnter(
            new PointerEventData(EventSystem.current));
        typeof(BlueprintEditorView).GetMethod("ShowTooltipNow", Fields).Invoke(view, null);
        Canvas.ForceUpdateCanvases();
        Require(!Overlaps((RectTransform)spacing.transform, Field<RectTransform>(view, "tooltip")),
            "Array spacing tooltip covers the spacing button");
        Click(spacing);
        Require(!Field<RectTransform>(view, "tooltip").gameObject.activeSelf,
            "Clicked Array spacing button remains hidden by its tooltip");
        int liveCallbacks = 0;
        float liveRise = float.NaN;
        view.ArrayChanged += (_, __, rise, ___, ____, _____, ______) =>
        { ++liveCallbacks; liveRise = rise; };
        TMP_InputField riseField = Field<TMP_InputField>(view, "arrayRise");
        Require(riseField is BlueprintEditorInputField,
            "Numeric scrub still uses TMP's competing selection-drag handler");
        riseField.SetTextWithoutNotify("0");
        var livePointer = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left, eligibleForClick = true, position = new Vector2(100,100) };
        ExecuteEvents.Execute(riseField.gameObject, livePointer, ExecuteEvents.beginDragHandler);
        livePointer.position += Vector2.right;
        ExecuteEvents.Execute(riseField.gameObject, livePointer, ExecuteEvents.dragHandler);
        Require(liveCallbacks > 0 && Mathf.Abs(liveRise - .05f) < .0001f,
            "Array scrub did not dispatch a live preview before drag release");
        ExecuteEvents.Execute(riseField.gameObject, livePointer, ExecuteEvents.endDragHandler);
        view.SetContextHints("ЛКМ: выбрать  ·  Tab: каталог");
        TMP_Text hints = Field<TMP_Text>(view, "statusHintText");
        Require(hints.text.Contains("Tab: каталог") && !hints.raycastTarget,
            "Bottom context hints are missing or intercept pointer input");
        view.ShowError("НЕЛЬЗЯ СОХРАНИТЬ", "Некорректные данные.", canRetry: false);
        Require(!ButtonObject(view, "SaveExit").activeSelf &&
            Button(view, "Cancel").GetComponentInChildren<TMP_Text>().text ==
                BuildWorksLocalization.Text("editor.view.return"),
            "Non-retryable validation error still offers a meaningless retry");
        view.HideDialog();
        int moveStepRequests = 0, angleStepRequests = 0;
        view.ArrayMoveStepRequested += () => ++moveStepRequests;
        view.ArrayAngleStepRequested += () => ++angleStepRequests;
        Click(Button(view, "ArrayMoveStep")); Click(Button(view, "ArrayAngleStep"));
        Require(moveStepRequests == 1 && angleStepRequests == 1,
            "Array rise/angle step controls do not dispatch");
        Reset(Field<TMP_InputField[]>(view, "arrayCount")[0], "2");
        Reset(Field<TMP_InputField[]>(view, "arrayCount")[1], "1");
        foreach (string field in new[] { "arrayRise", "arrayRotation", "arrayPitch", "arrayRoll", "arrayScaleStepX" })
            Reset(Field<TMP_InputField>(view, field), "0");
        view.SetTool(BlueprintEditorTool.Contour);
        Reset(Field<TMP_InputField>(view, "contourScaleStep"), "0");
        view.SetTool(BlueprintEditorTool.Select);
    }

    private static void TestOutlinerDragDrop(BlueprintEditorView view)
    {
        if (!Field<RectTransform>(view, "outliner").gameObject.activeInHierarchy)
            Click(Button(view, "CompactPane"));
        Require(Field<RectTransform>(view, "outliner").gameObject.activeInHierarchy,
            "Native compact-pane button did not return to the object tree");
        var parts = new List<BlueprintEditorPart>();
        for (int index = 0; index < 16; ++index)
            parts.Add(new BlueprintEditorPart("d" + index, "wall", "Деталь " + index,
                new Point3(index, 0, 0), new Rotation3(0,0,0,1)));
        var document = new BlueprintEditorDocument(null, "DnD", "Декор", parts, new[] {
            new BlueprintEditorGroup("target", "Целевая группа"),
            new BlueprintEditorGroup("parent", "Родитель"),
            new BlueprintEditorGroup("child", "Вложенная", parentGroupId: "parent"),
            new BlueprintEditorGroup("locked", "Закрытая", locked: true) });
        int routed = 0;
        view.NodeClicked += (id, ctrl, shift) => {
            if (ctrl || shift) document.ToggleSelection(id); else document.SelectOnly(id);
            view.Bind(document, true, null, Vector3.one);
        };
        view.MoveSelectionToGroupRequested += id => {
            ++routed;
            if (document.SetSelectionGroup(id)) view.Bind(document, true, null, Vector3.one);
        };
        document.SelectOnly("d0"); document.ToggleSelection("d1");
        view.Bind(document, true, null, Vector3.one);
        var collapsed = Field<HashSet<string>>(view, "collapsedGroups");
        // Fold by the real disclosure button before dropping into that group.
        foreach (Button child in Button(view, "Row_target").GetComponentsInChildren<Button>())
            if (child.name == "Disclosure") { Click(child); break; }
        Require(collapsed.Contains("target"), "Native group disclosure did not fold target");
        PointerEventData pointer = BeginTreeDrag(view, "Row_d0");
        Require(document.Selection.Count == 2, "Starting drag silently replaces multi-selection");
        DropTree(view, pointer, Button(view, "Row_target").gameObject);
        Require(routed == 1 && document.Parts[0].ParentGroupId == "target" && document.Parts[1].ParentGroupId == "target" &&
            document.Parts[0].Position.X == 0 && document.Parts[1].Position.X == 1 && !collapsed.Contains("target"),
            "Native multi-drop does not move both nodes, preserve pose and expand target");
        Require(document.Undo() && document.Parts[0].ParentGroupId == null && document.Parts[1].ParentGroupId == null && !document.CanUndo,
            "Native group drop is not a single atomic Undo");
        document.Redo();
        document.SelectOnly("target"); view.Bind(document, true, null, Vector3.one);
        pointer = BeginTreeDrag(view, "Row_d0");
        Require(document.Selection.Count == 1 && document.Selection[0] == "d0",
            "Dragging a nested child silently moves the selected parent group");
        ExecuteEvents.Execute(Field<RectTransform>(view, "outlinerRows").gameObject, pointer, ExecuteEvents.endDragHandler);
        document.SelectOnly("d0"); document.ToggleSelection("d1"); view.Bind(document, true, null, Vector3.one);
        pointer = BeginTreeDrag(view, "Row_d0");
        DropTree(view, pointer, Field<TMP_Text>(view, "outlinerTitle").transform.parent.gameObject);
        Require(document.Parts[0].ParentGroupId == null && document.Parts[1].ParentGroupId == null,
            "Header root drop does not detach the full multi-selection");
        while (document.CanUndo) document.Undo();
        document.SelectOnly("parent"); view.Bind(document, true, null, Vector3.one);
        foreach (string destination in new[] { "Row_parent", "Row_child", "Row_locked" })
        {
            int before = routed;
            pointer = BeginTreeDrag(view, "Row_parent");
            DropTree(view, pointer, Button(view, destination).gameObject, valid: false);
            Require(routed == before && !document.CanUndo, "Self/cycle/locked drop was routed as a valid command");
        }
        document.SelectOnly("d0"); view.Bind(document, true, null, Vector3.one);
        RectTransform stableRows = Field<RectTransform>(view, "outlinerRows");
        pointer = BeginTreeDrag(view, "Row_d2");
        Require(document.Selection.Count == 1 && document.Selection[0] == "d2" &&
            pointer.pointerDrag == stableRows.gameObject && stableRows.gameObject.activeInHierarchy,
            "Unselected-row drag does not select coherently or loses the drag host during RebuildOutliner");
        ScrollRect scroll = Field<RectTransform>(view, "outliner").GetComponent<ScrollRect>();
        Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 1f;
        var corners = new Vector3[4]; Field<RectTransform>(view, "outlinerRowViewport").GetWorldCorners(corners);
        pointer.position = RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[3]) * .5f + Vector3.right * 20f);
        ExecuteEvents.Execute(stableRows.gameObject, pointer, ExecuteEvents.dragHandler);
        view.Tick();
        float afterFirstTick = scroll.verticalNormalizedPosition;
        view.Tick();
        Require(afterFirstTick < 1f && scroll.verticalNormalizedPosition < afterFirstTick,
            "Short Outliner does not continuously edge-scroll while the pointer stays still");
        DropTree(view, pointer, Button(view, "Row_target").gameObject);
        Require(!Field<bool>(view, "outlinerDragging") &&
            Field<TMP_Text>(view, "outlinerTitle").text ==
                BuildWorksLocalization.Text("editor.view.outliner"),
            "Drop leaves a stale root target caption / dragging state");
        int beforeCancel = routed;
        pointer = BeginTreeDrag(view, "Row_d2");
        view.CancelOutlinerDrag();
        typeof(StandaloneInputModule).GetMethod("ReleaseMouse", Fields).Invoke(Module(),
            new object[] { pointer, Field<TMP_Text>(view, "outlinerTitle").transform.parent.gameObject });
        Require(!view.IsOutlinerDragging && routed == beforeCancel && document.Selection.Count == 1 &&
            document.Selection[0] == "d2" && document.Parts[2].ParentGroupId == "target",
            "Cancelled tree drag changes selection or still drops when the mouse is released");
    }

    private static PointerEventData BeginTreeDrag(BlueprintEditorView view, string rowName)
    {
        Button row = Button(view, rowName);
        GameObject host = ExecuteEvents.GetEventHandler<IDragHandler>(row.gameObject);
        Require(host == Field<RectTransform>(view, "outlinerRows").gameObject, "Outliner drag is captured by a disposable row or ScrollRect");
        var pointer = new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left, eligibleForClick = true,
            pointerPress = row.gameObject, pointerClick = row.gameObject, pointerDrag = host,
            pointerPressRaycast = new RaycastResult { gameObject = row.gameObject },
            pressPosition = new Vector2(100,100), position = new Vector2(120,100), delta = new Vector2(20,0) };
        typeof(PointerInputModule).GetMethod("ProcessDrag", Fields).Invoke(Module(), new object[] { pointer });
        Require(pointer.dragging && !pointer.eligibleForClick && Field<bool>(view, "outlinerDragging"),
            "Native drag threshold does not start Outliner transfer");
        return pointer;
    }

    private static void DropTree(BlueprintEditorView view, PointerEventData pointer, GameObject target, bool valid = true)
    {
        ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerEnterHandler);
        Outline cue = Field<Outline>(view, "outlinerDropCue");
        Require(valid ? cue && cue.enabled : !cue, "Outliner valid/invalid target cue is incorrect");
        typeof(StandaloneInputModule).GetMethod("ReleaseMouse", Fields).Invoke(Module(), new object[] { pointer, target });
        Require(!Field<Outline>(view, "outlinerDropCue"), "Native drop leaves target cue visible");
        Canvas.ForceUpdateCanvases();
    }

    private static StandaloneInputModule Module() => EventSystem.current.GetComponent<StandaloneInputModule>() ??
        EventSystem.current.gameObject.AddComponent<StandaloneInputModule>();

    private static void Scrub(TMP_InputField field, float delta)
    {
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
            eligibleForClick = true, position = new Vector2(100,100) };
        ExecuteEvents.Execute(field.gameObject, pointer, ExecuteEvents.beginDragHandler);
        pointer.position += Vector2.right * delta;
        ExecuteEvents.Execute(field.gameObject, pointer, ExecuteEvents.dragHandler);
        ExecuteEvents.Execute(field.gameObject, pointer, ExecuteEvents.endDragHandler);
        Require(!pointer.eligibleForClick, "Numeric scrub permits spurious click-edit after release");
    }

    private static void Reset(TMP_InputField field, string expected, bool focused = false)
    {
        Require(field.gameObject.activeInHierarchy && field.IsInteractable(), "Reset fixture targets inactive numeric input: " + field.name);
        int callbacks = 0;
        UnityEngine.Events.UnityAction<string> callback = _ => ++callbacks;
        field.onEndEdit.AddListener(callback);
        field.SetTextWithoutNotify("37");
        if (focused) Focus(field);
        RightClick(field);
        field.onEndEdit.RemoveListener(callback);
        Require(field.text == expected && callbacks == 1 && !field.isFocused,
            "RMB numeric reset did not commit actual default once: " + field.name + "=" + field.text + ", callbacks=" + callbacks);
    }

    private static void RightClick(TMP_InputField field) => RightClick(field.gameObject);

    private static void RightClick(GameObject target) => ExecuteEvents.Execute(target,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Right }, ExecuteEvents.pointerClickHandler);

    private static PointerEventData BeginRightHold(GameObject target)
    {
        var pointer = new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Right,
            position = Center((RectTransform)target.transform)
        };
        ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
        return pointer;
    }

    private static Vector2 Center(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) * .5f);
    }

    private static bool Overlaps(RectTransform first, RectTransform second)
    {
        var a = new Vector3[4]; var b = new Vector3[4];
        first.GetWorldCorners(a); second.GetWorldCorners(b);
        return a[0].x < b[2].x && a[2].x > b[0].x &&
            a[0].y < b[2].y && a[2].y > b[0].y;
    }

    private static void Focus(TMP_InputField field)
    {
        EventSystem.current.SetSelectedGameObject(field.gameObject); field.ActivateInputField();
        typeof(TMP_InputField).GetMethod("ActivateInputFieldInternal", Fields).Invoke(field, null);
        Require(field.isFocused, "Native TMP did not enter text editing");
    }

    private static void Scroll(GameObject target, float delta) => ExecuteEvents.ExecuteHierarchy(target,
        new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0,delta) }, ExecuteEvents.scrollHandler);

    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, Fields).GetValue(owner);
    private static Button Button(BlueprintEditorView view, string name)
    {
        foreach (Button button in view.RootCanvas.GetComponentsInChildren<Button>(true))
            if (button.name == name && button.gameObject.activeInHierarchy) return button;
        throw new InvalidOperationException("Missing active UI button: " + name);
    }
    private static GameObject ButtonObject(BlueprintEditorView view, string name)
    {
        foreach (Button button in view.RootCanvas.GetComponentsInChildren<Button>(true))
            if (button.name == name) return button.gameObject;
        throw new InvalidOperationException("Missing UI button: " + name);
    }
    private static void Click(Button button)
    {
        Require(button.interactable, "UI button is disabled: " + button.name);
        ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        Canvas.ForceUpdateCanvases();
    }
    private static void EnsureInside(RectTransform parent, RectTransform child)
    {
        var corners = new Vector3[4]; child.GetWorldCorners(corners);
        Rect bounds = parent.rect; bounds.xMin -= 0.1f; bounds.yMin -= 0.1f;
        bounds.xMax += 0.1f; bounds.yMax += 0.1f;
        foreach (Vector3 corner in corners)
            Require(bounds.Contains(parent.InverseTransformPoint(corner)), child.name + " extends outside " + parent.name);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
