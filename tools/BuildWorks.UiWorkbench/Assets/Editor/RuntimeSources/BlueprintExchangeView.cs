using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    // One file workflow for the editor and Hammer; no external picker dependency.
    internal sealed class BlueprintExchangeView : IDisposable
    {
        private static readonly string[] ImportStageKeys = {
            "exchange.stage_1", "exchange.stage_2", "exchange.stage_3", "exchange.stage_4", "exchange.stage_5",
            "exchange.stage_6", "exchange.stage_7", "exchange.stage_8", "exchange.stage_9"
        };
        private readonly GameObject root;
        private readonly RectTransform panel, rows;
        private readonly TMP_Text font, message, pageLabel, folderLabel;
        private readonly TMP_InputField directory, fileName;
        private readonly Button primary, copyPath, format, editPath, cancelPath;
        private readonly RectTransform progress, progressFill;
        private readonly Action<CompositeBlueprintStore.Blueprint[]> importCompleted;
        private string importOperationPath;
        private float operationStarted;
        private bool catalogRetry;
        private RectTransform categoryRow, categoryPicker, categoryRows;
        private TMP_InputField categoryInput, categorySearch;
        private TMP_Text categoryPageLabel;
        private Button categoryPrevious, categoryNext;
        private string[] categoryChoices = Array.Empty<string>();
        private int categoryPage;
        private bool categoryOverride;
        private RectTransform contentsPanel;
        private Button contentsBack, storageChoice, mountChoice, armorChoice, missingChoice;
        private TMP_Text contentsDetails;
        private Task<CompositeBlueprintStore.ContentsSummary> contentsInspection;
        private Task<CompositeBlueprintStore.ImportReview> importInspection;
        private Task<CompositeBlueprintStore.ImportReview> importPreparation;
        private Task<PlanBuildExport> planInspection;
        private Task<string> planExport;
        private PlanBuildExport planReview;
        private CompositeBlueprintStore.ImportReview importReview;
        private CompositeBlueprintStore.ContentsSummary contentsSummary;
        private CompositeBlueprintStore.MissingPartsReview missingReview;
        private bool reviewingContents, clearStorage, clearMounts, omitArmorState, omitMissingParts;
        private readonly BlueprintEditorSkin skin = new BlueprintEditorSkin();
        private readonly CompositeBlueprintStore store;
        private readonly CompositeBlueprintStore.Blueprint blueprint;
        private readonly Action finished;
        private readonly Func<CompositeBlueprintStore.Blueprint, string> place;
        private readonly Func<bool> placementCurrent;
        private readonly int placementRevision;
        private Task<CompositeBlueprintStore.Blueprint> placementPreparation;
        private readonly GameObject previousFocus;
        private Task<string[]> listing;
        private string[] entries = Array.Empty<string>();
        private string selected, resultPath, currentDirectory;
        private int page;
        private bool importing, archive, planBuild, completed, disposed;
        internal bool IsOpen => root && root.activeInHierarchy && !disposed;

        internal BlueprintExchangeView(Transform parent, TMP_Text template, CompositeBlueprintStore library,
            CompositeBlueprintStore.Blueprint source, Action closed, bool frontCanvas = false, string report = null,
            Func<CompositeBlueprintStore.Blueprint, string> place = null, Func<bool> placementCurrent = null,
            Action<CompositeBlueprintStore.Blueprint[]> imported = null)
        {
            store = library; blueprint = source; font = template; finished = closed;
            importCompleted = imported;
            this.place = place; this.placementCurrent = placementCurrent; placementRevision = store.Revision;
            previousFocus = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            var rect = Rect("BuildWorksExchange", parent, 0, 0, 0, 0);
            root = rect.gameObject;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, .78f);
            if (frontCanvas)
            {
                Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
                var canvas = root.AddComponent<Canvas>(); canvas.overrideSorting = true;
                canvas.sortingOrder = (parentCanvas ? parentCanvas.sortingOrder : 0) + 80;
                root.AddComponent<GraphicRaycaster>();
            }
            panel = Rect("ExchangeDialog", rect, 0, 0, 780, 580);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            skin.Apply(panel.gameObject.AddComponent<Image>(), BlueprintEditorSkin.Surface.Panel);
            Label("ExchangeTitle", T(source == null ? "exchange.import" : "exchange.export"), 20, 18, 620, 30, 22);
            Button("ExchangeClose", T("editor.view.close"), 650, 18, 110, Dispose);
            Label("ExchangeHelp", T(source == null ? "exchange.browse_help" : "exchange.export_help"), 20, 56, 740, 26, 14);
            folderLabel = Label("ExchangeFolder", "", 20, 86, 410, 30, 14);
            folderLabel.textWrappingMode = TextWrappingModes.NoWrap;
            folderLabel.overflowMode = TextOverflowModes.Ellipsis;
            editPath = Button("ExchangeEditPath", T("exchange.edit_path"), 438, 86, 144, () => SetDirectoryEditing(true));
            directory = Input("ExchangeDirectory", 20, 86, 562, 1024, 64);
            directory.lineType = TMP_InputField.LineType.MultiLineSubmit;
            directory.textComponent.textWrappingMode = TextWrappingModes.Normal;
            directory.textComponent.alignment = TextAlignmentOptions.TopLeft;
            directory.onSubmit.AddListener(_ => Browse(directory.text));
            cancelPath = Button("ExchangeCancelPath", T("exchange.cancel_path"), 590, 122, 170, () => SetDirectoryEditing(false));
            Button("ExchangeUp", T("exchange.up"), 590, 86, 80, () =>
            {
                try { Browse(Directory.GetParent(currentDirectory)?.FullName ?? currentDirectory); }
                catch (Exception error) { SetError(error); }
            });
            Button("ExchangeGo", T("exchange.go"), 678, 86, 82, () => Browse(directory.text));
            Location("ExchangeHome", T("exchange.library"), store.ExchangeDirectory, 20);
            Location("ExchangeDownloads", T("exchange.downloads"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), 208);
            Location("ExchangeDocuments", T("exchange.documents"), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 396);
            Location("ExchangeDesktop", T("exchange.desktop"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), 584);
            SetDirectoryEditing(false);
            rows = Rect("ExchangeEntries", panel, 20, 174, 740, 224);
            rows.gameObject.AddComponent<Image>().color = new Color(.05f, .055f, .055f, 1);
            Button("ExchangePrevious", "<", 20, 404, 38, () => { if (page > 0) { --page; RenderRows(); } });
            pageLabel = Label("ExchangePage", "", 64, 404, 110, 32, 14);
            Button("ExchangeNext", ">", 178, 404, 38, () => { if ((page + 1) * 7 < entries.Length) { ++page; RenderRows(); } });
            if (source != null)
            {
                Label("ExchangeNameLabel", T("exchange.filename"), 226, 404, 134, 32, 14);
                fileName = Input("ExchangeFileName", 360, 404, 224, 120);
                fileName.SetTextWithoutNotify(CompositeBlueprintStore.ExportName(source.name));
                format = Button("ExchangeFormat", T("exchange.format_json"), 592, 404, 168, () =>
                {
                    if (archive) { archive = false; planBuild = true; }
                    else if (planBuild) planBuild = false;
                    else archive = true;
                    format.GetComponentInChildren<TMP_Text>().text = T(planBuild ? "exchange.format_plan" : archive ? "exchange.format_zip" : "exchange.format_json");
                    panel.Find("ExchangeHelp").GetComponent<TMP_Text>().text = T(planBuild ? "exchange.plan_help" : "exchange.export_help");
                });
            }
            message = Label("ExchangeMessage", "", 20, 446, 740, 70, 14);
            message.overflowMode = TextOverflowModes.Ellipsis;
            progress = Rect("ExchangeProgress", panel, 20, 520, 740, 5);
            progress.gameObject.AddComponent<Image>().color = new Color(.15f, .17f, .17f, 1);
            progressFill = Rect("ExchangeProgressFill", progress, 0, 0, 0, 0);
            progressFill.anchorMin = Vector2.zero; progressFill.anchorMax = Vector2.one;
            progressFill.offsetMin = progressFill.offsetMax = Vector2.zero;
            progressFill.gameObject.AddComponent<Image>().color = new Color(1f, .78f, .32f, 1);
            progress.gameObject.SetActive(false);
            primary = Button("ExchangeSubmit", T(source == null ? "exchange.import" : "exchange.export"), 548, 530, 212, Submit);
            copyPath = Button("ExchangeCopyPath", T("exchange.copy_path"), 256, 530, 276,
                () => GUIUtility.systemCopyBuffer = resultPath ?? selected ?? directory.text);
            if (place != null)
            {
                panel.Find("ExchangeTitle").GetComponent<TMP_Text>().text = T("placement.contents_title");
                panel.Find("ExchangeHelp").GetComponent<TMP_Text>().text = T("placement.contents_help", source.name);
                ReviewContents();
                foreach (Transform child in panel)
                    if (child.name != "ExchangeTitle" && child.name != "ExchangeClose" && child.name != "ExchangeHelp" &&
                        child.name != "ExchangeMessage" && child.name != "ExchangeSubmit" &&
                        child.name != "ExchangeContents" && child.name != "ExchangeContentsBack")
                        child.gameObject.SetActive(false);
                contentsPanel.anchoredPosition = new Vector2(20, -102);
                Tick(); return;
            }
            if (report != null) { ShowReport(report); return; }
            string initial = Directory.Exists(store.ExchangeDirectory) ? store.ExchangeDirectory : Directory.GetParent(store.ExchangeDirectory).FullName;
            if (source != null)
                try { initial = CompositeBlueprintStore.ValidateExchangeDirectory(store.ExportDirectory); Directory.CreateDirectory(initial); }
                catch (Exception error) { SetError(error); }
            Browse(initial);
            Tick();
        }

        internal void Tick()
        {
            if (!IsOpen) return;
            var parent = (RectTransform)root.transform.parent;
            panel.localScale = Vector3.one * Mathf.Min(1, (parent.rect.width - 24) / 780, (parent.rect.height - 24) / 580);
            if (place != null && (store.Revision != placementRevision || placementCurrent?.Invoke() == false) ||
                reviewingContents && !importing && !completed && importReview?.IsHostCurrent?.Invoke() == false)
            {
                foreach (Selectable control in root.GetComponentsInChildren<Selectable>())
                    control.interactable = control is Scrollbar || control.name == "ExchangeClose" || control.name == "ExchangeContentsBack";
                message.text = T("placement.contents_stale"); return;
            }
            if (placementPreparation != null && placementPreparation.IsCompleted)
            {
                try
                {
                    var recipe = placementPreparation.GetAwaiter().GetResult();
                    string error = place(recipe);
                    if (error == null) { Dispose(); return; }
                    message.text = BuildWorksLocalization.ResolveUserText(error);
                }
                catch (Exception error) { SetError(error); }
                string failure = message.text;
                placementPreparation = null;
                foreach (Selectable control in root.GetComponentsInChildren<Selectable>()) control.interactable = true;
                RenderContents();
                message.text = failure;
            }
            if (listing != null && listing.IsCompleted)
            {
                try { entries = listing.GetAwaiter().GetResult(); message.text = entries.Length == 0 && blueprint == null ? T("exchange.empty") : ""; }
                catch (Exception error) { entries = Array.Empty<string>(); SetError(error); }
                listing = null; RenderRows();
            }
            if (importing)
            {
                if (store.ImportOperationPath != importOperationPath)
                {
                    importing = false; completed = true;
                    message.text = T("exchange.operation_changed");
                    UpdateImportProgress();
                    return;
                }
                try { store.CatalogAll(); }
                catch (Exception error)
                {
                    importing = false;
                    if (store.ImportResultPath == importOperationPath)
                        ShowSavedCatalogFailure(BuildWorksLocalization.ResolveUserText(error.Message));
                    else { importReview = null; contentsSummary = null; missingReview = null; SetError(error); }
                    UpdateImportProgress();
                    return;
                }
                if (!store.CatalogLoading)
                {
                    importing = false;
                    if (store.ImportError != null)
                    {
                        if (store.ImportResultPath == importOperationPath)
                        {
                            ShowSavedCatalogFailure(BuildWorksLocalization.ResolveUserText(store.ImportError));
                        }
                        else
                        {
                            contentsBack.interactable = true;
                            RenderContents();
                            importReview = null; contentsSummary = null; missingReview = null;
                            message.text = BuildWorksLocalization.ResolveUserText(store.ImportError);
                        }
                    }
                    else
                    {
                        completed = true; resultPath = store.ImportResultPath;
                        string name = Path.GetFileName(resultPath);
                        var imported = store.CatalogAll().Where(item => item.importSource == name ||
                            (item.importSource ?? "").StartsWith(name + "/", StringComparison.Ordinal)).ToArray();
                        if (imported.Length == 0)
                        { ShowSavedCatalogFailure(T("exchange.result_missing")); UpdateImportProgress(); return; }
                        string[] errors = imported.Where(item => item.importError != null)
                            .Select(item => BuildWorksLocalization.ResolveUserText(item.importError)).Distinct().ToArray();
                        if (imported.Length > 0 && importCompleted != null)
                        {
                            // document_0 is the explicitly stated initial destination for a multi-document archive.
                            imported = imported.OrderBy(item => item.importSource == name + "/document_0.buildworks.json" ? 0 : 1).ToArray();
                            Dispose();
                            importCompleted(imported);
                            return;
                        }
                        ShowResult(T("exchange.imported", resultPath) + (errors.Length == 0 ? "" : "\n" + string.Join("\n", errors)));
                    }
                }
            }
            if (contentsInspection != null && contentsInspection.IsCompleted)
            {
                try { contentsSummary = contentsInspection.GetAwaiter().GetResult(); if (reviewingContents) { RefreshMissing(); RenderContents(); } }
                catch (Exception error) { if (reviewingContents) SetError(error); }
                contentsInspection = null;
            }
            if (importInspection != null && importInspection.IsCompleted)
            {
                try
                {
                    var review = importInspection.GetAwaiter().GetResult();
                    if (reviewingContents) importPreparation = store.PrepareImportReview(review, name => name.GetStableHashCode());
                }
                catch (Exception error) { if (reviewingContents) SetError(error); }
                importInspection = null;
            }
            if (importPreparation != null && importPreparation.IsCompleted)
            {
                try
                {
                    var review = importPreparation.GetAwaiter().GetResult();
                    if (reviewingContents)
                    {
                        if (review.IsHostCurrent?.Invoke() != true) message.text = T("placement.contents_stale");
                        else { importReview = review; contentsSummary = review.Contents; missingReview = review.Missing; RenderContents(); }
                    }
                }
                catch (Exception error) { if (reviewingContents) SetError(error); }
                importPreparation = null;
            }
            if (planInspection != null && planInspection.IsCompleted)
            {
                try { planReview = planInspection.GetAwaiter().GetResult(); if (reviewingContents && planBuild) RenderPlanReview(); }
                catch (Exception error) { if (reviewingContents && planBuild) SetError(error); }
                planInspection = null;
            }
            if (planExport != null && planExport.IsCompleted)
            {
                try { resultPath = planExport.GetAwaiter().GetResult(); completed = true; ShowResult(T("exchange.exported", resultPath)); }
                catch (Exception error) { SetError(error); }
                planExport = null;
            }
            bool busy = importing || planExport != null || placementPreparation != null;
            primary.interactable = catalogRetry && !busy || !completed && !busy && ImportCategoryIsValid() && listing == null && !directory.gameObject.activeSelf &&
                (blueprint != null || selected != null) && (!reviewingContents ||
                (planBuild ? planReview != null : contentsSummary != null && missingReview != null &&
                (blueprint != null || missingReview.EmptyDocuments == 0) && (!omitMissingParts || missingReview.CanOmit)));
            copyPath.interactable = !busy;
            if (completed || busy)
                foreach (Selectable control in root.GetComponentsInChildren<Selectable>())
                    control.interactable = control is Scrollbar || control.name == "ExchangeClose" || control.name == "ExchangeCopyPath" && !busy ||
                        control.name == "ExchangeSubmit" && catalogRetry && !busy;
            else if (contentsBack) contentsBack.interactable = true;
            UpdateCategoryControls(busy);
            UpdateImportProgress();
            if (!busy && reviewingContents && !ImportCategoryIsValid()) message.text = T("exchange.invalid_category");
        }

        private void UpdateImportProgress()
        {
            int stage = importing ? (int)store.ImportProgress : importPreparation != null ? 2 : importInspection != null ? 1 : 0;
            progress.gameObject.SetActive(stage > 0 && !completed);
            panel.Find("ExchangeClose").GetComponentInChildren<TMP_Text>().text = T(importing ? "exchange.background" : "editor.view.close");
            if (stage == 0 || completed) return;
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(stage / 9f), 1f);
            message.text = T("exchange.progress", stage, 9, T(ImportStageKeys[stage - 1]),
                Mathf.Max(0, (int)(Time.realtimeSinceStartup - operationStarted)));
        }

        private void ShowSavedCatalogFailure(string error)
        {
            completed = catalogRetry = true; resultPath = store.ImportResultPath;
            contentsBack.interactable = false;
            primary.GetComponentInChildren<TMP_Text>().text = T("exchange.retry_catalog");
            ShowResult(T("exchange.saved_catalog_failed", resultPath, error));
            primary.interactable = copyPath.interactable = true;
        }

        private void Browse(string path)
        {
            if (listing != null || importing || completed || reviewingContents) return;
            try
            {
                string full = CompositeBlueprintStore.ValidateExchangeDirectory(path);
                currentDirectory = full;
                string leaf = Path.GetFileName(full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                folderLabel.text = T("exchange.current_folder", leaf.Length > 0 ? leaf : full);
                SetDirectoryEditing(false); selected = null; page = 0;
                entries = Array.Empty<string>(); RenderRows();
                message.text = T("exchange.loading");
                listing = Task.Run(() => CompositeBlueprintStore.BrowseExchangeDirectory(full));
            }
            catch (Exception error) { SetError(error); }
        }

        private void SetDirectoryEditing(bool editing)
        {
            directory.SetTextWithoutNotify(currentDirectory ?? "");
            directory.gameObject.SetActive(editing);
            folderLabel.gameObject.SetActive(!editing); editPath.gameObject.SetActive(!editing);
            cancelPath.gameObject.SetActive(editing);
            foreach (string name in new[] { "ExchangeHome", "ExchangeDownloads", "ExchangeDocuments", "ExchangeDesktop" })
                panel.Find(name).gameObject.SetActive(!editing);
            if (editing)
            {
                if (EventSystem.current) EventSystem.current.SetSelectedGameObject(directory.gameObject);
                directory.ActivateInputField();
            }
            else if (EventSystem.current && EventSystem.current.currentSelectedGameObject == directory.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private void RenderRows()
        {
            for (int i = rows.childCount - 1; i >= 0; --i)
            { rows.GetChild(i).gameObject.SetActive(false); UnityEngine.Object.Destroy(rows.GetChild(i).gameObject); }
            pageLabel.text = (page + 1) + " / " + Math.Max(1, (entries.Length + 6) / 7);
            for (int i = page * 7; i < Math.Min(entries.Length, (page + 1) * 7); ++i)
            {
                string path = entries[i]; bool folder = Directory.Exists(path);
                var button = Button("ExchangeEntry" + i, (folder ? T("exchange.folder") + " " : "") + Path.GetFileName(path),
                    0, (i % 7) * 32, 740, () =>
                    {
                        if (folder) Browse(path);
                        else { selected = path; message.text = path; RenderRows(); }
                    }, rows);
                button.interactable = !completed && !importing && (folder || blueprint == null);
                if (selected == path) skin.Apply(button, active: true);
            }
        }

        private void Submit()
        {
            if (catalogRetry && !importing)
            {
                if (!store.RetryImportCatalog(importOperationPath, out string refreshError))
                { message.text = BuildWorksLocalization.ResolveUserText(refreshError); return; }
                catalogRetry = completed = false; importing = true;
                operationStarted = Time.realtimeSinceStartup;
                Tick(); return;
            }
            if (completed || importing || planExport != null || placementPreparation != null || listing != null) return;
            if (place != null)
            {
                if (store.Revision != placementRevision || placementCurrent?.Invoke() == false || contentsSummary == null) return;
                bool storage = clearStorage, mounts = clearMounts;
                placementPreparation = Task.Run(() => CompositeBlueprintStore.CopyWithContentsPolicy(
                    blueprint, storage, mounts, name => name.GetStableHashCode()));
                message.text = T("exchange.loading"); Tick(); return;
            }
            if (!reviewingContents) { if (blueprint != null || selected != null) ReviewContents(); return; }
            if (planBuild)
            {
                if (planReview == null) return;
                try { planExport = store.ExportPlanBuild(planReview, blueprint, directory.text, fileName.text); message.text = T("exchange.loading"); }
                catch (Exception error) { SetError(error); }
                Tick(); return;
            }
            if (contentsSummary == null || missingReview == null) return;
            if (blueprint == null)
            {
                if (selected == null) return;
                CloseCategoryPicker();
                if (!store.TryImportReviewed(importReview, clearStorage, clearMounts, name => name.GetStableHashCode(), out string startError,
                    omitArmorState, omitMissingParts ? missingReview : null, categoryOverride ? categoryInput.text : null))
                { RefreshMissing(); RenderContents(); message.text = BuildWorksLocalization.ResolveUserText(startError); return; }
                importing = true; importOperationPath = store.ImportOperationPath;
                operationStarted = Time.realtimeSinceStartup; message.text = T("exchange.loading");
            }
            else
            {
                if (!store.TryExportFile(blueprint, directory.text, fileName.text, archive, out resultPath, out string error,
                    clearStorage, clearMounts, name => name.GetStableHashCode(), omitArmorState, omitMissingParts ? missingReview : null))
                { RefreshMissing(); RenderContents(); message.text = BuildWorksLocalization.ResolveUserText(error); return; }
                completed = true; ShowResult(T("exchange.exported", resultPath));
            }
            Tick();
        }

        private void SetError(Exception error) => message.text = BuildWorksLocalization.ResolveUserText(error.Message);

        internal void ShowReport(string value)
        {
            completed = true; listing = null; resultPath = value;
            panel.Find("ExchangeTitle").GetComponent<TMP_Text>().text = T("exchange.diagnostics");
            panel.Find("ExchangeHelp").GetComponent<TMP_Text>().text = T("exchange.diagnostics_help");
            copyPath.GetComponentInChildren<TMP_Text>().text = T("exchange.copy_report");
            ShowResult(value); Tick();
        }

        private void ReviewContents()
        {
            operationStarted = Time.realtimeSinceStartup;
            reviewingContents = true; contentsSummary = null; importReview = null; missingReview = null; planReview = null;
            clearStorage = clearMounts = omitArmorState = omitMissingParts = false;
            SetFileControls(false); copyPath.gameObject.SetActive(false);
            if (!contentsPanel)
            {
                contentsPanel = Rect("ExchangeContents", panel, 20, 174, 740, 260);
                contentsPanel.gameObject.AddComponent<Image>().color = new Color(.05f, .055f, .055f, 1);
                Label("ExchangeContentsTitle", T("exchange.contents_title"), 12, 8, 716, 30, 20, contentsPanel);
                var viewport = Rect("ExchangeContentsViewport", contentsPanel, 12, 48, 716, 92);
                viewport.gameObject.AddComponent<Image>().color = new Color(.05f, .055f, .055f, 1);
                viewport.gameObject.AddComponent<RectMask2D>();
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                contentsDetails = Label("ExchangeContentsSummary", "", 0, 0, 700, 92, 16, viewport);
                contentsDetails.alignment = TextAlignmentOptions.TopLeft;
                scroll.viewport = viewport; scroll.content = contentsDetails.rectTransform; scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 132;
                var barRect = Rect("ExchangeContentsScrollbar", viewport, 0, 0, 8, 0);
                barRect.anchorMin = new Vector2(1, 0); barRect.anchorMax = Vector2.one;
                barRect.pivot = new Vector2(1, .5f); barRect.anchoredPosition = Vector2.zero;
                barRect.gameObject.AddComponent<Image>().color = new Color(.08f, .08f, .07f, 1);
                var handle = Rect("ExchangeContentsHandle", barRect, 0, 0, 8, 22);
                var handleImage = handle.gameObject.AddComponent<Image>(); handleImage.color = new Color(.7f, .58f, .32f, 1);
                var scrollbar = barRect.gameObject.AddComponent<Scrollbar>(); scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scrollbar.handleRect = handle; scrollbar.targetGraphic = handleImage;
                scroll.verticalScrollbar = scrollbar; scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
                storageChoice = Button("ExchangeStorageChoice", "", 12, 152, 716, () =>
                { clearStorage = !clearStorage; RenderContents(); }, contentsPanel);
                mountChoice = Button("ExchangeMountChoice", "", 12, 194, 716, () =>
                { clearMounts = !clearMounts; RenderContents(); }, contentsPanel);
                armorChoice = Button("ExchangeArmorChoice", "", 12, 232, 716, () =>
                { omitArmorState = !omitArmorState; RenderContents(); }, contentsPanel);
                missingChoice = Button("ExchangeMissingChoice", "", 12, 232, 716, () =>
                { omitMissingParts = !omitMissingParts; RenderContents(); }, contentsPanel);
                contentsBack = Button("ExchangeContentsBack", T("exchange.back"), 256, 530, 276, () =>
                {
                    if (place != null) { Dispose(); return; }
                    reviewingContents = false; contentsInspection = null; importInspection = null; importPreparation = null; planInspection = null;
                    CloseCategoryPicker();
                    planReview = null; contentsPanel.gameObject.SetActive(false);
                    contentsBack.gameObject.SetActive(false); copyPath.gameObject.SetActive(true);
                    SetFileControls(true); message.text = "";
                    primary.GetComponentInChildren<TMP_Text>().text = T(blueprint == null ? "exchange.import" : "exchange.export");
                });
            }
            contentsPanel.gameObject.SetActive(true); contentsBack.gameObject.SetActive(true);
            if (blueprint == null) PrepareCategoryRow();
            armorChoice.gameObject.SetActive(false); missingChoice.gameObject.SetActive(false);
            storageChoice.interactable = mountChoice.interactable = primary.interactable = false;
            primary.GetComponentInChildren<TMP_Text>().text = T(place != null ? "placement.contents_confirm" : blueprint == null ? "exchange.confirm_import" : "exchange.confirm_export");
            if (place != null) contentsBack.GetComponentInChildren<TMP_Text>().text = T("editor.view.cancel");
            message.text = T("exchange.loading");
            if (planBuild)
            {
                storageChoice.gameObject.SetActive(false); mountChoice.gameObject.SetActive(false);
                contentsDetails.text = "";
                try { planInspection = store.ReviewPlanBuildExport(blueprint); }
                catch (Exception error) { SetError(error); }
                return;
            }
            storageChoice.gameObject.SetActive(true); mountChoice.gameObject.SetActive(true);
            if (blueprint != null)
            {
                var supports = store.MaterialSeedSupportSnapshot(new[] { blueprint });
                contentsInspection = Task.Run(() => CompositeBlueprintStore.InspectContents(blueprint, name => name.GetStableHashCode(), supports));
            }
            else
            {
                string source = selected;
                int worldLevel = Game.m_worldLevel; // Capture on host thread; file worker never reads Unity/game state.
                importInspection = Task.Run(() => CompositeBlueprintStore.ReviewImportFile(source, name => name.GetStableHashCode(), worldLevel));
            }
        }

        private void RenderPlanReview()
        {
            contentsPanel.Find("ExchangeContentsTitle").GetComponent<TMP_Text>().text = T("exchange.plan_title");
            string target = Path.Combine(directory.text, fileName.text);
            contentsDetails.text = T("exchange.plan_review", target, fileName.text, planReview.Parts, planReview.CustomSnaps,
                planReview.Groups, planReview.Supports, planReview.Anchors, planReview.Metadata, planReview.Sources, planReview.MaterialSeeds);
            if (planReview.IgnoredSnapPointRows > 0)
                contentsDetails.text += "\n" + T("exchange.snap_trailing_fields", planReview.IgnoredSnapPointRows);
            if (planReview.OmittedSigns.Count > 0)
                contentsDetails.text += "\n\n" + T("exchange.plan_signs") + "\n" +
                    string.Join("\n", planReview.OmittedSigns.Select(entry => T("exchange.loss_source", entry.Key, entry.Value)));
            if (planReview.OmittedState.Count > 0)
                contentsDetails.text += "\n\n" + T("exchange.plan_state") + "\n" +
                    string.Join("\n", planReview.OmittedState.Select(entry => T("exchange.loss_source", entry.Key, entry.Value)));
            var viewport = (RectTransform)contentsDetails.transform.parent;
            viewport.sizeDelta = new Vector2(716, 206);
            contentsDetails.rectTransform.sizeDelta = new Vector2(700, Mathf.Max(206, contentsDetails.GetPreferredValues(contentsDetails.text, 700, 0).y));
            viewport.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
            contentsPanel.sizeDelta = new Vector2(740, 266);
            primary.GetComponentInChildren<TMP_Text>().text = T("exchange.plan_confirm");
            message.text = T("exchange.plan_warning");
        }

        private void RefreshMissing()
        {
            try
            {
                missingReview = store.InspectMissing(blueprint == null ? importReview.Documents : new[] { blueprint },
                    name => name.GetStableHashCode());
            }
            catch (Exception error) { missingReview = null; SetError(error); }
        }

        private void RenderContents()
        {
            if (missingReview == null || contentsSummary == null) return;
            if (missingReview.Known && missingReview.Parts == 0) omitMissingParts = false;
            var effective = omitMissingParts && missingReview.CanOmit ? missingReview.RetainedContents : contentsSummary;
            contentsDetails.text = T("exchange.contents_counts", effective.Storages, effective.StoredItems,
                effective.Mounts, effective.Unknown, effective.AlreadyCleared);
            if (blueprint == null && categoryOverride &&
                CompositeBlueprintStore.TryNormalizeImportCategory(categoryInput.text, out string category))
                contentsDetails.text += "\n" + T("exchange.category_all", category, importReview.Documents.Count);
            if (blueprint == null && importReview?.Documents.Count > 1)
                contentsDetails.text += "\n" + T("exchange.archive_destination", importReview.Documents[0].name);
            if (effective.IgnoredSnapPointRows > 0)
                contentsDetails.text += "\n" + T("exchange.snap_trailing_fields", effective.IgnoredSnapPointRows);
            if (effective.ArmorStates > 0)
                contentsDetails.text += "\n" + T("exchange.armor_losses", effective.ArmorStates) + "\n" +
                    string.Join("\n", effective.ArmorStateSources.Select(entry => T("exchange.loss_source", entry.Key, entry.Value)));
            if (!missingReview.Known) contentsDetails.text += "\n" + T("exchange.missing_unavailable");
            else if (missingReview.Parts > 0)
                contentsDetails.text += "\n" + T("exchange.missing_losses", missingReview.Parts) + "\n" +
                    string.Join("\n", missingReview.Sources.Select(entry => T("exchange.loss_source", entry.Key, entry.Value)));
            int remainingUnknown = effective.RemainingUnknown(omitArmorState);
            storageChoice.interactable = remainingUnknown == effective.UnreadableMounts && effective.Storages > 0;
            mountChoice.interactable = remainingUnknown == effective.UnreadableStorages && effective.Mounts > 0;
            if (!storageChoice.interactable) clearStorage = false;
            if (!mountChoice.interactable) clearMounts = false;
            contentsPanel.Find("ExchangeContentsTitle").GetComponent<TMP_Text>().text = place != null
                ? T("placement.contents_title") : blueprint == null
                ? T("exchange.import_review", importReview.Documents.Count) : T("exchange.contents_title");
            storageChoice.GetComponentInChildren<TMP_Text>().text = T(place != null ? clearStorage ? "placement.storage_off" : "placement.storage_on" : clearStorage ? "exchange.storage_clear" : "exchange.storage_keep");
            mountChoice.GetComponentInChildren<TMP_Text>().text = T(place != null ? clearMounts ? "placement.mounts_off" : "placement.mounts_on" : clearMounts ? "exchange.mounts_clear" : "exchange.mounts_keep");
            armorChoice.gameObject.SetActive(place == null && effective.ArmorStates > 0);
            armorChoice.interactable = effective.ArmorStates > 0;
            if (!armorChoice.interactable) omitArmorState = false;
            missingChoice.gameObject.SetActive(place == null && missingReview.Known && missingReview.Parts > 0);
            missingChoice.interactable = missingReview.CanOmit || omitMissingParts;
            missingChoice.GetComponentInChildren<TMP_Text>().text = T(omitMissingParts ? "exchange.missing_omit" : "exchange.missing_keep");
            var choices = new[] { storageChoice, mountChoice, armorChoice, missingChoice }.Where(button => button.gameObject.activeSelf).ToArray();
            float contentsHeight = place == null ? 266 : 330;
            float contentTop = categoryRow && categoryRow.gameObject.activeSelf ? 82 : 48;
            float detailsHeight = contentsHeight - contentTop - choices.Length * 34 - 12;
            var viewport = (RectTransform)contentsDetails.transform.parent;
            viewport.anchoredPosition = new Vector2(12, -contentTop);
            viewport.sizeDelta = new Vector2(716, detailsHeight);
            contentsDetails.rectTransform.sizeDelta = new Vector2(700, Mathf.Max(detailsHeight, contentsDetails.GetPreferredValues(contentsDetails.text, 700, 0).y));
            for (int i = 0; i < choices.Length; ++i)
                ((RectTransform)choices[i].transform).anchoredPosition = new Vector2(12, -(contentTop + detailsHeight + 6 + i * 34));
            contentsPanel.sizeDelta = new Vector2(740, contentsHeight);
            armorChoice.GetComponentInChildren<TMP_Text>().text = T(omitArmorState ? "exchange.armor_omit" : "exchange.armor_keep");
            skin.Apply(armorChoice, active: omitArmorState);
            skin.Apply(missingChoice, active: omitMissingParts);
            skin.Apply(storageChoice, active: clearStorage); skin.Apply(mountChoice, active: clearMounts);
            message.text = place != null ? T("placement.contents_policy") : missingReview.EmptyDocuments > 0
                ? T(blueprint == null ? "exchange.missing_import_empty" : "exchange.missing_empty") :
                omitMissingParts ? T("exchange.missing_warning", missingReview.Parts, remainingUnknown, omitArmorState ? effective.ArmorStates : 0) :
                omitArmorState ? T("exchange.armor_warning", effective.ArmorStates, remainingUnknown) :
                T(contentsSummary.Unknown != 0 ? "exchange.contents_unknown" :
                blueprint == null ? "exchange.import_copy" : "exchange.contents_copy");
        }

        private bool ImportCategoryIsValid() => !categoryOverride ||
            CompositeBlueprintStore.TryNormalizeImportCategory(categoryInput.text, out _);

        private void UpdateCategoryControls(bool busy)
        {
            if (!categoryRow || !categoryRow.gameObject.activeInHierarchy) return;
            bool ready = !completed && !busy && reviewingContents && contentsSummary != null && missingReview != null;
            foreach (Selectable control in categoryRow.GetComponentsInChildren<Selectable>()) control.interactable = ready;
        }

        private void PrepareCategoryRow()
        {
            if (!categoryRow)
            {
                categoryRow = Rect("ExchangeCategoryRow", contentsPanel, 12, 48, 716, 30);
                Label("ExchangeCategoryLabel", T("exchange.category"), 0, 0, 90, 30, 14, categoryRow);
                categoryInput = Input("ExchangeCategoryInput", 92, 0, 404, 24, parent: categoryRow);
                categoryInput.placeholder = Label("ExchangeCategoryPlaceholder", T("exchange.category_original"),
                    0, 0, 388, 26, 14, categoryInput.textViewport);
                categoryInput.onValueChanged.AddListener(_ =>
                { categoryOverride = true; if (missingReview != null) RenderContents(); Tick(); });
                Button("ExchangeCategoryChoose", T("exchange.category_choose"), 502, 0, 112, OpenCategoryPicker, categoryRow);
                Button("ExchangeCategoryOriginal", T("exchange.category_reset"), 620, 0, 96, () =>
                { categoryOverride = false; categoryInput.SetTextWithoutNotify(""); RenderContents(); Tick(); }, categoryRow);
            }
            categoryOverride = false; categoryInput.SetTextWithoutNotify(""); CloseCategoryPicker();
        }

        private void OpenCategoryPicker()
        {
            try
            {
                categoryChoices = store.Categories().Concat(store.CatalogAll().Select(item => item.category))
                    .Concat(importReview?.Documents.Select(item => item.category) ?? Array.Empty<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception error) { SetError(error); return; }
            if (!categoryPicker)
            {
                categoryPicker = Rect("ExchangeCategoryPicker", panel, 20, 174, 740, 266);
                skin.Apply(categoryPicker.gameObject.AddComponent<Image>(), BlueprintEditorSkin.Surface.Panel);
                Label("ExchangeCategoryTitle", T("exchange.category_picker"), 12, 8, 590, 30, 20, categoryPicker);
                Button("ExchangeCategoryPickerClose", T("editor.view.cancel"), 620, 8, 108, CloseCategoryPicker, categoryPicker);
                categorySearch = Input("ExchangeCategorySearch", 12, 48, 716, 64, parent: categoryPicker);
                categorySearch.placeholder = Label("ExchangeCategorySearchHint", T("exchange.category_search"),
                    0, 0, 700, 26, 14, categorySearch.textViewport);
                categorySearch.onValueChanged.AddListener(_ => { categoryPage = 0; RenderCategoryChoices(); });
                categoryRows = Rect("ExchangeCategoryRows", categoryPicker, 12, 84, 716, 128);
                categoryPrevious = Button("ExchangeCategoryPrevious", "←", 12, 226, 80, () =>
                { --categoryPage; RenderCategoryChoices(); }, categoryPicker);
                categoryNext = Button("ExchangeCategoryNext", "→", 648, 226, 80, () =>
                { ++categoryPage; RenderCategoryChoices(); }, categoryPicker);
                categoryPageLabel = Label("ExchangeCategoryPage", "", 102, 226, 536, 30, 14, categoryPicker);
            }
            categorySearch.SetTextWithoutNotify(""); categoryPage = 0;
            categoryPicker.gameObject.SetActive(true); categoryPicker.SetAsLastSibling();
            contentsPanel.gameObject.SetActive(false); RenderCategoryChoices();
        }

        private void RenderCategoryChoices()
        {
            for (int i = categoryRows.childCount - 1; i >= 0; --i)
            { categoryRows.GetChild(i).gameObject.SetActive(false); UnityEngine.Object.Destroy(categoryRows.GetChild(i).gameObject); }
            var filtered = categoryChoices.Where(item => item.IndexOf(categorySearch.text, StringComparison.OrdinalIgnoreCase) >= 0).ToArray();
            int pages = Math.Max(1, (filtered.Length + 3) / 4);
            categoryPage = Mathf.Clamp(categoryPage, 0, pages - 1);
            for (int i = categoryPage * 4; i < Math.Min(filtered.Length, (categoryPage + 1) * 4); ++i)
            {
                string name = filtered[i];
                Button("ExchangeCategoryOption" + i, name, 0, (i % 4) * 32, 716, () =>
                { categoryOverride = true; categoryInput.SetTextWithoutNotify(name); CloseCategoryPicker(); RenderContents(); Tick(); }, categoryRows);
            }
            categoryPrevious.interactable = categoryPage > 0; categoryNext.interactable = categoryPage + 1 < pages;
            categoryPageLabel.text = T("exchange.category_page", categoryPage + 1, pages, filtered.Length);
        }

        private void CloseCategoryPicker()
        {
            if (categoryPicker) categoryPicker.gameObject.SetActive(false);
            if (contentsPanel) contentsPanel.gameObject.SetActive(reviewingContents);
        }

        private void SetFileControls(bool visible)
        {
            foreach (Transform child in panel)
                if (child.name == "ExchangeEntries" || child.name == "ExchangePrevious" || child.name == "ExchangeNext" ||
                    child.name == "ExchangePage" || child.name == "ExchangeNameLabel" || child.name == "ExchangeFileName" || child.name == "ExchangeFormat")
                    child.gameObject.SetActive(visible);
            // The reviewed destination and format stay fixed until Back.
            foreach (string name in new[] { "ExchangeDirectory", "ExchangeEditPath", "ExchangeCancelPath", "ExchangeUp", "ExchangeGo", "ExchangeHome",
                "ExchangeDownloads", "ExchangeDocuments", "ExchangeDesktop" })
                panel.Find(name).GetComponent<Selectable>().interactable = visible;
        }

        private void ShowResult(string value)
        {
            SetFileControls(false);
            if (contentsPanel) contentsPanel.gameObject.SetActive(false);
            if (contentsBack) contentsBack.gameObject.SetActive(false);
            copyPath.gameObject.SetActive(true);
            var viewport = panel.Find("ExchangeResult") as RectTransform;
            if (!viewport)
            {
                viewport = Rect("ExchangeResult", panel, 20, 174, 740, 342);
                viewport.gameObject.AddComponent<Image>().color = new Color(.05f, .055f, .055f, 1);
                viewport.gameObject.AddComponent<RectMask2D>();
                viewport.gameObject.AddComponent<ScrollRect>();
            }
            var scroll = viewport.GetComponent<ScrollRect>();
            message.rectTransform.SetParent(viewport, false); message.text = value;
            message.alignment = TextAlignmentOptions.TopLeft;
            message.overflowMode = TextOverflowModes.Overflow;
            message.rectTransform.anchoredPosition = Vector2.zero;
            message.rectTransform.sizeDelta = new Vector2(720, Mathf.Max(342, message.GetPreferredValues(value, 720, 0).y + 20));
            scroll.viewport = viewport; scroll.content = message.rectTransform; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 132;
        }
        private void Location(string name, string label, string path, float x)
        { Button(name, label, x, 130, 176, () => Browse(path)); }
        private static string T(string key, params object[] values) => BuildWorksLocalization.Text(key, values);

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height); return rect;
        }
        private TMP_Text Label(string name, string text, float x, float y, float width, float height, float size, Transform parent = null)
        {
            var label = Rect(name, parent ?? panel, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            if (font) { label.font = font.font; label.fontSharedMaterial = font.fontSharedMaterial; }
            label.text = text; label.fontSize = size; label.color = new Color(.94f, .90f, .80f, 1);
            // Windows paths contain literal \n, \r and \t; never interpret them as layout controls.
            label.richText = false; label.parseCtrlCharacters = false;
            label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
            return label;
        }
        private Button Button(string name, string text, float x, float y, float width, Action action, Transform parent = null)
        {
            var rect = Rect(name, parent ?? panel, x, y, width, 30); rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>(); skin.Apply(button); button.onClick.AddListener(() => action());
            var label = Label(name + "Label", text, 8, 0, width - 16, 30, 14, rect);
            label.overflowMode = TextOverflowModes.Ellipsis;
            return button;
        }
        private TMP_InputField Input(string name, float x, float y, float width, int limit, float height = 30, Transform parent = null)
        {
            var rect = Rect(name, parent ?? panel, x, y, width, height); rect.gameObject.AddComponent<Image>();
            var input = rect.gameObject.AddComponent<TMP_InputField>(); skin.Apply(input, input: true);
            var viewport = Rect(name + "Viewport", rect, 8, 2, width - 16, height - 4); viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label(name + "Text", "", 0, 0, width - 16, height - 4, 14, viewport);
            input.textViewport = viewport; input.textComponent = text; input.characterLimit = limit;
            input.lineType = TMP_InputField.LineType.SingleLine;
            // Assigning the text component can reset wrapping; native caret scrolling owns clipping.
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return input;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            importInspection = null; importPreparation = null;
            planReview?.CancelWrite();
            root.SetActive(false); UnityEngine.Object.Destroy(root); skin.Dispose();
            if (EventSystem.current && previousFocus && previousFocus.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousFocus);
            finished?.Invoke();
        }
    }
}
