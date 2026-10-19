using System;
using System.Linq;
using UnityEngine;
using Store = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class PrecisionPlacementSession
    {
        private BlueprintConstructionPartExecution projectPart;
        private Guid placementProjectId;
        internal Guid LastCreatedProject { get; private set; }
        // shortcut: structural project caller first; special surfaces and contents need their existing separate workflows.
        internal static bool SupportsProjectPart(Piece piece) => SupportsPrecisionPlacement(piece) &&
            piece.GetComponentsInChildren<TerrainModifier>(true).Length == 0 && !piece.GetComponent<Container>() &&
            !piece.GetComponent<ItemStand>() && !piece.GetComponent<ArmorStand>();

        // Consume the confirmation here; only the queued ProjectPart session may reach native placement.
        private bool TryCreateConstructionProject(Player player)
        {
            var runtime = BuildWorksPlugin.ConstructionProjects;
            if (sessionMode != SessionMode.BlueprintWorldPlacement || activeBlueprint == null ||
                !BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement || gizmoMode == GizmoMode.Repeat || gizmoMode == GizmoMode.Guide ||
                placementPlan.Count != activeBlueprint.parts.Count || placementPlanPieces.Count != placementPlan.Count ||
                currentBlueprintPlacements.Count != 0 || constructionPayments.Count != 0 || nativeConstructionPayment != null ||
                placementPlanPieces.Any(piece => !piece || !SupportsProjectPart(piece)) ||
                activeBlueprint.parts.Any(part => part.foreignData != null) || placementPlan.Any(pose => pose.ItemStandContents != null)) return false;
            try
            {
                if (runtime?.CanCreateFromPlacement != true) throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
                var poses = placementPlan.Select(pose => ProjectPose(pose.Position, pose.Rotation, pose.Scale)).ToArray();
                if (placementProjectId == Guid.Empty) placementProjectId = Guid.NewGuid();
                var record = runtime.Create(placementProjectId, activeBlueprint,
                    ProjectPose(blueprintRootPosition, blueprintRootRotation, Vector3.one), poses);
                LastCreatedProject = record.Id;
                placementProjectId = Guid.Empty;
                Finish(); suppressPlacementFrame = Time.frameCount;
                runtime.StartCreated(record.Id);
                ShowStatus(player, runtime.Board(record.Id) ? BuildWorksLocalization.Text("project.created") :
                    BuildWorksLocalization.Text("project.saved_error", record.Id.ToString("N"), runtime.Failure));
            }
            catch (Exception error)
            {
                suppressPlacementFrame = Time.frameCount;
                ShowStatus(player, BuildWorksLocalization.Text("project.error", error.Message));
                Debug.LogWarning("BuildWorks project confirmation stopped before native placement: " + error.Message);
            }
            return true;
        }
        private static Store.Pose ProjectPose(Vector3 p, Quaternion q, Vector3 scale) =>
            new Store.Pose(new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w, scale.x, scale.y, scale.z });

        internal bool BeginProjectPart(BlueprintConstructionPartExecution execution)
        {
            if (execution == null || !CanOpenConstructionProject) return false;
            var player = Player.m_localPlayer;
            Piece returnPiece = player.GetSelectedPiece();
            try
            {
                ClearLayout(); projectPart = execution; sessionMode = SessionMode.ProjectPart;
                blueprintPalettePiece = returnPiece;
                placementPlan.Add(execution.Pose); placementPlanPieces.Add(execution.Piece); placementSupplies.Add(null);
                if (!SelectPlanPiece(player, 0)) throw new InvalidOperationException("Native construction piece could not be selected.");
                currentPosition = execution.Pose.Position; currentRotation = execution.Pose.Rotation;
                state = PlacementState.Armed; requestAutomaticPlacement = true;
                dragAxis = GizmoAxis.None; dragHandle = GizmoHandleKind.None;
                RestoreCursor(); return true;
            }
            catch (Exception error)
            { execution.StopBeforeNative(error.Message); projectPart = null; Finish(); return false; }
        }
    }
}
