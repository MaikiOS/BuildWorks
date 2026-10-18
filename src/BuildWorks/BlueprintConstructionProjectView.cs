#if BUILDWORKS_TEST_STAND
using Input = OstrixMods.BuildWorks.BuildWorksQaInput;
#endif
using System;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    internal sealed class BlueprintConstructionProjectView
    {
        private GameObject root;
        private TMP_Text summary;
        private CursorLockMode oldLock;
        private bool oldVisible, confirming;
        private Guid project, cancelOperation;
        private System.Collections.Generic.IReadOnlyList<HammerBlueprintPieceRegistry.ResourceInfo> costs;
        private bool missing;
        private bool cursorOwned;
        private string operationError;
        private float nextRefresh;
        private int closingFrame = -1;
        internal bool IsOpen => root || closingFrame == Time.frameCount;

        internal void Tick(BlueprintConstructionProjectRuntime runtime, BlueprintConstructionProjectBoard board)
        {
            if (!board) { Close(); return; }
            if (Input.GetKeyDown(KeyCode.Escape)) { runtime.Close(); return; }
            if (!root || project != board.Id)
            {
                Close(); project = board.Id;
                TMP_Text template = Hud.instance ? Hud.instance.m_pieceDescription : null;
                if (!template || !template.font) throw new InvalidOperationException("Native project UI font is missing.");
                var blueprint = runtime.Read(board.Id).Blueprint;
                var pieces = blueprint.parts.Select(p => ZNetScene.instance.GetPrefab(p.prefabName)?.GetComponent<Piece>()).ToArray();
                missing = pieces.Any(p => !p); costs = HammerBlueprintPieceRegistry.ResourceCosts(pieces);
                oldLock = Cursor.lockState; oldVisible = Cursor.visible;
                root = new GameObject("BuildWorks_ConstructionProject", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 4900;
                var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = 1;
                var panel = Rect("Panel", root.transform, new Vector2(580, 420), Vector2.zero);
                panel.gameObject.AddComponent<Image>().color = new Color(.055f, .06f, .06f, .98f);
                Label(panel, template, board.BlueprintName, 24, new Vector2(300, 48), new Vector2(-110, 166));
                var viewport = Rect("ResourceScroll", panel, new Vector2(520, 260), new Vector2(0, 4));
                viewport.gameObject.AddComponent<Image>().color = Color.clear;
                viewport.gameObject.AddComponent<RectMask2D>();
                var scroll = viewport.gameObject.AddComponent<ScrollRect>();
                summary = Label(viewport, template, string.Empty, 18, new Vector2(510, Mathf.Max(260, costs.Count * 26 + 170)), Vector2.zero);
                summary.rectTransform.anchorMin = summary.rectTransform.anchorMax = new Vector2(.5f, 1);
                summary.rectTransform.pivot = new Vector2(.5f, 1); summary.rectTransform.anchoredPosition = Vector2.zero;
                scroll.viewport = viewport; scroll.content = summary.rectTransform; scroll.horizontal = false;
                scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 40;
                Button(panel, template, "project.close", new Vector2(184, 36), new Vector2(174, 166), runtime.Close);
                Button(panel, template, "project.cancel", new Vector2(250, 44), new Vector2(0, -170), () =>
                {
                    if (!confirming) { confirming = true; cancelOperation = Guid.NewGuid(); nextRefresh = 0; return; }
                    operationError = null;
                    try { runtime.CancelUnpaid(board.Id, board.Revision, cancelOperation); runtime.Close(); }
                    catch (Exception error) { operationError = BuildWorksLocalization.Text("project.error", error.Message); nextRefresh = 0; }
                });
                cursorOwned = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .5f;
            var text = new StringBuilder(BuildWorksLocalization.Text("project.unpaid", board.PartCount));
            if (confirming) text.AppendLine().AppendLine(BuildWorksLocalization.Text("project.cancel_confirm"));
            try
            {
                if (missing) text.AppendLine().AppendLine(BuildWorksLocalization.Text("project.missing"));
                else foreach (var cost in costs)
                {
                    int amount = checked((BlueprintResourceHudView.Free(GlobalKeys.NoBuildCost) ? 0 : cost.BuildAmount) +
                        (BlueprintResourceHudView.Free(GlobalKeys.NoCraftCost) ? 0 : cost.CraftAmount));
                    int have = Player.m_localPlayer.GetInventory().CountItems(cost.Item.m_itemData.m_shared.m_name, -1, true);
                    text.AppendLine().Append(cost.Name).Append(": ").Append(have).Append(" / ").Append(amount);
                }
                text.AppendLine().AppendLine(BuildWorksLocalization.Text("project.payment_pending"));
                if (operationError != null) text.AppendLine().AppendLine(operationError);
                summary.text = text.ToString();
            }
            catch (Exception error) { summary.text = BuildWorksLocalization.Text("project.error", error.Message); }
        }

        internal void Close()
        {
            if (!root && !cursorOwned) return;
            if (root) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
            root = null; summary = null;
            if (cursorOwned) { Cursor.lockState = oldLock; Cursor.visible = oldVisible; cursorOwned = false; }
            confirming = false; operationError = null; nextRefresh = 0; closingFrame = Time.frameCount;
        }
        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static TMP_Text Label(Transform parent, TMP_Text template, string text, int size, Vector2 bounds, Vector2 position)
        {
            var rect = Rect("Label", parent, bounds, position); rect.gameObject.SetActive(false);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = template.font; label.text = text; label.fontSize = size; label.color = new Color(.94f, .91f, .84f);
            label.alignment = TextAlignmentOptions.TopLeft; label.raycastTarget = false; rect.gameObject.SetActive(true); return label;
        }
        private static void Button(Transform parent, TMP_Text template, string key, Vector2 size, Vector2 position, Action action)
        {
            var rect = Rect(key, parent, size, position); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.28f, .23f, .16f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(() => action());
            var label = Label(rect, template, BuildWorksLocalization.Text(key), 17, size - new Vector2(12, 6), Vector2.zero);
            label.alignment = TextAlignmentOptions.Center;
        }
    }
}
