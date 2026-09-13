using System;
using System.Collections.Generic;

namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class HarmonyPatch : Attribute
    {
        internal HarmonyPatch(Type type, string methodName)
        {
        }
    }
}

internal sealed class Localization
{
    private readonly Dictionary<string, string> words =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private string language;

    internal Localization(string selectedLanguage)
    {
        language = selectedLanguage;
    }

    internal static Localization instance { get; set; }

    internal string GetSelectedLanguage() => language;

    internal void SetupLanguage(string selectedLanguage)
    {
        language = selectedLanguage;
    }

    private void AddWord(string key, string text)
    {
        words[key] = text;
    }

    internal string Localize(string token)
    {
        string key = token != null && token.StartsWith("$", StringComparison.Ordinal)
            ? token.Substring(1)
            : token;
        int dot = key == null ? -1 : key.IndexOf('.');
        if (dot >= 0) key = key.Substring(0, dot);
        return key != null && words.TryGetValue(key, out string text)
            ? text
            : "[" + key + "]";
    }
}

namespace OstrixMods.BuildWorks
{
    internal sealed class CompositeBlueprintStore
    {
        internal const string DefaultCategory = "OTHER";
    }
}
