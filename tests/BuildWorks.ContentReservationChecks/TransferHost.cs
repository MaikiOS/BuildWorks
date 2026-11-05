using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal struct Vector2i { internal int x, y; internal Vector2i(int x, int y) { this.x = x; this.y = y; } }
    internal struct Vector3 {
        internal float x,y,z;
        internal Vector3(float x,float y,float z) { this.x=x; this.y=y; this.z=z; }
        internal static float Distance(Vector3 a,Vector3 b) => (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
        internal static Vector3 Scale(Vector3 a,Vector3 b) => new Vector3(a.x*b.x,a.y*b.y,a.z*b.z);
        internal static Vector3 up=>new Vector3(0,1,0);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator *(Vector3 a,float value)=>new Vector3(a.x*value,a.y*value,a.z*value);
    }
    internal struct Quaternion {
        internal float x,y,z,w;
        internal Quaternion(float x,float y,float z,float w) { this.x=x;this.y=y;this.z=z;this.w=w; }
        internal static Quaternion identity => new Quaternion(0,0,0,1);
        internal static float Angle(Quaternion a,Quaternion b) => a.Equals(b) ? 0 : 180;
    }
    internal sealed class Transform {
        internal Vector3 position = new Vector3(), localScale = new Vector3(1,1,1);
        internal Quaternion rotation = new Quaternion(0,0,0,1);
    }
    internal sealed class GameObject : Obj
    {
        internal bool activeInHierarchy=true;
        internal string name = "piece_chest_wood";
        internal readonly Transform transform = new Transform();
        internal Piece Piece;
        internal Container Container;
        internal ZNetView RootView;
        internal ItemStand Stand;
        internal ItemDrop Drop;
        internal Rigidbody Body;
        internal WearNTear Wear;
        internal Collider[] Colliders=Array.Empty<Collider>();
        internal T[] GetComponentsInChildren<T>(bool includeInactive=false) where T:class
        {
            if(typeof(T)==typeof(Obj)) {
                var components=new Obj[]{Piece,Container,Stand,Wear,RootView}.Where(value=>value!=null).ToArray();
                foreach(var component in components)component.gameObject=this;
                return components.OfType<T>().ToArray();
            }
            return Colliders.OfType<T>().ToArray();
        }
        internal T GetComponentInParent<T>() where T:class => GetComponent<T>();
        internal T GetComponent<T>() where T : class {
            var found=typeof(T) == typeof(Piece) ? Piece as T :
            typeof(T) == typeof(ZNetView) ? RootView as T : typeof(T) == typeof(ItemStand) ? Stand as T :
            typeof(T) == typeof(ItemDrop) ? Drop as T : typeof(T) == typeof(Rigidbody) ? Body as T :
            typeof(T)==typeof(WearNTear)?Wear as T:Container as T;
            if(found is Obj component)component.gameObject=this;
            return found;
        }
    }
    internal sealed partial class Piece
    {
        internal new GameObject gameObject;
        internal T GetComponent<T>() where T:class => gameObject.GetComponent<T>();
        internal Transform transform => gameObject.transform;
        internal long Creator = 100;
        internal long GetCreator() => Creator;
    }
    internal sealed partial class Player
    {
        internal static Player m_localPlayer;
        internal static bool m_localPlayerExists = true;
        internal readonly HashSet<ItemDrop.ItemData> Queue = new HashSet<ItemDrop.ItemData>();
        internal ItemDrop.ItemData Equipped;
        internal Action BeforeUnequip;
        internal long GetPlayerID() => 100;
        internal bool Dead;
        internal float m_maxInteractDistance=3.5f;
        internal GameObject Hover;
        internal Vector3 Eye=new Vector3();
        internal GameObject GetHoverObject()=>Hover;
        internal Vector3 GetEyePoint()=>Eye;
        internal bool IsDead() => Dead;
        internal bool IsEquipActionQueued(ItemDrop.ItemData item) => Queue.Contains(item);
        internal bool IsItemEquiped(ItemDrop.ItemData item) => ReferenceEquals(Equipped, item);
        internal void RemoveEquipAction(ItemDrop.ItemData item) => Queue.Remove(item);
        internal void UnequipItem(ItemDrop.ItemData item, bool _) { BeforeUnequip?.Invoke(); item.m_equipped = false; if (Equipped == item) Equipped = null; }
    }
    internal sealed partial class Inventory
    {
        internal int Width = 8, Height = 8, MoveCalls;
        internal Action Changed, BeforeMove, AfterCredit, AfterSave;
        internal Action m_onChanged;
        internal bool NoChange, ReturnFalse;
        internal int? PartialAmount;
        internal int GetWidth() => Width;
        internal int GetHeight() => Height;
        internal ItemDrop.ItemData GetItemAt(int x, int y) => Items.FirstOrDefault(item => item.m_gridPos.x == x && item.m_gridPos.y == y);
        internal bool MoveItemToThis(Inventory from, ItemDrop.ItemData item, int amount, int x, int y)
        {
            MoveCalls++; BeforeMove?.Invoke();
            if (NoChange) return !ReturnFalse;
            int moved = PartialAmount ?? amount;
            var copy = item.Clone(); copy.m_stack = moved; copy.m_gridPos = new Vector2i(x, y);
            Items.Add(copy); item.m_stack -= moved; AfterCredit?.Invoke();
            if (Player.m_localPlayerExists) Changed?.Invoke();
            AfterSave?.Invoke();
            if (item.m_stack == 0) from.Items.Remove(item);
            from.Changed?.Invoke(); return !ReturnFalse;
        }
        internal byte[] SaveBytes()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(110); writer.Write((ushort)Items.Count);
                foreach (var item in Items) Program.WriteItem(writer, item);
                writer.Flush(); return stream.ToArray();
            }
        }
    }
    internal struct ZDOID { internal long Id, User; internal long UserID => User == 0 ? 99 : User; internal uint ID => (uint)Id; internal bool IsNone() => Id == 0;
        internal ZDOID(long user,uint id){User=user;Id=id;}
        internal static ZDOID None => default;
        public static bool operator ==(ZDOID a,ZDOID b) => a.Id==b.Id && a.User==b.User;
        public static bool operator !=(ZDOID a,ZDOID b) => !(a==b);
        public override bool Equals(object value) => value is ZDOID other && this==other;
        public override int GetHashCode() => Id.GetHashCode() ^ User.GetHashCode();
    }
    internal static class ZDOVars { internal const int s_items = 1, s_creator = 2, s_itemData = 949524933; }
    internal static class BlueprintConstructionPartExecution
    {
        internal static readonly int MarkerKey = "ostrix_buildworks_project_operation_v1".GetStableHashCode();
        internal static readonly int ReceiptKey = "ostrix_buildworks_project_receipt_v1".GetStableHashCode();
        internal static GameObject MountTarget;
        internal static readonly Dictionary<Guid,GameObject> MountTargets=new Dictionary<Guid,GameObject>();
        internal static bool StorageUnresolved;
        internal static void RequireStoragePreflight(long _) { if(StorageUnresolved) throw new InvalidOperationException("unresolved storage"); }
        internal static BlueprintForeignPartData StorageDemand(BlueprintConstructionProjectStore.Source source)=>PartContentsTickProbe.StorageDemand(source);
        internal static GameObject FindMarked(Guid operation,string prefab) => MountTargets.TryGetValue(operation,out var target)?target:MountTarget;
    }
    internal sealed class ZDO { internal ZDOID m_uid = new ZDOID { Id = 1 }; internal uint DataRevision; internal byte[] Raw;
        internal bool Valid=true;
        internal bool IsValid()=>Valid;
        internal bool Persistent = true;
        internal int PrefabHash = 123;
        internal readonly Dictionary<int,long> Longs = new Dictionary<int,long>();
        internal long GetLong(int key,long fallback) => Longs.TryGetValue(key,out var value)?value:fallback;
        internal void Set(int key,long value) { Longs[key]=value;DataRevision++; }
        internal readonly Dictionary<int, string> Strings = new Dictionary<int, string>();
        internal readonly Dictionary<int, byte[]> ByteArrays = new Dictionary<int, byte[]>();
        internal Action BeforeSet, AfterSet;
        internal string GetString(int key, string fallback) => Strings.TryGetValue(key, out string value) ? value : fallback;
        internal void Set(int key, string value) { BeforeSet?.Invoke(); if (GetString(key, null) != value) { Strings[key] = value; DataRevision++; } AfterSet?.Invoke(); }
        internal void Set(int key,byte[] value) { BeforeSet?.Invoke(); ByteArrays[key]=(byte[])value.Clone(); DataRevision++; AfterSet?.Invoke(); }
        internal int GetPrefab() => PrefabHash;
        internal byte[] GetByteArray(int key, byte[] fallback = null) => key == ZDOVars.s_items ? Raw ?? fallback : ByteArrays.TryGetValue(key, out var value) ? value : fallback; }
    internal sealed class ZNetView : Obj
    {
        internal bool m_persistent=true;
        internal readonly ZDO Data = new ZDO(); internal bool Valid = true, Owner = true;
        internal bool IsValid() => Valid; internal bool IsOwner() => Owner; internal ZDO GetZDO() => Data;
    }
    internal sealed class World
    {
        internal long m_uid = 1;
        internal FileHelpers.FileSource m_fileSource = FileHelpers.FileSource.Local;
        internal string Directory;
        internal string GetSaveDirectory(FileHelpers.FileSource _) => Directory;
    }
    internal sealed partial class ZNet : Obj
    {
        internal static ZNet instance = new ZNet(); internal World World = new World();
        internal bool Server = true, Saving;
        internal bool Dedicated;
        internal static bool Open; internal static bool m_loadError;
        internal readonly List<object> Peers = new List<object>();
        internal Action SaveAction;
        internal bool IsDedicated() => Dedicated;
        internal static bool IsOpenServer() => Open;
        internal List<object> GetPeers() => Peers;
        internal void Save(bool sync, bool saveOtherPlayerProfiles, bool waitForNextFrame) { SaveAction?.Invoke(); }
        internal World GetWorld() => World;
        internal long GetWorldUID() => World.m_uid;
        internal bool IsServer() => Server;
        internal bool IsSaving() => Saving;
    }
    internal static class SaveSystem
    {
        internal static uint Generation = 7;
        internal static string Suffix = "";
        internal static SaveSystemSessionFlags Flags;
        internal static bool HasSessionFlag(SaveSystemSessionFlags flag) => (Flags & flag) != 0;
        internal static uint GetSaveNumber() => Generation;
        internal static string MainFwl2FileName() => "_main." + Generation + ".fwl2" + Suffix;
        internal static string MainChunksFileName() => "_main." + Generation + ".chunks" + Suffix;
        internal static string MainOKFileName() => "_main." + Generation + ".ok" + Suffix;
        internal static string MainDb2FileName() => "_main." + Generation + ".db2" + Suffix;
    }
    [Flags] internal enum SaveSystemSessionFlags { DontSaveCharacter = 1, DontSaveWorld = 2 }
    internal sealed partial class ZDOMan { internal static ZDOMan instance = new ZDOMan(); }
    internal static class InventoryGui { internal static bool Visible; internal static bool IsVisible() => Visible; }
    internal static class PrivateArea { internal static bool Allowed = true; internal static bool CheckAccess(Vector3 _, float radius, bool flash, bool ward) => Allowed; }
    internal class WearNTear : Obj
    {
        internal Action m_onDestroyed;
        private ZNetView m_nview;
        internal WearNTear(ZNetView view = null) { m_nview = view; }
        internal void Remove(bool blockDrops) {
            var witness=BlueprintConstructionCancellationRemoval.CaptureWear(this,blockDrops);Exception failure=null;
            try { m_onDestroyed?.Invoke();ZDOMan.instance.DestroyZDO(m_nview.GetZDO());BlueprintConstructionCancellationRemoval.Started++; }
            catch(Exception error){failure=error;}
            BlueprintConstructionCancellationRemoval.ConfirmWear(witness,blockDrops,failure);if(failure!=null)throw failure;
        }
    }
    internal class Container : Obj
    {
        private ZNetView m_nview = new ZNetView();
        private bool m_loading;
        private uint m_lastRevision;
        internal readonly Inventory Inventory;
        internal ZNetView m_rootObjectOverride;
        internal bool Allowed = true, InUse, Supported = true, CaptureAllowed = true;
        internal ZNetView View => m_nview;
        internal Container(int width) { Inventory = new Inventory { Width = width, Height = 1 }; Inventory.Changed = Save; Save(); }
        internal Inventory GetInventory() => Inventory;
        internal WearNTear NativeCallbacksForTest()
        { Inventory.m_onChanged = OnContainerChanged; return new WearNTear(View) { m_onDestroyed = OnDestroyed }; }
        private void OnContainerChanged() { }
        private void OnDestroyed() { }
        internal bool IsInUse() => InUse;
        private bool CheckAccess(long _) => Allowed;
        internal void SetLoading(bool value) => m_loading = value;
        internal void ReplaceView() => m_nview = new ZNetView();
        internal void MakeRevisionStale() => m_lastRevision--;
        internal void SaveForTest() => Save();
        internal void SyncCacheForTest() => m_lastRevision = m_nview.Data.DataRevision;
        internal void LoadForTest(string marker)
        {
            m_nview.Data.DataRevision = 0; m_lastRevision = 0; m_nview.Data.Raw = Inventory.SaveBytes();
            if (marker != null) m_nview.Data.Strings[BlueprintStorageCapture.TransferMarkerKey] = marker;
        }
        private void Save()
        {
            if (m_loading || !m_nview.IsOwner()) return;
            m_nview.Data.Raw = Inventory.SaveBytes(); m_nview.Data.DataRevision++; m_lastRevision = m_nview.Data.DataRevision;
        }
    }
    internal static partial class BlueprintStorageCapture
    {
        internal static bool TrySnapshotInventory(Inventory inventory, out BlueprintForeignPartData data)
        {
            string info = inventory.Width + ":" + inventory.Height + ":" + inventory.Items.Count + ":" + inventory.Items.Sum(item => item.m_stack);
            if (inventory.Items.Count > 0) info += "|" + string.Join("|", inventory.Items.Select(item => item.m_dropPrefab.name).Distinct().OrderBy(name => name, StringComparer.Ordinal));
            data = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, info, Convert.ToBase64String(inventory.SaveBytes()), null);
            return BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out _);
        }
        internal static Action Captured;
        internal static (int Width, int Height)? PrefabGrid(GameObject placed) => placed?.Container?.Supported == true
            ? (placed.Container.Inventory.Width, placed.Container.Inventory.Height) : ((int, int)?)null;
        internal static bool TryCapture(Piece piece, Player player, out BlueprintForeignPartData data)
        {
            data = null; var container = piece.gameObject.Container; var inventory = container.Inventory;
            if (!container.CaptureAllowed || container.View.Data.Raw == null || !inventory.SaveBytes().SequenceEqual(container.View.Data.Raw)) return false;
            string info = inventory.Width + ":" + inventory.Height + ":" + inventory.Items.Count + ":" + inventory.Items.Sum(item => item.m_stack);
            if (inventory.Items.Count > 0) info += "|" + string.Join("|", inventory.Items.Select(item => item.m_dropPrefab.name).Distinct().OrderBy(name => name, StringComparer.Ordinal));
            data = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, info, Convert.ToBase64String(container.View.Data.Raw), null);
            bool valid = BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out _);
            if (valid) Captured?.Invoke();
            return valid;
        }
    }
    internal sealed class BlueprintConstructionRefund
    {
        private readonly Inventory source;
        internal bool Recorded = true; internal string Error;
        internal BlueprintConstructionRefund(Inventory source) { this.source = source; }
        internal bool IsFor(Inventory value) => ReferenceEquals(source, value);
        internal (ItemDrop.ItemData Item, int Before, int Paid)[] DescribePaid() => Array.Empty<(ItemDrop.ItemData, int, int)>();
    }
}
namespace BepInEx { internal static class Paths { internal static string ConfigPath; } }
