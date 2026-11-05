using System;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class PrecisionPlacementSession
    {
        private PendingPlacementOrder supportOrder;
        private bool selectingPlacementSupport;
        private int supportSelectionFrame;
        private bool supportCandidateNeedsSelection;
        private Action<int, string> supportProjectResult;
        private int PlacementResolvedCount => supportOrder?.ResolvedCount ?? placementPlanIndex;
        internal bool BlocksPlacementForSupport(Player player) => player == Player.m_localPlayer &&
            (selectingPlacementSupport || supportOrder != null && !supportOrder.Ready);

        private bool ResumeAutomaticPlacement(Player player)
        {
            automaticPlacementPaused = false;
            automaticPauseReason = null;
            RestoreCursor();
            if (supportOrder == null) return true;
            if (!BeginSupportSelection(player))
                PauseAutomaticPlacement(player, BuildWorksLocalization.Text("placement.pause.validation"));
            return false;
        }

        internal bool BeginProjectSupportSelection(Piece[] pieces, TransformSnapshot[] poses, bool[] resolved,
            Action<int, string> result)
        {
            if (!CanOpenConstructionProject || result == null || pieces == null || poses == null || resolved == null ||
                poses.Length == 0 || pieces.Length != poses.Length || poses.Length != resolved.Length)
                return false;
            var order = new PendingPlacementOrder(resolved);
            if (order.Complete) return false;
            Player player = Player.m_localPlayer;
            activeBlueprint = null;
            ClearLayout();
            supportProjectResult = result;
            blueprintPalettePiece = player.GetSelectedPiece();
            sessionMode = SessionMode.ProjectPart;
            state = PlacementState.Editing;
            placementPlan.AddRange(poses);
            placementPlanPieces.AddRange(pieces);
            supportOrder = order;
            if (!BeginSupportSelection(player))
            { supportProjectResult = null; Finish(); return false; }
            return true;
        }

        private bool BeginSupportSelection(Player player)
        {
            if (supportOrder == null || nativeConstructionPayment != null || pendingItemStandContents != null)
                return false;
            supportOrder.RestartPass();
            selectingPlacementSupport = true;
            requestAutomaticPlacement = false;
            supportCandidateNeedsSelection = true;
            supportSelectionFrame = Time.frameCount;
            return true;
        }

        private void SelectSupportCandidate(Player player)
        {
            placementPlanIndex = supportOrder.Current;
            if (placementPlanIndex < 0 || !SelectPlanPiece(player, placementPlanIndex))
            { StopSupportSelection(player, BuildWorksLocalization.Text("placement.first_piece_unavailable")); return; }
            TransformSnapshot pose = placementPlan[placementPlanIndex];
            currentPosition = pose.Position;
            currentRotation = pose.Rotation;
            ApplyEditingGhostTransform();
            // Native Setup schedules component destruction; validate on the following frame.
            supportSelectionFrame = Time.frameCount;
        }

        private void TickSupportSelection(Player player)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(BuildWorksPlugin.TogglePrecisionKey.Value))
            { Cancel(); return; }
            if (!player || player.IsDead() || !HasUsableBuildTool(player) ||
                (supportProjectResult == null || !supportCandidateNeedsSelection) && !ContextStillValid(player))
            { Cancel(restoreCursor: player && GameplayInputAvailable(player)); return; }
            if (!GameplayInputAvailable(player) || Time.frameCount <= supportSelectionFrame) return;
            if (supportCandidateNeedsSelection)
            {
                // Selection may have been left on the blueprint carrier after confirmation.
                supportCandidateNeedsSelection = false;
                SelectSupportCandidate(player);
                return;
            }
            bool noCost = (bool)NoPlacementCostField.GetValue(player);
            if (!noCost && !player.HaveRequirements(selectedPiece, Player.RequirementMode.CanBuild))
            { StopSupportSelection(player, RequirementsPauseReason(player)); return; }
            bool valid = ValidateCurrentPlacement(player);
            if (valid && LastPlacementContactFailure == PlacementContactFailure.None && PlacementSelectionStillValid(player))
            {
                supportOrder.AcceptContact();
                selectingPlacementSupport = false;
                if (supportProjectResult != null)
                {
                    var result = supportProjectResult; supportProjectResult = null;
                    int index = placementPlanIndex;
                    Finish();
                    result(index, null);
                    return;
                }
                state = PlacementState.Armed;
                requestAutomaticPlacement = true;
                return;
            }
            if (player.GetPlacementStatus() == Player.PlacementStatus.BlockedbyPlayer)
            {
                if (supportProjectResult != null)
                { StopSupportSelection(player, BuildWorksLocalization.Text("placement.pause.validation")); return; }
                selectingPlacementSupport = false;
                state = PlacementState.Armed;
                requestAutomaticPlacement = true;
                TrySkipPlayerBlockedPlacement(player);
                return;
            }
            if (player.GetPlacementStatus() == Player.PlacementStatus.NoRayHits &&
                LastPlacementContactFailure == PlacementContactFailure.NoContact)
            {
                Debug.Log("BuildWorks deferred unsupported original part " + placementPlanIndex + ".");
                if (supportOrder.DeferContact())
                { supportCandidateNeedsSelection = true; supportSelectionFrame = Time.frameCount; }
                else StopSupportSelection(player, BuildWorksLocalization.Text("placement.pause.support_missing"));
                return;
            }
            Debug.LogWarning("BuildWorks support selection stopped at original part " + placementPlanIndex +
                "; status=" + player.GetPlacementStatus() + "; contact=" + LastPlacementContactFailure);
            StopSupportSelection(player, BuildWorksLocalization.Text("placement.pause.validation"));
        }

        private void StopSupportSelection(Player player, string reason)
        {
            selectingPlacementSupport = false;
            if (supportProjectResult != null)
            {
                var result = supportProjectResult; supportProjectResult = null;
                Finish();
                ShowStatus(player, reason);
                result(-1, reason);
                return;
            }
            if (supportOrder.ResolvedCount == 0)
            {
                supportOrder = null;
                ReopenEditor(player, reason, preserveLayout: true);
                return;
            }
            supportOrder.RestartPass();
            placementPlanIndex = supportOrder.Current;
            if (placementPlanIndex >= 0)
            {
                currentPosition = placementPlan[placementPlanIndex].Position;
                currentRotation = placementPlan[placementPlanIndex].Rotation;
            }
            PauseAutomaticPlacement(player, reason);
        }
    }
}
