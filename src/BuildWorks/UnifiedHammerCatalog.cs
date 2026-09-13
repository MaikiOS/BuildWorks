using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    internal sealed class HammerCatalogPointerClick : MonoBehaviour, IPointerClickHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        internal Action<PointerEventData.InputButton> Clicked;
        internal Action<Vector2> RightPressed;
        internal Action<Vector2> RightReleased;

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Right) return;
            Clicked?.Invoke(data.button);
            data.Use();
        }

        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Right) return;
            RightPressed?.Invoke(data.position);
            data.Use();
        }

        public void OnPointerUp(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Right) return;
            RightReleased?.Invoke(data.position);
            data.Use();
        }
    }

    internal sealed class HammerCatalogFavoriteClick : MonoBehaviour, IPointerClickHandler
    {
        internal Action Clicked;

        public void OnPointerClick(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Middle) return;
            Clicked?.Invoke();
            data.Use();
        }
    }

    /// <summary>
    /// Adapts Valheim's Hammer inventory into the indexed BuildWorks catalog
    /// while preserving native selection, favorites, and placement ownership.
    /// </summary>
    internal sealed class UnifiedHammerCatalog : IDisposable
    {
        private const string VanillaHammerPrefab = "Hammer";
        private const string OdinHammerPrefab = "odin_hammer";
        private const string VanillaPieceTable = "_HammerPieceTable";
        private static string BlueprintCategoryName =>
            BuildWorksLocalization.Text("catalog.category.blueprints");
        private static string FavoriteCategoryName =>
            BuildWorksLocalization.Text("catalog.category.favorites");

        private readonly struct AddedPiece
        {
            public AddedPiece(
                GameObject prefab,
                Piece piece,
                Piece.PieceCategory category,
                PieceTable sourceTable,
                int sourceIndex)
            {
                Prefab = prefab;
                Piece = piece;
                OriginalCategory = category;
                SourceTable = sourceTable;
                SourceIndex = sourceIndex;
            }

            public GameObject Prefab { get; }
            public Piece Piece { get; }
            public Piece.PieceCategory OriginalCategory { get; }
            public PieceTable SourceTable { get; }
            public int SourceIndex { get; }
        }

        private readonly ManualLogSource log;
        private readonly List<AddedPiece> addedPieces = new List<AddedPiece>();
        private readonly List<Piece> reclassifiedPieces = new List<Piece>();
        private static readonly Dictionary<Piece, string> OdinSourceGroups =
            new Dictionary<Piece, string>();
        private PieceTable targetTable;
        private GameObject favoriteCategoryKeeper;
        private Recipe odinHammerRecipe;
        private bool odinHammerRecipeEnabled;
        private static bool categoryReady;
        private bool categoriesNormalized;
        private bool odinComplete;
        private float nextAttempt;

        internal static Piece.PieceCategory BlueprintCategory { get; private set; } =
            Piece.PieceCategory.Misc;
        internal static Piece.PieceCategory FavoriteCategory { get; private set; } =
            Piece.PieceCategory.Misc;

        internal static bool BlueprintCategoryReady => categoryReady;
        internal static bool FavoriteCategoryReady => categoryReady;

        public UnifiedHammerCatalog(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Update(Player player)
        {
            if (!player || Time.unscaledTime < nextAttempt ||
                categoryReady && categoriesNormalized && odinComplete)
                return;

            nextAttempt = Time.unscaledTime + 1f;
            GameObject hammer = ObjectDB.instance?.GetItemPrefab(VanillaHammerPrefab);
            PieceTable target = hammer
                ? hammer.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces
                : null;
            if (!target) return;

            if (!TryResolveJotunn(
                out object manager,
                out MethodInfo addCategory,
                out MethodInfo getCategoryMap,
                out _))
            {
                return;
            }

            if (!categoryReady)
            {
                BlueprintCategory = AddCategory(manager, addCategory, BlueprintCategoryName);
                FavoriteCategory = AddCategory(manager, addCategory, FavoriteCategoryName);
                categoryReady = true;
                log.LogInfo("BuildWorks registered blueprint category " +
                    (int)BlueprintCategory + " (" + BlueprintCategoryName + ").");
            }

            targetTable = target;
            if (!categoriesNormalized)
            {
                int normalized = NormalizeValheimCategories(target);
                AddFavoriteCategoryKeeper(target);
                categoriesNormalized = true;
                RefreshCategoryTabs();
                if (normalized > 0)
                    log.LogInfo("BuildWorks reindexed " + normalized +
                        " DeepNorth building pieces into Construction.");
            }

            if (odinComplete) return;
            if (!IsAssemblyLoaded("OdinArchitect"))
            {
                odinComplete = true;
                return;
            }
            GameObject odinHammer = ObjectDB.instance.GetItemPrefab(OdinHammerPrefab);
            PieceTable source = odinHammer
                ? odinHammer.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_buildPieces
                : null;
            if (!source) return;

            if (source == target)
            {
                DisableOdinHammerRecipe();
                odinComplete = true;
                return;
            }

            IDictionary categoryMap = getCategoryMap.Invoke(manager, null) as IDictionary;
            if (categoryMap == null) return;

            int added = 0;
            var sourcePieces = new List<GameObject>(source.m_pieces ?? new List<GameObject>());
            for (int sourceIndex = 0; sourceIndex < sourcePieces.Count; ++sourceIndex)
            {
                GameObject prefab = sourcePieces[sourceIndex];
                Piece piece = prefab ? prefab.GetComponent<Piece>() : null;
                if (!piece || ContainsPrefab(target.m_pieces, prefab)) continue;

                Piece.PieceCategory originalCategory = piece.m_category;
                string sourceName = categoryMap[originalCategory] as string ??
                    originalCategory.ToString();
                Piece.PieceCategory category = MapOdinCategory(sourceName, piece);
                OdinSourceGroups[piece] = NormalizeOdinGroup(sourceName);

                piece.m_category = category;
                target.m_pieces.Add(prefab);
                source.m_pieces.Remove(prefab);
                addedPieces.Add(new AddedPiece(
                    prefab,
                    piece,
                    originalCategory,
                    source,
                    sourceIndex));
                ++added;
            }

            DisableOdinHammerRecipe();
            odinComplete = true;
            typeof(Player).GetMethod(
                "UpdateAvailablePiecesList",
                BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(player, null);
            log.LogInfo("BuildWorks added " + added +
                " OdinArchitect pieces to the vanilla Hammer catalog.");
        }

        public void Dispose()
        {
            if (targetTable && favoriteCategoryKeeper)
                targetTable.m_pieces?.Remove(favoriteCategoryKeeper);
            if (favoriteCategoryKeeper) UnityEngine.Object.Destroy(favoriteCategoryKeeper);
            foreach (Piece piece in reclassifiedPieces)
                if (piece) piece.m_category = Piece.PieceCategory.DeepNorth;
            foreach (AddedPiece added in addedPieces)
            {
                if (targetTable && targetTable.m_pieces != null)
                    targetTable.m_pieces.Remove(added.Prefab);
                if (added.Piece) added.Piece.m_category = added.OriginalCategory;
                if (added.SourceTable && added.SourceTable.m_pieces != null &&
                    !ContainsPrefab(added.SourceTable.m_pieces, added.Prefab))
                    added.SourceTable.m_pieces.Insert(
                        Math.Min(added.SourceIndex, added.SourceTable.m_pieces.Count),
                        added.Prefab);
            }

            if (odinHammerRecipe) odinHammerRecipe.m_enabled = odinHammerRecipeEnabled;
            addedPieces.Clear();
            reclassifiedPieces.Clear();
            targetTable = null;
            favoriteCategoryKeeper = null;
            odinHammerRecipe = null;
            BlueprintCategory = Piece.PieceCategory.Misc;
            FavoriteCategory = Piece.PieceCategory.Misc;
            categoryReady = false;
            categoriesNormalized = false;
            odinComplete = false;
            nextAttempt = 0f;
            OdinSourceGroups.Clear();
        }

        private int NormalizeValheimCategories(PieceTable table)
        {
            int count = 0;
            foreach (GameObject prefab in table.m_pieces ?? new List<GameObject>())
            {
                Piece piece = prefab ? prefab.GetComponent<Piece>() : null;
                if (!piece || piece.m_category != Piece.PieceCategory.DeepNorth) continue;
                reclassifiedPieces.Add(piece);
                piece.m_category = Piece.PieceCategory.BuildingWorkbench;
                ++count;
            }
            return count;
        }

        private void AddFavoriteCategoryKeeper(PieceTable table)
        {
            if (favoriteCategoryKeeper || !table) return;
            favoriteCategoryKeeper = new GameObject("BuildWorks_FavoriteCategory");
            favoriteCategoryKeeper.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(favoriteCategoryKeeper);
            Piece keeper = favoriteCategoryKeeper.AddComponent<Piece>();
            keeper.m_enabled = false;
            keeper.m_category = FavoriteCategory;
            table.m_pieces.Add(favoriteCategoryKeeper);
        }

        internal static string OdinSourceGroup(Piece piece)
        {
            return piece && OdinSourceGroups.TryGetValue(piece, out string group)
                ? group
                : null;
        }

        internal static void RefreshCategoryTabs()
        {
            if (TryResolveJotunn(
                out object manager,
                out _,
                out _,
                out MethodInfo refreshCategories))
                refreshCategories.Invoke(manager, null);
        }

        private static Piece.PieceCategory MapOdinCategory(string sourceName, Piece piece)
        {
            string source = (sourceName ?? string.Empty).ToLowerInvariant();
            if (source.Contains("decor")) return Piece.PieceCategory.Furniture;
            if (source.Contains("misc")) return Piece.PieceCategory.Misc;
            if (source.Contains("door")) return Piece.PieceCategory.BuildingWorkbench;

            string material = HammerCatalogOrganizer.Group(piece);
            return material == HammerCatalogOrganizer.StoneGroup ||
                material == HammerCatalogOrganizer.BlackMarbleGroup ||
                material == HammerCatalogOrganizer.GraustenGroup ||
                material == HammerCatalogOrganizer.MetalGroup
                ? Piece.PieceCategory.BuildingStonecutter
                : Piece.PieceCategory.BuildingWorkbench;
        }

        private static string NormalizeOdinGroup(string sourceName)
        {
            string name = string.IsNullOrWhiteSpace(sourceName)
                ? "OdinArchitect Pieces"
                : sourceName.Trim();
            return name.StartsWith("OdinArchitect", StringComparison.OrdinalIgnoreCase)
                ? name
                : "OdinArchitect " + name;
        }

        private void DisableOdinHammerRecipe()
        {
            if (odinHammerRecipe || ObjectDB.instance?.m_recipes == null) return;
            foreach (Recipe recipe in ObjectDB.instance.m_recipes)
            {
                if (!recipe || !recipe.m_item || !string.Equals(
                    recipe.m_item.gameObject.name,
                    OdinHammerPrefab,
                    StringComparison.Ordinal)) continue;
                odinHammerRecipe = recipe;
                odinHammerRecipeEnabled = recipe.m_enabled;
                recipe.m_enabled = false;
                return;
            }
        }

        private static Piece.PieceCategory AddCategory(
            object manager,
            MethodInfo method,
            string name)
        {
            object result = method.Invoke(manager, new object[] { VanillaPieceTable, name });
            return result is Piece.PieceCategory category
                ? category
                : Piece.PieceCategory.Misc;
        }

        private static bool TryResolveJotunn(
            out object manager,
            out MethodInfo addCategory,
            out MethodInfo getCategoryMap,
            out MethodInfo refreshCategories)
        {
            manager = null;
            addCategory = null;
            getCategoryMap = null;
            refreshCategories = null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType("Jotunn.Managers.PieceManager", false);
                if (type == null) continue;
                manager = type.GetProperty(
                    "Instance",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null, null);
                addCategory = type.GetMethod(
                    "AddPieceCategory",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(string), typeof(string) },
                    null);
                getCategoryMap = type.GetMethod(
                    "GetPieceCategoriesMap",
                    BindingFlags.Public | BindingFlags.Instance);
                refreshCategories = type.GetMethod(
                    "RefreshCategories",
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance);
                return manager != null && addCategory != null && getCategoryMap != null &&
                    refreshCategories != null;
            }
            return false;
        }

        private static bool IsAssemblyLoaded(string name)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                if (string.Equals(assembly.GetName().Name, name, StringComparison.Ordinal))
                    return true;
            return false;
        }

        private static bool ContainsPrefab(List<GameObject> pieces, GameObject prefab)
        {
            if (pieces == null) return false;
            foreach (GameObject candidate in pieces)
                if (candidate == prefab || candidate && string.Equals(
                    candidate.name,
                    prefab.name,
                    StringComparison.Ordinal)) return true;
            return false;
        }
    }

    internal static class HammerCatalogOrganizer
    {
        internal const string AllSource = "all";
        internal const string VanillaSource = "vanilla";
        internal const string BlueprintsGroup = "blueprints";
        internal const string ActionsGroup = "actions";
        internal const string WoodGroup = "wood";
        internal const string CoreWoodGroup = "core_wood";
        internal const string DarkWoodGroup = "dark_wood";
        internal const string AshWoodGroup = "ash_wood";
        internal const string StoneGroup = "stone";
        internal const string BlackMarbleGroup = "black_marble";
        internal const string GraustenGroup = "grausten";
        internal const string MetalGroup = "metal";
        internal const string GlassGroup = "glass";
        internal const string OtherGroup = "other";
        internal readonly struct SelectionSnapshot
        {
            public SelectionSnapshot(Piece.PieceCategory category, Piece piece)
            {
                Category = category;
                Piece = piece;
                PrefabName = piece ? piece.gameObject.name : string.Empty;
            }

            public Piece.PieceCategory Category { get; }
            public Piece Piece { get; }
            public string PrefabName { get; }
            public bool IsValid => Piece || !string.IsNullOrEmpty(PrefabName);
        }

        internal readonly struct GroupInfo
        {
            public GroupInfo(string name, int count, int page, Piece firstPiece)
            {
                Name = name;
                Count = count;
                Page = page;
                FirstPiece = firstPiece;
            }

            public string Name { get; }
            public int Count { get; }
            public int Page { get; }
            public Piece FirstPiece { get; }
        }

        internal sealed class PageInfo
        {
            public int Page;
            public int PageCount;
            public int Total;
            public readonly List<GroupInfo> Groups = new List<GroupInfo>();
        }

        internal readonly struct SourceInfo
        {
            public SourceInfo(string name, int count)
            {
                Name = name;
                Count = count;
            }

            public string Name { get; }
            public int Count { get; }
        }

        private const int IndexVisibleColumns = 12;
        private const int IndexVisibleRows = 4;
        private const int VanillaVisibleRows = 6;
        private static readonly Dictionary<long, List<Piece>> FullLists =
            new Dictionary<long, List<Piece>>();
        private static readonly Dictionary<long, List<Piece>> AllLists =
            new Dictionary<long, List<Piece>>();
        private static readonly Dictionary<long, int> Pages =
            new Dictionary<long, int>();
        private static readonly HashSet<long> RequestedPages = new HashSet<long>();
        private static readonly Dictionary<long, Piece> RequestedSelections =
            new Dictionary<long, Piece>();
        private static bool paginationEnabled = true;
        private static bool indexEnabled = true;
        private static string search = string.Empty;
        private static readonly Dictionary<long, string> SourceFilters =
            new Dictionary<long, string>();
        private static readonly MethodInfo UpdateAvailablePiecesMethod = typeof(Player).GetMethod(
            "UpdateAvailablePiecesList",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo NativePieceButtonsField = typeof(BuildUi).GetField(
            "m_pieceButtons",
            BindingFlags.Instance | BindingFlags.NonPublic);
        internal static bool NativeBuildUiActive => Hud.instance && Hud.instance.m_buildUi;
        internal static bool NativeBuildUiVisible => NativeBuildUiActive && Hud.InBuildUi();
        private static bool NativeCatalogActive =>
            NativeBuildUiActive && !HammerCatalogView.NativeIndexActive;

        public static SelectionSnapshot CaptureSelection(PieceTable table) => table
            ? new SelectionSnapshot(table.GetSelectedCategory(), table.GetSelectedPiece())
            : default(SelectionSnapshot);

        public static void Reorder(PieceTable table, SelectionSnapshot selection)
        {
            if (!table || !table.m_canRemovePieces) return;
            List<List<Piece>> available = PrecisionPlacementSession.AvailableBuildPieces(table);
            if (available == null) return;
            PopulateFavorites(available);
            int pageSize = PageSize;
            SelectionSnapshot restoredSelection = selection;
            for (int categoryIndex = 0; categoryIndex < available.Count; ++categoryIndex)
            {
                List<Piece> pieces = available[categoryIndex];
                if (pieces == null) continue;
                long key = Key(table, (Piece.PieceCategory)categoryIndex);
                if (NativeCatalogActive)
                {
                    pieces.Sort(ComparePieces);
                    FullLists[key] = new List<Piece>(pieces);
                    AllLists[key] = new List<Piece>(pieces);
                    Pages[key] = 0;
                    RequestedPages.Remove(key);
                    RequestedSelections.Remove(key);
                    continue;
                }
                if (!indexEnabled)
                {
                    FullLists[key] = new List<Piece>(pieces);
                    AllLists[key] = new List<Piece>(pieces);
                }
                else
                {
                    pieces.Sort(ComparePieces);
                    var filtered = new List<Piece>(pieces);
                    AllLists[key] = new List<Piece>(pieces);
                    if (SourceFilters.TryGetValue(key, out string source) &&
                        !string.IsNullOrEmpty(source) && source != AllSource)
                        filtered.RemoveAll(piece => !MatchesSource(piece, source));
                    if (!string.IsNullOrEmpty(search))
                        filtered.RemoveAll(piece => !MatchesSearch(piece, search));
                    FullLists[key] = filtered;
                    pieces.Clear();
                    pieces.AddRange(filtered);
                }

                if (!paginationEnabled) continue;

                List<Piece> full = FullLists[key];
                int pageCount = Math.Max(1, (full.Count + pageSize - 1) / pageSize);
                int page = Pages.TryGetValue(key, out int saved) ? saved : 0;
                bool requested = RequestedPages.Remove(key);
                if (!requested && selection.IsValid &&
                    selection.Category == (Piece.PieceCategory)categoryIndex)
                {
                    int selectedFullIndex = FindPiece(full, selection);
                    if (selectedFullIndex >= 0) page = selectedFullIndex / pageSize;
                }
                page = Mathf.Clamp(page, 0, pageCount - 1);
                Pages[key] = page;
                ApplyPage(pieces, full, page);
                if (RequestedSelections.TryGetValue(key, out Piece requestedPiece))
                {
                    RequestedSelections.Remove(key);
                    if ((Piece.PieceCategory)categoryIndex == table.GetSelectedCategory())
                        restoredSelection = new SelectionSnapshot(
                            (Piece.PieceCategory)categoryIndex,
                            requestedPiece);
                }
            }

            RestoreSelection(table, restoredSelection);
        }

        public static bool ContainsFull(PieceTable table, Piece piece)
        {
            if (!table || !piece) return false;
            foreach (KeyValuePair<long, List<Piece>> entry in FullLists)
                if ((int)(entry.Key >> 32) == table.GetInstanceID() &&
                    entry.Value.Contains(piece)) return true;
            return false;
        }

        public static bool ContainsAvailable(PieceTable table, Piece piece)
        {
            if (!table || !piece) return false;
            foreach (KeyValuePair<long, List<Piece>> entry in AllLists)
                if ((int)(entry.Key >> 32) == table.GetInstanceID() &&
                    entry.Value.Contains(piece)) return true;
            return false;
        }

        public static string AvailabilitySignature(PieceTable table)
        {
            if (!table) return "0:0:0";
            int count = 0;
            long sum = 0;
            int xor = 0;
            foreach (KeyValuePair<long, List<Piece>> entry in AllLists)
            {
                if ((int)(entry.Key >> 32) != table.GetInstanceID()) continue;
                foreach (Piece piece in entry.Value)
                {
                    if (!piece || piece.gameObject.name.StartsWith(
                        HammerBlueprintPieceRegistry.PrefabPrefix,
                        StringComparison.Ordinal)) continue;
                    int id = piece.GetInstanceID();
                    ++count;
                    sum += id;
                    xor ^= id;
                }
            }
            return count + ":" + sum + ":" + xor;
        }

        public static bool EnsureVisible(PieceTable table, Piece piece)
        {
            if (!table || !piece) return false;
            foreach (KeyValuePair<long, List<Piece>> entry in AllLists)
            {
                if ((int)(entry.Key >> 32) != table.GetInstanceID()) continue;
                long key = entry.Key;
                Piece.PieceCategory category = (Piece.PieceCategory)(int)(uint)key;
                if (!entry.Value.Contains(piece)) continue;
                FullLists.TryGetValue(key, out List<Piece> full);
                int index = full?.IndexOf(piece) ?? -1;
                List<Piece> visible = PrecisionPlacementSession.AvailableBuildPieces(
                    table,
                    category);
                if (visible == null) return false;
                if (NativeCatalogActive) return visible.Contains(piece);
                if (index < 0)
                {
                    visible.Clear();
                    visible.Add(piece);
                    return true;
                }
                if (!paginationEnabled)
                {
                    visible.Clear();
                    visible.AddRange(full);
                    return true;
                }
                int page = index / PageSize;
                Pages[key] = page;
                ApplyPage(visible, full, page);
                return true;
            }
            return false;
        }

        public static PageInfo GetPageInfo(PieceTable table, Piece.PieceCategory category)
        {
            var info = new PageInfo { PageCount = 1 };
            if (!table || !FullLists.TryGetValue(Key(table, category), out List<Piece> full))
                return info;
            if (NativeCatalogActive)
            {
                info.Total = full.Count;
                return info;
            }
            long key = Key(table, category);
            info.Page = Pages.TryGetValue(key, out int page) ? page : 0;
            info.PageCount = Math.Max(1, (full.Count + PageSize - 1) / PageSize);
            info.Total = full.Count;
            string previous = null;
            int count = 0;
            int firstIndex = 0;
            for (int index = 0; index <= full.Count; ++index)
            {
                string current = index < full.Count ? Group(full[index]) : null;
                if (index > 0 && !string.Equals(current, previous, StringComparison.Ordinal))
                {
                    info.Groups.Add(new GroupInfo(
                        previous,
                        count,
                        firstIndex / PageSize,
                        full[firstIndex]));
                    count = 0;
                    firstIndex = index;
                }
                previous = current;
                ++count;
            }
            return info;
        }

        public static int GetTotalCount(PieceTable table, Piece.PieceCategory category)
        {
            if (!table) return 0;
            long key = Key(table, category);
            if (AllLists.TryGetValue(key, out List<Piece> all)) return all.Count;
            return FullLists.TryGetValue(key, out List<Piece> full) ? full.Count : 0;
        }

        public static void GoToPage(
            PieceTable table,
            Piece.PieceCategory category,
            int page,
            Player player)
        {
            if (!table || !player) return;
            long key = Key(table, category);
            Pages[key] = Math.Max(0, page);
            RequestedPages.Add(key);
            UpdateAvailablePiecesMethod?.Invoke(player, null);
        }

        public static void GoToGroup(
            PieceTable table,
            Piece.PieceCategory category,
            GroupInfo group,
            Player player)
        {
            if (!table || !player || !group.FirstPiece) return;
            long key = Key(table, category);
            Pages[key] = group.Page;
            RequestedPages.Add(key);
            RequestedSelections[key] = group.FirstPiece;
            UpdateAvailablePiecesMethod?.Invoke(player, null);
        }

        public static void HandlePaging(Player player)
        {
            Hud hud = Hud.instance;
            if (NativeCatalogActive || !player || !hud || !Hud.IsPieceSelectionVisible() ||
                HammerCatalogView.BlocksCatalogPaging) return;
            PieceTable table = PrecisionPlacementSession.GetBuildPieceTable(player);
            if (!table) return;
            int direction = Input.GetKeyDown(KeyCode.PageDown) ? 1 :
                Input.GetKeyDown(KeyCode.PageUp) ? -1 : 0;
            if (direction == 0)
            {
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(wheel) > 0.01f) direction = wheel < 0f ? 1 : -1;
            }
            if (direction == 0) return;
            if (HammerCatalogView.HandleBlueprintPaging(table, direction)) return;
            Piece.PieceCategory category = table.GetSelectedCategory();
            PageInfo info = GetPageInfo(table, category);
            if (info.PageCount <= 1) return;
            GoToPage(
                table,
                category,
                (info.Page + direction + info.PageCount) % info.PageCount,
                player);
        }

        public static void DisablePagination(PieceTable table)
        {
            paginationEnabled = false;
            if (!table) return;
            foreach (KeyValuePair<long, List<Piece>> entry in FullLists)
            {
                if ((int)(entry.Key >> 32) != table.GetInstanceID()) continue;
                Piece.PieceCategory category = (Piece.PieceCategory)(int)(uint)entry.Key;
                List<Piece> visible = PrecisionPlacementSession.AvailableBuildPieces(
                    table,
                    category);
                if (visible == null) continue;
                visible.Clear();
                visible.AddRange(entry.Value);
            }
            RequestedPages.Clear();
            RequestedSelections.Clear();
        }

        public static void Reset()
        {
            FullLists.Clear();
            AllLists.Clear();
            Pages.Clear();
            RequestedPages.Clear();
            RequestedSelections.Clear();
            paginationEnabled = true;
            indexEnabled = true;
            search = string.Empty;
            SourceFilters.Clear();
        }

        public static bool IndexEnabled => indexEnabled;

        public static int VisibleColumnCount => indexEnabled
            ? IndexVisibleColumns
            : Math.Max(1, PieceTable.m_gridWidth);

        public static int VisibleRowCount => indexEnabled
            ? IndexVisibleRows
            : VanillaVisibleRows;

        public static string Search => search;

        public static IReadOnlyList<SourceInfo> GetSources(
            PieceTable table,
            Piece.PieceCategory category)
        {
            var result = new List<SourceInfo>();
            if (!table || !AllLists.TryGetValue(Key(table, category), out List<Piece> all))
                return result;
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Piece piece in all)
            {
                string source = UnifiedHammerCatalog.OdinSourceGroup(piece) ?? VanillaSource;
                counts[source] = counts.TryGetValue(source, out int count) ? count + 1 : 1;
            }
            if (counts.Count <= 1) return result;
            result.Add(new SourceInfo(AllSource, all.Count));
            if (counts.TryGetValue(VanillaSource, out int vanilla))
                result.Add(new SourceInfo(VanillaSource, vanilla));
            foreach (KeyValuePair<string, int> entry in counts)
                if (entry.Key != VanillaSource)
                    result.Add(new SourceInfo(entry.Key, entry.Value));
            return result;
        }

        public static string GetSourceFilter(
            PieceTable table,
            Piece.PieceCategory category) =>
            table && SourceFilters.TryGetValue(Key(table, category), out string value)
                ? value
                : AllSource;

        public static void SetSourceFilter(
            PieceTable table,
            Piece.PieceCategory category,
            string value,
            Player player)
        {
            if (!table) return;
            SourceFilters[Key(table, category)] = value ?? AllSource;
            Pages[Key(table, category)] = 0;
            RequestRefresh(player);
        }

        public static void SetIndexEnabled(bool enabled, Player player)
        {
            if (indexEnabled == enabled) return;
            indexEnabled = enabled;
            RequestRefresh(player);
        }

        public static void OpenBlueprints(PieceTable table, Player player)
        {
            if (!table || !player || !UnifiedHammerCatalog.BlueprintCategoryReady) return;
            indexEnabled = true;
            table.SetCategory(UnifiedHammerCatalog.BlueprintCategory);
            RequestRefresh(player);
        }

        public static bool IsFavorite(Piece piece) => piece && Hud.instance &&
            Hud.instance.m_buildUi && Hud.instance.m_buildUi.IsFavoritePiece(piece);

        public static void ToggleFavorite(Piece piece, Player player)
        {
            BuildUi buildUi = Hud.instance?.m_buildUi;
            if (!piece || !player || !buildUi || piece.m_removePiece || piece.m_repairPiece)
                return;
            if (buildUi.IsFavoritePiece(piece)) buildUi.RemoveFromFavorites(piece);
            else buildUi.ToggleFavorite(piece, -1);
            RequestRefresh(player);
        }

        public static void RefreshFavoriteButtons(BuildUi buildUi, string prefabName)
        {
            var buttons = NativePieceButtonsField?.GetValue(buildUi) as IEnumerable;
            if (buttons == null || string.IsNullOrEmpty(prefabName)) return;
            foreach (object value in buttons)
            {
                var button = value as BuildUiPieceButton;
                Piece piece = button ? button.Piece : null;
                if (piece && piece.gameObject.name == prefabName)
                    button.RefreshFavorite();
            }
        }

        public static void EnsureCategoryNavigation(
            Player player,
            Piece.PieceCategory categoryBeforeNativeInput)
        {
            Hud hud = Hud.instance;
            if (NativeCatalogActive || !player || !hud || !Hud.IsPieceSelectionVisible() ||
                HammerCatalogView.BlocksCatalogPaging) return;
            int direction = Input.GetKeyDown(KeyCode.Q) ? -1 :
                Input.GetKeyDown(KeyCode.E) ? 1 : 0;
            if (direction == 0) return;
            PieceTable table = PrecisionPlacementSession.GetBuildPieceTable(player);
            if (!table || table.GetSelectedCategory() != categoryBeforeNativeInput) return;
            if (direction < 0) table.PrevCategory();
            else table.NextCategory();
            HammerCatalogView.Invalidate();
        }

        public static bool MoveIndexSelection(
            PieceTable table,
            int horizontal,
            int vertical)
        {
            if (!indexEnabled || !HammerCatalogView.IndexGridActive ||
                !table || !table.m_canRemovePieces) return false;
            List<Piece> pieces = PrecisionPlacementSession.AvailableBuildPieces(
                table,
                table.GetSelectedCategory());
            if (pieces == null || pieces.Count <= 1) return true;

            int current = pieces.IndexOf(table.GetSelectedPiece());
            if (current < 0) current = 0;
            int columns = IndexVisibleColumns;
            int target = current;
            if (horizontal != 0)
            {
                int rowStart = current / columns * columns;
                int rowCount = Math.Min(columns, pieces.Count - rowStart);
                int column = current - rowStart;
                target = rowStart + (column + horizontal + rowCount) % rowCount;
            }
            else if (vertical != 0)
            {
                int column = current % columns;
                int rows = (pieces.Count + columns - 1) / columns;
                int row = current / columns;
                for (int step = 0; step < rows; ++step)
                {
                    row = (row + vertical + rows) % rows;
                    int candidate = row * columns + column;
                    if (candidate >= pieces.Count) continue;
                    target = candidate;
                    break;
                }
            }

            int nativeWidth = Math.Max(1, PieceTable.m_gridWidth);
            table.SetSelected(new Vector2Int(target % nativeWidth, target / nativeWidth));
            return true;
        }

        public static void SetSearch(string value, Player player)
        {
            value = (value ?? string.Empty).Trim();
            if (string.Equals(search, value, StringComparison.OrdinalIgnoreCase)) return;
            search = value;
            Pages.Clear();
            RequestRefresh(player);
        }

        public static string Group(Piece piece)
        {
            if (!piece) return OtherGroup;
            if (piece.gameObject.name.StartsWith(
                HammerBlueprintPieceRegistry.PrefabPrefix,
                StringComparison.Ordinal)) return BlueprintsGroup;

            string text = (piece.gameObject.name + " " + piece.m_name).ToLowerInvariant();
            foreach (Piece.Requirement requirement in piece.m_resources ??
                Array.Empty<Piece.Requirement>())
            {
                if (!requirement?.m_resItem) continue;
                text += " " + requirement.m_resItem.gameObject.name.ToLowerInvariant();
                text += " " + requirement.m_resItem.m_itemData.m_shared.m_name.ToLowerInvariant();
            }

            if (text.Contains("repair")) return ActionsGroup;
            if (text.Contains("ashwood") || text.Contains("asksvin")) return AshWoodGroup;
            if (text.Contains("darkwood")) return DarkWoodGroup;
            if (text.Contains("blackmarble")) return BlackMarbleGroup;
            if (text.Contains("grausten")) return GraustenGroup;
            if (text.Contains("stone") || text.Contains("marble")) return StoneGroup;
            if (text.Contains("iron") || text.Contains("bronze") ||
                text.Contains("copper") || text.Contains("metal")) return MetalGroup;
            if (text.Contains("crystal") || text.Contains("glass")) return GlassGroup;
            if (text.Contains("corewood") || text.Contains("roundlog")) return CoreWoodGroup;
            if (text.Contains("wood")) return WoodGroup;
            return OtherGroup;
        }

        private static int ComparePieces(Piece left, Piece right)
        {
            int group = GroupRank(Group(left)).CompareTo(GroupRank(Group(right)));
            if (group != 0) return group;
            int shape = ShapeRank(left).CompareTo(ShapeRank(right));
            if (shape != 0) return shape;
            string leftName = left ? left.gameObject.name : string.Empty;
            string rightName = right ? right.gameObject.name : string.Empty;
            return string.Compare(leftName, rightName, StringComparison.Ordinal);
        }

        private static bool MatchesSearch(Piece piece, string value)
        {
            if (!piece) return false;
            string localized = Localization.instance != null
                ? Localization.instance.Localize(piece.m_name)
                : piece.m_name;
            string text = piece.gameObject.name + " " + piece.m_name + " " + localized;
            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool MatchesSource(Piece piece, string source)
        {
            string actual = UnifiedHammerCatalog.OdinSourceGroup(piece);
            return source == VanillaSource
                ? string.IsNullOrEmpty(actual)
                : string.Equals(actual, source, StringComparison.Ordinal);
        }

        private static void RequestRefresh(Player player)
        {
            if (!player) return;
            UpdateAvailablePiecesMethod?.Invoke(player, null);
            HammerCatalogView.Invalidate();
        }

        private static void PopulateFavorites(List<List<Piece>> available)
        {
            int category = (int)UnifiedHammerCatalog.FavoriteCategory;
            if (NativeCatalogActive || !UnifiedHammerCatalog.FavoriteCategoryReady ||
                category < 0 || category >= available.Count) return;
            List<Piece> favorites = available[category];
            if (favorites == null) return;
            favorites.Clear();
            var seen = new HashSet<Piece>();
            for (int categoryIndex = 0; categoryIndex < available.Count; ++categoryIndex)
            {
                if (categoryIndex == category || available[categoryIndex] == null) continue;
                foreach (Piece piece in available[categoryIndex])
                    if (IsFavorite(piece) && seen.Add(piece)) favorites.Add(piece);
            }
        }

        private static void ApplyPage(List<Piece> destination, List<Piece> full, int page)
        {
            destination.Clear();
            int start = page * PageSize;
            int end = Math.Min(full.Count, start + PageSize);
            for (int index = start; index < end; ++index)
                destination.Add(full[index]);
        }

        private static void RestoreSelection(PieceTable table, SelectionSnapshot selection)
        {
            Piece.PieceCategory category = table.GetSelectedCategory();
            List<Piece> visible = PrecisionPlacementSession.AvailableBuildPieces(table, category);
            if (visible == null || visible.Count == 0) return;
            int index = selection.Category == category ? FindPiece(visible, selection) : -1;
            if (index < 0) index = 0;
            int width = Math.Max(1, PieceTable.m_gridWidth);
            table.SetSelected(new Vector2Int(index % width, index / width));
        }

        private static int FindPiece(List<Piece> pieces, SelectionSnapshot selection)
        {
            int index = selection.Piece ? pieces.IndexOf(selection.Piece) : -1;
            if (index >= 0 || string.IsNullOrEmpty(selection.PrefabName)) return index;
            for (int candidate = 0; candidate < pieces.Count; ++candidate)
                if (pieces[candidate] && string.Equals(
                    pieces[candidate].gameObject.name,
                    selection.PrefabName,
                    StringComparison.Ordinal)) return candidate;
            return -1;
        }

        private static int GroupRank(string group)
        {
            switch (group)
            {
                case BlueprintsGroup: return 0;
                case ActionsGroup: return 1;
                case WoodGroup: return 2;
                case CoreWoodGroup: return 3;
                case DarkWoodGroup: return 4;
                case AshWoodGroup: return 5;
                case StoneGroup: return 6;
                case BlackMarbleGroup: return 7;
                case GraustenGroup: return 8;
                case MetalGroup: return 9;
                case GlassGroup: return 10;
                default: return 11;
            }
        }

        private static int ShapeRank(Piece piece)
        {
            if (!piece) return 99;
            string name = (piece.gameObject.name + " " + piece.m_name).ToLowerInvariant();
            if (name.Contains("beam") || name.Contains("pole") || name.Contains("pillar"))
                return 0;
            if (name.Contains("floor") || name.Contains("tile")) return 1;
            if (name.Contains("wall")) return 2;
            if (name.Contains("roof")) return 3;
            if (name.Contains("stair") || name.Contains("ladder")) return 4;
            if (name.Contains("door") || name.Contains("gate")) return 5;
            return 6;
        }

        private static int PageSize => VisibleColumnCount * VisibleRowCount;

        private static long Key(PieceTable table, Piece.PieceCategory category) =>
            ((long)table.GetInstanceID() << 32) ^ (uint)(int)category;
    }

    internal static class HammerCatalogView
    {
        private const string Prefix = "BuildWorks_Catalog_";
        private static readonly FieldInfo PieceIconsField = typeof(Hud).GetField(
            "m_pieceIcons",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo IconRootField = typeof(Hud).GetNestedType(
            "PieceIconData",
            BindingFlags.NonPublic)?.GetField(
                "m_go",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo NativeTabButtonsField = typeof(BuildUi).GetField(
            "m_tabButtons",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo NativeFavoriteStarField =
            typeof(BuildUiPieceButton).GetField(
                "m_favoriteStar",
                BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly List<GameObject> Decorations = new List<GameObject>();
        private static readonly List<HammerCatalogFavoriteClick> FavoriteClicks =
            new List<HammerCatalogFavoriteClick>();
        private static readonly List<RectState> PieceIconStates = new List<RectState>();
        private static readonly List<GameObject> PieceIconRoots = new List<GameObject>();
        private static readonly List<bool> PieceIconActiveStates = new List<bool>();
        private static readonly List<GameObject> NativeRootChildren = new List<GameObject>();
        private static readonly List<bool> NativeRootChildStates = new List<bool>();
        private static RectState maskState;
        private static RectState listRootState;
        private static bool listRootWasActive;
        private static GridLayoutGroup nativePieceGrid;
        private static GridLayoutGroup.Constraint nativePieceGridConstraint;
        private static int nativePieceGridConstraintCount;
        private static int appliedHudId;
        private static int appliedCategory = -1;
        private static int appliedSignature;
        private static int appliedCount = -1;
        private static int appliedPage = -1;
        private static CompositeBlueprintStore blueprintStore;
        private static HammerBlueprintPieceRegistry blueprintRegistry;
        private static Action<CompositeBlueprintStore.Blueprint, bool> blueprintAction;
        private static Action blueprintCreateAction;
        private static Action blueprintWorldSelectionAction;
        private static CompositeBlueprintStore.Blueprint selectedBlueprint;
        private static CompositeBlueprintStore.Blueprint contextBlueprint;
        private static GameObject contextMenu;
        private static readonly List<Button> ContextButtons = new List<Button>();
        private static string lastBlueprintClickId;
        private static float lastBlueprintClickAt = float.NegativeInfinity;
        private const string AllBlueprintCategories = "all";
        private static string blueprintCategory = AllBlueprintCategories;
        private static int blueprintPage;
        private static int blueprintsPerPage = 8;
        private static string deleteConfirmationId;
        private static bool showResources;
        private static float layoutRailWidth;
        private static float layoutWindowWidth;
        private static float layoutWindowHeight;
        private static int layoutSourceRows = 1;
        private static float layoutCategoryBottom = 42f;
        private static BlueprintThumbnailRenderer.PreviewSession previewSession;
        private static CompositeBlueprintStore.Blueprint previewBlueprint;
        private static float previewYaw;
        private static float previewPitch;
        private static float previewZoom;
        private static Vector2 previewMouse;
        private static bool previewDragging;
        private static bool indexGridActive;
        private static bool nativeIndexMode;
        private static bool nativeBuildUiWasVisible;
        private static bool legacyWindowWasActive;
        private static GameObject nativeIndexEntry;

        public static bool IndexGridActive => indexGridActive;
        public static bool NativeIndexActive => nativeIndexMode;

        internal static bool ShouldCaptureBlueprintContext()
        {
            if (!indexGridActive || !Input.GetMouseButtonDown(1)) return false;
            EventSystem eventSystem = EventSystem.current;
            if (!eventSystem) return false;
            var pointer = new PointerEventData(eventSystem) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            foreach (RaycastResult hit in hits)
                if (hit.gameObject &&
                    hit.gameObject.GetComponentInParent<HammerCatalogPointerClick>())
                    return true;
            return false;
        }

        public static void Configure(
            CompositeBlueprintStore store,
            HammerBlueprintPieceRegistry registry,
            Action<CompositeBlueprintStore.Blueprint, bool> action,
            Action createAction,
            Action worldSelectionAction = null)
        {
            blueprintStore = store;
            blueprintRegistry = registry;
            blueprintAction = action;
            blueprintCreateAction = createAction;
            blueprintWorldSelectionAction = worldSelectionAction;
            Invalidate();
        }

        public static void Unconfigure(CompositeBlueprintStore store)
        {
            if (blueprintStore != store) return;
            ExitNativeIndex(Hud.instance, Player.m_localPlayer);
            ClosePreview();
            Clear();
            blueprintStore = null;
            blueprintRegistry = null;
            blueprintAction = null;
            blueprintCreateAction = null;
            blueprintWorldSelectionAction = null;
        }

        public static void Invalidate()
        {
            appliedHudId = 0;
            appliedSignature = 0;
        }

        public static void Tick(Hud hud, Player player)
        {
            if (HammerCatalogOrganizer.NativeBuildUiActive)
            {
                if (!hud || !player || !HammerCatalogOrganizer.NativeBuildUiVisible ||
                    !PrecisionPlacementSession.UsesHammerPieceTable(player))
                {
                    nativeBuildUiWasVisible = false;
                    ExitNativeIndex(hud, player);
                    ClosePreview();
                    if (appliedHudId != 0) Clear();
                    return;
                }
                if (!nativeBuildUiWasVisible)
                    OpenDefaultIndex(hud, player);
                HandlePreviewInput();
                if (nativeIndexMode)
                {
                    EnterNativeIndex(hud);
                    Apply(hud, player);
                }
                else
                {
                    ApplyNative(hud, player);
                }
                return;
            }
            nativeBuildUiWasVisible = false;
            if (!hud || !player || !Hud.IsPieceSelectionVisible())
            {
                ClosePreview();
                if (appliedHudId != 0) Clear();
                return;
            }
            HandlePreviewInput();
            Apply(hud, player);
        }

        internal static void OpenDefaultIndex(Hud hud, Player player)
        {
            if (nativeBuildUiWasVisible || !hud || !player ||
                !HammerCatalogOrganizer.NativeBuildUiVisible ||
                !PrecisionPlacementSession.UsesHammerPieceTable(player)) return;
            nativeBuildUiWasVisible = true;
            nativeIndexMode = true;
            HammerCatalogOrganizer.SetIndexEnabled(true, player);
            EnterNativeIndex(hud);
            Invalidate();
        }

        internal static void MarkNativeBuildUiClosed()
        {
            nativeBuildUiWasVisible = false;
        }

        public static void Apply(Hud hud, Player player)
        {
            if (HammerCatalogOrganizer.NativeBuildUiActive && !nativeIndexMode)
            {
                ApplyNative(hud, player);
                return;
            }
            if (!hud || !player || !Hud.IsPieceSelectionVisible() ||
                !PrecisionPlacementSession.UsesHammerPieceTable(player))
            {
                Clear();
                return;
            }
            List<Piece> pieces = player.GetBuildPieces();
            IList icons = PieceIconsField?.GetValue(hud) as IList;
            int count = Math.Min(pieces?.Count ?? 0, icons?.Count ?? 0);
            PieceTable table = PrecisionPlacementSession.GetBuildPieceTable(player);
            int category = table ? (int)table.GetSelectedCategory() : -1;
            HammerCatalogOrganizer.PageInfo pageInfo = table
                ? HammerCatalogOrganizer.GetPageInfo(table, table.GetSelectedCategory())
                : new HammerCatalogOrganizer.PageInfo();
            int signature = 17;
            unchecked
            {
                for (int index = 0; index < count; ++index)
                {
                    Piece piece = pieces[index];
                    GameObject iconRoot = IconRootField?.GetValue(icons[index]) as GameObject;
                    signature = signature * 31 + (piece ? piece.GetInstanceID() : 0);
                    signature = signature * 31 + (iconRoot ? iconRoot.GetInstanceID() : 0);
                }
                signature = signature * 31 + pageInfo.Total;
                signature = signature * 31 + pageInfo.PageCount;
                signature = signature * 31 + Screen.width;
                signature = signature * 31 + Screen.height;
                Rect safeArea = Screen.safeArea;
                signature = signature * 31 + safeArea.x.GetHashCode();
                signature = signature * 31 + safeArea.y.GetHashCode();
                signature = signature * 31 + safeArea.width.GetHashCode();
                signature = signature * 31 + safeArea.height.GetHashCode();
                Canvas layoutCanvas = hud.m_pieceSelectionWindow
                    ? hud.m_pieceSelectionWindow.GetComponentInParent<Canvas>()
                    : null;
                RectTransform layoutCanvasRect = layoutCanvas
                    ? layoutCanvas.GetComponent<RectTransform>()
                    : null;
                signature = signature * 31 +
                    (layoutCanvas ? layoutCanvas.scaleFactor.GetHashCode() : 0);
                signature = signature * 31 +
                    (layoutCanvasRect ? layoutCanvasRect.rect.width.GetHashCode() : 0);
                signature = signature * 31 +
                    (layoutCanvasRect ? layoutCanvasRect.rect.height.GetHashCode() : 0);
                signature = signature * 31 + HammerCatalogOrganizer.Search.GetHashCode();
                signature = signature * 31 +
                    (HammerCatalogOrganizer.IndexEnabled ? 1 : 0);
                signature = signature * 31 + HammerCatalogOrganizer.GetSourceFilter(
                    table,
                    table.GetSelectedCategory()).GetHashCode();
                if (blueprintStore != null)
                {
                    foreach (CompositeBlueprintStore.Blueprint blueprint in blueprintStore.All())
                    {
                        signature = signature * 31 + blueprint.id.GetHashCode();
                        signature = signature * 31 + blueprint.name.GetHashCode();
                        signature = signature * 31 + blueprint.category.GetHashCode();
                    }
                }
            }
            if (appliedHudId == hud.GetInstanceID() && appliedCategory == category &&
                appliedSignature == signature && appliedCount == count &&
                appliedPage == pageInfo.Page &&
                DecorationsAreAlive())
            {
                NormalizeNativeCategoryTabs(hud, table);
                return;
            }

            Clear();
            if (!HammerCatalogOrganizer.IndexEnabled)
            {
                NormalizeNativeCategoryTabs(hud, table);
                AddVanillaModeControls(hud, player, table, pageInfo);
                appliedHudId = hud.GetInstanceID();
                appliedCategory = category;
                appliedSignature = signature;
                appliedCount = count;
                appliedPage = pageInfo.Page;
                return;
            }
            ApplyLayout(hud, table);
            ArrangeIndexGrid(icons);
            bool blueprints = UnifiedHammerCatalog.BlueprintCategoryReady &&
                table.GetSelectedCategory() == UnifiedHammerCatalog.BlueprintCategory;
            if (!blueprints) AddFavoriteControls(hud, player, pieces, count);
            NormalizeNativeCategoryTabs(hud, table);
            AddIndexRail(hud, player, table, table.GetSelectedCategory(), pageInfo, blueprints);
            AddModeToggle(hud, player, true);
            AddSourceTabs(hud, player, table, table.GetSelectedCategory());
            if (blueprints) AddBlueprintLibrary(hud, player);
            if (previewSession != null) AddPreviewEditor(hud);
            appliedHudId = hud.GetInstanceID();
            appliedCategory = category;
            appliedSignature = signature;
            appliedCount = count;
            appliedPage = pageInfo.Page;
        }

        private static void ApplyNative(Hud hud, Player player)
        {
            if (!hud || !player || !HammerCatalogOrganizer.NativeBuildUiVisible ||
                !PrecisionPlacementSession.UsesHammerPieceTable(player))
            {
                if (appliedHudId != 0) Clear();
                return;
            }
            RectTransform root = hud.m_buildUi.transform as RectTransform;
            if (!root) return;
            int signature = 17;
            unchecked
            {
                signature = signature * 31 + Screen.width;
                signature = signature * 31 + Screen.height;
                foreach (CompositeBlueprintStore.Blueprint blueprint in
                    blueprintStore?.All() ?? Array.Empty<CompositeBlueprintStore.Blueprint>())
                {
                    signature = signature * 31 + blueprint.id.GetHashCode();
                    signature = signature * 31 + blueprint.name.GetHashCode();
                    signature = signature * 31 + blueprint.category.GetHashCode();
                }
                signature = signature * 31 + HammerCatalogOrganizer.Search.GetHashCode();
                signature = signature * 31 + blueprintCategory.GetHashCode();
                signature = signature * 31 + blueprintPage;
            }
            if (appliedHudId == hud.GetInstanceID() && appliedSignature == signature &&
                DecorationsAreAlive())
            {
                PositionNativeIndexEntry(hud, root);
                return;
            }

            Clear();
            AddNativeIndexEntry(hud, player, root);
            appliedHudId = hud.GetInstanceID();
            appliedSignature = signature;
        }

        private static void AddNativeIndexEntry(Hud hud, Player player, RectTransform root)
        {
            nativeIndexEntry = AddButton(
                hud,
                root,
                "NativeIndexEntry",
                "BUILDWORKS",
                Vector2.zero,
                new Vector2(220f, 34f),
                false,
                () =>
                {
                    nativeIndexMode = true;
                    EnterNativeIndex(hud);
                    HammerCatalogOrganizer.OpenBlueprints(
                        PrecisionPlacementSession.GetBuildPieceTable(player),
                        player);
                });
            PositionNativeIndexEntry(hud, root);
            ApplyNativeButtonStyle(nativeIndexEntry, hud.m_buildUi);
            nativeIndexEntry.transform.SetAsLastSibling();
        }

        private static void PositionNativeIndexEntry(Hud hud, RectTransform root)
        {
            if (!nativeIndexEntry || !hud || !hud.m_buildUi || !root) return;
            RectTransform parent = nativeIndexEntry.transform.parent as RectTransform;
            if (!parent) return;
            RectTransform window = hud.m_pieceSelectionWindow
                ? hud.m_pieceSelectionWindow.transform as RectTransform
                : null;
            Bounds frame = RectTransformUtility.CalculateRelativeRectTransformBounds(
                parent,
                window ? window : root);
            float width = Mathf.Clamp(frame.size.x * 0.26f, 220f, 340f);
            RectTransform rect = (RectTransform)nativeIndexEntry.transform;
            rect.anchorMin = rect.anchorMax = parent.pivot;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(width, 34f);
            rect.localPosition = new Vector3(
                frame.center.x,
                frame.min.y - 8f,
                rect.localPosition.z);
            nativeIndexEntry.SetActive(true);
            nativeIndexEntry.transform.SetAsLastSibling();
        }

        private static void ApplyNativeButtonStyle(GameObject target, BuildUi buildUi)
        {
            IList nativeButtons = NativeTabButtonsField?.GetValue(buildUi) as IList;
            if (nativeButtons == null || nativeButtons.Count == 0 ||
                !(nativeButtons[0] is Button template) || !template) return;
            Button button = target.GetComponent<Button>();
            Image image = target.GetComponent<Image>();
            Image templateImage = template.targetGraphic as Image;
            if (templateImage)
            {
                image.sprite = templateImage.sprite;
                image.type = templateImage.type;
                image.material = templateImage.material;
                image.color = templateImage.color;
            }
            button.colors = template.colors;
            button.transition = template.transition;
            TMP_Text sourceText = template.GetComponentInChildren<TMP_Text>(true);
            TMP_Text targetText = target.GetComponentInChildren<TMP_Text>(true);
            if (!sourceText || !targetText) return;
            targetText.font = sourceText.font;
            targetText.fontSharedMaterial = sourceText.fontSharedMaterial;
            targetText.color = sourceText.color;
            targetText.fontSize = sourceText.fontSize;
            targetText.fontStyle = sourceText.fontStyle;
        }

        private static void EnterNativeIndex(Hud hud)
        {
            if (!hud || !hud.m_buildUi || !hud.m_pieceSelectionWindow ||
                NativeRootChildren.Count > 0) return;
            RectTransform root = hud.m_buildUi.transform as RectTransform;
            if (!root) return;
            Transform legacyBranch = hud.m_pieceSelectionWindow.transform;
            while (legacyBranch.parent && legacyBranch.parent != root)
                legacyBranch = legacyBranch.parent;
            legacyWindowWasActive = hud.m_pieceSelectionWindow.activeSelf;
            for (int index = 0; index < root.childCount; ++index)
            {
                GameObject child = root.GetChild(index).gameObject;
                if (child == legacyBranch.gameObject) continue;
                NativeRootChildren.Add(child);
                NativeRootChildStates.Add(child.activeSelf);
                child.SetActive(false);
            }
            hud.m_pieceSelectionWindow.SetActive(true);
        }

        private static void ExitNativeIndex(Hud hud, Player player)
        {
            if (!nativeIndexMode && NativeRootChildren.Count == 0) return;
            nativeIndexMode = false;
            for (int index = 0; index < NativeRootChildren.Count; ++index)
                if (NativeRootChildren[index])
                    NativeRootChildren[index].SetActive(NativeRootChildStates[index]);
            NativeRootChildren.Clear();
            NativeRootChildStates.Clear();
            if (hud && hud.m_pieceSelectionWindow)
                hud.m_pieceSelectionWindow.SetActive(legacyWindowWasActive);
            Clear();
            if (player) HammerCatalogOrganizer.SetIndexEnabled(false, player);
        }

        public static void Clear()
        {
            maskState?.Restore();
            listRootState?.Restore();
            if (listRootState != null && listRootState.Rect)
                listRootState.Rect.gameObject.SetActive(listRootWasActive);
            if (nativePieceGrid)
            {
                nativePieceGrid.constraint = nativePieceGridConstraint;
                nativePieceGrid.constraintCount = nativePieceGridConstraintCount;
                LayoutRebuilder.ForceRebuildLayoutImmediate(
                    nativePieceGrid.transform as RectTransform);
            }
            for (int index = 0; index < PieceIconStates.Count; ++index)
            {
                PieceIconStates[index].Restore();
                GameObject root = PieceIconRoots[index];
                if (root) root.SetActive(PieceIconActiveStates[index]);
            }
            maskState = null;
            listRootState = null;
            nativePieceGrid = null;
            nativePieceGridConstraintCount = 0;
            indexGridActive = false;
            PieceIconStates.Clear();
            PieceIconRoots.Clear();
            PieceIconActiveStates.Clear();
            foreach (HammerCatalogFavoriteClick click in FavoriteClicks)
            {
                if (!click) continue;
                click.enabled = false;
                UnityEngine.Object.Destroy(click);
            }
            FavoriteClicks.Clear();
            foreach (GameObject decoration in Decorations)
                if (decoration) UnityEngine.Object.Destroy(decoration);
            Decorations.Clear();
            nativeIndexEntry = null;
            contextMenu = null;
            ContextButtons.Clear();
            appliedHudId = 0;
            appliedCategory = -1;
            appliedSignature = 0;
            appliedCount = -1;
            appliedPage = -1;
        }

        private static bool DecorationsAreAlive()
        {
            foreach (GameObject decoration in Decorations)
                if (!decoration) return false;
            return true;
        }

        private static void ApplyLayout(Hud hud, PieceTable table)
        {
            RectTransform window = hud.m_pieceSelectionWindow
                ? hud.m_pieceSelectionWindow.GetComponent<RectTransform>()
                : null;
            RectTransform mask = hud.m_pieceListMask;
            RectTransform listRoot = hud.m_pieceListRoot;
            if (window)
            {
                layoutWindowWidth = window.rect.width;
                layoutWindowHeight = window.rect.height;
                float aspect = layoutWindowWidth / Mathf.Max(1f, layoutWindowHeight);
                layoutRailWidth = Mathf.Clamp(
                    layoutWindowWidth * (aspect < 1.55f ? 0.20f : 0.15f),
                    180f,
                    240f);
                layoutSourceRows = aspect < 1.55f ||
                    layoutWindowWidth - layoutRailWidth < 920f ? 2 : 1;
            }
            GameObject[] categoryTabs = hud.m_pieceCategoryTabs ?? Array.Empty<GameObject>();
            int categoryCount = table && table.m_categories != null
                ? Math.Min(table.m_categories.Count, categoryTabs.Length)
                : 0;
            RectTransform categoryRoot = hud.m_pieceCategoryRoot
                ? hud.m_pieceCategoryRoot.transform as RectTransform
                : null;
            if (categoryRoot)
                LayoutRebuilder.ForceRebuildLayoutImmediate(categoryRoot);
            layoutCategoryBottom = 42f;
            for (int index = 0; index < categoryCount; ++index)
            {
                RectTransform categoryRect = categoryTabs[index]
                    ? categoryTabs[index].transform as RectTransform
                    : null;
                if (!categoryRect || !window) continue;
                Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    window,
                    categoryRect);
                layoutCategoryBottom = Mathf.Max(
                    layoutCategoryBottom,
                    window.rect.yMax - bounds.min.y);
            }
            float sourceOffset = (layoutSourceRows - 1) * 28f;
            float contentLeft = layoutRailWidth + 20f;
            float contentTop = layoutCategoryBottom + 66f + sourceOffset;
            var contentSize = new Vector2(
                Mathf.Max(1f, layoutWindowWidth - contentLeft - 8f),
                Mathf.Max(1f, layoutWindowHeight - contentTop - 58f));
            if (listRoot)
            {
                listRootState = new RectState(listRoot);
                listRootWasActive = listRoot.gameObject.activeSelf;
            }
            contentSize = FitGridSize(window, listRoot, contentSize);
            if (mask)
            {
                maskState = new RectState(mask);
                PlaceInsideWindow(mask, window, contentLeft, contentTop, contentSize);
            }
            if (listRoot)
            {
                PlaceInsideWindow(listRoot, window, contentLeft, contentTop, Vector2.zero);
            }
        }

        private static Vector2 FitGridSize(
            RectTransform window,
            RectTransform listRoot,
            Vector2 fallback)
        {
            GridLayoutGroup grid = listRoot ? listRoot.GetComponent<GridLayoutGroup>() : null;
            int columns = HammerCatalogOrganizer.VisibleColumnCount;
            if (!window || !listRoot || !grid)
                return fallback;
            int rows = HammerCatalogOrganizer.VisibleRowCount;
            float scaleX = listRoot.lossyScale.x / Mathf.Max(0.0001f, window.lossyScale.x);
            float scaleY = listRoot.lossyScale.y / Mathf.Max(0.0001f, window.lossyScale.y);
            float width = grid.padding.left + grid.padding.right +
                columns * grid.cellSize.x +
                Math.Max(0, columns - 1) * grid.spacing.x;
            float height = grid.padding.top + grid.padding.bottom +
                rows * grid.cellSize.y + Math.Max(0, rows - 1) * grid.spacing.y;
            float fit = Mathf.Min(
                1f,
                fallback.x / Mathf.Max(1f, width * scaleX),
                fallback.y / Mathf.Max(1f, height * scaleY));
            Vector3 scale = listRoot.localScale;
            listRoot.localScale = new Vector3(scale.x * fit, scale.y * fit, scale.z);
            return new Vector2(width * scaleX * fit, height * scaleY * fit);
        }

        private static void ArrangeIndexGrid(IList icons)
        {
            if (icons == null || icons.Count == 0) return;
            var roots = new List<RectTransform>(icons.Count);
            for (int index = 0; index < icons.Count; ++index)
            {
                GameObject root = IconRootField?.GetValue(icons[index]) as GameObject;
                RectTransform rect = root ? root.transform as RectTransform : null;
                if (!root || !rect) continue;
                PieceIconRoots.Add(root);
                PieceIconActiveStates.Add(root.activeSelf);
                PieceIconStates.Add(new RectState(rect));
                roots.Add(rect);
            }
            if (roots.Count == 0) return;

            const int nativeColumns = PieceTable.m_gridWidth;
            Vector2 origin = roots[0].anchoredPosition;
            float stepX = roots.Count > 1
                ? roots[1].anchoredPosition.x - origin.x
                : roots[0].sizeDelta.x;
            float stepY = roots.Count > nativeColumns
                ? roots[nativeColumns].anchoredPosition.y - origin.y
                : -roots[0].sizeDelta.y;
            int columns = HammerCatalogOrganizer.VisibleColumnCount;
            int slots = columns * HammerCatalogOrganizer.VisibleRowCount;
            for (int index = 0; index < roots.Count; ++index)
            {
                roots[index].anchoredPosition = origin + new Vector2(
                    index % columns * stepX,
                    index / columns * stepY);
                roots[index].gameObject.SetActive(index < slots);
            }
            indexGridActive = true;

            GridLayoutGroup grid = roots[0].parent
                ? roots[0].parent.GetComponent<GridLayoutGroup>()
                : null;
            if (!grid) return;
            nativePieceGrid = grid;
            nativePieceGridConstraint = grid.constraint;
            nativePieceGridConstraintCount = grid.constraintCount;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            LayoutRebuilder.ForceRebuildLayoutImmediate(grid.transform as RectTransform);
        }

        private static void AddFavoriteControls(
            Hud hud,
            Player player,
            IReadOnlyList<Piece> pieces,
            int count)
        {
            Image nativeStar = NativeFavoriteStar(hud);
            int visible = Math.Min(count, PieceIconRoots.Count);
            for (int index = 0; index < visible; ++index)
            {
                Piece piece = pieces[index];
                RectTransform icon = PieceIconRoots[index]
                    ? PieceIconRoots[index].transform as RectTransform
                    : null;
                if (!piece || !icon || piece.m_removePiece || piece.m_repairPiece) continue;
                HammerCatalogFavoriteClick click = icon.gameObject.AddComponent<
                    HammerCatalogFavoriteClick>();
                click.Clicked = () => HammerCatalogOrganizer.ToggleFavorite(piece, player);
                FavoriteClicks.Add(click);
                if (!HammerCatalogOrganizer.IsFavorite(piece) || !nativeStar || !nativeStar.sprite)
                    continue;

                var star = new GameObject(
                    Prefix + "FavoriteStar",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image));
                RectTransform starRect = (RectTransform)star.transform;
                starRect.SetParent(icon, false);
                starRect.anchorMin = starRect.anchorMax = starRect.pivot = Vector2.one;
                starRect.anchoredPosition = new Vector2(-3f, -3f);
                float size = Mathf.Clamp(icon.rect.width * 0.18f, 14f, 19f);
                starRect.sizeDelta = new Vector2(size, size);
                Image image = star.GetComponent<Image>();
                image.sprite = nativeStar.sprite;
                image.material = nativeStar.material;
                image.color = nativeStar.color;
                image.type = nativeStar.type;
                image.preserveAspect = true;
                image.raycastTarget = false;
                star.transform.SetAsLastSibling();
                Decorations.Add(star);
            }
        }

        private static Image NativeFavoriteStar(Hud hud)
        {
            if (!hud || !hud.m_buildUi || NativeFavoriteStarField == null) return null;
            foreach (BuildUiPieceButton button in
                hud.m_buildUi.GetComponentsInChildren<BuildUiPieceButton>(true))
            {
                Image image = NativeFavoriteStarField.GetValue(button) as Image;
                if (image && image.sprite) return image;
            }
            return null;
        }

        private static void PlaceInsideWindow(
            RectTransform rect,
            RectTransform window,
            float left,
            float top,
            Vector2 size)
        {
            RectTransform parent = rect ? rect.parent as RectTransform : null;
            if (!parent || !window) return;
            Vector2 pivot = rect.pivot;
            Vector3 worldTopLeft = window.TransformPoint(new Vector3(
                window.rect.xMin + left,
                window.rect.yMax - top,
                0f));
            Vector3 worldBottomRight = window.TransformPoint(new Vector3(
                window.rect.xMin + left + size.x,
                window.rect.yMax - top - size.y,
                0f));
            Vector3 worldPivot = window.TransformPoint(new Vector3(
                window.rect.xMin + left + size.x * pivot.x,
                window.rect.yMax - top - size.y * (1f - pivot.y),
                0f));
            Vector2 localTopLeft = parent.InverseTransformPoint(worldTopLeft);
            Vector2 localBottomRight = parent.InverseTransformPoint(worldBottomRight);
            Vector2 localPivot = parent.InverseTransformPoint(worldPivot);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = localPivot - parent.rect.center;
            rect.sizeDelta = new Vector2(
                Mathf.Abs(localBottomRight.x - localTopLeft.x),
                Mathf.Abs(localTopLeft.y - localBottomRight.y));
        }

        private static void AddIndexRail(
            Hud hud,
            Player player,
            PieceTable table,
            Piece.PieceCategory category,
            HammerCatalogOrganizer.PageInfo info,
            bool blueprints)
        {
            if (!hud.m_pieceSelectionWindow || !table) return;
            var rail = new GameObject(Prefix + "Index", typeof(RectTransform), typeof(Image));
            rail.transform.SetParent(hud.m_pieceSelectionWindow.transform, false);
            var rect = (RectTransform)rail.transform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(10f, -8f);
            rect.sizeDelta = new Vector2(layoutRailWidth - 18f, -82f);
            Image background = rail.GetComponent<Image>();
            background.color = new Color(0.08f, 0.045f, 0.025f, 0.58f);
            background.raycastTarget = false;
            Decorations.Add(rail);

            AddRailText(
                hud,
                rail.transform,
                blueprints
                    ? BuildWorksLocalization.Text(
                        "catalog.title.blueprints", blueprintStore?.All().Count ?? 0)
                    : BuildWorksLocalization.Text("catalog.title.catalog", info.Total),
                new Vector2(10f, -8f),
                19f,
                new Color(0.94f, 0.72f, 0.31f, 1f),
                FontStyles.Bold);

            float buttonWidth = layoutRailWidth - 32f;
            float y = -38f;
            Piece repairPiece = FindRepairPiece(table);
            AddRailButton(
                hud,
                rail.transform,
                BuildWorksLocalization.Text("catalog.action.repair"),
                y,
                28f,
                Accent(HammerCatalogOrganizer.ActionsGroup),
                table.GetSelectedPiece() == repairPiece,
                () => SelectPiece(player, table, repairPiece),
                width: buttonWidth);
            y -= 32f;
            AddRailButton(
                hud,
                rail.transform,
                string.IsNullOrEmpty(HammerCatalogOrganizer.Search)
                    ? BuildWorksLocalization.Text("catalog.action.search")
                    : BuildWorksLocalization.Text(
                        "catalog.action.search_value", HammerCatalogOrganizer.Search),
                y,
                26f,
                Accent(HammerCatalogOrganizer.GlassGroup),
                !string.IsNullOrEmpty(HammerCatalogOrganizer.Search),
                () => TextInput.instance?.RequestText(
                    new PromptReceiver(
                        () => HammerCatalogOrganizer.Search,
                        value => HammerCatalogOrganizer.SetSearch(value, player)),
                    BuildWorksLocalization.Text("catalog.prompt.search"),
                    48),
                width: buttonWidth);
            y -= 36f;

            if (blueprints)
            {
                AddBlueprintCategories(hud, rail.transform, ref y, buttonWidth);
                return;
            }

            float buttonHeight = info.Groups.Count > 10 ? 20f : 24f;
            string selectedGroup = HammerCatalogOrganizer.Group(table.GetSelectedPiece());
            foreach (HammerCatalogOrganizer.GroupInfo group in info.Groups)
            {
                HammerCatalogOrganizer.GroupInfo targetGroup = group;
                AddRailButton(
                    hud,
                    rail.transform,
                    BuildWorksLocalization.CatalogLabel(group.Name) + "  " + group.Count,
                    y,
                    buttonHeight,
                    Accent(group.Name),
                    string.Equals(group.Name, selectedGroup, StringComparison.Ordinal),
                    () => HammerCatalogOrganizer.GoToGroup(
                        table,
                        category,
                        targetGroup,
                        player),
                    width: buttonWidth);
                y -= buttonHeight + 3f;
            }

            if (info.PageCount <= 1) return;
            float bottom = 12f;
            AddRailButton(
                hud,
                rail.transform,
                "◀",
                bottom,
                28f,
                Accent(HammerCatalogOrganizer.WoodGroup),
                false,
                () => HammerCatalogOrganizer.GoToPage(
                    table,
                    category,
                    (info.Page - 1 + info.PageCount) % info.PageCount,
                    player),
                fromBottom: true,
                width: 34f);
            AddRailText(
                hud,
                rail.transform,
                (info.Page + 1) + " / " + info.PageCount,
                new Vector2(48f, bottom),
                16f,
                new Color(0.93f, 0.83f, 0.62f, 1f),
                FontStyles.Bold,
                fromBottom: true,
                width: Mathf.Max(38f, buttonWidth - 76f));
            AddRailButton(
                hud,
                rail.transform,
                "▶",
                bottom,
                28f,
                Accent(HammerCatalogOrganizer.WoodGroup),
                false,
                () => HammerCatalogOrganizer.GoToPage(
                    table,
                    category,
                    (info.Page + 1) % info.PageCount,
                    player),
                fromBottom: true,
                x: buttonWidth - 27f,
                width: 34f);
        }

        private static void AddVanillaModeControls(
            Hud hud,
            Player player,
            PieceTable table,
            HammerCatalogOrganizer.PageInfo info)
        {
            if (!hud.m_pieceSelectionWindow) return;
            AddModeToggle(hud, player, false);
            if (info.PageCount <= 1 || !table) return;

            GameObject previous = AddButton(
                hud,
                hud.m_pieceSelectionWindow.transform,
                "VanillaPreviousPage",
                "◀",
                Vector2.zero,
                new Vector2(34f, 30f),
                false,
                () => HammerCatalogOrganizer.GoToPage(
                    table,
                    table.GetSelectedCategory(),
                    (info.Page - 1 + info.PageCount) % info.PageCount,
                    player));
            RectTransform previousRect = (RectTransform)previous.transform;
            previousRect.anchorMin = previousRect.anchorMax = previousRect.pivot =
                new Vector2(0f, 0f);
            previousRect.anchoredPosition = new Vector2(164f, -36f);

            GameObject page = AddButton(
                hud,
                hud.m_pieceSelectionWindow.transform,
                "VanillaPage",
                (info.Page + 1) + " / " + info.PageCount,
                Vector2.zero,
                new Vector2(58f, 30f),
                false,
                () => { });
            page.GetComponent<Button>().interactable = false;
            RectTransform pageRect = (RectTransform)page.transform;
            pageRect.anchorMin = pageRect.anchorMax = pageRect.pivot = new Vector2(0f, 0f);
            pageRect.anchoredPosition = new Vector2(202f, -36f);

            GameObject next = AddButton(
                hud,
                hud.m_pieceSelectionWindow.transform,
                "VanillaNextPage",
                "▶",
                Vector2.zero,
                new Vector2(34f, 30f),
                false,
                () => HammerCatalogOrganizer.GoToPage(
                    table,
                    table.GetSelectedCategory(),
                    (info.Page + 1) % info.PageCount,
                    player));
            RectTransform nextRect = (RectTransform)next.transform;
            nextRect.anchorMin = nextRect.anchorMax = nextRect.pivot = new Vector2(0f, 0f);
            nextRect.anchoredPosition = new Vector2(264f, -36f);
        }

        private static void AddModeToggle(Hud hud, Player player, bool indexMode)
        {
            if (!hud.m_pieceSelectionWindow) return;
            GameObject toggle = AddButton(
                hud,
                hud.m_pieceSelectionWindow.transform,
                "ModeToggle",
                indexMode
                    ? BuildWorksLocalization.Text("catalog.action.vanilla_hammer")
                    : BuildWorksLocalization.Text("catalog.action.index_mode"),
                Vector2.zero,
                new Vector2(indexMode ? 158f : 144f, 30f),
                false,
                () =>
                {
                    if (HammerCatalogOrganizer.NativeBuildUiActive && indexMode)
                        ExitNativeIndex(hud, player);
                    else
                        HammerCatalogOrganizer.SetIndexEnabled(!indexMode, player);
                });
            RectTransform rect = (RectTransform)toggle.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = new Vector2(
                indexMode ? layoutRailWidth + 20f : 12f,
                indexMode ? 8f : -36f);
        }

        private static void NormalizeNativeCategoryTabs(Hud hud, PieceTable table)
        {
            GameObject[] tabs = hud.m_pieceCategoryTabs ?? Array.Empty<GameObject>();
            if (!table || table.m_categories == null || tabs.Length == 0) return;
            int count = Math.Min(table.m_categories.Count, tabs.Length);
            for (int index = 0; index < count; ++index)
            {
                GameObject tab = tabs[index];
                if (!tab) continue;
                int total = HammerCatalogOrganizer.GetTotalCount(
                    table, table.m_categories[index]);
                foreach (TMP_Text label in tab.GetComponentsInChildren<TMP_Text>(true))
                {
                    string text = label.text ?? string.Empty;
                    int bracket = text.LastIndexOf('[');
                    string prefix = bracket >= 0
                        ? text.Substring(0, bracket)
                        : text.TrimEnd() + " ";
                    label.text = prefix + "[<color=yellow>" + total + "</color>]";
                }
            }
        }

        private static void AddSourceTabs(
            Hud hud,
            Player player,
            PieceTable table,
            Piece.PieceCategory category)
        {
            if (category == UnifiedHammerCatalog.BlueprintCategory || !table) return;
            IReadOnlyList<HammerCatalogOrganizer.SourceInfo> sources =
                HammerCatalogOrganizer.GetSources(table, category);
            if (sources.Count == 0 || !hud.m_pieceSelectionWindow) return;
            var root = new GameObject(Prefix + "Sources", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(hud.m_pieceSelectionWindow.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(
                layoutRailWidth + 20f,
                -layoutCategoryBottom - 30f);
            rect.sizeDelta = new Vector2(
                layoutWindowWidth - layoutRailWidth - 40f,
                30f + (layoutSourceRows - 1) * 28f);
            Image image = root.GetComponent<Image>();
            image.color = new Color(0.09f, 0.05f, 0.025f, 0.38f);
            image.raycastTarget = false;
            Decorations.Add(root);

            string selected = HammerCatalogOrganizer.GetSourceFilter(table, category);
            float x = 4f;
            float y = -3f;
            int row = 0;
            float available = rect.sizeDelta.x - 8f;
            foreach (HammerCatalogOrganizer.SourceInfo source in sources)
            {
                HammerCatalogOrganizer.SourceInfo target = source;
                string sourceName = source.Name == HammerCatalogOrganizer.AllSource ||
                    source.Name == HammerCatalogOrganizer.VanillaSource
                        ? BuildWorksLocalization.CatalogLabel(source.Name)
                        : ShortSourceName(source.Name);
                string label = sourceName + " [" + source.Count + "]";
                float width = Mathf.Clamp(42f + label.Length * 6.4f, 72f, 190f);
                if (x + width > available && row + 1 < layoutSourceRows)
                {
                    ++row;
                    x = 4f;
                    y -= 28f;
                }
                width = Mathf.Min(width, available - x);
                if (width < 52f) break;
                AddRailButton(
                    hud,
                    root.transform,
                    label,
                    y,
                    24f,
                    Accent(HammerCatalogOrganizer.WoodGroup),
                    string.Equals(selected, source.Name, StringComparison.Ordinal),
                    () => HammerCatalogOrganizer.SetSourceFilter(
                        table,
                        category,
                        target.Name,
                        player),
                    x: x,
                    width: width);
                x += width + 4f;
            }
        }

        private static void AddBlueprintCategories(
            Hud hud,
            Transform rail,
            ref float y,
            float width)
        {
            if (blueprintStore == null) return;
            AddRailButton(
                hud,
                rail,
                BuildWorksLocalization.Text("catalog.action.create_blueprint"),
                y,
                27f,
                Accent(HammerCatalogOrganizer.BlueprintsGroup),
                false,
                () => blueprintCreateAction?.Invoke(),
                width: width);
            y -= 32f;
            if (blueprintWorldSelectionAction != null)
            {
                AddRailButton(hud, rail,
                    BuildWorksLocalization.Text("catalog.action.select_in_world"), y, 27f,
                    Accent(HammerCatalogOrganizer.BlueprintsGroup), false,
                    () => blueprintWorldSelectionAction?.Invoke(), width: width);
                y -= 32f;
            }
            var categories = new List<string> { AllBlueprintCategories };
            categories.AddRange(blueprintStore.Categories());
            foreach (string category in categories)
            {
                string target = category;
                int count = category == AllBlueprintCategories
                    ? blueprintStore.All().Count
                    : CountBlueprints(category);
                AddRailButton(
                    hud,
                    rail,
                    (category == AllBlueprintCategories
                        ? BuildWorksLocalization.CatalogLabel(HammerCatalogOrganizer.AllSource)
                        : BuildWorksLocalization.BlueprintCategoryLabel(category)) + "  " + count,
                    y,
                    23f,
                    Accent(HammerCatalogOrganizer.BlueprintsGroup),
                    string.Equals(blueprintCategory, category, StringComparison.Ordinal),
                    () =>
                    {
                        blueprintCategory = target;
                        blueprintPage = 0;
                        selectedBlueprint = null;
                        showResources = false;
                        Invalidate();
                    },
                    width: width);
                y -= 26f;
            }
            AddRailButton(
                hud,
                rail,
                BuildWorksLocalization.Text("catalog.action.add_category"),
                y,
                23f,
                Accent(HammerCatalogOrganizer.WoodGroup),
                false,
                () => TextInput.instance?.RequestText(
                    new PromptReceiver(
                        () => string.Empty,
                        value =>
                        {
                            if (blueprintStore.TryAddCategory(value, out string error))
                            {
                                blueprintCategory = value.Trim().ToUpperInvariant();
                                Invalidate();
                            }
                            else ShowStatus(error);
                        }),
                    BuildWorksLocalization.Text("catalog.prompt.new_category"),
                    24),
                width: width);
            y -= 26f;
            if (blueprintCategory != AllBlueprintCategories &&
                blueprintCategory != CompositeBlueprintStore.DefaultCategory)
            {
                AddRailButton(
                    hud,
                    rail,
                    BuildWorksLocalization.Text("catalog.action.delete_category"),
                    y,
                    23f,
                    new Color(0.72f, 0.25f, 0.18f, 1f),
                    false,
                    () =>
                    {
                        if (!blueprintStore.TryDeleteCategory(
                            blueprintCategory,
                            out string error))
                        {
                            ShowStatus(error);
                            return;
                        }
                        blueprintCategory = CompositeBlueprintStore.DefaultCategory;
                        selectedBlueprint = null;
                        Invalidate();
                    },
                    width: width);
            }
        }

        private static void AddBlueprintLibrary(Hud hud, Player player)
        {
            if (blueprintStore == null || blueprintRegistry == null ||
                !hud.m_pieceSelectionWindow) return;
            if (hud.m_pieceListRoot) hud.m_pieceListRoot.gameObject.SetActive(false);

            float top = layoutCategoryBottom + 30f;
            float left = layoutRailWidth + 20f;
            float width = layoutWindowWidth - left - 20f;
            float height = layoutWindowHeight - top - 58f;
            AddBlueprintLibrary(
                hud,
                player,
                hud.m_pieceSelectionWindow.transform,
                left,
                top,
                width,
                height);
        }

        private static void AddBlueprintLibrary(
            Hud hud,
            Player player,
            Transform parent,
            float left,
            float top,
            float width,
            float height)
        {
            if (blueprintStore == null || blueprintRegistry == null || !parent) return;
            float inspectorWidth = Mathf.Clamp(width * 0.27f, 244f, 300f);
            inspectorWidth = Mathf.Min(
                inspectorWidth,
                Mathf.Max(160f, width - 220f - 18f));
            float cardsWidth = Mathf.Max(1f, width - inspectorWidth - 18f);
            float cardWidth = cardsWidth < 520f ? cardsWidth : 176f;
            int columns = Mathf.Max(1, Mathf.FloorToInt((cardsWidth + 8f) / (cardWidth + 8f)));
            cardWidth = (cardsWidth - (columns - 1) * 8f) / columns;
            int rows = Mathf.Max(1, Mathf.FloorToInt((height - 42f) / 132f));
            blueprintsPerPage = Math.Max(1, columns * rows);

            var filtered = new List<CompositeBlueprintStore.Blueprint>();
            foreach (CompositeBlueprintStore.Blueprint blueprint in blueprintStore.All())
            {
                if (blueprintCategory != AllBlueprintCategories && !string.Equals(
                    blueprint.category,
                    blueprintCategory,
                    StringComparison.Ordinal)) continue;
                if (!string.IsNullOrEmpty(HammerCatalogOrganizer.Search) &&
                    blueprint.name.IndexOf(
                        HammerCatalogOrganizer.Search,
                        StringComparison.OrdinalIgnoreCase) < 0) continue;
                filtered.Add(blueprint);
            }
            int pageCount = Math.Max(1, (filtered.Count + blueprintsPerPage - 1) /
                blueprintsPerPage);
            blueprintPage = Mathf.Clamp(blueprintPage, 0, pageCount - 1);
            if (selectedBlueprint == null || !filtered.Contains(selectedBlueprint))
                selectedBlueprint = filtered.Count > 0 ? filtered[0] : null;

            var cards = AddPanel(
                parent,
                "BlueprintCards",
                new Vector2(left, -top),
                new Vector2(cardsWidth, height),
                new Color(0.08f, 0.045f, 0.025f, 0.22f));
            int start = blueprintPage * blueprintsPerPage;
            int end = Math.Min(filtered.Count, start + blueprintsPerPage);
            for (int index = start; index < end; ++index)
            {
                int local = index - start;
                int column = local % columns;
                int row = local / columns;
                AddBlueprintCard(
                    hud,
                    player,
                    cards.transform,
                    filtered[index],
                    new Vector2(column * (cardWidth + 8f), -row * 132f),
                    new Vector2(cardWidth, 124f));
            }
            if (filtered.Count == 0)
                AddRailText(
                    hud,
                    cards.transform,
                    BuildWorksLocalization.Text("catalog.empty"),
                    new Vector2(16f, -18f),
                    18f,
                    new Color(0.88f, 0.76f, 0.55f, 1f),
                    FontStyles.Normal,
                    width: cardsWidth - 32f);
            if (pageCount > 1)
            {
                AddRailButton(hud, cards.transform, "◀", 8f, 28f,
                    Accent(HammerCatalogOrganizer.WoodGroup), false,
                    () => ChangeBlueprintPage(-1), fromBottom: true, width: 34f);
                AddRailText(hud, cards.transform, (blueprintPage + 1) + " / " + pageCount,
                    new Vector2(44f, 8f), 15f, Color.white, FontStyles.Bold,
                    fromBottom: true, width: 72f);
                AddRailButton(hud, cards.transform, "▶", 8f, 28f,
                    Accent(HammerCatalogOrganizer.WoodGroup), false,
                    () => ChangeBlueprintPage(1), fromBottom: true,
                    x: 112f, width: 34f);
            }

            var inspector = AddPanel(
                parent,
                "BlueprintInspector",
                new Vector2(left + cardsWidth + 18f, -top),
                new Vector2(inspectorWidth, height),
                new Color(0.10f, 0.055f, 0.025f, 0.62f));
            AddBlueprintInspector(hud, player, inspector.transform, inspectorWidth, height);
        }

        private static void AddBlueprintCard(
            Hud hud,
            Player player,
            Transform parent,
            CompositeBlueprintStore.Blueprint blueprint,
            Vector2 position,
            Vector2 size)
        {
            GameObject card = AddButton(
                hud,
                parent,
                "BlueprintCard",
                string.Empty,
                position,
                size,
                selectedBlueprint == blueprint,
                () => { });
            Button cardButton = card.GetComponent<Button>();
            cardButton.onClick.RemoveAllListeners();
            HammerCatalogPointerClick pointer = card.AddComponent<HammerCatalogPointerClick>();
            pointer.Clicked = button =>
            {
                if (button != PointerEventData.InputButton.Left) return;
                float now = Time.unscaledTime;
                bool doubleClick = string.Equals(lastBlueprintClickId, blueprint.id,
                    StringComparison.Ordinal) && now - lastBlueprintClickAt <= 0.35f;
                lastBlueprintClickId = blueprint.id;
                lastBlueprintClickAt = now;
                SelectBlueprint(blueprint);
                if (doubleClick) blueprintAction?.Invoke(blueprint, false);
                else Invalidate();
            };
            pointer.RightPressed = position =>
            {
                SelectBlueprint(blueprint);
                contextBlueprint = blueprint;
                AddBlueprintContextMenu(hud, player, parent, blueprint, position);
            };
            pointer.RightReleased = ReleaseBlueprintContextMenu;
            if (blueprintRegistry.TryGetPiece(blueprint, out Piece piece) && piece.m_icon)
                AddIcon(card.transform, piece.m_icon, new Vector2(8f, -8f),
                    new Vector2(size.x - 16f, size.y - 42f));
            AddRailText(
                hud,
                card.transform,
                blueprint.name + "\n" + BuildWorksLocalization.Text(
                    "catalog.card.parts", blueprint.parts.Count),
                new Vector2(8f, -(size.y - 35f)),
                14f,
                Color.white,
                FontStyles.Bold,
                width: size.x - 16f);
            AddButton(
                hud,
                card.transform,
                "ManageBlueprint",
                "⋯",
                new Vector2(size.x - 38f, -4f),
                new Vector2(34f, 26f),
                selectedBlueprint == blueprint,
                () =>
                {
                    SelectBlueprint(blueprint);
                    Invalidate();
                });
        }

        private static void SelectBlueprint(CompositeBlueprintStore.Blueprint blueprint)
        {
            selectedBlueprint = blueprint;
            showResources = false;
            deleteConfirmationId = null;
        }

        private static void AddBlueprintContextMenu(
            Hud hud,
            Player player,
            Transform parent,
            CompositeBlueprintStore.Blueprint blueprint,
            Vector2 screenPosition)
        {
            if (contextMenu) UnityEngine.Object.Destroy(contextMenu);
            ContextButtons.Clear();
            GameObject menu = AddPanel(parent, "BlueprintContextMenu",
                Vector2.zero,
                new Vector2(244f, 260f), new Color(0.08f, 0.04f, 0.02f, 0.98f));
            contextMenu = menu;
            RectTransform parentRect = parent as RectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPosition,
                null, out Vector2 local);
            Rect bounds = parentRect.rect;
            RectTransform menuRect = (RectTransform)menu.transform;
            menuRect.pivot = new Vector2(
                local.x + 244f <= bounds.xMax ? 0f : 1f,
                local.y - 260f >= bounds.yMin ? 1f : 0f);
            menuRect.anchoredPosition = new Vector2(
                Mathf.Clamp(local.x, bounds.xMin, bounds.xMax),
                Mathf.Clamp(local.y, bounds.yMin, bounds.yMax));
            menu.transform.SetAsLastSibling();
            AddRailText(hud, menu.transform, blueprint.name, new Vector2(10f, -8f), 15f,
                Color.white, FontStyles.Bold, width: 190f);
            float y = -38f;
            float width = 224f;
            AddContextAction(hud, menu.transform,
                BuildWorksLocalization.Text("catalog.action.place"), y, width,
                () => { contextBlueprint = null; blueprintAction?.Invoke(blueprint, false); });
            y -= 30f;
            AddContextAction(hud, menu.transform,
                BuildWorksLocalization.Text("catalog.action.edit"), y, width,
                () => { contextBlueprint = null; blueprintAction?.Invoke(blueprint, true); });
            y -= 30f;
            AddContextAction(hud, menu.transform,
                BuildWorksLocalization.Text("catalog.action.rename"), y, width,
                () => TextInput.instance?.RequestText(new PromptReceiver(
                    () => blueprint.name,
                    value =>
                    {
                        if (!blueprintStore.TryRename(blueprint, value, out string error))
                            ShowStatus(error);
                        contextBlueprint = null;
                        Invalidate();
                    }), BuildWorksLocalization.Text("catalog.prompt.blueprint_name"), 48));
            y -= 30f;
            AddContextAction(hud, menu.transform, BuildWorksLocalization.Text(
                "catalog.action.category",
                BuildWorksLocalization.BlueprintCategoryLabel(blueprint.category)), y, width,
                () => { contextBlueprint = null; CycleBlueprintCategory(blueprint); });
            y -= 30f;
            AddContextAction(hud, menu.transform,
                BuildWorksLocalization.Text("catalog.action.refresh_preview"), y, width,
                () => { contextBlueprint = null; RefreshBlueprintPreview(blueprint); });
            y -= 30f;
            AddContextAction(hud, menu.transform,
                BuildWorksLocalization.Text("catalog.action.resources"), y, width,
                () => { contextBlueprint = null; showResources = true; Invalidate(); });
            y -= 30f;
            bool confirming = deleteConfirmationId == blueprint.id;
            AddContextAction(hud, menu.transform,
                confirming
                    ? BuildWorksLocalization.Text("catalog.action.confirm_delete")
                    : BuildWorksLocalization.Text("catalog.action.delete"), y, width,
                () => DeleteBlueprint(player, blueprint), danger: true);
        }

        private static void AddContextAction(Hud hud, Transform parent, string label,
            float y, float width, Action action, bool danger = false)
        {
            GameObject button = AddActionButton(hud, parent, label, y, width, action, danger);
            ContextButtons.Add(button.GetComponent<Button>());
        }

        private static void ReleaseBlueprintContextMenu(Vector2 screenPosition)
        {
            if (!contextMenu) return;
            Button chosen = null;
            foreach (Button button in ContextButtons)
                if (button && RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)button.transform, screenPosition))
                {
                    chosen = button;
                    break;
                }
            contextMenu.SetActive(false);
            contextMenu = null;
            ContextButtons.Clear();
            contextBlueprint = null;
            chosen?.onClick.Invoke();
        }

        private static void AddBlueprintInspector(
            Hud hud,
            Player player,
            Transform parent,
            float width,
            float height)
        {
            if (selectedBlueprint == null)
            {
                AddRailText(hud, parent,
                    BuildWorksLocalization.Text("catalog.choose_blueprint"),
                    new Vector2(16f, -16f), 18f,
                    Color.white, FontStyles.Bold, width: width - 32f);
                return;
            }
            CompositeBlueprintStore.Blueprint blueprint = selectedBlueprint;
            AddRailText(hud, parent, blueprint.name, new Vector2(16f, -14f), 20f,
                new Color(0.96f, 0.78f, 0.39f, 1f), FontStyles.Bold, width: width - 32f);
            AddRailText(hud, parent,
                BuildWorksLocalization.Text("catalog.blueprint_summary",
                    blueprint.parts.Count,
                    BuildWorksLocalization.BlueprintCategoryLabel(blueprint.category)),
                new Vector2(16f, -46f), 14f,
                new Color(0.88f, 0.80f, 0.66f, 1f), FontStyles.Normal,
                width: width - 32f);
            if (blueprintRegistry.TryGetPiece(blueprint, out Piece piece) && piece.m_icon)
                AddIcon(parent, piece.m_icon, new Vector2(16f, -74f),
                    new Vector2(width - 32f, Mathf.Min(104f, height * 0.20f)));

            const float actionHeight = 28f;
            const int actionCount = 7;
            float actionTop = Mathf.Min(184f, 86f + height * 0.20f);
            actionTop = Mathf.Min(
                actionTop,
                Mathf.Max(86f, height - actionCount * actionHeight - 8f));
            float y = -actionTop;
            float buttonWidth = width - 32f;
            AddActionButton(hud, parent,
                BuildWorksLocalization.Text("catalog.action.place"), y, buttonWidth,
                () => blueprintAction?.Invoke(blueprint, false));
            y -= actionHeight;
            AddActionButton(hud, parent,
                BuildWorksLocalization.Text("catalog.action.edit"), y, buttonWidth,
                () => blueprintAction?.Invoke(blueprint, true));
            y -= actionHeight;
            AddActionButton(hud, parent,
                BuildWorksLocalization.Text("catalog.action.rename"), y, buttonWidth,
                () => TextInput.instance?.RequestText(
                    new PromptReceiver(
                        () => blueprint.name,
                        value =>
                        {
                            if (!blueprintStore.TryRename(blueprint, value, out string error))
                                ShowStatus(error);
                            Invalidate();
                        }),
                    BuildWorksLocalization.Text("catalog.prompt.blueprint_name"),
                    48));
            y -= actionHeight;
            AddActionButton(hud, parent, BuildWorksLocalization.Text(
                "catalog.action.category",
                BuildWorksLocalization.BlueprintCategoryLabel(blueprint.category)), y, buttonWidth,
                () => CycleBlueprintCategory(blueprint));
            y -= actionHeight;
            AddActionButton(hud, parent,
                BuildWorksLocalization.Text("catalog.action.refresh_preview"), y, buttonWidth,
                () => RefreshBlueprintPreview(blueprint));
            y -= actionHeight;
            AddActionButton(hud, parent,
                showResources
                    ? BuildWorksLocalization.Text("catalog.action.hide_resources")
                    : BuildWorksLocalization.Text("catalog.action.resources"),
                y, buttonWidth, () => { showResources = !showResources; Invalidate(); });
            y -= actionHeight;
            bool confirming = deleteConfirmationId == blueprint.id;
            AddActionButton(
                hud,
                parent,
                confirming
                    ? BuildWorksLocalization.Text("catalog.action.confirm_delete")
                    : BuildWorksLocalization.Text("catalog.action.delete"),
                y,
                buttonWidth,
                () => DeleteBlueprint(player, blueprint),
                danger: true);

            if (showResources) AddResourcesPopup(hud, parent, blueprint, width, height);
        }

        public static bool HandleBlueprintPaging(PieceTable table, int direction)
        {
            if (!UnifiedHammerCatalog.BlueprintCategoryReady || !table ||
                table.GetSelectedCategory() != UnifiedHammerCatalog.BlueprintCategory)
                return false;
            ChangeBlueprintPage(direction);
            return true;
        }

        public static bool ShouldCaptureWheel =>
            (!HammerCatalogOrganizer.NativeBuildUiActive || nativeIndexMode) &&
            HammerCatalogOrganizer.IndexEnabled && Hud.instance &&
            Hud.IsPieceSelectionVisible();

        public static bool BlocksCatalogPaging => previewSession != null || showResources;

        private static GameObject AddPanel(
            Transform parent,
            string name,
            Vector2 position,
            Vector2 size,
            Color color)
        {
            var panel = new GameObject(Prefix + name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = (RectTransform)panel.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = panel.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            Decorations.Add(panel);
            return panel;
        }

        private static GameObject AddButton(
            Hud hud,
            Transform parent,
            string name,
            string label,
            Vector2 position,
            Vector2 size,
            bool selected,
            Action action,
            bool danger = false)
        {
            var root = new GameObject(Prefix + name, typeof(RectTransform),
                typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = root.GetComponent<Image>();
            Image template = FirstCategoryImage(hud);
            if (template)
            {
                image.sprite = template.sprite;
                image.type = template.type;
            }
            image.color = danger
                ? new Color(0.52f, 0.12f, 0.08f, 0.92f)
                : selected
                    ? new Color(0.68f, 0.45f, 0.18f, 0.96f)
                    : new Color(0.27f, 0.14f, 0.065f, 0.86f);
            Button button = root.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            if (!string.IsNullOrEmpty(label))
                AddRailText(hud, root.transform, label, Vector2.zero, 14f,
                    Color.white, FontStyles.Bold, stretch: true);
            Decorations.Add(root);
            return root;
        }

        private static GameObject AddActionButton(
            Hud hud,
            Transform parent,
            string label,
            float y,
            float width,
            Action action,
            bool danger = false)
        {
            return AddButton(hud, parent, "Action", label, new Vector2(16f, y),
                new Vector2(width, 28f), false, action, danger);
        }

        private static void AddIcon(
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 size)
        {
            if (!sprite) return;
            var root = new GameObject(Prefix + "Icon", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = root.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            Decorations.Add(root);
        }

        private static void AddResourcesPopup(
            Hud hud,
            Transform parent,
            CompositeBlueprintStore.Blueprint blueprint,
            float width,
            float height)
        {
            IReadOnlyList<HammerBlueprintPieceRegistry.ResourceInfo> resources =
                blueprintRegistry.Resources(blueprint);
            float popupHeight = Mathf.Min(height - 24f, 84f + resources.Count * 34f);
            GameObject popup = AddPanel(parent, "Resources", new Vector2(6f, -6f),
                new Vector2(width - 12f, popupHeight),
                new Color(0.055f, 0.028f, 0.012f, 0.98f));
            AddRailText(hud, popup.transform,
                BuildWorksLocalization.Text("catalog.action.resources"),
                new Vector2(12f, -10f), 18f,
                new Color(0.96f, 0.76f, 0.36f, 1f), FontStyles.Bold,
                width: width - 70f);
            AddButton(hud, popup.transform, "CloseResources", "×",
                new Vector2(width - 48f, -8f), new Vector2(28f, 26f), false,
                () => { showResources = false; Invalidate(); });

            var viewport = new GameObject(Prefix + "ResourcesViewport", typeof(RectTransform),
                typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(popup.transform, false);
            var viewportRect = (RectTransform)viewport.transform;
            viewportRect.anchorMin = viewportRect.anchorMax = new Vector2(0f, 1f);
            viewportRect.pivot = new Vector2(0f, 1f);
            viewportRect.anchoredPosition = new Vector2(8f, -44f);
            viewportRect.sizeDelta = new Vector2(width - 28f, popupHeight - 52f);
            Image viewportImage = viewport.GetComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.001f);
            viewportImage.raycastTarget = true;
            Decorations.Add(viewport);

            var content = new GameObject(Prefix + "ResourcesContent", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(
                0f,
                Mathf.Max(viewportRect.sizeDelta.y, Math.Max(1, resources.Count) * 34f));
            Decorations.Add(content);

            ScrollRect scroll = popup.AddComponent<ScrollRect>();
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            float y = 0f;
            foreach (HammerBlueprintPieceRegistry.ResourceInfo resource in resources)
            {
                AddIcon(content.transform, resource.Icon, new Vector2(4f, y),
                    new Vector2(28f, 28f));
                AddRailText(hud, content.transform,
                    resource.Name + "  ×" + resource.Amount,
                    new Vector2(40f, y - 2f), 15f, Color.white, FontStyles.Normal,
                    width: width - 78f);
                y -= 34f;
            }
            if (resources.Count == 0)
                AddRailText(hud, content.transform,
                    BuildWorksLocalization.Text("catalog.resources.none"),
                    new Vector2(4f, 0f), 15f, Color.white, FontStyles.Normal,
                    width: width - 36f);
        }

        private static void CycleBlueprintCategory(
            CompositeBlueprintStore.Blueprint blueprint)
        {
            IReadOnlyList<string> categories = blueprintStore.Categories();
            if (categories.Count == 0) return;
            int index = -1;
            for (int candidate = 0; candidate < categories.Count; ++candidate)
                if (string.Equals(categories[candidate], blueprint.category,
                    StringComparison.Ordinal)) index = candidate;
            string next = categories[(index + 1 + categories.Count) % categories.Count];
            if (!blueprintStore.TrySetCategory(blueprint, next, out string error))
                ShowStatus(error);
            Invalidate();
        }

        private static void RefreshBlueprintPreview(
            CompositeBlueprintStore.Blueprint blueprint)
        {
            ClosePreview();
            previewSession = blueprintRegistry.BeginPreview(blueprint);
            if (previewSession == null)
            {
                ShowStatus(BuildWorksLocalization.Text("catalog.preview.open_failed"));
                return;
            }
            previewBlueprint = blueprint;
            previewYaw = blueprint.previewYaw;
            previewPitch = blueprint.previewPitch;
            previewZoom = blueprint.previewZoom;
            Invalidate();
        }

        private static void AddPreviewEditor(Hud hud)
        {
            if (previewSession == null || !hud.m_pieceSelectionWindow) return;
            float left = layoutRailWidth + 20f;
            float top = layoutCategoryBottom + 20f;
            float width = layoutWindowWidth - left - 18f;
            float height = layoutWindowHeight - top - 58f;
            GameObject panel = AddPanel(
                hud.m_pieceSelectionWindow.transform,
                "PreviewEditor",
                new Vector2(left, -top),
                new Vector2(width, height),
                new Color(0.035f, 0.02f, 0.012f, 0.985f));
            panel.transform.SetAsLastSibling();
            Canvas parentCanvas = panel.GetComponentInParent<Canvas>();
            Canvas modalCanvas = panel.AddComponent<Canvas>();
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder = (parentCanvas ? parentCanvas.sortingOrder : 0) + 50;
            panel.AddComponent<GraphicRaycaster>();
            AddRailText(hud, panel.transform,
                BuildWorksLocalization.Text("catalog.preview.title", previewBlueprint.name),
                new Vector2(14f, -10f), 20f,
                new Color(0.96f, 0.76f, 0.36f, 1f), FontStyles.Bold,
                width: width - 28f);
            AddRailText(hud, panel.transform,
                BuildWorksLocalization.Text("catalog.preview.controls"),
                new Vector2(14f, -39f), 14f,
                new Color(0.86f, 0.80f, 0.70f, 1f), FontStyles.Normal,
                width: width - 28f);

            float imageSize = Mathf.Min(width - 40f, height - 142f);
            var raw = new GameObject(Prefix + "PreviewImage", typeof(RectTransform),
                typeof(RawImage));
            raw.transform.SetParent(panel.transform, false);
            var rawRect = (RectTransform)raw.transform;
            rawRect.anchorMin = rawRect.anchorMax = new Vector2(0.5f, 1f);
            rawRect.pivot = new Vector2(0.5f, 1f);
            rawRect.anchoredPosition = new Vector2(0f, -66f);
            rawRect.sizeDelta = new Vector2(imageSize, imageSize);
            RawImage rawImage = raw.GetComponent<RawImage>();
            rawImage.texture = previewSession.Texture;
            rawImage.color = Color.white;
            rawImage.raycastTarget = true;
            Decorations.Add(raw);

            float buttonY = -(height - 46f);
            float gap = 8f;
            float buttonWidth = (width - 28f - gap * 3f) / 4f;
            AddButton(hud, panel.transform, "PreviewAuto",
                BuildWorksLocalization.Text("catalog.preview.auto"),
                new Vector2(14f, buttonY), new Vector2(buttonWidth, 30f), false,
                () => SetPreviewView(45f, 30f, 1f));
            AddButton(hud, panel.transform, "PreviewReset",
                BuildWorksLocalization.Text("catalog.preview.reset"),
                new Vector2(14f + (buttonWidth + gap), buttonY),
                new Vector2(buttonWidth, 30f), false,
                () => SetPreviewView(
                    previewBlueprint.previewYaw,
                    previewBlueprint.previewPitch,
                    previewBlueprint.previewZoom));
            AddButton(hud, panel.transform, "PreviewCancel",
                BuildWorksLocalization.Text("catalog.preview.cancel"),
                new Vector2(14f + (buttonWidth + gap) * 2f, buttonY),
                new Vector2(buttonWidth, 30f), false,
                () => { ClosePreview(); Invalidate(); });
            AddButton(hud, panel.transform, "PreviewSave",
                BuildWorksLocalization.Text("catalog.preview.save"),
                new Vector2(14f + (buttonWidth + gap) * 3f, buttonY),
                new Vector2(buttonWidth, 30f), true,
                SavePreview);
        }

        private static void HandlePreviewInput()
        {
            if (previewSession == null) return;
            if (Input.GetKeyDown(KeyCode.F))
            {
                SetPreviewView(45f, 30f, 1f);
                return;
            }
            if (Input.GetMouseButtonDown(2))
            {
                previewDragging = true;
                previewMouse = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(2)) previewDragging = false;
            if (previewDragging && Input.GetMouseButton(2))
            {
                Vector2 mouse = Input.mousePosition;
                Vector2 delta = mouse - previewMouse;
                previewMouse = mouse;
                if (delta.sqrMagnitude > 0.01f)
                    SetPreviewView(
                        previewYaw + delta.x * 0.25f,
                        Mathf.Clamp(previewPitch - delta.y * 0.25f, -80f, 80f),
                        previewZoom,
                        rebuildUi: false);
            }
            float wheel = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.01f)
                SetPreviewView(previewYaw, previewPitch,
                    Mathf.Clamp(previewZoom * (wheel > 0f ? 1.12f : 0.89f), 0.5f, 3f),
                    rebuildUi: false);
        }

        private static void SetPreviewView(
            float yaw,
            float pitch,
            float zoom,
            bool rebuildUi = true)
        {
            if (previewSession == null) return;
            previewYaw = Mathf.Repeat(yaw, 360f);
            previewPitch = Mathf.Clamp(pitch, -80f, 80f);
            previewZoom = Mathf.Clamp(zoom, 0.5f, 3f);
            previewSession.Render(previewYaw, previewPitch, previewZoom);
            if (rebuildUi) Invalidate();
        }

        private static void SavePreview()
        {
            if (previewSession == null || previewBlueprint == null) return;
            CompositeBlueprintStore.Blueprint blueprint = previewBlueprint;
            if (!blueprintStore.TrySetPreview(
                blueprint,
                previewYaw,
                previewPitch,
                previewZoom,
                out string error))
            {
                ShowStatus(error);
                return;
            }
            ClosePreview();
            if (!blueprintRegistry.RefreshThumbnail(blueprint))
                ShowStatus(BuildWorksLocalization.Text("catalog.preview.save_failed"));
            Invalidate();
        }

        private static void ClosePreview()
        {
            previewSession?.Dispose();
            previewSession = null;
            previewBlueprint = null;
            previewDragging = false;
        }

        private static void DeleteBlueprint(
            Player player,
            CompositeBlueprintStore.Blueprint blueprint)
        {
            if (deleteConfirmationId != blueprint.id)
            {
                deleteConfirmationId = blueprint.id;
                Invalidate();
                return;
            }
            blueprintRegistry.SelectSafePieceBeforeDelete(player, blueprint);
            if (!blueprintStore.TryDelete(blueprint, out string error))
            {
                ShowStatus(error);
                deleteConfirmationId = null;
                Invalidate();
                return;
            }
            blueprintRegistry.DeleteThumbnail(blueprint.id);
            selectedBlueprint = null;
            deleteConfirmationId = null;
            showResources = false;
            Invalidate();
        }

        private static void ChangeBlueprintPage(int direction)
        {
            int count = 0;
            if (blueprintStore != null)
            {
                foreach (CompositeBlueprintStore.Blueprint blueprint in blueprintStore.All())
                    if ((blueprintCategory == AllBlueprintCategories ||
                            blueprint.category == blueprintCategory) &&
                        (string.IsNullOrEmpty(HammerCatalogOrganizer.Search) ||
                        blueprint.name.IndexOf(HammerCatalogOrganizer.Search,
                            StringComparison.OrdinalIgnoreCase) >= 0)) ++count;
            }
            int pages = Math.Max(1, (count + blueprintsPerPage - 1) / blueprintsPerPage);
            blueprintPage = (blueprintPage + direction + pages) % pages;
            selectedBlueprint = null;
            Invalidate();
        }

        private static int CountBlueprints(string category)
        {
            int count = 0;
            foreach (CompositeBlueprintStore.Blueprint blueprint in blueprintStore.All())
                if (blueprint.category == category) ++count;
            return count;
        }

        private static Piece FindRepairPiece(PieceTable table)
        {
            foreach (GameObject prefab in table?.m_pieces ?? new List<GameObject>())
            {
                Piece piece = prefab ? prefab.GetComponent<Piece>() : null;
                if (piece && (prefab.name.IndexOf("repair", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    piece.m_name.IndexOf("repair", StringComparison.OrdinalIgnoreCase) >= 0))
                    return piece;
            }
            return null;
        }

        private static void SelectPiece(Player player, PieceTable table, Piece piece)
        {
            if (!player || !piece) return;
            HammerCatalogOrganizer.EnsureVisible(table, piece);
            player.SetSelectedPiece(piece);
        }

        private static string ShortSourceName(string source) => source.StartsWith(
            "OdinArchitect ",
            StringComparison.OrdinalIgnoreCase)
            ? source.Substring("OdinArchitect ".Length)
            : source;

        private static Image FirstCategoryImage(Hud hud)
        {
            foreach (GameObject tab in hud.m_pieceCategoryTabs ?? Array.Empty<GameObject>())
            {
                Image image = tab ? tab.GetComponent<Image>() : null;
                if (image) return image;
            }
            return null;
        }

        private static void ShowStatus(string text)
        {
            if (string.IsNullOrWhiteSpace(text) || !Player.m_localPlayer) return;
            Player.m_localPlayer.Message(
                MessageHud.MessageType.Center,
                "BuildWorks: " + BuildWorksLocalization.ResolveUserText(text),
                0,
                null);
        }

        private sealed class PromptReceiver : TextReceiver
        {
            private readonly Func<string> getText;
            private readonly Action<string> setText;

            public PromptReceiver(Func<string> getText, Action<string> setText)
            {
                this.getText = getText;
                this.setText = setText;
            }

            public string GetText() => getText();

            public void SetText(string text) => setText(text);
        }

        private static void AddRailButton(
            Hud hud,
            Transform parent,
            string label,
            float y,
            float height,
            Color accent,
            bool selected,
            Action action,
            bool fromBottom = false,
            float x = 7f,
            float width = 138f)
        {
            var root = new GameObject(Prefix + "Button", typeof(RectTransform),
                typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, fromBottom ? 0f : 1f);
            rect.pivot = new Vector2(0f, fromBottom ? 0f : 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            Image image = root.GetComponent<Image>();
            image.color = selected
                ? new Color(accent.r, accent.g, accent.b, 0.42f)
                : new Color(0.16f, 0.09f, 0.045f, 0.52f);
            Button button = root.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.86f, 0.56f, 1f);
            colors.pressedColor = new Color(0.72f, 0.51f, 0.25f, 1f);
            button.colors = colors;
            button.onClick.AddListener(() => action());
            AddRailText(
                hud,
                root.transform,
                label,
                Vector2.zero,
                fromBottom ? 14f : 12f,
                selected ? Color.white : new Color(0.92f, 0.79f, 0.55f, 1f),
                selected ? FontStyles.Bold : FontStyles.Normal,
                stretch: true);
        }

        private static void AddRailText(
            Hud hud,
            Transform parent,
            string value,
            Vector2 position,
            float fontSize,
            Color color,
            FontStyles style,
            bool fromBottom = false,
            float width = 132f,
            bool stretch = false)
        {
            TMP_Text template = hud.m_pieceDescription ? hud.m_pieceDescription :
                hud.m_buildSelection;
            if (!template) return;
            TMP_Text text = UnityEngine.Object.Instantiate(template, parent, false);
            text.gameObject.name = Prefix + "Text";
            text.gameObject.SetActive(true);
            RectTransform rect = text.rectTransform;
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = new Vector2(7f, 0f);
                rect.offsetMax = new Vector2(-5f, 0f);
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0f, fromBottom ? 0f : 1f);
                rect.pivot = new Vector2(0f, fromBottom ? 0f : 1f);
                rect.anchoredPosition = position;
                rect.sizeDelta = new Vector2(width, 24f);
            }
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 8f;
            text.fontSizeMax = fontSize;
            text.fontStyle = style;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
        }

        private sealed class RectState
        {
            private readonly RectTransform rect;
            private readonly Vector2 size;
            private readonly Vector2 position;
            private readonly Vector2 anchorMin;
            private readonly Vector2 anchorMax;
            private readonly Vector2 pivot;
            private readonly Vector3 scale;

            public RectState(RectTransform rect)
            {
                this.rect = rect;
                size = rect.sizeDelta;
                position = rect.anchoredPosition;
                anchorMin = rect.anchorMin;
                anchorMax = rect.anchorMax;
                pivot = rect.pivot;
                scale = rect.localScale;
            }

            public RectTransform Rect => rect;

            public void Restore()
            {
                if (!rect) return;
                rect.anchorMin = anchorMin;
                rect.anchorMax = anchorMax;
                rect.pivot = pivot;
                rect.sizeDelta = size;
                rect.anchoredPosition = position;
                rect.localScale = scale;
            }
        }

        private static Color Accent(string group)
        {
            switch (group)
            {
                case HammerCatalogOrganizer.BlueprintsGroup:
                    return new Color(0.72f, 0.48f, 0.95f, 1f);
                case HammerCatalogOrganizer.ActionsGroup:
                    return new Color(0.92f, 0.42f, 0.24f, 1f);
                case HammerCatalogOrganizer.MetalGroup:
                    return new Color(0.60f, 0.75f, 0.82f, 1f);
                case HammerCatalogOrganizer.GlassGroup:
                    return new Color(0.48f, 0.86f, 0.88f, 1f);
                case HammerCatalogOrganizer.StoneGroup:
                case HammerCatalogOrganizer.BlackMarbleGroup:
                case HammerCatalogOrganizer.GraustenGroup:
                    return new Color(0.70f, 0.69f, 0.61f, 1f);
                case HammerCatalogOrganizer.AshWoodGroup:
                    return new Color(0.88f, 0.75f, 0.47f, 1f);
                case HammerCatalogOrganizer.DarkWoodGroup:
                    return new Color(0.63f, 0.43f, 0.27f, 1f);
                case HammerCatalogOrganizer.CoreWoodGroup:
                    return new Color(0.73f, 0.54f, 0.30f, 1f);
                case HammerCatalogOrganizer.WoodGroup:
                    return new Color(0.86f, 0.62f, 0.30f, 1f);
                default: return new Color(0.80f, 0.66f, 0.39f, 1f);
            }
        }
    }
}
