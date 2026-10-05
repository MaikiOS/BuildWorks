using System;
using System.Reflection;
using OstrixMods.BuildWorks.Geometry;
using Splatform;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintSignText
    {
        private static readonly FieldInfo Viewable = typeof(Sign).GetField("m_isViewable", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool CanApply(Piece prefab, string text)
        {
            if (text == null) return true;
            Sign sign = prefab ? prefab.GetComponent<Sign>() : null;
            return sign && prefab.GetComponent<ZNetView>() && BlueprintEditorPart.IsValidSignText(text) &&
                text.Length <= sign.m_characterLimit;
        }

        internal static bool TryCapture(Piece piece, out string text)
        {
            text = null;
            Sign sign = piece ? piece.GetComponent<Sign>() : null;
            if (!sign) return true;
            ZNetView view = piece.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || Viewable?.GetValue(sign) is not true) return false;
            text = view.GetZDO().GetString(ZDOVars.s_text, sign.m_defaultText);
            return BlueprintEditorPart.IsValidSignText(text);
        }

        internal static bool TryApply(GameObject placed, string text)
        {
            Sign sign = placed ? placed.GetComponent<Sign>() : null;
            ZNetView view = placed ? placed.GetComponent<ZNetView>() : null;
            if (!sign || !view || !view.IsValid() || !BlueprintEditorPart.IsValidSignText(text) ||
                text == null || text.Length > sign.m_characterLimit ||
                !PlatformManager.DistributionPlatform.PrivilegeProvider.CheckPrivilege(Privilege.ViewUserGeneratedContent).IsGranted())
                return false;
            sign.SetText(text);
            // Native SetText may refuse ward access without throwing. Check stored text, not the censored widget.
            return view.GetZDO().GetString(ZDOVars.s_text, sign.m_defaultText) == text;
        }
    }
}
