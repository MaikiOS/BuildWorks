using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using AnchorAdjustment = OstrixMods.BuildWorks.Geometry.AnchorAdjustment;
using AnchorBounds = OstrixMods.BuildWorks.Geometry.AnchorBounds;
using Point3 = OstrixMods.BuildWorks.Geometry.Point3;

namespace OstrixMods.BuildWorks
{
    internal sealed class HammerBlueprintPieceRegistry : IDisposable
    {
        internal const string PrefabPrefix = "BuildWorks_Blueprint_";
        private static readonly FieldInfo AvailablePiecesField = typeof(PieceTable).GetField(
            "m_availablePiecesByCategory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdateAvailablePiecesMethod = typeof(Player).GetMethod(
            "UpdateAvailablePiecesList",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo SetupPlacementGhostMethod = typeof(Player).GetMethod(
            "SetupPlacementGhost",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly CompositeBlueprintStore store;
        private readonly BlueprintThumbnailRenderer thumbnailRenderer =
            new BlueprintThumbnailRenderer();
        private readonly Dictionary<Piece, CompositeBlueprintStore.Blueprint> blueprintsByPiece =
            new Dictionary<Piece, CompositeBlueprintStore.Blueprint>();
        private readonly Dictionary<Piece, Vector3> placementOriginsByPiece =
            new Dictionary<Piece, Vector3>();
        private readonly Dictionary<string, Piece> piecesByBlueprintId =
            new Dictionary<string, Piece>(StringComparer.Ordinal);
        private readonly Dictionary<string, IReadOnlyList<Piece>> sourcePiecesByBlueprintId =
            new Dictionary<string, IReadOnlyList<Piece>>(StringComparer.Ordinal);
        private readonly List<GameObject> prefabs = new List<GameObject>();
        private readonly Dictionary<Piece, Bounds> sourceBounds = new Dictionary<Piece, Bounds>();
        private GameObject holder;
        private GameObject categoryMarker;
        private PieceTable registeredTable;
        private string registeredSignature = string.Empty;
        private int registeredRevision = -1, registeredPieceCount = -1;
        private string registeredAvailability;

        public HammerBlueprintPieceRegistry(CompositeBlueprintStore store)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public void Update(Player player)
        {
            if (!UnifiedHammerCatalog.BlueprintCategoryReady)
            {
                if (registeredTable)
                {
                    Detach();
                    thumbnailRenderer.Dispose();
                }
                return;
            }
            ProcessThumbnail(out _, out _);
            PieceTable table = PrecisionPlacementSession.GetBuildPieceTable(player);
            string availability = HammerCatalogOrganizer.AvailabilitySignature(table);
            if (table == registeredTable && store.Revision == registeredRevision &&
                (table ? table.m_pieces.Count : 0) == registeredPieceCount && availability == registeredAvailability) return;
            string signature = BuildSignature(store.All(), table);
            if (table == registeredTable && signature == registeredSignature)
            {
                registeredRevision = store.Revision;
                registeredPieceCount = table ? table.m_pieces.Count : 0;
                registeredAvailability = availability;
                return;
            }

            string selectedBlueprintId = null;
            Piece selectedPiece = player ? player.GetSelectedPiece() : null;
            if (selectedPiece && blueprintsByPiece.TryGetValue(
                selectedPiece,
                out CompositeBlueprintStore.Blueprint selectedBlueprint))
            {
                selectedBlueprintId = selectedBlueprint.id;
            }

            Detach();
            if (!table || !PrecisionPlacementSession.UsesHammerPieceTable(player)) return;

            holder = new GameObject("BuildWorks_BlueprintPieces")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(holder);
            holder.SetActive(false);

            categoryMarker = new GameObject(PrefabPrefix + "CategoryMarker")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            categoryMarker.transform.SetParent(holder.transform, false);
            Piece markerPiece = categoryMarker.AddComponent<Piece>();
            markerPiece.m_name = BuildWorksLocalization.Token("catalog.category.blueprints");
            markerPiece.m_category = UnifiedHammerCatalog.BlueprintCategory;
            markerPiece.m_enabled = false;
            markerPiece.m_resources = Array.Empty<Piece.Requirement>();
            table.m_pieces.Add(categoryMarker);

            Piece selectedReplacement = null;
            foreach (CompositeBlueprintStore.Blueprint blueprint in store.All())
            {
                if (!PrecisionPlacementSession.TryResolveBlueprintPieces(
                    player,
                    blueprint,
                    out List<Piece> pieces,
                    out string missingPrefab))
                {
                    Debug.LogWarning("BuildWorks hid blueprint " + blueprint.name +
                        ": prefab is unavailable in this Hammer: " + missingPrefab);
                    continue;
                }

                GameObject prefab = CreatePrefab(
                    blueprint,
                    pieces,
                    out Vector3 placementOrigin);
                Piece piece = prefab.GetComponent<Piece>();
                table.m_pieces.Add(prefab);
                prefabs.Add(prefab);
                blueprintsByPiece.Add(piece, blueprint);
                placementOriginsByPiece.Add(piece, placementOrigin);
                piecesByBlueprintId.Add(blueprint.id, piece);
                sourcePiecesByBlueprintId.Add(blueprint.id, new List<Piece>(pieces));
                if (string.Equals(
                    blueprint.id,
                    selectedBlueprintId,
                    StringComparison.Ordinal))
                {
                    selectedReplacement = piece;
                }
            }

            registeredTable = table;
            registeredSignature = signature;
            registeredRevision = store.Revision;
            registeredPieceCount = table.m_pieces.Count;
            registeredAvailability = availability;
            if (player && UpdateAvailablePiecesMethod != null)
            {
                UpdateAvailablePiecesMethod.Invoke(player, null);
                ApplyCatalogLayout(table);
            }
            else
                ApplyCatalogLayout(table);
            Debug.Log("BuildWorks registered " + piecesByBlueprintId.Count +
                " blueprint palette pieces in category " +
                (int)UnifiedHammerCatalog.BlueprintCategory + ".");
            UnifiedHammerCatalog.RefreshCategoryTabs();
            if (player)
            {
                Piece replacement = selectedReplacement ? selectedReplacement : selectedPiece;
                if (replacement && PrecisionPlacementSession.IsBuildPieceAvailable(
                    table,
                    replacement))
                {
                    player.SetSelectedPiece(replacement);
                    if (selectedReplacement && SetupPlacementGhostMethod != null)
                        SetupPlacementGhostMethod.Invoke(player, null);
                }
            }
        }

        public bool TryGetBlueprint(
            Piece piece,
            out CompositeBlueprintStore.Blueprint blueprint)
        {
            blueprint = null;
            return piece && blueprintsByPiece.TryGetValue(piece, out blueprint);
        }

        public bool TryGetPlacementOrigin(Piece piece, out Vector3 origin)
        {
            origin = Vector3.zero;
            return piece && placementOriginsByPiece.TryGetValue(piece, out origin);
        }

        public bool TryGetPiece(
            CompositeBlueprintStore.Blueprint blueprint,
            out Piece piece)
        {
            piece = null;
            return blueprint != null && piecesByBlueprintId.TryGetValue(blueprint.id, out piece);
        }

        public bool TryGetSourcePieces(
            CompositeBlueprintStore.Blueprint blueprint,
            out IReadOnlyList<Piece> pieces)
        {
            pieces = null;
            return blueprint != null && sourcePiecesByBlueprintId.TryGetValue(
                blueprint.id,
                out pieces);
        }

        public bool Select(Player player, CompositeBlueprintStore.Blueprint blueprint)
        {
            if (!player || !TryGetPiece(blueprint, out Piece piece)) return false;
            PieceTable table = PrecisionPlacementSession.GetBuildPieceTable(player);
            HammerCatalogOrganizer.EnsureVisible(table, piece);
            return SelectPiece(player, piece);
        }

        public bool SelectPiece(Player player, Piece piece)
        {
            if (!player || !piece) return false;
            if (!player.SetSelectedPiece(piece)) return false;
            SetupPlacementGhostMethod?.Invoke(player, null);
            return true;
        }

        public void SelectSafePieceBeforeDelete(
            Player player,
            CompositeBlueprintStore.Blueprint blueprint)
        {
            if (!player || blueprint == null || !TryGetBlueprint(
                player.GetSelectedPiece(),
                out CompositeBlueprintStore.Blueprint selected) ||
                selected != blueprint) return;
            PieceTable table = PrecisionPlacementSession.GetBuildPieceTable(player);
            foreach (GameObject prefab in table?.m_pieces ?? new List<GameObject>())
            {
                Piece candidate = prefab ? prefab.GetComponent<Piece>() : null;
                if (!candidate || prefab.name.StartsWith(PrefabPrefix,
                    StringComparison.Ordinal) ||
                    !PrecisionPlacementSession.IsBuildPieceAvailable(table, candidate))
                    continue;
                HammerCatalogOrganizer.EnsureVisible(table, candidate);
                player.SetSelectedPiece(candidate);
                SetupPlacementGhostMethod?.Invoke(player, null);
                return;
            }
        }

        public IReadOnlyList<ResourceInfo> Resources(
            CompositeBlueprintStore.Blueprint blueprint)
            => TrySources(blueprint, out IReadOnlyList<Piece> pieces)
                ? ResourceCosts(pieces) : Array.Empty<ResourceInfo>();

        internal static IReadOnlyList<ResourceInfo> ResourceCosts(IReadOnlyList<Piece> pieces)
        {
            var result = new List<ResourceInfo>();
            var amounts = new Dictionary<string, ResourceInfo>(StringComparer.Ordinal);
            foreach (Piece piece in pieces)
            {
                foreach (Piece.Requirement requirement in piece?.m_resources ??
                    Array.Empty<Piece.Requirement>())
                {
                    ItemDrop item = requirement?.m_resItem;
                    int amount = requirement?.GetAmount(1) ?? 0;
                    if (!item || amount <= 0) continue;
                    string key = item.gameObject.name;
                    string name = Localization.instance != null
                        ? Localization.instance.Localize(item.m_itemData.m_shared.m_name)
                        : item.m_itemData.m_shared.m_name;
                    Sprite icon = item.m_itemData.GetIcon();
                    amounts.TryGetValue(key, out ResourceInfo existing);
                    bool craft = piece.FreeBuildKey() == GlobalKeys.NoCraftCost;
                    amounts[key] = new ResourceInfo(name, icon, existing.Amount + amount, item,
                        existing.BuildAmount + (craft ? 0 : amount),
                        existing.CraftAmount + (craft ? amount : 0));
                }
            }
            result.AddRange(amounts.Values);
            result.Sort((left, right) => string.Compare(
                left.Name,
                right.Name,
                StringComparison.CurrentCultureIgnoreCase));
            return result;
        }

        internal string Description(CompositeBlueprintStore.Blueprint blueprint)
            => "BuildWorks: " + BuildWorksLocalization.Text(
                "blueprint.piece.description", blueprint?.parts.Count ?? 0);

        internal bool ProcessThumbnail(out string id, out Sprite thumbnail)
        {
            if (!thumbnailRenderer.ProcessOne(out id, out thumbnail) || !thumbnail) return false;
            if (piecesByBlueprintId.TryGetValue(id, out Piece iconPiece)) iconPiece.m_icon = thumbnail;
            HammerCatalogView.UpdateThumbnail(id, thumbnail);
            return true;
        }

        private bool TrySources(CompositeBlueprintStore.Blueprint blueprint, out IReadOnlyList<Piece> pieces)
        {
            pieces = null;
            if (blueprint == null) return false;
            if (sourcePiecesByBlueprintId.TryGetValue(blueprint.id, out pieces)) return true;
            if (!blueprint.IsExternal || !PrecisionPlacementSession.TryResolveBlueprintPieces(
                Player.m_localPlayer, blueprint, out List<Piece> resolved, out _, forEditor: true)) return false;
            pieces = resolved;
            sourcePiecesByBlueprintId[blueprint.id] = pieces;
            return true;
        }

        internal Sprite Icon(CompositeBlueprintStore.Blueprint blueprint)
        {
            Sprite cached = thumbnailRenderer.GetCached(blueprint);
            if (cached) return cached;
            if (TryGetPiece(blueprint, out Piece piece)) return piece.m_icon;
            return TrySources(blueprint, out IReadOnlyList<Piece> pieces)
                ? thumbnailRenderer.GetOrQueue(blueprint, pieces) : null;
        }

        public Sprite RefreshThumbnail(
            CompositeBlueprintStore.Blueprint blueprint, out string error)
        {
            error = null;
            if (!TrySources(blueprint, out IReadOnlyList<Piece> pieces)) return null;
            Sprite sprite = thumbnailRenderer.Refresh(blueprint, pieces, out error);
            if (sprite && piecesByBlueprintId.TryGetValue(
                blueprint.id,
                out Piece iconPiece))
            {
                iconPiece.m_icon = sprite;
                HammerCatalogView.Invalidate();
            }
            return sprite;
        }

        public BlueprintThumbnailRenderer.PreviewSession BeginPreview(
            CompositeBlueprintStore.Blueprint blueprint, out string error)
        {
            error = null;
            return TrySources(blueprint, out IReadOnlyList<Piece> pieces)
                ? thumbnailRenderer.BeginPreview(blueprint, pieces, out error)
                : null;
        }

        public void DeleteThumbnail(string blueprintId) =>
            thumbnailRenderer.Delete(blueprintId);

        public void ApplyCatalogLayout(PieceTable table)
        {
            if (!table || table != registeredTable || AvailablePiecesField == null)
            {
                return;
            }

            var available = AvailablePiecesField.GetValue(table) as List<List<Piece>>;
            int categoryIndex = (int)UnifiedHammerCatalog.BlueprintCategory;
            if (available == null || categoryIndex < 0) return;
            while (available.Count <= categoryIndex) available.Add(new List<Piece>());
            if (available[categoryIndex] == null)
                available[categoryIndex] = new List<Piece>();

            List<Piece> pieces = available[categoryIndex];
            Piece markerPiece = categoryMarker
                ? categoryMarker.GetComponent<Piece>()
                : null;
            if (markerPiece) pieces.Remove(markerPiece);
            foreach (GameObject prefab in prefabs)
            {
                Piece blueprintPiece = prefab ? prefab.GetComponent<Piece>() : null;
                if (!blueprintPiece) continue;
                pieces.Remove(blueprintPiece);
            }

            int insertAt = pieces.FindIndex(piece =>
                HammerCatalogOrganizer.Group(piece) == HammerCatalogOrganizer.ActionsGroup);
            if (insertAt >= 0)
            {
                Piece repairPiece = pieces[insertAt];
                pieces.RemoveAt(insertAt);
                pieces.Insert(0, repairPiece);
                insertAt = 1;
            }
            else
                insertAt = 0;

            foreach (GameObject prefab in prefabs)
            {
                Piece blueprintPiece = prefab ? prefab.GetComponent<Piece>() : null;
                if (!blueprintPiece) continue;
                table.m_availablePieces.Add(blueprintPiece);
                pieces.Insert(insertAt++, blueprintPiece);
            }
        }

        public void Dispose()
        {
            Detach();
            thumbnailRenderer.Dispose();
        }

        private GameObject CreatePrefab(
            CompositeBlueprintStore.Blueprint blueprint,
            IReadOnlyList<Piece> pieces,
            out Vector3 placementOrigin)
        {
            bool supportFrame = BlueprintPlacementFrame.TryResolve(blueprint,
                out placementOrigin, out Quaternion placementRotation);
            GameObject prefab = new GameObject(PrefabPrefix + blueprint.id)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = LayerMask.NameToLayer("piece")
            };
            prefab.transform.SetParent(holder.transform, false);

            Piece piece = prefab.AddComponent<Piece>();
            piece.m_name = blueprint.name;
            piece.m_description = "BuildWorks: " + BuildWorksLocalization.Text(
                "blueprint.piece.description", blueprint.parts.Count);
            piece.m_category = UnifiedHammerCatalog.BlueprintCategory;
            piece.m_usage = Piece.UsageTagFlags.Misc;
            piece.m_canRotate = true;
            piece.m_canBeRemoved = false;
            piece.m_allowedInDungeons = true;
            piece.m_resources = Array.Empty<Piece.Requirement>();
            piece.m_icon = thumbnailRenderer.GetOrQueue(blueprint, pieces) ??
                FirstPieceIcon(pieces);

            bool heavy = pieces.Count > CompositeBlueprintStore.InteractivePartBudget;
            int frameIndex = CompositeBlueprintStore.FramePartIndex(blueprint);
            for (int index = 0; index < pieces.Count; ++index)
            {
                if (heavy && index != frameIndex) continue;
                GameObject visual = PlacementGhostPreviewView.CreateVisualClone(
                    pieces[index].gameObject,
                    "Part_" + index,
                    prefab.layer);
                visual.transform.SetParent(prefab.transform, false);
                visual.transform.localPosition = blueprint.parts[index].position.ToVector3();
                visual.transform.localRotation = blueprint.parts[index].rotation.ToQuaternion();
                visual.transform.localScale = Vector3.Scale(pieces[index].transform.localScale,
                    blueprint.parts[index].scale.ToVector3());
                visual.SetActive(true);
            }

            Bounds visualBounds;
            if (heavy ? TryGetBlueprintFrameBounds(blueprint, pieces, out visualBounds) :
                TryGetLocalVisualBounds(prefab, out visualBounds))
            {
                Bounds frameBounds = visualBounds;
                if (!heavy && CompositeBlueprintStore.HasExplicitFrame(blueprint))
                {
                    bool initialized = false;
                    for (int index = 0; index < blueprint.parts.Count; ++index)
                    {
                        if (!CompositeBlueprintStore.IsFramePart(blueprint, index)) continue;
                        Transform child = prefab.transform.GetChild(index);
                        if (!TryGetLocalVisualBounds(child.gameObject, out Bounds childBounds)) continue;
                        Bounds transformed = TransformBounds(prefab.transform, child, childBounds);
                        if (!initialized) { frameBounds = transformed; initialized = true; }
                        else frameBounds.Encapsulate(transformed);
                    }
                }
                if (!supportFrame)
                    placementOrigin = new Vector3(
                        frameBounds.center.x,
                        frameBounds.min.y,
                        frameBounds.center.z);
                Bounds localBounds = BlueprintPlacementFrame.RebaseVisuals(
                    prefab.transform, frameBounds, placementOrigin, placementRotation);

                var placementBounds = new GameObject(
                    "BuildWorks_PlacementBounds",
                    typeof(BoxCollider))
                {
                    hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave,
                    // Player.SetupPlacementGhost only keeps colliders from the normal
                    // placement mask enabled, then moves the clone to the ghost layer.
                    layer = prefab.layer
                };
                placementBounds.transform.SetParent(prefab.transform, false);
                BoxCollider collider = placementBounds.GetComponent<BoxCollider>();
                collider.center = localBounds.center;
                collider.size = new Vector3(
                    Mathf.Max(localBounds.size.x, 0.05f),
                    Mathf.Max(localBounds.size.y, 0.05f),
                    Mathf.Max(localBounds.size.z, 0.05f));

                int snapIndex = 0;
                IReadOnlyList<Vector3> nativeSnapPoints = BlueprintNativeSnapPoints(blueprint, pieces);
                // With an explicit world anchor, normal Hammer placement must expose
                // that part's native points only. Bounds remain a fallback for pieces
                // such as furniture that do not provide native snap points.
                if (!CompositeBlueprintStore.HasExplicitFrame(blueprint) || nativeSnapPoints.Count == 0)
                {
                    AnchorBounds bounds = AnchorAdjustment.CreateBounds(new[]
                    {
                        new Point3(frameBounds.min.x, frameBounds.min.y, frameBounds.min.z),
                        new Point3(frameBounds.max.x, frameBounds.max.y, frameBounds.max.z)
                    });
                    for (int index = 0; index < AnchorAdjustment.AnchorCount; ++index)
                    {
                        Point3 point = bounds.Anchor(index);
                        AddSnapPoint(prefab.transform, snapIndex++, BlueprintPlacementFrame.ToLocal(
                            new Vector3((float)point.X, (float)point.Y, (float)point.Z),
                            placementOrigin, placementRotation));
                    }
                }
                foreach (Vector3 point in nativeSnapPoints)
                    AddSnapPoint(prefab.transform, snapIndex++, BlueprintPlacementFrame.ToLocal(
                        point, placementOrigin, placementRotation));
                foreach (CompositeBlueprintStore.VectorData point in blueprint.customSnapPoints ??
                    Enumerable.Empty<CompositeBlueprintStore.VectorData>())
                    AddSnapPoint(prefab.transform, snapIndex++, BlueprintPlacementFrame.ToLocal(
                        point.ToVector3(), placementOrigin, placementRotation));
            }
            else if (supportFrame)
                BlueprintPlacementFrame.RebaseVisuals(prefab.transform, default,
                    placementOrigin, placementRotation);

            return prefab;
        }

        private static IReadOnlyList<Vector3> BlueprintNativeSnapPoints(
            CompositeBlueprintStore.Blueprint blueprint,
            IReadOnlyList<Piece> pieces)
        {
            var sets = new List<IReadOnlyList<Point3>>();
            bool explicitFrame = CompositeBlueprintStore.HasExplicitFrame(blueprint);
            for (int index = 0; index < pieces.Count; ++index)
            {
                if (explicitFrame && !CompositeBlueprintStore.IsFramePart(blueprint, index))
                    continue;
                Piece piece = pieces[index];
                CompositeBlueprintStore.Part part = blueprint.parts[index];
                var native = new List<Transform>();
                piece.GetSnapPoints(native);
                var local = new List<Point3>(native.Count);
                foreach (Transform snap in native)
                {
                    if (!snap) continue;
                    Vector3 pieceLocal = Vector3.Scale(piece.transform.localScale,
                        piece.transform.InverseTransformPoint(snap.position));
                    Vector3 point = part.position.ToVector3() + part.rotation.ToQuaternion() *
                        Vector3.Scale(pieceLocal, part.scale.ToVector3());
                    if (float.IsNaN(point.x) || float.IsInfinity(point.x) ||
                        float.IsNaN(point.y) || float.IsInfinity(point.y) ||
                        float.IsNaN(point.z) || float.IsInfinity(point.z)) continue;
                    local.Add(new Point3(point.x, point.y, point.z));
                }
                sets.Add(local);
            }
            IReadOnlyList<Point3> selected = explicitFrame && sets.Count == 1
                ? sets[0]
                : AnchorAdjustment.ExternalCompositeSnapPoints(sets, tolerance: 0.0001);
            var result = new List<Vector3>(selected.Count);
            foreach (Point3 point in selected)
            {
                if (result.Count == 512) break;
                result.Add(new Vector3((float)point.X, (float)point.Y, (float)point.Z));
            }
            return result;
        }

        private bool TryGetBlueprintFrameBounds(CompositeBlueprintStore.Blueprint blueprint,
            IReadOnlyList<Piece> pieces, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            for (int index = 0; index < pieces.Count; ++index)
            {
                if (CompositeBlueprintStore.HasExplicitFrame(blueprint) &&
                    !CompositeBlueprintStore.IsFramePart(blueprint, index)) continue;
                Piece piece = pieces[index];
                if (!sourceBounds.TryGetValue(piece, out Bounds local))
                {
                    GameObject template = PlacementGhostPreviewView.CreateVisualClone(piece.gameObject,
                        "BuildWorks_BoundsProbe", piece.gameObject.layer);
                    try
                    {
                        if (!TryGetLocalVisualBounds(template, out local)) return false;
                        sourceBounds.Add(piece, local);
                    }
                    finally { template.SetActive(false); UnityEngine.Object.Destroy(template); }
                }
                var part = blueprint.parts[index];
                Matrix4x4 matrix = Matrix4x4.TRS(part.position.ToVector3(), part.rotation.ToQuaternion(),
                    Vector3.Scale(piece.transform.localScale, part.scale.ToVector3()));
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z));
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return found;
        }

        private static void AddSnapPoint(Transform parent, int index, Vector3 localPosition)
        {
            GameObject snap = new GameObject("snappoint" + index.ToString("000"))
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave,
                tag = "snappoint"
            };
            snap.transform.SetParent(parent, false);
            snap.transform.localPosition = localPosition;
        }

        private static Bounds TransformBounds(Transform root, Transform source, Bounds sourceBounds)
        {
            Bounds result = default;
            for (int corner = 0; corner < 8; ++corner)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? sourceBounds.min.x : sourceBounds.max.x,
                    (corner & 2) == 0 ? sourceBounds.min.y : sourceBounds.max.y,
                    (corner & 4) == 0 ? sourceBounds.min.z : sourceBounds.max.z);
                Vector3 local = root.InverseTransformPoint(source.TransformPoint(point));
                if (corner == 0) result = new Bounds(local, Vector3.zero);
                else result.Encapsulate(local);
            }
            return result;
        }

        private void Detach()
        {
            if (registeredTable && AvailablePiecesField != null)
            {
                var available = AvailablePiecesField.GetValue(registeredTable) as
                    List<List<Piece>>;
                if (available != null)
                {
                    foreach (List<Piece> category in available)
                    {
                        if (category == null) continue;
                        for (int index = category.Count - 1; index >= 0; --index)
                        {
                            Piece piece = category[index];
                            if (piece && blueprintsByPiece.ContainsKey(piece))
                                category.RemoveAt(index);
                        }
                    }
                }
            }
            if (registeredTable && registeredTable.m_pieces != null)
            {
                foreach (GameObject prefab in prefabs)
                    if (prefab)
                    {
                        Piece piece = prefab.GetComponent<Piece>();
                        if (piece) registeredTable.m_availablePieces.Remove(piece);
                        registeredTable.m_pieces.Remove(prefab);
                    }
                if (categoryMarker) registeredTable.m_pieces.Remove(categoryMarker);
            }
            prefabs.Clear();
            blueprintsByPiece.Clear();
            placementOriginsByPiece.Clear();
            piecesByBlueprintId.Clear();
            sourcePiecesByBlueprintId.Clear();
            sourceBounds.Clear();
            if (holder) UnityEngine.Object.Destroy(holder);
            holder = null;
            categoryMarker = null;
            registeredTable = null;
            registeredSignature = string.Empty;
        }

        internal static bool TryGetLocalVisualBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer = filter ? filter.GetComponent<Renderer>() : null;
                if (!filter || !filter.sharedMesh || !renderer || !renderer.enabled ||
                    !IsActiveWithin(root.transform, filter.transform)) continue;
                Bounds meshBounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                        (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                        (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                    Vector3 local = root.transform.InverseTransformPoint(
                        filter.transform.TransformPoint(point));
                    if (!found)
                    {
                        bounds = new Bounds(local, Vector3.zero);
                        found = true;
                    }
                    else
                    {
                        bounds.Encapsulate(local);
                    }
                }
            }
            return found;
        }

        private static bool IsActiveWithin(Transform root, Transform child)
        {
            for (Transform current = child; current; current = current.parent)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current == root) return true;
            }
            return false;
        }

        private static string BuildSignature(
            IReadOnlyList<CompositeBlueprintStore.Blueprint> blueprints,
            PieceTable table)
        {
            int blueprintCount = blueprints?.Count ?? 0;
            var signature = new StringBuilder();
            for (int index = 0; index < blueprintCount; ++index)
            {
                CompositeBlueprintStore.Blueprint blueprint = blueprints[index];
                AppendText(signature, blueprint?.id);
                AppendText(signature, blueprint?.name);
                AppendText(signature, blueprint?.prefabName);
                AppendText(signature, blueprint?.category);
                AppendText(signature, blueprint?.primaryPartId);
                AppendText(signature, blueprint?.primaryGroupId);
                AppendText(signature, blueprint?.placementSupportId);
                if (blueprint != null)
                {
                    AppendFloat(signature, blueprint.previewYaw);
                    AppendFloat(signature, blueprint.previewPitch);
                    AppendFloat(signature, blueprint.previewZoom);
                }
                int partCount = blueprint?.parts?.Count ?? 0;
                signature.Append(partCount).Append(':');
                for (int partIndex = 0; partIndex < partCount; ++partIndex)
                {
                    CompositeBlueprintStore.Part part = blueprint.parts[partIndex];
                    AppendText(signature, part?.prefabName);
                    if (part == null) continue;
                    AppendVector(signature, part.position);
                    AppendQuaternion(signature, part.rotation);
                    AppendVector(signature, part.scale);
                    AppendText(signature, part.signText);
                }
                int anchorCount = blueprint?.anchors?.Count ?? 0;
                signature.Append(anchorCount).Append(':');
                for (int anchorIndex = 0; anchorIndex < anchorCount; ++anchorIndex)
                    AppendVector(signature, blueprint.anchors[anchorIndex]);
                int customCount = blueprint?.customSnapPoints?.Count ?? 0;
                signature.Append(customCount).Append(':');
                for (int pointIndex = 0; pointIndex < customCount; ++pointIndex)
                    AppendVector(signature, blueprint.customSnapPoints[pointIndex]);
                int supportCount = blueprint?.supports?.Count ?? 0;
                signature.Append(supportCount).Append(':');
                for (int supportIndex = 0; supportIndex < supportCount; ++supportIndex)
                {
                    CompositeBlueprintStore.Support support = blueprint.supports[supportIndex];
                    AppendText(signature, support?.stableId);
                    if (support == null) continue;
                    AppendText(signature, support.name);
                    AppendVector(signature, support.position);
                    AppendQuaternion(signature, support.rotation);
                }
            }

            int catalogCount = 0;
            long catalogSum = 0;
            int catalogXor = 0;
            if (table && table.m_pieces != null)
            {
                foreach (GameObject prefab in table.m_pieces)
                {
                    if (!prefab || prefab.name.StartsWith(PrefabPrefix,
                        StringComparison.Ordinal)) continue;
                    int id = prefab.GetInstanceID();
                    ++catalogCount;
                    catalogSum += id;
                    catalogXor ^= id;
                }
            }
            return signature.Append('#').Append(catalogCount).Append(':')
                .Append(catalogSum).Append(':').Append(catalogXor).Append('#')
                .Append(HammerCatalogOrganizer.AvailabilitySignature(table)).ToString();
        }

        private static void AppendText(StringBuilder signature, string value)
        {
            value = value ?? string.Empty;
            signature.Append(value.Length).Append(':').Append(value).Append(';');
        }

        private static void AppendVector(
            StringBuilder signature,
            CompositeBlueprintStore.VectorData value)
        {
            AppendFloat(signature, value.x);
            AppendFloat(signature, value.y);
            AppendFloat(signature, value.z);
        }

        private static void AppendQuaternion(
            StringBuilder signature,
            CompositeBlueprintStore.QuaternionData value)
        {
            AppendFloat(signature, value.x);
            AppendFloat(signature, value.y);
            AppendFloat(signature, value.z);
            AppendFloat(signature, value.w);
        }

        private static void AppendFloat(StringBuilder signature, float value)
        {
            signature.Append(value.ToString("R", CultureInfo.InvariantCulture)).Append(',');
        }

        private static Sprite FirstPieceIcon(IReadOnlyList<Piece> pieces)
        {
            if (pieces == null) return null;
            foreach (Piece piece in pieces)
                if (piece && piece.m_icon) return piece.m_icon;
            return null;
        }

        internal readonly struct ResourceInfo
        {
            public ResourceInfo(string name, Sprite icon, int amount, ItemDrop item,
                int buildAmount, int craftAmount)
            {
                Name = name;
                Icon = icon;
                Amount = amount;
                Item = item;
                BuildAmount = buildAmount;
                CraftAmount = craftAmount;
            }

            public string Name { get; }
            public Sprite Icon { get; }
            public int Amount { get; }
            public ItemDrop Item { get; }
            public int BuildAmount { get; }
            public int CraftAmount { get; }
        }
    }
}
