// External plugin identity/settings only; runtime MonoBehaviour sources also need the identity.
namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksPlugin
    {
        internal const string PluginGuid = "ostrmod.buildworks";
        internal sealed class DockSetting { public int Value; }
        internal static readonly DockSetting EditorHudDock = new DockSetting();
        internal static readonly DockSetting InterfaceScalePercent = new DockSetting { Value = 80 };
    }
}
