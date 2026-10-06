using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    /// <summary>
    /// Catalogue test adapter for the isolated Unity Workbench. Production
    /// uses BuildWorksLocalization.cs and Valheim's selected language.
    /// </summary>
    internal static class BuildWorksLocalization
    {
        private const string TokenPrefix = "buildworks_";
        private static readonly Dictionary<string, string> English = LoadCatalog("English");
        private static readonly Dictionary<string, string> Russian = LoadCatalog("Russian");
        internal static bool RussianLanguage;

        internal static string Token(string key) =>
            "$" + TokenPrefix + key.Replace('.', '_');

        internal static string Text(string key, params object[] arguments)
        {
            string text = (RussianLanguage ? Russian : English).TryGetValue(key, out string value) ? value : key;
            return arguments == null || arguments.Length == 0
                ? text
                : string.Format(CultureInfo.InvariantCulture, text, arguments);
        }

        internal static string CatalogLabel(string id)
        {
            string key = "catalog.group." + (id ?? string.Empty).ToLowerInvariant();
            return English.ContainsKey(key) ? Text(key) : id;
        }

        internal static string BlueprintCategoryLabel(string category) =>
            string.Equals(category, CompositeBlueprintStore.DefaultCategory,
                StringComparison.Ordinal)
                ? Text("blueprint.category.default")
                : category;

        internal static string ResolveUserText(string text)
        {
            const string marker = "$" + TokenPrefix;
            if (string.IsNullOrEmpty(text) || !text.StartsWith(marker, StringComparison.Ordinal))
                return text;
            string payload = text.Substring(marker.Length);
            int separator = payload.IndexOf('\t');
            return separator < 0
                ? Text(payload)
                : Text(payload.Substring(0, separator), payload.Substring(separator + 1));
        }

        private static Dictionary<string, string> LoadCatalog(string language)
        {
            string path = Path.Combine(
                Application.dataPath, "Editor", "RuntimeSources", language + ".tsv");
            var catalog = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(path))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                int separator = line.IndexOf('\t');
                if (separator <= 0) continue;
                catalog.Add(
                    line.Substring(0, separator),
                    line.Substring(separator + 1)
                        .Replace("\\n", "\n")
                        .Replace("\\t", "\t")
                        .Replace("\\\\", "\\"));
            }
            return catalog;
        }
    }
}
