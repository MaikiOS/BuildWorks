// Isolated sign host contracts, not a substitute for Valheim permission/network acceptance.
using System.Collections.Generic;
using UnityEngine;

public sealed class ZNetView : MonoBehaviour
{
    public bool Valid = true;
    private readonly ZDO data = new ZDO();
    public bool IsValid() => Valid;
    public ZDO GetZDO() => data;
}
public sealed class ZDO
{
    private readonly Dictionary<int, string> strings = new Dictionary<int, string>();
    public string GetString(int key, string fallback) => strings.TryGetValue(key, out string value) ? value : fallback;
    public void Set(int key, string value) => strings[key] = value;
}
public static class ZDOVars { public const int s_text = 1; }
public sealed class Sign : MonoBehaviour
{
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
