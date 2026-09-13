using System;
using System.Collections.Generic;
using System.Globalization;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    internal enum BlueprintEditorTool
    {
        Select,
        Transform,
        Array,
        Contour
    }

    internal enum BlueprintEditorPivotMode
    {
        SelectionCenter,
        ActiveObject,
        Bounds
    }

    internal enum BlueprintEditorDialogDecision
    {
        SaveAndExit,
        DiscardAndExit,
        Cancel,
        Retry,
        Acknowledge
    }

    internal sealed class BlueprintEditorCatalogItem
    {
        internal BlueprintEditorCatalogItem(
            string prefabName,
            string displayName,
            Sprite icon,
            string category,
            string material,
            string source,
            int index,
            CompositeBlueprintStore.Blueprint blueprint = null)
        {
            PrefabName = prefabName;
            DisplayName = displayName;
            Icon = icon;
            Category = string.IsNullOrWhiteSpace(category)
                ? HammerCatalogOrganizer.OtherGroup
                : category;
            Material = string.IsNullOrWhiteSpace(material)
                ? HammerCatalogOrganizer.OtherGroup
                : material;
            Source = string.IsNullOrWhiteSpace(source)
                ? HammerCatalogOrganizer.VanillaSource
                : source;
            Index = Math.Max(0, index);
            Blueprint = blueprint;
        }

        internal string PrefabName { get; }
        internal string DisplayName { get; }
        internal Sprite Icon { get; }
        internal string Category { get; }
        internal string Material { get; }
        internal string Source { get; }
        internal int Index { get; }
        internal CompositeBlueprintStore.Blueprint Blueprint { get; }
        internal bool IsBlueprint => Blueprint != null;
    }

    internal sealed class BlueprintEditorHoverTarget : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        ISelectHandler, IDeselectHandler
    {
        private Action<RectTransform, string> entered;
        private Action exited;
        private string baseTooltip;
        private string tooltip;

        internal void Initialize(
            string text,
            Action<RectTransform, string> onEntered,
            Action onExited)
        {
            baseTooltip = tooltip = text;
            entered = onEntered;
            exited = onExited;
        }

        internal void SetAvailability(bool available, string unavailableReason) =>
            tooltip = available ? baseTooltip : unavailableReason;

        public void OnPointerEnter(PointerEventData eventData) =>
            entered?.Invoke((RectTransform)transform, tooltip);

        public void OnPointerExit(PointerEventData eventData) => exited?.Invoke();

        public void OnPointerDown(PointerEventData eventData) => exited?.Invoke();

        public void OnSelect(BaseEventData eventData) =>
            entered?.Invoke((RectTransform)transform, tooltip);

        public void OnDeselect(BaseEventData eventData) => exited?.Invoke();
    }

    internal sealed class BlueprintEditorInputField : TMP_InputField
    {
        private bool NumericScrub => GetComponent<BlueprintEditorNumericScrub>() != null;

        public override void OnBeginDrag(PointerEventData eventData)
        {
            if (!NumericScrub) base.OnBeginDrag(eventData);
        }

        public override void OnDrag(PointerEventData eventData)
        {
            if (!NumericScrub) base.OnDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            if (!NumericScrub) base.OnEndDrag(eventData);
        }
    }

    internal sealed class BlueprintEditorNumericScrub : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        private TMP_InputField input;
        private float unitsPerPixel;
        private float minimum;
        private float maximum;
        private bool wholeNumbers;
        private float resetValue;
        internal Func<float> StepProvider;
        internal Func<float> UnitsPerPixelProvider;
        private float startValue;
        private float startX;
        private bool dragging;
        internal bool Dragging => dragging;
        private IBlueprintEditorInput editorInput;

        internal void Initialize(
            TMP_InputField target,
            float dragUnitsPerPixel,
            float minimumValue,
            float maximumValue,
            bool useWholeNumbers,
            IBlueprintEditorInput inputSource = null,
            float defaultValue = 0f)
        {
            input = target;
            resetValue = defaultValue;
            unitsPerPixel = dragUnitsPerPixel;
            minimum = minimumValue;
            maximum = maximumValue;
            wholeNumbers = useWholeNumbers;
            editorInput = inputSource ?? UnityBlueprintEditorInput.Instance;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || unitsPerPixel <= 0f ||
                !input.IsInteractable() || !TryRead(input.text, out startValue) ||
                float.IsNaN(startValue) || float.IsInfinity(startValue)) return;
            eventData.eligibleForClick = false;
            startX = eventData.position.x;
            dragging = true;
            input.DeactivateInputField();
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            bool precise = editorInput.GetKey(KeyCode.LeftShift) || editorInput.GetKey(KeyCode.RightShift);
            float multiplier = precise ? 0.1f :
                editorInput.GetKey(KeyCode.LeftControl) || editorInput.GetKey(KeyCode.RightControl) ? 10f : 1f;
            float units = UnitsPerPixelProvider?.Invoke() ?? unitsPerPixel;
            float delta = (eventData.position.x - startX) * units * multiplier;
            if (StepProvider != null && !precise)
            {
                float step = StepProvider();
                delta = Mathf.Round(delta / step) * step;
            }
            float value = Mathf.Clamp(startValue + delta, minimum, maximum);
            if (wholeNumbers && StepProvider == null) value = Mathf.Round(value);
            input.text = value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Right || !input.IsInteractable()) return;
            dragging = false;
            // Reset before deselection, so TMP cannot commit the previous edit on focus loss.
            input.SetTextWithoutNotify(resetValue.ToString("0.###", CultureInfo.InvariantCulture));
            bool focused = input.isFocused;
            input.DeactivateInputField();
            if (EventSystem.current && EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            if (!focused) input.onEndEdit.Invoke(input.text);
            eventData.Use();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            dragging = false;
            input.onEndEdit.Invoke(input.text);
        }

        private void OnDisable() => dragging = false;

        private static bool TryRead(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value) ||
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    internal sealed class BlueprintEditorCatalogScroll : MonoBehaviour, IScrollHandler
    {
        internal Action<float> Scroll;
        public void OnScroll(PointerEventData data) => Scroll?.Invoke(data.scrollDelta.y);
    }

    internal sealed class BlueprintEditorOutlinerDrag : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        internal Action<PointerEventData> Begin, Drag;
        internal Action End;
        public void OnBeginDrag(PointerEventData data) => Begin?.Invoke(data);
        public void OnDrag(PointerEventData data) => Drag?.Invoke(data);
        public void OnEndDrag(PointerEventData data) => End?.Invoke();
    }

    internal sealed class BlueprintEditorOutlinerDrop : MonoBehaviour,
        IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        internal string StableId;
        internal bool IsGroup;
        internal Action<BlueprintEditorOutlinerDrop> Enter, Drop;
        internal Action<BlueprintEditorOutlinerDrop> Exit;
        public void OnDrop(PointerEventData data) => Drop?.Invoke(this);
        public void OnPointerEnter(PointerEventData data) => Enter?.Invoke(this);
        public void OnPointerExit(PointerEventData data) => Exit?.Invoke(this);
    }

    internal sealed class BlueprintEditorRightClick : MonoBehaviour,
        IPointerDownHandler, IPointerUpHandler
    {
        internal Action<Vector2> Pressed;
        internal Action<Vector2> Released;

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Right) return;
            Pressed?.Invoke(data.position);
            data.Use();
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Right) return;
            Released?.Invoke(data.position);
            data.Use();
        }
    }

    /// <summary>
    /// Builds and updates the Blueprint Editor canvas, tool inspector, catalog,
    /// status/help bars, and object-tree interactions.
    /// </summary>
    internal sealed class BlueprintEditorView : IDisposable
    {
        private static string T(string key, params object[] arguments) =>
            BuildWorksLocalization.Text(key, arguments);

        private static readonly Color PanelColor = new Color(0.055f, 0.060f, 0.060f, 0.97f);
        private static readonly Color PanelRaised = new Color(0.105f, 0.095f, 0.080f, 0.985f);
        private static readonly Color ButtonColor = new Color(0.155f, 0.145f, 0.125f, 1f);
        private static readonly Color SelectedColor = new Color(0.46f, 0.255f, 0.055f, 1f);
        private static readonly Color AccentColor = new Color(0.92f, 0.61f, 0.18f, 1f);
        private static readonly Color TextColor = new Color(0.94f, 0.91f, 0.84f, 1f);
        private static readonly Color MutedColor = new Color(0.66f, 0.64f, 0.59f, 1f);

        private readonly TMP_Text fontTemplate;
        private readonly BlueprintEditorIconLibrary icons;
        private readonly GameObject canvasObject;
        private readonly Canvas canvas;
        private readonly CanvasScaler canvasScaler;
        private readonly RectTransform safeRoot;
        private readonly BlueprintEditorSkin skin = new BlueprintEditorSkin();
        private readonly RectTransform top;
        private readonly RectTransform rail;
        private readonly RectTransform viewport;
        private readonly RectTransform rightColumn;
        private readonly RectTransform outliner;
        private readonly RectTransform inspector;
        private readonly TMP_Text inspectorTitle;
        private readonly RectTransform inspectorTabs;
        private readonly RectTransform status;
        private readonly RectTransform outlinerRows;
        private readonly TMP_Text outlinerTitle;
        private bool outlinerDragging;
        private Vector2 outlinerDragPosition;
        internal bool IsOutlinerDragging => outlinerDragging;
        internal void CancelOutlinerDrag() => EndOutlinerDrag();
        private Outline outlinerDropCue;
        private readonly RectTransform outlinerRowViewport;
        private readonly TMP_InputField outlinerSearch;
        private readonly Button outlinerFilterButton;
        private readonly GameObject outlinerMenu;
        private readonly RectTransform outlinerMenuRows;
        private readonly RectTransform outlinerContextMenu;
        private readonly RectTransform outlinerContextViewport;
        private readonly RectTransform outlinerContextRows;
        private readonly List<BlueprintEditorNumericScrub> numericScrubs =
            new List<BlueprintEditorNumericScrub>();
        private readonly RectTransform railViewport;
        private readonly TMP_Text documentTitle;
        private readonly TMP_Text inspectorContent;
        private readonly GameObject inspectorEditPanel;
        private readonly TMP_InputField inspectorName;
        private readonly TMP_Text inspectorPositionLabel;
        private readonly TMP_Text inspectorRotationLabel;
        private readonly TMP_Text inspectorScaleLabel;
        private readonly TMP_InputField[] inspectorPosition = new TMP_InputField[3];
        private readonly TMP_InputField[] inspectorRotation = new TMP_InputField[3];
        private readonly TMP_InputField inspectorScale;
        private readonly Button inspectorResetButton;
        private readonly Button inspectorSpaceButton;
        private readonly TMP_InputField inspectorMoveStep;
        private readonly TMP_InputField inspectorAngleStep;
        private readonly Button inspectorAnchorVisibilityButton;
        private readonly Button inspectorMagnetButton;
        private readonly List<Button> anchorPinButtons = new List<Button>();
        private readonly List<Button> anchorConstraintButtons = new List<Button>();
        private readonly Button inspectorPivotButton;
        private readonly Button inspectorSelectionPivotButton;
        private readonly Button inspectorActivePivotButton;
        private readonly Button[] inspectorBoundsButtons = new Button[9];
        private readonly GameObject groupEditPanel;
        private readonly TMP_InputField groupName;
        private readonly TMP_Text detailInfo;
        private readonly Button detailVisibilityButton;
        private readonly Button detailLockButton;
        private readonly Button detailGroupButton;
        private readonly GameObject blueprintEditPanel;
        private readonly TMP_InputField blueprintName;
        private readonly TMP_InputField blueprintCategory;
        private readonly TMP_Text blueprintInfo;
        private readonly GameObject arrayEditPanel;
        private readonly TMP_InputField[] arrayCount = new TMP_InputField[2];
        private readonly TMP_InputField arrayRise;
        private readonly TMP_InputField arrayPitch;
        private readonly TMP_InputField arrayRoll;
        private readonly Button arrayDistributionButton;
        private readonly Button arrayStepButton;
        private readonly Button arrayMoveStepButton;
        private readonly Button arrayAngleStepButton;
        private readonly Button arraySymmetryButton;
        private string arrayDirectionInfo = T("editor.view.array_direction");
        private readonly TMP_InputField arrayRotation;
        private readonly TMP_InputField arrayScaleStepX;
        private readonly TMP_Text arrayInfo;
        private readonly Button arrayApplyButton;
        private readonly GameObject contourEditPanel;
        private readonly TMP_Text contourInfo;
        private readonly TMP_InputField contourScaleStep;
        private readonly Button contourApplyButton;
        private readonly TMP_Text statusText;
        private readonly TMP_Text statusHintText;
        private float statusMessageUntil;
        private readonly Button undoButton;
        private readonly Button redoButton;
        private readonly Button saveButton;
        private readonly Button compactPaneButton;
        private readonly Button outlinerNewGroupButton;
        private readonly List<Button> outlinerSelectionButtons = new List<Button>();
        private readonly Button[] inspectorTabButtons = new Button[3];
        private readonly GameObject fullTitle;
        private readonly TMP_Text lightingLabel;
        private readonly Dictionary<BlueprintEditorTool, Button> toolButtons =
            new Dictionary<BlueprintEditorTool, Button>();
        private readonly RectTransform tooltip;
        private readonly TMP_Text tooltipText;
        private readonly GameObject catalog;
        private readonly RectTransform catalogPanel;
        private readonly RectTransform catalogGrid;
        private readonly RectTransform catalogCategories;
        private readonly RectTransform catalogCategoryTabs;
        private readonly RectTransform catalogSources;
        private readonly TMP_InputField catalogSearch;
        private readonly Button catalogPreviousPage;
        private readonly Button catalogNextPage;
        private readonly Button catalogMaterialsPrevious;
        private readonly Button catalogMaterialsNext;
        private readonly TMP_Text catalogPageText;
        private readonly TMP_Text catalogBreadcrumb;
        private readonly TMP_Text catalogMaterialTitle;
        private readonly Button catalogPartsModeButton;
        private readonly Button catalogBlueprintsModeButton;
        private readonly TMP_InputField catalogPageInput;
        private readonly ScrollRect catalogMaterialScroll;
        private readonly GameObject viewportSettings;
        private readonly Button interfaceScaleButton;
        private readonly RectTransform viewportControls;
        private readonly TMP_InputField scaleStepInput;
        public float ScaleStepPercent { get; private set; } = 10f;
        private readonly TMP_InputField[] viewportScaleInputs = new TMP_InputField[5];
        private readonly Button gridButton;
        private readonly Button projectionButton;
        private readonly IBlueprintEditorInput input;
        private readonly GameObject modal;
        private readonly TMP_Text modalTitle;
        private readonly TMP_Text modalMessage;
        private readonly Button dialogPrimaryButton;
        private readonly Button dialogDiscardButton;
        private readonly Button dialogCancelButton;
        private readonly List<CanvasGroup> modalBlockedGroups = new List<CanvasGroup>();
        private Rect lastSafeArea;
        private Vector2 lastCanvasSize;
        private RectTransform pendingTooltipAnchor;
        private string pendingTooltipText;
        private float tooltipAt;
        private BlueprintEditorTool selectedTool;
        private BlueprintEditorLightingPreset lightingPreset;
        private bool compact;
        private bool compactInspector;
        private int inspectorTab;
        private bool errorDialog;
        private string inspectorNodeId;
        private string groupNodeId;
        private bool inspectorDelta;
        private bool detailVisible;
        private bool detailLocked;
        private string detailParentGroupId;
        private bool localTransformSpace = true;
        private bool customTransformPivot;
        private BlueprintEditorPivotMode transformPivotMode;
        private int transformBoundsPivot = 4;
        private float translationStep = 0.05f;
        private float rotationStep = 1f;
        private bool showAllTransformAnchors = true;
        private bool transformMeshSnap;
        private Vector3? blueprintBoundsSize;
        private BlueprintEditorDocument boundDocument;
        private readonly List<BlueprintEditorCatalogItem> shownCatalogItems =
            new List<BlueprintEditorCatalogItem>();
        private readonly HashSet<string> collapsedGroups =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> missingPrefabNodes =
            new HashSet<string>(StringComparer.Ordinal);
        private string selectedCatalogCategory;
        private string hoveredNodeId;
        private string operationMetrics = string.Empty;
        private string statusMessage = string.Empty;
        private string selectedCatalogMaterial;
        private string selectedCatalogSource;
        private int catalogPage;
        private int catalogPageCount = 1;
        private bool catalogBlueprintMode;
        private bool showGrid = true;
        private bool orthographic;
        private int outlinerFilter;
        private bool outlinerReparentOnly;
        private bool outlinerContextTracking;
        private bool catalogStateInitialized;
        private float requestedUiScale = 1f;
        private bool disposed;

        internal BlueprintEditorView(TMP_Text textTemplate, IBlueprintEditorInput editorInput = null)
        {
            input = editorInput ?? UnityBlueprintEditorInput.Instance;
            fontTemplate = textTemplate;
            icons = new BlueprintEditorIconLibrary();
            canvasObject = new GameObject(
                "BuildWorks_BlueprintEditorView",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            try
            {
                canvasObject.hideFlags = HideFlags.HideAndDontSave;
                canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 5000;
                canvasScaler = canvasObject.GetComponent<CanvasScaler>();
                canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                canvasScaler.matchWidthOrHeight = 1f;
                UpdateCanvasScale();

                safeRoot = CreateRect("SafeArea", canvasObject.transform);
                top = CreatePanel("Top", safeRoot, PanelRaised, BlueprintEditorSkin.Surface.Header);
                rail = CreatePanel("ToolRail", safeRoot, PanelColor);
                viewport = CreatePanel("ViewportInput", safeRoot, Color.clear, null);
                viewport.GetComponent<Image>().raycastTarget = false;
                rightColumn = CreatePanel("RightColumn", safeRoot, PanelColor, null);
                outliner = CreatePanel("Outliner", safeRoot, PanelRaised);
                inspector = CreatePanel("Inspector", safeRoot, PanelRaised);
                status = CreatePanel("Status", safeRoot, PanelColor, BlueprintEditorSkin.Surface.Header);
                status.GetComponent<Image>().raycastTarget = false;

                HorizontalLayoutGroup topLayout = top.gameObject.AddComponent<HorizontalLayoutGroup>();
                topLayout.padding = new RectOffset(8, 8, 12, 12);
                topLayout.spacing = 8f;
                topLayout.childAlignment = TextAnchor.MiddleLeft;
                topLayout.childControlHeight = true;
                topLayout.childForceExpandHeight = true;
                topLayout.childForceExpandWidth = false;
                CreateButton("Back", top, T("editor.view.back"), 80f, () => CloseRequested?.Invoke());
                fullTitle = CreateText("Header", top, T("editor.view.title"), 18f,
                    FontStyles.Bold, TextAlignmentOptions.MidlineLeft).gameObject;
                fullTitle.AddComponent<LayoutElement>().preferredWidth = 300f;
                documentTitle = CreateText("Document", top, T("editor.new_blueprint"), 16f,
                    FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                documentTitle.overflowMode = TextOverflowModes.Ellipsis;
                documentTitle.gameObject.AddComponent<LayoutElement>().preferredWidth = 220f;
                undoButton = CreateIconButton(
                    "Undo", top, "undo", "↶", 44f, () => UndoRequested?.Invoke(),
                    T("editor.view.undo_tooltip"));
                redoButton = CreateIconButton(
                    "Redo", top, "redo", "↷", 44f, () => RedoRequested?.Invoke(),
                    T("editor.view.redo_tooltip"));
                CreateIconButton("View", top, null, T("editor.view.settings"), 112f,
                    ToggleViewportSettings, T("editor.view.settings_tooltip"));
                Button lightButton = CreateButton("Lighting", top, T("editor.view.light"), 92f, CycleLighting);
                lightingLabel = lightButton.GetComponentInChildren<TMP_Text>();
                saveButton = CreateButton("Save", top, T("editor.view.save"), 110f,
                    () => SaveRequested?.Invoke(), primary: true);
                CreateButton("Exit", top, T("editor.view.exit"), 80f, () => CloseRequested?.Invoke());
                compactPaneButton = CreateButton("CompactPane", top, T("editor.view.objects"), 96f,
                    ToggleCompactPane);
                compactPaneButton.gameObject.SetActive(false);

                var railScroll = rail.gameObject.AddComponent<ScrollRect>();
                railScroll.horizontal = false;
                railScroll.vertical = true;
                railScroll.movementType = ScrollRect.MovementType.Clamped;
                railViewport = CreateRect("RailViewport", rail);
                railViewport.gameObject.AddComponent<RectMask2D>();
                RectTransform railContent = CreateRect("RailContent", railViewport);
                railContent.anchorMin = new Vector2(0f, 1f);
                railContent.anchorMax = new Vector2(1f, 1f);
                railContent.pivot = new Vector2(0.5f, 1f);
                railContent.offsetMin = railContent.offsetMax = Vector2.zero;
                var railLayout = railContent.gameObject.AddComponent<VerticalLayoutGroup>();
                railLayout.padding = new RectOffset(6, 6, 4, 4);
                railLayout.spacing = 4f;
                railLayout.childAlignment = TextAnchor.UpperCenter;
                railLayout.childControlWidth = true;
                railLayout.childControlHeight = false;
                railLayout.childForceExpandWidth = true;
                railLayout.childForceExpandHeight = false;
                ContentSizeFitter railFitter = railContent.gameObject.AddComponent<ContentSizeFitter>();
                railFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                railScroll.viewport = railViewport;
                railScroll.content = railContent;
                AddTool(railContent, BlueprintEditorTool.Select,
                    "select", "↖", T("editor.view.tool_select"));
                AddTool(railContent, BlueprintEditorTool.Transform,
                    "axes", "✥", T("editor.view.tool_transform"));
                AddTool(railContent, BlueprintEditorTool.Array,
                    "duplicate", "▣", T("editor.view.tool_array"));
                AddTool(railContent, BlueprintEditorTool.Contour,
                    "snap", "⌁", T("editor.view.tool_contour"));

                RectTransform rootDrop = CreatePanel("OutlinerRootDrop", outliner, Color.clear, null);
                SetTopAnchored(rootDrop, 8f, 4f, 56f, 32f);
                outlinerTitle = CreateText("OutlinerTitle", rootDrop, T("editor.view.outliner"), 16f,
                    FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                SetInsets(outlinerTitle.rectTransform, 4f, 0f, 4f, 0f);
                AddOutlinerDrop(rootDrop, null, true);
                AddTooltip(rootDrop, T("editor.view.root_drop_tooltip"));
                RectTransform outlinerCommands = CreateRect("OutlinerCommands", outliner);
                SetTopAnchored(outlinerCommands, 8f, 40f, 8f, 32f);
                var commandsLayout = outlinerCommands.gameObject.AddComponent<HorizontalLayoutGroup>();
                commandsLayout.spacing = 4f;
                commandsLayout.childControlWidth = commandsLayout.childControlHeight = true;
                commandsLayout.childForceExpandWidth = true;
                AddOutlinerToolbarButton(outlinerCommands, "NewGroup", null, T("editor.view.new_group_short"), T("editor.view.empty_group"),
                    () => CreateGroupRequested?.Invoke(), false);
                outlinerNewGroupButton = AddOutlinerToolbarButton(outlinerCommands,
                    "GroupSelection", "group", T("editor.view.group_short"), T("editor.view.group_selection"),
                    () => GroupRequested?.Invoke(), false);
                AddOutlinerToolbarButton(outlinerCommands, "UngroupSelection", "ungroup", T("editor.view.ungroup_short"),
                    T("editor.view.ungroup_selection"), () => UngroupRequested?.Invoke());
                AddOutlinerToolbarButton(outlinerCommands, "ReparentSelection", "folder", T("editor.view.reparent_short"),
                    T("editor.view.reparent_selection"), () => ToggleOutlinerMenu(true));
                AddOutlinerToolbarButton(outlinerCommands, "RenameSelection", "edit", T("editor.view.name_short"),
                    T("editor.view.rename_selection"), BeginRenameSelection);
                AddOutlinerToolbarButton(outlinerCommands, "HideSelectionToolbar", "visibility", "●",
                    T("editor.view.toggle_visibility"), ToggleSelectedVisibility);
                AddOutlinerToolbarButton(outlinerCommands, "LockSelectionToolbar", "lock", "■",
                    T("editor.view.toggle_lock"), ToggleSelectedLock);
                Button outlinerMenuButton = CreateIconButton(
                    "OutlinerMenuButton", outliner, "menu", "⋯", 44f,
                    ToggleOutlinerMenu, T("editor.view.outliner_commands"));
                SetTopRight((RectTransform)outlinerMenuButton.transform, 6f, 4f, 44f, 32f);
                RectTransform searchPanel = CreatePanel("Search", outliner, ButtonColor);
                SetTopAnchored(searchPanel, 8f, 76f, 88f, 32f);
                TMP_Text search = CreateText("Text", searchPanel, string.Empty, 13f,
                    FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                search.color = MutedColor;
                search.raycastTarget = true;
                SetInsets((RectTransform)search.transform, 8f, 0f, 8f, 0f);
                TMP_Text searchPlaceholder = CreateText(
                    "Placeholder", searchPanel, T("editor.view.search"), 13f,
                    FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                searchPlaceholder.color = MutedColor;
                SetInsets((RectTransform)searchPlaceholder.transform, 8f, 0f, 8f, 0f);
                outlinerSearch = searchPanel.gameObject.AddComponent<TMP_InputField>();
                skin.Apply(outlinerSearch, input: true);
                outlinerSearch.textComponent = search;
                outlinerSearch.placeholder = searchPlaceholder;
                outlinerSearch.lineType = TMP_InputField.LineType.SingleLine;
                outlinerSearch.characterLimit = 48;
                outlinerSearch.onValueChanged.AddListener(_ =>
                {
                    if (boundDocument != null) RebuildOutliner(boundDocument);
                });
                outlinerFilterButton = CreateButton(
                    "OutlinerFilter", outliner, T("editor.view.all"), 76f, CycleOutlinerFilter);
                SetTopRight((RectTransform)outlinerFilterButton.transform, 8f, 76f, 76f, 32f);
                RectTransform rowViewport = CreateRect("RowsViewport", outliner);
                outlinerRowViewport = rowViewport;
                SetInsets(rowViewport, 4f, (float)BlueprintEditorLayout.OutlinerHeaderHeight - 4f, 4f, 4f);
                rowViewport.gameObject.AddComponent<RectMask2D>();
                ScrollRect rowScroll = outliner.gameObject.AddComponent<ScrollRect>();
                rowScroll.horizontal = false;
                rowScroll.vertical = true;
                rowScroll.movementType = ScrollRect.MovementType.Clamped;
                rowScroll.scrollSensitivity = 88f;
                outlinerRows = CreateRect("Rows", rowViewport);
                outlinerRows.anchorMin = new Vector2(0f, 1f);
                outlinerRows.anchorMax = new Vector2(1f, 1f);
                outlinerRows.pivot = new Vector2(0.5f, 1f);
                var rowsLayout = outlinerRows.gameObject.AddComponent<VerticalLayoutGroup>();
                rowsLayout.spacing = 0f;
                rowsLayout.childControlWidth = true;
                rowsLayout.childControlHeight = true;
                rowsLayout.childForceExpandWidth = true;
                rowsLayout.childForceExpandHeight = false;
                ContentSizeFitter rowsFitter = outlinerRows.gameObject.AddComponent<ContentSizeFitter>();
                rowsFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                rowScroll.viewport = rowViewport;
                rowScroll.content = outlinerRows;
                BlueprintEditorOutlinerDrag treeDrag = outlinerRows.gameObject.AddComponent<BlueprintEditorOutlinerDrag>();
                treeDrag.Begin = BeginOutlinerDrag;
                treeDrag.Drag = data => outlinerDragPosition = data.position;
                treeDrag.End = EndOutlinerDrag;

                outlinerMenu = CreatePanel("OutlinerMenu", outliner, PanelColor).gameObject;
                RectTransform outlinerMenuRect = (RectTransform)outlinerMenu.transform;
                SetInsets(outlinerMenuRect, 4f,
                    (float)BlueprintEditorLayout.OutlinerHeaderHeight - 4f, 4f, 4f);
                outlinerMenu.gameObject.AddComponent<RectMask2D>();
                RectTransform outlinerMenuViewport = CreateRect(
                    "Viewport", outlinerMenu.transform);
                SetInsets(outlinerMenuViewport, 6f, 6f, 6f, 6f);
                outlinerMenuViewport.gameObject.AddComponent<RectMask2D>();
                ScrollRect outlinerMenuScroll = outlinerMenu.AddComponent<ScrollRect>();
                outlinerMenuScroll.horizontal = false;
                outlinerMenuScroll.vertical = true;
                outlinerMenuScroll.movementType = ScrollRect.MovementType.Clamped;
                outlinerMenuScroll.scrollSensitivity = 88f;
                outlinerMenuRows = CreateRect("Rows", outlinerMenuViewport);
                outlinerMenuRows.anchorMin = new Vector2(0f, 1f);
                outlinerMenuRows.anchorMax = new Vector2(1f, 1f);
                outlinerMenuRows.pivot = new Vector2(0.5f, 1f);
                var outlinerMenuLayout =
                    outlinerMenuRows.gameObject.AddComponent<VerticalLayoutGroup>();
                outlinerMenuLayout.spacing = 4f;
                outlinerMenuLayout.childControlHeight = true;
                outlinerMenuLayout.childControlWidth = true;
                outlinerMenuLayout.childForceExpandHeight = false;
                outlinerMenuLayout.childForceExpandWidth = true;
                ContentSizeFitter outlinerMenuFitter =
                    outlinerMenuRows.gameObject.AddComponent<ContentSizeFitter>();
                outlinerMenuFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                outlinerMenuScroll.viewport = outlinerMenuViewport;
                outlinerMenuScroll.content = outlinerMenuRows;
                outlinerMenu.SetActive(false);

                outlinerContextMenu = CreatePanel(
                    "OutlinerContextMenu", safeRoot, PanelRaised);
                outlinerContextMenu.anchorMin = outlinerContextMenu.anchorMax =
                    new Vector2(0.5f, 0.5f);
                outlinerContextMenu.pivot = new Vector2(0f, 1f);
                outlinerContextMenu.sizeDelta = new Vector2(500f, 220f);
                RectTransform contextViewport = CreateRect("Viewport", outlinerContextMenu);
                outlinerContextViewport = contextViewport;
                SetInsets(contextViewport, 8f, 8f, 8f, 8f);
                contextViewport.gameObject.AddComponent<RectMask2D>();
                ScrollRect contextScroll = outlinerContextMenu.gameObject.AddComponent<ScrollRect>();
                contextScroll.horizontal = false;
                contextScroll.vertical = true;
                contextScroll.movementType = ScrollRect.MovementType.Clamped;
                contextScroll.scrollSensitivity = 80f;
                outlinerContextRows = CreateRect("Rows", contextViewport);
                outlinerContextRows.anchorMin = new Vector2(0f, 1f);
                outlinerContextRows.anchorMax = new Vector2(1f, 1f);
                outlinerContextRows.pivot = new Vector2(0.5f, 1f);
                outlinerContextRows.anchoredPosition = Vector2.zero;
                contextScroll.viewport = contextViewport;
                contextScroll.content = outlinerContextRows;
                outlinerContextMenu.gameObject.SetActive(false);

                inspectorTitle = CreateText("InspectorTitle", inspector, T("editor.view.object_properties"), 16f,
                    FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                SetTopAnchored((RectTransform)inspectorTitle.transform, 8f, 4f, 8f, 32f);
                inspectorTabs = CreateRect("InspectorTabs", inspector);
                SetTopAnchored(inspectorTabs, 8f, 38f, 8f, 28f);
                var tabLayout = inspectorTabs.gameObject.AddComponent<HorizontalLayoutGroup>();
                tabLayout.spacing = 4f;
                tabLayout.childControlHeight = true;
                tabLayout.childControlWidth = false;
                tabLayout.childForceExpandHeight = true;
                tabLayout.childForceExpandWidth = false;
                inspectorTabButtons[0] = CreateButton(
                    "TransformTab", inspectorTabs, T("editor.view.transform_short"), 104f, () => SetInspectorTab(0), true);
                inspectorTabButtons[1] = CreateButton(
                    "PartTab", inspectorTabs, T("editor.view.part"), 76f, () => SetInspectorTab(1));
                inspectorTabButtons[2] = CreateButton(
                    "BlueprintTab", inspectorTabs, T("editor.view.blueprint"), 76f, () => SetInspectorTab(2));
                foreach (Button button in inspectorTabButtons)
                {
                    LayoutElement layout = button.GetComponent<LayoutElement>();
                    layout.minHeight = layout.preferredHeight = 28f;
                    ((RectTransform)button.transform).sizeDelta = new Vector2(
                        ((RectTransform)button.transform).sizeDelta.x, 28f);
                    button.GetComponentInChildren<TMP_Text>().fontSize = 11f;
                }
                RectTransform inspectorViewport = CreateRect("InspectorViewport", inspector);
                SetInsets(inspectorViewport, 12f, 42f, 12f, 8f);
                inspectorViewport.gameObject.AddComponent<RectMask2D>();
                RectTransform inspectorBody = CreateRect("InspectorBody", inspectorViewport);
                inspectorBody.anchorMin = new Vector2(0f, 1f);
                inspectorBody.anchorMax = new Vector2(1f, 1f);
                inspectorBody.pivot = new Vector2(0.5f, 1f);
                inspectorBody.anchoredPosition = Vector2.zero;
                inspectorBody.sizeDelta = new Vector2(0f, 500f);
                inspectorContent = CreateText("InspectorContent", inspectorBody,
                    T("editor.view.nothing_selected"), 14f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                inspectorContent.textWrappingMode = TextWrappingModes.Normal;
                SetInsets((RectTransform)inspectorContent.transform, 0f, 0f, 0f, 0f);

                inspectorEditPanel = CreateRect("InspectorEdit", inspectorBody).gameObject;
                SetInsets((RectTransform)inspectorEditPanel.transform, 0f, 0f, 0f, 0f);
                inspectorName = CreateInput(
                    "Name", inspectorEditPanel.transform, new Vector2(0f, -24f), 228f);
                inspectorName.gameObject.SetActive(false);
                inspectorName.onSubmit.AddListener(_ => SubmitInspector());
                inspectorPositionLabel = CreateInspectorLabel(
                    inspectorEditPanel.transform, T("editor.view.position_m"), 0f);
                CreateAxisInputs(inspectorEditPanel.transform, inspectorPosition, 24f, 0.01f);
                inspectorRotationLabel = CreateInspectorLabel(
                    inspectorEditPanel.transform, T("editor.view.rotation_deg"), 62f);
                CreateAxisInputs(inspectorEditPanel.transform, inspectorRotation, 86f, 0.25f);
                foreach (TMP_InputField input in inspectorPosition)
                {
                    input.onSubmit.AddListener(_ => SubmitInspector());
                    input.onEndEdit.AddListener(_ => SubmitInspector());
                }
                foreach (TMP_InputField input in inspectorRotation)
                {
                    input.onSubmit.AddListener(_ => SubmitInspector());
                    input.onEndEdit.AddListener(_ => SubmitInspector());
                }
                inspectorScaleLabel = CreateInspectorLabel(
                    inspectorEditPanel.transform, T("editor.view.uniform_scale"), 124f);
                inspectorScale = CreateInput(
                    "Scale", inspectorEditPanel.transform, new Vector2(0f, -148f), 228f,
                    1f, 1f, 400f, true, 100f);
                inspectorScale.GetComponent<BlueprintEditorNumericScrub>().StepProvider = () => ScaleStepPercent;
                inspectorScale.onSubmit.AddListener(_ => SubmitInspector());
                inspectorScale.onEndEdit.AddListener(_ => SubmitInspector());
                Button applyInspector = CreateButton(
                    "ApplyInspector", inspectorEditPanel.transform, T("editor.view.apply"), 120f,
                    SubmitInspector, primary: true);
                SetInspectorButton((RectTransform)applyInspector.transform, 0f, 188f, 120f);
                inspectorResetButton = CreateButton(
                    "ResetInspector", inspectorEditPanel.transform, T("editor.view.reset"), 100f,
                    ResetInspector);
                SetInspectorButton((RectTransform)inspectorResetButton.transform, 128f, 188f, 100f);
                inspectorSpaceButton = CreateButton(
                    "TransformSpace", inspectorEditPanel.transform, T("editor.view.axes_local"), 110f,
                    () => SpaceRequested?.Invoke());
                SetInspectorButton((RectTransform)inspectorSpaceButton.transform, 0f, 232f, 70f);
                TMP_Text moveStepLabel = CreateText(
                    "MoveStepLabel", inspectorEditPanel.transform, T("editor.view.move_step_short"), 10f,
                    FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                SetTopLeft((RectTransform)moveStepLabel.transform, 76f, 232f, 22f, 36f);
                inspectorMoveStep = CreateInput(
                    "MoveStep", inspectorEditPanel.transform, new Vector2(98f, -232f), 48f,
                    0.001f, 0.001f, 10f, resetValue: 0.05f);
                TMP_Text angleStepLabel = CreateText(
                    "AngleStepLabel", inspectorEditPanel.transform, "Δ°", 10f,
                    FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                SetTopLeft((RectTransform)angleStepLabel.transform, 150f, 232f, 22f, 36f);
                inspectorAngleStep = CreateInput(
                    "AngleStep", inspectorEditPanel.transform, new Vector2(172f, -232f), 56f,
                    0.1f, 0.1f, 90f, resetValue: 1f);
                inspectorMoveStep.onEndEdit.AddListener(_ => SubmitSnap());
                inspectorAngleStep.onEndEdit.AddListener(_ => SubmitSnap());
                AddTooltip((RectTransform)inspectorMoveStep.transform,
                    T("editor.view.move_step_tooltip"));
                AddTooltip((RectTransform)inspectorAngleStep.transform,
                    T("editor.view.angle_step_tooltip"));
                inspectorAnchorVisibilityButton = CreateButton(
                    "TransformAnchors", inspectorEditPanel.transform, T("editor.view.points_all"), 110f,
                    () => AnchorVisibilityRequested?.Invoke());
                SetInspectorButton(
                    (RectTransform)inspectorAnchorVisibilityButton.transform, 0f, 276f, 110f);
                inspectorMagnetButton = CreateButton(
                    "TransformMagnet", inspectorEditPanel.transform, T("editor.view.magnet_game"), 110f,
                    () => MeshSnapRequested?.Invoke());
                SetInspectorButton(
                    (RectTransform)inspectorMagnetButton.transform, 118f, 276f, 110f);
                inspectorSelectionPivotButton = CreateButton(
                    "PivotSelection", inspectorEditPanel.transform, T("editor.view.center"), 70f,
                    () => PivotModeRequested?.Invoke(BlueprintEditorPivotMode.SelectionCenter));
                SetInspectorButton(
                    (RectTransform)inspectorSelectionPivotButton.transform, 0f, 320f, 70f);
                inspectorActivePivotButton = CreateButton(
                    "PivotActive", inspectorEditPanel.transform, T("editor.view.active_short"), 70f,
                    () => PivotModeRequested?.Invoke(BlueprintEditorPivotMode.ActiveObject));
                SetInspectorButton(
                    (RectTransform)inspectorActivePivotButton.transform, 78f, 320f, 70f);
                inspectorPivotButton = CreateButton(
                    "TransformPivot", inspectorEditPanel.transform, T("editor.view.select"), 72f,
                    () => PivotRequested?.Invoke());
                SetInspectorButton((RectTransform)inspectorPivotButton.transform, 156f, 320f, 72f);
                TMP_Text boundsPivotLabel = CreateInspectorLabel(
                    inspectorEditPanel.transform, T("editor.view.bounds_pivot"), 366f);
                string[] pivotLabels = { "↖", "↑", "↗", "←", "•", "→", "↙", "↓", "↘" };
                for (int index = 0; index < inspectorBoundsButtons.Length; ++index)
                {
                    int captured = index;
                    Button button = CreateButton(
                        "BoundsPivot" + index,
                        inspectorEditPanel.transform,
                        pivotLabels[index],
                        68f,
                        () => BoundsPivotRequested?.Invoke(captured));
                    RectTransform rect = (RectTransform)button.transform;
                    SetInspectorButton(rect, index % 3 * 80f, 390f + index / 3 * 34f, 68f);
                    rect.sizeDelta = new Vector2(68f, 28f);
                    inspectorBoundsButtons[index] = button;
                }
                inspectorSelectionPivotButton.gameObject.SetActive(false);
                inspectorActivePivotButton.gameObject.SetActive(false);
                inspectorPivotButton.gameObject.SetActive(false);
                boundsPivotLabel.gameObject.SetActive(false);
                foreach (Button button in inspectorBoundsButtons)
                    button.gameObject.SetActive(false);
                CreateAnchorControls(inspectorEditPanel.transform, 320f);
                inspectorEditPanel.SetActive(false);

                groupEditPanel = CreateRect("GroupEdit", inspectorBody).gameObject;
                SetInsets((RectTransform)groupEditPanel.transform, 0f, 0f, 0f, 0f);
                CreateInspectorLabel(groupEditPanel.transform, T("editor.view.name"), 0f);
                groupName = CreateInput(
                    "GroupName", groupEditPanel.transform, new Vector2(0f, -24f), 228f);
                groupName.onSubmit.AddListener(_ => SubmitDetailName());
                detailInfo = CreateText(
                    "DetailInfo", groupEditPanel.transform, string.Empty, 12f,
                    FontStyles.Normal, TextAlignmentOptions.TopLeft);
                detailInfo.textWrappingMode = TextWrappingModes.Normal;
                SetTopAnchored((RectTransform)detailInfo.transform, 0f, 72f, 0f, 82f);
                detailVisibilityButton = CreateButton(
                    "DetailVisibility", groupEditPanel.transform, T("editor.view.visibility"), 228f,
                    ToggleDetailVisibility);
                SetInspectorButton((RectTransform)detailVisibilityButton.transform, 0f, 160f, 228f);
                detailLockButton = CreateButton(
                    "DetailLock", groupEditPanel.transform, T("editor.view.lock"), 228f,
                    ToggleDetailLock);
                SetInspectorButton((RectTransform)detailLockButton.transform, 0f, 204f, 228f);
                detailGroupButton = CreateButton(
                    "DetailGroup", groupEditPanel.transform, T("editor.view.group_root"), 228f,
                    CycleDetailGroup);
                SetInspectorButton((RectTransform)detailGroupButton.transform, 0f, 248f, 228f);
                Button applyGroup = CreateButton(
                    "ApplyDetailName", groupEditPanel.transform, T("editor.view.apply_name"), 228f,
                    SubmitDetailName, primary: true);
                SetInspectorButton((RectTransform)applyGroup.transform, 0f, 292f, 228f);
                groupEditPanel.SetActive(false);

                blueprintEditPanel = CreateRect("BlueprintEdit", inspectorBody).gameObject;
                SetInsets((RectTransform)blueprintEditPanel.transform, 0f, 0f, 0f, 0f);
                CreateInspectorLabel(blueprintEditPanel.transform, T("editor.view.blueprint_name"), 0f);
                blueprintName = CreateInput(
                    "BlueprintName", blueprintEditPanel.transform, new Vector2(0f, -24f), 228f);
                CreateInspectorLabel(blueprintEditPanel.transform, T("editor.view.category"), 72f);
                blueprintCategory = CreateInput(
                    "BlueprintCategory", blueprintEditPanel.transform,
                    new Vector2(0f, -96f), 228f);
                blueprintName.onSubmit.AddListener(_ => SubmitBlueprintMetadata());
                blueprintCategory.onSubmit.AddListener(_ => SubmitBlueprintMetadata());
                blueprintInfo = CreateText(
                    "BlueprintInfo", blueprintEditPanel.transform, string.Empty, 12f,
                    FontStyles.Normal, TextAlignmentOptions.TopLeft);
                blueprintInfo.textWrappingMode = TextWrappingModes.Normal;
                SetTopAnchored((RectTransform)blueprintInfo.transform, 0f, 142f, 0f, 92f);
                Button frameBlueprint = CreateButton(
                    "FrameBlueprint", blueprintEditPanel.transform, T("editor.view.frame_blueprint"), 228f,
                    () => FrameAllRequested?.Invoke());
                SetInspectorButton((RectTransform)frameBlueprint.transform, 0f, 240f, 228f);
                Button applyBlueprint = CreateButton(
                    "ApplyBlueprint", blueprintEditPanel.transform, T("editor.view.apply"), 228f,
                    SubmitBlueprintMetadata, primary: true);
                SetInspectorButton((RectTransform)applyBlueprint.transform, 0f, 284f, 228f);
                blueprintEditPanel.SetActive(false);

                arrayEditPanel = CreateRect("ArrayEdit", inspectorBody).gameObject;
                SetInsets((RectTransform)arrayEditPanel.transform, 0f, 0f, 0f, 0f);
                arrayCount[0] = CreateArrayField("ArrayCountX", T("editor.view.in_row"), 0f, 0f, 1f, 1f, 128f, true, 2f);
                arrayCount[1] = CreateArrayField("ArrayCountY", T("editor.view.rows"), 118f, 0f, 1f, 1f, 128f, true, 1f);
                arrayDistributionButton = CreateButton("ArrayDistribution", arrayEditPanel.transform,
                    T("editor.view.pack"), 110f, () => ArrayDistributionRequested?.Invoke());
                SetInspectorButton((RectTransform)arrayDistributionButton.transform, 0f, 44f, 110f);
                arrayStepButton = CreateButton("ArrayStep", arrayEditPanel.transform,
                    T("editor.array_no_gap"), 110f, () => ArrayStepRequested?.Invoke());
                SetInspectorButton((RectTransform)arrayStepButton.transform, 118f, 44f, 110f);
                AddTooltip((RectTransform)arrayStepButton.transform, T("editor.view.array_gap_tooltip"));
                arrayRise = CreateArrayField("ArrayRise", T("editor.view.rise_m"), 0f, 84f, .05f, -100f, 100f);
                arrayRotation = CreateArrayField("ArrayRotation", T("editor.view.turn_deg"), 118f, 84f, 1f, -360f, 360f);
                arrayPitch = CreateArrayField("ArrayPitch", T("editor.view.pitch_deg"), 0f, 128f, 1f, -360f, 360f);
                arrayRoll = CreateArrayField("ArrayRoll", T("editor.view.roll_deg"), 118f, 128f, 1f, -360f, 360f);
                arraySymmetryButton = CreateButton("ArraySymmetry", arrayEditPanel.transform,
                    T("editor.view.symmetry"), 110f, () => ArraySymmetryRequested?.Invoke());
                SetInspectorButton((RectTransform)arraySymmetryButton.transform, 0f, 172f, 110f);
                arrayScaleStepX = CreateArrayField("ArrayScaleX", T("editor.view.scale_step"), 118f, 168f, 1f, -300f, 300f);
                AddTooltip((RectTransform)arrayScaleStepX.transform, T("editor.view.scale_step_repeat_tooltip"));
                arrayMoveStepButton = CreateButton("ArrayMoveStep", arrayEditPanel.transform,
                    T("editor.view.move_step_value", "0.05"), 110f, () => ArrayMoveStepRequested?.Invoke());
                SetInspectorButton((RectTransform)arrayMoveStepButton.transform, 0f, 212f, 110f);
                arrayAngleStepButton = CreateButton("ArrayAngleStep", arrayEditPanel.transform,
                    T("editor.view.angle_step_value", "1"), 110f, () => ArrayAngleStepRequested?.Invoke());
                SetInspectorButton((RectTransform)arrayAngleStepButton.transform, 118f, 212f, 110f);
                AddTooltip((RectTransform)arrayMoveStepButton.transform,
                    T("editor.view.array_move_step_tooltip"));
                AddTooltip((RectTransform)arrayAngleStepButton.transform,
                    T("editor.view.array_angle_step_tooltip"));
                arrayInfo = CreateText("ArrayInfo", arrayEditPanel.transform, arrayDirectionInfo,
                    11f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
                arrayInfo.color = MutedColor;
                arrayInfo.textWrappingMode = TextWrappingModes.Normal;
                SetTopAnchored((RectTransform)arrayInfo.transform, 0f, 254f, 0f, 24f);
                arrayApplyButton = CreateButton("ApplyArray", arrayEditPanel.transform, T("editor.view.apply"), 110f,
                    () => ApplyArrayRequested?.Invoke(), primary: true);
                SetInspectorButton((RectTransform)arrayApplyButton.transform, 0f, 280f, 110f);
                Button cancelArray = CreateButton("CancelArray", arrayEditPanel.transform, T("editor.view.cancel"), 110f,
                    () => CancelArrayRequested?.Invoke());
                SetInspectorButton((RectTransform)cancelArray.transform, 118f, 280f, 110f);
                CreateAnchorControls(arrayEditPanel.transform, 320f);
                arrayCount[0].SetTextWithoutNotify("2");
                arrayCount[1].SetTextWithoutNotify("1");
                foreach (TMP_InputField field in new[] { arrayCount[0], arrayCount[1], arrayRise,
                arrayRotation, arrayPitch, arrayRoll, arrayScaleStepX })
                {
                    field.onValueChanged.AddListener(_ => SubmitArrayPreview(clearOnInvalid: false));
                    field.onEndEdit.AddListener(_ => SubmitArrayPreview(clearOnInvalid: true));
                }
                BlueprintEditorNumericScrub riseScrub = arrayRise.GetComponent<BlueprintEditorNumericScrub>();
                riseScrub.StepProvider = riseScrub.UnitsPerPixelProvider = () => translationStep;
                foreach (TMP_InputField field in new[] { arrayRotation, arrayPitch, arrayRoll })
                {
                    BlueprintEditorNumericScrub scrub = field.GetComponent<BlueprintEditorNumericScrub>();
                    scrub.StepProvider = scrub.UnitsPerPixelProvider = () => rotationStep;
                }
                arrayEditPanel.SetActive(false);

                contourEditPanel = CreateRect("ContourEdit", inspectorBody).gameObject;
                SetInsets((RectTransform)contourEditPanel.transform, 0f, 0f, 0f, 0f);
                CreateInspectorLabel(contourEditPanel.transform, T("editor.view.contour"), 0f);
                contourInfo = CreateText(
                    "ContourInfo", contourEditPanel.transform, string.Empty, 13f,
                    FontStyles.Normal, TextAlignmentOptions.TopLeft);
                contourInfo.textWrappingMode = TextWrappingModes.Normal;
                SetTopAnchored((RectTransform)contourInfo.transform, 0f, 32f, 0f, 144f);
                CreateCompactLabel(contourEditPanel.transform, T("editor.view.step_percent"), 184f);
                contourScaleStep = CreateInput(
                    "ContourScale", contourEditPanel.transform, new Vector2(92f, -182f),
                    136f, 1f, -99f, 300f, true);
                contourScaleStep.SetTextWithoutNotify("0");
                contourScaleStep.onEndEdit.AddListener(_ => SubmitContourPreview());
                contourApplyButton = CreateButton(
                    "ApplyContour", contourEditPanel.transform, T("editor.view.apply"), 228f,
                    () => ApplyContourRequested?.Invoke(), primary: true);
                SetInspectorButton((RectTransform)contourApplyButton.transform, 0f, 228f, 110f);
                Button cancelContour = CreateButton(
                    "CancelContour", contourEditPanel.transform, T("editor.view.cancel"), 228f,
                    () => CancelContourRequested?.Invoke());
                SetInspectorButton((RectTransform)cancelContour.transform, 118f, 228f, 110f);
                CreateAnchorControls(contourEditPanel.transform, 274f);
                contourEditPanel.SetActive(false);

                statusText = CreateText("StatusText", status,
                    T("editor.view.viewport_help"),
                    13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                statusText.color = MutedColor;
                statusText.raycastTarget = false;
                statusText.overflowMode = TextOverflowModes.Ellipsis;
                SetInsets((RectTransform)statusText.transform, 12f, 2f, 12f, 24f);
                statusHintText = CreateText("StatusHints", status, string.Empty,
                    12f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                statusHintText.color = TextColor;
                statusHintText.raycastTarget = false;
                statusHintText.overflowMode = TextOverflowModes.Ellipsis;
                SetInsets((RectTransform)statusHintText.transform, 12f, 24f, 12f, 2f);

                tooltip = CreatePanel("Tooltip", safeRoot, new Color(0.06f, 0.07f, 0.08f, 0.99f), BlueprintEditorSkin.Surface.Tooltip);
                tooltip.GetComponent<Image>().raycastTarget = false;
                tooltip.anchorMin = tooltip.anchorMax = new Vector2(0.5f, 0.5f);
                tooltip.sizeDelta = new Vector2(250f, 58f);
                tooltip.pivot = new Vector2(0f, 0.5f);
                tooltipText = CreateText("TooltipText", tooltip, string.Empty, 13f,
                    FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                SetInsets((RectTransform)tooltipText.transform, 10f, 4f, 10f, 4f);
                tooltip.gameObject.SetActive(false);

                viewportControls = CreatePanel("ViewportControls", viewport, Color.clear, null);
                SetTopRight(viewportControls, 12f, 12f, 84f, 38f);
                gridButton = CreateIconButton("GridToggle", viewportControls, null, "#", 38f,
                    () => { showGrid = !showGrid; PublishViewportSettings(); }, T("editor.view.grid_tooltip"));
                SetTopLeft((RectTransform)gridButton.transform, 0f, 0f, 38f, 38f);
                projectionButton = CreateIconButton("ProjectionToggle", viewportControls, "frame", "◈", 38f,
                    () => { SetProjection(!orthographic); ProjectionChanged?.Invoke(orthographic); },
                    T("editor.view.projection_tooltip"));
                SetTopLeft((RectTransform)projectionButton.transform, 46f, 0f, 38f, 38f);
                SetSelected(gridButton, showGrid);
                viewportSettings = CreatePanel("ViewportSettings", viewport, PanelRaised).gameObject;
                SetTopRight((RectTransform)viewportSettings.transform, 12f, 56f, 320f, 454f);
                TMP_Text viewTitle = CreateText("ViewportSettingsTitle", viewportSettings.transform,
                    T("editor.view.viewport_settings"), 15f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                SetTopLeft((RectTransform)viewTitle.transform, 12f, 8f, 240f, 28f);
                Button viewClose = CreateButton("ViewportSettingsClose", viewportSettings.transform,
                    "×", 32f, HideViewportSettings);
                SetTopRight((RectTransform)viewClose.transform, 8f, 8f, 32f, 28f);
                Button frameAll = CreateButton("ViewportFrameAll", viewportSettings.transform, T("editor.view.frame_all"), 142f,
                    () => FrameAllRequested?.Invoke());
                SetTopLeft((RectTransform)frameAll.transform, 12f, 44f, 142f, 32f);
                Button frameSelected = CreateButton("ViewportFrameSelection", viewportSettings.transform, T("editor.view.frame_selected"), 142f,
                    () => FrameSelectionRequested?.Invoke());
                SetTopLeft((RectTransform)frameSelected.transform, 164f, 44f, 142f, 32f);
                TMP_Text sizeTitle = CreateText("GizmoSizeTitle", viewportSettings.transform,
                    T("editor.view.gizmo_size"), 12f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                SetTopLeft((RectTransform)sizeTitle.transform, 12f, 86f, 294f, 26f);
                string[] sizeLabels = {
                T("editor.view.move_arrows"),
                T("editor.view.rotation_rings"),
                T("editor.view.array_arrows"),
                T("editor.view.points"),
                T("editor.view.scale_handle") };
                for (int index = 0; index < viewportScaleInputs.Length; ++index)
                {
                    TMP_Text sizeLabel = CreateText("GizmoSizeLabel" + index, viewportSettings.transform,
                        sizeLabels[index], 13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                    SetTopLeft((RectTransform)sizeLabel.transform, 12f, 118f + index * 38f, 192f, 32f);
                    TMP_InputField sizeInput = CreateInput("GizmoSize" + index, viewportSettings.transform,
                        new Vector2(212f, -118f - index * 38f), 94f, 1f, 50f, 250f, true, 100f);
                    ((RectTransform)sizeInput.transform).sizeDelta = new Vector2(94f, 32f);
                    sizeInput.SetTextWithoutNotify("100");
                    sizeInput.onEndEdit.AddListener(_ => PublishViewportSettings());
                    viewportScaleInputs[index] = sizeInput;
                }
                TMP_Text scaleStepLabel = CreateText("ScaleStepLabel", viewportSettings.transform,
                    T("editor.view.scale_step_label"), 13f, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                SetTopLeft(scaleStepLabel.rectTransform, 12f, 310f, 192f, 32f);
                scaleStepInput = CreateInput("ScaleStepPercent", viewportSettings.transform,
                    new Vector2(212f, -310f), 94f, .1f, .1f, 100f, resetValue: 10f);
                ((RectTransform)scaleStepInput.transform).sizeDelta = new Vector2(94f, 32f);
                scaleStepInput.SetTextWithoutNotify("10");
                scaleStepInput.onEndEdit.AddListener(_ => PublishScaleStep());
                AddTooltip(scaleStepInput.transform as RectTransform,
                    T("editor.view.scale_step_tooltip"));
                interfaceScaleButton = CreateButton("InterfaceScale", viewportSettings.transform,
                    T("editor.view.interface_scale", 100), 294f, CycleUiScale);
                SetTopLeft((RectTransform)interfaceScaleButton.transform, 12f, 354f, 294f, 36f);
                Button resetView = CreateButton("ViewportSettingsReset", viewportSettings.transform,
                    T("editor.view.reset_settings"), 294f, () =>
                    {
                        scaleStepInput.SetTextWithoutNotify("10");
                        PublishScaleStep();
                        SetViewportSettings(1f, 1f, 1f, 1f, 1f, true);
                        PublishViewportSettings();
                    });
                SetTopLeft((RectTransform)resetView.transform, 12f, 404f, 294f, 36f);
                viewportSettings.SetActive(false);

                catalog = CreatePanel(
                    "CatalogOverlay", safeRoot, new Color(0f, 0f, 0f, 0.72f), null).gameObject;
                catalogPanel = CreatePanel("Catalog", catalog.transform, PanelRaised);
                catalogPanel.anchorMin = catalogPanel.anchorMax = new Vector2(0.5f, 0.5f);
                catalogPanel.pivot = new Vector2(0.5f, 0.5f);
                catalogPanel.sizeDelta = new Vector2(1420f, 680f);
                TMP_Text catalogTitle = CreateText(
                    "CatalogTitle", catalogPanel, T("editor.view.catalog"), 18f,
                    FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                SetTopAnchored((RectTransform)catalogTitle.transform, 324f, 12f, 120f, 44f);
                catalogPartsModeButton = CreateButton("CatalogPartsMode", catalogPanel,
                    T("editor.view.parts"), 140f, () => SetCatalogMode(false), true);
                SetTopLeft((RectTransform)catalogPartsModeButton.transform, 20f, 12f, 140f, 44f);
                catalogBlueprintsModeButton = CreateButton("CatalogBlueprintsMode", catalogPanel,
                    T("editor.view.blueprints"), 140f, () => SetCatalogMode(true));
                SetTopLeft((RectTransform)catalogBlueprintsModeButton.transform, 168f, 12f, 140f, 44f);
                Button catalogClose = CreateButton(
                    "CatalogClose", catalogPanel, T("editor.view.close"), 88f, HideCatalog);
                RectTransform catalogCloseRect = (RectTransform)catalogClose.transform;
                catalogCloseRect.anchorMin = catalogCloseRect.anchorMax = new Vector2(1f, 1f);
                catalogCloseRect.pivot = new Vector2(1f, 1f);
                catalogCloseRect.anchoredPosition = new Vector2(-16f, -12f);
                catalogCloseRect.sizeDelta = new Vector2(88f, 44f);
                RectTransform catalogSearchPanel = CreatePanel(
                    "CatalogSearch", catalogPanel, ButtonColor);
                SetTopAnchored(catalogSearchPanel, 20f, 64f, 20f, 36f);
                TMP_Text catalogSearchText = CreateText(
                    "Text", catalogSearchPanel, string.Empty, 13f,
                    FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                catalogSearchText.raycastTarget = true;
                SetInsets((RectTransform)catalogSearchText.transform, 8f, 0f, 8f, 0f);
                TMP_Text catalogSearchPlaceholder = CreateText(
                    "Placeholder", catalogSearchPanel, T("editor.view.catalog_search"), 13f,
                    FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                catalogSearchPlaceholder.color = MutedColor;
                SetInsets((RectTransform)catalogSearchPlaceholder.transform, 8f, 0f, 8f, 0f);
                catalogSearch = catalogSearchPanel.gameObject.AddComponent<TMP_InputField>();
                skin.Apply(catalogSearch, input: true);
                catalogSearch.textComponent = catalogSearchText;
                catalogSearch.placeholder = catalogSearchPlaceholder;
                catalogSearch.lineType = TMP_InputField.LineType.SingleLine;
                catalogSearch.characterLimit = 48;
                catalogSearch.onValueChanged.AddListener(_ =>
                {
                    catalogPage = 0;
                    RebuildCatalog();
                });
                catalogCategoryTabs = CreateCatalogFilterStrip("CatalogCategoryTabs", T("editor.view.part_type"), 108f);
                catalogSources = CreateCatalogFilterStrip("CatalogSources", T("editor.view.source"), 150f);
                catalogMaterialTitle = CreateText("CatalogMaterialTitle", catalogPanel,
                    T("editor.view.material"), 13f, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                SetTopLeft((RectTransform)catalogMaterialTitle.transform, 20f, 196f, 308f, 28f);
                RectTransform catalogCategoryViewport = CreateRect(
                    "CatalogCategoryViewport", catalogPanel);
                SetTopLeft(catalogCategoryViewport, 20f, 232f, 308f, 360f);
                catalogCategoryViewport.gameObject.AddComponent<RectMask2D>();
                catalogCategories = CreateRect("CatalogCategories", catalogCategoryViewport);
                catalogCategories.anchorMin = new Vector2(0f, 1f);
                catalogCategories.anchorMax = new Vector2(1f, 1f);
                catalogCategories.pivot = new Vector2(0.5f, 1f);
                catalogCategories.anchoredPosition = Vector2.zero;
                var categoryLayout = catalogCategories.gameObject.AddComponent<GridLayoutGroup>();
                categoryLayout.cellSize = new Vector2(152f, 36f);
                categoryLayout.spacing = new Vector2(4f, 4f);
                categoryLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                categoryLayout.constraintCount = 2;
                ContentSizeFitter categoryFitter =
                    catalogCategories.gameObject.AddComponent<ContentSizeFitter>();
                categoryFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                ScrollRect categoryScroll =
                    catalogCategoryViewport.gameObject.AddComponent<ScrollRect>();
                catalogMaterialScroll = categoryScroll;
                categoryScroll.horizontal = false;
                categoryScroll.vertical = true;
                categoryScroll.movementType = ScrollRect.MovementType.Clamped;
                categoryScroll.scrollSensitivity = 120f;
                categoryScroll.viewport = catalogCategoryViewport;
                categoryScroll.content = catalogCategories;
                RectTransform catalogViewport = CreateRect("CatalogViewport", catalogPanel);
                SetTopLeft(
                    catalogViewport,
                    348f,
                    196f,
                    (float)BlueprintEditorLayout.CatalogGridWidth,
                    (float)BlueprintEditorLayout.CatalogGridHeight);
                catalogViewport.gameObject.AddComponent<RectMask2D>();
                catalogViewport.gameObject.AddComponent<Image>().color = Color.clear;
                catalogViewport.gameObject.AddComponent<BlueprintEditorCatalogScroll>().Scroll = delta =>
                {
                    if (!HasCatalog || HasModal || IsTextInputFocused) return;
                    if (delta < 0f) NextCatalogPage();
                    else if (delta > 0f) PreviousCatalogPage();
                };
                catalogGrid = CreateRect("CatalogGrid", catalogViewport);
                catalogGrid.anchorMin = new Vector2(0f, 1f);
                catalogGrid.anchorMax = new Vector2(1f, 1f);
                catalogGrid.pivot = new Vector2(0.5f, 1f);
                var catalogLayout = catalogGrid.gameObject.AddComponent<GridLayoutGroup>();
                catalogLayout.cellSize = new Vector2(
                    (float)BlueprintEditorLayout.CatalogCellWidth,
                    (float)BlueprintEditorLayout.CatalogCellHeight);
                catalogLayout.spacing = Vector2.one * (float)BlueprintEditorLayout.CatalogCellGap;
                catalogLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                catalogLayout.constraintCount = BlueprintEditorLayout.CatalogColumns;
                catalogLayout.childAlignment = TextAnchor.UpperLeft;
                ContentSizeFitter catalogFitter =
                    catalogGrid.gameObject.AddComponent<ContentSizeFitter>();
                catalogFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                catalogPreviousPage = CreateButton(
                    "CatalogPrevious", catalogPanel, "<", 44f, PreviousCatalogPage);
                SetBottomLeft((RectTransform)catalogPreviousPage.transform, 348f, 14f, 44f, 36f);
                catalogPageText = CreateText(
                    "CatalogPage", catalogPanel, "1 / 1", 13f,
                    FontStyles.Bold, TextAlignmentOptions.Center);
                RectTransform catalogPageRect = (RectTransform)catalogPageText.transform;
                catalogPageRect.anchorMin = catalogPageRect.anchorMax = new Vector2(0f, 0f);
                catalogPageRect.pivot = new Vector2(0f, 0f);
                catalogPageRect.anchoredPosition = new Vector2(400f, 14f);
                catalogPageRect.sizeDelta = new Vector2(100f, 36f);
                catalogNextPage = CreateButton(
                    "CatalogNext", catalogPanel, ">", 44f, NextCatalogPage);
                SetBottomLeft((RectTransform)catalogNextPage.transform, 508f, 14f, 44f, 36f);
                catalogPageInput = CreateInput("CatalogPageJump", catalogPanel,
                    new Vector2(570f, -630f), 68f, 0f, resetValue: 1f);
                ((RectTransform)catalogPageInput.transform).sizeDelta = new Vector2(68f, 36f);
                catalogPageInput.contentType = TMP_InputField.ContentType.IntegerNumber;
                catalogPageInput.onEndEdit.AddListener(_ => JumpCatalogPage());
                TMP_Text pageHint = CreateText("CatalogPageHint", catalogPanel,
                    T("editor.view.page_help"), 12f,
                    FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                SetTopLeft((RectTransform)pageHint.transform, 648f, 630f, 600f, 36f);
                catalogBreadcrumb = CreateText("CatalogBreadcrumb", catalogPanel, string.Empty, 12f,
                    FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                catalogBreadcrumb.overflowMode = TextOverflowModes.Ellipsis;
                SetTopLeft((RectTransform)catalogBreadcrumb.transform, 20f, 598f, 1380f, 28f);
                catalogMaterialsPrevious = CreateButton("MaterialsPageUp", catalogPanel, T("editor.view.materials_up"), 148f,
                    () => ScrollCatalogMaterials(1f));
                SetBottomLeft((RectTransform)catalogMaterialsPrevious.transform, 20f, 14f, 148f, 36f);
                catalogMaterialsNext = CreateButton("MaterialsPageDown", catalogPanel, T("editor.view.materials_down"), 152f,
                    () => ScrollCatalogMaterials(-1f));
                SetBottomLeft((RectTransform)catalogMaterialsNext.transform, 176f, 14f, 152f, 36f);
                catalog.SetActive(false);

                modal = CreatePanel("Modal", safeRoot, new Color(0f, 0f, 0f, 0.72f), null).gameObject;
                RectTransform dialog = CreatePanel("Dialog", modal.transform, PanelRaised);
                dialog.anchorMin = dialog.anchorMax = new Vector2(0.5f, 0.5f);
                dialog.pivot = new Vector2(0.5f, 0.5f);
                dialog.sizeDelta = new Vector2(520f, 260f);
                modalTitle = CreateText("DialogTitle", dialog, T("editor.view.unsaved_title"), 18f,
                    FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                SetTopAnchored((RectTransform)modalTitle.transform, 20f, 18f, 20f, 36f);
                modalMessage = CreateText("DialogMessage", dialog, string.Empty, 15f,
                    FontStyles.Normal, TextAlignmentOptions.TopLeft);
                modalMessage.textWrappingMode = TextWrappingModes.Normal;
                SetInsets((RectTransform)modalMessage.transform, 20f, 64f, 20f, 90f);
                dialogPrimaryButton = CreateDialogButton(
                    dialog, "SaveExit", T("editor.view.save_exit"), 20f,
                    () => DialogDecided?.Invoke(errorDialog
                        ? BlueprintEditorDialogDecision.Retry
                        : BlueprintEditorDialogDecision.SaveAndExit), true);
                dialogDiscardButton = CreateDialogButton(
                    dialog, "Discard", T("editor.view.discard_exit"), 190f,
                    () => DialogDecided?.Invoke(BlueprintEditorDialogDecision.DiscardAndExit), false);
                dialogCancelButton = CreateDialogButton(
                    dialog, "Cancel", T("editor.view.cancel"), 380f,
                    () => DialogDecided?.Invoke(errorDialog
                        ? BlueprintEditorDialogDecision.Acknowledge
                        : BlueprintEditorDialogDecision.Cancel), false);
                modal.SetActive(false);

                foreach (RectTransform panel in new[] { top, rail, viewport, rightColumn,
                outliner, inspector, status })
                    modalBlockedGroups.Add(panel.gameObject.AddComponent<CanvasGroup>());

                SetTool(BlueprintEditorTool.Select);
                ApplySafeAreaAndLayout(force: true);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal event Action CloseRequested;
        internal event Action SaveRequested;
        internal event Action UndoRequested;
        internal event Action RedoRequested;
        internal event Action GroupRequested;
        internal event Action CreateGroupRequested;
        internal event Action ShowAllRequested;
        internal event Action HideSelectionRequested;
        internal event Action LockSelectionRequested;
        internal event Action UngroupRequested;
        internal event Action<string> MoveSelectionToGroupRequested;
        internal event Action DuplicateSelectionRequested;
        internal event Action DeleteSelectionRequested;
        internal event Action<string> PrimaryPartRequested;
        internal event Action<string> PrimaryGroupRequested;
        internal event Action<string, string> GroupPivotRequested;
        internal event Action<float> UiScaleChanged;
        internal event Action<float, float> SnapChanged;
        internal event Action SpaceRequested;
        internal event Action AnchorVisibilityRequested;
        internal event Action MeshSnapRequested;
        internal event Action PinSelectedAnchorRequested;
        internal event Action<GizmoAxis> AnchorConstraintRequested;
        internal event Action PivotRequested;
        internal event Action<BlueprintEditorPivotMode> PivotModeRequested;
        internal event Action<int> BoundsPivotRequested;
        internal event Action ResetTransformRequested;
        internal event Action FrameAllRequested;
        internal event Action FrameSelectionRequested;
        internal event Action<BlueprintEditorTool> ToolSelected;
        internal event Action<BlueprintEditorLightingPreset> LightingChanged;
        internal event Action<float, float, float, float, float, bool> ViewportSettingsChanged;
        internal event Action<bool> ProjectionChanged;
        internal event Action<string, bool, bool> NodeClicked;
        internal event Action<string, bool> VisibilityChanged;
        internal event Action<string, bool> LockChanged;
        internal event Action<BlueprintEditorDialogDecision> DialogDecided;
        internal event Action<BlueprintEditorCatalogItem, bool> CatalogPartSelected;
        internal event Action<string, string, Vector3, Vector3, float, bool> InspectorSubmitted;
        internal event Action<string, string> RenameSubmitted;
        internal event Action<string, string> BlueprintMetadataSubmitted;
        internal event Action<bool> SelectionVisibilityRequested;
        internal event Action<bool> SelectionLockRequested;
        internal event Action<int, int, float, float, float, float, float> ArrayChanged;
        internal event Action ArrayDistributionRequested;
        internal event Action ArrayStepRequested;
        internal event Action ArrayMoveStepRequested;
        internal event Action ArrayAngleStepRequested;
        internal event Action ArraySymmetryRequested;
        internal event Action ApplyArrayRequested;
        internal event Action CancelArrayRequested;
        internal event Action ApplyContourRequested;
        internal event Action CancelContourRequested;
        internal event Action<float> ContourChanged;

        internal RectTransform Viewport => viewport;
        internal Canvas RootCanvas => canvas;
        internal bool HasModal => modal.activeSelf;
        internal bool HasCatalog => catalog.activeSelf;
        internal bool HasOutlinerMenu => outlinerMenu.activeSelf || outlinerContextMenu.gameObject.activeSelf;
        internal bool HasOutlinerContextMenu => outlinerContextMenu.gameObject.activeSelf;
        internal bool HasViewportSettings => viewportSettings.activeSelf;
        internal bool IsNumericScrubbing
        {
            get
            {
                foreach (BlueprintEditorNumericScrub scrub in numericScrubs)
                    if (scrub && scrub.Dragging) return true;
                return false;
            }
        }
        internal bool IsTextInputFocused
        {
            get
            {
                GameObject selected = EventSystem.current
                    ? EventSystem.current.currentSelectedGameObject
                    : null;
                TMP_InputField input = selected
                    ? selected.GetComponentInParent<TMP_InputField>()
                    : null;
                return input && input.isFocused;
            }
        }

        internal void CancelTextEdit()
        {
            GameObject selected = EventSystem.current
                ? EventSystem.current.currentSelectedGameObject
                : null;
            TMP_InputField input = selected
                ? selected.GetComponentInParent<TMP_InputField>()
                : null;
            if (input && input != outlinerSearch && input != catalogSearch &&
                boundDocument != null)
                BindInspector(boundDocument);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        internal void FocusMoveStep()
        {
            if (!inspectorMoveStep || !inspectorMoveStep.gameObject.activeInHierarchy) return;
            inspectorMoveStep.Select();
            inspectorMoveStep.ActivateInputField();
        }

        internal Rect ViewportScreenRect()
        {
            var corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 minimum = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 maximum = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
            return Rect.MinMaxRect(minimum.x, minimum.y, maximum.x, maximum.y);
        }

        internal void SetUiScale(float value)
        {
            requestedUiScale = Mathf.Clamp(value, 0.6f, 1.4f);
            SetButtonLabel(interfaceScaleButton, T(
                "editor.view.interface_scale", Mathf.RoundToInt(requestedUiScale * 100f)));
            UpdateCanvasScale();
            ApplySafeAreaAndLayout(force: true);
        }

        private void CycleUiScale()
        {
            int percent = Mathf.RoundToInt(requestedUiScale * 100f);
            percent = percent >= 140 ? 60 : Mathf.Clamp(percent + 10, 60, 140);
            SetUiScale(percent / 100f);
            UiScaleChanged?.Invoke(requestedUiScale);
        }

        internal void Tick()
        {
            if (disposed) return;
            if (statusMessageUntil > 0f && Time.unscaledTime >= statusMessageUntil)
            {
                statusMessageUntil = 0f;
                statusMessage = string.Empty;
                RefreshStatusText();
            }
            ApplySafeAreaAndLayout(force: false);
            if (outlinerContextTracking && !input.GetMouseButton(1))
                ReleaseOutlinerContext(input.MousePosition);
            if (outlinerDragging) ScrollOutlinerDrag();
            if (HasCatalog && !HasModal && !IsTextInputFocused)
            {
                if (input.GetKeyDown(KeyCode.PageUp) || input.GetKeyDown(KeyCode.Q)) PreviousCatalogPage();
                else if (input.GetKeyDown(KeyCode.PageDown) || input.GetKeyDown(KeyCode.E)) NextCatalogPage();
                if (input.GetKeyDown(KeyCode.Home)) { catalogPage = 0; RebuildCatalog(); }
                if (input.GetKeyDown(KeyCode.End)) { catalogPage = catalogPageCount - 1; RebuildCatalog(); }
            }
            if (pendingTooltipAnchor && !tooltip.gameObject.activeSelf &&
                Time.unscaledTime >= tooltipAt)
                ShowTooltipNow();
            status.SetAsLastSibling();
        }

        internal void Bind(
            BlueprintEditorDocument document,
            bool canSave,
            IReadOnlyCollection<string> missingNodeIds,
            Vector3? boundsSize)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            boundDocument = document;
            blueprintBoundsSize = boundsSize;
            missingPrefabNodes.Clear();
            if (missingNodeIds != null)
                foreach (string stableId in missingNodeIds)
                    if (!string.IsNullOrEmpty(stableId)) missingPrefabNodes.Add(stableId);
            documentTitle.text = document.Name + (document.IsDirty ? "  •" : string.Empty);
            documentTitle.color = document.IsDirty ? AccentColor : TextColor;
            SetButtonInteractable(undoButton, document.CanUndo);
            SetButtonInteractable(redoButton, document.CanRedo);
            saveButton.interactable = canSave && document.IsDirty && document.Parts.Count >= 2;
            bool hasSelection = document.Selection.Count > 0;
            int editableCount = document.EditablePartSelectionCount;
            bool hasEditableSelection = editableCount > 0;
            foreach (KeyValuePair<BlueprintEditorTool, Button> entry in toolButtons)
                if (entry.Key != BlueprintEditorTool.Select)
                    SetAvailability(entry.Value,
                        hasEditableSelection,
                        hasSelection ? T("editor.view.selection_unavailable") :
                            T("editor.view.select_first"));
            SetAvailability(outlinerNewGroupButton, document.MovableNodeSelectionCount >= 2,
                T("editor.view.group_two_objects"));
            foreach (Button button in outlinerSelectionButtons)
                SetAvailability(button, button.name == "RenameSelection" ? document.Selection.Count == 1 : hasSelection,
                    button.name == "RenameSelection"
                        ? T("editor.view.rename_one_object")
                        : T("editor.view.select_object_or_group"));
            if (!hasEditableSelection && selectedTool != BlueprintEditorTool.Select)
            {
                SetTool(BlueprintEditorTool.Select);
                ToolSelected?.Invoke(BlueprintEditorTool.Select);
            }
            RebuildOutliner(document);
            BindInspector(document);
        }

        internal void SetTransformContext(
            bool localSpace,
            float moveStep,
            float angleStep,
            bool customPivot,
            BlueprintEditorPivotMode pivotMode,
            int boundsPivot)
        {
            localTransformSpace = localSpace;
            translationStep = moveStep;
            rotationStep = angleStep;
            customTransformPivot = customPivot;
            transformPivotMode = pivotMode;
            transformBoundsPivot = Mathf.Clamp(boundsPivot, 0, 8);
            UpdateTransformControls();
        }

        internal void SetGizmoOptions(bool showAllAnchors, bool meshSnapEnabled)
        {
            showAllTransformAnchors = showAllAnchors;
            transformMeshSnap = meshSnapEnabled;
            UpdateTransformControls();
        }

        internal void SetAnchorState(bool selected, bool selectedPinned, GizmoAxis constraint)
        {
            foreach (Button button in anchorPinButtons)
            {
                SetButtonInteractable(button, selected);
                SetButtonLabel(button, T(selectedPinned
                    ? "editor.view.unpin"
                    : "editor.view.pin"));
                SetSelected(button, selectedPinned);
            }
            for (int index = 0; index < anchorConstraintButtons.Count; ++index)
            {
                SetSelected(anchorConstraintButtons[index], index % 4 == (int)constraint);
            }
        }

        internal void SetArrayParameters(int countX, int countY, RepeatDistributionMode distribution,
            string stepLabel, bool symmetric, bool hasDirection, string stepInfo,
            float moveStep, float angleStep)
        {
            arrayCount[0].SetTextWithoutNotify(countX.ToString());
            arrayCount[1].SetTextWithoutNotify(countY.ToString());
            SetButtonLabel(arrayDistributionButton, T(distribution == RepeatDistributionMode.Pack
                ? "editor.view.pack"
                : distribution == RepeatDistributionMode.Fit
                    ? "editor.array_fit"
                    : "editor.view.exact_step"));
            SetButtonLabel(arrayStepButton, stepLabel);
            SetButtonInteractable(arrayStepButton, distribution != RepeatDistributionMode.Fit);
            SetButtonLabel(arrayMoveStepButton, T(
                "editor.view.move_step_value",
                moveStep.ToString("0.###", CultureInfo.InvariantCulture)));
            SetButtonLabel(arrayAngleStepButton, T(
                "editor.view.angle_step_value",
                angleStep.ToString("0.###", CultureInfo.InvariantCulture)));
            SetSelected(arraySymmetryButton, symmetric);
            arrayDirectionInfo = hasDirection
                ? stepInfo + "\n" + T("editor.view.array_wheel_help")
                : T("editor.view.array_direction");
        }

        private TMP_InputField CreateArrayField(string name, string label, float left, float top,
            float step, float minimum, float maximum, bool integer = false, float resetValue = 0f)
        {
            TMP_Text text = CreateText(name + "Label", arrayEditPanel.transform, label,
                10f, FontStyles.Bold, TextAlignmentOptions.Left);
            SetTopLeft(text.rectTransform, left, top, 110f, 16f);
            TMP_InputField field = CreateInput(name, arrayEditPanel.transform,
                new Vector2(left, -(top + 16f)), 110f, step, minimum, maximum, integer, resetValue);
            field.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 28f);
            field.SetTextWithoutNotify(resetValue.ToString("0.###", CultureInfo.InvariantCulture));
            return field;
        }

        internal void SetTool(BlueprintEditorTool tool)
        {
            selectedTool = tool;
            inspectorTab = 0;
            foreach (KeyValuePair<BlueprintEditorTool, Button> entry in toolButtons)
            {
                bool active = entry.Key == selectedTool;
                SetSelected(entry.Value, active);
            }
            if (boundDocument != null) BindInspector(boundDocument);
        }

        internal void SetContourState(
            string sourceName,
            int supportCount,
            int previewCount,
            bool closed)
        {
            string support = supportCount < 2
                ? T("editor.view.not_selected")
                : T(closed ? "editor.view.ring_parts" : "editor.view.chain_parts", supportCount);
            contourInfo.text = T(
                "editor.view.contour_info",
                string.IsNullOrEmpty(sourceName) ? "—" : sourceName,
                support,
                previewCount);
            SetButtonInteractable(contourApplyButton, supportCount >= 2 && previewCount > 0);
        }

        internal void SetArrayState(int previewCount, string warning = null)
        {
            arrayInfo.text = string.IsNullOrEmpty(warning)
                ? arrayDirectionInfo + (previewCount > 0
                    ? "\n" + T("editor.view.copies_enter_esc", previewCount)
                    : "")
                : warning;
            arrayInfo.color = string.IsNullOrEmpty(warning) ? MutedColor : AccentColor;
            SetButtonInteractable(arrayApplyButton,
                previewCount > 0 && string.IsNullOrEmpty(warning));
        }

        internal void SetStatus(string text, bool error = false)
        {
            statusMessageUntil = 0f;
            statusMessage = text ?? string.Empty;
            RefreshStatusText();
            statusText.color = error ? new Color(1f, 0.42f, 0.32f) : MutedColor;
        }

        internal void SetTransientStatus(string text, float seconds = 1.5f)
        {
            SetStatus(text);
            statusMessageUntil = Time.unscaledTime + Mathf.Max(0.1f, seconds);
        }

        internal void SetContextHints(string text) =>
            statusHintText.text = text ?? string.Empty;

        internal void SetOperationMetrics(int selectedCount, Vector3 translation,
            Vector3 rotationDegrees, float scale, int previewCount, int totalCount)
        {
            operationMetrics = T(
                "editor.view.operation_metrics",
                selectedCount,
                translation.x.ToString("0.##"),
                translation.y.ToString("0.##"),
                translation.z.ToString("0.##"),
                rotationDegrees.x.ToString("0.#"),
                rotationDegrees.y.ToString("0.#"),
                rotationDegrees.z.ToString("0.#"),
                (scale * 100f).ToString("0.#"),
                totalCount,
                previewCount > 0 ? T("editor.view.copy_suffix", previewCount) : string.Empty);
            RefreshStatusText();
        }

        private void RefreshStatusText() => statusText.text = operationMetrics +
            (string.IsNullOrEmpty(statusMessage) ? string.Empty : "\n" + statusMessage);

        internal void SetHoveredNode(string stableId)
        {
            if (hoveredNodeId == stableId) return;
            string previous = hoveredNodeId;
            hoveredNodeId = stableId;
            RefreshRowColor(previous);
            RefreshRowColor(stableId);
        }

        private void RefreshRowColor(string stableId)
        {
            if (stableId == null || boundDocument == null) return;
            Transform row = outlinerRows.Find("Row_" + stableId);
            if (row) row.GetComponent<Image>().color = Contains(boundDocument.Selection, stableId)
                ? SelectedColor : stableId == hoveredNodeId
                    ? new Color(0.07f, 0.22f, 0.32f, 1f) : ButtonColor;
        }

        internal void ShowUnsavedDialog(string blueprintName)
        {
            errorDialog = false;
            modalTitle.text = T("editor.view.unsaved_title");
            modalMessage.text = T("editor.view.unsaved_message", blueprintName);
            SetButtonLabel(dialogPrimaryButton, T("editor.view.save_exit"));
            SetButtonLabel(dialogCancelButton, T("editor.view.cancel"));
            dialogPrimaryButton.gameObject.SetActive(true);
            dialogDiscardButton.gameObject.SetActive(true);
            ShowDialog();
        }

        internal void ShowError(string titleText, string message, bool canRetry = true)
        {
            errorDialog = true;
            modalTitle.text = BuildWorksLocalization.ResolveUserText(titleText);
            modalMessage.text = BuildWorksLocalization.ResolveUserText(message);
            SetButtonLabel(dialogPrimaryButton, T("editor.view.retry"));
            SetButtonLabel(dialogCancelButton, T(canRetry
                ? "editor.view.close"
                : "editor.view.return"));
            dialogPrimaryButton.gameObject.SetActive(canRetry);
            dialogDiscardButton.gameObject.SetActive(false);
            ShowDialog();
        }

        internal void HideDialog()
        {
            modal.SetActive(false);
            foreach (CanvasGroup group in modalBlockedGroups) group.interactable = true;
        }

        internal void SetViewportSettings(float move, float rotation, float array,
            float points, float uniformScaleHandle, bool grid)
        {
            float[] values = { move, rotation, array, points, uniformScaleHandle };
            for (int index = 0; index < values.Length; ++index)
                viewportScaleInputs[index].SetTextWithoutNotify(
                    (Mathf.Clamp(values[index], 0.5f, 2.5f) * 100f).ToString("0", CultureInfo.InvariantCulture));
            showGrid = grid;
            SetSelected(gridButton, grid);
        }

        internal void SetProjection(bool value)
        {
            orthographic = value;
            SetSelected(projectionButton, value);
        }

        public bool IsViewportControlHit(Vector2 position)
        {
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return viewportControls.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(viewportControls, position, uiCamera);
        }

        private void PublishScaleStep()
        {
            if (!TryRead(scaleStepInput, out float value) || float.IsNaN(value) || float.IsInfinity(value)) value = 10f;
            ScaleStepPercent = Mathf.Clamp(value, .1f, 100f);
            scaleStepInput.SetTextWithoutNotify(ScaleStepPercent.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private void PublishViewportSettings()
        {
            var values = new float[viewportScaleInputs.Length];
            for (int index = 0; index < values.Length; ++index)
            {
                if (!TryRead(viewportScaleInputs[index], out float value) ||
                    float.IsNaN(value) || float.IsInfinity(value)) value = 100f;
                values[index] = Mathf.Clamp(value * 0.01f, 0.5f, 2.5f);
            }
            SetViewportSettings(values[0], values[1], values[2], values[3], values[4], showGrid);
            ViewportSettingsChanged?.Invoke(values[0], values[1], values[2], values[3], values[4], showGrid);
        }

        private void ToggleViewportSettings()
        {
            HideOutlinerMenu();
            viewportSettings.SetActive(!viewportSettings.activeSelf);
            EndTooltip();
        }

        internal void HideViewportSettings() => viewportSettings.SetActive(false);

        private RectTransform CreateCatalogFilterStrip(string name, string label, float top)
        {
            TMP_Text caption = CreateText(name + "Label", catalogPanel, label, 12f,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            SetTopLeft((RectTransform)caption.transform, 20f, top, 120f, 36f);
            RectTransform strip = CreatePanel(name + "Viewport", catalogPanel, Color.clear, null);
            SetTopLeft(strip, 148f, top, 1168f, 36f);
            strip.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect(name, strip);
            content.anchorMin = content.anchorMax = new Vector2(0f, 1f);
            content.pivot = new Vector2(0f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 36f);
            HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            content.gameObject.AddComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;
            ScrollRect scroll = strip.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true;
            scroll.vertical = false;
            scroll.scrollSensitivity = 160f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = strip;
            scroll.content = content;
            Button previous = CreateButton(name + "Previous", catalogPanel, "‹", 36f,
                () => ScrollFilterStrip(scroll, -1f));
            SetTopLeft((RectTransform)previous.transform, 1324f, top, 36f, 36f);
            Button next = CreateButton(name + "Next", catalogPanel, "›", 36f,
                () => ScrollFilterStrip(scroll, 1f));
            SetTopLeft((RectTransform)next.transform, 1364f, top, 36f, 36f);
            return content;
        }

        private static void ScrollFilterStrip(ScrollRect scroll, float direction)
        {
            Canvas.ForceUpdateCanvases();
            float extent = scroll.content.rect.width - scroll.viewport.rect.width;
            if (extent <= 0f) return;
            scroll.horizontalNormalizedPosition = Mathf.Clamp01(scroll.horizontalNormalizedPosition +
                direction * scroll.viewport.rect.width * 0.85f / extent);
        }

        private void ScrollCatalogMaterials(float direction)
        {
            Canvas.ForceUpdateCanvases();
            float extent = catalogCategories.rect.height - catalogMaterialScroll.viewport.rect.height;
            if (extent <= 0f) return;
            catalogMaterialScroll.verticalNormalizedPosition = Mathf.Clamp01(
                catalogMaterialScroll.verticalNormalizedPosition + direction * 320f / extent);
        }

        private void SetCatalogMode(bool blueprints)
        {
            catalogBlueprintMode = blueprints;
            selectedCatalogCategory = selectedCatalogMaterial = selectedCatalogSource = null;
            catalogPage = 0;
            catalogMaterialScroll.verticalNormalizedPosition = 1f;
            RebuildCatalogFilters();
            RebuildCatalog();
        }

        private void JumpCatalogPage()
        {
            if (int.TryParse(catalogPageInput.text, out int page))
                catalogPage = Mathf.Clamp(page - 1, 0, catalogPageCount - 1);
            RebuildCatalog();
        }

        private void FitCatalogFilterLabel(Button button, string fullLabel)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.enableAutoSizing = true;
            label.fontSizeMin = 9f;
            label.fontSizeMax = 12f;
            label.textWrappingMode = TextWrappingModes.Normal;
            AddTooltip((RectTransform)button.transform, fullLabel);
        }

        internal void ShowCatalog(IReadOnlyList<BlueprintEditorCatalogItem> items)
        {
            EndTooltip();
            HideViewportSettings();
            HideOutlinerMenu();
            shownCatalogItems.Clear();
            if (items != null)
                for (int index = 0; index < items.Count; ++index)
                    if (items[index] != null) shownCatalogItems.Add(items[index]);
            bool firstOpen = !catalogStateInitialized;
            if (firstOpen)
            {
                selectedCatalogCategory = null;
                selectedCatalogMaterial = null;
                selectedCatalogSource = null;
                catalogPage = 0;
                catalogBlueprintMode = false;
                catalogSearch.SetTextWithoutNotify(string.Empty);
                catalogStateInitialized = true;
            }
            RebuildCatalogFilters();
            RebuildCatalog();
            SetPanelsInteractable(false);
            catalog.SetActive(true);
            catalog.transform.SetAsLastSibling();
            status.SetAsLastSibling();
            if (firstOpen && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(catalogSearch.gameObject);
        }

        private void RebuildCatalog()
        {
            ClearChildren(catalogGrid);
            string filter = (catalogSearch.text ?? string.Empty).Trim();
            var filtered = new List<BlueprintEditorCatalogItem>();
            foreach (BlueprintEditorCatalogItem item in shownCatalogItems)
            {
                if (item.IsBlueprint != catalogBlueprintMode) continue;
                if (selectedCatalogCategory != null &&
                    !string.Equals(item.Category, selectedCatalogCategory,
                        StringComparison.CurrentCultureIgnoreCase)) continue;
                if (selectedCatalogMaterial != null &&
                    !string.Equals(item.Material, selectedCatalogMaterial,
                        StringComparison.CurrentCultureIgnoreCase)) continue;
                if (selectedCatalogSource != null &&
                    !string.Equals(item.Source, selectedCatalogSource,
                        StringComparison.CurrentCultureIgnoreCase)) continue;
                if (!Matches(item.DisplayName, filter) &&
                    !Matches(item.PrefabName, filter) &&
                    !Matches((item.Index + 1).ToString(), filter) &&
                    !Matches("#" + (item.Index + 1), filter)) continue;
                filtered.Add(item);
            }
            int pageSize = BlueprintEditorLayout.CatalogPageSize;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(filtered.Count / (float)pageSize));
            catalogPageCount = pageCount;
            catalogPage = Mathf.Clamp(catalogPage, 0, pageCount - 1);
            int first = catalogPage * pageSize;
            int last = Mathf.Min(first + pageSize, filtered.Count);
            for (int index = first; index < last; ++index)
            {
                BlueprintEditorCatalogItem item = filtered[index];
                BlueprintEditorCatalogItem captured = item;
                Button card = CreateButton(
                    "Catalog_" + item.PrefabName,
                    catalogGrid,
                    item.IsBlueprint ? item.DisplayName : "#" + (item.Index + 1),
                    (float)BlueprintEditorLayout.CatalogCellWidth,
                    () => CatalogPartSelected?.Invoke(captured, true));
                RectTransform cardRect = (RectTransform)card.transform;
                cardRect.sizeDelta = new Vector2(
                    (float)BlueprintEditorLayout.CatalogCellWidth,
                    (float)BlueprintEditorLayout.CatalogCellHeight);
                TMP_Text label = card.GetComponentInChildren<TMP_Text>();
                label.fontSize = 10f;
                label.alignment = TextAlignmentOptions.TopLeft;
                label.color = MutedColor;
                SetTopAnchored((RectTransform)label.transform, 5f, 3f, 5f, 18f);
                if (item.IsBlueprint)
                {
                    label.alignment = TextAlignmentOptions.Top;
                    label.color = TextColor;
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 8f;
                    label.fontSizeMax = 10f;
                    label.textWrappingMode = TextWrappingModes.Normal;
                    label.overflowMode = TextOverflowModes.Ellipsis;
                    label.maxVisibleLines = 2;
                    SetTopAnchored((RectTransform)label.transform, 4f, 66f, 4f, 26f);
                }
                if (item.Icon)
                {
                    RectTransform iconRect = CreateRect("Icon", card.transform);
                    iconRect.anchorMin = new Vector2(0.5f, 1f);
                    iconRect.anchorMax = new Vector2(0.5f, 1f);
                    iconRect.pivot = new Vector2(0.5f, 1f);
                    iconRect.anchoredPosition = new Vector2(0f, item.IsBlueprint ? -4f : -18f);
                    iconRect.sizeDelta = item.IsBlueprint ? new Vector2(60f, 60f) : new Vector2(68f, 68f);
                    Image image = iconRect.gameObject.AddComponent<Image>();
                    image.sprite = item.Icon;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
                BlueprintEditorHoverTarget hover =
                    card.gameObject.AddComponent<BlueprintEditorHoverTarget>();
                hover.Initialize(
                    "#" + (item.Index + 1) + " · " + item.DisplayName +
                    "\n" + BuildWorksLocalization.CatalogLabel(item.Material) + " · " +
                    BuildWorksLocalization.CatalogLabel(item.Source),
                    BeginTooltip,
                    EndTooltip);
            }
            catalogPageText.text = (catalogPage + 1) + " / " + pageCount;
            catalogPageInput.SetTextWithoutNotify((catalogPage + 1).ToString(CultureInfo.InvariantCulture));
            string categoryLabel = selectedCatalogCategory == null
                ? T("editor.view.all_types")
                : catalogBlueprintMode
                    ? BuildWorksLocalization.BlueprintCategoryLabel(selectedCatalogCategory)
                    : BuildWorksLocalization.CatalogLabel(selectedCatalogCategory);
            string materialLabel = selectedCatalogMaterial == null
                ? T("editor.view.all_materials")
                : BuildWorksLocalization.CatalogLabel(selectedCatalogMaterial);
            string sourceLabel = selectedCatalogSource == null
                ? T("editor.view.all_sources")
                : BuildWorksLocalization.CatalogLabel(selectedCatalogSource);
            catalogBreadcrumb.text = T(
                "editor.view.catalog_breadcrumb",
                catalogBlueprintMode ? T("editor.view.blueprints") : T("editor.view.parts"),
                categoryLabel,
                catalogBlueprintMode ? string.Empty : materialLabel + "  ›  ",
                sourceLabel,
                filtered.Count);
            catalogPreviousPage.interactable = catalogPage > 0;
            catalogNextPage.interactable = catalogPage + 1 < pageCount;
        }

        private void RebuildCatalogFilters()
        {
            ClearChildren(catalogCategories);
            ClearChildren(catalogCategoryTabs);
            ClearChildren(catalogSources);
            SetSelected(catalogPartsModeButton, !catalogBlueprintMode);
            SetSelected(catalogBlueprintsModeButton, catalogBlueprintMode);
            catalogMaterialTitle.text = catalogBlueprintMode
                ? T("editor.view.blueprint_category")
                : T("editor.view.material");
            catalogPanel.Find("CatalogCategoryTabsLabel").GetComponent<TMP_Text>().text =
                catalogBlueprintMode ? T("editor.view.type") : T("editor.view.part_type");
            SetButtonLabel(catalogMaterialsPrevious, catalogBlueprintMode
                ? T("editor.view.categories_up")
                : T("editor.view.materials_up"));
            SetButtonLabel(catalogMaterialsNext, catalogBlueprintMode
                ? T("editor.view.categories_down")
                : T("editor.view.materials_down"));
            if (!catalogBlueprintMode) AddCatalogMaterial(T("editor.view.all_materials"), null);
            AddCatalogCategory(catalogBlueprintMode
                ? T("editor.view.all_categories")
                : T("editor.view.all_types"), null);
            if (catalogBlueprintMode)
                CreateButton("BlueprintType", catalogCategoryTabs,
                    T("editor.view.saved_blueprints"), 240f, null).interactable = false;
            AddCatalogSource(T("editor.view.all_sources"), null);
            var categories = new SortedSet<string>(
                StringComparer.CurrentCultureIgnoreCase);
            var materials = new SortedSet<string>(
                StringComparer.CurrentCultureIgnoreCase);
            var sources = new SortedSet<string>(
                StringComparer.CurrentCultureIgnoreCase);
            foreach (BlueprintEditorCatalogItem item in shownCatalogItems)
            {
                if (item.IsBlueprint != catalogBlueprintMode) continue;
                categories.Add(item.Category);
                if (selectedCatalogCategory != null && !string.Equals(item.Category,
                    selectedCatalogCategory, StringComparison.CurrentCultureIgnoreCase)) continue;
                materials.Add(item.Material);
                if (selectedCatalogMaterial == null || string.Equals(item.Material,
                    selectedCatalogMaterial, StringComparison.CurrentCultureIgnoreCase)) sources.Add(item.Source);
            }
            foreach (string category in categories)
                AddCatalogCategory(category.ToUpperInvariant(), category);
            if (!catalogBlueprintMode)
                foreach (string material in materials)
                    AddCatalogMaterial(material.ToUpperInvariant(), material);
            foreach (string source in sources)
                AddCatalogSource(source.ToUpperInvariant(), source);
        }

        private void AddCatalogCategory(string label, string category)
        {
            string captured = category;
            Button button = CreateButton(
                "Category_" + (category ?? "All"),
                catalogBlueprintMode ? catalogCategories : catalogCategoryTabs,
                label,
                132f,
                () =>
                {
                    selectedCatalogCategory = captured;
                    selectedCatalogMaterial = selectedCatalogSource = null;
                    catalogMaterialScroll.verticalNormalizedPosition = 1f;
                    catalogPage = 0;
                    RebuildCatalogFilters();
                    RebuildCatalog();
                },
                string.Equals(selectedCatalogCategory, category,
                    StringComparison.CurrentCultureIgnoreCase));
            RectTransform rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(132f, 36f);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 132f;
            layout.minHeight = layout.preferredHeight = 36f;
            FitCatalogFilterLabel(button, label);
        }

        private void AddCatalogSource(string label, string source)
        {
            string captured = source;
            Button button = CreateButton(
                "Source_" + (source ?? "All"),
                catalogSources,
                label,
                152f,
                () =>
                {
                    selectedCatalogSource = captured;
                    catalogPage = 0;
                    RebuildCatalogFilters();
                    RebuildCatalog();
                },
                string.Equals(selectedCatalogSource, source,
                    StringComparison.CurrentCultureIgnoreCase));
            RectTransform rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(152f, 36f);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = 152f;
            layout.minHeight = layout.preferredHeight = 36f;
            FitCatalogFilterLabel(button, label);
        }

        private void AddCatalogMaterial(string label, string material)
        {
            string captured = material;
            Button button = CreateButton(
                "Material_" + (material ?? "All"),
                catalogCategories,
                label,
                116f,
                () =>
                {
                    selectedCatalogMaterial = captured;
                    selectedCatalogSource = null;
                    catalogPage = 0;
                    RebuildCatalogFilters();
                    RebuildCatalog();
                },
                string.Equals(selectedCatalogMaterial, material,
                    StringComparison.CurrentCultureIgnoreCase));
            RectTransform rect = (RectTransform)button.transform;
            rect.sizeDelta = new Vector2(116f, 36f);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 36f;
            FitCatalogFilterLabel(button, label);
        }

        private void PreviousCatalogPage()
        {
            if (catalogPage <= 0) return;
            --catalogPage;
            RebuildCatalog();
        }

        private void NextCatalogPage()
        {
            ++catalogPage;
            RebuildCatalog();
        }

        internal void HideCatalog()
        {
            if (!catalog.activeSelf) return;
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(null);
            catalog.SetActive(false);
            SetPanelsInteractable(true);
            EndTooltip();
        }

        internal void HideOutlinerMenu()
        {
            outlinerMenu.SetActive(false);
            outlinerContextMenu.gameObject.SetActive(false);
            outlinerContextTracking = false;
            outlinerRowViewport.gameObject.SetActive(true);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            EndOutlinerDrag();
            boundDocument = null;
            if (canvasObject)
            {
                canvasObject.SetActive(false);
                UnityEngine.Object.Destroy(canvasObject);
            }
            icons?.Dispose();
            skin.Dispose();
        }

        private void AddTool(
            Transform parent,
            BlueprintEditorTool tool,
            string iconName,
            string fallbackIcon,
            string tooltipValue)
        {
            Button button = AddCommand(parent, iconName, fallbackIcon, tooltipValue, () =>
            {
                SetTool(tool);
                ToolSelected?.Invoke(tool);
            });
            toolButtons.Add(tool, button);
        }

        private Button AddCommand(
            Transform parent,
            string iconName,
            string fallbackIcon,
            string tooltipValue,
            Action action)
        {
            Button button = CreateIconButton(
                "Tool_" + parent.childCount,
                parent,
                iconName,
                fallbackIcon,
                44f,
                action,
                tooltipValue);
            return button;
        }

        private void AddOutlinerDrop(RectTransform target, string stableId, bool group)
        {
            var drop = target.gameObject.AddComponent<BlueprintEditorOutlinerDrop>();
            drop.StableId = stableId;
            drop.IsGroup = group;
            drop.Enter = ShowOutlinerDrop;
            drop.Exit = current => { if (outlinerDropCue && outlinerDropCue.gameObject == current.gameObject) ClearOutlinerDropCue(); };
            drop.Drop = CommitOutlinerDrop;
        }

        private void BeginOutlinerDrag(PointerEventData data)
        {
            EndOutlinerDrag();
            if (data.button != PointerEventData.InputButton.Left || boundDocument == null) return;
            GameObject hit = data.pointerPressRaycast.gameObject;
            var source = hit ? hit.GetComponentInParent<BlueprintEditorOutlinerDrop>() : null;
            // Eye/lock/disclosure buttons remain clicks; dragging the row name moves the node.
            if (!source || source.StableId == null || hit.GetComponentInParent<Button>() != source.GetComponent<Button>() ||
                !boundDocument.IsEffectivelyVisible(source.StableId) || boundDocument.IsEffectivelyLocked(source.StableId)) return;
            string sourceId = source.StableId;
            bool selected = Contains(boundDocument.Selection, sourceId);
            if (!selected) NodeClicked?.Invoke(sourceId, false, false);
            if (!Contains(boundDocument.Selection, sourceId)) return;
            outlinerDragging = true;
            outlinerDragPosition = data.position;
            data.eligibleForClick = false;
            outlinerTitle.text = T("editor.view.move_to_root");
            EndTooltip();
        }

        private void ScrollOutlinerDrag()
        {
            if (!outlinerDragging) return;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(outlinerRowViewport, outlinerDragPosition, uiCamera, out Vector2 point)) return;
            Rect rect = outlinerRowViewport.rect;
            if (point.x < rect.xMin || point.x > rect.xMax) return;
            float direction = point.y > rect.yMax - 20f ? 1f : point.y < rect.yMin + 20f ? -1f : 0f;
            float overflow = outlinerRows.rect.height - rect.height;
            if (direction != 0f && overflow > 0f)
            {
                ScrollRect scroll = outliner.GetComponent<ScrollRect>();
                scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition +
                    direction * 240f * input.UnscaledDeltaTime / overflow);
            }
        }

        private bool CanDropOutliner(BlueprintEditorOutlinerDrop target)
        {
            if (!outlinerDragging || !target.IsGroup || boundDocument == null || HasModal || HasCatalog) return false;
            string id = target.StableId;
            if (id != null && (!boundDocument.IsEffectivelyVisible(id) || boundDocument.IsEffectivelyLocked(id))) return false;
            // Reject selected groups and their descendants before showing a valid target cue.
            for (string parent = id; parent != null;)
            {
                if (Contains(boundDocument.Selection, parent)) return false;
                string next = null;
                foreach (BlueprintEditorGroup group in boundDocument.Groups)
                    if (group.StableId == parent) { next = group.ParentGroupId; break; }
                parent = next;
            }
            return true;
        }

        private void ShowOutlinerDrop(BlueprintEditorOutlinerDrop target)
        {
            ClearOutlinerDropCue();
            if (!CanDropOutliner(target)) return;
            outlinerDropCue = target.GetComponent<Outline>();
            if (!outlinerDropCue) outlinerDropCue = target.gameObject.AddComponent<Outline>();
            if (target.StableId == null) target.GetComponent<Image>().color = ButtonColor;
            outlinerDropCue.effectColor = AccentColor;
            outlinerDropCue.effectDistance = new Vector2(2f, -2f);
            outlinerDropCue.enabled = true;
        }

        private void CommitOutlinerDrop(BlueprintEditorOutlinerDrop target)
        {
            if (!CanDropOutliner(target)) { EndOutlinerDrag(); return; }
            string targetId = target.StableId;
            EndOutlinerDrag();
            MoveSelectionToGroupRequested?.Invoke(targetId);
            if (targetId != null && collapsedGroups.Remove(targetId) && boundDocument != null)
                RebuildOutliner(boundDocument);
        }

        private void ClearOutlinerDropCue()
        {
            if (outlinerDropCue)
            {
                outlinerDropCue.enabled = false;
                if (outlinerTitle && outlinerDropCue.transform == outlinerTitle.transform.parent)
                    outlinerDropCue.GetComponent<Image>().color = Color.clear;
            }
            outlinerDropCue = null;
        }

        private void EndOutlinerDrag()
        {
            outlinerDragging = false;
            ClearOutlinerDropCue();
            if (outlinerTitle) outlinerTitle.text = T("editor.view.outliner");
        }

        private void RebuildOutliner(BlueprintEditorDocument document)
        {
            for (int index = outlinerRows.childCount - 1; index >= 0; --index)
            {
                GameObject oldRow = outlinerRows.GetChild(index).gameObject;
                oldRow.SetActive(false);
                UnityEngine.Object.Destroy(oldRow);
            }

            string filter = (outlinerSearch.text ?? string.Empty).Trim();
            foreach (BlueprintEditorGroup group in document.Groups)
            {
                if (group.ParentGroupId == null && BranchMatches(document, group, filter))
                    AddOutlinerGroup(document, group, filter, 0);
            }
            foreach (BlueprintEditorPart part in document.Parts)
            {
                if (part.ParentGroupId != null) continue;
                if (!Matches(part.DisplayName, filter) &&
                    !Matches(part.PrefabName, filter)) continue;
                if (!PassesOutlinerFilter(
                    document.IsPartSelected(part.StableId),
                    document.IsEffectivelyVisible(part.StableId),
                    document.IsEffectivelyLocked(part.StableId),
                    missingPrefabNodes.Contains(part.StableId))) continue;
                AddOutlinerRow(document, part.StableId, part.DisplayName,
                    part.Visible, part.Locked, true, false, 0);
            }
        }

        private bool BranchMatches(
            BlueprintEditorDocument document,
            BlueprintEditorGroup group,
            string filter)
        {
            if (Matches(group.Name, filter) && PassesOutlinerFilter(
                Contains(document.Selection, group.StableId),
                document.IsEffectivelyVisible(group.StableId),
                document.IsEffectivelyLocked(group.StableId), missing: false)) return true;
            foreach (BlueprintEditorGroup child in document.Groups)
                if (child.ParentGroupId == group.StableId &&
                    BranchMatches(document, child, filter)) return true;
            foreach (BlueprintEditorPart part in document.Parts)
                if (part.ParentGroupId == group.StableId &&
                    (Matches(part.DisplayName, filter) || Matches(part.PrefabName, filter)) &&
                    PassesOutlinerFilter(
                        document.IsPartSelected(part.StableId),
                        document.IsEffectivelyVisible(part.StableId),
                        document.IsEffectivelyLocked(part.StableId),
                        missingPrefabNodes.Contains(part.StableId))) return true;
            return false;
        }

        private void AddOutlinerGroup(
            BlueprintEditorDocument document,
            BlueprintEditorGroup group,
            string filter,
            int depth)
        {
            bool collapsed = collapsedGroups.Contains(group.StableId) &&
                string.IsNullOrEmpty(filter);
            AddOutlinerRow(document, group.StableId, group.Name,
                group.Visible, group.Locked, false, collapsed, depth);
            if (collapsed) return;
            foreach (BlueprintEditorGroup child in document.Groups)
                if (child.ParentGroupId == group.StableId &&
                    BranchMatches(document, child, filter))
                    AddOutlinerGroup(document, child, filter, depth + 1);
            foreach (BlueprintEditorPart part in document.Parts)
            {
                if (part.ParentGroupId != group.StableId ||
                    !Matches(part.DisplayName, filter) &&
                    !Matches(part.PrefabName, filter) ||
                    !PassesOutlinerFilter(
                        document.IsPartSelected(part.StableId),
                        document.IsEffectivelyVisible(part.StableId),
                        document.IsEffectivelyLocked(part.StableId),
                        missingPrefabNodes.Contains(part.StableId))) continue;
                AddOutlinerRow(document, part.StableId, part.DisplayName,
                    part.Visible, part.Locked, true, false, depth + 1);
            }
        }

        private void AddOutlinerRow(
            BlueprintEditorDocument document,
            string stableId,
            string labelText,
            bool visible,
            bool locked,
            bool part,
            bool collapsed,
            int depth)
        {
            RectTransform row = CreatePanel("Row_" + stableId, outlinerRows,
                Contains(document.Selection, stableId) ? SelectedColor :
                    stableId == hoveredNodeId ? new Color(0.07f, 0.22f, 0.32f, 1f) : ButtonColor, null);
            LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
            rowLayout.preferredHeight = (float)BlueprintEditorLayout.OutlinerRowHeight;
            rowLayout.minHeight = (float)BlueprintEditorLayout.OutlinerRowHeight;
            Button select = row.gameObject.AddComponent<Button>();
            select.transition = Selectable.Transition.ColorTint;
            select.onClick.AddListener(() => NodeClicked?.Invoke(
                stableId,
                input.GetKey(KeyCode.LeftControl) || input.GetKey(KeyCode.RightControl),
                input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift)));
            BlueprintEditorRightClick context =
                row.gameObject.AddComponent<BlueprintEditorRightClick>();
            context.Pressed = position => OpenOutlinerContext(stableId, position);
            context.Released = ReleaseOutlinerContext;
            AddOutlinerDrop(row, stableId, !part);
            TMP_Text name = CreateText("Name", row, labelText, 12f, FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            name.overflowMode = TextOverflowModes.Ellipsis;
            bool groupPivot = false;
            foreach (BlueprintEditorGroup group in document.Groups)
                if (group.PivotPartId == stableId) { groupPivot = true; break; }
            string marker = part && document.PrimaryPartId == stableId ? "◆ " : string.Empty;
            if (part && groupPivot) marker += "◇ ";
            if (!part)
            {
                BlueprintEditorGroup group = FindGroup(document, stableId);
                if (group != null && group.PivotPartId != null) marker += "◇ ";
                if (document.PrimaryGroupId == stableId) marker += "★ ";
            }
            name.text = marker + labelText +
                (part && missingPrefabNodes.Contains(stableId) ? "  !" : string.Empty);
            SetInsets((RectTransform)name.transform,
                40f + depth * 18f, 0f, 96f, 0f);
            if (!part)
            {
                Button disclosure = CreateButton(
                    "Disclosure", row, collapsed ? ">" : "v", 32f,
                    () => ToggleGroupCollapsed(stableId));
                RectTransform disclosureRect = (RectTransform)disclosure.transform;
                disclosureRect.anchorMin = disclosureRect.anchorMax = new Vector2(0f, 0.5f);
                disclosureRect.pivot = new Vector2(0f, 0.5f);
                disclosureRect.anchoredPosition = new Vector2(depth * 18f, 0f);
                disclosureRect.sizeDelta = new Vector2(32f, 44f);
            }
            Button eye = CreateIconButton(
                "Eye", row, visible ? "visibility" : "hidden", visible ? "●" : "○", 44f,
                () => VisibilityChanged?.Invoke(stableId, !visible),
                T(visible ? "editor.view.hide_object" : "editor.view.show_object"));
            RectTransform eyeRect = (RectTransform)eye.transform;
            eyeRect.anchorMin = eyeRect.anchorMax = new Vector2(1f, 0.5f);
            eyeRect.pivot = new Vector2(1f, 0.5f);
            eyeRect.anchoredPosition = new Vector2(-48f, 0f);
            eyeRect.sizeDelta = new Vector2(44f, 44f);
            Button lockButton = CreateIconButton(
                "Lock", row, locked ? "lock" : "unlock", locked ? "■" : "□", 44f,
                () => LockChanged?.Invoke(stableId, !locked),
                T(locked ? "editor.view.unlock_object" : "editor.view.lock_object"));
            RectTransform lockRect = (RectTransform)lockButton.transform;
            lockRect.anchorMin = lockRect.anchorMax = new Vector2(1f, 0.5f);
            lockRect.pivot = new Vector2(1f, 0.5f);
            lockRect.anchoredPosition = new Vector2(-2f, 0f);
            lockRect.sizeDelta = new Vector2(44f, 44f);
            if (!part) name.color = AccentColor;
        }

        private bool PassesOutlinerFilter(
            bool selected,
            bool visible,
            bool locked,
            bool missing)
        {
            switch (outlinerFilter)
            {
                case 1: return selected;
                case 2: return !visible;
                case 3: return locked;
                case 4: return missing;
                default: return true;
            }
        }

        private void ToggleGroupCollapsed(string stableId)
        {
            if (!collapsedGroups.Add(stableId)) collapsedGroups.Remove(stableId);
            if (boundDocument != null) RebuildOutliner(boundDocument);
        }

        private void CycleOutlinerFilter()
        {
            outlinerFilter = (outlinerFilter + 1) % 5;
            string[] labels = {
                T("editor.view.all"),
                T("editor.view.selection"),
                T("editor.view.hidden"),
                T("editor.view.locked"),
                T("editor.view.missing") };
            SetButtonLabel(outlinerFilterButton, labels[outlinerFilter]);
            if (boundDocument != null) RebuildOutliner(boundDocument);
        }

        private Button AddOutlinerToolbarButton(Transform parent, string name, string icon,
            string fallback, string description, Action clicked, bool requiresSelection = true)
        {
            Button button = CreateIconButton(name, parent, icon, fallback, 32f, clicked, description);
            LayoutElement layout = button.GetComponent<LayoutElement>();
            layout.minWidth = 28f;
            layout.preferredWidth = 44f;
            layout.minHeight = layout.preferredHeight = 32f;
            button.GetComponentInChildren<TMP_Text>().fontSize = 10f;
            if (requiresSelection) outlinerSelectionButtons.Add(button);
            return button;
        }

        private void BeginRenameSelection()
        {
            if (boundDocument == null || boundDocument.Selection.Count != 1) return;
            HideOutlinerMenu();
            OpenInspectorTab(1);
            if (groupName.gameObject.activeInHierarchy && groupName.interactable)
            {
                groupName.Select();
                groupName.ActivateInputField();
            }
        }

        private void OpenInspectorTab(int tab)
        {
            ToolSelected?.Invoke(BlueprintEditorTool.Select);
            SetTool(BlueprintEditorTool.Select);
            SetInspectorTab(tab);
            if (compact && !compactInspector) ToggleCompactPane();
        }

        private void ToggleSelectedVisibility()
        {
            if (boundDocument == null || boundDocument.Selection.Count == 0) return;
            bool allVisible = true;
            foreach (string stableId in boundDocument.Selection)
                allVisible &= boundDocument.IsEffectivelyVisible(stableId);
            SelectionVisibilityRequested?.Invoke(!allVisible);
        }

        private void ToggleSelectedLock()
        {
            if (boundDocument == null || boundDocument.Selection.Count == 0) return;
            bool allLocked = true;
            foreach (string stableId in boundDocument.Selection)
                allLocked &= boundDocument.IsEffectivelyLocked(stableId);
            SelectionLockRequested?.Invoke(!allLocked);
        }

        private void ToggleOutlinerMenu() => ToggleOutlinerMenu(false);

        private void OpenOutlinerContext(string stableId, Vector2 screenPosition)
        {
            if (boundDocument == null) return;
            if (!Contains(boundDocument.Selection, stableId))
                NodeClicked?.Invoke(stableId, false, false);
            RebuildOutlinerContext(stableId);
            PositionOutlinerContext(screenPosition);
            outlinerContextMenu.gameObject.SetActive(true);
            outlinerContextTracking = true;
            outlinerContextMenu.SetAsLastSibling();
        }

        private void PositionOutlinerContext(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeRoot, screenPosition, canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null : canvas.worldCamera, out Vector2 local);
            Rect area = safeRoot.rect;
            float width = outlinerContextMenu.rect.width;
            float height = outlinerContextMenu.rect.height;
            outlinerContextMenu.pivot = new Vector2(
                local.x + width <= area.xMax ? 0f : 1f,
                local.y - height >= area.yMin ? 1f : 0f);
            local.x = Mathf.Clamp(local.x,
                area.xMin + outlinerContextMenu.pivot.x * width,
                area.xMax - (1f - outlinerContextMenu.pivot.x) * width);
            local.y = Mathf.Clamp(local.y,
                area.yMin + outlinerContextMenu.pivot.y * height,
                area.yMax - (1f - outlinerContextMenu.pivot.y) * height);
            outlinerContextMenu.localPosition = local;
        }

        private void RebuildOutlinerContext(string stableId)
        {
            ClearChildren(outlinerContextRows);
            bool part = FindPart(boundDocument, stableId) != null;
            bool primaryPart = part && boundDocument.PrimaryPartId == stableId;
            bool primaryGroup = !part && boundDocument.PrimaryGroupId == stableId;
            var actions = new List<Action>
            {
                BeginRenameSelection,
                ToggleSelectedVisibility,
                ToggleSelectedLock,
                () => GroupRequested?.Invoke(),
                () => UngroupRequested?.Invoke(),
                () => DuplicateSelectionRequested?.Invoke(),
                () => DeleteSelectionRequested?.Invoke(),
                () => MoveSelectionToGroupRequested?.Invoke(null)
            };
            var labels = new List<string>
            {
                T("editor.view.rename"), T("editor.view.show_hide"),
                T("editor.view.lock_unlock"), T("editor.view.group"),
                T("editor.view.ungroup"), T("editor.view.duplicate"),
                T("editor.view.delete"), T("editor.view.to_root")
            };
            var enabled = new List<bool>
            {
                boundDocument.Selection.Count == 1,
                boundDocument.Selection.Count > 0,
                boundDocument.Selection.Count > 0,
                boundDocument.MovableNodeSelectionCount >= 2,
                boundDocument.Selection.Count > 0,
                boundDocument.Selection.Count > 0,
                boundDocument.CanDeleteSelection,
                boundDocument.Selection.Count > 0
            };
            if (part)
            {
                actions.Add(() => PrimaryPartRequested?.Invoke(primaryPart ? null : stableId));
                labels.Add(T(primaryPart
                    ? "editor.view.remove_blueprint_frame"
                    : "editor.view.blueprint_frame"));
                enabled.Add(true);
                foreach (BlueprintEditorGroup group in boundDocument.Groups)
                {
                    if (!boundDocument.IsPartInGroup(stableId, group.StableId)) continue;
                    string groupId = group.StableId;
                    bool groupPivot = group.PivotPartId == stableId;
                    actions.Add(() => GroupPivotRequested?.Invoke(
                        groupId, groupPivot ? null : stableId));
                    labels.Add(T(groupPivot
                        ? "editor.view.remove_group_pivot"
                        : "editor.view.group_pivot", group.Name));
                    enabled.Add(true);
                }
            }
            else
            {
                BlueprintEditorGroup group = FindGroup(boundDocument, stableId);
                if (group?.PivotPartId != null)
                {
                    actions.Add(() => GroupPivotRequested?.Invoke(stableId, null));
                    labels.Add(T("editor.view.auto_group_center"));
                    enabled.Add(true);
                }
                if (primaryGroup)
                {
                    actions.Add(() => PrimaryGroupRequested?.Invoke(null));
                    labels.Add(T("editor.view.remove_old_frame"));
                    enabled.Add(true);
                }
            }
            int rows = (actions.Count + 1) / 2;
            float contentHeight = rows * 40f;
            outlinerContextMenu.sizeDelta = new Vector2(500f,
                Mathf.Min(16f + contentHeight, Mathf.Max(96f, safeRoot.rect.height - 32f)));
            outlinerContextRows.sizeDelta = new Vector2(0f, contentHeight);
            for (int index = 0; index < actions.Count; ++index)
            {
                Action command = actions[index];
                Button button = CreateButton("Marking" + index, outlinerContextRows,
                    labels[index], 238f, () => command?.Invoke());
                SetButtonInteractable(button, enabled[index]);
                SetTopLeft((RectTransform)button.transform,
                    index % 2 * 246f, index / 2 * 40f, 238f, 36f);
            }
        }

        private void ReleaseOutlinerContext(Vector2 screenPosition)
        {
            if (input.GetMouseButton(1)) return;
            outlinerContextTracking = false;
            if (!outlinerContextMenu.gameObject.activeSelf) return;
            Button chosen = null;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : canvas.worldCamera;
            if (RectTransformUtility.RectangleContainsScreenPoint(
                outlinerContextViewport, screenPosition, uiCamera))
                foreach (Button button in outlinerContextRows.GetComponentsInChildren<Button>())
                    if (button.IsInteractable() && RectTransformUtility.RectangleContainsScreenPoint(
                        (RectTransform)button.transform, screenPosition, uiCamera))
                    {
                        chosen = button;
                        break;
                    }
            outlinerContextMenu.gameObject.SetActive(false);
            chosen?.onClick.Invoke();
        }

        private void ToggleOutlinerMenu(bool reparentOnly)
        {
            bool show = !outlinerMenu.activeSelf || outlinerReparentOnly != reparentOnly;
            if (!show) { HideOutlinerMenu(); return; }
            ShowOutlinerMenu(reparentOnly);
        }

        private void ShowOutlinerMenu(bool reparentOnly)
        {
            outlinerReparentOnly = reparentOnly;
            HideViewportSettings();
            outlinerMenu.SetActive(true);
            outlinerRowViewport.gameObject.SetActive(false);
            RebuildOutlinerMenu();
            outlinerMenu.transform.SetAsLastSibling();
        }

        private void RebuildOutlinerMenu()
        {
            ClearChildren(outlinerMenuRows);
            if (!outlinerReparentOnly)
            {
                CreateButton("ShowAll", outlinerMenuRows, T("editor.view.show_all"), 238f,
                    () => InvokeOutlinerCommand(ShowAllRequested));
                CreateButton("HideSelection", outlinerMenuRows, T("editor.view.hide_selected"), 238f,
                    () => InvokeOutlinerCommand(HideSelectionRequested));
                CreateButton("LockSelection", outlinerMenuRows, T("editor.view.lock_selected"), 238f,
                    () => InvokeOutlinerCommand(LockSelectionRequested));
                CreateButton("Ungroup", outlinerMenuRows, T("editor.view.ungroup"), 238f,
                    () => InvokeOutlinerCommand(UngroupRequested));
                CreateButton("PartProperties", outlinerMenuRows, T("editor.view.part_properties"), 238f,
                    () => InvokeOutlinerCommand(() => OpenInspectorTab(1)));
                CreateButton("BlueprintProperties", outlinerMenuRows, T("editor.view.blueprint_properties"), 238f,
                    () => InvokeOutlinerCommand(() => OpenInspectorTab(2)));
            }
            if (boundDocument == null) return;
            CreateButton("MoveRoot", outlinerMenuRows, T("editor.view.to_root"), 238f,
                () => InvokeMoveToGroup(null));
            foreach (BlueprintEditorGroup group in boundDocument.Groups)
            {
                string groupId = group.StableId;
                CreateButton("Move_" + groupId, outlinerMenuRows,
                    T("editor.view.to_group", group.Name), 238f,
                    () => InvokeMoveToGroup(groupId));
            }
        }

        private void InvokeOutlinerCommand(Action command)
        {
            HideOutlinerMenu();
            command?.Invoke();
        }

        private void InvokeMoveToGroup(string groupId)
        {
            HideOutlinerMenu();
            MoveSelectionToGroupRequested?.Invoke(groupId);
        }

        private void BindInspector(BlueprintEditorDocument document)
        {
            inspectorEditPanel.SetActive(false);
            groupEditPanel.SetActive(false);
            blueprintEditPanel.SetActive(false);
            arrayEditPanel.SetActive(false);
            contourEditPanel.SetActive(false);
            inspectorContent.gameObject.SetActive(true);
            inspectorNodeId = null;
            groupNodeId = null;
            ResetInspectorFieldColors();
            bool toolParameters = selectedTool == BlueprintEditorTool.Array ||
                selectedTool == BlueprintEditorTool.Contour;
            inspectorTitle.text = T("editor.view.tool_parameters");
            inspectorTabs.gameObject.SetActive(false);
            if (selectedTool == BlueprintEditorTool.Select && inspectorTab == 0 &&
                document.Selection.Count == 0)
            {
                inspectorContent.text = T(
                    "editor.view.selection_help", document.Selection.Count);
                return;
            }
            if (selectedTool == BlueprintEditorTool.Array)
            {
                inspectorContent.gameObject.SetActive(false);
                arrayEditPanel.SetActive(true);
                return;
            }
            if (selectedTool == BlueprintEditorTool.Contour)
            {
                inspectorContent.gameObject.SetActive(false);
                contourEditPanel.SetActive(true);
                return;
            }
            if (inspectorTab == 2)
            {
                inspectorContent.gameObject.SetActive(false);
                blueprintEditPanel.SetActive(true);
                blueprintName.SetTextWithoutNotify(document.Name);
                blueprintCategory.SetTextWithoutNotify(document.Category);
                string bounds = blueprintBoundsSize.HasValue
                    ? T(
                        "editor.view.bounds_m",
                        blueprintBoundsSize.Value.x.ToString("0.##"),
                        blueprintBoundsSize.Value.y.ToString("0.##"),
                        blueprintBoundsSize.Value.z.ToString("0.##"))
                    : T("editor.view.no_geometry");
                blueprintInfo.text = T(
                    "editor.view.blueprint_info",
                    document.Parts.Count,
                    document.Groups.Count,
                    bounds,
                    missingPrefabNodes.Count,
                    T(document.IsDirty
                        ? "editor.view.unsaved_changes"
                        : "editor.view.changes_saved"));
                return;
            }
            if (document.Selection.Count == 0)
            {
                inspectorContent.text = T("editor.view.select_in_viewport");
                return;
            }
            if (inspectorTab == 1)
            {
                BindDetailInspector(document);
                return;
            }
            if (document.EditablePartSelectionCount == 0)
            {
                inspectorContent.text = T("editor.view.selection_locked_hidden");
                return;
            }

            BlueprintEditorPart singlePart = null;
            if (document.Selection.Count == 1)
            {
                string selected = document.Selection[0];
                foreach (BlueprintEditorPart part in document.Parts)
                    if (part.StableId == selected) singlePart = part;
            }
            inspectorDelta = singlePart == null;
            inspectorNodeId = singlePart?.StableId;
            inspectorContent.gameObject.SetActive(false);
            inspectorEditPanel.SetActive(true);
            inspectorName.interactable = singlePart != null;
            inspectorName.SetTextWithoutNotify(singlePart != null
                ? singlePart.DisplayName
                : T("editor.view.parts_delta", document.EditablePartSelectionCount));
            inspectorPositionLabel.text = T(inspectorDelta
                ? "editor.view.offset_delta_m"
                : "editor.view.position_m");
            inspectorRotationLabel.text = T(inspectorDelta
                ? "editor.view.rotation_delta_deg"
                : "editor.view.rotation_deg");
            inspectorScaleLabel.text = T(inspectorDelta
                ? "editor.view.selection_scale"
                : "editor.view.uniform_scale");
            SetButtonLabel(inspectorResetButton, T("editor.view.reset"));
            if (singlePart == null)
            {
                SetAxisValues(inspectorPosition, Vector3.zero);
                SetAxisValues(inspectorRotation, Vector3.zero);
                inspectorScale.SetTextWithoutNotify("100");
            }
            else
            {
                SetAxisValues(inspectorPosition, new Vector3(
                    (float)singlePart.Position.X,
                    (float)singlePart.Position.Y,
                    (float)singlePart.Position.Z));
                Quaternion quaternion = new Quaternion(
                    (float)singlePart.Rotation.X,
                    (float)singlePart.Rotation.Y,
                    (float)singlePart.Rotation.Z,
                    (float)singlePart.Rotation.W);
                Vector3 euler = quaternion.eulerAngles;
                SetAxisValues(inspectorRotation, new Vector3(
                    SignedAngle(euler.x), SignedAngle(euler.y), SignedAngle(euler.z)));
                inspectorScale.SetTextWithoutNotify(
                    ((float)singlePart.Scale.X * 100f).ToString("0.##"));
            }
            UpdateTransformControls();
        }

        private void BindDetailInspector(BlueprintEditorDocument document)
        {
            inspectorContent.gameObject.SetActive(false);
            groupEditPanel.SetActive(true);
            bool single = document.Selection.Count == 1;
            string selected = single ? document.Selection[0] : null;
            BlueprintEditorPart selectedPart = null;
            BlueprintEditorGroup selectedGroup = null;
            foreach (BlueprintEditorPart part in document.Parts)
                if (part.StableId == selected) selectedPart = part;
            foreach (BlueprintEditorGroup group in document.Groups)
                if (group.StableId == selected) selectedGroup = group;

            groupNodeId = single ? selected : null;
            groupName.interactable = single;
            groupName.SetTextWithoutNotify(selectedPart != null
                ? selectedPart.DisplayName
                : selectedGroup != null
                    ? selectedGroup.Name
                    : T("editor.view.object_count", document.Selection.Count));

            detailVisible = true;
            detailLocked = true;
            bool partsOnly = document.Selection.Count > 0;
            bool parentInitialized = false;
            detailParentGroupId = null;
            foreach (string stableId in document.Selection)
            {
                BlueprintEditorPart part = FindPart(document, stableId);
                BlueprintEditorGroup group = FindGroup(document, stableId);
                if (part != null)
                {
                    detailVisible &= part.Visible;
                    detailLocked &= part.Locked;
                    if (!parentInitialized)
                    {
                        detailParentGroupId = part.ParentGroupId;
                        parentInitialized = true;
                    }
                    else if (detailParentGroupId != part.ParentGroupId)
                        detailParentGroupId = string.Empty;
                }
                else if (group != null)
                {
                    detailVisible &= group.Visible;
                    detailLocked &= group.Locked;
                    partsOnly = false;
                }
            }
            if (selectedPart != null)
            {
                Point3 scale = selectedPart.Scale;
                bool uniform = Math.Abs(scale.X - scale.Y) < 0.000001 &&
                    Math.Abs(scale.X - scale.Z) < 0.000001;
                detailInfo.text = T(
                    "editor.view.part_info",
                    selectedPart.PrefabName,
                    uniform
                        ? (scale.X * 100.0).ToString("0.###", CultureInfo.CurrentCulture) + "%"
                        : T("editor.view.non_uniform_scale"),
                    GroupName(document, selectedPart.ParentGroupId),
                    document.PrimaryPartId == selectedPart.StableId
                        ? T("editor.view.blueprint_frame_marker")
                        : string.Empty);
            }
            else if (selectedGroup != null)
            {
                int count = 0;
                foreach (BlueprintEditorPart part in document.Parts)
                    if (part.ParentGroupId == selectedGroup.StableId) ++count;
                detailInfo.text = T(
                    "editor.view.group_info",
                    count,
                    selectedGroup.PivotPartId == null
                        ? T("editor.view.auto_group_center")
                        : PartName(document, selectedGroup.PivotPartId));
            }
            else
            {
                detailInfo.text = T(
                    "editor.view.multi_selection_info", document.Selection.Count);
            }
            SetButtonLabel(detailVisibilityButton,
                T(detailVisible
                    ? "editor.view.hide_selected"
                    : "editor.view.show_selected"));
            SetButtonLabel(detailLockButton,
                T(detailLocked
                    ? "editor.view.unlock"
                    : "editor.view.lock_selected"));
            detailGroupButton.interactable = partsOnly;
            SetButtonLabel(detailGroupButton,
                T("editor.view.group_value", detailParentGroupId == string.Empty
                    ? T("editor.view.mixed")
                    : GroupName(document, detailParentGroupId)));
        }

        private void SetInspectorTab(int tab)
        {
            inspectorTab = Mathf.Clamp(tab, 0, inspectorTabButtons.Length - 1);
            for (int index = 0; index < inspectorTabButtons.Length; ++index)
            {
                bool active = index == inspectorTab;
                SetSelected(inspectorTabButtons[index], active);
            }
            if (boundDocument != null) BindInspector(boundDocument);
        }

        private void SubmitDetailName()
        {
            if (!string.IsNullOrEmpty(groupNodeId))
                RenameSubmitted?.Invoke(groupNodeId, groupName.text);
        }

        private void SubmitInspector()
        {
            if (!inspectorEditPanel.activeSelf) return;
            bool pxValid = TryRead(inspectorPosition[0], out float px);
            bool pyValid = TryRead(inspectorPosition[1], out float py);
            bool pzValid = TryRead(inspectorPosition[2], out float pz);
            bool rxValid = TryRead(inspectorRotation[0], out float rx);
            bool ryValid = TryRead(inspectorRotation[1], out float ry);
            bool rzValid = TryRead(inspectorRotation[2], out float rz);
            bool scaleValid = TryRead(inspectorScale, out float scale) &&
                scale >= 1f && scale <= 400f;
            SetInputValid(inspectorPosition[0], pxValid);
            SetInputValid(inspectorPosition[1], pyValid);
            SetInputValid(inspectorPosition[2], pzValid);
            SetInputValid(inspectorRotation[0], rxValid);
            SetInputValid(inspectorRotation[1], ryValid);
            SetInputValid(inspectorRotation[2], rzValid);
            SetInputValid(inspectorScale, scaleValid);
            bool nameValid = inspectorDelta || !string.IsNullOrWhiteSpace(inspectorName.text);
            SetInputValid(inspectorName, nameValid);
            if (!pxValid || !pyValid || !pzValid || !rxValid || !ryValid ||
                !rzValid || !scaleValid || !nameValid)
            {
                SetStatus(T("editor.view.invalid_red_field"), error: true);
                return;
            }
            InspectorSubmitted?.Invoke(
                inspectorNodeId,
                inspectorName.text,
                new Vector3(px, py, pz),
                new Vector3(rx, ry, rz),
                scale / 100f,
                inspectorDelta);
        }

        private void SubmitSnap()
        {
            bool moveValid = TryRead(inspectorMoveStep, out float move) &&
                move >= 0.001f && move <= 10f;
            bool angleValid = TryRead(inspectorAngleStep, out float angle) &&
                angle >= 0.1f && angle <= 90f;
            SetInputValid(inspectorMoveStep, moveValid);
            SetInputValid(inspectorAngleStep, angleValid);
            if (!moveValid || !angleValid)
            {
                SetStatus(T("editor.view.invalid_steps"), error: true);
                return;
            }
            SnapChanged?.Invoke(move, angle);
        }

        private void SubmitArrayPreview(bool clearOnInvalid = true)
        {
            bool valid = true;
            TMP_InputField[] fields = { arrayCount[0], arrayCount[1], arrayRise,
                arrayRotation, arrayPitch, arrayRoll, arrayScaleStepX };
            var values = new float[fields.Length];
            for (int index = 0; index < fields.Length; ++index)
            {
                bool fieldValid = TryRead(fields[index], out values[index]);
                if (index < 2) fieldValid &= values[index] >= 1 && values[index] <= 128 &&
                    values[index] == Mathf.Round(values[index]);
                else if (index == 2) fieldValid &= Mathf.Abs(values[index]) <= 100f;
                else if (index < 6) fieldValid &= Mathf.Abs(values[index]) <= 360f;
                else fieldValid &= values[index] >= -300f && values[index] <= 300f;
                SetInputValid(fields[index], fieldValid);
                valid &= fieldValid;
            }
            if (!valid)
            {
                if (clearOnInvalid)
                    SetArrayState(0, T("editor.view.invalid_array_values"));
                return;
            }
            ArrayChanged?.Invoke((int)values[0], (int)values[1], values[2], values[3],
                values[4], values[5], values[6] / 100f);
        }

        private void SubmitContourPreview()
        {
            bool valid = TryRead(contourScaleStep, out float scale) &&
                scale >= -99f && scale <= 300f &&
                (Mathf.Abs(scale) < 0.00001f || Mathf.Abs(scale) >= 1f);
            SetInputValid(contourScaleStep, valid);
            if (!valid)
            {
                SetStatus(T("editor.view.invalid_scale_step"), error: true);
                return;
            }
            ContourChanged?.Invoke(scale / 100f);
        }

        private void ResetInspector()
        {
            if (!inspectorEditPanel.activeSelf) return;
            ResetTransformRequested?.Invoke();
        }

        private void SubmitBlueprintMetadata()
        {
            string name = (blueprintName.text ?? string.Empty).Trim();
            string category = (blueprintCategory.text ?? string.Empty).Trim();
            bool nameValid = name.Length >= 1 && name.Length <= 48;
            bool categoryValid = category.Length >= 1 && category.Length <= 24;
            SetInputValid(blueprintName, nameValid);
            SetInputValid(blueprintCategory, categoryValid);
            if (!nameValid || !categoryValid)
            {
                SetStatus(T("editor.view.invalid_metadata"), error: true);
                return;
            }
            BlueprintMetadataSubmitted?.Invoke(name, category);
        }

        private void ToggleDetailVisibility()
        {
            if (!groupEditPanel.activeSelf) return;
            if (!string.IsNullOrEmpty(groupNodeId))
                VisibilityChanged?.Invoke(groupNodeId, !detailVisible);
            else SelectionVisibilityRequested?.Invoke(!detailVisible);
        }

        private void ToggleDetailLock()
        {
            if (!groupEditPanel.activeSelf) return;
            if (!string.IsNullOrEmpty(groupNodeId))
                LockChanged?.Invoke(groupNodeId, !detailLocked);
            else SelectionLockRequested?.Invoke(!detailLocked);
        }

        private void CycleDetailGroup()
        {
            if (boundDocument == null || !detailGroupButton.interactable) return;
            string next = null;
            if (detailParentGroupId == null && boundDocument.Groups.Count > 0)
                next = boundDocument.Groups[0].StableId;
            else
            {
                for (int index = 0; index < boundDocument.Groups.Count; ++index)
                    if (boundDocument.Groups[index].StableId == detailParentGroupId &&
                        index + 1 < boundDocument.Groups.Count)
                        next = boundDocument.Groups[index + 1].StableId;
            }
            MoveSelectionToGroupRequested?.Invoke(next);
        }

        private void UpdateTransformControls()
        {
            if (inspectorSpaceButton == null) return;
            SetButtonLabel(inspectorSpaceButton,
                T(localTransformSpace
                    ? "editor.view.axes_local"
                    : "editor.view.axes_world"));
            inspectorMoveStep.SetTextWithoutNotify(translationStep.ToString("0.###"));
            inspectorAngleStep.SetTextWithoutNotify(rotationStep.ToString("0.###"));
            SetButtonLabel(inspectorAnchorVisibilityButton,
                T(showAllTransformAnchors
                    ? "editor.view.points_all"
                    : "editor.view.points_nearby"));
            SetButtonLabel(inspectorMagnetButton,
                T(transformMeshSnap
                    ? "editor.view.magnet_mesh"
                    : "editor.view.magnet_game"));
            SetButtonLabel(inspectorPivotButton,
                T(customTransformPivot
                    ? "editor.view.reset"
                    : "editor.view.select"));
            SetSelected(inspectorSelectionPivotButton,
                !customTransformPivot &&
                transformPivotMode == BlueprintEditorPivotMode.SelectionCenter);
            SetSelected(inspectorActivePivotButton,
                !customTransformPivot &&
                transformPivotMode == BlueprintEditorPivotMode.ActiveObject);
            for (int index = 0; index < inspectorBoundsButtons.Length; ++index)
                SetSelected(inspectorBoundsButtons[index],
                    !customTransformPivot &&
                    transformPivotMode == BlueprintEditorPivotMode.Bounds &&
                    transformBoundsPivot == index);
        }

        private void SetSelected(Button button, bool selected)
        {
            if (!button) return;
            skin.Apply(button, selected);
            UpdateButtonIconColor(button);
        }

        private static void SetAxisValues(TMP_InputField[] fields, Vector3 value)
        {
            fields[0].SetTextWithoutNotify(value.x.ToString("0.###"));
            fields[1].SetTextWithoutNotify(value.y.ToString("0.###"));
            fields[2].SetTextWithoutNotify(value.z.ToString("0.###"));
        }

        private static BlueprintEditorPart FindPart(
            BlueprintEditorDocument document,
            string stableId)
        {
            foreach (BlueprintEditorPart part in document.Parts)
                if (part.StableId == stableId) return part;
            return null;
        }

        private static BlueprintEditorGroup FindGroup(
            BlueprintEditorDocument document,
            string stableId)
        {
            foreach (BlueprintEditorGroup group in document.Groups)
                if (group.StableId == stableId) return group;
            return null;
        }

        private static string GroupName(BlueprintEditorDocument document, string groupId)
        {
            BlueprintEditorGroup group = FindGroup(document, groupId);
            return group?.Name ?? "ROOT";
        }

        private static string PartName(BlueprintEditorDocument document, string partId)
        {
            BlueprintEditorPart part = FindPart(document, partId);
            return part?.DisplayName ?? T("editor.view.auto_group_center");
        }

        private void CycleLighting()
        {
            lightingPreset = (BlueprintEditorLightingPreset)(((int)lightingPreset + 1) % 3);
            lightingLabel.text = T(lightingPreset == BlueprintEditorLightingPreset.Neutral
                ? "editor.view.light_neutral"
                : lightingPreset == BlueprintEditorLightingPreset.Warm
                    ? "editor.view.light_warm"
                    : "editor.view.light_outline");
            LightingChanged?.Invoke(lightingPreset);
        }

        private Button CreateDialogButton(
            RectTransform dialog,
            string name,
            string text,
            float x,
            Action clicked,
            bool primary)
        {
            float width = name == "Discard" ? 180f : 140f;
            Button button = CreateButton(name, dialog, text, width, clicked, primary);
            RectTransform rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(x, 18f);
            rect.sizeDelta = new Vector2(width, 44f);
            return button;
        }

        private TMP_Text CreateInspectorLabel(Transform parent, string value, float top)
        {
            TMP_Text label = CreateText(
                value, parent, value, 11f, FontStyles.Bold,
                TextAlignmentOptions.MidlineLeft);
            label.color = MutedColor;
            SetTopAnchored((RectTransform)label.transform, 0f, top, 0f, 22f);
            return label;
        }

        private void CreateAnchorControls(Transform parent, float top)
        {
            Button pin = CreateButton("PinSelectedAnchor", parent, T("editor.view.pin"), 228f,
                () => PinSelectedAnchorRequested?.Invoke());
            SetInspectorButton((RectTransform)pin.transform, 0f, top, 228f);
            AddTooltip((RectTransform)pin.transform,
                T("editor.view.pin_anchor_tooltip"));
            pin.interactable = false;
            anchorPinButtons.Add(pin);
            for (int index = 0; index < 4; ++index)
            {
                GizmoAxis axis = (GizmoAxis)index;
                float width = index == 0 ? 72f : 44f;
                Button constraint = CreateButton("AnchorConstraint" + axis, parent,
                    index == 0 ? T("editor.view.free_short") : axis.ToString(), width,
                    () => AnchorConstraintRequested?.Invoke(axis));
                RectTransform rect = (RectTransform)constraint.transform;
                SetInspectorButton(rect, index == 0 ? 0f : 80f + (index - 1) * 52f, top + 42f, width);
                rect.sizeDelta = new Vector2(width, 28f);
                AddTooltip(rect, index == 0
                    ? T("editor.view.free_rotation_tooltip")
                    : T("editor.view.axis_rotation_tooltip", axis));
                anchorConstraintButtons.Add(constraint);
            }
        }

        private void CreateAxisInputs(
            Transform parent,
            TMP_InputField[] destination,
            float top,
            float scrubUnitsPerPixel = 0f)
        {
            string[] axes = { "X", "Y", "Z" };
            for (int index = 0; index < destination.Length; ++index)
            {
                float x = index * 76f;
                TMP_Text axis = CreateText(
                    "Axis" + axes[index], parent, axes[index], 11f,
                    FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
                RectTransform axisRect = (RectTransform)axis.transform;
                axisRect.anchorMin = axisRect.anchorMax = new Vector2(0f, 1f);
                axisRect.pivot = new Vector2(0f, 1f);
                axisRect.anchoredPosition = new Vector2(x, -top);
                axisRect.sizeDelta = new Vector2(14f, 36f);
                destination[index] = CreateInput(
                    axes[index], parent, new Vector2(x + 16f, -top), 56f,
                    scrubUnitsPerPixel);
            }
        }

        private void CreateCompactAxisRow(
            Transform parent,
            string labelText,
            TMP_InputField[] destination,
            float top)
        {
            CreateCompactLabel(parent, labelText, top + 2f);
            for (int index = 0; index < destination.Length; ++index)
            {
                destination[index] = CreateInput(
                    labelText.Replace(" ", string.Empty) + index,
                    parent,
                    new Vector2(72f + index * 54f, -top),
                    48f,
                    0.01f);
                ((RectTransform)destination[index].transform).sizeDelta =
                    new Vector2(48f, 34f);
                AddTooltip((RectTransform)destination[index].transform, T(
                    "editor.view.numeric_axis_tooltip",
                    labelText,
                    index == 0 ? "X" : index == 1 ? "Y" : "Z"));
            }
        }

        private TMP_Text CreateCompactLabel(Transform parent, string value, float top)
        {
            TMP_Text label = CreateText(
                value.Replace(" ", string.Empty), parent, value, 10f,
                FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            label.color = MutedColor;
            SetTopLeft((RectTransform)label.transform, 0f, top, 68f, 32f);
            return label;
        }

        private TMP_InputField CreateInput(
            string name,
            Transform parent,
            Vector2 position,
            float width,
            float scrubUnitsPerPixel = 0f,
            float minimum = float.NegativeInfinity,
            float maximum = float.PositiveInfinity,
            bool wholeNumbers = false,
            float? resetValue = null)
        {
            RectTransform background = CreatePanel("Input" + name, parent, ButtonColor, BlueprintEditorSkin.Surface.Input);
            background.anchorMin = background.anchorMax = new Vector2(0f, 1f);
            background.pivot = new Vector2(0f, 1f);
            background.anchoredPosition = position;
            background.sizeDelta = new Vector2(width, 36f);
            TMP_Text text = CreateText(
                "Text", background, string.Empty, 12f, FontStyles.Normal,
                TextAlignmentOptions.MidlineLeft);
            text.raycastTarget = true;
            SetInsets((RectTransform)text.transform, 6f, 0f, 6f, 0f);
            TMP_InputField input = background.gameObject.AddComponent<BlueprintEditorInputField>();
            skin.Apply(input, input: true);
            input.textComponent = text;
            input.textViewport = background;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.characterLimit = name.IndexOf("Name", StringComparison.Ordinal) >= 0
                ? 48
                : name.IndexOf("Category", StringComparison.Ordinal) >= 0 ? 24 : 16;
            if (scrubUnitsPerPixel > 0f || resetValue.HasValue)
            {
                BlueprintEditorNumericScrub scrub =
                    background.gameObject.AddComponent<BlueprintEditorNumericScrub>();
                numericScrubs.Add(scrub);
                scrub.Initialize(
                    input, scrubUnitsPerPixel, minimum, maximum, wholeNumbers, this.input, resetValue ?? 0f);
            }
            return input;
        }

        private void AddTooltip(RectTransform target, string value)
        {
            BlueprintEditorHoverTarget hover =
                target.gameObject.AddComponent<BlueprintEditorHoverTarget>();
            hover.Initialize(value, BeginTooltip, EndTooltip);
        }

        private void ResetInspectorFieldColors()
        {
            SetInputValid(inspectorName, true);
            SetInputValid(groupName, true);
            SetInputValid(blueprintName, true);
            SetInputValid(blueprintCategory, true);
            foreach (TMP_InputField input in inspectorPosition) SetInputValid(input, true);
            foreach (TMP_InputField input in inspectorRotation) SetInputValid(input, true);
            SetInputValid(inspectorScale, true);
            SetInputValid(inspectorMoveStep, true);
            SetInputValid(inspectorAngleStep, true);
            foreach (TMP_InputField input in arrayCount) SetInputValid(input, true);
            SetInputValid(arrayRise, true);
            SetInputValid(arrayPitch, true);
            SetInputValid(arrayRoll, true);
            SetInputValid(arrayRotation, true);
            SetInputValid(arrayScaleStepX, true);
            SetInputValid(contourScaleStep, true);
        }

        private static void SetInputValid(TMP_InputField input, bool valid)
        {
            Image image = input ? input.GetComponent<Image>() : null;
            if (image) image.color = valid
                ? Color.white
                : new Color(1f, 0.32f, 0.24f, 1f);
        }

        private static void SetInspectorButton(
            RectTransform rect,
            float x,
            float top,
            float width)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -top);
            rect.sizeDelta = new Vector2(width, 40f);
        }

        private static bool TryRead(TMP_InputField input, out float value)
        {
            return float.TryParse(
                    input.text,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out value) ||
                float.TryParse(
                    input.text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out value);
        }

        private static float SignedAngle(float value) => Mathf.DeltaAngle(0f, value);

        private static bool Matches(string value, string filter) =>
            string.IsNullOrEmpty(filter) ||
            (value ?? string.Empty).IndexOf(
                filter, StringComparison.CurrentCultureIgnoreCase) >= 0;

        private void ShowDialog()
        {
            EndTooltip();
            SetPanelsInteractable(false);
            modal.SetActive(true);
            modal.transform.SetAsLastSibling();
            if (EventSystem.current)
                EventSystem.current.SetSelectedGameObject(dialogCancelButton.gameObject);
        }

        private void SetPanelsInteractable(bool value)
        {
            foreach (CanvasGroup group in modalBlockedGroups) group.interactable = value;
        }

        private void ToggleCompactPane()
        {
            compactInspector = !compactInspector;
            ApplySafeAreaAndLayout(force: true);
        }

        private static void SetAvailability(
            Button button,
            bool available,
            string unavailableReason)
        {
            SetButtonInteractable(button, available);
            button.GetComponent<BlueprintEditorHoverTarget>()?.SetAvailability(
                available, unavailableReason);
        }

        private static void SetButtonInteractable(Button button, bool available)
        {
            button.interactable = available;
            UpdateButtonIconColor(button);
        }

        private static void UpdateButtonIconColor(Button button)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label) label.color = button.interactable ? TextColor : MutedColor;
            Transform iconTransform = button.transform.Find("Icon");
            Image icon = iconTransform ? iconTransform.GetComponent<Image>() : null;
            if (icon) icon.color = button.interactable ? TextColor : MutedColor;
        }

        private static void SetButtonLabel(Button button, string value)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label) label.text = value;
        }

        private void BeginTooltip(RectTransform anchor, string text)
        {
            if (modal.activeSelf) return;
            pendingTooltipAnchor = anchor;
            pendingTooltipText = text;
            tooltipAt = Time.unscaledTime + 0.35f;
        }

        private void EndTooltip()
        {
            pendingTooltipAnchor = null;
            pendingTooltipText = null;
            tooltip.gameObject.SetActive(false);
        }

        private void ShowTooltipNow()
        {
            if (modal.activeSelf) return;
            tooltipText.text = pendingTooltipText;
            Vector3[] corners = new Vector3[4];
            pendingTooltipAnchor.GetWorldCorners(corners);
            Vector2 bottomLeft, topRight;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeRoot,
                RectTransformUtility.WorldToScreenPoint(null, corners[0]),
                null,
                out bottomLeft);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                safeRoot,
                RectTransformUtility.WorldToScreenPoint(null, corners[2]),
                null,
                out topRight);
            Rect safeRect = safeRoot.rect;
            float right = topRight.x + 8f;
            float x = right + tooltip.rect.width <= safeRect.xMax
                ? right
                : Mathf.Max(safeRect.xMin, bottomLeft.x - tooltip.rect.width - 8f);
            float y = Mathf.Clamp((bottomLeft.y + topRight.y) * 0.5f,
                safeRect.yMin + tooltip.rect.height * 0.5f,
                safeRect.yMax - tooltip.rect.height * 0.5f);
            tooltip.localPosition = new Vector3(x, y, 0f);
            tooltip.gameObject.SetActive(true);
            tooltip.SetAsLastSibling();
        }

        private void ApplySafeAreaAndLayout(bool force)
        {
            if (UpdateCanvasScale()) force = true;
            Vector2 displaySize = canvas.renderingDisplaySize;
            Rect safe = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? Screen.safeArea : new Rect(Vector2.zero, displaySize);
            if (!force && safe == lastSafeArea &&
                lastCanvasSize == displaySize) return;
            lastSafeArea = safe;
            lastCanvasSize = displaySize;
            safeRoot.anchorMin = new Vector2(safe.xMin / displaySize.x, safe.yMin / displaySize.y);
            safeRoot.anchorMax = new Vector2(safe.xMax / displaySize.x, safe.yMax / displaySize.y);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
            Canvas.ForceUpdateCanvases();
            double width = safeRoot.rect.width;
            double height = safeRoot.rect.height;
            BlueprintEditorLayout layout = BlueprintEditorLayout.Compute(width, height);
            float catalogScale = (float)Math.Min(
                1.0,
                Math.Min(width * 0.94 / 1420.0, height * 0.90 / 680.0));
            catalogPanel.localScale = Vector3.one * catalogScale;
            Place(top, layout.Top);
            Place(rail, layout.Rail);
            Place(viewport, layout.Viewport);
            Place(rightColumn, layout.RightColumn);
            Place(outliner, layout.Outliner);
            Place(inspector, layout.Inspector);
            Place(status, layout.Status);
            compact = layout.Compact;
            fullTitle.SetActive(!compact);
            compactPaneButton.gameObject.SetActive(compact);
            if (compact)
            {
                double paneHeight = BlueprintEditorLayout.OutlinerHeaderHeight +
                    Math.Floor((layout.RightColumn.Height -
                        BlueprintEditorLayout.OutlinerHeaderHeight) /
                        BlueprintEditorLayout.OutlinerRowHeight) *
                    BlueprintEditorLayout.OutlinerRowHeight;
                var pane = new EditorRect(
                    layout.RightColumn.X,
                    layout.RightColumn.Y,
                    layout.RightColumn.Width,
                    paneHeight);
                Place(outliner, pane);
                Place(inspector, layout.RightColumn);
                outliner.gameObject.SetActive(!compactInspector);
                inspector.gameObject.SetActive(compactInspector);
                SetButtonLabel(compactPaneButton, T(compactInspector
                    ? "editor.view.properties"
                    : "editor.view.objects"));
            }
            else
            {
                outliner.gameObject.SetActive(true);
                inspector.gameObject.SetActive(true);
            }
            double visibleRailHeight = Math.Floor(layout.Rail.Height / 48.0) * 48.0;
            SetTopAnchored(railViewport, 0f, 0f, 0f, (float)visibleRailHeight);
        }

        private bool UpdateCanvasScale()
        {
            Vector2 value = new Vector2(1920f, 1080f) / requestedUiScale;
            if ((canvasScaler.referenceResolution - value).sqrMagnitude < 0.01f)
                return false;
            canvasScaler.referenceResolution = value;
            return true;
        }

        private static void Place(RectTransform target, EditorRect rect)
        {
            target.anchorMin = target.anchorMax = new Vector2(0f, 1f);
            target.pivot = new Vector2(0f, 1f);
            target.anchoredPosition = new Vector2((float)rect.X, (float)-rect.Y);
            target.sizeDelta = new Vector2((float)rect.Width, (float)rect.Height);
        }

        private RectTransform CreatePanel(string name, Transform parent, Color color,
            BlueprintEditorSkin.Surface? surface = BlueprintEditorSkin.Surface.Panel)
        {
            RectTransform rect = CreateRect(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            if (surface.HasValue) skin.Apply(image, surface.Value);
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private Button CreateIconButton(
            string name,
            Transform parent,
            string iconName,
            string fallbackText,
            float width,
            Action clicked,
            string tooltipValue = null,
            bool primary = false)
        {
            Button button = CreateButton(
                name, parent, fallbackText, width, clicked, primary);
            if (!string.IsNullOrWhiteSpace(tooltipValue))
            {
                BlueprintEditorHoverTarget hover =
                    button.gameObject.AddComponent<BlueprintEditorHoverTarget>();
                hover.Initialize(tooltipValue, BeginTooltip, EndTooltip);
            }
            Sprite sprite = icons.Get(iconName);
            if (!sprite) return button;

            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            if (label) label.text = string.Empty;
            RectTransform iconRect = CreateRect("Icon", button.transform);
            SetInsets(iconRect, 6f, 6f, 6f, 6f);
            Image icon = iconRect.gameObject.AddComponent<Image>();
            icon.sprite = sprite;
            icon.color = TextColor;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            return button;
        }

        private Button CreateButton(
            string name,
            Transform parent,
            string labelText,
            float width,
            Action clicked,
            bool primary = false)
        {
            RectTransform rect = CreateRect(name, parent);
            rect.sizeDelta = new Vector2(width, 44f);
            Image image = rect.gameObject.AddComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            skin.Apply(button, primary);
            LayoutElement layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = 44f;
            layout.preferredHeight = 44f;
            button.targetGraphic = image;
            button.onClick.AddListener(() => { EndTooltip(); clicked?.Invoke(); });
            TMP_Text label = CreateText("Label", rect, labelText, 13f, FontStyles.Bold,
                TextAlignmentOptions.Center);
            label.enableAutoSizing = true;
            label.fontSizeMin = 10f;
            label.fontSizeMax = 13f;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.margin = new Vector4(6f, 0f, 6f, 0f);
            label.raycastTarget = false;
            return button;
        }

        private TMP_Text CreateText(
            string name,
            Transform parent,
            string value,
            float size,
            FontStyles style,
            TextAlignmentOptions alignment)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.SetActive(false);
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            RectTransform rect = gameObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            TextMeshProUGUI text = gameObject.AddComponent<TextMeshProUGUI>();
            if (fontTemplate)
            {
                text.font = fontTemplate.font;
                text.fontSharedMaterial = fontTemplate.fontSharedMaterial;
            }
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = TextColor;
            text.raycastTarget = false;
            gameObject.SetActive(true);
            return text;
        }

        private static void SetInsets(
            RectTransform target,
            float left,
            float top,
            float right,
            float bottom)
        {
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;
            target.offsetMin = new Vector2(left, bottom);
            target.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopAnchored(
            RectTransform target,
            float left,
            float top,
            float right,
            float height)
        {
            target.anchorMin = new Vector2(0f, 1f);
            target.anchorMax = new Vector2(1f, 1f);
            target.pivot = new Vector2(0.5f, 1f);
            target.offsetMin = new Vector2(left, -top - height);
            target.offsetMax = new Vector2(-right, -top);
        }

        private static void SetTopRight(
            RectTransform target,
            float right,
            float top,
            float width,
            float height)
        {
            target.anchorMin = target.anchorMax = new Vector2(1f, 1f);
            target.pivot = new Vector2(1f, 1f);
            target.anchoredPosition = new Vector2(-right, -top);
            target.sizeDelta = new Vector2(width, height);
        }

        private static void SetTopLeft(
            RectTransform target,
            float left,
            float top,
            float width,
            float height)
        {
            target.anchorMin = target.anchorMax = new Vector2(0f, 1f);
            target.pivot = new Vector2(0f, 1f);
            target.anchoredPosition = new Vector2(left, -top);
            target.sizeDelta = new Vector2(width, height);
        }

        private static void SetBottomLeft(
            RectTransform target,
            float left,
            float bottom,
            float width,
            float height)
        {
            target.anchorMin = target.anchorMax = new Vector2(0f, 0f);
            target.pivot = new Vector2(0f, 0f);
            target.anchoredPosition = new Vector2(left, bottom);
            target.sizeDelta = new Vector2(width, height);
        }

        private static void ClearChildren(Transform parent)
        {
            for (int index = parent.childCount - 1; index >= 0; --index)
            {
                GameObject child = parent.GetChild(index).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int index = 0; index < values.Count; ++index)
                if (values[index] == value) return true;
            return false;
        }
    }
}
