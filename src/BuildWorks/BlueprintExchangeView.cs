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
        private readonly TMP_Text font, message, pageLabel;
        private readonly TMP_InputField directory, fileName;
        private readonly Button primary, copyPath, format;
        private readonly BlueprintEditorSkin skin = new BlueprintEditorSkin();
        private readonly CompositeBlueprintStore store;
        private readonly CompositeBlueprintStore.Blueprint blueprint;
        private readonly Action finished;
        private readonly GameObject previousFocus;
        private Task<string[]> listing;
        private string[] entries = Array.Empty<string>();
        private string selected, resultPath;
        private int page;
        private bool importing, archive, completed, disposed;
        internal bool IsOpen => root && root.activeInHierarchy && !disposed;

        internal BlueprintExchangeView(Transform parent, TMP_Text template, CompositeBlueprintStore library,
            CompositeBlueprintStore.Blueprint source, Action closed, bool frontCanvas = false)
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
            Label("ExchangeTitle", T(source == null ? "exchange.import" : "exchange.export"), 20, 18, 740, 30, 22);
            Label("ExchangeHelp", T(source == null ? "exchange.browse_help" : "exchange.export_help"), 20, 56, 740, 26, 14);
            directory = Input("ExchangeDirectory", 20, 86, 562, 1024);
            directory.onSubmit.AddListener(_ => Browse(directory.text));
            Button("ExchangeUp", T("exchange.up"), 590, 86, 80, () =>
            {
                try { Browse(Directory.GetParent(directory.text)?.FullName ?? directory.text); }
                catch (Exception error) { SetError(error); }
            });
            Button("ExchangeGo", T("exchange.go"), 678, 86, 82, () => Browse(directory.text));
            Location("ExchangeHome", T("exchange.library"), store.ExchangeDirectory, 20);
            Location("ExchangeDownloads", T("exchange.downloads"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"), 208);
            Location("ExchangeDocuments", T("exchange.documents"), Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 396);
            Location("ExchangeDesktop", T("exchange.desktop"), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), 584);
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
                { archive = !archive; format.GetComponentInChildren<TMP_Text>().text = T(archive ? "exchange.format_zip" : "exchange.format_json"); });
            }
            message = Label("ExchangeMessage", "", 20, 446, 740, 70, 14);
            message.overflowMode = TextOverflowModes.Ellipsis;
            primary = Button("ExchangeSubmit", T(source == null ? "exchange.import" : "exchange.export"), 548, 530, 212, Submit);
            copyPath = Button("ExchangeCopyPath", T("exchange.copy_path"), 256, 530, 276,
                () => GUIUtility.systemCopyBuffer = resultPath ?? selected ?? directory.text);
            Button("ExchangeClose", T("editor.view.close"), 20, 530, 220, Dispose);
            string initial = Directory.Exists(store.ExchangeDirectory) ? store.ExchangeDirectory : Directory.GetParent(store.ExchangeDirectory).FullName;
            if (source != null)
                try { initial = CompositeBlueprintStore.ValidateExchangeDirectory(store.ExportDirectory); Directory.CreateDirectory(initial); }
                catch (Exception error) { SetError(error); }
            Browse(initial);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(directory.gameObject);
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
                        message.text = BuildWorksLocalization.ResolveUserText(store.ImportError);
                        foreach (Selectable control in panel.GetComponentsInChildren<Selectable>()) control.interactable = true;
                        RenderRows();
                    }
                    else
                    {
                        completed = true; resultPath = Path.Combine(store.ImportDirectory, Path.GetFileName(selected));
                        string name = Path.GetFileName(selected);
                        var imported = store.CatalogAll().Where(item => item.importSource == name ||
                            (item.importSource ?? "").StartsWith(name + "/", StringComparison.Ordinal)).ToArray();
                        string[] errors = imported.Where(item => item.importError != null)
                            .Select(item => BuildWorksLocalization.ResolveUserText(item.importError)).Distinct().ToArray();
                        ShowResult(T("exchange.imported", resultPath) + (errors.Length == 0 ? "" : "\n" + string.Join("\n", errors)));
                    }
                }
            }
            primary.interactable = !completed && !importing && listing == null && (blueprint != null || selected != null);
            copyPath.interactable = !importing;
            if (completed || importing)
                foreach (Selectable control in root.GetComponentsInChildren<Selectable>())
                    control.interactable = control.name == "ExchangeClose" || control.name == "ExchangeCopyPath" && !importing;
        }

        private void Browse(string path)
        {
            if (listing != null || importing || completed) return;
            try
            {
                string full = CompositeBlueprintStore.ValidateExchangeDirectory(path);
                directory.SetTextWithoutNotify(full); selected = null; page = 0;
                entries = Array.Empty<string>(); RenderRows();
                message.text = T("exchange.loading");
                listing = Task.Run(() => CompositeBlueprintStore.BrowseExchangeDirectory(full));
            }
            catch (Exception error) { SetError(error); }
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
            if (completed || importing || listing != null) return;
            if (blueprint == null)
            {
                if (selected == null) return;
                if (!store.TryImportFile(selected, out string error)) { message.text = BuildWorksLocalization.ResolveUserText(error); return; }
                importing = true; message.text = T("exchange.loading");
            }
            else
            {
                if (!store.TryExportFile(blueprint, directory.text, fileName.text, archive, out resultPath, out string error))
                { message.text = BuildWorksLocalization.ResolveUserText(error); return; }
                completed = true; ShowResult(T("exchange.exported", resultPath));
            }
            Tick();
        }

        private void SetError(Exception error) => message.text = BuildWorksLocalization.ResolveUserText(error.Message);
        private void ShowResult(string value)
        {
            foreach (Transform child in panel)
                if (child.name == "ExchangeEntries" || child.name == "ExchangePrevious" || child.name == "ExchangeNext" ||
                    child.name == "ExchangePage" || child.name == "ExchangeNameLabel" || child.name == "ExchangeFileName" || child.name == "ExchangeFormat")
                    child.gameObject.SetActive(false);
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
            label.richText = false; label.raycastTarget = false; label.alignment = TextAlignmentOptions.MidlineLeft;
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
        private TMP_InputField Input(string name, float x, float y, float width, int limit)
        {
            var rect = Rect(name, panel, x, y, width, 30); rect.gameObject.AddComponent<Image>();
            var input = rect.gameObject.AddComponent<TMP_InputField>(); skin.Apply(input, input: true);
            var viewport = Rect(name + "Viewport", rect, 8, 2, width - 16, 26); viewport.gameObject.AddComponent<RectMask2D>();
            var text = Label(name + "Text", "", 0, 0, width - 16, 26, 14, viewport);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            input.textViewport = viewport; input.textComponent = text; input.characterLimit = limit;
            input.lineType = TMP_InputField.LineType.SingleLine; return input;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            root.SetActive(false); UnityEngine.Object.Destroy(root); skin.Dispose();
            if (EventSystem.current && previousFocus && previousFocus.activeInHierarchy)
                EventSystem.current.SetSelectedGameObject(previousFocus);
            finished?.Invoke();
        }
    }
}
