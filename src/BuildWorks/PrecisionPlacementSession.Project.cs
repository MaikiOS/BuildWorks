using System;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class PrecisionPlacementSession
    {
        private BlueprintConstructionPartExecution projectPart;
        // shortcut: structural project caller first; special surfaces and contents need their existing separate workflows.
        internal static bool SupportsProjectPart(Piece piece) => SupportsPrecisionPlacement(piece) &&
            piece.GetComponentsInChildren<TerrainModifier>(true).Length == 0 && !piece.GetComponent<Container>() &&
            !piece.GetComponent<ItemStand>() && !piece.GetComponent<ArmorStand>();

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
