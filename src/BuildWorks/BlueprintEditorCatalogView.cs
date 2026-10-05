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
        private TMP_InputField catalogSearch, catalogPageInput;
        private TMP_Text catalogPageText, catalogBreadcrumb, catalogEmpty;
        private Button catalogPartsModeButton, catalogBlueprintsModeButton, catalogAtlasButton, catalogQuickButton,
            catalogDetailsButton, catalogMaterialButton, catalogSourceButton, catalogPreviousPage, catalogNextPage;
        private BlueprintCatalogLayout catalogLayout;
        private bool catalogBlueprintMode, catalogQuick, catalogDetails = true, catalogStateInitialized;
        private int catalogPage, catalogPageCount = 1;
        private readonly HashSet<string> catalogFavorites = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> catalogRecent = new List<string>();
        private readonly CatalogFilters catalogPartFilters = new CatalogFilters(), catalogBlueprintFilters = new CatalogFilters();
        private CatalogFilters CatalogFilter => catalogBlueprintMode ? catalogBlueprintFilters : catalogPartFilters;
        private sealed class CatalogFilters
        {
            internal string Query = "", Section, Family, Material, Source, Station, Kind;
            internal int Page;
        }

        private string CatalogText(string key) => T("editor.catalog." + key);
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
            AddTooltip(inputRect, CatalogText("page_help"));
            catalog.SetActive(false);
        }

        private void LayoutCatalog()
        {
            catalogLayout = BlueprintCatalogLayout.Compute(safeRoot.rect.width, safeRoot.rect.height, catalogQuick, catalogDetails);
            catalogPanel.sizeDelta = new Vector2((float)catalogLayout.Width, (float)catalogLayout.Height);
            catalogPanel.localScale = Vector3.one;
            SetTopLeft((RectTransform)catalogPartsModeButton.transform, 12, 12, 102, 36);
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
        }

        internal void ShowCatalog(IReadOnlyList<BlueprintEditorCatalogItem> items)
        {
            shownCatalogItems.Clear(); if (items != null) shownCatalogItems.AddRange(items);
            catalogStateInitialized = true; catalog.SetActive(true); catalog.transform.SetAsLastSibling();
            SetPanelsInteractable(false); catalogSearch.SetTextWithoutNotify(CatalogFilter.Query);
            RebuildCatalog(); EndTooltip();
        }
        private void SetCatalogMode(bool blueprint)
        {
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
                (f.Section == null || (f.Section == "favorites" ? catalogFavorites.Contains(item.PrefabName) :
                 f.Section == "recent" ? catalogRecent.Contains(item.PrefabName) :
                 catalogBlueprintMode ? f.Section == "category:" + item.Category : item.Section == f.Section)) &&
                (f.Family == null || item.Family == f.Family) && (f.Material == null || item.Material == f.Material) &&
                (f.Source == null || item.Source == f.Source) && (f.Station == null || item.StationId == f.Station) &&
                (f.Kind == null || (f.Kind == "upgrades" ? item.IsUpgrade : f.Kind == "stations" ? item.IsStation : !item.IsUpgrade && !item.IsStation)) &&
                (string.IsNullOrWhiteSpace(f.Query) || Matches(item.DisplayName, f.Query) || Matches(item.PrefabName, f.Query) ||
                 Matches("#" + (item.Index + 1), f.Query))).ToList();
            if (f.Section == "recent") filtered.Sort((a, b) => catalogRecent.IndexOf(a.PrefabName).CompareTo(catalogRecent.IndexOf(b.PrefabName)));
            catalogPageCount = Math.Max(1, (filtered.Count + catalogLayout.PageSize - 1) / catalogLayout.PageSize);
            catalogPage = f.Page = Mathf.Clamp(f.Page, 0, catalogPageCount - 1);
            foreach (BlueprintEditorCatalogItem item in filtered.Skip(catalogPage * catalogLayout.PageSize).Take(catalogLayout.PageSize)) AddCatalogCard(item);
            catalogEmpty.gameObject.SetActive(filtered.Count == 0);
            catalogPageText.text = (catalogPage + 1) + " / " + catalogPageCount;
            catalogPageInput.SetTextWithoutNotify((catalogPage + 1).ToString());
            catalogPreviousPage.interactable = catalogPage > 0; catalogNextPage.interactable = catalogPage + 1 < catalogPageCount;
            catalogBreadcrumb.text = CatalogText("found") + ": " + filtered.Count +
                (f.Material == null ? "" : " · " + BuildWorksLocalization.CatalogLabel(f.Material)) +
                (f.Source == null ? "" : " · " + BuildWorksLocalization.CatalogLabel(f.Source)) +
                (f.Station == null ? "" : " · " + shownCatalogItems.FirstOrDefault(x => x.StationId == f.Station)?.StationName) +
                " · " + CatalogText("click_help");
            SetSelected(catalogPartsModeButton, !catalogBlueprintMode); SetSelected(catalogBlueprintsModeButton, catalogBlueprintMode);
            SetSelected(catalogAtlasButton, !catalogQuick); SetSelected(catalogQuickButton, catalogQuick);
            SetSelected(catalogDetailsButton, catalogDetails && catalogLayout.Details.Width > 0);
            SetButtonLabel(catalogMaterialButton, f.Material == null ? CatalogText("all_materials") : BuildWorksLocalization.CatalogLabel(f.Material));
            SetButtonLabel(catalogSourceButton, f.Source == null ? CatalogText("all_sources") : BuildWorksLocalization.CatalogLabel(f.Source));
            RebuildCatalogNavigation(); RebuildCatalogContext(); PreviewCatalogItem(filtered.FirstOrDefault());
        }
        private void AddCatalogCard(BlueprintEditorCatalogItem item)
        {
            BlueprintEditorCatalogItem captured = item;
            Button button = CreateButton("Catalog_" + item.PrefabName, catalogGrid, item.DisplayName, (float)catalogLayout.CellWidth, () =>
            {
                catalogRecent.Remove(captured.PrefabName); catalogRecent.Insert(0, captured.PrefabName);
                if (catalogRecent.Count > 32) catalogRecent.RemoveAt(32);
                CatalogPartSelected?.Invoke(captured, true);
            });
            LayoutElement element = button.GetComponent<LayoutElement>(); element.minHeight = element.preferredHeight = 152;
            TMP_Text name = button.GetComponentInChildren<TMP_Text>(); name.enableAutoSizing = false; name.fontSize = 14;
            name.textWrappingMode = TextWrappingModes.Normal; name.alignment = TextAlignmentOptions.TopLeft;
            name.margin = Vector4.zero; name.maxVisibleLines = item.IsUpgrade ? 2 : 3;
            SetTopAnchored((RectTransform)name.transform, 8, 94, 8, item.IsUpgrade ? 36 : 52);
            AddCatalogImage(button.transform, "Thumbnail", item.Icon, 8, 6, (float)catalogLayout.CellWidth - 16, 82);
            Button favorite = CreateIconButton("CatalogFavorite", button.transform, "favorite", "*", 28, () =>
            { if (!catalogFavorites.Add(captured.PrefabName)) catalogFavorites.Remove(captured.PrefabName); RebuildCatalog(); }, CatalogText("favorites"));
            SetTopRight((RectTransform)favorite.transform, 4, 4, 28, 28);
            SetSelected(favorite, catalogFavorites.Contains(item.PrefabName));
            if (item.IsUpgrade)
            {
                TMP_Text station = CreateText("StationRelation", button.transform, "→ " + (item.StationName ?? CatalogText("unknown_station")),
                    12, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
                station.textWrappingMode = TextWrappingModes.NoWrap; station.overflowMode = TextOverflowModes.Ellipsis;
                SetTopAnchored((RectTransform)station.transform, 8, 130, 8, 18);
            }
            BlueprintEditorHoverTarget hover = button.gameObject.AddComponent<BlueprintEditorHoverTarget>();
            hover.Initialize(item.DisplayName + "\n" + item.PrefabName + " · #" + (item.Index + 1),
                (rect, text) => { PreviewCatalogItem(captured); BeginTooltip(rect, text); }, EndTooltip);
        }
        private void AddCatalogImage(Transform parent, string name, Sprite sprite, float x, float y, float w, float h)
        {
            RectTransform rect = CreateRect(name, parent); SetTopLeft(rect, x, y, w, h);
            Image image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite;
            image.preserveAspect = true; image.raycastTarget = false; image.color = sprite ? Color.white : Color.clear;
        }
        private void RebuildCatalogNavigation()
        {
            ClearCatalogChildren(catalogNavigation);
            var values = new List<string> { "all", "favorites", "recent" };
            if (catalogBlueprintMode) values.AddRange(shownCatalogItems.Where(x => x.IsBlueprint).Select(x => "category:" + x.Category).Distinct());
            else values.AddRange(new[] { "building", "decor", "furniture", "resources", "crafting" });
            // ponytail: blueprint user categories scroll in Atlas; Quick has a compact category picker.
            if (catalogBlueprintMode && catalogQuick && values.Count > 8) values = new List<string> { "all", "favorites", "recent", "categories" };
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
            ClearCatalogChildren(catalogPreview); if (item == null) return;
            TMP_Text title = CreateText("PreviewName", catalogPreview, item.DisplayName, 18, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            title.textWrappingMode = TextWrappingModes.Normal; title.overflowMode = TextOverflowModes.Ellipsis;
            title.maxVisibleLines = 3;
            SetTopAnchored((RectTransform)title.transform, 12, 12, 12, 62);
            AddCatalogImage(catalogPreview, "PreviewIcon", item.Icon, 12, 78, 220, 140);
            string description = BuildWorksLocalization.CatalogLabel(item.Material) + "\n" + BuildWorksLocalization.CatalogLabel(item.Source) +
                "\n" + item.PrefabName + (item.IsUpgrade ? "\n\n" + CatalogText("upgrade_to") + "\n" + (item.StationName ?? CatalogText("unknown_station")) : "");
            TMP_Text body = CreateText("PreviewMetadata", catalogPreview, description, 14, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            body.textWrappingMode = TextWrappingModes.Normal; body.overflowMode = TextOverflowModes.Ellipsis;
            SetTopAnchored((RectTransform)body.transform, 12, 230, 12, Mathf.Max(60, catalogPreview.rect.height - 300));
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
            else values.AddRange(items.Select(x => kind == "material" ? x.Material : kind == "source" ? x.Source : x.Category)
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
