using System;
using System.Collections.Generic;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class MaterialVariationAcceptance
{
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException("Material variation: " + reason); }

    internal static void Run(Camera camera, GameObject wall)
    {
        GameObject source = PlacementGhostPreviewView.CreateVisualClone(wall, "Variation source", wall.layer);
        var branch = new GameObject("Nested scope");
        branch.transform.SetParent(source.transform, false);
        GameObject shape = PlacementGhostPreviewView.CreateVisualClone(wall, "Nested mesh", wall.layer);
        shape.transform.SetParent(branch.transform, false);
        var component = branch.AddComponent<RandomMaterialValues>();
        var property = new RandomMaterialValues.VectorVariationProperty {
            m_minimum = new Vector4(.1f, .2f, .3f, .4f), m_maximum = Vector4.one,
            m_propertyNames = new List<string> { "_Variation", "_VariationAlias" } };
        component.m_vectorProperties.Add(property);
        component.m_vectorProperties.Add(property);
        GameObject a = null, b = null;
        try
        {
            var original = new MaterialPropertyBlock(); original.SetFloat("_Unrelated", 42);
            shape.GetComponentInChildren<Renderer>().SetPropertyBlock(original);
            a = PlacementGhostPreviewView.CreateVisualClone(source, "Seed zero", source.layer, 0);
            b = PlacementGhostPreviewView.CreateVisualClone(source, "Seed maximum", source.layer, int.MaxValue);
            var actual = new MaterialPropertyBlock();
            Renderer aNested = a.transform.GetChild(a.transform.childCount - 1).GetComponentInChildren<Renderer>();
            Renderer bNested = b.transform.GetChild(b.transform.childCount - 1).GetComponentInChildren<Renderer>();
            aNested.GetPropertyBlock(actual);
            Vector4 zero = property.GetValue(1);
            Require(actual.GetVector("_Variation") == zero && actual.GetVector("_VariationAlias") == zero,
                "seed zero, nested renderer scope or native property-index offset differs");
            bNested.GetPropertyBlock(actual);
            Require(actual.GetVector("_Variation") == property.GetValue(int.MinValue), "Int32 seed wrap differs");
            shape.GetComponentInChildren<Renderer>().GetPropertyBlock(actual);
            Require(actual.GetFloat("_Unrelated") == 42 && actual.GetVector("_Variation") == Vector4.zero,
                "source renderer properties were mutated");
            Renderer outside = a.GetComponent<Renderer>();
            if (outside)
            {
                outside.GetPropertyBlock(actual);
                Require(actual.GetVector("_Variation") == Vector4.zero, "child variation escaped its scope");
            }

            using (var scene = new BlueprintEditorScene(camera, name => source))
            {
                var part = new BlueprintEditorPart("seeded", "woodwall", "Seeded", new Point3(0, 0, 0),
                    new Rotation3(0, 0, 0, 1), materialSeed: 0);
                var doc = new BlueprintEditorDocument(null, "Variation", "OTHER", new[] { part });
                Require(scene.TrySync(doc, out string error), error);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                object visual = ((System.Collections.IDictionary)scene.GetType().GetField("visuals", flags).GetValue(scene))["seeded"];
                var publicFlags = BindingFlags.Instance | BindingFlags.Public;
                var root = (GameObject)visual.GetType().GetField("Root", publicFlags).GetValue(visual);
                Renderer nested = root.transform.GetChild(root.transform.childCount - 1).GetComponentInChildren<Renderer>();
                visual.GetType().GetField("Hovered", publicFlags).SetValue(visual, true);
                scene.GetType().GetMethod("ApplyAppearance", flags).Invoke(scene, new[] { visual });
                nested.GetPropertyBlock(actual);
                Require(actual.GetVector("_Variation") == zero && actual.GetColor("_Color").b == 1,
                    "hover erased variation or tint");
                visual.GetType().GetField("Hovered", publicFlags).SetValue(visual, false);
                scene.GetType().GetMethod("ApplyAppearance", flags).Invoke(scene, new[] { visual });
                nested.GetPropertyBlock(actual);
                Require(actual.GetVector("_Variation") == zero && actual.GetColor("_Color") == Color.clear,
                    "hover exit lost variation or retained tint");
                var changed = new BlueprintEditorPart("seeded", "woodwall", "Seeded", new Point3(0, 0, 0),
                    new Rotation3(0, 0, 0, 1), materialSeed: 1234);
                Require(scene.TrySync(new BlueprintEditorDocument(null, "Changed", "OTHER", new[] { changed }), out error), error);
                object replacement = ((System.Collections.IDictionary)scene.GetType().GetField("visuals", flags).GetValue(scene))["seeded"];
                var replacementRoot = (GameObject)replacement.GetType().GetField("Root", publicFlags).GetValue(replacement);
                replacementRoot.transform.GetChild(replacementRoot.transform.childCount - 1).GetComponentInChildren<Renderer>().GetPropertyBlock(actual);
                Require(replacementRoot != root && !root.activeSelf && actual.GetVector("_Variation") == property.GetValue(1235),
                    "changed seed reused stale renderer state");
                scene.ShowDuplicatePreview(new[] { part });
                scene.ShowDuplicatePreview(new[] { changed });
                var previews = (System.Collections.IList)scene.GetType().GetField("duplicatePreviews", flags).GetValue(scene);
                object temporary = previews[0];
                var temporaryRoot = (GameObject)temporary.GetType().GetField("Root", publicFlags).GetValue(temporary);
                temporaryRoot.transform.GetChild(temporaryRoot.transform.childCount - 1).GetComponentInChildren<Renderer>().GetPropertyBlock(actual);
                Require(actual.GetVector("_Variation") == property.GetValue(1235), "temporary copy reused stale seed");
            }
            source.AddComponent<RandomMaterialValues>().m_vectorProperties.Add(property);
            Require(!BlueprintMaterialVariation.IsSupported(source), "overlapping native scopes were guessed");
            try { BlueprintMaterialVariation.ApplyPreview(source, a, 0); throw new InvalidOperationException("overlap accepted"); }
            catch (ArgumentException) { }
        }
        finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(source); }
    }
}
