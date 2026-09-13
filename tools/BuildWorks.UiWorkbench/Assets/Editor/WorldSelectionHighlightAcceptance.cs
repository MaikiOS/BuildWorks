using System;
using OstrixMods.BuildWorks;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class WorldSelectionHighlightAcceptance
{
    internal static string Run()
    {
        var highlight = new WorldSelectionHighlight();
        GameObject first = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject second = GameObject.CreatePrimitive(PrimitiveType.Cube);
        first.name = "WorldSelectionFirst";
        second.name = "WorldSelectionSecond";
        var materialA = new Material(Shader.Find("Standard")) { color = new Color(0.2f, 0.3f, 0.4f, 1f) };
        var materialB = new Material(Shader.Find("Standard")) { color = new Color(0.7f, 0.6f, 0.5f, 1f) };
        Renderer firstRenderer = first.GetComponent<MeshRenderer>();
        Renderer secondRenderer = second.GetComponent<MeshRenderer>();
        firstRenderer.sharedMaterials = new[] { materialA, materialB };
        secondRenderer.sharedMaterials = new[] { materialA, materialB };
        var block = new MaterialPropertyBlock();
        Color originalIndexed = new Color(0.15f, 0.45f, 0.25f, 1f);
        Color blue = new Color(0.6f, 0.8f, 1f, 1f);
        Color orange = new Color(1f, 0.55f, 0.12f, 1f);
        try
        {
            block.SetColor("_Color", originalIndexed);
            block.SetFloat("_FixtureWeight", 0.25f);
            firstRenderer.SetPropertyBlock(block, 0);
            block.Clear();
            block.SetFloat("_NativeWear", 0.2f);
            block.SetColor("_Color", Color.red);
            firstRenderer.SetPropertyBlock(block);
            highlight.Set(first, false, true);
            AssertTint(firstRenderer, 0, blue);
            AssertTint(firstRenderer, 1, blue);
            firstRenderer.GetPropertyBlock(block, 0);
            Require(Mathf.Abs(block.GetFloat("_FixtureWeight") - 0.25f) < 0.0001f,
                "World hover lost pre-existing per-material property");
            firstRenderer.GetPropertyBlock(block, 1);
            Require(Mathf.Abs(block.GetFloat("_NativeWear") - 0.2f) < 0.0001f,
                "World hover did not include native properties when indexed block was empty");

            highlight.Set(first, true, true);
            highlight.Set(second, true, false);
            AssertTint(firstRenderer, 0, orange);
            AssertTint(firstRenderer, 1, orange);
            AssertTint(secondRenderer, 0, orange);
            // This is the exact native MaterialMan write surface: renderer-level MPB.
            block.Clear();
            block.SetFloat("_NativeWear", 0.8f);
            block.SetColor("_Color", new Color(0.3f, 0.1f, 0.4f, 1f));
            firstRenderer.SetPropertyBlock(block);
            AssertTint(firstRenderer, 0, orange);
            AssertTint(firstRenderer, 1, orange);
            highlight.Set(first, false, true);
            AssertTint(firstRenderer, 0, blue);
            AssertTint(secondRenderer, 0, orange);
            highlight.Set(first, false, false);
            firstRenderer.GetPropertyBlock(block, 0);
            Require(Close(block.GetColor("_Color"), originalIndexed) &&
                Mathf.Abs(block.GetFloat("_FixtureWeight") - 0.25f) < 0.0001f,
                "World unhighlight did not restore original indexed block");
            firstRenderer.GetPropertyBlock(block, 1);
            Require(block.isEmpty, "World unhighlight left a tint in originally empty indexed slot");
            firstRenderer.GetPropertyBlock(block);
            Require(Mathf.Abs(block.GetFloat("_NativeWear") - 0.8f) < 0.0001f &&
                Close(block.GetColor("_Color"), new Color(0.3f, 0.1f, 0.4f, 1f)),
                "World unhighlight overwrote live native properties with an old snapshot");
            highlight.Set(first, false, true);
            firstRenderer.GetPropertyBlock(block, 1);
            Require(Mathf.Abs(block.GetFloat("_NativeWear") - 0.8f) < 0.0001f,
                "World re-hover did not capture current native state");
            highlight.Clear();
            highlight.Clear();
            firstRenderer.GetPropertyBlock(block, 0);
            Require(Close(block.GetColor("_Color"), originalIndexed), "Clear did not restore first selected object");
            secondRenderer.GetPropertyBlock(block, 0);
            Require(block.isEmpty, "Clear did not restore second independently selected object");
            Require(firstRenderer.sharedMaterials[0] == materialA && secondRenderer.sharedMaterials[1] == materialB &&
                Close(materialA.color, new Color(0.2f, 0.3f, 0.4f, 1f)) &&
                Close(materialB.color, new Color(0.7f, 0.6f, 0.5f, 1f)), "World highlight mutated/replaced shared materials");
            return "Actual WorldSelectionHighlight + native renderers: blue before click, orange selection priority, " +
                "independent objects, two material slots, native global MPB changes survive indexed restore/Clear";
        }
        finally
        {
            highlight.Clear();
            first.SetActive(false);
            second.SetActive(false);
            Object.Destroy(first);
            Object.Destroy(second);
            Object.Destroy(materialA);
            Object.Destroy(materialB);
        }
    }

    private static void AssertTint(Renderer renderer, int slot, Color expected)
    {
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block, slot);
        Require(Close(block.GetColor("_Color"), expected) &&
            Close(block.GetColor("_EmissionColor"), expected * 0.4f), "World indexed tint or emission differs");
    }

    private static bool Close(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.0001f && Mathf.Abs(a.g - b.g) < 0.0001f &&
        Mathf.Abs(a.b - b.b) < 0.0001f && Mathf.Abs(a.a - b.a) < 0.0001f;

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
