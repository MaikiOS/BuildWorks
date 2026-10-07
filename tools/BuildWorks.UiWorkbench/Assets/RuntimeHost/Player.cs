using UnityEngine;
public class Player : Humanoid
{
    public static Player m_localPlayer;
    public long GetPlayerID() => 17;
}
public class Humanoid : MonoBehaviour
{
    public readonly Inventory Inventory = new Inventory();
    public Inventory GetInventory() => Inventory;
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
    public bool ContainsItem(ItemDrop.ItemData item) => Items.Contains(item);
    public bool RemoveOneItem(ItemDrop.ItemData item)
    {
        if (!Items.Contains(item)) return false;
        if (item.m_stack > 1) --item.m_stack; else Items.Remove(item);
        return true;
    }
    // Inert API fixture only. Native byte layout is independently checked against Valheim IL.
    public void Save(ZPackage package) { package.Bytes = (byte[])Snapshot.Clone(); OnSave?.Invoke(); }
}
public class ZPackage
{
    public byte[] Bytes;
    private readonly System.IO.MemoryStream stream = new System.IO.MemoryStream();
    public void Write(byte value) => stream.WriteByte(value);
    public System.IO.BinaryWriter Writer => new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, true);
    public byte[] GetArray() => Bytes ?? stream.ToArray();
}

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
