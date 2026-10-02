using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Only external Valheim dependencies. Rendering, aggregation layout and restoration
// are the hash-verified production BlueprintResourceHudView, not a facsimile.
public class Localization
{
    public static Localization instance = new Localization();
    public string Localize(string token) => token == "$menu_none" ? "None" : token.TrimStart('$');
}
public static class InventoryGui
{
    public static int Calls;
    public static bool SetupRequirement(Transform root, Piece.Requirement req, Player player,
        bool craft, int quality, int craftMultiplier)
    {
        ++Calls;
        root.Find("res_icon").GetComponent<Image>().sprite = req.m_resItem.m_itemData.GetIcon();
        root.Find("res_name").GetComponent<TMP_Text>().text = Localization.instance.Localize(req.m_resItem.m_itemData.m_shared.m_name);
        root.Find("res_amount").GetComponent<TMP_Text>().text = (req.m_amount * craftMultiplier).ToString();
        root.GetComponent<UITooltip>().m_text = req.m_resItem.m_itemData.m_shared.m_name;
        return true;
    }
}

internal static class BlueprintResourceHudAcceptance
{
    internal static string Run(TMP_FontAsset font)
    {
        var root = new GameObject("NativeHudCardFixture", typeof(RectTransform), typeof(Canvas), typeof(Hud), typeof(Player));
        var zone = new GameObject("FixtureZone", typeof(ZoneSystem));
        var item = new GameObject("FixtureWood", typeof(ItemDrop));
        var stationObject = new GameObject("FixtureStation", typeof(CraftingStation));
        ZoneSystem previous = ZoneSystem.instance;
        try
        {
            ZoneSystem.instance = zone.GetComponent<ZoneSystem>();
            RectTransform canvas = (RectTransform)root.transform;
            canvas.sizeDelta = new Vector2(1920,1080);
            RectTransform panel = Rect("Info", canvas, new Vector2(640,190), Vector2.zero);
            root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 10f);
            panel.pivot = new Vector2(.5f, 0f);
            Hud hud = root.GetComponent<Hud>();
            hud.m_rootObject = root;
            hud.m_buildSelection = Text("Title", panel, new Vector2(240,140), "Blueprint");
            hud.m_pieceDescription = Text("Description", panel, new Vector2(240,100), "Five parts");
            hud.m_buildIcon = Rect("Icon", panel, new Vector2(64,64), new Vector2(-200,140)).gameObject.AddComponent<Image>();
            hud.m_requirementItems = new GameObject[4];
            for (int index = 0; index < 4; ++index)
            {
                RectTransform cell = Rect("Requirement" + index, panel, new Vector2(80,80), new Vector2(-260 + index*84,44));
                cell.gameObject.AddComponent<UITooltip>();
                Rect("res_icon", cell, new Vector2(64,64), Vector2.zero).gameObject.AddComponent<Image>();
                Text("res_name", cell, Vector2.up * 20, "");
                Text("res_amount", cell, -Vector2.up * 25, "");
                hud.m_requirementItems[index] = cell.gameObject;
            }
            Player player = root.GetComponent<Player>();
            player.Inventory.Amount = 8;
            ItemDrop wood = item.GetComponent<ItemDrop>();
            Sprite icon = Sprite.Create(Texture2D.whiteTexture, new Rect(0,0,1,1), Vector2.one * .5f);
            wood.m_itemData.Icon = icon;
            CraftingStation station = stationObject.GetComponent<CraftingStation>();
            station.m_icon = icon;
            var resources = new List<Piece.Requirement>();
            var paid = new List<int>();
            for (int index = 0; index < 24; ++index)
            { resources.Add(new Piece.Requirement { m_resItem = wood, m_amount = 10 }); paid.Add(4); }
            Vector2 size = panel.sizeDelta, position = panel.anchoredPosition;
            Vector2 firstPosition = ((RectTransform)hud.m_requirementItems[0].transform).anchoredPosition;
            Vector3 titlePosition = hud.m_buildSelection.transform.localPosition;
            using (var view = new BlueprintResourceHudView())
            {
                int calls = InventoryGui.Calls;
                view.Show(hud, player, resources, paid, new[] { station });
                Require(InventoryGui.Calls - calls == 24, "Resources bypass native SetupRequirement");
                Require(panel.rect.width > size.x && panel.rect.height > size.y, "Native panel does not widen and wrap");
                Require(hud.m_requirementItems.Length == 4, "Adapter replaced native requirement array");
                Require(hud.m_requirementItems[0].transform.Find("res_icon").GetComponent<Image>().sprite == icon &&
                    hud.m_requirementItems[0].transform.Find("res_amount").GetComponent<TMP_Text>().text == "10" &&
                    hud.m_requirementItems[0].transform.Find("res_amount").GetComponent<TMP_Text>().color == Color.white,
                    "Total quantity/icon or mixed paid amount availability incorrect");
                Require(hud.m_buildSelection.transform.localPosition.y > titlePosition.y, "Header intersects new card rows");
                var clones = new List<GameObject>();
                foreach (Transform child in panel.Find("BuildWorks_RequirementViewport/Cards"))
                    if (child.name == "BuildWorks_BlueprintRequirement") clones.Add(child.gameObject);
                Require(clones.Count == 21 && clones[20].transform.Find("res_name").GetComponent<TMP_Text>().text == "workbench" &&
                    clones[20].GetComponent<UITooltip>().m_text == "$workbench" &&
                    clones[20].transform.Find("res_amount").GetComponent<TMP_Text>().text == "None",
                    "Overflow cards or station label/raw tooltip/missing state incorrect");
                CraftingStation.Available = true;
                view.Show(hud, player, resources, paid, new[] { station });
                Require(clones[20].transform.Find("res_amount").GetComponent<TMP_Text>().text == "", "Station availability is stale");
                view.Show(hud, player, resources.GetRange(0,1), paid.GetRange(0,1), Array.Empty<CraftingStation>());
                Require(!clones[0].activeSelf && !hud.m_requirementItems[1].activeSelf, "Short blueprint leaves old cards visible");
                // Simulate native UpdateBuild before the ordinary-piece postfix.
                hud.m_requirementItems[1].SetActive(true);
                view.Restore();
                Require(panel.sizeDelta == size && panel.anchoredPosition == position &&
                    ((RectTransform)hud.m_requirementItems[0].transform).anchoredPosition == firstPosition &&
                    hud.m_buildSelection.transform.localPosition == titlePosition && hud.m_requirementItems[1].activeSelf,
                    "Ordinary HUD geometry/visibility not restored");
                foreach (GameObject clone in clones) Require(!clone.activeSelf, "Teardown leaves overflow card");
                view.Show(hud, player, resources, paid, new[] { station });
                view.Show(null, player, resources, paid, Array.Empty<CraftingStation>());
                Require(panel.sizeDelta == size, "Missing host leaves modified panel");
                view.Show(hud, player, resources, paid, new[] { station });
                canvas.sizeDelta = new Vector2(640,360);
                view.Show(hud, player, resources, paid, new[] { station });
                ScrollRect scroll = panel.GetComponentInChildren<ScrollRect>();
                RectTransform clipped = scroll.viewport;
                Require(panel.rect.height <= 334f && scroll.enabled && scroll.verticalScrollbar.gameObject.activeSelf &&
                    scroll.content.rect.height > clipped.rect.height,
                    "Small screen overflow is not bounded and scrollable: panel=" + panel.rect + "; canvas=" + canvas.rect +
                    "; scroll=" + scroll.enabled + "; content=" + scroll.content.rect + "; viewport=" + clipped.rect);
                var corners = new Vector3[4];
                clipped.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = canvas.InverseTransformPoint(corner);
                    Require(local.x >= canvas.rect.xMin - .01f && local.x <= canvas.rect.xMax + .01f &&
                        local.y >= canvas.rect.yMin - .01f && local.y <= canvas.rect.yMax + .01f,
                        "Active resize moves native card viewport outside canvas: " + local);
                }
                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                view.Restore();
                Require(panel.sizeDelta == size && hud.m_requirementItems[0].transform.parent == panel,
                    "Overflow scrolling leaks reparenting into ordinary HUD");
                Object.DestroyImmediate(icon);
            }
            return "Actual blueprint HUD adapter: native resource cells + total icons/quantity; paid availability; station live state; 25-card wrap; ordinary/missing-host restore. Native hierarchy and game costs require owner smoke.";

            TMP_Text Text(string name, Transform parent, Vector2 offset, string content)
            {
                RectTransform rect = Rect(name, parent, new Vector2(280,32), offset);
                TMP_Text text = rect.gameObject.AddComponent<TextMeshProUGUI>();
                text.font = font; text.text = content;
                return text;
            }
        }
        finally
        {
            ZoneSystem.instance = previous;
            Object.DestroyImmediate(root); Object.DestroyImmediate(zone);
            Object.DestroyImmediate(item); Object.DestroyImmediate(stationObject);
        }
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var root = new GameObject(name, typeof(RectTransform));
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = Vector2.one * .5f;
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
