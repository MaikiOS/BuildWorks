using UnityEngine;
public class Player : MonoBehaviour
{
    public static Player m_localPlayer;
    public readonly Inventory Inventory = new Inventory();
    public Inventory GetInventory() => Inventory;
    public long GetPlayerID() => 17;
}
public class Inventory
{
    public int Amount;
    public int CountItems(string name, int quality = -1, bool matchWorldLevel = true) => Amount;
    public int Width = 5, Height = 2;
    public readonly System.Collections.Generic.List<ItemDrop.ItemData> Items = new System.Collections.Generic.List<ItemDrop.ItemData>();
    public byte[] Snapshot;
    public System.Action OnSave;
    public int GetWidth() => Width;
    public int GetHeight() => Height;
    public System.Collections.Generic.List<ItemDrop.ItemData> GetAllItems() => Items;
    // Inert API fixture only. Native byte layout is independently checked against Valheim IL.
    public void Save(ZPackage package) { package.Bytes = (byte[])Snapshot.Clone(); OnSave?.Invoke(); }
}
public class ZPackage { public byte[] Bytes; public byte[] GetArray() => Bytes; }

// Inert host hash fixture, verified against the native StringExtensionMethods IL.
public static class StringExtensionMethods
{
    public static int GetStableHashCode(this string text)
    {
        unchecked
        {
            int first = 5381, second = first;
            for (int index = 0; index < text.Length && text[index] != 0; index += 2)
            {
                first = first * 33 ^ text[index];
                if (index == text.Length - 1 || text[index + 1] == 0) break;
                second = second * 33 ^ text[index + 1];
            }
            return first + second * 1566083941;
        }
    }
}
