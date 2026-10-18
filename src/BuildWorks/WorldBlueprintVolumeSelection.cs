#if BUILDWORKS_TEST_STAND
using Input = OstrixMods.BuildWorks.BuildWorksQaInput;
#endif
using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class PrecisionPlacementSession
    {
        private readonly WorldSelectionVolumeView blueprintVolumeView = new WorldSelectionVolumeView();
        private readonly HashSet<Piece> blueprintVolumeIncluded = new HashSet<Piece>();
        private readonly HashSet<Piece> blueprintVolumeExcluded = new HashSet<Piece>();
        private readonly List<Piece> blueprintVolumeRows = new List<Piece>();
        private (string name, bool selected)[] blueprintVolumeHudItems = Array.Empty<(string, bool)>();
        private bool blueprintVolumeMode, blueprintVolumeReady, blueprintVolumeDirty;
        private bool blueprintVolumeDragValid;
        private Vector3 blueprintVolumeFirst, blueprintVolumeSecond, blueprintVolumeDragFirst;
        private float blueprintVolumePlane, blueprintVolumeBottom, blueprintVolumeTop;
        private string blueprintVolumeStatus;

        private void BeginBlueprintVolumeSelection()
        {
            ClearBlueprintVolumeSelection();
            blueprintVolumeMode = true;
            blueprintVolumePlane = currentPosition.y;
            blueprintVolumeBottom = -2f;
            blueprintVolumeTop = 20f;
            UpdateBlueprintVolumeHud();
        }

        private void ClearBlueprintVolumeSelection()
        {
            blueprintVolumeMode = blueprintVolumeReady = blueprintVolumeDirty = false;
            blueprintVolumeDragValid = false;
            blueprintVolumeIncluded.Clear();
            blueprintVolumeExcluded.Clear();
            blueprintVolumeRows.Clear();
            blueprintVolumeHudItems = Array.Empty<(string, bool)>();
            blueprintVolumeStatus = null;
            blueprintVolumeView.Hide();
        }

        private void ToggleBlueprintVolumeMode()
        {
            CancelBlueprintSelectionDrag();
            blueprintVolumeMode = !blueprintVolumeMode;
            blueprintVolumeReady = blueprintVolumeDirty = false;
            blueprintVolumeIncluded.Clear();
            blueprintVolumeIncluded.UnionWith(blueprintSelection.Where(p => p));
            blueprintVolumeExcluded.Clear();
            blueprintVolumeRows.Clear();
            blueprintVolumeStatus = null;
            blueprintVolumeView.Hide();
            UpdateBlueprintVolumeHud();
        }

        private WorldSelectionVolume BlueprintVolume(Vector3 first, Vector3 second) =>
            new WorldSelectionVolume(new Point3(first.x, blueprintVolumePlane + blueprintVolumeBottom, first.z),
                new Point3(second.x, blueprintVolumePlane + blueprintVolumeTop, second.z));

        private void ShowBlueprintVolume(WorldSelectionVolume volume) =>
            blueprintVolumeView.Show(ToUnity(volume.Minimum), ToUnity(volume.Maximum));

        private void AdjustBlueprintVolumeHeight(float delta, bool top)
        {
            if (!selectingBlueprint || !blueprintVolumeMode || float.IsNaN(delta) || float.IsInfinity(delta)) return;
            float previous = top ? blueprintVolumeTop : blueprintVolumeBottom;
            float next = Mathf.Clamp(previous + delta, -512f, 512f);
            if (next == previous) return;
            if (top) blueprintVolumeTop = next;
            else blueprintVolumeBottom = next;
            blueprintVolumeDirty = blueprintVolumeReady;
            blueprintVolumeStatus = null;
            if (blueprintVolumeReady && blueprintVolumeBottom <= blueprintVolumeTop)
                ShowBlueprintVolume(BlueprintVolume(blueprintVolumeFirst, blueprintVolumeSecond));
            else blueprintVolumeView.Hide();
            UpdateBlueprintVolumeHud(refreshItems: false);
        }

        private void CompleteBlueprintVolumeHeightEdit()
        {
            if (blueprintVolumeDirty) RefreshBlueprintVolumeSelection();
        }

        private bool TryBlueprintAreaPoint(Camera camera, Vector2 mouse, out Vector3 point)
        {
            Ray ray = camera.ScreenPointToRay(mouse);
            var plane = new Plane(Vector3.up, new Vector3(0, blueprintVolumePlane, 0));
            point = Vector3.zero;
            if (!plane.Raycast(ray, out float distance) || distance < 0 || distance > 512f) return false;
            point = ray.GetPoint(distance);
            return IsFinite(point);
        }

        private void CancelBlueprintSelectionDrag()
        {
            blueprintSelectionDragging = false;
            blueprintVolumeDragValid = false;
            hud.HideSelectionBox();
            if (blueprintVolumeMode && blueprintVolumeReady && blueprintVolumeBottom <= blueprintVolumeTop)
                ShowBlueprintVolume(BlueprintVolume(blueprintVolumeFirst, blueprintVolumeSecond));
            else blueprintVolumeView.Hide();
        }

        private void HandleBlueprintVolumeInput(Camera camera, Vector2 mouse)
        {
            if (Input.GetMouseButtonDown(0) && !MouseOverEditorPanel(mouse))
            {
                blueprintVolumeDragValid = TryBlueprintAreaPoint(camera, mouse, out blueprintVolumeDragFirst);
                blueprintSelectionDragging = true;
                blueprintSelectionStart = mouse;
                suppressPlacementFrame = Time.frameCount;
            }
            if (!blueprintSelectionDragging) return;
            bool draggingArea = (mouse - blueprintSelectionStart).sqrMagnitude >= 64f;
            if (Input.GetMouseButton(0) && draggingArea && blueprintVolumeDragValid && TryBlueprintAreaPoint(camera, mouse, out Vector3 preview))
                ShowBlueprintVolume(BlueprintVolume(blueprintVolumeDragFirst, preview));
            if (!Input.GetMouseButtonUp(0)) return;
            blueprintSelectionDragging = false;
            if (MouseOverEditorPanel(mouse)) { CancelBlueprintSelectionDrag(); return; }
            if (!draggingArea)
            {
                CancelBlueprintSelectionDrag();
                if (TryGetPlacedPieceAt(camera, mouse, out Piece piece, out _) && IsBlueprintPieceSelectable(piece))
                    ToggleBlueprintVolumePiece(piece, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
                return;
            }
            if (!blueprintVolumeDragValid || !TryBlueprintAreaPoint(camera, mouse, out Vector3 end))
            {
                CancelBlueprintSelectionDrag();
                ShowStatus(Player.m_localPlayer, BuildWorksLocalization.Text("selection.area_ray_failed"));
                return;
            }
            blueprintVolumeFirst = blueprintVolumeDragFirst;
            blueprintVolumeSecond = end;
            blueprintVolumeDragValid = false;
            blueprintVolumeReady = blueprintVolumeDirty = true;
            blueprintVolumeStatus = null;
            RefreshBlueprintVolumeSelection();
        }

        private void ToggleBlueprintVolumeRow(int row)
        {
            if (row >= 0 && row < blueprintVolumeRows.Count && blueprintVolumeRows[row])
                ToggleBlueprintVolumePiece(blueprintVolumeRows[row], false);
        }

        private void ToggleBlueprintVolumePiece(Piece piece, bool remove)
        {
            if (!blueprintVolumeReady || blueprintVolumeDirty)
            {
                ShowStatus(Player.m_localPlayer, BuildWorksLocalization.Text("selection.refresh_required"));
                return;
            }
            bool wasIncluded = blueprintVolumeIncluded.Contains(piece);
            bool wasExcluded = blueprintVolumeExcluded.Contains(piece);
            if (remove || blueprintSelection.Contains(piece))
            { blueprintVolumeIncluded.Remove(piece); blueprintVolumeExcluded.Add(piece); }
            else { blueprintVolumeExcluded.Remove(piece); blueprintVolumeIncluded.Add(piece); }
            if (RefreshBlueprintVolumeSelection()) return;
            if (wasIncluded) blueprintVolumeIncluded.Add(piece); else blueprintVolumeIncluded.Remove(piece);
            if (wasExcluded) blueprintVolumeExcluded.Add(piece); else blueprintVolumeExcluded.Remove(piece);
        }

        private bool RefreshBlueprintVolumeSelection()
        {
            if (!selectingBlueprint || !blueprintVolumeMode || !blueprintVolumeReady) return false;
            if (blueprintVolumeBottom > blueprintVolumeTop)
            {
                blueprintVolumeStatus = BuildWorksLocalization.Text("selection.height_invalid");
                UpdateBlueprintVolumeHud();
                return false;
            }
            WorldSelectionVolume volume = BlueprintVolume(blueprintVolumeFirst, blueprintVolumeSecond);
            if (!TryBlueprintVolumeZones(volume, out int unloaded))
            {
                blueprintVolumeStatus = BuildWorksLocalization.Text("selection.zone_limit");
                UpdateBlueprintVolumeHud();
                return false;
            }
            nearbyPieces.Clear();
            Piece.GetAllPiecesInRadius(ToUnity(volume.Center), (float)volume.QueryRadius, nearbyPieces);
            List<Piece> loaded = nearbyPieces.Where(IsBlueprintPieceSelectable).ToList();
            Piece[] included = blueprintVolumeIncluded.Where(IsBlueprintPieceSelectable).ToArray();
            if (!volume.TrySelect(loaded, p => ToGeometry(p.transform.position), included,
                    blueprintVolumeExcluded, CompositeBlueprintStore.EditorPartBudget, out List<Piece> selected))
            {
                blueprintVolumeStatus = BuildWorksLocalization.Text("selection.limit", CompositeBlueprintStore.EditorPartBudget);
                UpdateBlueprintVolumeHud();
                return false;
            }
            if (!TryCommitBlueprintVolumeSelection(selected, out string error))
            {
                blueprintVolumeStatus = error;
                UpdateBlueprintVolumeHud();
                return false;
            }
            blueprintVolumeRows.Clear();
            var seen = new HashSet<Piece>();
            foreach (Piece piece in loaded.Concat(included).Concat(blueprintVolumeExcluded.Where(IsBlueprintPieceSelectable)))
                if ((volume.Contains(ToGeometry(piece.transform.position)) || blueprintVolumeIncluded.Contains(piece) ||
                        blueprintVolumeExcluded.Contains(piece)) && seen.Add(piece))
                    blueprintVolumeRows.Add(piece);
            blueprintVolumeDirty = false;
            blueprintVolumeStatus = BuildWorksLocalization.Text("selection.loaded_status", unloaded, selected.Count);
            ShowBlueprintVolume(volume);
            UpdateBlueprintVolumeHud();
            return true;
        }

        private bool TryCommitBlueprintVolumeSelection(List<Piece> selected, out string error)
        {
            error = null;
            if (selected == null || selected.Count > CompositeBlueprintStore.EditorPartBudget)
            {
                error = BuildWorksLocalization.Text("selection.limit", CompositeBlueprintStore.EditorPartBudget);
                return false;
            }
#if BUILDWORKS_TEST_STAND
            if (selected.Count > CompositeBlueprintStore.InteractivePartBudget)
            {
                try { BuildWorksTestIsolation.RequireLoadedSession(); }
                catch (Exception failure)
                {
                    error = BuildWorksLocalization.Text("qa.large_selection_blocked", failure.Message);
                    return false;
                }
            }
#endif
            var next = new HashSet<Piece>(selected);
            var previous = new HashSet<Piece>(blueprintSelection);
            foreach (Piece piece in blueprintSelection)
                if (!next.Contains(piece)) SetBlueprintPieceHighlight(piece, false);
            foreach (Piece piece in selected)
                if (!previous.Contains(piece)) SetBlueprintPieceHighlight(piece, true);
            blueprintSelection.Clear();
            blueprintSelection.AddRange(selected);
            return true;
        }

        private static bool TryBlueprintVolumeZones(WorldSelectionVolume volume, out int unloaded)
        {
            unloaded = 0;
            if (!ZoneSystem.instance) return false;
            double minX = Math.Floor((volume.Minimum.X + 32) / 64), maxX = Math.Floor((volume.Maximum.X + 32) / 64);
            double minZ = Math.Floor((volume.Minimum.Z + 32) / 64), maxZ = Math.Floor((volume.Maximum.Z + 32) / 64);
            if (minX < short.MinValue || maxX > short.MaxValue || minZ < short.MinValue || maxZ > short.MaxValue ||
                (maxX - minX + 1) * (maxZ - minZ + 1) > 4096) return false;
            var first = ZoneSystem.GetZone(ToUnity(volume.Minimum));
            var last = ZoneSystem.GetZone(ToUnity(volume.Maximum));
            for (int x = first.x; x <= last.x; ++x)
                for (int z = first.y; z <= last.y; ++z)
                    if (!ZoneSystem.instance.IsZoneLoaded(new Vector2s(x, z))) ++unloaded;
            return true;
        }

        private void UpdateBlueprintVolumeHud(bool refreshItems = true)
        {
            if (refreshItems)
            {
                var selected = new HashSet<Piece>(blueprintSelection);
                blueprintVolumeHudItems = blueprintVolumeRows.Select(p => (p ? Localization.instance?.Localize(p.m_name) ?? p.m_name : "?",
                    selected.Contains(p))).ToArray();
            }
            hud.SetWorldSelectionState(blueprintVolumeMode, blueprintVolumeBottom, blueprintVolumeTop,
                BuildWorksLocalization.Text("selection.plane", blueprintVolumePlane) + "\n" +
                (blueprintVolumeStatus ?? BuildWorksLocalization.Text(blueprintVolumeDirty ?
                    "selection.refresh_required" : "selection.area_hint")),
                blueprintVolumeHudItems);
        }
    }
}
