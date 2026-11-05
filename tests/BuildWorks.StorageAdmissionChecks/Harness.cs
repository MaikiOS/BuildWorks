using System;
using System.Reflection;

// Actual production predicates; Piece/component/grid substitutes do not prove Unity physics or prefab safety.
namespace BuildWorksAdmissionChecks
{
    public class NativeObject { public static implicit operator bool(NativeObject value) { return !ReferenceEquals(value, null); } }
    public class StationExtension : NativeObject { }
    public class Container : NativeObject { }
    public class ItemStand : NativeObject { }
    public class ArmorStand : NativeObject { }
    public class TerrainModifier : NativeObject { }
    public class Piece : NativeObject
    {
        public bool m_repairPiece, m_removePiece, m_groundPiece, m_groundOnly, m_cultivatedGroundOnly,
            m_vegetationGroundOnly, m_waterPiece, m_noInWater, m_notOnWood, m_notOnTiltingSurface,
            m_inCeilingOnly, m_notOnFloor;
        public NativeObject m_mustConnectTo;
        public float m_blockRadius;
        public bool Station, Terrain, Storage, Stand, Armor, Grid;
        public Piece gameObject { get { return this; } }
        public T GetComponent<T>() where T : NativeObject, new()
        {
            bool found = typeof(T) == typeof(StationExtension) ? Station : typeof(T) == typeof(Container) ? Storage :
                typeof(T) == typeof(ItemStand) ? Stand : typeof(T) == typeof(ArmorStand) && Armor;
            return found ? new T() : null;
        }
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T : new()
        { return Terrain ? new[] { new T() } : new T[0]; }
    }
    public static class BlueprintStorageCapture
    { public static int? PrefabGrid(Piece piece) { return piece.Storage && piece.Grid ? 10 : (int?)null; } }

    public class PrecisionPlacementSession
    {
        // PRODUCTION_PRECISION
        // PRODUCTION_PROJECT
        static int checks;
        static void Require(bool value, string name)
        { checks++; if (!value) throw new InvalidOperationException(name); }
        static Piece Chest() { return new Piece { Storage=true, Grid=true, m_noInWater=true, m_notOnTiltingSurface=true }; }
        public static void Run()
        {
            Require(!SupportsStorageProjectPart(null) && !SupportsProjectPart(null) && !SupportsPrecisionPlacement(null), "Null piece admitted");
            var chest = Chest();
            Require(SupportsStorageProjectPart(chest), "REPRO349: native wooden chest surface flags block storage project admission");
            Require(!SupportsPrecisionPlacement(chest) && !SupportsProjectPart(chest), "Storage surface exception leaked to structural precision/projects");
            foreach (var flags in new[] { new[] { false,false }, new[] { true,false }, new[] { false,true }, new[] { true,true } })
            {
                chest=Chest(); chest.m_noInWater=flags[0]; chest.m_notOnTiltingSurface=flags[1];
                Require(SupportsStorageProjectPart(chest), "Storage flag combination rejected");
                Require(SupportsPrecisionPlacement(chest) == (!flags[0] && !flags[1]), "Existing precision surface semantics changed");
                Require(!SupportsProjectPart(chest), "Container entered structural project route");
            }
            foreach (string name in new[] { "m_repairPiece","m_removePiece","m_groundPiece","m_groundOnly","m_cultivatedGroundOnly",
                "m_vegetationGroundOnly","m_waterPiece","m_notOnWood","m_inCeilingOnly","m_notOnFloor" })
            {
                chest=Chest(); typeof(Piece).GetField(name).SetValue(chest,true);
                Require(!SupportsStorageProjectPart(chest), "Storage bypassed restriction: "+name);
                chest.m_noInWater=chest.m_notOnTiltingSurface=false;
                Require(!SupportsPrecisionPlacement(chest), "Precision bypassed restriction: "+name);
            }
            chest=Chest(); chest.m_mustConnectTo=new NativeObject(); Require(!SupportsStorageProjectPart(chest), "Connection restriction bypassed");
            foreach (float radius in new[] { 1f, float.PositiveInfinity, float.NaN })
            { chest=Chest(); chest.m_blockRadius=radius; Require(!SupportsStorageProjectPart(chest), "Block radius bypassed"); }
            chest=Chest(); chest.Station=true; Require(!SupportsStorageProjectPart(chest), "Station extension bypassed");
            chest=Chest(); chest.Terrain=true; Require(!SupportsStorageProjectPart(chest), "Terrain modifier bypassed");
            chest=Chest(); chest.Grid=false; Require(!SupportsStorageProjectPart(chest), "Unsupported native grid admitted");
            chest=Chest(); chest.Storage=false; Require(!SupportsStorageProjectPart(chest), "Non-container admitted");
            var structural=new Piece(); Require(SupportsProjectPart(structural) && SupportsPrecisionPlacement(structural) && !SupportsStorageProjectPart(structural), "Structural baseline changed");
            structural.Stand=true; Require(!SupportsProjectPart(structural), "Item stand entered structural project");
            structural.Stand=false; structural.Armor=true; Require(!SupportsProjectPart(structural), "Armor stand entered structural project");
            Console.WriteLine("PASS storage admission: "+checks+" actual extracted predicate assertions; no Unity/native placement proof.");
        }
    }
}
