using UnityEngine;
public class Player : MonoBehaviour
{
    public static Player m_localPlayer;
    public readonly Inventory Inventory = new Inventory();
    public Inventory GetInventory() => Inventory;
}
public class Inventory
{
    public int Amount;
    public int CountItems(string name, int quality = -1, bool matchWorldLevel = true) => Amount;
}
