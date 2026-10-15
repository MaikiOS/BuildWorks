// Isolated sign host contracts, not a substitute for Valheim permission/network acceptance.
using System.Collections.Generic;
using UnityEngine;

public sealed class ZNetView : MonoBehaviour
{
    public static bool m_useInitZDO;
    public static ZDO m_initZDO;
    public bool Valid = true;
    public bool Owner = true;
    private readonly ZDO data = new ZDO();
    public bool IsValid() => Valid;
    public bool IsOwner() => Owner;
    public ZDO GetZDO() => data;
}
public sealed class ZDO
{
    public ZDOID m_uid;
    public int Writes;
    public System.Action<int> AfterRead;
    public uint DataRevision => (uint)Writes;
    private readonly Dictionary<int, string> strings = new Dictionary<int, string>();
    private readonly Dictionary<int, int> integers = new Dictionary<int, int>();
    private readonly Dictionary<int, byte[]> bytes = new Dictionary<int, byte[]>();
    public string GetString(int key, string fallback) => strings.TryGetValue(key, out string value) ? value : fallback;
    public void Set(int key, string value) { ++Writes; strings[key] = value; }
    public int GetInt(int key, int fallback)
    {
        int result = integers.TryGetValue(key, out int value) ? value : fallback;
        AfterRead?.Invoke(key); return result;
    }
    public bool GetBool(int key, bool fallback) => integers.TryGetValue(key, out int value) ? value != 0 : fallback;
    public byte[] GetByteArray(int key, byte[] fallback)
    {
        byte[] result = bytes.TryGetValue(key, out byte[] value) ? value : fallback;
        AfterRead?.Invoke(key); return result;
    }
    public void Set(int key, int value) { ++Writes; integers[key] = value; }
    public void Set(int key, byte[] value) { ++Writes; bytes[key] = value; }
}
// Inert identity comparison only; no native user mapping, allocation or persistence.
public struct ZDOID
{
    public long UserID => UserKey;
    public ushort UserKey;
    public uint ID;
    public bool IsNone() => UserKey == 0 && ID == 0;
    public static bool operator ==(ZDOID left, ZDOID right) => left.UserKey == right.UserKey && left.ID == right.ID;
    public static bool operator !=(ZDOID left, ZDOID right) => !(left == right);
    public override bool Equals(object other) => other is ZDOID value && this == value;
    public override int GetHashCode() => UserKey.GetHashCode() ^ ID.GetHashCode();
}
public static class ZDOVars
{
    public const int s_text = 1, s_item = 2, s_variant = 3, s_quality = 4, s_type = 5, s_itemData = 6, s_items = 7, s_addedDefaultItems = 8;
}
public sealed class Sign : MonoBehaviour
{
    public TMPro.TextMeshProUGUI m_textWidget;
    public string m_defaultText = "Default";
    public int m_characterLimit = 50;
    private bool m_isViewable = true;
    public bool Denied, Throw;
    public int Calls;
    public void SetText(string text)
    {
        ++Calls;
        if (Throw) throw new System.InvalidOperationException("Test sign failure");
        if (!Denied) GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_text, text);
    }
}
public static class CensorShittyWords
{
    // Contract stub only. The installed native filter is not executed in this laboratory.
    public static void Filter(string input, out string output) => output = input?.Replace("QA_FILTER_WORD", "***");
}
namespace Splatform
{
    public enum Privilege { ViewUserGeneratedContent = 2 }
    public enum PrivilegeResult { Denied, Granted }
    public static class PrivilegeResultExtentions { public static bool IsGranted(this PrivilegeResult value) => value == PrivilegeResult.Granted; }
    public sealed class PrivilegeProvider
    {
        public bool Granted = true;
        public PrivilegeResult CheckPrivilege(Privilege privilege) => Granted ? PrivilegeResult.Granted : PrivilegeResult.Denied;
    }
    public sealed class DistributionPlatform { public readonly PrivilegeProvider PrivilegeProvider = new PrivilegeProvider(); }
    public static class PlatformManager { public static readonly DistributionPlatform DistributionPlatform = new DistributionPlatform(); }
}
