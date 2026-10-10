using UnityEngine;
public class Player : Humanoid
{
    public static Player m_localPlayer;
    public long GetPlayerID() => 17;
    // Inert native search contract fixture; world target collection is supplied by the test.
    private readonly System.Collections.Generic.List<Transform> m_tempSnapPoints1 = new System.Collections.Generic.List<Transform>();
    private readonly System.Collections.Generic.List<Transform> m_tempSnapPoints2 = new System.Collections.Generic.List<Transform>();
    private int m_manualSnapPoint = -1;
    public readonly System.Collections.Generic.List<Transform> SnapTargets = new System.Collections.Generic.List<Transform>();
    public int NativeSnapCalls;
    public int ManualSnap { get => m_manualSnapPoint; set => m_manualSnapPoint = value; }
    public System.Action BeforeSnapIteration;
    public bool TestSnap(Transform ghost, float radius, out Transform a, out Transform b) =>
        FindClosestSnapPoints(ghost, radius, out a, out b, null);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private bool FindClosestSnapPoints(Transform ghost, float radius, out Transform a, out Transform b,
        System.Collections.Generic.List<Piece> pieces)
    {
        m_tempSnapPoints1.Clear(); ghost.GetComponent<Piece>().GetSnapPoints(m_tempSnapPoints1);
        m_tempSnapPoints2.Clear(); m_tempSnapPoints2.AddRange(SnapTargets);
        a = null; b = null; float best = 9999999f;
        if (m_manualSnapPoint >= 0)
        {
            if (!FindClosestSnappoint(m_tempSnapPoints1[m_manualSnapPoint].position, m_tempSnapPoints2,
                radius, out Transform target, out _)) return false;
            a = m_tempSnapPoints1[m_manualSnapPoint]; b = target; return true;
        }
        foreach (Transform source in m_tempSnapPoints1)
        {
            BeforeSnapIteration?.Invoke();
            if (FindClosestSnappoint(source.position, m_tempSnapPoints2, radius, out Transform target, out float distance) && distance < best)
            { best = distance; a = source; b = target; }
        }
        return a != null;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private bool FindClosestSnappoint(Vector3 point, System.Collections.Generic.List<Transform> points,
        float radius, out Transform closest, out float distance)
    {
        ++NativeSnapCalls; closest = null; distance = 999999f;
        foreach (Transform target in points)
        {
            float candidate = Vector3.Distance(target.position, point);
            if (!(candidate > radius) && candidate < distance) { closest = target; distance = candidate; }
        }
        return closest != null;
    }
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
