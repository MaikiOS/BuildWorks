using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintExchangeView
    {
        private bool batchMode;
        private Button batchToggle, batchDocumentsButton;
        private readonly HashSet<string> batchFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> batchIncluded = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> batchExportIncluded = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> batchCategories = new Dictionary<string, string>(StringComparer.Ordinal);
        private CompositeBlueprintStore.Blueprint[] batchAllDocuments, batchExportChoices, batchImported;
        private CompositeBlueprintStore.ImportReview batchRawReview;
        private CancellationTokenSource batchCancellation;
        private RectTransform batchPicker, batchRows;
        private TMP_Text batchPageLabel;
        private Button batchPrevious, batchNext;
        private int batchPage;
        private static string BatchKey(int index) => index.ToString(CultureInfo.InvariantCulture);

        private Dictionary<int, string> BatchCategoryOverrides()
        {
            var result = new Dictionary<int, string>();
            if (batchAllDocuments == null || importReview == null) return result;
            int current = 0;
            for (int i = 0; i < batchAllDocuments.Length; ++i)
                if (batchIncluded.Contains(BatchKey(i)))
                {
                    if (batchCategories.TryGetValue(BatchKey(i), out string value)) result.Add(current, value);
                    ++current;
                }
            return result;
        }

        private void CreateBatchToggle()
        {
            if (blueprint != null) panel.Find("ExchangeNameLabel").gameObject.SetActive(false);
            batchToggle = Button("ExchangeBatchToggle", T("exchange.batch_choose"), 226, 404, 134, () =>
            {
                batchMode = !batchMode; page = 0; selected = null;
                batchFiles.Clear(); batchIncluded.Clear(); batchExportIncluded.Clear(); batchCategories.Clear(); batchAllDocuments = null; batchRawReview = null;
                batchCancellation?.Dispose(); batchCancellation = batchMode ? new CancellationTokenSource() : null;
                if (batchMode && blueprint != null)
                {
                    batchExportChoices = store.CatalogAll().ToArray();
                    batchExportIncluded.Add(blueprint.id); planBuild = false; archive = true;
                }
                batchToggle.GetComponentInChildren<TMP_Text>().text = T(batchMode ? "exchange.batch_single" : "exchange.batch_choose");
                if (format) { format.interactable = !batchMode; format.GetComponentInChildren<TMP_Text>().text = T(archive ? "exchange.format_zip" : "exchange.format_json"); }
                RenderRows(); Tick();
            });
        }

        private void ToggleBatchFile(string path)
        {
            if (!batchFiles.Remove(path))
            {
                if (batchFiles.Count >= CompositeBlueprintStore.MaximumBatchFiles) { message.text = T("exchange.batch_limit"); return; }
                batchFiles.Add(path);
            }
        }

        private void RenderBatchExportRows()
        {
            for (int i = rows.childCount - 1; i >= 0; --i)
            { rows.GetChild(i).gameObject.SetActive(false); UnityEngine.Object.Destroy(rows.GetChild(i).gameObject); }
            pageLabel.text = (page + 1) + " / " + Math.Max(1, (batchExportChoices.Length + 6) / 7);
            for (int i = page * 7; i < Math.Min(batchExportChoices.Length, (page + 1) * 7); ++i)
            {
                var document = batchExportChoices[i];
                var button = Button("ExchangeBatchExport" + i, (batchExportIncluded.Contains(document.id) ? "[x] " : "[ ] ") + document.name,
                    0, (i % 7) * 32, 740, () =>
                    {
                        if (!batchExportIncluded.Remove(document.id))
                        {
                            if (batchExportIncluded.Count >= CompositeBlueprintStore.MaximumBatchFiles) { message.text = T("exchange.batch_limit"); return; }
                            batchExportIncluded.Add(document.id);
                        }
                        RenderRows(); Tick();
                    }, rows);
                skin.Apply(button, active: batchExportIncluded.Contains(document.id));
                button.interactable = document.importError == null && !document.importAbsoluteScale;
            }
        }

        private void TickBatch()
        {
            if (!batchMode) return;
            if (importInspection?.Status == TaskStatus.RanToCompletion && batchRawReview == null)
            {
                batchRawReview = importInspection.Result;
                batchAllDocuments = batchRawReview.Documents.ToArray();
                for (int i = 0; i < batchAllDocuments.Length; ++i) batchIncluded.Add(BatchKey(i));
            }
            bool busy = importing || planExport != null || importInspection != null || importPreparation != null;
            if (!reviewingContents && !completed && !busy)
            {
                int count = blueprint == null ? batchFiles.Count : batchExportIncluded.Count;
                message.text = T("exchange.batch_selected", count);
            }
            if (batchDocumentsButton) batchDocumentsButton.interactable = !busy && !completed && contentsSummary != null;
            if (batchPicker && batchPicker.gameObject.activeSelf)
                primary.interactable = false;
            if (completed && batchImported != null)
            {
                primary.GetComponentInChildren<TMP_Text>().text = T("exchange.batch_catalog");
                primary.interactable = true;
            }
        }

        private bool SubmitBatch()
        {
            if (!batchMode) return false;
            if (batchPicker && batchPicker.gameObject.activeSelf || importing || planExport != null ||
                listing != null || importInspection != null || importPreparation != null) return true;
            if (completed && batchImported != null) { Dispose(); return true; }
            if (!reviewingContents && !completed)
            {
                if (blueprint == null)
                {
                    if (batchFiles.Count == 0) return true;
                    batchRawReview = null; batchAllDocuments = null; batchCategories.Clear(); batchIncluded.Clear();
                    // Existing review/preparation handles host snapshots and missing/content policies.
                    selected = batchFiles.First(); ReviewContents(); return true;
                }
                if (batchExportIncluded.Count == 0) return true;
                batchAllDocuments = batchExportChoices.Where(document => batchExportIncluded.Contains(document.id)).Select(document => document.ImportCopy()).ToArray();
                batchIncluded.Clear(); batchCategories.Clear();
                for (int i = 0; i < batchAllDocuments.Length; ++i) batchIncluded.Add(BatchKey(i));
                batchRawReview = new CompositeBlueprintStore.ImportReview { Documents = batchAllDocuments.ToList() };
                ReviewContents();
                return true;
            }
            if (blueprint == null) return false;
            if (completed || importing || importPreparation != null || contentsSummary == null || batchIncluded.Count == 0) return true;
            if (importReview?.IsHostCurrent?.Invoke() != true)
            { message.text = T("placement.contents_stale"); return true; }
            try
            {
                var categories = BatchCategoryOverrides();
                var documents = importReview.Documents.Select((document, index) =>
                {
                    var copy = document.ImportCopy();
                    if (categories.TryGetValue(index, out string category) && category != null) copy.category = category;
                    return copy;
                }).ToArray();
                planExport = store.ExportBatch(documents, directory.text, fileName.text, clearStorage, clearMounts,
                    name => name.GetStableHashCode(), omitArmorState, batchCancellation.Token);
                message.text = T("exchange.loading");
            }
            catch (Exception error) { SetError(error); }
            return true;
        }

        private string BatchResultSummary(CompositeBlueprintStore.Blueprint[] imported)
        {
            if (!batchMode) return "";
            batchImported = imported;
            return "\n" + string.Join("\n", imported.Select(document => T("exchange.batch_done", document.name,
                BuildWorksLocalization.BlueprintCategoryLabel(document.category)))) + BatchErrors();
        }

        private string BatchErrors() => batchRawReview?.FileErrors == null ? "" : "\n" + string.Join("\n",
            batchRawReview.FileErrors.Select(entry => T("exchange.batch_skipped", Path.GetFileName(entry.Source), BuildWorksLocalization.ResolveUserText(entry.Error))));

        private void UpdateBatchControls(bool busy)
        {
            if (!batchMode) return;
            if (!reviewingContents && !completed)
                primary.interactable = !busy && listing == null && !directory.gameObject.activeSelf &&
                    (blueprint == null ? batchFiles.Count > 0 : batchExportIncluded.Count > 0);
            else if (reviewingContents && !completed)
            {
                primary.interactable &= batchIncluded.Count > 0 && (!batchPicker || !batchPicker.gameObject.activeSelf);
                if (batchIncluded.Count == 0 && !busy) message.text = T("exchange.batch_empty");
            }
            if (completed && batchImported != null)
            {
                primary.GetComponentInChildren<TMP_Text>().text = T("exchange.batch_catalog");
                primary.interactable = true;
            }
        }

        private void AppendBatchReview()
        {
            if (!batchMode || batchAllDocuments == null) return;
            var categories = BatchCategoryOverrides();
            contentsDetails.text = BatchErrors().TrimStart('\n') + "\n" + contentsDetails.text + "\n" + T("exchange.batch_atomic") +
                (blueprint == null ? "\n" + T("exchange.batch_destination", importReview.Documents.FirstOrDefault()?.name ?? "—") : "") +
                "\n" + string.Join("\n", importReview.Documents.Select((document, index) => T("exchange.batch_document", document.name, document.parts.Count,
                    categories.TryGetValue(index, out string specific) ? specific ?? document.category : categoryOverride ? categoryInput.text : document.category)));
            if (!batchDocumentsButton)
            {
                batchDocumentsButton = Button("ExchangeBatchDocuments", T("exchange.batch_documents"), 516, 8, 212, OpenBatchDocuments, contentsPanel);
                contentsPanel.Find("ExchangeContentsTitle").GetComponent<RectTransform>().sizeDelta = new Vector2(498, 30);
            }
            batchDocumentsButton.gameObject.SetActive(true);
        }

        private void OpenBatchDocuments()
        {
            if (batchAllDocuments == null) return;
            if (!batchPicker)
            {
                batchPicker = Rect("ExchangeBatchPicker", panel, 20, 102, 740, 332);
                batchPicker.gameObject.AddComponent<Image>().color = new Color(.05f, .055f, .055f, 1);
                Label("ExchangeBatchPickerTitle", T("exchange.batch_documents"), 12, 8, 500, 30, 18, batchPicker);
                Button("ExchangeBatchPickerClose", T("exchange.batch_apply"), 552, 8, 176, () =>
                {
                    batchPicker.gameObject.SetActive(false); contentsPanel.gameObject.SetActive(true);
                    var selectedDocuments = batchAllDocuments.Where((document, index) => batchIncluded.Contains(BatchKey(index))).ToList();
                    var review = new CompositeBlueprintStore.ImportReview
                    {
                        Source = batchRawReview.Source,
                        Bytes = batchRawReview.Bytes,
                        Sources = batchRawReview.Sources,
                        FileErrors = batchRawReview.FileErrors,
                        Documents = selectedDocuments
                    };
                    contentsSummary = null; missingReview = null; importReview = null;
                    importPreparation = store.PrepareImportReview(review, name => name.GetStableHashCode()); Tick();
                }, batchPicker);
                Label("ExchangeBatchPickerHelp", T("exchange.batch_categories_help"), 12, 44, 716, 40, 14, batchPicker);
                batchRows = Rect("ExchangeBatchRows", batchPicker, 12, 88, 716, 188);
                batchPrevious = Button("ExchangeBatchPrevious", "←", 12, 288, 80, () => { --batchPage; RenderBatchDocuments(); }, batchPicker);
                batchNext = Button("ExchangeBatchNext", "→", 648, 288, 80, () => { ++batchPage; RenderBatchDocuments(); }, batchPicker);
                batchPageLabel = Label("ExchangeBatchPage", "", 104, 288, 532, 30, 14, batchPicker);
            }
            batchPage = 0; batchPicker.gameObject.SetActive(true); batchPicker.SetAsLastSibling();
            contentsPanel.gameObject.SetActive(false); primary.interactable = false; RenderBatchDocuments();
        }

        private void RenderBatchDocuments()
        {
            for (int i = batchRows.childCount - 1; i >= 0; --i)
            { batchRows.GetChild(i).gameObject.SetActive(false); UnityEngine.Object.Destroy(batchRows.GetChild(i).gameObject); }
            int pages = Math.Max(1, (batchAllDocuments.Length + 3) / 4);
            batchPage = Mathf.Clamp(batchPage, 0, pages - 1);
            for (int i = batchPage * 4; i < Math.Min(batchAllDocuments.Length, (batchPage + 1) * 4); ++i)
            {
                var document = batchAllDocuments[i]; string key = BatchKey(i); float y = (i % 4) * 46;
                Button("ExchangeBatchInclude" + i, batchIncluded.Contains(key) ? "[x]" : "[ ]", 0, y, 38, () =>
                { if (!batchIncluded.Remove(key)) batchIncluded.Add(key); RenderBatchDocuments(); }, batchRows);
                Label("ExchangeBatchName" + i, document.name + " · " + document.parts.Count + " · " +
                    Path.GetFileName(document.sourceDocuments?.FirstOrDefault()?.name ?? document.importSource), 44, y, 348, 42, 14, batchRows);
                var input = Input("ExchangeBatchCategory" + i, 402, y, 252, 24, parent: batchRows);
                input.SetTextWithoutNotify(batchCategories.TryGetValue(key, out string value) ? value ?? document.category : "");
                input.placeholder = Label("ExchangeBatchCategoryHint" + i, T("exchange.batch_inherit"), 0, 0, 236, 26, 14, input.textViewport);
                input.onEndEdit.AddListener(text => { if (string.IsNullOrWhiteSpace(text)) batchCategories.Remove(key); else batchCategories[key] = text; });
                Button("ExchangeBatchOriginal" + i, T("exchange.batch_original"), 662, y, 54, () =>
                { batchCategories[key] = null; RenderBatchDocuments(); }, batchRows);
            }
            batchPrevious.interactable = batchPage > 0; batchNext.interactable = batchPage + 1 < pages;
            batchPageLabel.text = T("exchange.batch_page", batchPage + 1, pages, batchAllDocuments.Length);
        }
    }
}
