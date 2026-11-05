using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintEditorScene
    {
        private readonly struct StructuralPose
        {
            internal StructuralPose(VisualNode visual)
            {
                Visual = visual; Position = visual.Root.transform.position;
                Rotation = visual.Root.transform.rotation; Scale = visual.Root.transform.lossyScale;
            }
            internal VisualNode Visual { get; }
            internal Vector3 Position { get; }
            internal Quaternion Rotation { get; }
            internal Vector3 Scale { get; }
            internal bool Same(StructuralPose other) => Visual == other.Visual && Position.Equals(other.Position) &&
                Rotation.Equals(other.Rotation) && Scale.Equals(other.Scale);
        }

        private readonly List<StructuralPose> structuralCurrent = new List<StructuralPose>();
        private readonly List<StructuralPose> structuralLast = new List<StructuralPose>();
        private readonly Dictionary<VisualNode, BlueprintStructuralAnalysis.Node> structuralColors =
            new Dictionary<VisualNode, BlueprintStructuralAnalysis.Node>();
        private BlueprintStructuralSnapshot structuralSnapshot;
        private BlueprintStructuralAnalysis structuralAnalysis;
        private IEnumerator<int> structuralWork;
        private BlueprintEditorDocument structuralDocument;
        private int structuralRevision = -1;
        private bool structuralWaiting;
        private float structuralStartAt;
        private string structuralError;
        internal bool StructuralEnabled { get; private set; }
        internal bool StructuralBusy => StructuralEnabled && (structuralWaiting || structuralWork != null);
        internal BlueprintStructuralAnalysis StructuralAnalysis => structuralAnalysis;
        internal string StructuralError => structuralError;

        internal void SetStructuralCheck(bool enabled)
        {
            ThrowIfDisposed();
            if (StructuralEnabled == enabled) return;
            StructuralEnabled = enabled;
            CancelStructuralAnalysis();
            structuralLast.Clear(); structuralDocument = null; structuralRevision = -1;
            structuralWaiting = enabled; structuralStartAt = Time.realtimeSinceStartup + .15f;
        }

        private void InvalidateStructuralAnalysis()
        {
            if (!StructuralEnabled) return;
            CancelStructuralAnalysis();
            structuralWaiting = true; structuralStartAt = Time.realtimeSinceStartup + .15f;
            structuralRevision = -1;
        }

        internal void UpdateStructuralAnalysis(BlueprintEditorDocument document)
        {
            if (!StructuralEnabled || disposed || initialDocument != null) return;
            structuralCurrent.Clear();
            foreach (VisualNode visual in visuals.Values)
                if (visual.Root) structuralCurrent.Add(new StructuralPose(visual));
            foreach (VisualNode visual in contourPreviews.Concat(duplicatePreviews))
                if (visual.Root && visual.Root.activeSelf) structuralCurrent.Add(new StructuralPose(visual));
            if (placementPreview?.Root && placementPreview.Root.activeSelf) structuralCurrent.Add(new StructuralPose(placementPreview));
            bool changed = structuralDocument != document || structuralRevision != document.RevisionToken ||
                structuralCurrent.Count != structuralLast.Count;
            for (int index = 0; !changed && index < structuralCurrent.Count; ++index)
                changed = !structuralCurrent[index].Same(structuralLast[index]);
            if (changed)
            {
                CancelStructuralAnalysis();
                structuralLast.Clear(); structuralLast.AddRange(structuralCurrent);
                structuralDocument = document; structuralRevision = document.RevisionToken;
                structuralWaiting = true; structuralStartAt = Time.realtimeSinceStartup + .15f;
            }
            if (structuralWaiting && Time.realtimeSinceStartup >= structuralStartAt)
            { structuralWaiting = false; structuralWork = CalculateStructural().GetEnumerator(); }
            if (structuralWork == null) return;
            var watch = Stopwatch.StartNew();
            try
            {
                for (int index = 0; index < 16 && (index == 0 || watch.Elapsed.TotalMilliseconds < 8); ++index)
                    if (!structuralWork.MoveNext()) { structuralWork.Dispose(); structuralWork = null; break; }
            }
            catch (Exception error)
            {
                CancelStructuralAnalysis(); structuralError = error.Message;
                UnityEngine.Debug.LogWarning("BuildWorks structural estimate failed: " + error);
            }
        }

        private IEnumerable<int> CalculateStructural()
        {
            structuralSnapshot = new BlueprintStructuralSnapshot();
            foreach (StructuralPose pose in structuralLast)
            {
                VisualNode visual = pose.Visual;
                GameObject source = visual.Placeholder ? null : resolveVisualSource(visual.PrefabName);
                structuralSnapshot.Add(visual.StructuralId, source, pose.Position, pose.Rotation, pose.Scale);
                yield return 0;
            }
            structuralAnalysis = new BlueprintStructuralAnalysis(structuralSnapshot, groundHeight);
            structuralSnapshot = null;
            while (!structuralAnalysis.Step(1, 8)) yield return 0;
            var byId = structuralLast.ToDictionary(pose => pose.Visual.StructuralId, pose => pose.Visual, StringComparer.Ordinal);
            foreach (BlueprintStructuralAnalysis.Node node in structuralAnalysis.Nodes)
                structuralColors[byId[node.Part.Id]] = node;
            foreach (VisualNode visual in structuralColors.Keys) ApplyAppearance(visual);
        }

        private void CancelStructuralAnalysis()
        {
            structuralWork?.Dispose(); structuralWork = null;
            structuralAnalysis?.Dispose(); structuralAnalysis = null;
            structuralSnapshot?.Dispose(); structuralSnapshot = null;
            structuralError = null;
            if (structuralColors.Count == 0) return;
            var colored = structuralColors.Keys.ToArray(); structuralColors.Clear();
            foreach (VisualNode visual in colored) if (visual.Root) ApplyAppearance(visual);
        }

        private void ApplyStructuralAppearance(VisualNode visual, Renderer renderer)
        {
            if (!StructuralEnabled || !structuralColors.TryGetValue(visual, out BlueprintStructuralAnalysis.Node node)) return;
            Color color = new Color(.55f, .55f, .55f);
            if (node.ColorValue.HasValue)
            {
                float value = node.ColorValue.Value;
                color = new Color(.6f, .8f, 1f);
                if (value >= 0)
                {
                    Color.RGBToHSV(Color.Lerp(Color.red, Color.green, value), out float h, out _, out _);
                    color = Color.HSVToRGB(h, Mathf.Lerp(1, .5f, value), Mathf.Lerp(1.2f, .9f, value));
                }
            }
            if (visual.Selected || visual.ActiveSelection || visual.Hovered) color = Color.Lerp(color, Color.white, .12f);
            renderer.GetPropertyBlock(appearanceProperties);
            appearanceProperties.SetColor("_Color", color); appearanceProperties.SetColor("_BaseColor", color);
            appearanceProperties.SetColor("_EmissionColor", color * .4f);
            renderer.SetPropertyBlock(appearanceProperties);
        }

        internal string StructuralStatus(string activeId)
        {
            if (!StructuralEnabled) return BuildWorksLocalization.Text("editor.support.off");
            string ground = BuildWorksLocalization.Text("editor.support.ground", groundHeight.ToString("0.###", CultureInfo.CurrentCulture));
            if (structuralError != null) return ground + "\n" + BuildWorksLocalization.Text("editor.support.failed");
            if (StructuralBusy || structuralAnalysis == null)
                return ground + "\n" + BuildWorksLocalization.Text("editor.support.progress", structuralAnalysis?.Processed ?? 0, structuralLast.Count);
            var nodes = structuralAnalysis.Nodes;
            string summary = BuildWorksLocalization.Text("editor.support.summary",
                nodes.Count(node => node.State == BlueprintStructuralState.Supported),
                nodes.Count(node => node.State == BlueprintStructuralState.Unsupported),
                nodes.Count(node => node.State == BlueprintStructuralState.Unknown || node.State == BlueprintStructuralState.NotApplicable));
            BlueprintStructuralAnalysis.Node selected = nodes.FirstOrDefault(node => node.Part.Id == activeId);
            if (selected == null) return ground + "\n" + summary;
            if (!selected.ColorValue.HasValue)
            {
                string key = selected.State == BlueprintStructuralState.NotApplicable ? "editor.support.reason.static" :
                    selected.Reason == "native-query-capacity" ? "editor.support.reason.capacity" :
                    selected.Reason == "analysis-budget" ? "editor.support.reason.budget" :
                    selected.Reason == "unknown-support-chain" || selected.Reason == "unknown-contact-part" ? "editor.support.reason.chain" :
                    selected.Reason == "missing-prefab" || selected.Reason == "incomplete-blueprint-geometry" ||
                    selected.Reason == "unknown-native-envelope" || selected.Reason == "unsupported-collider" ? "editor.support.reason.geometry" : "editor.support.reason.model";
                return ground + "\n" + summary + "\n" + BuildWorksLocalization.Text(key);
            }
            string detail = BuildWorksLocalization.Text("editor.support.value", selected.Result.Support.ToString("0.##", CultureInfo.CurrentCulture),
                selected.Part.Profile.Value.Minimum.ToString("0.##", CultureInfo.CurrentCulture));
            if (selected.WouldFall) detail += " · " + BuildWorksLocalization.Text("editor.support.fall");
            else if (selected.State == BlueprintStructuralState.Unsupported) detail += " · " + BuildWorksLocalization.Text("editor.support.no_wear");
            string supportId = selected.State == BlueprintStructuralState.Unsupported ? selected.LostSupportId ?? selected.PrimaryId : selected.PrimaryId;
            string source = StructuralSourceName(supportId);
            string secondary = StructuralSourceName(selected.SecondaryId);
            if (source != null && secondary != null && selected.State == BlueprintStructuralState.Supported) source += " + " + secondary;
            if (source != null) detail += "\n" + BuildWorksLocalization.Text("editor.support.source", source);
            return ground + "\n" + summary + "\n" + detail;
        }

        private string StructuralSourceName(string id) => id == "@ground"
            ? BuildWorksLocalization.Text("editor.support.assumed_ground")
            : structuralDocument.Parts.FirstOrDefault(part => part.StableId == id)?.DisplayName ??
                structuralLast.FirstOrDefault(pose => pose.Visual.StructuralId == id).Visual?.DisplayName;
    }
}
