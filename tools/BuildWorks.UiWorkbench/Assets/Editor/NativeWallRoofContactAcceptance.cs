using System;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class NativeWallRoofContactAcceptance
{
    public static void Verify()
    {
        GameObject ghost = null, roof = null;
        try
        {
            // Recorded native test5 original1 and its paid original2 support, run161753.
            ghost = new GameObject("RecordedWallGhost") { layer = 2 };
            ghost.transform.SetPositionAndRotation(new Vector3(2565.72949f, 42.3642426f, 142.129883f),
                new Quaternion(0, .707106829f, 0, -.707106829f));
            ghost.transform.localScale = new Vector3(1.00000024f, 1, 1.00000024f);
            ghost.AddComponent<WearNTear>();
            var wall = ghost.AddComponent<BoxCollider>();
            wall.size = new Vector3(2, 2, .3f);
            roof = new GameObject("RecordedRoofSupport");
            roof.AddComponent<WearNTear>().m_supports = true;
            void Add(Vector3 position, Quaternion rotation, Vector3 scale, Vector3 size)
            {
                var item = new GameObject("collider") { layer = 0 };
                item.transform.SetParent(roof.transform, false);
                item.transform.SetPositionAndRotation(position, rotation);
                item.transform.localScale = scale;
                var box = item.AddComponent<BoxCollider>();
                box.size = size;
                box.center = new Vector3(0, -.0000610351563f, 0);
            }
            Add(new Vector3(2566.72949f, 42.3642426f, 143.129883f),
                new Quaternion(.270598054f, -.653281569f, -.270598054f, -.65328151f),
                new Vector3(.400000125f, .599999905f, 2.83604789f),
                new Vector3(1.00000072f, 1.00000095f, 1.00000024f));
            Add(new Vector3(2566.72949f, 42.3642426f, 143.134888f),
                new Quaternion(.270598054f, .65328151f, .270598054f, -.65328151f),
                new Vector3(.400000125f, .600000024f, 2.83604741f),
                new Vector3(1.00000072f, 1.00000024f, 1.00000012f));
            Physics.SyncTransforms();
            if (!BuildWorksPlacementValidation.TryGetColliderBounds(wall, out Bounds shape, out Quaternion rotation))
                throw new InvalidOperationException("Recorded wall collider bounds missing.");
            var overlap = Physics.OverlapBox(shape.center, shape.extents + Vector3.one * .15f,
                rotation, 1, QueryTriggerInteraction.Ignore);
            if (overlap.Length == 0) throw new InvalidOperationException("Native expanded wall envelope misses recorded roof.");
            bool valid = BuildWorksPlacementValidation.TryFindContact(ghost, wall.bounds, 1,
                new Vector3(2562.17358f, 42.9073868f, 138.255814f), 8, PlacementContactSides.All,
                out RaycastHit contact, out PlacementContactFailure failure);
            if (!valid || !contact.collider || !contact.collider.transform.IsChildOf(roof.transform))
                throw new InvalidOperationException("Recorded wall/roof contact refused: " + failure);
            Debug.Log("BUILDWORKS_NATIVE_WALL_ROOF_CONTACT_OK actual recorded two-box support / native envelope / real contact");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        finally
        {
            if (ghost) Object.DestroyImmediate(ghost);
            if (roof) Object.DestroyImmediate(roof);
        }
    }
}
