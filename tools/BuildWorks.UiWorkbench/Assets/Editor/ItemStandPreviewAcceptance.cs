using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Object = UnityEngine.Object;

// Executes the actual Scene/attachment/thumbnail code against a verified inert
// host fixture. It does not claim native inventory, world or mod-script parity.
internal static class ItemStandPreviewAcceptance
{
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException("Item stand preview: " + reason); }
    private static T Field<T>(object value, string name) => (T)value.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
    private static Transform Content(GameObject root) => root.GetComponentsInChildren<Transform>(true)
        .SingleOrDefault(child => child.name == "BuildWorks_ItemStandContent");
    private static BlueprintEditorPart Part(BlueprintForeignPartData data) => new BlueprintEditorPart(
        "holder", "fixture-stand", "Stand", new Point3(2, 1, 3), new Rotation3(0, .70710678118, 0, .70710678118),
        scale: new Point3(1.5, 2, .5), foreignData: data);

    private static object Call(object value, string name, params object[] args) => value.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, args);

    private static void AuthoringChecks(Camera template, GameObject source, BlueprintForeignPartData captured, string output)
    {
        string path = Path.Combine(output, "stand-authoring-" + Guid.NewGuid().ToString("N") + ".json");
        var store = new CompositeBlueprintStore(path);
        var saved = new CompositeBlueprintStore.Blueprint { id = "stand-authoring", name = "Stand authoring", parts = new List<CompositeBlueprintStore.Part> {
            new CompositeBlueprintStore.Part { stableId = "holder", prefabName = source.name, displayName = "Stand",
                foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(captured) } } };
        var recipe = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, "SwordFixture:0:1:0", null, null);
        var card = new BlueprintEditorCatalogItem("SwordFixture", "Sword fixture", null, "OTHER", "OTHER", "ITEMS", 0, itemContents: recipe);
        var noAttach = new BlueprintEditorCatalogItem("NoAttachFixture", "No attach fixture", null, "OTHER", "OTHER", "ITEMS", 1,
            itemContents: new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, "NoAttachFixture:0:1:0", null, null));
        using (var editor = new BlueprintEditorController(store, template, null, _ => source, name => name,
            () => Array.Empty<BlueprintEditorCatalogItem>(), message => { throw new InvalidOperationException(message); },
            itemCatalogItemProvider: () => new[] { noAttach, card }))
        {
            Require(editor.OpenNew(out string error), error);
            Require((bool)Call(editor, "ApplyDocumentEdit", new Func<bool>(() => editor.Document.AddPart(
                new BlueprintEditorPart("holder", source.name, "Stand", new Point3(), new Rotation3(0, 0, 0, 1), foreignData: captured))), false), "cannot create holder draft");
            Require(editor.AddPart(source.name, "Second stand"), "cannot create second part");
            string id = editor.Document.Parts[0].StableId;
            Call(editor, "SelectNode", id, false, false);
            var view = Field<BlueprintEditorView>(editor, "view");
            Require(view.ItemStandEditable && view.ItemStandHasContents, "selected stand tools unavailable");
            view.ShowToolsMenu(Vector2.zero, false);
            var tools = Field<List<UnityEngine.UI.Button>>(view, "toolsEntries");
            tools.Single(button => button.name == "ToolsItem0").onClick.Invoke();
            Require(view.HasCatalog && !Field<bool>(view, "catalogBlueprintMode") && Field<bool>(view, "catalogAttachmentMode"), "attachment catalogue not opened");
            Require(Field<RectTransform>(view, "catalogNavigation").GetComponentsInChildren<UnityEngine.UI.Button>()
                .All(button => !button.name.Contains("building")), "building navigation leaked into item catalogue");
            Require(!Field<RectTransform>(view, "catalogGrid").GetComponentsInChildren<UnityEngine.UI.Button>()
                .Any(value => value.name == "Catalog_NoAttachFixture"), "item without native attach appeared in catalogue");
            var button = Field<RectTransform>(view, "catalogGrid").GetComponentsInChildren<UnityEngine.UI.Button>()
                .Single(value => value.name == "Catalog_SwordFixture");
            BlueprintForeignPartData original = editor.Document.Parts[0].ForeignData;
            button.onClick.Invoke();
            Require(!view.HasCatalog && view.HasModal && editor.Document.Parts[0].ForeignData == original &&
                Field<BlueprintEditorCatalogItem>(editor, "placementItem") == null, "one click mutated before confirmation or spawned a Piece");
            Field<UnityEngine.UI.Button>(view, "dialogCancelButton").onClick.Invoke();
            Require(!view.HasModal && editor.Document.Parts[0].ForeignData == original, "cancel lost old state");
            Call(editor, "RequestItemContents", false);
            Call(editor, "BeginPartPlacement", card, true);
            Field<UnityEngine.UI.Button>(view, "dialogPrimaryButton").onClick.Invoke();
            Require(editor.Document.Parts[0].ForeignData == recipe && !view.HasModal &&
                Field<RectTransform>(view, "outlinerRows").Find("Contents_" + id), "confirmed recipe or holder tree relationship absent");
            Call(editor, "ApplyHistory", new Func<bool>(editor.Document.Undo), new Func<bool>(editor.Document.Redo));
            Require(editor.Document.Parts[0].ForeignData.Info == captured.Info &&
                editor.Document.Parts[0].ForeignData.Zdo == captured.Zdo, "Undo did not restore the full package");
            Call(editor, "RequestItemContents", true);
            Field<UnityEngine.UI.Button>(view, "dialogPrimaryButton").onClick.Invoke();
            Require(editor.Document.Parts[0].ForeignData == null, "explicit detach did not clear draft");
            Call(editor, "RequestItemContents", false);
            Call(editor, "BeginPartPlacement", card, true);
            Require(!view.HasModal && editor.Document.Parts[0].ForeignData == recipe, "empty stand required a second click");
            Require((bool)Call(editor, "TrySave", false, true), "authored recipe could not save");
            var reopened = new CompositeBlueprintStore(path).All().Single();
            Require(reopened.parts[0].foreignData.info == recipe.Info && reopened.parts[0].foreignData.zdo == null &&
                saved.parts[0].foreignData.info == captured.Info && saved.parts[0].foreignData.zdo == captured.Zdo,
                "save changed source or reused the old item package");
            Call(editor, "RequestItemContents", true);
            editor.Document.SetLocked(id, true);
            Field<UnityEngine.UI.Button>(view, "dialogPrimaryButton").onClick.Invoke();
            Require(editor.Document.Parts[0].ForeignData == recipe, "stale locked stand accepted contents change");
            editor.Close(true);
        }
    }

    private static BlueprintForeignPartData CaptureChecks(GameObject source, GameObject item)
    {
        Piece piece = source.AddComponent<Piece>();
        var stand = source.GetComponent<ItemStand>();
        var overrideObject = new GameObject("InertStandNetworkOverride");
        var view = overrideObject.AddComponent<ZNetView>();
        typeof(ItemStand).GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(stand, view);
        ZDO zdo = view.GetZDO();
        const int hash = -1729;
        ObjectDB.instance.ItemHashes.Add(hash, item);
        byte[] package = { 109, 2, 0, 255, 67 };
        string encoded = Convert.ToBase64String(package);
        zdo.Set(ZDOVars.s_item, hash); zdo.Set(ZDOVars.s_variant, 2);
        zdo.Set(ZDOVars.s_quality, 3); zdo.Set(ZDOVars.s_type, 0);
        zdo.Set(ZDOVars.s_itemData, package);
        int writes = zdo.Writes;
        Require(BlueprintItemStandCapture.TryCapture(piece, out var captured) &&
            captured.Format == BlueprintForeignPartData.NativeItemStandFormat &&
            captured.Info == "SwordFixture:2:3:0" && captured.Zdo == encoded && captured.Chance == null,
            "native capture lost identity/variant/quality/orientation/package");
        Require(zdo.Writes == writes && zdo.GetByteArray(ZDOVars.s_itemData, null) == package,
            "capture mutated network data");
        package[1] = 77;
        Require(captured.Zdo == encoded, "snapshot aliases live native bytes");
        foreach (byte[] invalid in new[] { new byte[2], new byte[24577], new byte[] { 0, 1, 2 } })
        {
            zdo.Set(ZDOVars.s_itemData, invalid);
            Require(!BlueprintItemStandCapture.TryCapture(piece, out var rejected) && rejected == null,
                "invalid/oversized package accepted");
        }
        zdo.Set(ZDOVars.s_itemData, (byte[])null);
        Require(BlueprintItemStandCapture.TryCapture(piece, out var legacy) && legacy.Zdo == null &&
            legacy.Info == captured.Info, "legacy stand without item package discarded");
        zdo.Set(ZDOVars.s_quality, 0);
        Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "invalid native quality accepted");
        zdo.Set(ZDOVars.s_quality, 3);
        ObjectDB.instance.ItemHashes.Remove(hash);
        Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "missing item silently erased");
        ObjectDB.instance.ItemHashes.Add(hash, item);
        view.Valid = false;
        Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "invalid view accepted");
        view.Valid = true;
        zdo.Set(ZDOVars.s_item, 0);
        Require(BlueprintItemStandCapture.TryCapture(piece, out var empty) && empty == null, "empty stand changed");
        var armour = source.AddComponent<ArmorStand>();
        Require(!BlueprintItemStandCapture.TryCapture(piece, out _), "armour pose/slots silently discarded");
        Object.Destroy(armour);
        Object.Destroy(overrideObject);
        return captured;
    }

    internal static IEnumerable<object> Run(Camera template, string output)
    {
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "fixture-stand";
        source.SetActive(false);
        source.transform.localScale = new Vector3(1.4f, .8f, 1.1f);
        var stand = source.AddComponent<ItemStand>();
        new GameObject("duplicate-anchor").transform.SetParent(source.transform, false);
        var anchor = new GameObject("duplicate-anchor").transform;
        anchor.SetParent(source.transform, false);
        anchor.localPosition = new Vector3(.1f, 1.2f, .3f);
        anchor.localRotation = Quaternion.Euler(0, 25, 90);
        anchor.localScale = new Vector3(.7f, 1.2f, .9f);
        stand.m_attachOther = anchor;
        var item = new GameObject("SwordFixture");
        item.SetActive(false);
        var drop = item.AddComponent<ItemDrop>();
        drop.m_itemData.m_shared.m_maxQuality = 4;
        drop.m_itemData.m_shared.m_variants = 3;
        drop.m_itemData.m_shared.m_scaleByQuality = .25f;
        var horizontal = new ItemStand.OrientationSettings {
            m_orientations = ItemStand.Orientation.Horizontal, m_position = new Vector3(.2f, .1f, -.3f),
            m_rotation = new Vector3(0, 90, 15), m_scale = new Vector3(.8f, 1.2f, 1.1f) };
        var vertical = new ItemStand.OrientationSettings {
            m_orientations = ItemStand.Orientation.Vertical, m_position = new Vector3(-.3f, .2f, .1f),
            m_rotation = new Vector3(45, 0, 0), m_scale = new Vector3(1.2f, .8f, .9f) };
        drop.m_itemData.m_shared.m_itemStandOffsets.Add(horizontal);
        drop.m_itemData.m_shared.m_itemStandOffsets.Add(vertical);
        var attach = new GameObject("attach").transform;
        attach.SetParent(item.transform, false);
        attach.localPosition = new Vector3(.11f, -.21f, .13f);
        attach.localRotation = Quaternion.Euler(12, 34, 56);
        attach.localScale = new Vector3(.6f, .8f, .6f);
        var attachObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        attachObject.name = "attachobj";
        attachObject.transform.SetParent(attach, false);
        // Native ItemStand overwrites these root values with attach + offset.
        attachObject.transform.localPosition = new Vector3(8, 9, 10);
        attachObject.transform.localScale = new Vector3(4, 5, 6);
        attachObject.AddComponent<ItemStyle>();
        var material = new Material(Shader.Find("BuildWorks/ItemPreviewFixture"));
        material.color = new Color(1f, .55f, .1f);
        attachObject.GetComponent<Renderer>().sharedMaterial = material;
        ObjectDB previousDb = ObjectDB.instance;
        ObjectDB.instance = new ObjectDB();
        ObjectDB.instance.Items.Add(item.name, item);
        var noAttach = new GameObject("NoAttachFixture");
        noAttach.SetActive(false);
        noAttach.AddComponent<ItemDrop>();
        ObjectDB.instance.Items.Add(noAttach.name, noAttach);
        int sourceChildren = source.transform.childCount;
        var data = new BlueprintForeignPartData("blueprint", "\"SwordFixture:2:3:0\"", "raw-zdo", ".5");
        try
        {
            BlueprintForeignPartData captured = CaptureChecks(source, item);
            yield return null;
            AuthoringChecks(template, source, captured, output);
            yield return null;
            stand.m_orientationType = ItemStand.Orientation.Horizontal;
            using (var scene = new BlueprintEditorScene(template, _ => source))
            {
                var doc = new BlueprintEditorDocument(null, "Captured", "Test", new[] { Part(captured) });
                Require(scene.TrySync(doc, out string warning) && warning == null &&
                    Content(Field<GameObject>(scene, "root")) && doc.Parts[0].ForeignData.Zdo == captured.Zdo,
                    "native-captured contents were not previewed or raw package changed");
            }
            yield return null;
            foreach (ItemStand.Orientation orientation in new[] { ItemStand.Orientation.Horizontal, ItemStand.Orientation.Vertical })
            {
                stand.m_orientationType = orientation;
                ItemStand.OrientationSettings offset = orientation == ItemStand.Orientation.Horizontal ? horizontal : vertical;
                var doc = new BlueprintEditorDocument(null, "Contents", "Test", new[] { Part(data) });
                using (var scene = new BlueprintEditorScene(template, _ => source))
                {
                    Require(scene.TrySync(doc, out string warning) && warning == null, warning);
                    GameObject root = Field<GameObject>(scene, "root");
                    Transform partRoot = root.transform.Find("Part_holder");
                    Transform content = Content(root);
                    Require(content && content.parent == partRoot.GetChild(anchor.GetSiblingIndex()), "duplicate-named anchor mapping");
                    Vector3 expectedPosition = attach.localPosition + offset.m_position;
                    Quaternion expectedRotation = attach.localRotation * Quaternion.Euler(offset.m_rotation);
                    Vector3 expectedScale = Vector3.Scale(Vector3.Scale(attach.localScale, Vector3.one * 1.5f), offset.m_scale);
                    Require(Vector3.Distance(content.localPosition, expectedPosition) < 1e-5f &&
                        Quaternion.Angle(content.localRotation, expectedRotation) < .01f &&
                        Vector3.Distance(content.localScale, expectedScale) < 1e-5f, "native offset/quality transform");
                    Require(Vector3.Distance(content.position, partRoot.GetChild(anchor.GetSiblingIndex()).TransformPoint(expectedPosition)) < 1e-5f,
                        "non-unit holder/part scale and rotation");
                    Require(content.GetComponent<Renderer>().sharedMaterial.GetInt("_Style") == 2 && material.GetInt("_Style") == 0,
                        "variant isolation");
                    Require(!root.GetComponentInChildren<ItemStand>(true) && !root.GetComponentInChildren<ItemDrop>(true) &&
                        !root.GetComponentInChildren<ItemStyle>(true) && !root.GetComponentInChildren<Collider>(true),
                        "gameplay, equipment scripts or colliders copied");
                    doc.SelectOnly("holder");
                    Require(scene.TrySync(doc, out warning) && Content(root) == content, "selection recreated item");
                    Require(doc.ApplyTransformDelta(new Point3(1, 0, 0), new Rotation3(0, 0, 0, 1), new Point3()), "transform");
                    Require(scene.TrySync(doc, out warning) && Content(root) == content &&
                        content.GetComponent<Renderer>().sharedMaterial.GetInt("_Style") == 2, "move/highlight lost variant");
                    Require(doc.Undo() && scene.TrySync(doc, out warning) && Content(root) == content, "Undo item lifetime");
                    scene.ShowDuplicatePreview(new[] { Part(data) });
                    Require(root.GetComponentsInChildren<Transform>(true).Count(child => child.name == "BuildWorks_ItemStandContent") == 2,
                        "duplicate temporary preview omitted contents");
                    scene.ClearDuplicatePreview();
                    yield return null;
                    Require(Content(root) == content, "temporary cleanup retired source contents");
                    var changed = new BlueprintForeignPartData("blueprint", "SwordFixture:1:1:0", "raw-zdo", ".5");
                    var changedDoc = new BlueprintEditorDocument(null, "Changed", "Test", new[] { Part(changed) });
                    Require(scene.TrySync(changedDoc, out warning), warning);
                    yield return null;
                    Require(Content(root) != content && Content(root).GetComponent<Renderer>().sharedMaterial.GetInt("_Style") == 1,
                        "same prefab changed contents cache");
                    Require(scene.TrySync(doc, out warning), warning);
                    yield return null;
                    Require(Content(root).GetComponent<Renderer>().sharedMaterial.GetInt("_Style") == 2, "restored contents");
                    Require(Field<System.Collections.IDictionary>(scene, "sharedPreviewMaterials").Count == 1,
                        "retired owned variant materials accumulate in the shared cache");
                }
                yield return null;
            }
            foreach (string info in new[] { "opaque", "SwordFixture:0:0:0", "SwordFixture:3:1:0", "SwordFixture:0:1:99", "SwordFixture:0:1:-1", "MissingItem:0:1:0" })
            {
                var raw = new BlueprintForeignPartData("blueprint", info, "raw-zdo", ".5");
                using (var scene = new BlueprintEditorScene(template, _ => source))
                {
                    var doc = new BlueprintEditorDocument(null, "Unsupported", "Test", new[] { Part(raw) });
                    Require(scene.TrySync(doc, out string warning) && !string.IsNullOrEmpty(warning) &&
                        !Content(Field<GameObject>(scene, "root")), "invalid or missing contents silently shown: " + info);
                    Require(doc.Parts[0].ForeignData.Info == info && doc.Parts[0].ForeignData.Zdo == "raw-zdo", "raw state changed");
                }
                yield return null;
            }
            var blueprint = new CompositeBlueprintStore.Blueprint { id = "item-preview-fixture", name = "Item preview", parts = new List<CompositeBlueprintStore.Part> {
                new CompositeBlueprintStore.Part { prefabName = source.name, position = new CompositeBlueprintStore.VectorData(Vector3.zero),
                    rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity), scale = new CompositeBlueprintStore.VectorData(Vector3.one),
                    foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(data) } } };
            var pieces = new[] { source.GetComponent<Piece>() };
            using (var renderer = new BlueprintThumbnailRenderer())
            using (var preview = renderer.BeginPreview(blueprint, pieces, out string previewError))
            {
                Require(preview != null && previewError == null && Content(Field<GameObject>(preview, "root")), "rotatable preview omitted item");
                Sprite thumbnail = renderer.Refresh(blueprint, pieces, out string thumbnailError);
                Require(thumbnailError == null, "unexpected thumbnail error");
                Require(thumbnail && thumbnail.texture, "thumbnail render failed");
                File.WriteAllBytes(Path.Combine(output, "item-stand-contents-proof.png"), thumbnail.texture.EncodeToPNG());
            }
            var scaler = attachObject.AddComponent<ParticleIntensityScaler>();
            using (var scene = new BlueprintEditorScene(template, _ => source))
            {
                var doc = new BlueprintEditorDocument(null, "Particle contents", "Test", new[] { Part(data) });
                Require(scene.TrySync(doc, out string warning) && !string.IsNullOrEmpty(warning) &&
                    !Content(Field<GameObject>(scene, "root")) && doc.Parts[0].ForeignData == data,
                    "particle quality visuals were silently dropped, executed or changed");
            }
            Object.Destroy(scaler);
            yield return null;
            var spriteObject = new GameObject("UnsupportedSprite");
            spriteObject.transform.SetParent(attachObject.transform, false);
            spriteObject.AddComponent<SpriteRenderer>();
            using (var renderer = new BlueprintThumbnailRenderer())
            {
                using (var preview = renderer.BeginPreview(blueprint, pieces, out string previewError))
                    Require(preview == null && !string.IsNullOrEmpty(previewError), "partial preview shown without reason");
                Require(!renderer.Refresh(blueprint, pieces, out string thumbnailError) && !string.IsNullOrEmpty(thumbnailError),
                    "partial thumbnail stored without reason");
                Require(!renderer.GetCached(blueprint), "partial thumbnail cached");
            }
            using (var reopened = new BlueprintThumbnailRenderer())
                Require(!reopened.GetCached(blueprint), "failed refresh restored stale thumbnail after reopening");
            using (var scene = new BlueprintEditorScene(template, _ => source))
            {
                var doc = new BlueprintEditorDocument(null, "Unsupported renderer", "Test", new[] { Part(data) });
                Require(scene.TrySync(doc, out string warning) && !string.IsNullOrEmpty(warning) &&
                    !Content(Field<GameObject>(scene, "root")) && doc.Parts[0].ForeignData == data,
                    "unsupported renderer silently omitted");
            }
            Object.Destroy(spriteObject);
            Object.Destroy(attachObject.GetComponent<ItemStyle>());
            yield return null;
            attachObject.AddComponent<UnsupportedItemVisual>();
            using (var scene = new BlueprintEditorScene(template, _ => source))
            {
                var doc = new BlueprintEditorDocument(null, "Unsupported script", "Test", new[] { Part(data) });
                Require(scene.TrySync(doc, out string warning) && !string.IsNullOrEmpty(warning) &&
                    !Content(Field<GameObject>(scene, "root")), "unknown equipment script was approximated or executed");
            }
            Require(source.transform.childCount == sourceChildren && !Content(source) &&
                attachObject.transform.localPosition == new Vector3(8, 9, 10) && material.GetInt("_Style") == 0 &&
                data.Info == "\"SwordFixture:2:3:0\"" && data.Zdo == "raw-zdo" && data.Chance == ".5", "source mutated");
        }
        finally
        {
            ObjectDB.instance = previousDb;
            Object.Destroy(source); Object.Destroy(item); Object.Destroy(noAttach); Object.Destroy(material);
        }
        yield return null;
    }
}
