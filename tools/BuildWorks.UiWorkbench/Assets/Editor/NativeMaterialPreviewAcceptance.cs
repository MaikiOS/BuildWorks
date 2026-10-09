using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;
using OstrixMods.BuildWorks;

internal static class NativeMaterialPreviewAcceptance
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static object Get(object target, string name) => target.GetType().GetField(name, Instance).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Instance).SetValue(target, value);
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    internal static void Run(string output)
    {
        Assembly plugin = typeof(NativePlacementMaterialPreview).Assembly;
        Type pieceType = typeof(Piece);
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        Type randomType = pieceType.Assembly.GetType("RandomMaterialValues", true);
        Type propertyType = randomType.GetNestedType("VectorVariationProperty", BindingFlags.Public);
        Type previewType = plugin.GetType("OstrixMods.BuildWorks.NativePlacementMaterialPreview", true);
        Type cloneType = plugin.GetType("OstrixMods.BuildWorks.PlacementGhostPreviewView", true);
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube); source.SetActive(false);
        GameObject ghost = null; object preview = Activator.CreateInstance(previewType, true);
        MethodInfo show = previewType.GetMethod("Show", Instance), clear = previewType.GetMethod("Clear", Instance);
        try
        {
            object component = source.AddComponent(randomType), property = Activator.CreateInstance(propertyType);
            Set(property, "m_minimum", new Vector4(.1f, .2f, .3f, .4f));
            Set(property, "m_maximum", new Vector4(.5f, .6f, .7f, .8f));
            Set(property, "m_propertyNames", new System.Collections.Generic.List<string> { "_NativeVariation" });
            ((IList)Get(component, "m_vectorProperties")).Add(property);
            ghost = (GameObject)cloneType.GetMethod("CreateVisualClone", Static).Invoke(null, new object[] { source, "Native inert ghost", 0, null });
            Renderer original = ghost.GetComponent<Renderer>();
            var block = new MaterialPropertyBlock(); block.SetVector("_NativeVariation", Vector4.one);
            block.SetFloat("_Other", 42); block.SetColor("_Color", Color.red); original.SetPropertyBlock(block);
            block.Clear(); block.SetFloat("_IndexedOnly", 77); original.SetPropertyBlock(block, 0);
            ghost.transform.SetPositionAndRotation(new Vector3(2, 3, 4), Quaternion.Euler(12, 73, 4));
            ghost.transform.localScale = new Vector3(.5f, 2f, 1.25f);
            GameObject previous = null;
            foreach (int seed in new[] { 0, int.MaxValue, 0 })
            {
                show.Invoke(preview, new object[] { source, ghost, seed });
                GameObject visual = (GameObject)Get(preview, "visual");
                Require(visual && visual != ghost && original.forceRenderingOff && original.enabled,
                    "Preview replaced native validation object or failed to suppress duplicate geometry");
                if (previous) Require(!previous.activeSelf, "Changed material seed left an old visual active");
                Require(visual.transform.position == ghost.transform.position && visual.transform.rotation == ghost.transform.rotation &&
                    visual.transform.localScale == ghost.transform.lossyScale, "Material visual differs from native ghost XYZ pose");
                visual.GetComponent<Renderer>().GetPropertyBlock(block);
                var owned = Activator.CreateInstance(propertyType);
                Set(owned, "m_minimum", Get(property, "m_minimum")); Set(owned, "m_maximum", Get(property, "m_maximum"));
                Vector4 expected = (Vector4)propertyType.GetMethod("GetValue").Invoke(owned, new object[] { seed });
                Require(block.GetVector("_NativeVariation") == expected && block.GetColor("_Color") == Color.red && block.GetFloat("_Other") == 42,
                    "Native ghost preview lost seeded values, validation tint or unrelated property");
                visual.GetComponent<Renderer>().GetPropertyBlock(block, 0);
                Require(block.GetFloat("_IndexedOnly") == 77, "Native per-material property override was lost");
                original.GetPropertyBlock(block);
                Require(block.GetVector("_NativeVariation") == Vector4.one && block.GetFloat("_Other") == 42,
                    "Preview mutated native ghost MPB");
                previous = visual;
            }
            block.Clear(); block.SetFloat("_Other", 99); original.SetPropertyBlock(block);
            original.SetPropertyBlock(null, 0);
            show.Invoke(preview, new object[] { source, ghost, 0 });
            var same = (GameObject)Get(preview, "visual");
            Require(same == previous, "Unchanged seed rebuilt the native material copy");
            same.GetComponent<Renderer>().GetPropertyBlock(block);
            Require(!block.HasColor("_Color") && block.GetFloat("_Other") == 99, "Stale invalid tint or caller value survived a native update");
            same.GetComponent<Renderer>().GetPropertyBlock(block, 0);
            Require(block.isEmpty, "Removed per-material override survived a native update");
            show.Invoke(preview, new object[] { source, ghost, null });
            Require(!original.forceRenderingOff && !same.activeSelf && Get(preview, "visual") == null,
                "Null seed did not restore native renderer and clear its owned copy");
            original.forceRenderingOff = true;
            show.Invoke(preview, new object[] { source, ghost, 0 });
            Require(((GameObject)Get(preview, "visual")).GetComponent<Renderer>().forceRenderingOff,
                "Preview exposed an already hidden native renderer");
            clear.Invoke(preview, null); Require(original.forceRenderingOff, "Cleanup lost previous rendering flag");
            original.forceRenderingOff = false;
            try { show.Invoke(preview, new object[] { source, ghost, -1 }); throw new Exception("Invalid native seed rendered"); }
            catch (TargetInvocationException error) { Require(error.InnerException is ArgumentException, "Wrong native preview refusal"); }
            Require(!original.forceRenderingOff && Get(preview, "visual") == null, "Failed preview leaked visibility state");
            Require(propertyType.GetField("random", Instance).GetValue(property) == null, "Native ghost preview changed fixture source RNG");
            File.WriteAllText(Path.Combine(output, "native-material-preview-check.txt"), "PASS Workbench runtime native material visual copy: seed A/B/A, XYZ pose, caller/tint update, cache, flags, null/failure cleanup; native MPB/source untouched, no game lifecycle executed");
        }
        finally
        {
            clear.Invoke(preview, null);
            if (ghost) UnityEngine.Object.DestroyImmediate(ghost);
            UnityEngine.Object.DestroyImmediate(source);
        }
    }

}
