using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
using Splatform;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    // Text is visual data here. No Sign, ZNetView or world identity is cloned.
    internal static class BlueprintSignPreview
    {
        internal static bool Allowed
        {
            get
            {
                try { return PlatformManager.DistributionPlatform.PrivilegeProvider
                    .CheckPrivilege(Privilege.ViewUserGeneratedContent).IsGranted(); }
                catch { return false; }
            }
        }

        private static readonly int ScaleX = Shader.PropertyToID("_ScaleX");
        private static readonly int ScaleY = Shader.PropertyToID("_ScaleY");

        internal static void ApplyScale(Renderer renderer, MaterialPropertyBlock properties, float scale)
        {
            Material material = renderer.sharedMaterial;
            if (!material || !material.HasProperty(ScaleX) || !material.HasProperty(ScaleY)) return;
            // TMP's world-space SDF scale is baked at unit scale. The shader's two scale
            // factors produce the same result without a separate mesh for each world TRS.
            properties.SetFloat(ScaleX, material.GetFloat(ScaleX) * Mathf.Abs(scale));
            properties.SetFloat(ScaleY, material.GetFloat(ScaleY) * Mathf.Abs(scale));
        }

        internal static GameObject Add(GameObject source, GameObject visual, string text)
        {
            if (text == null || !Allowed) return null;
            Sign sign = source ? source.GetComponent<Sign>() : null;
            if (!sign || !sign.m_textWidget || !BlueprintEditorPart.IsValidSignText(text) || text.Length > sign.m_characterLimit)
                throw new ArgumentException(BuildWorksLocalization.Text("blueprint.sign_preview_unavailable"));
            // Foreign blueprints have no verified platform author. Use the native pure filter.
            CensorShittyWords.Filter(text, out string filtered);
            if (string.IsNullOrEmpty(filtered)) return null;
            return Bake(source.transform, sign.m_textWidget, visual, filtered);
        }

        internal static GameObject Bake(Transform source, TextMeshProUGUI sample, GameObject visual, string text)
        {
            if (!source || !sample || !sample.font || !sample.fontSharedMaterial || !visual ||
                !sample.transform.IsChildOf(source) || !BlueprintEditorPart.IsValidSignText(text))
                throw new ArgumentException(BuildWorksLocalization.Text("blueprint.sign_preview_unavailable"));
            GameObject temporary = null, result = null;
            try
            {
                temporary = new GameObject("BuildWorks_SignBake", typeof(RectTransform), typeof(Canvas))
                    { hideFlags = HideFlags.HideAndDontSave, layer = visual.layer };
                Canvas canvas = temporary.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.enabled = false;
                var widget = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
                    { hideFlags = HideFlags.HideAndDontSave, layer = visual.layer };
                widget.transform.SetParent(temporary.transform, false);
                var target = widget.GetComponent<TextMeshProUGUI>();
                target.rectTransform.anchorMin = target.rectTransform.anchorMax = new Vector2(.5f, .5f);
                target.rectTransform.pivot = sample.rectTransform.pivot;
                target.rectTransform.sizeDelta = sample.rectTransform.rect.size;
                target.font = sample.font;
                target.fontSharedMaterial = sample.fontSharedMaterial;
                target.fontSize = sample.fontSize;
                target.fontSizeMin = sample.fontSizeMin;
                target.fontSizeMax = sample.fontSizeMax;
                target.enableAutoSizing = sample.enableAutoSizing;
                target.fontStyle = sample.fontStyle;
                target.fontWeight = sample.fontWeight;
                target.alignment = sample.alignment;
                target.margin = sample.margin;
                target.color = sample.color;
                target.enableVertexGradient = sample.enableVertexGradient;
                target.colorGradient = sample.colorGradient;
                target.characterSpacing = sample.characterSpacing;
                target.wordSpacing = sample.wordSpacing;
                target.lineSpacing = sample.lineSpacing;
                target.paragraphSpacing = sample.paragraphSpacing;
                target.lineSpacingAdjustment = sample.lineSpacingAdjustment;
                target.textWrappingMode = sample.textWrappingMode;
                target.overflowMode = sample.overflowMode;
                target.richText = sample.richText;
                target.parseCtrlCharacters = sample.parseCtrlCharacters;
                target.isRightToLeftText = sample.isRightToLeftText;
                target.fontFeatures = new List<UnityEngine.TextCore.OTL_FeatureTag>(sample.fontFeatures);
                target.extraPadding = sample.extraPadding;
                target.raycastTarget = false;
                target.text = text;
                target.ForceMeshUpdate(true, true);

                result = new GameObject("BuildWorks_SignGlyphs")
                    { hideFlags = HideFlags.HideAndDontSave, layer = visual.layer };
                result.transform.SetParent(visual.transform, false);
                var ownership = result.AddComponent<VisualCloneOwnedAssets>();
                result.AddComponent<BlueprintSignGlyphs>();
                // Preserve nested non-uniform transforms without decomposing a potentially sheared matrix.
                var ancestors = new Stack<Transform>();
                for (Transform node = sample.transform; node != source; node = node.parent) ancestors.Push(node);
                Transform parent = result.transform;
                foreach (Transform node in ancestors)
                {
                    var copy = new GameObject(node.name) { hideFlags = HideFlags.HideAndDontSave, layer = visual.layer };
                    copy.transform.SetParent(parent, false);
                    copy.transform.localPosition = node.localPosition;
                    copy.transform.localRotation = node.localRotation;
                    copy.transform.localScale = node.localScale;
                    parent = copy.transform;
                }
                for (int index = 0; index < target.textInfo.materialCount; ++index)
                {
                    TMP_MeshInfo info = target.textInfo.meshInfo[index];
                    if (info.vertexCount == 0 || !info.mesh || !info.material) continue;
                    Mesh mesh = UnityEngine.Object.Instantiate(info.mesh);
                    mesh.hideFlags = HideFlags.HideAndDontSave;
                    ownership.Track(mesh);
                    var material = new Material(info.material) { hideFlags = HideFlags.HideAndDontSave };
                    ownership.Track(material);
                    var glyph = new GameObject("Glyphs_" + index, typeof(MeshFilter), typeof(MeshRenderer))
                        { hideFlags = HideFlags.HideAndDontSave, layer = visual.layer };
                    glyph.transform.SetParent(parent, false);
                    glyph.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = glyph.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                result.GetComponent<BlueprintSignGlyphs>().Refresh();
                return result;
            }
            catch
            {
                if (result) { result.SetActive(false); UnityEngine.Object.Destroy(result); }
                throw;
            }
            finally
            {
                if (temporary) { temporary.SetActive(false); UnityEngine.Object.Destroy(temporary); }
            }
        }
    }

    internal sealed class BlueprintSignGlyphs : MonoBehaviour
    {
        private Renderer[] renderers;
        private MaterialPropertyBlock properties;
        private void LateUpdate() => Refresh();
        internal void Refresh()
        {
            renderers ??= GetComponentsInChildren<Renderer>(true);
            properties ??= new MaterialPropertyBlock();
            bool allowed = BlueprintSignPreview.Allowed;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer) continue;
                renderer.enabled = allowed;
                renderer.GetPropertyBlock(properties);
                BlueprintSignPreview.ApplyScale(renderer, properties, renderer.transform.lossyScale.y);
                renderer.SetPropertyBlock(properties);
            }
        }
    }
}
