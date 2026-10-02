using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    // Display-only adapter. Never assigns costs to the blueprint marker Piece.
    internal sealed class BlueprintResourceHudView : IDisposable
    {
        private Hud host;
        private RectTransform panel;
        private readonly List<RectState> states = new List<RectState>();
        private readonly List<GameObject> extras = new List<GameObject>();
        private Vector2 cellSize;
        private float spacing;
        private RectTransform viewport, content;
        private ScrollRect scroll;
        private GameObject scrollTrack;

        internal bool CapturesWheel => scroll && scroll.enabled && viewport.gameObject.activeInHierarchy &&
            Cursor.lockState == CursorLockMode.None &&
            RectTransformUtility.RectangleContainsScreenPoint(viewport, Input.mousePosition,
                viewport.GetComponentInParent<Canvas>().worldCamera);

        internal void Show(Hud hud, Player player, IReadOnlyList<Piece.Requirement> resources,
            IReadOnlyList<int> paidAmounts, IReadOnlyList<CraftingStation> stations)
        {
            if (!hud || !player || hud.m_requirementItems == null ||
                hud.m_requirementItems.Length == 0) { Restore(); return; }
            if (host != hud || !panel)
            {
                Restore();
                RectTransform first = hud.m_requirementItems[0].transform as RectTransform;
                if (!first || !hud.m_pieceDescription || !hud.m_buildSelection || !hud.m_buildIcon) return;
                Transform common = first.parent;
                while (common && (!hud.m_pieceDescription.transform.IsChildOf(common) ||
                    !hud.m_buildSelection.transform.IsChildOf(common) || !hud.m_buildIcon.transform.IsChildOf(common)))
                    common = common.parent;
                panel = common as RectTransform;
                // Do not resize an entire HUD canvas when an unfamiliar prefab has no common info panel.
                if (!panel || panel.GetComponent<Canvas>() || panel == hud.m_rootObject.transform)
                { panel = null; return; }
                host = hud;
                states.Add(new RectState(panel));
                states.Add(new RectState(hud.m_pieceDescription.rectTransform));
                states.Add(new RectState(hud.m_buildSelection.rectTransform));
                states.Add(new RectState(hud.m_buildIcon.rectTransform));
                foreach (GameObject cell in hud.m_requirementItems)
                    states.Add(new RectState((RectTransform)cell.transform));
                cellSize = first.rect.size;
                Vector2 firstCell = panel.InverseTransformPoint(first.position);
                spacing = cellSize.x + 4f;
                if (hud.m_requirementItems.Length > 1)
                {
                    float nativeSpacing = Mathf.Abs(panel.InverseTransformPoint(
                        hud.m_requirementItems[1].transform.position).x - firstCell.x);
                    if (nativeSpacing > 1f) spacing = nativeSpacing;
                }
                viewport = new GameObject("BuildWorks_RequirementViewport", typeof(RectTransform),
                    typeof(Image), typeof(RectMask2D), typeof(ScrollRect)).GetComponent<RectTransform>();
                viewport.SetParent(panel, false);
                viewport.gameObject.layer = panel.gameObject.layer;
                viewport.GetComponent<Image>().color = Color.clear;
                content = new GameObject("Cards", typeof(RectTransform)).GetComponent<RectTransform>();
                content.SetParent(viewport, false);
                content.gameObject.layer = panel.gameObject.layer;
                content.anchorMin = content.anchorMax = Vector2.up; content.pivot = Vector2.up;
                scroll = viewport.GetComponent<ScrollRect>();
                scroll.viewport = viewport; scroll.content = content;
                scroll.horizontal = false; scroll.scrollSensitivity = 80f;
                scroll.movementType = ScrollRect.MovementType.Clamped;
                RectTransform track = new GameObject("ScrollTrack", typeof(RectTransform), typeof(Image), typeof(Scrollbar))
                    .GetComponent<RectTransform>();
                track.SetParent(viewport, false); track.gameObject.layer = panel.gameObject.layer;
                track.anchorMin = Vector2.right; track.anchorMax = Vector2.one;
                track.offsetMin = new Vector2(-10f, 0f); track.offsetMax = Vector2.zero;
                track.GetComponent<Image>().color = new Color(0f, 0f, 0f, .4f);
                RectTransform thumb = new GameObject("Thumb", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                thumb.SetParent(track, false); thumb.gameObject.layer = panel.gameObject.layer;
                thumb.GetComponent<Image>().color = new Color(.75f, .61f, .36f, .8f);
                Scrollbar bar = track.GetComponent<Scrollbar>();
                bar.handleRect = thumb; bar.targetGraphic = thumb.GetComponent<Image>();
                bar.direction = Scrollbar.Direction.BottomToTop;
                scroll.verticalScrollbar = bar;
                scrollTrack = track.gameObject;
            }
            int count = resources.Count + stations.Count;
            for (int index = 0; index < Mathf.Max(count, host.m_requirementItems.Length); ++index)
            {
                GameObject cell = Cell(index);
                cell.SetActive(index < count);
                if (index >= count) continue;
                TMP_Text amount = cell.transform.Find("res_amount").GetComponent<TMP_Text>();
                if (index < resources.Count)
                {
                    Piece.Requirement requirement = resources[index];
                    InventoryGui.SetupRequirement(cell.transform, requirement, player, false, 0, 1);
                    bool enough = player.GetInventory().CountItems(
                        requirement.m_resItem.m_itemData.m_shared.m_name, -1, true) >= paidAmounts[index];
                    amount.color = enough ? Color.white : MissingColor();
                }
                else
                {
                    CraftingStation station = stations[index - resources.Count];
                    bool available = CraftingStation.HaveBuildStationInRange(station.m_name, player.transform.position);
                    Image icon = cell.transform.Find("res_icon").GetComponent<Image>();
                    icon.sprite = station.m_icon;
                    icon.color = available ? Color.white : Color.gray;
                    TMP_Text name = cell.transform.Find("res_name").GetComponent<TMP_Text>();
                    name.text = Localization.instance.Localize(station.m_name);
                    cell.GetComponent<UITooltip>().m_text = station.m_name;
                    amount.text = available ? "" : Localization.instance.Localize("$menu_none");
                    amount.color = available || Free(GlobalKeys.NoCraftCost) ? Color.white : MissingColor();
                }
            }
            Layout(count);
        }

        internal static bool Free(GlobalKeys key) => ZoneSystem.instance && ZoneSystem.instance.GetGlobalKey(key);
        private static Color MissingColor() => Mathf.Sin(Time.time * 10f) > 0f ? Color.red : Color.white;

        private GameObject Cell(int index)
        {
            if (index < host.m_requirementItems.Length) return host.m_requirementItems[index];
            int extra = index - host.m_requirementItems.Length;
            while (extras.Count <= extra)
            {
                GameObject clone = UnityEngine.Object.Instantiate(host.m_requirementItems[0],
                    content);
                clone.name = "BuildWorks_BlueprintRequirement";
                extras.Add(clone);
            }
            return extras[extra];
        }

        private void Layout(int count)
        {
            states[0].Restore();
            Rect original = states[0].Size;
            Canvas canvas = panel.GetComponentInParent<Canvas>();
            RectTransform canvasRect = canvas ? canvas.transform as RectTransform : null;
            Vector3 bottom = panel.TransformPoint(new Vector3(0f, panel.rect.yMin));
            float panelHeightLimit = canvasRect ? Mathf.Max(original.height,
                (canvasRect.rect.yMax - canvasRect.InverseTransformPoint(bottom).y - 16f) /
                Mathf.Max(.01f, panel.lossyScale.y / canvasRect.lossyScale.y)) : original.height;
            float maximumWidth = canvasRect ? canvasRect.rect.width * .85f *
                canvasRect.lossyScale.x / Mathf.Max(.01f, panel.lossyScale.x) : original.width;
            int columns = Mathf.Max(1, Mathf.Min(Mathf.Max(1, count),
                Mathf.FloorToInt((maximumWidth - 32f) / spacing)));
            float width = Mathf.Max(original.width, columns * spacing + 32f);
            float rowHeight = cellSize.y + 8f;
            int rows = Mathf.Max(1, Mathf.CeilToInt((float)count / columns));
            for (int index = 1; index < 4; ++index) states[index].Restore();
            // The native description may wrap over several lines. Reserve its measured
            // height instead of reusing the ordinary-piece card position.
            float textWidth = Mathf.Max(1f, width - states[3].Size.width - 44f);
            TMP_Text title = host.m_buildSelection;
            TMP_Text description = host.m_pieceDescription;
            title.textWrappingMode = description.textWrappingMode = TextWrappingModes.Normal;
            float titleHeight = Mathf.Max(states[2].Size.height,
                title.GetPreferredValues(title.text, textWidth, Mathf.Infinity).y);
            float descriptionHeight = Mathf.Max(states[1].Size.height,
                description.GetPreferredValues(description.text, textWidth, Mathf.Infinity).y);
            float headerHeight = Mathf.Max(states[3].Size.height, titleHeight + 8f + descriptionHeight);
            float cardTop = 16f + headerHeight + 16f;
            float contentHeight = rows * rowHeight - 8f;
            float extraHeight = Mathf.Clamp(cardTop + contentHeight + 16f - original.height,
                0f, Mathf.Max(0f, panelHeightLimit - original.height));
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, original.height + extraHeight);
            panel.anchoredPosition += Vector2.up * (extraHeight * panel.pivot.y);
            Place(host.m_buildIcon.rectTransform, panel, new Vector2(16f, -16f), states[3].Size.size);
            float textLeft = 28f + states[3].Size.width;
            Place(title.rectTransform, panel, new Vector2(textLeft, -16f), new Vector2(textWidth, titleHeight));
            Place(description.rectTransform, panel, new Vector2(textLeft, -24f - titleHeight),
                new Vector2(textWidth, descriptionHeight));
            Place(viewport, panel, new Vector2(16f, -cardTop), new Vector2(columns * spacing + 12f,
                Mathf.Min(contentHeight, Mathf.Max(1f, panel.rect.height - cardTop - 16f))));
            content.sizeDelta = new Vector2(columns * spacing, contentHeight);
            scroll.enabled = content.rect.height > viewport.rect.height + .1f;
            scrollTrack.SetActive(scroll.enabled);
            if (!scroll.enabled) content.anchoredPosition = Vector2.zero;
            for (int index = 0; index < Mathf.Max(count, host.m_requirementItems.Length); ++index)
            {
                RectTransform cell = (RectTransform)Cell(index).transform;
                cell.SetParent(content, false);
                cell.anchorMin = cell.anchorMax = Vector2.up;
                cell.pivot = new Vector2(.5f, .5f);
                cell.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, cellSize.x);
                cell.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, cellSize.y);
                cell.anchoredPosition = new Vector2((index % columns) * spacing + cellSize.x * .5f,
                    -(index / columns) * rowHeight - cellSize.y * .5f);
            }
            for (int index = 0; index < extras.Count; ++index)
                if (index + host.m_requirementItems.Length >= count) extras[index].SetActive(false);
        }

        private static void Place(RectTransform rect, RectTransform parent, Vector2 position, Vector2 size)
        {
            if (rect.parent != parent) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
            rect.sizeDelta = size; rect.anchoredPosition = position;
        }

        internal void Restore()
        {
            // Native UpdateBuild has already refreshed ordinary-cell visibility; restore only geometry.
            foreach (RectState state in states) state.Restore();
            states.Clear();
            foreach (GameObject extra in extras)
                if (extra) { extra.SetActive(false); UnityEngine.Object.Destroy(extra); }
            extras.Clear();
            if (viewport) { viewport.gameObject.SetActive(false); UnityEngine.Object.Destroy(viewport.gameObject); }
            viewport = content = null; scroll = null;
            host = null; panel = null;
        }

        public void Dispose() => Restore();

        private sealed class RectState
        {
            internal readonly RectTransform Rect;
            internal readonly Rect Size;
            private readonly Vector2 minimum, maximum, pivot, delta, position;
            private readonly Transform parent;
            private readonly int sibling;
            private readonly TMP_Text text;
            private readonly TextWrappingModes wrapping;
            internal RectState(RectTransform rect)
            {
                Rect = rect; Size = rect.rect;
                minimum = rect.anchorMin; maximum = rect.anchorMax; pivot = rect.pivot;
                delta = rect.sizeDelta; position = rect.anchoredPosition;
                parent = rect.parent; sibling = rect.GetSiblingIndex();
                text = rect.GetComponent<TMP_Text>();
                if (text) wrapping = text.textWrappingMode;
            }
            internal void Restore()
            {
                if (!Rect) return;
                if (Rect.parent != parent) Rect.SetParent(parent, false);
                Rect.SetSiblingIndex(sibling);
                Rect.anchorMin = minimum; Rect.anchorMax = maximum; Rect.pivot = pivot;
                Rect.sizeDelta = delta; Rect.anchoredPosition = position;
                if (text) text.textWrappingMode = wrapping;
            }
        }
    }
}
