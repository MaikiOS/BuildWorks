using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace OstrixMods.BuildWorks.Geometry
{
    public sealed class BlueprintEditorPart
    {
        public BlueprintEditorPart(
            string stableId,
            string prefabName,
            string displayName,
            Point3 position,
            Rotation3 rotation,
            string parentGroupId = null,
            bool visible = true,
            bool locked = false,
            Point3? scale = null)
        {
            StableId = Required(stableId, nameof(stableId));
            PrefabName = Required(prefabName, nameof(prefabName));
            DisplayName = Required(displayName, nameof(displayName));
            Validate(position, nameof(position));
            Position = position;
            Rotation = rotation.Normalized();
            Scale = NormalizeScale(scale ?? new Point3(1.0, 1.0, 1.0), nameof(scale));
            ParentGroupId = parentGroupId;
            Visible = visible;
            Locked = locked;
        }

        public string StableId { get; }
        public string PrefabName { get; }
        public string DisplayName { get; internal set; }
        public Point3 Position { get; internal set; }
        public Rotation3 Rotation { get; internal set; }
        public Point3 Scale { get; internal set; }
        public string ParentGroupId { get; internal set; }
        public bool Visible { get; internal set; }
        public bool Locked { get; internal set; }

        internal BlueprintEditorPart Clone() => new BlueprintEditorPart(
            StableId,
            PrefabName,
            DisplayName,
            Position,
            Rotation,
            ParentGroupId,
            Visible,
            Locked,
            Scale);

        internal static string Required(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Value is required.", name);
            return value.Trim();
        }

        internal static void Validate(Point3 value, string name)
        {
            if (!GeometryMath.IsFinite(value.X) || !GeometryMath.IsFinite(value.Y) ||
                !GeometryMath.IsFinite(value.Z))
                throw new ArgumentOutOfRangeException(name);
        }

        internal static void ValidateScale(Point3 value, string name)
        {
            Validate(value, name);
            if (value.X < BlueprintEditorDocument.MinimumScale ||
                value.Y < BlueprintEditorDocument.MinimumScale ||
                value.Z < BlueprintEditorDocument.MinimumScale ||
                value.X > BlueprintEditorDocument.MaximumScale ||
                value.Y > BlueprintEditorDocument.MaximumScale ||
                value.Z > BlueprintEditorDocument.MaximumScale)
                throw new ArgumentOutOfRangeException(name);
        }

        public static Point3 NormalizeScale(Point3 value, string name)
        {
            var normalized = new Point3(
                NormalizeScaleBoundary(value.X),
                NormalizeScaleBoundary(value.Y),
                NormalizeScaleBoundary(value.Z));
            ValidateScale(normalized, name);
            return normalized;
        }

        private static double NormalizeScaleBoundary(double value)
        {
            // Unity/Store use float. Absorb at most two float rounding units at
            // the bounds; preserve every in-range value and reject larger errors.
            const double relativeTolerance = 2.384185791015625e-7;
            double minimum = BlueprintEditorDocument.MinimumScale;
            double maximum = BlueprintEditorDocument.MaximumScale;
            if (value < minimum && value >= minimum * (1.0 - relativeTolerance))
                return minimum;
            if (value > maximum && value <= maximum * (1.0 + relativeTolerance))
                return maximum;
            return value;
        }
    }

    public sealed class BlueprintEditorGroup
    {
        public BlueprintEditorGroup(
            string stableId,
            string name,
            bool visible = true,
            bool locked = false,
            string parentGroupId = null,
            string pivotPartId = null)
        {
            StableId = BlueprintEditorPart.Required(stableId, nameof(stableId));
            Name = BlueprintEditorPart.Required(name, nameof(name));
            Visible = visible;
            Locked = locked;
            ParentGroupId = parentGroupId;
            PivotPartId = string.IsNullOrWhiteSpace(pivotPartId) ? null : pivotPartId.Trim();
        }

        public string StableId { get; }
        public string Name { get; internal set; }
        public bool Visible { get; internal set; }
        public bool Locked { get; internal set; }
        public string ParentGroupId { get; internal set; }
        public string PivotPartId { get; internal set; }

        internal BlueprintEditorGroup Clone() =>
            new BlueprintEditorGroup(StableId, Name, Visible, Locked, ParentGroupId, PivotPartId);
    }

    public sealed class BlueprintEditorDocument
    {
        public const int MaximumParts = 128;
        public const int MaximumGroups = 64;
        public const int MaximumHistory = 32;
        public const double MinimumScale = 0.01;
        public const double MaximumScale = 4.0;

        private sealed class State
        {
            public int Token;
            public string Name;
            public string Category;
            public List<BlueprintEditorPart> Parts;
            public List<BlueprintEditorGroup> Groups;
            public List<string> Selection;
            public string ActiveNodeId;
            public string PrimaryPartId;
            public string PrimaryGroupId;
        }

        private readonly List<BlueprintEditorPart> parts = new List<BlueprintEditorPart>();
        private readonly List<BlueprintEditorGroup> groups = new List<BlueprintEditorGroup>();
        private readonly List<string> selection = new List<string>();
        private readonly ReadOnlyCollection<BlueprintEditorPart> partsView;
        private readonly ReadOnlyCollection<BlueprintEditorGroup> groupsView;
        private readonly ReadOnlyCollection<string> selectionView;
        private readonly List<State> undo = new List<State>();
        private readonly List<State> redo = new List<State>();
        private readonly List<State> provisionalRedo = new List<State>();
        private bool hasProvisionalEdit;
        private int currentToken;
        private int cleanToken;
        private int nextToken = 1;

        public BlueprintEditorDocument(
            string sourceBlueprintId,
            string name,
            string category,
            IEnumerable<BlueprintEditorPart> sourceParts = null,
            IEnumerable<BlueprintEditorGroup> sourceGroups = null,
            string primaryPartId = null,
            string primaryGroupId = null)
        {
            partsView = parts.AsReadOnly();
            groupsView = groups.AsReadOnly();
            selectionView = selection.AsReadOnly();
            SourceBlueprintId = string.IsNullOrWhiteSpace(sourceBlueprintId)
                ? null
                : sourceBlueprintId.Trim();
            Name = BlueprintEditorPart.Required(name, nameof(name));
            Category = BlueprintEditorPart.Required(category, nameof(category));
            if (sourceGroups != null)
            {
                foreach (BlueprintEditorGroup group in sourceGroups)
                {
                    if (group == null) throw new ArgumentException("Group is null.", nameof(sourceGroups));
                    groups.Add(group.Clone());
                }
            }
            if (sourceParts != null)
            {
                foreach (BlueprintEditorPart part in sourceParts)
                {
                    if (part == null) throw new ArgumentException("Part is null.", nameof(sourceParts));
                    parts.Add(part.Clone());
                }
            }
            PrimaryPartId = string.IsNullOrWhiteSpace(primaryPartId)
                ? null
                : primaryPartId.Trim();
            PrimaryGroupId = string.IsNullOrWhiteSpace(primaryGroupId)
                ? null
                : primaryGroupId.Trim();
            ValidateDocument();
        }

        public string SourceBlueprintId { get; private set; }
        public string Name { get; private set; }
        public string Category { get; private set; }
        public IReadOnlyList<BlueprintEditorPart> Parts => partsView;
        public IReadOnlyList<BlueprintEditorGroup> Groups => groupsView;
        public IReadOnlyList<string> Selection => selectionView;
        public string ActiveNodeId { get; private set; }
        public string PrimaryPartId { get; private set; }
        public string PrimaryGroupId { get; private set; }
        public bool IsDirty => currentToken != cleanToken;
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;
        public int EditablePartSelectionCount => SelectedEditableParts().Count;
        public int MovableNodeSelectionCount => MovableSelectedNodes().Count;
        public bool CanDeleteSelection
        {
            get
            {
                foreach (string selectedId in selection)
                {
                    BlueprintEditorPart part = FindPart(selectedId);
                    if (part != null && !IsEffectivelyLocked(part.StableId)) return true;
                    BlueprintEditorGroup group = FindGroup(selectedId);
                    if (group != null && !IsEffectivelyLocked(group.StableId)) return true;
                }
                return false;
            }
        }

        public bool SelectOnly(string stableId)
        {
            if (!ContainsNode(stableId)) return false;
            selection.Clear();
            selection.Add(stableId);
            ActiveNodeId = stableId;
            return true;
        }

        public bool ToggleSelection(string stableId)
        {
            if (!ContainsNode(stableId)) return false;
            int index = selection.IndexOf(stableId);
            if (index >= 0)
            {
                selection.RemoveAt(index);
                if (ActiveNodeId == stableId)
                    ActiveNodeId = selection.Count == 0 ? null : selection[selection.Count - 1];
            }
            else
            {
                selection.Add(stableId);
                ActiveNodeId = stableId;
            }
            return true;
        }

        public bool SelectRange(string stableId)
        {
            if (!ContainsNode(stableId)) return false;
            if (string.IsNullOrEmpty(ActiveNodeId) || !ContainsNode(ActiveNodeId))
                return SelectOnly(stableId);
            List<string> order = SiblingOrder(stableId);
            int first = order.IndexOf(ActiveNodeId);
            int last = order.IndexOf(stableId);
            if (first < 0 || last < 0) return SelectOnly(stableId);
            if (first > last)
            {
                int swap = first;
                first = last;
                last = swap;
            }
            selection.Clear();
            for (int index = first; index <= last; ++index) selection.Add(order[index]);
            ActiveNodeId = stableId;
            return true;
        }

        public void ClearSelection()
        {
            selection.Clear();
            ActiveNodeId = null;
        }

        public bool AddPart(BlueprintEditorPart part)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            if (parts.Count >= MaximumParts)
                throw new InvalidOperationException("Maximum part count reached.");
            if (ContainsNode(part.StableId))
                throw new ArgumentException("Stable ID is already used.", nameof(part));
            if (part.ParentGroupId != null && FindGroup(part.ParentGroupId) == null)
                throw new ArgumentException("Parent group does not exist.", nameof(part));
            return Commit(() =>
            {
                parts.Add(part.Clone());
                selection.Clear();
                selection.Add(part.StableId);
                ActiveNodeId = part.StableId;
            });
        }

        public bool Rename(string stableId, string name)
        {
            string normalized = BlueprintEditorPart.Required(name, nameof(name));
            BlueprintEditorPart part = FindPart(stableId);
            BlueprintEditorGroup group = FindGroup(stableId);
            if (part == null && group == null) return false;
            return Commit(() =>
            {
                if (part != null) part.DisplayName = normalized;
                else group.Name = normalized;
            });
        }

        public bool SetPrimaryPart(string stableId)
        {
            string normalized = string.IsNullOrWhiteSpace(stableId) ? null : stableId.Trim();
            if (normalized != null && FindPart(normalized) == null) return false;
            return Commit(() => PrimaryPartId = normalized);
        }

        public bool SetPrimaryGroup(string stableId)
        {
            string normalized = string.IsNullOrWhiteSpace(stableId) ? null : stableId.Trim();
            if (normalized != null)
            {
                if (FindGroup(normalized) == null) return false;
                bool hasPart = false;
                foreach (BlueprintEditorPart part in parts)
                    if (IsPartInGroup(part.StableId, normalized)) { hasPart = true; break; }
                if (!hasPart) return false;
            }
            return Commit(() => PrimaryGroupId = normalized);
        }

        public bool SetGroupPivot(string groupId, string partId)
        {
            BlueprintEditorGroup group = FindGroup(groupId);
            string normalized = string.IsNullOrWhiteSpace(partId) ? null : partId.Trim();
            if (group == null || normalized != null &&
                (FindPart(normalized) == null || !IsPartInGroup(normalized, group.StableId)))
                return false;
            return Commit(() => group.PivotPartId = normalized);
        }

        public bool SetMetadata(string name, string category)
        {
            string normalizedName = BlueprintEditorPart.Required(name, nameof(name));
            string normalizedCategory = BlueprintEditorPart.Required(
                category, nameof(category)).ToUpperInvariant();
            if (normalizedName.Length > 48)
                throw new ArgumentOutOfRangeException(nameof(name));
            if (normalizedCategory.Length > 24)
                throw new ArgumentOutOfRangeException(nameof(category));
            return Commit(() =>
            {
                Name = normalizedName;
                Category = normalizedCategory;
            });
        }

        public bool SetPartProperties(
            string stableId,
            string displayName,
            Point3 position,
            Rotation3 rotation,
            Point3? scale = null)
        {
            BlueprintEditorPart part = FindPart(stableId);
            if (part == null || !IsEffectivelyVisible(stableId) ||
                IsEffectivelyLocked(stableId)) return false;
            string normalizedName = BlueprintEditorPart.Required(
                displayName, nameof(displayName));
            BlueprintEditorPart.Validate(position, nameof(position));
            Rotation3 normalizedRotation = rotation.Normalized();
            Point3 normalizedScale = BlueprintEditorPart.NormalizeScale(scale ?? part.Scale, nameof(scale));
            return Commit(() =>
            {
                part.DisplayName = normalizedName;
                part.Position = position;
                part.Rotation = normalizedRotation;
                part.Scale = normalizedScale;
            });
        }

        public bool CreateEmptyGroup(string stableId, string name, string parentGroupId = null)
        {
            var group = new BlueprintEditorGroup(stableId, name, parentGroupId: parentGroupId);
            if (groups.Count >= MaximumGroups)
                throw new InvalidOperationException("Maximum group count reached.");
            if (ContainsNode(group.StableId))
                throw new ArgumentException("Stable ID is already used.", nameof(stableId));
            if (parentGroupId != null && (FindGroup(parentGroupId) == null || IsEffectivelyLocked(parentGroupId)))
                throw new ArgumentException("Parent group is unavailable.", nameof(parentGroupId));
            return Commit(() =>
            {
                groups.Add(group);
                SelectOnly(group.StableId);
            });
        }

        public bool CreateGroup(string stableId, string name)
        {
            string normalizedId = BlueprintEditorPart.Required(stableId, nameof(stableId));
            if (groups.Count >= MaximumGroups)
                throw new InvalidOperationException("Maximum group count reached.");
            if (ContainsNode(normalizedId))
                throw new ArgumentException("Stable ID is already used.", nameof(stableId));
            List<string> selectedNodes = MovableSelectedNodes();
            if (selectedNodes.Count < 2) return false;
            string parentGroupId = ParentGroupIdOf(selectedNodes[0]);
            foreach (string selectedId in selectedNodes)
                if (ParentGroupIdOf(selectedId) != parentGroupId) return false;
            var group = new BlueprintEditorGroup(
                normalizedId, name, parentGroupId: parentGroupId);
            return Commit(() =>
            {
                groups.Add(group);
                foreach (string selectedId in selectedNodes)
                    SetParentGroupId(selectedId, group.StableId);
                selection.Clear();
                selection.Add(group.StableId);
                ActiveNodeId = group.StableId;
            });
        }

        public bool SetSelectionGroup(string groupId)
        {
            BlueprintEditorGroup target = FindGroup(groupId);
            if (groupId != null && (target == null ||
                IsEffectivelyLocked(groupId))) return false;
            List<string> selectedNodes = MovableSelectedNodes();
            if (selectedNodes.Count == 0) return false;
            foreach (string selectedId in selectedNodes)
            {
                if (selectedId == groupId) return false;
                BlueprintEditorGroup group = FindGroup(selectedId);
                if (group != null && IsGroupDescendantOf(groupId, group.StableId))
                    return false;
            }
            return Commit(() =>
            {
                foreach (string selectedId in selectedNodes)
                    SetParentGroupId(selectedId, groupId);
                ClearInvalidGroupPivots();
            });
        }

        public bool UngroupSelection()
        {
            var selectedIds = new HashSet<string>(selection, StringComparer.Ordinal);
            var selectedParts = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlueprintEditorPart part in parts)
                if (selectedIds.Contains(part.StableId) &&
                    !IsEffectivelyLocked(part.StableId))
                    selectedParts.Add(part.StableId);
            var groupsToRemove = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlueprintEditorGroup group in groups)
                if (selectedIds.Contains(group.StableId) &&
                    !IsEffectivelyLocked(group.StableId))
                    groupsToRemove.Add(group.StableId);
            if (selectedParts.Count == 0 && groupsToRemove.Count == 0) return false;
            return Commit(() =>
            {
                var changed = new List<string>();
                foreach (BlueprintEditorPart part in parts)
                {
                    string parent = part.ParentGroupId;
                    if (selectedParts.Contains(part.StableId))
                    {
                        BlueprintEditorGroup currentParent = FindGroup(parent);
                        parent = currentParent?.ParentGroupId;
                    }
                    string survivingParent = SurvivingParent(parent, groupsToRemove);
                    if (survivingParent == part.ParentGroupId) continue;
                    part.ParentGroupId = survivingParent;
                    changed.Add(part.StableId);
                }
                foreach (BlueprintEditorGroup group in groups)
                {
                    if (groupsToRemove.Contains(group.StableId)) continue;
                    string survivingParent = SurvivingParent(
                        group.ParentGroupId, groupsToRemove);
                    if (survivingParent == group.ParentGroupId) continue;
                    group.ParentGroupId = survivingParent;
                    changed.Add(group.StableId);
                }
                groups.RemoveAll(group => groupsToRemove.Contains(group.StableId));
                if (PrimaryGroupId != null && groupsToRemove.Contains(PrimaryGroupId))
                    PrimaryGroupId = null;
                ClearInvalidGroupPivots();
                selection.Clear();
                foreach (string changedId in changed)
                {
                    selection.Add(changedId);
                    ActiveNodeId = changedId;
                }
                if (selection.Count == 0) ActiveNodeId = null;
            });
        }

        public bool SetSelectionVisibility(bool visible)
        {
            if (selection.Count == 0) return false;
            var selectedIds = new HashSet<string>(selection, StringComparer.Ordinal);
            return Commit(() =>
            {
                foreach (BlueprintEditorPart part in parts)
                    if (selectedIds.Contains(part.StableId)) part.Visible = visible;
                foreach (BlueprintEditorGroup group in groups)
                    if (selectedIds.Contains(group.StableId)) group.Visible = visible;
            });
        }

        public bool SetSelectionLocked(bool locked)
        {
            if (selection.Count == 0) return false;
            var selectedIds = new HashSet<string>(selection, StringComparer.Ordinal);
            return Commit(() =>
            {
                foreach (BlueprintEditorPart part in parts)
                    if (selectedIds.Contains(part.StableId)) part.Locked = locked;
                foreach (BlueprintEditorGroup group in groups)
                    if (selectedIds.Contains(group.StableId)) group.Locked = locked;
            });
        }

        public bool ShowAll()
        {
            return Commit(() =>
            {
                foreach (BlueprintEditorPart part in parts) part.Visible = true;
                foreach (BlueprintEditorGroup group in groups) group.Visible = true;
            });
        }

        public bool IsolateSelection() => SetVisibilityRelativeToSelection(invert: false);

        public bool ShowAllExceptSelection() => SetVisibilityRelativeToSelection(invert: true);

        private bool SetVisibilityRelativeToSelection(bool invert)
        {
            if (selection.Count == 0) return false;
            var selectedIds = new List<string>(selection);
            return Commit(() =>
            {
                foreach (BlueprintEditorGroup group in groups) group.Visible = true;
                foreach (BlueprintEditorPart part in parts)
                    part.Visible = IsPartSelected(part.StableId, selectedIds) != invert;
            });
        }

        public bool SetVisibility(string stableId, bool visible)
        {
            BlueprintEditorPart part = FindPart(stableId);
            BlueprintEditorGroup group = FindGroup(stableId);
            if (part == null && group == null) return false;
            return Commit(() =>
            {
                if (part != null) part.Visible = visible;
                else group.Visible = visible;
            });
        }

        public bool SetLocked(string stableId, bool locked)
        {
            BlueprintEditorPart part = FindPart(stableId);
            BlueprintEditorGroup group = FindGroup(stableId);
            if (part == null && group == null) return false;
            return Commit(() =>
            {
                if (part != null) part.Locked = locked;
                else group.Locked = locked;
            });
        }

        public bool IsEffectivelyVisible(string stableId)
        {
            BlueprintEditorPart part = FindPart(stableId);
            BlueprintEditorGroup group = FindGroup(stableId);
            if (part == null && group == null) return false;
            if (part != null && !part.Visible || group != null && !group.Visible) return false;
            return AncestorsVisible(part != null ? part.ParentGroupId : group.ParentGroupId);
        }

        public bool IsEffectivelyLocked(string stableId)
        {
            BlueprintEditorPart part = FindPart(stableId);
            BlueprintEditorGroup group = FindGroup(stableId);
            if (part == null && group == null) return false;
            if (part != null && part.Locked || group != null && group.Locked) return true;
            return AncestorsLocked(part != null ? part.ParentGroupId : group.ParentGroupId);
        }

        public bool IsPartSelected(string stableId) => IsPartSelected(stableId, selection);

        public bool IsPartInGroup(string partId, string groupId)
        {
            BlueprintEditorPart part = FindPart(partId);
            if (part == null || string.IsNullOrEmpty(groupId)) return false;
            for (BlueprintEditorGroup group = FindGroup(part.ParentGroupId);
                group != null; group = FindGroup(group.ParentGroupId))
                if (group.StableId == groupId) return true;
            return false;
        }

        public bool IsPartSelected(string stableId, IReadOnlyList<string> nodeIds)
        {
            BlueprintEditorPart part = FindPart(stableId);
            if (part == null || nodeIds == null) return false;
            foreach (string nodeId in nodeIds)
            {
                if (nodeId == stableId) return true;
                for (BlueprintEditorGroup group = FindGroup(part.ParentGroupId);
                    group != null; group = FindGroup(group.ParentGroupId))
                    if (nodeId == group.StableId) return true;
            }
            return false;
        }

        public bool ApplyTransformDelta(
            Point3 translation,
            Rotation3 rotation,
            Point3 pivot,
            double uniformScale = 1.0)
        {
            BlueprintEditorPart.Validate(translation, nameof(translation));
            BlueprintEditorPart.Validate(pivot, nameof(pivot));
            if (!GeometryMath.IsFinite(uniformScale) || uniformScale <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(uniformScale));
            Rotation3 delta = rotation.Normalized();
            List<BlueprintEditorPart> targets = SelectedEditableParts();
            if (targets.Count == 0) return false;
            return Commit(() =>
            {
                foreach (BlueprintEditorPart part in targets)
                {
                    part.Position = pivot +
                        delta.Rotate((part.Position - pivot) * uniformScale) + translation;
                    part.Rotation = Multiply(delta, part.Rotation).Normalized();
                    part.Scale = BlueprintEditorPart.NormalizeScale(
                        part.Scale * uniformScale, nameof(uniformScale));
                }
            });
        }

        public bool DuplicateSelection(Point3 offset)
        {
            BlueprintEditorPart.Validate(offset, nameof(offset));
            List<string> roots = MovableSelectedNodes();
            if (roots.Count == 0) return false;
            var rootIds = new HashSet<string>(roots, StringComparer.Ordinal);
            var groupIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (BlueprintEditorGroup group in groups)
                if (rootIds.Contains(group.StableId) ||
                    HasSelectedAncestor(group.ParentGroupId, rootIds))
                    groupIds.Add(group.StableId, Guid.NewGuid().ToString("N"));
            if (groups.Count + groupIds.Count > MaximumGroups)
                throw new InvalidOperationException("Maximum group count reached.");
            var targets = new List<BlueprintEditorPart>();
            foreach (BlueprintEditorPart part in parts)
                if (rootIds.Contains(part.StableId) ||
                    part.ParentGroupId != null && groupIds.ContainsKey(part.ParentGroupId))
                    targets.Add(part);
            if (parts.Count + targets.Count > MaximumParts)
                throw new InvalidOperationException("Maximum part count reached.");
            var partIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (BlueprintEditorPart part in targets)
                partIds.Add(part.StableId, Guid.NewGuid().ToString("N"));
            var groupCopies = new List<BlueprintEditorGroup>(groupIds.Count);
            foreach (BlueprintEditorGroup group in groups)
            {
                if (!groupIds.TryGetValue(group.StableId, out string copyId)) continue;
                string parentId = group.ParentGroupId;
                if (parentId != null && groupIds.TryGetValue(parentId, out string copyParent))
                    parentId = copyParent;
                string pivotPartId = group.PivotPartId;
                if (pivotPartId != null && partIds.TryGetValue(pivotPartId, out string copyPivot))
                    pivotPartId = copyPivot;
                groupCopies.Add(new BlueprintEditorGroup(copyId, group.Name + " копия",
                    group.Visible, group.Locked, parentId, pivotPartId));
            }
            var copies = new List<BlueprintEditorPart>(targets.Count);
            var copiedRoots = new List<string>();
            foreach (string rootId in roots)
                if (groupIds.TryGetValue(rootId, out string copyGroupId))
                    copiedRoots.Add(copyGroupId);
            foreach (BlueprintEditorPart part in targets)
            {
                string copyId = partIds[part.StableId];
                string parentId = part.ParentGroupId;
                if (parentId != null && groupIds.TryGetValue(parentId, out string copyParent))
                    parentId = copyParent;
                copies.Add(new BlueprintEditorPart(
                    copyId,
                    part.PrefabName,
                    part.DisplayName + " копия",
                    part.Position + offset,
                    part.Rotation,
                    parentId,
                    part.Visible,
                    part.Locked,
                    part.Scale));
                if (rootIds.Contains(part.StableId)) copiedRoots.Add(copyId);
            }
            return Commit(() =>
            {
                groups.AddRange(groupCopies);
                parts.AddRange(copies);
                selection.Clear();
                selection.AddRange(copiedRoots);
                ActiveNodeId = copiedRoots[copiedRoots.Count - 1];
            });
        }

        public bool InsertBlueprint(BlueprintEditorDocument source, Point3 position, Rotation3 rotation)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            BlueprintEditorPart.Validate(position, nameof(position));
            Rotation3 orientation = rotation.Normalized();
            if (source.parts.Count == 0) return false;
            if (parts.Count + source.parts.Count > MaximumParts ||
                groups.Count + source.groups.Count + 1 > MaximumGroups)
                throw new InvalidOperationException("Maximum blueprint size reached.");
            string rootId = Guid.NewGuid().ToString("N");
            var groupIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (BlueprintEditorGroup group in source.groups)
                groupIds.Add(group.StableId, Guid.NewGuid().ToString("N"));
            var partIds = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (BlueprintEditorPart part in source.parts)
                partIds.Add(part.StableId, Guid.NewGuid().ToString("N"));
            var newGroups = new List<BlueprintEditorGroup>
            { new BlueprintEditorGroup(rootId, source.Name, pivotPartId:
                source.PrimaryPartId != null ? partIds[source.PrimaryPartId] : null) };
            foreach (BlueprintEditorGroup group in source.groups)
                newGroups.Add(new BlueprintEditorGroup(groupIds[group.StableId], group.Name,
                    group.Visible, group.Locked,
                    group.ParentGroupId == null ? rootId : groupIds[group.ParentGroupId],
                    group.PivotPartId == null ? null : partIds[group.PivotPartId]));
            var newParts = new List<BlueprintEditorPart>(source.parts.Count);
            foreach (BlueprintEditorPart part in source.parts)
                newParts.Add(new BlueprintEditorPart(partIds[part.StableId],
                    part.PrefabName, part.DisplayName, position + orientation.Rotate(part.Position),
                    Multiply(orientation, part.Rotation).Normalized(),
                    part.ParentGroupId == null ? rootId : groupIds[part.ParentGroupId],
                    part.Visible, part.Locked, part.Scale));
            return Commit(() =>
            {
                groups.AddRange(newGroups);
                parts.AddRange(newParts);
                SelectOnly(rootId);
            });
        }

        public IReadOnlyList<BlueprintEditorPart> PreviewArray(
            int countX,
            int countY,
            Point3 stepX,
            Point3 stepY,
            Point3 rotationAxis,
            double rotationDegrees,
            double rise,
            bool symmetric,
            double scaleStep,
            Point3 backAnchor,
            Point3 frontAnchor,
            Point3? pivot = null) => BuildArrayCopies(
                countX, countY, stepX, stepY, rotationAxis, rotationDegrees, rise,
                symmetric, scaleStep, backAnchor, frontAnchor, pivot).AsReadOnly();

        public bool ApplyArray(
            int countX,
            int countY,
            Point3 stepX,
            Point3 stepY,
            Point3 rotationAxis,
            double rotationDegrees,
            double rise,
            bool symmetric,
            double scaleStep,
            Point3 backAnchor,
            Point3 frontAnchor,
            Point3? pivot = null)
        {
            List<BlueprintEditorPart> copies = BuildArrayCopies(
                countX, countY, stepX, stepY, rotationAxis, rotationDegrees, rise,
                symmetric, scaleStep, backAnchor, frontAnchor, pivot);
            if (copies.Count == 0) return false;
            return Commit(() =>
            {
                parts.AddRange(copies);
                selection.Clear();
                foreach (BlueprintEditorPart copy in copies) selection.Add(copy.StableId);
                ActiveNodeId = copies[copies.Count - 1].StableId;
            });
        }

        public IReadOnlyList<BlueprintEditorPart> PreviewContour(
            IReadOnlyList<string> orderedSupportIds,
            bool closed,
            double scaleStep) => BuildContourCopies(
                orderedSupportIds, closed, scaleStep).AsReadOnly();

        public bool ApplyContour(
            IReadOnlyList<string> orderedSupportIds,
            bool closed,
            double scaleStep)
        {
            List<BlueprintEditorPart> copies = BuildContourCopies(
                orderedSupportIds, closed, scaleStep);
            if (copies.Count == 0) return false;
            return Commit(() =>
            {
                parts.AddRange(copies);
                selection.Clear();
                foreach (BlueprintEditorPart copy in copies) selection.Add(copy.StableId);
                ActiveNodeId = copies[copies.Count - 1].StableId;
            });
        }

        public bool DeleteSelection(bool deleteGroupChildren)
        {
            if (selection.Count == 0) return false;
            var selectedIds = new HashSet<string>(selection, StringComparer.Ordinal);
            var requestedGroupIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlueprintEditorGroup group in groups)
                if (selectedIds.Contains(group.StableId) || deleteGroupChildren &&
                    HasSelectedAncestor(group.ParentGroupId, selectedIds))
                    requestedGroupIds.Add(group.StableId);
            var deletableGroupIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlueprintEditorGroup group in groups)
                if (requestedGroupIds.Contains(group.StableId) &&
                    !IsEffectivelyLocked(group.StableId))
                    deletableGroupIds.Add(group.StableId);
            var deletablePartIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlueprintEditorPart part in parts)
            {
                bool selectedPart = selectedIds.Contains(part.StableId);
                bool childOfSelectedGroup = deleteGroupChildren &&
                    HasSelectedAncestor(part.ParentGroupId, selectedIds);
                if ((selectedPart || childOfSelectedGroup) &&
                    !IsEffectivelyLocked(part.StableId))
                    deletablePartIds.Add(part.StableId);
            }
            if (deletablePartIds.Count == 0 && deletableGroupIds.Count == 0) return false;

            return Commit(() =>
            {
                parts.RemoveAll(part => deletablePartIds.Contains(part.StableId));
                if (PrimaryPartId != null && deletablePartIds.Contains(PrimaryPartId))
                    PrimaryPartId = null;
                foreach (BlueprintEditorPart part in parts)
                    part.ParentGroupId = SurvivingParent(
                        part.ParentGroupId, deletableGroupIds);
                foreach (BlueprintEditorGroup group in groups)
                    if (!deletableGroupIds.Contains(group.StableId))
                        group.ParentGroupId = SurvivingParent(
                            group.ParentGroupId, deletableGroupIds);
                groups.RemoveAll(group => deletableGroupIds.Contains(group.StableId));
                if (PrimaryGroupId != null && deletableGroupIds.Contains(PrimaryGroupId))
                    PrimaryGroupId = null;
                ClearInvalidGroupPivots();
                ClearSelection();
            });
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            AcceptLastEdit();
            redo.Add(Capture());
            State previous = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            Restore(previous);
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            AcceptLastEdit();
            PushBounded(undo, Capture());
            State next = redo[redo.Count - 1];
            redo.RemoveAt(redo.Count - 1);
            Restore(next);
            return true;
        }

        public bool RollbackLastEdit()
        {
            if (!hasProvisionalEdit || undo.Count == 0) return false;
            State previous = undo[undo.Count - 1];
            undo.RemoveAt(undo.Count - 1);
            redo.Clear();
            redo.AddRange(provisionalRedo);
            provisionalRedo.Clear();
            hasProvisionalEdit = false;
            Restore(previous);
            return true;
        }

        public void AcceptLastEdit()
        {
            provisionalRedo.Clear();
            hasProvisionalEdit = false;
        }

        public void AdoptSavedIdentity(string sourceBlueprintId, string name, string category)
        {
            SourceBlueprintId = BlueprintEditorPart.Required(
                sourceBlueprintId, nameof(sourceBlueprintId));
            Name = BlueprintEditorPart.Required(name, nameof(name));
            Category = BlueprintEditorPart.Required(category, nameof(category));
        }

        public void MarkClean()
        {
            cleanToken = currentToken;
        }

        public List<BlueprintEditorPart> CopyParts()
        {
            var copy = new List<BlueprintEditorPart>(parts.Count);
            foreach (BlueprintEditorPart part in parts) copy.Add(part.Clone());
            return copy;
        }

        public List<BlueprintEditorGroup> CopyGroups()
        {
            var copy = new List<BlueprintEditorGroup>(groups.Count);
            foreach (BlueprintEditorGroup group in groups) copy.Add(group.Clone());
            return copy;
        }

        private bool Commit(Action change)
        {
            State before = Capture();
            try
            {
                change();
                ValidateDocument();
            }
            catch
            {
                Restore(before);
                throw;
            }
            if (Same(before)) return false;
            PushBounded(undo, before);
            provisionalRedo.Clear();
            provisionalRedo.AddRange(redo);
            redo.Clear();
            hasProvisionalEdit = true;
            currentToken = nextToken++;
            return true;
        }

        private List<BlueprintEditorPart> SelectedEditableParts()
        {
            var result = new List<BlueprintEditorPart>();
            foreach (BlueprintEditorPart part in parts)
                if (IsPartSelected(part.StableId) && IsEffectivelyVisible(part.StableId) &&
                    !IsEffectivelyLocked(part.StableId)) result.Add(part);
            return result;
        }

        private List<BlueprintEditorPart> BuildArrayCopies(
            int countX,
            int countY,
            Point3 stepX,
            Point3 stepY,
            Point3 rotationAxis,
            double rotationDegrees,
            double rise,
            bool symmetric,
            double scaleStep,
            Point3 backAnchor,
            Point3 frontAnchor,
            Point3? requestedPivot)
        {
            if (countX < 1 || countY < 1 || countX > MaximumParts || countY > MaximumParts)
                throw new ArgumentOutOfRangeException(nameof(countX));
            BlueprintEditorPart.Validate(stepX, nameof(stepX));
            BlueprintEditorPart.Validate(stepY, nameof(stepY));
            BlueprintEditorPart.Validate(rotationAxis, nameof(rotationAxis));
            BlueprintEditorPart.Validate(backAnchor, nameof(backAnchor));
            BlueprintEditorPart.Validate(frontAnchor, nameof(frontAnchor));
            if (requestedPivot.HasValue)
                BlueprintEditorPart.Validate(requestedPivot.Value, nameof(requestedPivot));
            if (!GeometryMath.IsFinite(scaleStep) || !GeometryMath.IsFinite(rise) ||
                !GeometryMath.IsFinite(rotationDegrees))
                throw new ArgumentOutOfRangeException(nameof(scaleStep));

            List<BlueprintEditorPart> sources = SelectedEditableParts();
            if (sources.Count == 0) return new List<BlueprintEditorPart>();
            Point3 pivot = requestedPivot ?? default;
            if (!requestedPivot.HasValue)
            {
                foreach (BlueprintEditorPart source in sources) pivot += source.Position;
                pivot = pivot * (1.0 / sources.Count);
            }

            if ((long)sources.Count * (countX * countY - 1) + parts.Count > MaximumParts)
                throw new InvalidOperationException("Maximum part count reached.");
            IReadOnlyList<LayoutTransform3> row = countX == 1
                ? new[] { new LayoutTransform3(pivot, new Point3(0, 1, 0), 0) }
                : GuidePathSampling.SampleRepeat(pivot, stepX, countX, rise, rotationAxis,
                    rotationDegrees, symmetric, backAnchor, frontAnchor, scaleStep);
            var copies = new List<BlueprintEditorPart>();
            for (int y = 0; y < countY; ++y)
            {
                int logicalY = symmetric ? (y == 0 ? 0 : (y & 1) != 0 ? (y + 1) / 2 : -y / 2) : y;
                for (int x = 0; x < row.Count; ++x)
                {
                    if (x == 0 && y == 0) continue;
                    LayoutTransform3 sample = row[x];
                    double halfRadians = sample.IncrementalRotationDegrees * Math.PI / 360.0;
                    double sine = Math.Sin(halfRadians);
                    var turn = new Rotation3(sample.RotationAxis.X * sine,
                        sample.RotationAxis.Y * sine, sample.RotationAxis.Z * sine, Math.Cos(halfRadians));
                    foreach (BlueprintEditorPart source in sources)
                    {
                        Point3 copyScale = BlueprintEditorPart.NormalizeScale(
                            source.Scale * sample.UniformScale, nameof(scaleStep));
                        copies.Add(new BlueprintEditorPart(
                            Guid.NewGuid().ToString("N"), source.PrefabName,
                            source.DisplayName + " массив " + (x + 1) + ":" + (y + 1),
                            sample.Position + stepY * logicalY +
                                turn.Rotate((source.Position - pivot) * sample.UniformScale),
                            Multiply(turn, source.Rotation).Normalized(), source.ParentGroupId,
                            source.Visible, source.Locked, copyScale));
                    }
                }
            }
            return copies;
        }

        private List<BlueprintEditorPart> BuildContourCopies(
            IReadOnlyList<string> orderedSupportIds,
            bool closed,
            double scaleStep)
        {
            if (orderedSupportIds == null || orderedSupportIds.Count < (closed ? 3 : 2))
                throw new ArgumentOutOfRangeException(nameof(orderedSupportIds));
            if (!GeometryMath.IsFinite(scaleStep))
                throw new ArgumentOutOfRangeException(nameof(scaleStep));
            if (Math.Abs(scaleStep) > 1e-12 && Math.Abs(scaleStep) < 0.01 - 1e-7)
                throw new ArgumentOutOfRangeException(nameof(scaleStep));

            var supportIds = new HashSet<string>(StringComparer.Ordinal);
            var supports = new List<BlueprintEditorPart>(orderedSupportIds.Count);
            string supportPrefab = null;
            for (int index = 0; index < orderedSupportIds.Count; ++index)
            {
                string stableId = BlueprintEditorPart.Required(
                    orderedSupportIds[index], nameof(orderedSupportIds));
                BlueprintEditorPart support = FindPart(stableId);
                if (support == null || !supportIds.Add(stableId) ||
                    !IsEffectivelyVisible(stableId))
                    throw new ArgumentException("Contour support is unavailable.",
                        nameof(orderedSupportIds));
                if (supportPrefab == null) supportPrefab = support.PrefabName;
                else if (support.PrefabName != supportPrefab)
                    throw new ArgumentException("Contour supports must use one prefab.",
                        nameof(orderedSupportIds));
                supports.Add(support);
            }

            List<BlueprintEditorPart> sources = SelectedEditableParts();
            if (sources.Count == 0) return new List<BlueprintEditorPart>();
            foreach (BlueprintEditorPart source in sources)
                if (supportIds.Contains(source.StableId))
                    throw new InvalidOperationException(
                        "Contour source cannot also be a support.");

            Point3 pivot = default;
            foreach (BlueprintEditorPart source in sources) pivot += source.Position;
            pivot = pivot * (1.0 / sources.Count);
            int referenceIndex = 0;
            double nearest = double.PositiveInfinity;
            for (int index = 0; index < supports.Count; ++index)
            {
                double distance = (supports[index].Position - pivot).LengthSquared;
                if (distance >= nearest) continue;
                nearest = distance;
                referenceIndex = index;
            }
            BlueprintEditorPart reference = supports[referenceIndex];
            Rotation3 inverseReference = Inverse(reference.Rotation);
            var copies = new List<BlueprintEditorPart>();
            for (int index = 0; index < supports.Count; ++index)
            {
                if (index == referenceIndex) continue;
                int step = Math.Abs(index - referenceIndex);
                if (closed) step = Math.Min(step, supports.Count - step);
                double scale = 1.0 + scaleStep * step;
                if (scale <= 0.0) continue;
                BlueprintEditorPart support = supports[index];
                foreach (BlueprintEditorPart source in sources)
                {
                    Point3 copyScale = BlueprintEditorPart.NormalizeScale(
                        source.Scale * scale, nameof(scaleStep));
                    if (parts.Count + copies.Count >= MaximumParts)
                        throw new InvalidOperationException("Maximum part count reached.");
                    Point3 localPosition = inverseReference.Rotate(
                        source.Position - reference.Position);
                    Rotation3 localRotation = Multiply(
                        inverseReference, source.Rotation).Normalized();
                    copies.Add(new BlueprintEditorPart(
                        Guid.NewGuid().ToString("N"),
                        source.PrefabName,
                        source.DisplayName + " контур " + (index + 1),
                        support.Position + support.Rotation.Rotate(localPosition),
                        Multiply(support.Rotation, localRotation).Normalized(),
                        source.ParentGroupId,
                        source.Visible,
                        source.Locked,
                        copyScale));
                }
            }
            return copies;
        }

        private void ValidateDocument()
        {
            if (parts.Count > MaximumParts || groups.Count > MaximumGroups)
                throw new ArgumentOutOfRangeException("Document limits exceeded.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlueprintEditorGroup group in groups)
                if (!ids.Add(group.StableId))
                    throw new ArgumentException("Duplicate stable ID: " + group.StableId);
            foreach (BlueprintEditorGroup group in groups)
            {
                if (group.ParentGroupId != null && FindGroup(group.ParentGroupId) == null)
                    throw new ArgumentException("Missing parent group: " + group.ParentGroupId);
                var ancestry = new HashSet<string>(StringComparer.Ordinal) { group.StableId };
                for (BlueprintEditorGroup parent = FindGroup(group.ParentGroupId);
                    parent != null; parent = FindGroup(parent.ParentGroupId))
                    if (!ancestry.Add(parent.StableId))
                        throw new ArgumentException("Group hierarchy contains a cycle.");
            }
            foreach (BlueprintEditorPart part in parts)
            {
                if (!ids.Add(part.StableId))
                    throw new ArgumentException("Duplicate stable ID: " + part.StableId);
                if (part.ParentGroupId != null && FindGroup(part.ParentGroupId) == null)
                    throw new ArgumentException("Missing parent group: " + part.ParentGroupId);
                BlueprintEditorPart.Validate(part.Position, nameof(part.Position));
                BlueprintEditorPart.ValidateScale(part.Scale, nameof(part.Scale));
                part.Rotation = part.Rotation.Normalized();
            }
            foreach (BlueprintEditorGroup group in groups)
                if (group.PivotPartId != null &&
                    (FindPart(group.PivotPartId) == null ||
                        !IsPartInGroup(group.PivotPartId, group.StableId)))
                    throw new ArgumentException("Group pivot is outside its group: " +
                        group.StableId);
            if (PrimaryPartId != null && FindPart(PrimaryPartId) == null)
                throw new ArgumentException("Primary part does not exist: " + PrimaryPartId);
            if (PrimaryGroupId != null && FindGroup(PrimaryGroupId) == null)
                throw new ArgumentException("Primary group does not exist: " + PrimaryGroupId);
        }

        private bool ContainsNode(string stableId) =>
            FindPart(stableId) != null || FindGroup(stableId) != null;

        private List<string> SiblingOrder(string stableId)
        {
            string parentGroupId = ParentGroupIdOf(stableId);
            var result = new List<string>(parts.Count + groups.Count);
            foreach (BlueprintEditorGroup group in groups)
                if (group.ParentGroupId == parentGroupId) result.Add(group.StableId);
            foreach (BlueprintEditorPart part in parts)
                if (part.ParentGroupId == parentGroupId) result.Add(part.StableId);
            return result;
        }

        private List<string> MovableSelectedNodes()
        {
            var result = new List<string>();
            var selectedIds = new HashSet<string>(selection, StringComparer.Ordinal);
            foreach (string stableId in selection)
            {
                if (!IsEffectivelyVisible(stableId) || IsEffectivelyLocked(stableId)) continue;
                if (HasSelectedAncestor(ParentGroupIdOf(stableId), selectedIds)) continue;
                result.Add(stableId);
            }
            return result;
        }

        private string ParentGroupIdOf(string stableId)
        {
            BlueprintEditorPart part = FindPart(stableId);
            if (part != null) return part.ParentGroupId;
            return FindGroup(stableId)?.ParentGroupId;
        }

        private void SetParentGroupId(string stableId, string parentGroupId)
        {
            BlueprintEditorPart part = FindPart(stableId);
            if (part != null) part.ParentGroupId = parentGroupId;
            else FindGroup(stableId).ParentGroupId = parentGroupId;
        }

        private bool IsGroupDescendantOf(string groupId, string ancestorId)
        {
            for (BlueprintEditorGroup group = FindGroup(groupId);
                group != null; group = FindGroup(group.ParentGroupId))
                if (group.StableId == ancestorId) return true;
            return false;
        }

        private bool HasSelectedAncestor(
            string parentGroupId, HashSet<string> selectedIds)
        {
            for (BlueprintEditorGroup group = FindGroup(parentGroupId);
                group != null; group = FindGroup(group.ParentGroupId))
                if (selectedIds.Contains(group.StableId)) return true;
            return false;
        }

        private bool AncestorsVisible(string parentGroupId)
        {
            for (BlueprintEditorGroup group = FindGroup(parentGroupId);
                group != null; group = FindGroup(group.ParentGroupId))
                if (!group.Visible) return false;
            return true;
        }

        private bool AncestorsLocked(string parentGroupId)
        {
            for (BlueprintEditorGroup group = FindGroup(parentGroupId);
                group != null; group = FindGroup(group.ParentGroupId))
                if (group.Locked) return true;
            return false;
        }

        private string SurvivingParent(
            string parentGroupId, HashSet<string> removedGroupIds)
        {
            BlueprintEditorGroup group = FindGroup(parentGroupId);
            while (group != null && removedGroupIds.Contains(group.StableId))
                group = FindGroup(group.ParentGroupId);
            return group?.StableId;
        }

        private BlueprintEditorPart FindPart(string stableId) =>
            parts.Find(part => part.StableId == stableId);

        private BlueprintEditorGroup FindGroup(string stableId) =>
            stableId == null ? null : groups.Find(group => group.StableId == stableId);

        private void ClearInvalidGroupPivots()
        {
            foreach (BlueprintEditorGroup group in groups)
                if (group.PivotPartId != null &&
                    (FindPart(group.PivotPartId) == null ||
                        !IsPartInGroup(group.PivotPartId, group.StableId)))
                    group.PivotPartId = null;
        }

        private State Capture() => new State
        {
            Token = currentToken,
            Name = Name,
            Category = Category,
            Parts = CopyParts(),
            Groups = CopyGroups(),
            Selection = new List<string>(selection),
            ActiveNodeId = ActiveNodeId,
            PrimaryPartId = PrimaryPartId,
            PrimaryGroupId = PrimaryGroupId
        };

        private void Restore(State state)
        {
            Name = state.Name;
            Category = state.Category;
            parts.Clear();
            groups.Clear();
            selection.Clear();
            foreach (BlueprintEditorPart part in state.Parts) parts.Add(part.Clone());
            foreach (BlueprintEditorGroup group in state.Groups) groups.Add(group.Clone());
            selection.AddRange(state.Selection);
            ActiveNodeId = state.ActiveNodeId;
            PrimaryPartId = state.PrimaryPartId;
            PrimaryGroupId = state.PrimaryGroupId;
            currentToken = state.Token;
        }

        private bool Same(State state)
        {
            if (state.Name != Name || state.Category != Category ||
                state.Parts.Count != parts.Count || state.Groups.Count != groups.Count ||
                state.Selection.Count != selection.Count || state.ActiveNodeId != ActiveNodeId ||
                state.PrimaryPartId != PrimaryPartId || state.PrimaryGroupId != PrimaryGroupId)
                return false;
            for (int index = 0; index < parts.Count; ++index)
            {
                BlueprintEditorPart left = state.Parts[index];
                BlueprintEditorPart right = parts[index];
                if (left.StableId != right.StableId || left.PrefabName != right.PrefabName ||
                    left.DisplayName != right.DisplayName || left.ParentGroupId != right.ParentGroupId ||
                    left.Visible != right.Visible || left.Locked != right.Locked ||
                    !Same(left.Position, right.Position) || !Same(left.Rotation, right.Rotation) ||
                    !Same(left.Scale, right.Scale))
                    return false;
            }
            for (int index = 0; index < groups.Count; ++index)
            {
                BlueprintEditorGroup left = state.Groups[index];
                BlueprintEditorGroup right = groups[index];
                if (left.StableId != right.StableId || left.Name != right.Name ||
                    left.Visible != right.Visible || left.Locked != right.Locked ||
                    left.ParentGroupId != right.ParentGroupId ||
                    left.PivotPartId != right.PivotPartId)
                    return false;
            }
            for (int index = 0; index < selection.Count; ++index)
                if (state.Selection[index] != selection[index]) return false;
            return true;
        }

        private static void PushBounded(List<State> history, State state)
        {
            history.Add(state);
            if (history.Count > MaximumHistory) history.RemoveAt(0);
        }

        private static bool Same(Point3 left, Point3 right) =>
            left.X == right.X && left.Y == right.Y && left.Z == right.Z;

        private static bool Same(Rotation3 left, Rotation3 right) =>
            left.X == right.X && left.Y == right.Y && left.Z == right.Z && left.W == right.W;

        private static Rotation3 Multiply(Rotation3 left, Rotation3 right) =>
            new Rotation3(
                left.W * right.X + left.X * right.W + left.Y * right.Z - left.Z * right.Y,
                left.W * right.Y - left.X * right.Z + left.Y * right.W + left.Z * right.X,
                left.W * right.Z + left.X * right.Y - left.Y * right.X + left.Z * right.W,
                left.W * right.W - left.X * right.X - left.Y * right.Y - left.Z * right.Z);

        private static Rotation3 Inverse(Rotation3 value)
        {
            Rotation3 normalized = value.Normalized();
            return new Rotation3(-normalized.X, -normalized.Y, -normalized.Z, normalized.W);
        }
    }
}
