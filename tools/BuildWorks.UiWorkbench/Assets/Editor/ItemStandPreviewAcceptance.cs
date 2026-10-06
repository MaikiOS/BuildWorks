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
        int sourceChildren = source.transform.childCount;
        var data = new BlueprintForeignPartData("blueprint", "\"SwordFixture:2:3:0\"", "raw-zdo", ".5");
        try
        {
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
            var pieces = new[] { source.AddComponent<Piece>() };
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
            Object.Destroy(source); Object.Destroy(item); Object.Destroy(material);
        }
        yield return null;
    }
}
