using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintEditorView
    {
        private GameObject catalog;
        private RectTransform catalogPanel, catalogGrid, catalogNavigation, catalogContext, catalogPreview, catalogPicker;
        private BlueprintEditorCatalogItem catalogPreviewItem;
        private bool catalogReplacementMode;
        private bool catalogPositioningMode;

        internal void SetCatalogReplacement(bool value, bool positioning = false)
        {
            catalogReplacementMode = value;
            catalogPositioningMode = value && positioning;
            RebuildCatalog();
        }
        internal event Action CatalogImportRequested;
        internal event Action<CompositeBlueprintStore.Blueprint> CatalogExportRequested;
        internal event Action<CompositeBlueprintStore.Blueprint> CatalogManageRequested;
        private Button catalogImportButton, catalogExportButton;
        private TMP_InputField catalogSearch, catalogPageInput;
        private TMP_Text catalogPageText, catalogBreadcrumb, catalogEmpty;
        private Button catalogPartsModeButton, catalogBlueprintsModeButton, catalogAtlasButton, catalogQuickButton,
            catalogDetailsButton, catalogMaterialButton, catalogSourceButton, catalogPreviousPage, catalogNextPage;
        private BlueprintCatalogLayout catalogLayout;
        private bool catalogBlueprintMode, catalogQuick, catalogDetails = true, catalogStateInitialized, catalogAttachmentMode;
        private int catalogPage, catalogPageCount = 1;
        private string catalogNavigationId, catalogNavigationSource;
        private readonly HashSet<string> catalogFavorites = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> catalogRecent = new List<string>();
        private readonly CatalogFilters catalogPartFilters = new CatalogFilters(), catalogBlueprintFilters = new CatalogFilters();
        private readonly CatalogFilters catalogItemFilters = new CatalogFilters();
        private CatalogFilters CatalogFilter => catalogAttachmentMode ? catalogItemFilters : catalogBlueprintMode ? catalogBlueprintFilters : catalogPartFilters;
        private sealed class CatalogFilters
        {
            internal string Query = "", Section, Family, Material, Source, Station, Kind;
            internal int Page;
        }

        private string CatalogText(string key) => T("editor.catalog." + key);
        private bool CompactCatalogHints => HasCatalog && safeRoot.rect.height -
            BlueprintEditorLayout.TopHeight - BlueprintEditorLayout.StatusHeight < 300;
        private void InitializeCatalog()
        {
            catalog = CreatePanel("CatalogOverlay", safeRoot, new Color(0f, 0f, 0f, 0.68f), null).gameObject;
            catalogPanel = CreatePanel("CatalogPanel", catalog.transform, PanelColor);
            catalogPanel.anchorMin = catalogPanel.anchorMax = catalogPanel.pivot = new Vector2(.5f, .5f);
            catalogPartsModeButton = CreateButton("CatalogPartsMode", catalogPanel, CatalogText("parts"), 118,
                () => SetCatalogMode(false));
            catalogBlueprintsModeButton = CreateButton("CatalogBlueprintsMode", catalogPanel, CatalogText("blueprints"), 118,
                () => SetCatalogMode(true));
            catalogAtlasButton = CreateButton("CatalogAtlas", catalogPanel, CatalogText("atlas"), 88,
                () => { catalogQuick = false; RebuildCatalog(); });
            catalogQuickButton = CreateButton("CatalogQuick", catalogPanel, CatalogText("quick"), 88,
                () => { catalogQuick = true; RebuildCatalog(); });
            catalogDetailsButton = CreateIconButton("CatalogPreviewToggle", catalogPanel, "visibility", "…", 36,
                () => { catalogDetails = !catalogDetails; RebuildCatalog(); }, CatalogText("details"));
            Button close = CreateIconButton("CatalogClose", catalogPanel, "close", "×", 36, HideCatalog, T("editor.view.close"));
            SetTopRight((RectTransform)close.transform, 12, 12, 36, 36);
            RectTransform searchRect = CreatePanel("CatalogSearch", catalogPanel, PanelRaised);
            catalogSearch = searchRect.gameObject.AddComponent<TMP_InputField>();
            TMP_Text searchText = CreateText("Text", searchRect, "", 16, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            SetInsets((RectTransform)searchText.transform, 10, 2, 10, 2);
            catalogSearch.textViewport = (RectTransform)searchText.transform;
            catalogSearch.textComponent = searchText;
            TMP_Text placeholder = CreateText("Placeholder", searchRect, CatalogText("search"), 15,
                FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            SetInsets((RectTransform)placeholder.transform, 10, 2, 10, 2);
            catalogSearch.placeholder = placeholder;
            catalogSearch.onValueChanged.AddListener(value => { CatalogFilter.Query = value; CatalogFilter.Page = 0; RebuildCatalog(); });
            catalogMaterialButton = CreateButton("CatalogMaterials", catalogPanel, "", 164, () => OpenCatalogPicker("material"));
            catalogSourceButton = CreateButton("CatalogSources", catalogPanel, "", 164, () => OpenCatalogPicker("source"));
            Button reset = CreateIconButton("CatalogReset", catalogPanel, "undo", "↺", 36, () =>
            {
                CatalogFilter.Section = CatalogFilter.Family = CatalogFilter.Material = CatalogFilter.Source =
                    CatalogFilter.Station = CatalogFilter.Kind = null;
                CatalogFilter.Query = ""; CatalogFilter.Page = 0;
                catalogSearch.SetTextWithoutNotify(""); CloseCatalogPicker(); RebuildCatalog();
            }, CatalogText("reset"));
            SetTopRight((RectTransform)reset.transform, 12, 60, 36, 40);
            catalogNavigation = CreateRect("CatalogNavigation", catalogPanel);
            catalogContext = CreateRect("CatalogContext", catalogPanel);
            catalogGrid = CreateRect("CatalogGrid", catalogPanel);
            GridLayoutGroup grid = catalogGrid.gameObject.AddComponent<GridLayoutGroup>();
            grid.spacing = new Vector2(8, 8); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            catalogGrid.gameObject.AddComponent<BlueprintEditorCatalogScroll>().Scroll = delta =>
            {
                if (IsTextInputFocused || catalogPicker || delta == 0) return;
                if (delta < 0) NextCatalogPage(); else PreviousCatalogPage();
            };
            catalogPreview = CreatePanel("CatalogPreview", catalogPanel, new Color(.06f, .07f, .07f, .9f));
            catalogEmpty = CreateText("CatalogEmpty", catalogPanel, CatalogText("no_results"), 18, FontStyles.Normal, TextAlignmentOptions.Center);
            catalogBreadcrumb = CreateText("CatalogBreadcrumb", catalogPanel, "", 13, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            catalogPreviousPage = CreateButton("CatalogPreviousPage", catalogPanel, "‹", 36, PreviousCatalogPage);
            catalogNextPage = CreateButton("CatalogNextPage", catalogPanel, "›", 36, NextCatalogPage);
            catalogPageText = CreateText("CatalogPageText", catalogPanel, "", 16, FontStyles.Normal, TextAlignmentOptions.Center);
            RectTransform inputRect = CreatePanel("CatalogPageInput", catalogPanel, PanelRaised);
            catalogPageInput = inputRect.gameObject.AddComponent<TMP_InputField>();
            TMP_Text inputText = CreateText("Text", inputRect, "1", 15, FontStyles.Normal, TextAlignmentOptions.Center);
            catalogPageInput.textViewport = (RectTransform)inputText.transform; catalogPageInput.textComponent = inputText;
            catalogPageInput.contentType = TMP_InputField.ContentType.IntegerNumber;
            catalogPageInput.onEndEdit.AddListener(JumpCatalogPage);
            catalogImportButton = CreateButton("CatalogImport", catalogPanel, T("exchange.import"), 106,
                () => CatalogImportRequested?.Invoke());
            catalogExportButton = CreateButton("CatalogExport", catalogPanel, T("exchange.export"), 106,
                () => CatalogExportRequested?.Invoke(catalogPreviewItem?.Blueprint));
            AddTooltip(inputRect, CatalogText("page_help"));
            catalog.SetActive(false);
        }

        private void LayoutCatalog()
        {
            float bottom = hintsExpanded && !CompactCatalogHints ? (float)BlueprintEditorLayout.StatusHeight : 72f;
            float availableHeight = safeRoot.rect.height - (float)BlueprintEditorLayout.TopHeight - bottom;
            float scale = Mathf.Min(.8f, availableHeight / 456f);
            catalogLayout = BlueprintCatalogLayout.Compute(safeRoot.rect.width / scale,
                Mathf.Max(480, availableHeight / scale), catalogQuick, catalogDetails);
            catalogPanel.sizeDelta = new Vector2((float)catalogLayout.Width, (float)catalogLayout.Height);
            catalogPanel.localScale = Vector3.one * scale;
            catalogPanel.anchoredPosition = new Vector2(0, (bottom - (float)BlueprintEditorLayout.TopHeight) * .5f);
            SetTopLeft((RectTransform)catalogPartsModeButton.transform, 12, 12, catalogAttachmentMode ? 218 : 102, 36);
            SetButtonLabel(catalogPartsModeButton, catalogAttachmentMode ? T("editor.item.catalog_title") : CatalogText("parts"));
            catalogBlueprintsModeButton.gameObject.SetActive(!catalogAttachmentMode);
            catalogPartsModeButton.interactable = !catalogAttachmentMode;
            SetTopLeft((RectTransform)catalogBlueprintsModeButton.transform, 120, 12, 112, 36);
            SetTopRight((RectTransform)catalogAtlasButton.transform, 280, 12, 88, 36);
            SetTopRight((RectTransform)catalogQuickButton.transform, 186, 12, 88, 36);
            SetTopRight((RectTransform)catalogDetailsButton.transform, 138, 12, 36, 36);
            SetTopLeft((RectTransform)catalogSearch.transform, 12, 60, (float)catalogLayout.Width - 408, 40);
            SetTopRight((RectTransform)catalogMaterialButton.transform, 226, 60, 164, 40);
            SetTopRight((RectTransform)catalogSourceButton.transform, 56, 60, 164, 40);
            Place(catalogNavigation, catalogLayout.Navigation); Place(catalogContext, catalogLayout.Context);
            Place(catalogGrid, catalogLayout.Grid); Place(catalogPreview, catalogLayout.Details);
            catalogPreview.gameObject.SetActive(catalogLayout.Details.Width > 0);
            Place((RectTransform)catalogEmpty.transform, catalogLayout.Grid);
            GridLayoutGroup grid = catalogGrid.GetComponent<GridLayoutGroup>();
            grid.constraintCount = catalogLayout.Columns;
            grid.cellSize = new Vector2((float)catalogLayout.CellWidth, (float)BlueprintCatalogLayout.CellHeight);
            SetBottomLeft((RectTransform)catalogBreadcrumb.transform, 12, 42, (float)catalogLayout.Width - 24, 20);
            SetBottomLeft((RectTransform)catalogPreviousPage.transform, 12, 8, 36, 30);
            SetBottomLeft((RectTransform)catalogPageText.transform, 54, 8, 88, 30);
            SetBottomLeft((RectTransform)catalogNextPage.transform, 148, 8, 36, 30);
            SetBottomLeft((RectTransform)catalogPageInput.transform, 198, 8, 48, 30);
            SetBottomLeft((RectTransform)catalogImportButton.transform, (float)catalogLayout.Width - 232, 8, 106, 30);
            SetBottomLeft((RectTransform)catalogExportButton.transform, (float)catalogLayout.Width - 118, 8, 106, 30);
            catalogImportButton.gameObject.SetActive(catalogBlueprintMode);
            catalogExportButton.gameObject.SetActive(catalogBlueprintMode);
        }

        internal void ShowCatalog(IReadOnlyList<BlueprintEditorCatalogItem> items)
        {
            catalogAttachmentMode = false;
            if (!catalog.activeSelf) catalogReplacementMode = false;
            OpenCatalog(items);
        }

        internal bool ShowImportedCatalog(IReadOnlyList<BlueprintEditorCatalogItem> items, CompositeBlueprintStore.Blueprint target)
        {
            if (target == null || items == null || !items.Any(item => item.Blueprint?.id == target.id &&
                item.Blueprint.importSource == target.importSource)) return false;
            catalogAttachmentMode = false; catalogBlueprintMode = true;
            var filter = catalogBlueprintFilters;
            filter.Query = ""; filter.Section = "category:" + target.category;
            filter.Family = filter.Material = filter.Source = filter.Station = filter.Kind = null;
            filter.Page = 0;
            catalogNavigationId = target.id; catalogNavigationSource = target.importSource;
            OpenCatalog(items);
            Transform card = catalogGrid.Find("Catalog_" + target.id);
            if (card && EventSystem.current) EventSystem.current.SetSelectedGameObject(card.gameObject);
            return true;
        }

        private static bool SameCatalogItem(BlueprintEditorCatalogItem a, BlueprintEditorCatalogItem b) =>
            a != null && b != null && a.PrefabName == b.PrefabName && a.Blueprint?.importSource == b.Blueprint?.importSource;

        internal void ShowItemStandCatalog(IReadOnlyList<BlueprintEditorCatalogItem> items)
        {
            catalogAttachmentMode = true;
            catalogBlueprintMode = false;
            OpenCatalog(items);
        }

        private void OpenCatalog(IReadOnlyList<BlueprintEditorCatalogItem> items)
        {
            shownCatalogItems.Clear(); if (items != null) shownCatalogItems.AddRange(items);
            catalogStateInitialized = true; catalog.SetActive(true); catalog.transform.SetAsLastSibling();
            SetPanelsInteractable(false); catalogSearch.SetTextWithoutNotify(CatalogFilter.Query);
            ApplySafeAreaAndLayout(true);
            EndTooltip();
        }
        private void SetCatalogMode(bool blueprint)
        {
            if (catalogAttachmentMode) return;
            catalogBlueprintMode = blueprint; CloseCatalogPicker();
            catalogSearch.SetTextWithoutNotify(CatalogFilter.Query); RebuildCatalog();
        }
        private static void ClearCatalogChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; --i)
            { Transform child = parent.GetChild(i); child.gameObject.SetActive(false); child.SetParent(null, false); UnityEngine.Object.Destroy(child.gameObject); }
        }
        private void RebuildCatalog()
        {
            if (!catalogStateInitialized) return;
            LayoutCatalog(); ClearCatalogChildren(catalogGrid);
            CatalogFilters f = CatalogFilter;
            var filtered = shownCatalogItems.Where(item => item.IsBlueprint == catalogBlueprintMode &&
                item.Hidden == (catalogBlueprintMode && f.Section == "hidden") &&
                (f.Section == null || (f.Section == "favorites" ? catalogFavorites.Contains(item.PrefabName) :
                 f.Section == "recent" ? catalogRecent.Contains(item.PrefabName) :
                 catalogBlueprintMode && f.Section == "hidden" ? true :
                 catalogBlueprintMode ? f.Section == "category:" + item.Category : item.Section == f.Section)) &&
                (f.Family == null || item.Family == f.Family) && (f.Material == null || item.Materials.Contains(f.Material)) &&
                (f.Source == null || item.Source == f.Source) && (f.Station == null || item.StationId == f.Station) &&
                (f.Kind == null || (f.Kind == "upgrades" ? item.IsUpgrade : f.Kind == "stations" ? item.IsStation : !item.IsUpgrade && !item.IsStation)) &&
                (string.IsNullOrWhiteSpace(f.Query) || Matches(item.DisplayName, f.Query) || Matches(item.PrefabName, f.Query) ||
                 Matches("#" + (item.Index + 1), f.Query))).ToList();
            if (f.Section == "recent") filtered.Sort((a, b) => catalogRecent.IndexOf(a.PrefabName).CompareTo(catalogRecent.IndexOf(b.PrefabName)));
            catalogPageCount = Math.Max(1, (filtered.Count + catalogLayout.PageSize - 1) / catalogLayout.PageSize);
            if (catalogNavigationId != null)
            {
                int index = filtered.FindIndex(item => item.Blueprint?.id == catalogNavigationId &&
                    item.Blueprint.importSource == catalogNavigationSource);
                if (index >= 0) { f.Page = index / catalogLayout.PageSize; catalogPreviewItem = filtered[index]; }
                catalogNavigationId = catalogNavigationSource = null;
            }
            catalogPage = f.Page = Mathf.Clamp(f.Page, 0, catalogPageCount - 1);
            var visible = filtered.Skip(catalogPage * catalogLayout.PageSize).Take(catalogLayout.PageSize).ToArray();
            var selected = visible.FirstOrDefault(item => SameCatalogItem(item, catalogPreviewItem)) ?? visible.FirstOrDefault();
            catalogPreviewItem = selected;
            foreach (BlueprintEditorCatalogItem item in filtered.Skip(catalogPage * catalogLayout.PageSize).Take(catalogLayout.PageSize)) AddCatalogCard(item);
            catalogEmpty.gameObject.SetActive(filtered.Count == 0);
            catalogPageText.text = (catalogPage + 1) + " / " + catalogPageCount;
            catalogPageInput.SetTextWithoutNotify((catalogPage + 1).ToString());
            catalogPreviousPage.interactable = catalogPage > 0; catalogNextPage.interactable = catalogPage + 1 < catalogPageCount;
            catalogBreadcrumb.text = CatalogText("found") + ": " + filtered.Count +
                (f.Material == null ? "" : " · " + BuildWorksLocalization.CatalogLabel(f.Material)) +
                (f.Source == null ? "" : " · " + BuildWorksLocalization.CatalogLabel(f.Source)) +
                (f.Station == null ? "" : " · " + shownCatalogItems.FirstOrDefault(x => x.StationId == f.Station)?.StationName) +
                " · " + (catalogReplacementMode ? T(catalogPositioningMode ? "editor.position_catalog_help" : "editor.replace_catalog_help") :
                    catalogAttachmentMode ? T("editor.item.click_help") : CatalogText("click_help"));
            SetSelected(catalogPartsModeButton, !catalogBlueprintMode); SetSelected(catalogBlueprintsModeButton, catalogBlueprintMode);
            SetSelected(catalogAtlasButton, !catalogQuick); SetSelected(catalogQuickButton, catalogQuick);
            SetSelected(catalogDetailsButton, catalogDetails && catalogLayout.Details.Width > 0);
            SetButtonLabel(catalogMaterialButton, f.Material == null ? CatalogText("all_materials") : BuildWorksLocalization.CatalogLabel(f.Material));
            SetButtonLabel(catalogSourceButton, f.Source == null ? CatalogText("all_sources") : BuildWorksLocalization.CatalogLabel(f.Source));
            RebuildCatalogNavigation(); RebuildCatalogContext(); PreviewCatalogItem(selected);
        }
        private void AddCatalogCard(BlueprintEditorCatalogItem item)
        {
            BlueprintEditorCatalogItem captured = item;
            Button button = CreateButton("Catalog_" + item.PrefabName, catalogGrid, item.DisplayName, (float)catalogLayout.CellWidth, () =>
            {
                if (captured.Hidden) { PreviewCatalogItem(captured); return; }
                if (!string.IsNullOrEmpty(captured.UnavailableReason))
                { PreviewCatalogItem(captured); SetStatus(captured.UnavailableReason, error: true); return; }
                catalogRecent.Remove(captured.PrefabName); catalogRecent.Insert(0, captured.PrefabName);
                if (catalogRecent.Count > 32) catalogRecent.RemoveAt(32);
                CatalogPartSelected?.Invoke(captured, true);
            });
            LayoutElement element = button.GetComponent<LayoutElement>(); element.minHeight = element.preferredHeight = 152;
            SetSelected(button, SameCatalogItem(item, catalogPreviewItem));
            TMP_Text name = button.GetComponentInChildren<TMP_Text>(); name.enableAutoSizing = false; name.fontSize = 14;
            if (!string.IsNullOrEmpty(item.UnavailableReason) || !string.IsNullOrEmpty(item.WarningReason)) name.text = "[!] " + item.DisplayName;
            name.textWrappingMode = TextWrappingModes.Normal; name.alignment = TextAlignmentOptions.TopLeft;
            name.overflowMode = TextOverflowModes.Ellipsis;
            name.margin = Vector4.zero; name.maxVisibleLines = item.IsUpgrade ? 2 : 3;
            SetTopAnchored((RectTransform)name.transform, 8, 94, 8, item.IsUpgrade ? 36 : 52);
            AddCatalogImage(button.transform, "Thumbnail", item.Icon, 8, 6, (float)catalogLayout.CellWidth - 16, 82);
            if (item.IsBlueprint && !item.Icon)
                AddEmptyPreview(button.transform.Find("Thumbnail"));
            Button favorite = CreateIconButton("CatalogFavorite", button.transform, "favorite", "*", 28, () =>
            { if (!catalogFavorites.Add(captured.PrefabName)) catalogFavorites.Remove(captured.PrefabName); RebuildCatalog(); }, CatalogText("favorites"));
            SetTopRight((RectTransform)favorite.transform, 4, 4, 28, 28);
            SetSelected(favorite, catalogFavorites.Contains(item.PrefabName));
            if (item.IsBlueprint)
            {
                Button manage = CreateIconButton("CatalogManage", button.transform, "menu", "...", 28,
                    () => CatalogManageRequested?.Invoke(captured.Blueprint), CatalogManagementLabel(item));
                SetTopRight((RectTransform)manage.transform, 36, 4, 28, 28);
            }
            if (item.IsUpgrade)
            {
                TMP_Text station = CreateText("StationRelation", button.transform, "→ " + (item.StationName ?? CatalogText("unknown_station")),
                    12, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                station.textWrappingMode = TextWrappingModes.NoWrap; station.overflowMode = TextOverflowModes.Ellipsis;
                SetTopAnchored((RectTransform)station.transform, 8, 130, 8, 18);
            }
            BlueprintEditorHoverTarget hover = button.gameObject.AddComponent<BlueprintEditorHoverTarget>();
            hover.Initialize(item.DisplayName + "\n" + (item.UnavailableReason ?? item.WarningReason ?? item.PrefabName + " · #" + (item.Index + 1)),
                (rect, text) => { PreviewCatalogItem(captured); BeginTooltip(rect, text); }, EndTooltip);
        }
        private void AddCatalogImage(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
        {
            RectTransform rect = CreateRect(name, parent); SetTopLeft(rect, x, y, w, h);
            Image image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite;
            image.preserveAspect = true; image.raycastTarget = false; image.color = sprite ? Color.white : Color.clear;
        }
        private void AddEmptyPreview(Transform parent)
        {
            TMP_Text text = CreateText("EmptyPreview", parent, T("catalog.thumbnail_pending"), 12f,
                FontStyles.Normal, TextAlignmentOptions.Center);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            SetInsets(text.rectTransform, 8f, 30f, 8f, 4f);
        }
        private void RebuildCatalogNavigation()
        {
            ClearCatalogChildren(catalogNavigation);
            var values = new List<string> { "all", "favorites", "recent" };
            if (catalogBlueprintMode)
            {
                values.Add("hidden");
                values.AddRange(shownCatalogItems.Where(x => x.IsBlueprint && !x.Hidden).Select(x => "category:" + x.Category).Distinct());
            }
            else if (!catalogAttachmentMode) values.AddRange(new[] { "building", "decor", "lighting", "furniture", "resources", "crafting" });
            // ponytail: blueprint user categories scroll in Atlas; Quick has a compact category picker.
            if (catalogBlueprintMode && catalogQuick && values.Count > 8) values = new List<string> { "all", "favorites", "recent", "hidden", "categories" };
            int columns = catalogQuick ? Math.Max(1, (int)Math.Floor(catalogNavigation.rect.width / 116)) : 1;
            RectTransform content = catalogNavigation;
            if (!catalogQuick)
            {
                if (!catalogNavigation.GetComponent<RectMask2D>()) catalogNavigation.gameObject.AddComponent<RectMask2D>();
                ScrollRect scroll = catalogNavigation.GetComponent<ScrollRect>() ?? catalogNavigation.gameObject.AddComponent<ScrollRect>();
                scroll.enabled = true;
                content = CreateRect("NavigationContent", catalogNavigation); scroll.viewport = catalogNavigation;
                scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped;
                SetTopLeft(content, 0, 0, 142, Math.Max(catalogNavigation.rect.height, values.Count * 40));
            }
            else { ScrollRect scroll = catalogNavigation.GetComponent<ScrollRect>(); if (scroll) scroll.enabled = false; }
            foreach (string value in values)
            {
                string key = value; int index = values.IndexOf(value);
                string label = key.StartsWith("category:", StringComparison.Ordinal) ? BuildWorksLocalization.CatalogLabel(key.Substring(9)) : CatalogText(key);
                Button button = CreateButton("Category_" + key, content, label, catalogQuick ? catalogNavigation.rect.width / columns - 6 : 142, () =>
                {
                    if (key == "categories") { OpenCatalogPicker("category"); return; }
                    CatalogFilter.Section = key == "all" ? null : key;
                    CatalogFilter.Family = CatalogFilter.Station = CatalogFilter.Kind = null; CatalogFilter.Page = 0; RebuildCatalog();
                });
                SetTopLeft((RectTransform)button.transform, (index % columns) * catalogNavigation.rect.width / columns,
                    (index / columns) * 40, catalogQuick ? catalogNavigation.rect.width / columns - 6 : 142, 34);
                SetSelected(button, (CatalogFilter.Section ?? "all") == key); AddTooltip((RectTransform)button.transform, label);
            }
        }
        private void RebuildCatalogContext()
        {
            ClearCatalogChildren(catalogContext);
            var values = new List<string>();
            if (!catalogBlueprintMode && CatalogFilter.Section == "building") values.AddRange(new[] { "all", "walls", "floors", "roofs", "frame", "openings", "stairs" });
            if (!catalogBlueprintMode && CatalogFilter.Section == "furniture") values.AddRange(new[] { "all", "seating", "beds", "storage", "tables", "rugs", "other_furniture" });
            if (!catalogBlueprintMode && CatalogFilter.Section == "crafting") values.AddRange(new[] { "workstation", "all", "stations", "upgrades", "devices" });
            if (values.Count == 0) return;
            ScrollRect scroll = catalogContext.GetComponent<ScrollRect>() ?? catalogContext.gameObject.AddComponent<ScrollRect>();
            if (!catalogContext.GetComponent<RectMask2D>()) catalogContext.gameObject.AddComponent<RectMask2D>();
            RectTransform content = CreateRect("ContextContent", catalogContext);
            SetTopLeft(content, 0, 0, Math.Max(catalogContext.rect.width, values.Count * 106), 40);
            scroll.viewport = catalogContext; scroll.content = content; scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < values.Count; i++)
            {
                string key = values[i]; bool crafting = CatalogFilter.Section == "crafting";
                Button button = CreateButton("CatalogFamily_" + key, content, CatalogText(key), 100, () =>
                {
                    if (key == "workstation") { OpenCatalogPicker("station"); return; }
                    if (crafting) CatalogFilter.Kind = key == "all" ? null : key; else CatalogFilter.Family = key == "all" ? null : key;
                    CatalogFilter.Page = 0; RebuildCatalog();
                });
                SetTopLeft((RectTransform)button.transform, i * 106, 0, 100, 34);
                SetSelected(button, ((crafting ? CatalogFilter.Kind : CatalogFilter.Family) ?? "all") == key);
            }
        }
        private void PreviewCatalogItem(BlueprintEditorCatalogItem item)
        {
            catalogPreviewItem = item;
            catalogExportButton.interactable = item?.Blueprint != null;
            ClearCatalogChildren(catalogPreview); if (item == null) return;
            TMP_Text title = CreateText("PreviewName", catalogPreview, item.DisplayName, 18, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            title.textWrappingMode = TextWrappingModes.Normal; title.overflowMode = TextOverflowModes.Ellipsis;
            title.maxVisibleLines = 3;
            SetTopAnchored((RectTransform)title.transform, 12, 12, 12, 62);
            AddCatalogImage(catalogPreview, "PreviewIcon", item.Icon, 12, 78, 220, 140);
            if (item.IsBlueprint && !item.Icon) AddEmptyPreview(catalogPreview.Find("PreviewIcon"));
            string description = BuildWorksLocalization.CatalogLabel(item.Material) + "\n" + BuildWorksLocalization.CatalogLabel(item.Source) +
                "\n" + item.PrefabName + (item.IsUpgrade ? "\n\n" + CatalogText("upgrade_to") + "\n" + (item.StationName ?? CatalogText("unknown_station")) : "");
            if (item.IsBlueprint && item.Blueprint.IsExternal)
                description = BuildWorksLocalization.Text("import.read_only") + "\n" + item.Blueprint.importSource +
                    "\n" + BuildWorksLocalization.Text("catalog.card.parts", item.Blueprint.importPartCount);
            if (item.ItemContents != null) description = T("editor.item.defaults") + "\n\n" + item.PrefabName;
            if (!string.IsNullOrEmpty(item.UnavailableReason)) description += "\n\n" + item.UnavailableReason;
            else if (!string.IsNullOrEmpty(item.WarningReason)) description += "\n\n" + item.WarningReason +
                (item.IsBlueprint ? "\n" + T("catalog.thumbnail_markers") : "");
            if (!item.ResourcesKnown) description += "\n\n" + T("editor.catalog_cost_unknown");
            RectTransform viewport = CreatePanel("PreviewBody", catalogPreview, Color.clear, null);
            SetTopAnchored(viewport, 8, 224, 8, Mathf.Max(32, catalogPreview.rect.height - 286));
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            RectTransform content = CreateRect("PreviewBodyContent", viewport);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            TMP_Text body = CreateText("PreviewMetadata", content, description, 14, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal; body.overflowMode = TextOverflowModes.Ellipsis;
            float metadataHeight = body.GetPreferredValues(description, 204, Mathf.Infinity).y + 8;
            float y = 0;
            if (item.IsBlueprint)
            {
                Button manage = CreateButton("CatalogManageDetails", content, CatalogManagementLabel(item), 204,
                    () => CatalogManageRequested?.Invoke(item.Blueprint));
                SetTopLeft((RectTransform)manage.transform, 4, 0, 204, 38);
                y = 44;
            }
            SetTopLeft((RectTransform)body.transform, 4, y, 204, metadataHeight);
            y += metadataHeight;
            if (!string.IsNullOrEmpty(item.UnavailableReason) || item.ItemContents != null || !item.ResourcesKnown)
            { SetTopLeft(content, 0, 0, 212, y); return; }
            TMP_Text costTitle = CreateText("PreviewCostTitle", content, CatalogText("build_cost"), 14, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            SetTopLeft(costTitle.rectTransform, 4, y, 204, 24); y += 26;
            foreach (BlueprintEditorCatalogResource resource in item.Resources)
            {
                AddCatalogImage(content, "ResourceIcon", resource.Icon, 4, y, 28, 28);
                TMP_Text row = CreateText("ResourceCost", content, resource.Name + " × " + resource.Amount, 13, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                row.textWrappingMode = TextWrappingModes.Normal;
                float rowHeight = Mathf.Max(32, row.GetPreferredValues(row.text, 166, Mathf.Infinity).y + 4);
                SetTopLeft(row.rectTransform, 38, y, 166, rowHeight); y += rowHeight;
            }
            AddCatalogImage(content, "RequiredStationIcon", item.RequiredStationIcon, 4, y, 28, 28);
            TMP_Text required = CreateText("RequiredStation", content,
                CatalogText("required_station") + ": " + (item.RequiredStation ?? CatalogText("no_station")), 13, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            required.textWrappingMode = TextWrappingModes.Normal;
            float stationHeight = Mathf.Max(36, required.GetPreferredValues(required.text, 166, Mathf.Infinity).y + 8);
            SetTopLeft(required.rectTransform, 38, y, 166, stationHeight);
            SetTopLeft(content, 0, 0, 212, y + stationHeight);
            if (!string.IsNullOrEmpty(item.StationId))
            {
                AddCatalogImage(catalogPreview, "StationIcon", item.StationIcon, 12, catalogPreview.rect.height - 54, 32, 32);
                Button station = CreateButton("CatalogGoToStation", catalogPreview, CatalogText("station_group"), 178, () =>
                {
                    CatalogFilter.Section = "crafting"; CatalogFilter.Family = CatalogFilter.Kind = null;
                    CatalogFilter.Station = item.StationId; CatalogFilter.Page = 0; RebuildCatalog();
                });
                SetBottomLeft((RectTransform)station.transform, 50, 12, 178, 38);
            }
        }
        private static string CatalogManagementLabel(BlueprintEditorCatalogItem item) => BuildWorksLocalization.Text(
            !item.Blueprint.IsExternal ? "catalog.action.delete" : item.Hidden ? "catalog.action.restore" : "catalog.action.hide");
        internal void UpdateCatalogThumbnail(string id, Sprite icon)
        {
            foreach (BlueprintEditorCatalogItem item in shownCatalogItems)
            {
                if (item.Blueprint?.id != id) continue;
                item.Icon = icon;
                Transform image = catalogGrid ? catalogGrid.Find("Catalog_" + item.PrefabName + "/Thumbnail") : null;
                if (image && image.Find("EmptyPreview")) UnityEngine.Object.Destroy(image.Find("EmptyPreview").gameObject);
                if (image) { var graphic = image.GetComponent<Image>(); graphic.sprite = icon; graphic.color = Color.white; }
                if (catalogPreviewItem == item && catalogPreview)
                {
                    Transform preview = catalogPreview.Find("PreviewIcon");
                    if (preview && preview.Find("EmptyPreview")) UnityEngine.Object.Destroy(preview.Find("EmptyPreview").gameObject);
                    if (preview) { var graphic = preview.GetComponent<Image>(); graphic.sprite = icon; graphic.color = Color.white; }
                }
            }
        }
        private void OpenCatalogPicker(string kind)
        {
            CloseCatalogPicker();
            catalogPicker = CreatePanel("CatalogPicker", catalogPanel, PanelColor);
            SetTopRight(catalogPicker, 12, 104, 300, Mathf.Min(420, (float)catalogLayout.Height - 174));
            Button close = CreateIconButton("PickerClose", catalogPicker, "close", "×", 30, CloseCatalogPicker, T("editor.view.close"));
            SetTopRight((RectTransform)close.transform, 6, 6, 30, 30);
            TMP_Text title = CreateText("PickerTitle", catalogPicker, CatalogText(kind), 16, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            SetTopLeft((RectTransform)title.transform, 10, 6, 242, 30);
            RectTransform viewport = CreateRect("CatalogPickerViewport", catalogPicker); SetInsets(viewport, 8, 42, 8, 8);
            viewport.gameObject.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.gameObject.AddComponent<ScrollRect>();
            RectTransform content = CreateRect("CatalogPickerContent", viewport);
            var items = shownCatalogItems.Where(x => x.IsBlueprint == catalogBlueprintMode).ToList();
            var values = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>(null, CatalogText("all")) };
            if (kind == "station") values.AddRange(items.Where(x => x.StationId != null).GroupBy(x => x.StationId)
                .Select(x => new KeyValuePair<string, string>(x.Key, x.First().StationName ?? x.Key)).OrderBy(x => x.Value));
            else if (kind == "material") values.AddRange(items.OrderBy(x => x.MaterialOrder).SelectMany(x => x.Materials)
                .Distinct().Select(x => new KeyValuePair<string, string>(x, BuildWorksLocalization.CatalogLabel(x))));
            else values.AddRange(items.Select(x => kind == "source" ? x.Source : x.Category)
                .Distinct().OrderBy(x => x).Select(x => new KeyValuePair<string, string>(x, BuildWorksLocalization.CatalogLabel(x))));
            SetTopLeft(content, 0, 0, 284, values.Count * 38);
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            for (int i = 0; i < values.Count; i++)
            {
                KeyValuePair<string, string> value = values[i];
                Button button = CreateButton("CatalogOption_" + value.Key, content, value.Value, 284, () =>
                {
                    if (kind == "material") CatalogFilter.Material = value.Key;
                    else if (kind == "source") CatalogFilter.Source = value.Key;
                    else if (kind == "station") { CatalogFilter.Section = "crafting"; CatalogFilter.Station = value.Key; CatalogFilter.Kind = CatalogFilter.Family = null; }
                    else CatalogFilter.Section = value.Key == null ? null : "category:" + value.Key;
                    CatalogFilter.Page = 0; CloseCatalogPicker(); RebuildCatalog();
                });
                SetTopLeft((RectTransform)button.transform, 0, i * 38, 284, 34); AddTooltip((RectTransform)button.transform, value.Value);
            }
        }
        private void CloseCatalogPicker()
        {
            if (!catalogPicker) return;
            catalogPicker.gameObject.SetActive(false); UnityEngine.Object.Destroy(catalogPicker.gameObject); catalogPicker = null;
        }
        internal void CancelCatalog() { if (catalogPicker) CloseCatalogPicker(); else HideCatalog(); }
        private void PreviousCatalogPage() { CatalogFilter.Page = Math.Max(0, catalogPage - 1); RebuildCatalog(); }
        private void NextCatalogPage() { CatalogFilter.Page = Math.Min(catalogPageCount - 1, catalogPage + 1); RebuildCatalog(); }
        private void JumpCatalogPage(string value) { if (int.TryParse(value, out int page)) { CatalogFilter.Page = page - 1; RebuildCatalog(); } }
    }
}
