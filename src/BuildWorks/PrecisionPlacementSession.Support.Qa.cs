#if BUILDWORKS_TEST_STAND
using System;
using System.Linq;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksUnpaidSupportQa
    {
        internal static string Result = "not-started";
        internal static bool Start(float offsetX, float offsetY, float offsetZ)
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            return BuildWorksPlugin.QaPlacementSession?.QaProbeCurrentSupport(new Vector3(offsetX, offsetY, offsetZ)) == true;
        }
        internal static string State() => BuildWorksPlugin.QaPlacementSession?.QaSupportProbeState() ?? "session-unavailable";
        internal static void Release()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksPlugin.QaPlacementSession?.QaReleaseSupportProbe();
        }
    }

    internal sealed partial class PrecisionPlacementSession
    {
        private bool qaUnpaidSupportFence;
        internal string QaSupportProbeState()
        {
            if (BuildWorksUnpaidSupportQa.Result.StartsWith("pending;", StringComparison.Ordinal) && !selectingPlacementSupport)
                BuildWorksUnpaidSupportQa.Result = "cancelled;" + BuildWorksUnpaidSupportQa.Result;
            return BuildWorksUnpaidSupportQa.Result + ";fenced=" + qaUnpaidSupportFence;
        }

        internal void QaReleaseSupportProbe()
        {
            if (!qaUnpaidSupportFence) return;
            if (selectingPlacementSupport) Cancel();
            QaSupportProbeState();
            suppressPlacementFrame = Time.frameCount;
            qaUnpaidSupportFence = false;
        }

        internal bool QaProbeCurrentSupport(Vector3 offset)
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            if (state != PlacementState.Editing || sessionMode != SessionMode.BlueprintWorldPlacement ||
                activeBlueprint == null || blueprintEditPartIndex >= 0 || gizmoMode == GizmoMode.Repeat || gizmoMode == GizmoMode.Guide ||
                selectingPlacementSupport || supportOrder != null || projectPart != null ||
                nativeConstructionPayment != null || currentBlueprintPlacements.Count != 0 || constructionPayments.Count != 0 ||
                placementPlan.Count == 0 || placementPlan.Count > 64 || placementPlan.Count != placementPlanPieces.Count ||
                !BuildWorksPlugin.ConstructionProjects.QaReadonlyIdle)
                throw new InvalidOperationException("Unpaid support probe needs an untouched whole-blueprint editor plan and idle QA execution.");
            if (float.IsNaN(offset.x) || float.IsNaN(offset.y) || float.IsNaN(offset.z) ||
                float.IsInfinity(offset.x) || float.IsInfinity(offset.y) || float.IsInfinity(offset.z) || offset.magnitude > 16f)
                throw new ArgumentException("Probe offset must be finite and bounded.");
            string source = activeBlueprint.id;
            var pieces = placementPlanPieces.ToArray();
            var poses = placementPlan.Select(pose => new TransformSnapshot(pose.Position + offset, pose.Rotation,
                pose.Scale, pose.SignText, pose.ItemStandContents, pose.CustomSnapPoints, pose.MaterialSeed)).ToArray();
            BuildWorksUnpaidSupportQa.Result = "pending;source=" + source + ";parts=" + poses.Length;
            qaUnpaidSupportFence = true;
            Finish();
            suppressPlacementFrame = Time.frameCount;
            try
            {
                bool started = BeginProjectSupportSelection(pieces, poses, new bool[poses.Length], (index, reason) =>
                {
                    suppressPlacementFrame = Time.frameCount;
                    BuildWorksUnpaidSupportQa.Result = "finished;source=" + source + ";parts=" + poses.Length +
                        ";originalIndex=" + index + ";contact=" + LastPlacementContactFailure + ";reason=" + reason;
                });
                if (!started) BuildWorksUnpaidSupportQa.Result = "not-started;source=" + source;
                return started;
            }
            catch { BuildWorksUnpaidSupportQa.Result = "faulted;source=" + source; throw; }
        }
    }
}
#endif
