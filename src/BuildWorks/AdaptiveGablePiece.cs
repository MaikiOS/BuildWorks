using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class AdaptiveGablePieceRegistry
    {
        internal const string PrefabName = "BuildWorks_AdaptiveGablePanel";
        private static GameObject holder;
        private static GameObject prefab;
        private static Texture2D texture;
        private static Material material;
        private static Sprite icon;
        private static ZNetScene registeredScene;
        private static PieceTable registeredPieceTable;
        private static bool networkRegistered;

        public static void RegisterNetworkPrefab(ZNetScene scene)
        {
            if (!scene) return;
            if (registeredScene != scene)
            {
                DetachFromRegistries();
            }
            networkRegistered = false;
            GameObject existingNamed = scene.m_prefabs.Find(
                item => item && item.name == PrefabName);
            if (existingNamed)
            {
                networkRegistered = existingNamed == prefab;
                if (networkRegistered)
                {
                    registeredScene = scene;
                }
                if (networkRegistered && ObjectDB.instance)
                    RegisterHammerPiece(ObjectDB.instance);
                return;
            }
            int hash = PrefabName.GetStableHashCode();
            foreach (GameObject existing in scene.m_prefabs)
            {
                if (existing && existing.name.GetStableHashCode() == hash)
                {
                    Debug.LogError("BuildWorks adaptive piece disabled: prefab hash collision with " +
                        existing.name + ".");
                    return;
                }
            }
            EnsurePrefab(scene);
            scene.m_prefabs.Add(prefab);
            registeredScene = scene;
            networkRegistered = true;
            if (ObjectDB.instance) RegisterHammerPiece(ObjectDB.instance);
        }

        public static void RegisterHammerPiece(ObjectDB database)
        {
            if (!database || !prefab || !networkRegistered) return;
            GameObject hammer = database.GetItemPrefab("Hammer");
            ItemDrop wood = database.GetItemPrefab("Wood")?.GetComponent<ItemDrop>();
            ItemDrop hammerDrop = hammer ? hammer.GetComponent<ItemDrop>() : null;
            PieceTable table = hammerDrop?.m_itemData?.m_shared?.m_buildPieces;
            if (!table || !wood) return;

            Piece piece = prefab.GetComponent<Piece>();
            piece.m_resources = new[]
            {
                new Piece.Requirement
                {
                    m_resItem = wood,
                    m_amount = 2,
                    m_recover = true
                }
            };
            if (!table.m_pieces.Exists(item => item && item.name == PrefabName))
                table.m_pieces.Add(prefab);
            registeredPieceTable = table;
        }

        public static void Dispose()
        {
            DetachFromRegistries();
            if (holder) UnityEngine.Object.Destroy(holder);
            if (icon) UnityEngine.Object.Destroy(icon);
            if (material) UnityEngine.Object.Destroy(material);
            if (texture) UnityEngine.Object.Destroy(texture);
            holder = null;
            prefab = null;
            icon = null;
            material = null;
            texture = null;
            networkRegistered = false;
        }

        public static void AbortRegistration() => Dispose();

        private static void DetachFromRegistries()
        {
            if (registeredPieceTable != null && registeredPieceTable.m_pieces != null && prefab)
                registeredPieceTable.m_pieces.Remove(prefab);
            if (registeredScene && registeredScene.m_prefabs != null && prefab)
                registeredScene.m_prefabs.Remove(prefab);
            registeredPieceTable = null;
            registeredScene = null;
            networkRegistered = false;
        }

        private static void EnsurePrefab(ZNetScene scene)
        {
            if (prefab) return;
            holder = new GameObject("BuildWorks_RuntimePrefabs")
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            UnityEngine.Object.DontDestroyOnLoad(holder);
            holder.SetActive(false);

            prefab = new GameObject(PrefabName);
            prefab.transform.SetParent(holder.transform, false);
            prefab.layer = LayerMask.NameToLayer("piece");

            ZNetView view = prefab.AddComponent<ZNetView>();
            view.m_persistent = true;
            view.m_distant = false;
            view.m_type = ZDO.ObjectType.Solid;
            view.m_syncInitialScale = false;

            Piece piece = prefab.AddComponent<Piece>();
            piece.m_name = BuildWorksLocalization.Token("adaptive_gable.name");
            piece.m_description = BuildWorksLocalization.Token("adaptive_gable.description");
            piece.m_category = Piece.PieceCategory.BuildingWorkbench;
            piece.m_canRotate = true;
            piece.m_canBeRemoved = true;
            piece.m_allowedInDungeons = true;
            piece.m_resources = Array.Empty<Piece.Requirement>();

            GameObject visual = new GameObject("BuildWorks_GableVisual");
            visual.transform.SetParent(prefab.transform, false);
            visual.layer = prefab.layer;
            visual.AddComponent<MeshFilter>();
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMaterial(scene);
            MeshCollider collider = prefab.AddComponent<MeshCollider>();

            AdaptiveGablePiece runtime = prefab.AddComponent<AdaptiveGablePiece>();
            runtime.Initialize(visual.GetComponent<MeshFilter>(), collider);
            runtime.BuildTemplate();

            WearNTear wear = prefab.AddComponent<WearNTear>();
            wear.m_new = visual;
            wear.m_worn = visual;
            wear.m_broken = visual;
            wear.m_health = 200f;
            wear.m_materialType = WearNTear.MaterialType.Wood;
            wear.m_supports = true;
            wear.m_noRoofWear = true;
            piece.m_icon = icon;
        }

        private static Material GetMaterial(ZNetScene scene)
        {
            if (material) return material;
            texture = CreateWoodTexture();
            icon = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                64f);
            icon.name = "BuildWorks_AdaptiveGableIcon";

            Material vanillaWood = FindVanillaWoodMaterial(scene);
            Shader fallback = Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (!vanillaWood && !fallback)
                throw new InvalidOperationException("No compatible material found.");
            material = vanillaWood ? new Material(vanillaWood) : new Material(fallback);
            material.name = "BuildWorks_OriginalNordicTimber";
            material.hideFlags = HideFlags.HideAndDontSave;
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            return material;
        }

        private static Material FindVanillaWoodMaterial(ZNetScene scene)
        {
            GameObject wall = scene.m_prefabs.Find(item => item && item.name == "woodwall");
            MeshRenderer renderer = wall ? wall.GetComponentInChildren<MeshRenderer>(true) : null;
            return renderer ? renderer.sharedMaterial : null;
        }

        private static Texture2D CreateWoodTexture()
        {
            const int size = 64;
            Texture2D result = new Texture2D(size, size, TextureFormat.RGBA32, true, false)
            {
                name = "BuildWorks_OriginalNordicTimber_Albedo",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; ++y)
            {
                bool seam = y % 16 < 2;
                for (int x = 0; x < size; ++x)
                {
                    int grain = ((x * 17 + y * 31 + (x * y) % 19) & 15) - 7;
                    int band = (y / 16 & 1) == 0 ? 8 : -4;
                    pixels[y * size + x] = seam
                        ? new Color32(48, 30, 18, 255)
                        : new Color32(
                            (byte)Mathf.Clamp(108 + band + grain, 0, 255),
                            (byte)Mathf.Clamp(69 + band + grain / 2, 0, 255),
                            (byte)Mathf.Clamp(36 + grain / 3, 0, 255),
                            255);
                }
            }
            result.SetPixels32(pixels);
            result.Apply(true, true);
            return result;
        }
    }

    internal sealed class AdaptiveGablePiece : MonoBehaviour, IPlaced
    {
        private const int SchemaVersion = 1;
        private const string SchemaKey = "BuildWorks_GableSchema";
        private const string WidthKey = "BuildWorks_GableWidthMm";
        private const string LeftHeightKey = "BuildWorks_GableLeftHeightMm";
        private const string RightHeightKey = "BuildWorks_GableRightHeightMm";
        private const string ThicknessKey = "BuildWorks_GableThicknessMm";
        private const int DefaultWidthMm = 2000;
        private const int DefaultLeftHeightMm = 1000;
        private const int DefaultRightHeightMm = 2000;
        private const int DefaultThicknessMm = 180;

        [SerializeField] private int widthMm = DefaultWidthMm;
        [SerializeField] private int leftHeightMm = DefaultLeftHeightMm;
        [SerializeField] private int rightHeightMm = DefaultRightHeightMm;
        [SerializeField] private int thicknessMm = DefaultThicknessMm;

        private MeshFilter filter;
        private MeshCollider collider;
        private Mesh mesh;
        private readonly List<Transform> snapPoints = new List<Transform>(8);

        public void Initialize(MeshFilter targetFilter, MeshCollider targetCollider)
        {
            filter = targetFilter;
            collider = targetCollider;
        }

        public void BuildTemplate()
        {
            EnsureSnapPoints();
            Rebuild(widthMm, leftHeightMm, rightHeightMm, thicknessMm);
        }

        public int WidthMm => widthMm;
        public int LeftHeightMm => leftHeightMm;
        public int RightHeightMm => rightHeightMm;
        public int ThicknessMm => thicknessMm;

        public void AdjustDimension(int dimension, int deltaMm)
        {
            int width = widthMm;
            int left = leftHeightMm;
            int right = rightHeightMm;
            int thickness = thicknessMm;
            switch (dimension)
            {
                case 0: width = Mathf.Clamp(width + deltaMm, 250, 8000); break;
                case 1: left = Mathf.Clamp(left + deltaMm, 50, 6000); break;
                case 2: right = Mathf.Clamp(right + deltaMm, 50, 6000); break;
                case 3: thickness = Mathf.Clamp(thickness + deltaMm, 50, 500); break;
                default: return;
            }
            Rebuild(width, left, right, thickness);
        }

        public void CopyDimensionsFrom(AdaptiveGablePiece source)
        {
            if (source)
                Rebuild(source.widthMm, source.leftHeightMm, source.rightHeightMm, source.thicknessMm);
        }

        public void SetDimensions(int width, int leftHeight, int rightHeight, int thickness)
        {
            Rebuild(width, leftHeight, rightHeight, thickness);
        }

        private void Awake()
        {
            filter = filter ? filter : GetComponentInChildren<MeshFilter>(true);
            collider = collider ? collider : GetComponent<MeshCollider>();
            EnsureSnapPoints();
            Rebuild(widthMm, leftHeightMm, rightHeightMm, thicknessMm);
        }

        private void Start()
        {
            ZDO zdo = GetComponent<ZNetView>()?.GetZDO();
            if (zdo == null || zdo.GetInt(SchemaKey, 0) != SchemaVersion) return;
            try
            {
                Rebuild(
                    zdo.GetInt(WidthKey, DefaultWidthMm),
                    zdo.GetInt(LeftHeightKey, DefaultLeftHeightMm),
                    zdo.GetInt(RightHeightKey, DefaultRightHeightMm),
                    zdo.GetInt(ThicknessKey, DefaultThicknessMm));
            }
            catch (ArgumentOutOfRangeException)
            {
                Debug.LogWarning("BuildWorks adaptive panel had invalid saved dimensions; defaults restored.");
                Rebuild(DefaultWidthMm, DefaultLeftHeightMm, DefaultRightHeightMm, DefaultThicknessMm);
            }
        }

        public void OnPlaced()
        {
            ZNetView view = GetComponent<ZNetView>();
            ZDO zdo = view ? view.GetZDO() : null;
            if (zdo == null || !view.IsOwner()) return;
            zdo.Set(SchemaKey, SchemaVersion);
            zdo.Set(WidthKey, widthMm);
            zdo.Set(LeftHeightKey, leftHeightMm);
            zdo.Set(RightHeightKey, rightHeightMm);
            zdo.Set(ThicknessKey, thicknessMm);
        }

        private void Rebuild(int widthMm, int leftHeightMm, int rightHeightMm, int thicknessMm)
        {
            AdaptiveMeshData data = AdaptiveGablePanel.Generate(
                widthMm / 1000.0,
                leftHeightMm / 1000.0,
                rightHeightMm / 1000.0,
                thicknessMm / 1000.0);
            this.widthMm = widthMm;
            this.leftHeightMm = leftHeightMm;
            this.rightHeightMm = rightHeightMm;
            this.thicknessMm = thicknessMm;
            if (!mesh)
            {
                mesh = new Mesh { name = "BuildWorks_AdaptiveGableMesh" };
            }
            else
            {
                mesh.Clear();
            }
            mesh.vertices = ConvertPoints(data.Vertices);
            mesh.normals = ConvertPoints(data.Normals);
            mesh.tangents = ConvertTangents(data.Tangents);
            mesh.uv = ConvertUv(data.UV);
            mesh.triangles = CopyIndices(data.Triangles);
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            EnsureSnapPoints();
            for (int index = 0; index < snapPoints.Count; ++index)
                snapPoints[index].localPosition = ToUnity(data.SnapPoints[index]);
        }

        private void EnsureSnapPoints()
        {
            if (snapPoints.Count == 8) return;
            snapPoints.Clear();
            for (int index = 0; index < 8; ++index)
            {
                Transform existing = transform.Find("snappoint" + index);
                if (!existing)
                {
                    GameObject point = new GameObject("snappoint" + index);
                    point.transform.SetParent(transform, false);
                    point.tag = "snappoint";
                    existing = point.transform;
                }
                snapPoints.Add(existing);
            }
        }

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
        }

        private static Vector3[] ConvertPoints(IReadOnlyList<Point3> points)
        {
            Vector3[] result = new Vector3[points.Count];
            for (int index = 0; index < result.Length; ++index) result[index] = ToUnity(points[index]);
            return result;
        }

        private static Vector4[] ConvertTangents(IReadOnlyList<Point4> points)
        {
            Vector4[] result = new Vector4[points.Count];
            for (int index = 0; index < result.Length; ++index)
            {
                Point4 point = points[index];
                result[index] = new Vector4(
                    (float)point.X, (float)point.Y, (float)point.Z, (float)point.W);
            }
            return result;
        }

        private static Vector2[] ConvertUv(IReadOnlyList<Point2> points)
        {
            Vector2[] result = new Vector2[points.Count];
            for (int index = 0; index < result.Length; ++index)
                result[index] = new Vector2((float)points[index].X, (float)points[index].Z);
            return result;
        }

        private static int[] CopyIndices(IReadOnlyList<int> indices)
        {
            int[] result = new int[indices.Count];
            for (int index = 0; index < result.Length; ++index) result[index] = indices[index];
            return result;
        }

        private static Vector3 ToUnity(Point3 point) =>
            new Vector3((float)point.X, (float)point.Y, (float)point.Z);
    }
}
