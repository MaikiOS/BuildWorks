using System;
using System.Globalization;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    internal sealed class PrecisionPlacementHudView : IDisposable
    {
        private readonly Action<GizmoMode> selectMode;
        private readonly Action<GizmoAxis> selectConstraint;
        private readonly Action toggleSpace;
        private readonly Action toggleAnchorVisibility;
        private readonly Action cycleHandleScale;
        private readonly Action cycleTranslationStep;
        private readonly Action cycleRotationStep;
        private readonly Action cycleArrayStep;
        private readonly Action cycleDistribution;
        private readonly Action<float> adjustRise;
        private readonly Action<float> adjustRepeatTurn;
        private readonly Action<float> adjustRepeatPitch;
        private readonly Action<float> adjustRepeatRoll;
        private readonly Action<float> adjustRepeatScale;
        private readonly Action toggleSymmetry;
        private readonly Action toggleMeshSnap;
        private readonly Action toggleAutoAlignment;
        private readonly Action<int> adjustCount;
        private readonly Action saveBlueprint;
        private readonly Action openWorkspaceCatalog;
        private readonly Action cycleEditorLighting;
        private readonly Action undo;
        private readonly Action redo;
        private readonly Action reset;
        private readonly Action cancel;
        private readonly Action confirm;
        private GameObject root;
        private RectTransform panel;
        private GameObject editorControls;
        private GameObject workspaceControls;
        private GameObject pausedControls;
        private TMP_Text pauseReasonText;
        private TMP_Text workspaceCountText;
        private TMP_Text workspaceLightingText;
        private TMP_Text title;
        private TMP_Text interfaceScaleText;
        private TMP_Text hint;
        private TMP_Text values;
        private TMP_Text moveText;
        private TMP_Text rotateText;
        private TMP_Text repeatText;
        private TMP_Text guideText;
        private TMP_Text countText;
        private TMP_Text countMinusText;
        private TMP_Text countPlusText;
        private TMP_Text spaceText;
        private TMP_Text stepText;
        private TMP_Text anchorVisibilityText;
        private TMP_Text handleScaleText;
        private TMP_Text freeConstraintText;
        private TMP_Text xConstraintText;
        private TMP_Text yConstraintText;
        private TMP_Text zConstraintText;
        private TMP_Text undoText;
        private TMP_Text redoText;
        private TMP_Text confirmText;
        private TMP_Text distributionText;
        private TMP_Text riseText;
        private TMP_Text repeatTurnText;
        private TMP_Text repeatPitchText;
        private TMP_Text repeatRollText;
        private TMP_Text repeatScaleText;
        private AngleDragControl riseControl;
        private AngleDragControl repeatScaleControl;
        private AngleDragControl repeatTurnControl;
        private AngleDragControl repeatPitchControl;
        private AngleDragControl repeatRollControl;
        private TMP_Text symmetryText;
        private TMP_Text snapModeText;
        private TMP_Text alignmentText;
        private TMP_Text blueprintSaveText;
        private TMP_Text blueprintText;
        private TMP_Text editorLightingText;
        private GameObject selectionBox;
        private RectTransform selectionBoxRect;

        public bool IsEditingAngle =>
            riseControl != null && riseControl.IsEditing ||
            repeatScaleControl != null && repeatScaleControl.IsEditing ||
            repeatTurnControl != null && repeatTurnControl.IsEditing ||
            repeatPitchControl != null && repeatPitchControl.IsEditing ||
            repeatRollControl != null && repeatRollControl.IsEditing;

        public PrecisionPlacementHudView(
            Action<GizmoMode> selectMode,
            Action<GizmoAxis> selectConstraint,
            Action toggleSpace,
            Action toggleAnchorVisibility,
            Action cycleHandleScale,
            Action cycleTranslationStep,
            Action cycleRotationStep,
            Action cycleArrayStep,
            Action cycleDistribution,
            Action<float> adjustRise,
            Action<float> adjustRepeatTurn,
            Action<float> adjustRepeatPitch,
            Action<float> adjustRepeatRoll,
            Action<float> adjustRepeatScale,
            Action toggleSymmetry,
            Action toggleMeshSnap,
            Action toggleAutoAlignment,
            Action<int> adjustCount,
            Action saveBlueprint,
            Action openWorkspaceCatalog,
            Action cycleEditorLighting,
            Action undo,
            Action redo,
            Action reset,
            Action cancel,
            Action confirm)
        {
            this.selectMode = selectMode;
            this.selectConstraint = selectConstraint;
            this.toggleSpace = toggleSpace;
            this.toggleAnchorVisibility = toggleAnchorVisibility;
            this.cycleHandleScale = cycleHandleScale;
            this.cycleTranslationStep = cycleTranslationStep;
            this.cycleRotationStep = cycleRotationStep;
            this.cycleArrayStep = cycleArrayStep;
            this.cycleDistribution = cycleDistribution;
            this.adjustRise = adjustRise;
            this.adjustRepeatTurn = adjustRepeatTurn;
            this.adjustRepeatPitch = adjustRepeatPitch;
            this.adjustRepeatRoll = adjustRepeatRoll;
            this.adjustRepeatScale = adjustRepeatScale;
            this.toggleSymmetry = toggleSymmetry;
            this.toggleMeshSnap = toggleMeshSnap;
            this.toggleAutoAlignment = toggleAutoAlignment;
            this.adjustCount = adjustCount;
            this.saveBlueprint = saveBlueprint;
            this.openWorkspaceCatalog = openWorkspaceCatalog;
            this.cycleEditorLighting = cycleEditorLighting;
            this.undo = undo;
            this.redo = redo;
            this.reset = reset;
            this.cancel = cancel;
            this.confirm = confirm;
        }

        public void ShowEditing(
            GizmoMode mode,
            GizmoAxis constraintAxis,
            bool localSpace,
            bool showAllAnchors,
            string handleScale,
            int undoCount,
            int redoCount,
            RepeatDistributionMode distribution,
            int copyCount,
            int planeCount,
            float rise,
            float repeatTurn,
            float repeatPitch,
            float repeatRoll,
            float repeatScaleStep,
            bool symmetric,
            bool meshSnapEnabled,
            bool autoAlignmentEnabled,
            string translationStep,
            string rotationStep,
            string step,
            string blueprintName,
            int blueprintParts,
            int blueprintEditPartIndex,
            bool blueprintWorldPlacement,
            bool blueprintWorkspacePartEditing,
            string editorLighting,
            bool canSaveBlueprint,
            bool selectingBlueprint,
            int selectedBlueprintParts,
            string modeHint,
            string offset,
            string rotation)
        {
            if (!EnsureCreated())
            {
                return;
            }
            root.SetActive(true);
            editorControls.SetActive(true);
            workspaceControls.SetActive(false);
            pausedControls.SetActive(false);
            panel.sizeDelta = new Vector2(580f, 350f);
            title.text = selectingBlueprint
                ? "BUILDWORKS — СОЗДАНИЕ ЧЕРТЕЖА"
                : blueprintWorkspacePartEditing
                    ? "BUILDWORKS — ТОЧНОЕ ПОЛОЖЕНИЕ ДЕТАЛИ"
                : blueprintWorldPlacement
                    ? "BUILDWORKS — ТОЧНАЯ УСТАНОВКА ЧЕРТЕЖА"
                : !string.IsNullOrEmpty(blueprintName)
                    ? "BUILDWORKS — РЕДАКТОР ЧЕРТЕЖА"
                    : "BUILDWORKS — ТОЧНАЯ УСТАНОВКА";
            hint.text = selectingBlueprint
                ? "ЛКМ: деталь или рамка  ·  Ctrl: убрать  ·  первая деталь задаёт оси"
                : !string.IsNullOrEmpty(modeHint)
                ? modeHint
                : autoAlignmentEnabled
                    ? "Стрелки: сдвиг  ·  Кольца: вращение  ·  Автостык: оси + точки  ·  Ctrl+точка: ручной магнит"
                    : "Стрелки: сдвиг  ·  Кольца: вращение  ·  Ctrl+точка: магнит";
            values.text = selectingBlueprint
                ? "Выбрано деталей: " + selectedBlueprintParts +
                    "\nНажми СОХРАНИТЬ ВЫБОР, когда состав готов."
                : offset + "\n" + rotation;
            moveText.text = "СДВИГ " + translationStep;
            rotateText.text = "УГОЛ " + rotationStep;
            repeatText.text = mode == GizmoMode.Repeat ? "● МАССИВ" : "МАССИВ";
            guideText.text = mode == GizmoMode.Guide ? "● КОНТУР" : "КОНТУР";
            countText.text = mode == GizmoMode.Repeat
                ? "МАССИВ: " + copyCount + "×" + Math.Max(1, planeCount)
                : mode == GizmoMode.Guide
                    ? "КОНТУР: " + copyCount
                    : "ДЕТАЛЕЙ: " + copyCount;
            spaceText.text = localSpace ? "ОСИ: ЛОК." : "ОСИ: МИР.";
            stepText.text = "ШАГ МАССИВА: " + step;
            anchorVisibilityText.text = showAllAnchors ? "ТОЧКИ: ВСЕ" : "ТОЧКИ: РЯДОМ";
            handleScaleText.text = "РУЧКИ " + handleScale;
            distributionText.text = distribution == RepeatDistributionMode.Fit
                ? "РАСТЯНУТЬ"
                : distribution == RepeatDistributionMode.Exact ? "ТОЧН. ШАГ" : "УПАКОВАТЬ";
            riseControl.SetValue(rise, "ПОДЪЁМ " + rise.ToString("+0.##;-0.##;0") + " м");
            repeatTurnControl.SetValue(repeatTurn,
                "ПОВОРОТ " + repeatTurn.ToString("+0.###;-0.###;0") + "°");
            repeatPitchControl.SetValue(repeatPitch,
                "НАКЛОН " + repeatPitch.ToString("+0.###;-0.###;0") + "°");
            repeatRollControl.SetValue(repeatRoll,
                "КРЕН " + repeatRoll.ToString("+0.###;-0.###;0") + "°");
            repeatScaleControl.SetValue(repeatScaleStep,
                "МАСШТАБ " + repeatScaleStep.ToString("+0.##;-0.##;0") + "%");
            symmetryText.text = symmetric ? "● СИММЕТРИЯ" : "СИММЕТРИЯ";
            snapModeText.text = meshSnapEnabled ? "МАГНИТ: МЕШ" : "МАГНИТ: ИГРА";
            alignmentText.text = autoAlignmentEnabled
                ? "АВТОСТЫК: ВКЛ"
                : "АВТОСТЫК: ВЫКЛ";
            blueprintSaveText.text = selectingBlueprint
                ? "СОХРАНИТЬ ВЫБОР · " + selectedBlueprintParts
                : canSaveBlueprint ? "ВЫБРАТЬ В МИРЕ"
                : blueprintWorkspacePartEditing
                    ? "ЦЕЛЬ: ВРЕМЕННАЯ ДЕТАЛЬ"
                : !string.IsNullOrEmpty(blueprintName)
                    ? blueprintEditPartIndex < 0
                        ? "ЦЕЛЬ: ВСЯ ГРУППА"
                        : "ЦЕЛЬ: ДЕТАЛЬ " + (blueprintEditPartIndex + 1) + "/" +
                            blueprintParts
                    : "ВЫБРАТЬ В МИРЕ";
            blueprintText.text = !string.IsNullOrEmpty(blueprintName)
                ? blueprintWorldPlacement
                    ? "● " + blueprintName + " · редактируется вся группа"
                    : "● " + blueprintName + " · клик по детали или Tab — следующая"
                : string.Empty;
            editorLightingText.text = "СВЕТ: " + editorLighting;
            SetButtonVisible(blueprintSaveText,
                !blueprintWorkspacePartEditing &&
                    (canSaveBlueprint || !string.IsNullOrEmpty(blueprintName)));
            blueprintText.gameObject.SetActive(
                !selectingBlueprint && !blueprintWorkspacePartEditing &&
                !string.IsNullOrEmpty(blueprintName));
            SetButtonVisible(editorLightingText,
                !blueprintWorldPlacement && !selectingBlueprint &&
                    (blueprintWorkspacePartEditing ||
                    !string.IsNullOrEmpty(blueprintName)));
            bool repeatControls = mode == GizmoMode.Repeat;
            bool guideControls = mode == GizmoMode.Guide;
            bool blueprintPartEditing = blueprintWorkspacePartEditing ||
                !string.IsNullOrEmpty(blueprintName) && blueprintEditPartIndex >= 0;
            SetButtonVisible(distributionText, !selectingBlueprint && repeatControls);
            SetButtonVisible(riseText, !selectingBlueprint && repeatControls);
            SetButtonVisible(repeatTurnText, !selectingBlueprint && repeatControls);
            SetButtonVisible(repeatPitchText, !selectingBlueprint && repeatControls);
            SetButtonVisible(repeatRollText, !selectingBlueprint && repeatControls);
            SetButtonVisible(repeatScaleText, !selectingBlueprint && repeatControls);
            SetButtonVisible(symmetryText, !selectingBlueprint && repeatControls);
            SetButtonVisible(moveText, !selectingBlueprint && !guideControls);
            SetButtonVisible(rotateText, !selectingBlueprint);
            SetButtonVisible(repeatText, !selectingBlueprint && !blueprintPartEditing);
            SetButtonVisible(guideText, !selectingBlueprint && !blueprintPartEditing);
            SetButtonVisible(snapModeText, !selectingBlueprint);
            SetButtonVisible(alignmentText, !selectingBlueprint);
            SetButtonVisible(stepText,
                !selectingBlueprint && repeatControls && distribution != RepeatDistributionMode.Fit);
            SetButtonVisible(handleScaleText, !selectingBlueprint);
            countText.gameObject.SetActive(!selectingBlueprint && (repeatControls || guideControls));
            SetButtonVisible(countMinusText, !selectingBlueprint && repeatControls);
            SetButtonVisible(countPlusText, !selectingBlueprint && repeatControls);
            bool anchorControls = !selectingBlueprint;
            SetButtonVisible(freeConstraintText, anchorControls);
            SetButtonVisible(xConstraintText, anchorControls);
            SetButtonVisible(yConstraintText, anchorControls);
            SetButtonVisible(zConstraintText, anchorControls);
            freeConstraintText.text = constraintAxis == GizmoAxis.None ? "● СВОБ." : "СВОБ.";
            xConstraintText.text = constraintAxis == GizmoAxis.X ? "● ВОКРУГ X" : "ВОКРУГ X";
            yConstraintText.text = constraintAxis == GizmoAxis.Y ? "● ВОКРУГ Y" : "ВОКРУГ Y";
            zConstraintText.text = constraintAxis == GizmoAxis.Z ? "● ВОКРУГ Z" : "ВОКРУГ Z";
            undoText.text = "ОТМЕНИТЬ (" + undoCount + ")";
            redoText.text = "ВЕРНУТЬ (" + redoCount + ")";
            confirmText.text = selectingBlueprint ? "СОХРАНИТЬ"
                : blueprintWorldPlacement
                ? "УСТАНОВИТЬ"
                : blueprintWorkspacePartEditing ||
                !string.IsNullOrEmpty(blueprintName)
                ? "ПРИМЕНИТЬ"
                : "УСТАНОВИТЬ";
        }

        public void ShowArmed(
            int placed,
            int total,
            bool waitingForStamina,
            bool waitingForTool,
            bool waitingForSelection,
            bool paused = false,
            string pauseReason = null)
        {
            if (!EnsureCreated())
            {
                return;
            }
            root.SetActive(true);
            editorControls.SetActive(false);
            workspaceControls.SetActive(false);
            pausedControls.SetActive(paused);
            panel.sizeDelta = new Vector2(580f, paused ? 132f : 66f);
            pauseReasonText.text = pauseReason ?? string.Empty;
            title.text = paused
                ? "BUILDWORKS — ПАУЗА " + placed + "/" + total
                : waitingForSelection
                ? "BUILDWORKS — ВЫБЕРИ ПРЕЖНЮЮ ДЕТАЛЬ " + placed + "/" + total
                : waitingForTool
                ? "BUILDWORKS — ЗАМЕНИ МОЛОТОК " + placed + "/" + total
                : waitingForStamina
                    ? "BUILDWORKS — ЖДЁМ ВЫНОСЛИВОСТЬ " + placed + "/" + total
                    : "BUILDWORKS — УСТАНОВКА " + placed + "/" + total;
            hint.text = paused
                ? "Оставшиеся детали сохранены · продолжение с детали " + (placed + 1)
                : waitingForSelection
                ? "Курсор свободен: выбери ту же деталь на новом молотке · серия сохранена"
                : waitingForTool
                ? "Курсор свободен: открой инвентарь и возьми исправный молоток · серия сохранена"
                : waitingForStamina
                    ? "Серия продолжится сама после восстановления · F9 или Esc — отмена"
                    : "Детали устанавливаются автоматически · F9 или Esc — отмена";
        }

        public void ShowFrozen()
        {
            if (!EnsureCreated()) return;
            root.SetActive(true);
            editorControls.SetActive(false);
            workspaceControls.SetActive(false);
            pausedControls.SetActive(false);
            panel.sizeDelta = new Vector2(580f, 66f);
            title.text = "BUILDWORKS — ПОЛОЖЕНИЕ ЗАМОРОЖЕНО";
            hint.text = "Esc: продолжить редактирование · F9: вернуться в обычное строительство";
        }

        public void ShowSelectionBox(Vector2 start, Vector2 end)
        {
            if (!EnsureCreated()) return;
            Vector2 minimum = Vector2.Min(start, end);
            Vector2 maximum = Vector2.Max(start, end);
            selectionBoxRect.anchoredPosition = minimum;
            selectionBoxRect.sizeDelta = maximum - minimum;
            selectionBox.SetActive(true);
        }

        public void HideSelectionBox()
        {
            if (selectionBox) selectionBox.SetActive(false);
        }

        public void ShowPassive(bool continuation)
        {
            if (!EnsureCreated())
            {
                return;
            }
            root.SetActive(true);
            editorControls.SetActive(false);
            workspaceControls.SetActive(false);
            pausedControls.SetActive(false);
            panel.sizeDelta = new Vector2(580f, 58f);
            title.text = continuation
                ? "BUILDWORKS — ЕСТЬ ПРОДОЛЖЕНИЕ РЯДА"
                : "BUILDWORKS — ПОМОЩНИК ВКЛЮЧЁН";
            hint.text = continuation
                ? "Alt+ось: продолжить от конечной точки  ·  F9: редактировать новый объект"
                : "ЛКМ: обычно  ·  Alt+ось: ряд  ·  F9: открыть точное редактирование";
        }

        public void ShowBlueprintWorkspace(
            string blueprintName,
            int partCount,
            string selectedPiece,
            string lighting)
        {
            if (!EnsureCreated()) return;
            root.SetActive(true);
            editorControls.SetActive(false);
            workspaceControls.SetActive(true);
            pausedControls.SetActive(false);
            panel.sizeDelta = new Vector2(580f, 98f);
            title.text = string.IsNullOrEmpty(blueprintName)
                ? "BUILDWORKS — НОВЫЙ ЧЕРТЕЖ"
                : "BUILDWORKS — РЕДАКТОР: " + blueprintName;
            hint.text = "ЛКМ: поставить · СКМ: удалить · ПКМ: каталог/обзор · колесо: повернуть · Ctrl+колесо: масштаб";
            workspaceCountText.text = "ДЕТАЛЕЙ: " + partCount +
                (string.IsNullOrEmpty(selectedPiece) ? string.Empty : " · " + selectedPiece);
            workspaceLightingText.text = "СВЕТ: " + lighting;
        }

        public bool ContainsScreenPoint(Vector2 point)
        {
            if (!root || !root.activeInHierarchy || !panel)
            {
                return false;
            }
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            Camera camera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.RectangleContainsScreenPoint(panel, point, camera);
        }

        public void Hide()
        {
            HideSelectionBox();
            if (root)
            {
                root.SetActive(false);
            }
        }

        public void Dispose()
        {
            if (selectionBox)
            {
                UnityEngine.Object.Destroy(selectionBox);
            }
            if (root)
            {
                UnityEngine.Object.Destroy(root);
            }
            selectionBox = null;
            selectionBoxRect = null;
            workspaceControls = null;
            pausedControls = null;
            pauseReasonText = null;
            workspaceCountText = null;
            workspaceLightingText = null;
            root = null;
            panel = null;
        }

        private bool EnsureCreated()
        {
            Hud hud = Hud.instance;
            if (!hud || !hud.m_rootObject)
            {
                return false;
            }
            if (root && root.transform.parent == hud.m_rootObject.transform)
            {
                ApplyDock();
                return true;
            }
            Dispose();

            root = new GameObject(
                "BuildWorks_PrecisionHud",
                typeof(RectTransform),
                typeof(Image));
            root.transform.SetParent(hud.m_rootObject.transform, false);
            panel = root.GetComponent<RectTransform>();
            Image background = root.GetComponent<Image>();
            Image panelTemplate = hud.m_pieceSelectionWindow
                ? hud.m_pieceSelectionWindow.GetComponent<Image>() ??
                    hud.m_pieceSelectionWindow.GetComponentInChildren<Image>(true)
                : null;
            CopyImageStyle(panelTemplate, background);
            background.color = new Color(0.09f, 0.075f, 0.055f, 0.96f);
            background.raycastTarget = false;

            TMP_Text fontTemplate = hud.m_buildSelection;
            title = CreateText("Title", root.transform, new Vector2(10f, -7f),
                new Vector2(488f, 22f), 15f, TextAlignmentOptions.Center, fontTemplate);
            title.color = new Color(1f, 0.78f, 0.35f, 1f);
            hint = CreateText("Hint", root.transform, new Vector2(10f, -30f),
                new Vector2(560f, 20f), 11f, TextAlignmentOptions.Center, fontTemplate);
            hint.color = new Color(0.9f, 0.88f, 0.8f, 1f);
            Color accent = new Color(0.78f, 0.48f, 0.16f, 0.95f);
            CreateAccent("HeaderRule", root.transform, new Vector2(10f, -52f),
                new Vector2(560f, 1.5f), accent, 0f);
            CreateAccent("LeftRune", root.transform, new Vector2(8f, -17f),
                new Vector2(6f, 6f), accent, 45f);
            CreateAccent("RightRune", root.transform, new Vector2(572f, -17f),
                new Vector2(6f, 6f), accent, 45f);

            editorControls = new GameObject("EditorControls", typeof(RectTransform));
            editorControls.transform.SetParent(root.transform, false);
            RectTransform controlsRect = editorControls.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0f, 1f);
            controlsRect.anchorMax = new Vector2(0f, 1f);
            controlsRect.pivot = new Vector2(0f, 1f);
            controlsRect.anchoredPosition = Vector2.zero;

            selectionBox = new GameObject(
                "BuildWorks_BlueprintSelectionBox",
                typeof(RectTransform),
                typeof(Image));
            selectionBox.transform.SetParent(hud.m_rootObject.transform, false);
            selectionBoxRect = selectionBox.GetComponent<RectTransform>();
            selectionBoxRect.anchorMin = selectionBoxRect.anchorMax = Vector2.zero;
            selectionBoxRect.pivot = Vector2.zero;
            Image selectionImage = selectionBox.GetComponent<Image>();
            selectionImage.color = new Color(1f, 0.62f, 0.12f, 0.18f);
            selectionImage.raycastTarget = false;
            selectionBox.SetActive(false);

            Image buttonTemplate = hud.m_pieceIconPrefab
                ? hud.m_pieceIconPrefab.GetComponentInChildren<Image>(true)
                : null;
            interfaceScaleText = CreateButton("InterfaceScale", root.transform,
                new Vector2(500f, -4f), new Vector2(68f, 24f), buttonTemplate,
                fontTemplate, CycleInterfaceScale);
            pausedControls = new GameObject("PausedPlacementControls", typeof(RectTransform));
            pausedControls.transform.SetParent(root.transform, false);
            RectTransform pausedRect = pausedControls.GetComponent<RectTransform>();
            pausedRect.anchorMin = pausedRect.anchorMax = pausedRect.pivot = new Vector2(0f, 1f);
            pausedRect.anchoredPosition = Vector2.zero;
            pauseReasonText = CreateText("PauseReason", pausedRect, new Vector2(10f, -57f),
                new Vector2(560f, 28f), 11f, TextAlignmentOptions.Center, fontTemplate);
            CreateButton("ResumePlacement", pausedRect, new Vector2(10f, -92f),
                new Vector2(274f, 28f), buttonTemplate, fontTemplate, confirm).text = "ПРОДОЛЖИТЬ · ENTER";
            CreateButton("CancelPlacement", pausedRect, new Vector2(290f, -92f),
                new Vector2(280f, 28f), buttonTemplate, fontTemplate, cancel).text = "ЗАКОНЧИТЬ СЕРИЮ · ESC";
            pausedControls.SetActive(false);
            workspaceControls = new GameObject("BlueprintWorkspaceControls", typeof(RectTransform));
            workspaceControls.transform.SetParent(root.transform, false);
            RectTransform workspaceRect = workspaceControls.GetComponent<RectTransform>();
            workspaceRect.anchorMin = new Vector2(0f, 1f);
            workspaceRect.anchorMax = new Vector2(0f, 1f);
            workspaceRect.pivot = new Vector2(0f, 1f);
            workspaceRect.anchoredPosition = Vector2.zero;
            workspaceCountText = CreateText("WorkspaceCount", workspaceRect,
                new Vector2(10f, -58f), new Vector2(158f, 28f),
                11f, TextAlignmentOptions.MidlineLeft, fontTemplate);
            CreateButton("WorkspaceCatalog", workspaceRect,
                new Vector2(172f, -58f), new Vector2(68f, 28f),
                buttonTemplate, fontTemplate, openWorkspaceCatalog).text = "КАТАЛОГ";
            workspaceLightingText = CreateButton("WorkspaceLighting", workspaceRect,
                new Vector2(244f, -58f), new Vector2(106f, 28f),
                buttonTemplate, fontTemplate, cycleEditorLighting);
            CreateButton("WorkspaceCancel", workspaceRect,
                new Vector2(354f, -58f), new Vector2(96f, 28f),
                buttonTemplate, fontTemplate, cancel).text = "ОТМЕНА";
            CreateButton("WorkspaceSave", workspaceRect,
                new Vector2(454f, -58f), new Vector2(116f, 28f),
                buttonTemplate, fontTemplate, confirm).text = "СОХРАНИТЬ";
            workspaceControls.SetActive(false);

            moveText = CreateButton("Move", controlsRect, new Vector2(10f, -56f),
                new Vector2(128f, 28f), buttonTemplate, fontTemplate,
                cycleTranslationStep);
            rotateText = CreateButton("Rotate", controlsRect, new Vector2(142f, -56f),
                new Vector2(96f, 28f), buttonTemplate, fontTemplate,
                cycleRotationStep);
            repeatText = CreateButton("Repeat", controlsRect, new Vector2(242f, -56f),
                new Vector2(90f, 28f), buttonTemplate, fontTemplate,
                () => selectMode(GizmoMode.Repeat));
            guideText = CreateButton("Guide", controlsRect, new Vector2(337f, -56f),
                new Vector2(90f, 28f), buttonTemplate, fontTemplate,
                () => selectMode(GizmoMode.Guide));
            CreateButton("Dock", controlsRect, new Vector2(432f, -56f),
                new Vector2(60f, 28f), buttonTemplate, fontTemplate, CycleDock).text = "HUD";
            spaceText = CreateButton("Space", controlsRect, new Vector2(496f, -56f),
                new Vector2(74f, 28f), buttonTemplate, fontTemplate, toggleSpace);

            freeConstraintText = CreateButton("ConstraintFree", controlsRect, new Vector2(10f, -90f),
                new Vector2(104f, 28f), buttonTemplate, fontTemplate,
                () => selectConstraint(GizmoAxis.None));
            xConstraintText = CreateButton("ConstraintX", controlsRect, new Vector2(119f, -90f),
                new Vector2(104f, 28f), buttonTemplate, fontTemplate,
                () => selectConstraint(GizmoAxis.X));
            yConstraintText = CreateButton("ConstraintY", controlsRect, new Vector2(228f, -90f),
                new Vector2(104f, 28f), buttonTemplate, fontTemplate,
                () => selectConstraint(GizmoAxis.Y));
            zConstraintText = CreateButton("ConstraintZ", controlsRect, new Vector2(337f, -90f),
                new Vector2(104f, 28f), buttonTemplate, fontTemplate,
                () => selectConstraint(GizmoAxis.Z));
            anchorVisibilityText = CreateButton("AnchorVisibility", controlsRect,
                new Vector2(446f, -90f), new Vector2(124f, 28f),
                buttonTemplate, fontTemplate, toggleAnchorVisibility);

            stepText = CreateButton("Step", controlsRect, new Vector2(10f, -124f),
                new Vector2(100f, 28f), buttonTemplate, fontTemplate, cycleArrayStep);
            handleScaleText = CreateButton("HandleScale", controlsRect, new Vector2(115f, -124f),
                new Vector2(100f, 28f), buttonTemplate, fontTemplate, cycleHandleScale);
            countMinusText = CreateButton("CountMinus", controlsRect, new Vector2(220f, -124f),
                new Vector2(36f, 28f), buttonTemplate, fontTemplate, () => adjustCount(-1));
            countMinusText.text = "−";
            countText = CreateText("Count", controlsRect, new Vector2(260f, -124f),
                new Vector2(100f, 28f), 12f, TextAlignmentOptions.Center, fontTemplate);
            countPlusText = CreateButton("CountPlus", controlsRect, new Vector2(364f, -124f),
                new Vector2(36f, 28f), buttonTemplate, fontTemplate, () => adjustCount(1));
            countPlusText.text = "+";
            snapModeText = CreateButton("SnapMode", controlsRect, new Vector2(405f, -124f),
                new Vector2(165f, 28f), buttonTemplate, fontTemplate, toggleMeshSnap);

            distributionText = CreateButton("Distribution", controlsRect, new Vector2(10f, -158f),
                new Vector2(135f, 28f), buttonTemplate, fontTemplate, cycleDistribution);
            alignmentText = CreateButton("Alignment", controlsRect, new Vector2(150f, -158f),
                new Vector2(135f, 28f), buttonTemplate, fontTemplate, toggleAutoAlignment);
            riseText = CreateAngleControl("Rise", controlsRect, new Vector2(290f, -158f),
                new Vector2(135f, 28f), buttonTemplate, fontTemplate, adjustRise, out riseControl,
                false, 0.05f, -100f, 100f);
            repeatTurnText = CreateAngleControl("RepeatTurn", controlsRect,
                new Vector2(430f, -158f), new Vector2(140f, 28f),
                buttonTemplate, fontTemplate, adjustRepeatTurn, out repeatTurnControl);
            repeatPitchText = CreateAngleControl("RepeatPitch", controlsRect,
                new Vector2(10f, -192f), new Vector2(135f, 28f),
                buttonTemplate, fontTemplate, adjustRepeatPitch, out repeatPitchControl);
            repeatRollText = CreateAngleControl("RepeatRoll", controlsRect,
                new Vector2(150f, -192f), new Vector2(135f, 28f),
                buttonTemplate, fontTemplate, adjustRepeatRoll, out repeatRollControl);
            repeatScaleText = CreateAngleControl("RepeatScale", controlsRect,
                new Vector2(290f, -192f), new Vector2(135f, 28f),
                buttonTemplate, fontTemplate, adjustRepeatScale, out repeatScaleControl,
                false, 1f, -300f, 300f);
            symmetryText = CreateButton("Symmetry", controlsRect, new Vector2(430f, -192f),
                new Vector2(140f, 28f), buttonTemplate, fontTemplate, toggleSymmetry);

            blueprintSaveText = CreateButton("BlueprintSave", controlsRect,
                new Vector2(10f, -226f), new Vector2(170f, 28f),
                buttonTemplate, fontTemplate, saveBlueprint);
            blueprintSaveText.text = "СОХР. ЧЕРТЕЖ";
            blueprintText = CreateText("Blueprint", controlsRect,
                new Vector2(190f, -226f), new Vector2(244f, 28f),
                12f, TextAlignmentOptions.MidlineLeft, fontTemplate);
            blueprintText.color = new Color(1f, 0.78f, 0.35f, 1f);
            editorLightingText = CreateButton("EditorLighting", controlsRect,
                new Vector2(440f, -226f), new Vector2(130f, 28f),
                buttonTemplate, fontTemplate, cycleEditorLighting);

            values = CreateText("Values", controlsRect, new Vector2(10f, -260f),
                new Vector2(560f, 42f), 10f, TextAlignmentOptions.TopLeft, fontTemplate);
            undoText = CreateButton("Undo", controlsRect, new Vector2(10f, -312f),
                new Vector2(106f, 28f), buttonTemplate, fontTemplate, undo);
            redoText = CreateButton("Redo", controlsRect, new Vector2(120f, -312f),
                new Vector2(106f, 28f), buttonTemplate, fontTemplate, redo);
            CreateButton("Reset", controlsRect, new Vector2(230f, -312f),
                new Vector2(106f, 28f), buttonTemplate, fontTemplate, reset).text = "СБРОСИТЬ";
            CreateButton("Cancel", controlsRect, new Vector2(340f, -312f),
                new Vector2(106f, 28f), buttonTemplate, fontTemplate, cancel).text = "ВЫКЛЮЧИТЬ";
            confirmText = CreateButton("Confirm", controlsRect, new Vector2(450f, -312f),
                new Vector2(120f, 28f), buttonTemplate, fontTemplate, confirm);
            confirmText.text = "УСТАНОВИТЬ";

            ApplyDock();
            return true;
        }

        private static void SetButtonVisible(TMP_Text label, bool visible)
        {
            if (label && label.transform.parent)
                label.transform.parent.gameObject.SetActive(visible);
        }

        private void CycleDock()
        {
            BuildWorksPlugin.EditorHudDock.Value =
                (Mathf.Clamp(BuildWorksPlugin.EditorHudDock.Value, 0, 2) + 1) % 3;
            ApplyDock();
        }

        private void CycleInterfaceScale()
        {
            int scale = Mathf.Clamp(BuildWorksPlugin.InterfaceScalePercent.Value, 60, 140);
            BuildWorksPlugin.InterfaceScalePercent.Value = scale >= 140 ? 60 : scale + 10;
            ApplyDock();
        }

        private void ApplyDock()
        {
            if (!panel)
            {
                return;
            }
            int scale = Mathf.Clamp(BuildWorksPlugin.InterfaceScalePercent.Value, 60, 140);
            panel.localScale = Vector3.one * (scale / 100f);
            if (interfaceScaleText) interfaceScaleText.text = "UI " + scale + "%";
            switch (Mathf.Clamp(BuildWorksPlugin.EditorHudDock.Value, 0, 2))
            {
                case 1:
                    SetDock(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 110f));
                    break;
                case 2:
                    SetDock(new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f));
                    break;
                default:
                    SetDock(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 24f));
                    break;
            }
        }

        private void SetDock(Vector2 anchor, Vector2 pivot, Vector2 position)
        {
            panel.anchorMin = anchor;
            panel.anchorMax = anchor;
            panel.pivot = pivot;
            panel.anchoredPosition = position;
        }

        private static TMP_Text CreateButton(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Image imageTemplate,
            TMP_Text fontTemplate,
            Action click)
        {
            GameObject buttonObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            CopyImageStyle(imageTemplate, image);
            image.color = new Color(0.28f, 0.23f, 0.16f, 0.98f);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => click());
            TMP_Text label = CreateText(
                "Label",
                buttonObject.transform,
                Vector2.zero,
                size,
                12f,
                TextAlignmentOptions.Center,
                fontTemplate);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            label.color = Color.white;
            return label;
        }

        private static TMP_Text CreateAngleControl(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Image imageTemplate,
            TMP_Text fontTemplate,
            Action<float> adjust,
            out AngleDragControl control,
            bool normalizeDegrees = true,
            float unitStep = 1f,
            float minimum = float.MinValue,
            float maximum = float.MaxValue)
        {
            GameObject controlObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(TMP_InputField),
                typeof(AngleDragControl));
            controlObject.transform.SetParent(parent, false);
            RectTransform rect = controlObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = controlObject.GetComponent<Image>();
            CopyImageStyle(imageTemplate, image);
            image.color = new Color(0.28f, 0.23f, 0.16f, 0.98f);
            TMP_Text label = CreateText(
                "Label",
                controlObject.transform,
                Vector2.zero,
                size,
                12f,
                TextAlignmentOptions.Center,
                fontTemplate);
            RectTransform labelRect = label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = Vector2.zero;
            label.color = Color.white;
            TMP_InputField input = controlObject.GetComponent<TMP_InputField>();
            input.textComponent = label;
            input.targetGraphic = image;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.enabled = false;
            control = controlObject.GetComponent<AngleDragControl>();
            control.Initialize(adjust, input, label, normalizeDegrees, unitStep, minimum, maximum);
            return label;
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment,
            TMP_Text template)
        {
            TMP_Text text = template
                ? UnityEngine.Object.Instantiate(template, parent, false)
                : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI))
                    .GetComponent<TextMeshProUGUI>();
            if (!template) text.transform.SetParent(parent, false);
            text.gameObject.name = name;
            text.gameObject.SetActive(true);
            text.text = string.Empty;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.raycastTarget = false;
            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        private static void CreateAccent(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 size,
            Color color,
            float angle)
        {
            var accent = new GameObject(name, typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(parent, false);
            RectTransform rect = accent.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            Image image = accent.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        private static void CopyImageStyle(Image source, Image target)
        {
            if (!source)
            {
                return;
            }
            target.sprite = source.sprite;
            target.material = source.material;
            target.type = source.type;
            target.preserveAspect = source.preserveAspect;
        }
    }

    internal sealed class AngleDragControl : MonoBehaviour,
        IBeginDragHandler,
        IDragHandler,
        IScrollHandler,
        IPointerClickHandler
    {
        private Action<float> adjust;
        private TMP_InputField input;
        private TMP_Text label;
        private Vector2 previousPointer;
        private float editStartValue;
        private float editAppliedValue;
        private bool normalizeDegrees;
        private float unitStep;
        private float minimum;
        private float maximum;

        public float Value { get; set; }
        public bool IsEditing => input && input.enabled;

        public void Initialize(Action<float> adjustAngle, TMP_InputField inputField, TMP_Text valueLabel,
            bool normalizeDegrees = true, float unitStep = 1f,
            float minimum = float.MinValue, float maximum = float.MaxValue)
        {
            this.normalizeDegrees = normalizeDegrees;
            this.unitStep = unitStep;
            this.minimum = minimum;
            this.maximum = maximum;
            adjust = adjustAngle;
            input = inputField;
            label = valueLabel;
            input.onValueChanged.AddListener(PreviewTextEdit);
            input.onEndEdit.AddListener(EndTextEdit);
        }

        public void SetValue(float value, string text)
        {
            Value = value;
            if (!IsEditing) label.text = text;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (IsEditing || eventData.button != PointerEventData.InputButton.Left) return;
            eventData.eligibleForClick = false;
            previousPointer = eventData.position;
            eventData.Use();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (IsEditing || eventData.button != PointerEventData.InputButton.Left || adjust == null) return;
            float pixels = eventData.position.x - previousPointer.x;
            previousPointer = eventData.position;
            if (Mathf.Abs(pixels) < 0.01f) return;
            adjust(pixels * DragScale() * unitStep);
            eventData.Use();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (IsEditing || adjust == null || Mathf.Abs(eventData.scrollDelta.y) < 0.01f) return;
            adjust(Mathf.Sign(eventData.scrollDelta.y) * WheelStep() * unitStep);
            eventData.Use();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (adjust == null) return;
            if (eventData.button == PointerEventData.InputButton.Right && !IsEditing)
            {
                adjust(-Value);
                eventData.Use();
            }
            else if (eventData.button == PointerEventData.InputButton.Left && !eventData.dragging && !IsEditing)
            {
                BeginTextEdit();
                eventData.Use();
            }
        }

        private void BeginTextEdit()
        {
            editStartValue = Value;
            editAppliedValue = Value;
            input.enabled = true;
            input.SetTextWithoutNotify(Value.ToString("0.###", CultureInfo.InvariantCulture));
            input.ActivateInputField();
            input.selectionAnchorPosition = 0;
            input.selectionFocusPosition = input.text.Length;
        }

        private void PreviewTextEdit(string text)
        {
            if (!TryParseAngle(text, out float value)) return;
            adjust(value - editAppliedValue);
            editAppliedValue = value;
        }

        private void EndTextEdit(string text)
        {
            if (input.wasCanceled || !TryParseAngle(text, out _))
                adjust(editStartValue - editAppliedValue);
            else
                PreviewTextEdit(text);
            input.enabled = false;
        }

        private bool TryParseAngle(string text, out float value)
        {
            bool parsed = float.TryParse(
                text.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value) && !float.IsNaN(value) && !float.IsInfinity(value);
            if (parsed && normalizeDegrees)
                value = (float)PrecisionAdjustment.NormalizeDegrees(value);
            else if (parsed)
                value = Mathf.Clamp(value, minimum, maximum);
            return parsed;
        }

        private static float DragScale()
        {
            if (ControlHeld()) return 1f;
            if (ShiftHeld()) return 0.1f;
            return 0.25f;
        }

        private static float WheelStep()
        {
            if (ControlHeld()) return 5f;
            if (ShiftHeld()) return 0.1f;
            return 1f;
        }

        private static bool ControlHeld() =>
            Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);

        private static bool ShiftHeld() =>
            Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
    }
}
