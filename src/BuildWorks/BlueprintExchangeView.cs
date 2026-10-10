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
        private readonly GameObject root;
        private readonly RectTransform panel, rows;
        private readonly TMP_Text font, message, pageLabel, folderLabel;
        private readonly TMP_InputField directory, fileName;
        private readonly Button primary, copyPath, format, editPath, cancelPath;
        private RectTransform contentsPanel;
        private Button contentsBack, storageChoice, mountChoice, armorChoice, missingChoice;
        private TMP_Text contentsDetails;
        private Task<CompositeBlueprintStore.ContentsSummary> contentsInspection;
        private Task<CompositeBlueprintStore.ImportReview> importInspection;
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
        private readonly GameObject previousFocus;
        private Task<string[]> listing;
        private string[] entries = Array.Empty<string>();
        private string selected, resultPath, currentDirectory;
        private int page;
        private bool importing, archive, planBuild, completed, disposed;
        internal bool IsOpen => root && root.activeInHierarchy && !disposed;

        internal BlueprintExchangeView(Transform parent, TMP_Text template, CompositeBlueprintStore library,
            CompositeBlueprintStore.Blueprint source, Action closed, bool frontCanvas = false, string report = null)
        {
            store = library; blueprint = source; font = template; finished = closed;
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
            primary = Button("ExchangeSubmit", T(source == null ? "exchange.import" : "exchange.export"), 548, 530, 212, Submit);
            copyPath = Button("ExchangeCopyPath", T("exchange.copy_path"), 256, 530, 276,
                () => GUIUtility.systemCopyBuffer = resultPath ?? selected ?? directory.text);
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
            if (listing != null && listing.IsCompleted)
            {
                try { entries = listing.GetAwaiter().GetResult(); message.text = entries.Length == 0 && blueprint == null ? T("exchange.empty") : ""; }
                catch (Exception error) { entries = Array.Empty<string>(); SetError(error); }
                listing = null; RenderRows();
            }
            if (importing)
            {
                store.CatalogAll();
                if (!store.CatalogLoading)
                {
                    importing = false;
                    if (store.ImportError != null)
                    {
                        contentsBack.interactable = true;
                        RenderContents();
                        message.text = BuildWorksLocalization.ResolveUserText(store.ImportError);
                    }
                    else
                    {
                        completed = true; resultPath = store.ImportResultPath;
                        string name = Path.GetFileName(resultPath);
                        var imported = store.CatalogAll().Where(item => item.importSource == name ||
                            (item.importSource ?? "").StartsWith(name + "/", StringComparison.Ordinal)).ToArray();
                        string[] errors = imported.Where(item => item.importError != null)
                            .Select(item => BuildWorksLocalization.ResolveUserText(item.importError)).Distinct().ToArray();
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
                    if (reviewingContents) { importReview = store.PrepareImportReview(review, name => name.GetStableHashCode()); contentsSummary = importReview.Contents; RefreshMissing(); RenderContents(); }
                }
                catch (Exception error) { if (reviewingContents) SetError(error); }
                importInspection = null;
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
            bool busy = importing || planExport != null;
            primary.interactable = !completed && !busy && listing == null && !directory.gameObject.activeSelf &&
                (blueprint != null || selected != null) && (!reviewingContents ||
                (planBuild ? planReview != null : contentsSummary != null && missingReview != null && (!omitMissingParts || missingReview.CanOmit)));
            copyPath.interactable = !busy;
            if (completed || busy)
                foreach (Selectable control in root.GetComponentsInChildren<Selectable>())
                    control.interactable = control.name == "ExchangeClose" || control.name == "ExchangeCopyPath" && !busy;
            else if (contentsBack) contentsBack.interactable = true;
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
            if (completed || importing || planExport != null || listing != null) return;
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
                if (!store.TryImportReviewed(importReview, clearStorage, clearMounts, name => name.GetStableHashCode(), out string error,
                    omitArmorState, omitMissingParts ? missingReview : null))
                { RefreshMissing(); RenderContents(); message.text = BuildWorksLocalization.ResolveUserText(error); return; }
                importing = true; message.text = T("exchange.loading");
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
                    reviewingContents = false; contentsInspection = null; importInspection = null; planInspection = null;
                    planReview = null; contentsPanel.gameObject.SetActive(false);
                    contentsBack.gameObject.SetActive(false); copyPath.gameObject.SetActive(true);
                    SetFileControls(true); message.text = "";
                    primary.GetComponentInChildren<TMP_Text>().text = T(blueprint == null ? "exchange.import" : "exchange.export");
                });
            }
            contentsPanel.gameObject.SetActive(true); contentsBack.gameObject.SetActive(true);
            armorChoice.gameObject.SetActive(false); missingChoice.gameObject.SetActive(false);
            storageChoice.interactable = mountChoice.interactable = primary.interactable = false;
            primary.GetComponentInChildren<TMP_Text>().text = T(blueprint == null ? "exchange.confirm_import" : "exchange.confirm_export");
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
            if (missingReview == null) return;
            if (missingReview.Known && missingReview.Parts == 0) omitMissingParts = false;
            var effective = omitMissingParts && missingReview.CanOmit ? missingReview.RetainedContents : contentsSummary;
            contentsDetails.text = T("exchange.contents_counts", effective.Storages, effective.StoredItems,
                effective.Mounts, effective.Unknown, effective.AlreadyCleared);
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
            contentsPanel.Find("ExchangeContentsTitle").GetComponent<TMP_Text>().text = blueprint == null
                ? T("exchange.import_review", importReview.Documents.Count) : T("exchange.contents_title");
            storageChoice.GetComponentInChildren<TMP_Text>().text = T(clearStorage ? "exchange.storage_clear" : "exchange.storage_keep");
            mountChoice.GetComponentInChildren<TMP_Text>().text = T(clearMounts ? "exchange.mounts_clear" : "exchange.mounts_keep");
            armorChoice.gameObject.SetActive(effective.ArmorStates > 0);
            armorChoice.interactable = effective.ArmorStates > 0;
            if (!armorChoice.interactable) omitArmorState = false;
            missingChoice.gameObject.SetActive(missingReview.Known && missingReview.Parts > 0);
            missingChoice.interactable = missingReview.CanOmit || omitMissingParts;
            missingChoice.GetComponentInChildren<TMP_Text>().text = T(omitMissingParts ? "exchange.missing_omit" : "exchange.missing_keep");
            var choices = new[] { storageChoice, mountChoice, armorChoice, missingChoice }.Where(button => button.gameObject.activeSelf).ToArray();
            float detailsHeight = 266 - 48 - choices.Length * 34 - 12;
            var viewport = (RectTransform)contentsDetails.transform.parent;
            viewport.sizeDelta = new Vector2(716, detailsHeight);
            contentsDetails.rectTransform.sizeDelta = new Vector2(700, Mathf.Max(detailsHeight, contentsDetails.GetPreferredValues(contentsDetails.text, 700, 0).y));
            for (int i = 0; i < choices.Length; ++i)
                ((RectTransform)choices[i].transform).anchoredPosition = new Vector2(12, -(48 + detailsHeight + 6 + i * 34));
            contentsPanel.sizeDelta = new Vector2(740, 266);
            armorChoice.GetComponentInChildren<TMP_Text>().text = T(omitArmorState ? "exchange.armor_omit" : "exchange.armor_keep");
            skin.Apply(armorChoice, active: omitArmorState);
            skin.Apply(missingChoice, active: omitMissingParts);
            skin.Apply(storageChoice, active: clearStorage); skin.Apply(mountChoice, active: clearMounts);
            message.text = missingReview.EmptyDocuments > 0 ? T("exchange.missing_empty") :
                omitMissingParts ? T("exchange.missing_warning", missingReview.Parts, remainingUnknown, omitArmorState ? effective.ArmorStates : 0) :
                omitArmorState ? T("exchange.armor_warning", effective.ArmorStates, remainingUnknown) :
                T(contentsSummary.Unknown != 0 ? "exchange.contents_unknown" :
                blueprint == null ? "exchange.import_copy" : "exchange.contents_copy");
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
            var viewport = Rect("ExchangeResult", panel, 20, 174, 740, 342);
            viewport.gameObject.AddComponent<Image>().color = new Color(.05f, .055f, .055f, 1);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
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
        private TMP_InputField Input(string name, float x, float y, float width, int limit, float height = 30)
        {
            var rect = Rect(name, panel, x, y, width, height); rect.gameObject.AddComponent<Image>();
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
            planReview?.CancelWrite();
            root.SetActive(false); UnityEngine.Object.Destroy(root); skin.Dispose();
            if (EventSystem.current && previousFocus && previousFocus.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousFocus);
            finished?.Invoke();
        }
    }
}
