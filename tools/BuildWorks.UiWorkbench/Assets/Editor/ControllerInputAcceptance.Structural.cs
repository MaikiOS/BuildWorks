using System.Collections;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using TMPro;
using UnityEngine;

internal static partial class ControllerInputAcceptance
{
    private static IEnumerable TestStructuralOverlay(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view, Camera uiCamera, RenderTexture target, string output, GameObject source)
    {
        Require(!scene.StructuralEnabled && scene.StructuralAnalysis == null, "Structural overlay must start off");
        var document = controller.Document;
        int revision = document.RevisionToken;
        bool dirty = document.IsDirty;
        var material = source.AddComponent<WearNTear>(); material.m_comOffset = Vector3.zero;
        var visual = Field<IDictionary>(scene, "visuals")[document.Parts[0].StableId];
        Renderer renderer = Field<GameObject>(visual, "Root").GetComponentsInChildren<Renderer>(true)[0];
        var before = new MaterialPropertyBlock(); renderer.GetPropertyBlock(before);
        int[] materialIds = renderer.sharedMaterials.Select(value => value.GetInstanceID()).ToArray();
        try
        {
            view.HideViewportSettings(); NativeClick(view, uiCamera, "ViewportSectionButton1"); yield return null;
            NativeClick(view, uiCamera, "StructuralCheck");
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            Require(scene.StructuralEnabled && scene.StructuralAnalysis.Nodes.Count == document.Parts.Count &&
                scene.StructuralAnalysis.Nodes.Any(node => node.State == BlueprintStructuralState.Supported),
                "Structural toggle did not analyze actual document pieces");
            var colored = new MaterialPropertyBlock(); renderer.GetPropertyBlock(colored);
            Require(colored.HasColor("_Color") && materialIds.SequenceEqual(renderer.sharedMaterials.Select(value => value.GetInstanceID())),
                "Structural overlay did not color owned renderers or replaced their materials");
            Save(scene, view, uiCamera, target, Path.Combine(output, "input-structural-supported.png"));
            string movingId = document.Parts[0].StableId;
            scene.PreviewTransform(document, new[] { movingId }, Vector3.up * 5, Quaternion.identity, Vector3.zero);
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            Require(scene.StructuralAnalysis.Nodes.Single(node => node.Part.Id == movingId).State == BlueprintStructuralState.Unsupported,
                "Structural overlay ignored the live transform preview pose");
            Require(scene.TrySync(document, out _), "Could not restore structural transform preview");
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            var first = document.Parts[0];
            scene.ShowDuplicatePreview(new[] { new OstrixMods.BuildWorks.Geometry.BlueprintEditorPart("structuralCopy",
                first.PrefabName, "Structural copy", first.Position + new OstrixMods.BuildWorks.Geometry.Point3(0, 5, 0), first.Rotation) });
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            Require(scene.StructuralAnalysis.Nodes.Count == document.Parts.Count + 1 &&
                scene.StructuralAnalysis.Nodes.Any(node => node.Part.Id.StartsWith("@preview/") && node.State == BlueprintStructuralState.Unsupported),
                "Structural overlay omitted the moving copy preview");
            scene.ClearDuplicatePreview();
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            scene.PreviewTransform(document, new[] { first.StableId }, Vector3.up * 5, Quaternion.identity, Vector3.zero);
            var previewBase = V(first.Position) + Vector3.up * (5 - renderer.bounds.size.y * .9f);
            scene.ShowDuplicatePreview(new[] { new OstrixMods.BuildWorks.Geometry.BlueprintEditorPart("supportPreview",
                first.PrefabName, "Preview support witness",
                new OstrixMods.BuildWorks.Geometry.Point3(previewBase.x, previewBase.y, previewBase.z), first.Rotation, scale: first.Scale) });
            scene.SetGroundHeight(previewBase.y);
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            var previewReceiver = scene.StructuralAnalysis.Nodes.Single(node => node.Part.Id == first.StableId);
            Require(previewReceiver.State == BlueprintStructuralState.Supported && previewReceiver.PrimaryId.StartsWith("@preview/"),
                "Preview source fixture did not support the selected receiver");
            Require(scene.StructuralStatus(first.StableId).Contains("Preview support witness"),
                "Structural status omitted the actual preview support source");
            var reusedPreview = Field<GameObject>(Field<IList>(scene, "duplicatePreviews")[0], "Root");
            scene.ShowDuplicatePreview(new[] { new OstrixMods.BuildWorks.Geometry.BlueprintEditorPart("supportPreview",
                first.PrefabName, "Renamed preview support",
                new OstrixMods.BuildWorks.Geometry.Point3(previewBase.x, previewBase.y, previewBase.z), first.Rotation, scale: first.Scale) });
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            Require(Field<GameObject>(Field<IList>(scene, "duplicatePreviews")[0], "Root") == reusedPreview &&
                scene.StructuralStatus(first.StableId).Contains("Renamed preview support") &&
                !scene.StructuralStatus(first.StableId).Contains("Preview support witness"),
                "Reused preview kept the previous support source name");
            scene.ClearDuplicatePreview(); scene.SetGroundHeight(0);
            Require(scene.TrySync(document, out _), "Preview support fixture could not restore document visuals");
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            TMP_InputField height = Field<TMP_InputField>(view, "groundHeightInput");
            height.SetTextWithoutNotify("-20"); height.onEndEdit.Invoke(height.text);
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            Require(scene.StructuralAnalysis.Nodes.All(node => node.State == BlueprintStructuralState.Unsupported && node.WouldFall),
                "Lowered assumed ground left false supported editor pieces");
            Save(scene, view, uiCamera, target, Path.Combine(output, "input-structural-unsupported.png"));
            material.FixtureMaximum = float.NaN;
            height.SetTextWithoutNotify("0"); height.onEndEdit.Invoke(height.text);
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            Require(scene.StructuralAnalysis.Nodes.All(node => node.State == BlueprintStructuralState.Unknown && !node.ColorValue.HasValue),
                "Unknown prefab material was displayed as verified support");
            material.FixtureMaximum = 100;
            height.SetTextWithoutNotify("0"); height.onEndEdit.Invoke(height.text);
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            Require(document.RevisionToken == revision && document.IsDirty == dirty,
                "Structural settings/estimation changed document/history");
            Require(document.CanUndo && document.Undo(), "Structural Undo fixture could not undo");
            Call(controller, "Bind", "Structural Undo");
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            Require(scene.StructuralAnalysis.Nodes.Count == document.Parts.Count, "Structural analysis ignored Undo document revision");
            Require(document.Redo(), "Structural Undo fixture could not redo");
            Call(controller, "Bind", "Structural Redo");
            foreach (object tick in FinishStructural(controller, input, scene, view)) yield return tick;
            Require(document.RevisionToken == revision && scene.StructuralAnalysis.Nodes.Count == document.Parts.Count,
                "Structural analysis ignored Redo or changed its restored revision");
            NativeClick(view, uiCamera, "StructuralCheck"); controller.LateUpdate(); yield return null;
            var restored = new MaterialPropertyBlock(); renderer.GetPropertyBlock(restored);
            Require(!scene.StructuralEnabled && scene.StructuralAnalysis == null &&
                restored.HasColor("_Color") == before.HasColor("_Color") && restored.GetColor("_Color") == before.GetColor("_Color") &&
                restored.HasColor("_BaseColor") == before.HasColor("_BaseColor") && restored.GetColor("_BaseColor") == before.GetColor("_BaseColor") &&
                restored.GetColor("_EmissionColor") == before.GetColor("_EmissionColor"),
                "Turning support overlay off left material tint behind");
            Require(materialIds.SequenceEqual(renderer.sharedMaterials.Select(value => value.GetInstanceID())), "Overlay toggling replaced renderer materials");
            Debug.Log("BUILDWORKS_EDITOR_STRUCTURAL_OVERLAY_OK actual UI/settings/materials/UndoRedo;fixture profiles only");
        }
        finally
        {
            if (scene.StructuralEnabled) scene.SetStructuralCheck(false);
            Object.DestroyImmediate(material); view.HideViewportSettings();
        }
    }

    private static IEnumerable FinishStructural(BlueprintEditorController controller, InputFrames input,
        BlueprintEditorScene scene, BlueprintEditorView view)
    {
        int frames = 0;
        do
        {
            input.Next(view.ViewportScreenRect().center); controller.Update(); controller.LateUpdate();
            yield return null;
        } while (scene.StructuralBusy && frames++ < 1200);
        Require(!scene.StructuralBusy && scene.StructuralError == null && scene.StructuralAnalysis != null && scene.StructuralAnalysis.Complete,
            "Editor structural estimate did not finish: " + scene.StructuralError);
    }
}
