using UnityEngine;

// Read-only capture gates only; this host fixture never restores or opens an inventory.
public class Container : MonoBehaviour
{
    private ZNetView m_nview;
    public readonly Inventory Inventory = new Inventory();
    public bool Denied, Busy, m_autoDestroyEmpty, m_checkGuardStone;
    public Vagon m_wagon;
    public DropTable m_defaultItems = new DropTable();
    private bool CheckAccess(long playerId) => !Denied;
    public bool IsInUse() => Busy;
    public Inventory GetInventory() => Inventory;
}
public class Vagon : MonoBehaviour { }
public class DerivedStorage : Container { }
public class DropTable
{
    public class DropData { }
    public System.Collections.Generic.List<DropData> m_drops = new System.Collections.Generic.List<DropData>();
}
public class TombStone : MonoBehaviour { }
public class Corpse : MonoBehaviour { }
public class Incinerator : MonoBehaviour { }
public static class PrivateArea
{
    public static bool Allowed = true;
    public static bool CheckAccess(Vector3 position, float radius, bool flash, bool wardCheck)
    { if (flash) throw new System.InvalidOperationException("Capture flashed ward"); return Allowed; }
}
