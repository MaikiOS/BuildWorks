using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class PrecisionPlacementHudView
    {
        private const int WorldSelectionPageSize = 6;
        private GameObject worldSelectionControls;
        private TMP_Text worldSelectionMode, worldSelectionSummary, worldSelectionStatus;
        private TMP_Text worldSelectionBottom, worldSelectionTop, worldSelectionRefresh, worldSelectionPage;
        private readonly TMP_Text[] worldSelectionRows = new TMP_Text[WorldSelectionPageSize];
        private AngleDragControl worldSelectionBottomControl, worldSelectionTopControl;
        private IReadOnlyList<(string name, bool selected)> worldSelectionItems = Array.Empty<(string, bool)>();
        private int worldSelectionPageIndex;
        private Action worldSelectionToggle, worldSelectionApply;
        private Action worldSelectionHeightFinished;
        private Action<float, bool> worldSelectionHeight;
        private Action<int> worldSelectionToggleRow;
        private bool worldSelectionVolume;
        private bool WorldSelectionEditing => worldSelectionControls && worldSelectionControls.activeInHierarchy &&
            (worldSelectionBottomControl.gameObject.activeInHierarchy && worldSelectionBottomControl.IsEditing ||
                worldSelectionTopControl.gameObject.activeInHierarchy && worldSelectionTopControl.IsEditing);

        internal void ConfigureWorldSelection(Action toggle, Action<float, bool> height, Action apply, Action<int> toggleRow,
            Action heightFinished = null)
        {
            worldSelectionToggle = toggle;
            worldSelectionHeight = height;
            worldSelectionApply = apply;
            worldSelectionToggleRow = toggleRow;
            worldSelectionHeightFinished = heightFinished;
        }

        internal void SetWorldSelectionState(bool volume, float bottom, float top, string status,
            IReadOnlyList<(string name, bool selected)> items)
        {
            if (!EnsureCreated()) return;
            worldSelectionVolume = volume;
            worldSelectionItems = items;
            worldSelectionMode.text = BuildWorksLocalization.Text(volume ? "selection.mode_volume" : "selection.mode_rectangle");
            worldSelectionStatus.text = volume ? status : BuildWorksLocalization.Text("selection.rectangle_hint");
            worldSelectionBottomControl.SetValue(bottom, BuildWorksLocalization.Text("selection.bottom", bottom));
            worldSelectionTopControl.SetValue(top, BuildWorksLocalization.Text("selection.top", top));
            SetButtonVisible(worldSelectionBottom, volume);
            SetButtonVisible(worldSelectionTop, volume);
            SetButtonVisible(worldSelectionRefresh, volume);
            UpdateWorldSelectionPage();
        }

        private void CreateWorldSelectionControls(Image buttonTemplate, TMP_Text fontTemplate)
        {
            worldSelectionControls = new GameObject("WorldSelectionControls", typeof(RectTransform));
            worldSelectionControls.transform.SetParent(root.transform, false);
            RectTransform rect = worldSelectionControls.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = Vector2.zero;
            worldSelectionMode = CreateButton("SelectionMode", rect, new Vector2(10, -56), new Vector2(190, 28),
                buttonTemplate, fontTemplate, () => worldSelectionToggle?.Invoke());
            worldSelectionSummary = CreateText("SelectionCount", rect, new Vector2(210, -56), new Vector2(360, 28),
                12, TextAlignmentOptions.MidlineLeft, fontTemplate);
            worldSelectionBottom = CreateAngleControl("VolumeBottom", rect, new Vector2(10, -90), new Vector2(175, 28),
                buttonTemplate, fontTemplate, value => worldSelectionHeight?.Invoke(value, false), out worldSelectionBottomControl,
                false, .05f, -512, 512);
            worldSelectionTop = CreateAngleControl("VolumeTop", rect, new Vector2(190, -90), new Vector2(175, 28),
                buttonTemplate, fontTemplate, value => worldSelectionHeight?.Invoke(value, true), out worldSelectionTopControl,
                false, .05f, -512, 512);
            worldSelectionBottomControl.EditFinished = worldSelectionTopControl.EditFinished = () => worldSelectionHeightFinished?.Invoke();
            worldSelectionRefresh = CreateButton("VolumeRefresh", rect, new Vector2(370, -90), new Vector2(200, 28),
                buttonTemplate, fontTemplate, () => worldSelectionApply?.Invoke());
            worldSelectionRefresh.text = BuildWorksLocalization.Text("selection.refresh");
            worldSelectionStatus = CreateText("SelectionStatus", rect, new Vector2(10, -122), new Vector2(560, 28),
                10, TextAlignmentOptions.TopLeft, fontTemplate);
            for (int i = 0; i < WorldSelectionPageSize; ++i)
            {
                int row = i;
                worldSelectionRows[i] = CreateButton("SelectionRow" + i, rect,
                    new Vector2(10, -152 - i * 22), new Vector2(560, 20), buttonTemplate, fontTemplate,
                    () => worldSelectionToggleRow?.Invoke(worldSelectionPageIndex * WorldSelectionPageSize + row));
                worldSelectionRows[i].fontSize = 11;
                worldSelectionRows[i].overflowMode = TextOverflowModes.Ellipsis;
            }
            CreateButton("SelectionPrevious", rect, new Vector2(10, -288), new Vector2(48, 20),
                buttonTemplate, fontTemplate, () => { --worldSelectionPageIndex; UpdateWorldSelectionPage(); }).text = "‹";
            worldSelectionPage = CreateText("SelectionPage", rect, new Vector2(62, -288), new Vector2(460, 20),
                11, TextAlignmentOptions.Center, fontTemplate);
            CreateButton("SelectionNext", rect, new Vector2(526, -288), new Vector2(44, 20),
                buttonTemplate, fontTemplate, () => { ++worldSelectionPageIndex; UpdateWorldSelectionPage(); }).text = "›";
            CreateButton("SelectionCancel", rect, new Vector2(10, -312), new Vector2(270, 28),
                buttonTemplate, fontTemplate, cancel).text = BuildWorksLocalization.Text("hud.cancel");
            CreateButton("SelectionSave", rect, new Vector2(290, -312), new Vector2(280, 28),
                buttonTemplate, fontTemplate, saveBlueprint).text = BuildWorksLocalization.Text("hud.save");
            worldSelectionControls.SetActive(false);
        }

        private void UpdateWorldSelectionPage()
        {
            int count = worldSelectionVolume ? worldSelectionItems.Count : 0;
            int pages = Math.Max(1, (count + WorldSelectionPageSize - 1) / WorldSelectionPageSize);
            worldSelectionPageIndex = Mathf.Clamp(worldSelectionPageIndex, 0, pages - 1);
            worldSelectionPage.text = count == 0 ? string.Empty :
                BuildWorksLocalization.Text("selection.page", worldSelectionPageIndex + 1, pages, count);
            for (int i = 0; i < WorldSelectionPageSize; ++i)
            {
                int index = worldSelectionPageIndex * WorldSelectionPageSize + i;
                SetButtonVisible(worldSelectionRows[i], index < count);
                if (index < count) worldSelectionRows[i].text = (worldSelectionItems[index].selected ? "[x] " : "[ ] ") +
                    worldSelectionItems[index].name;
            }
        }
    }
}
