using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace OstrixMods.BuildWorks
{
    /// <summary>
    /// Owns BuildWorks text. Runtime code refers to stable keys; English is the
    /// fallback and Valheim's selected language supplies an optional overlay.
    /// </summary>
    internal static class BuildWorksLocalization
    {
        private const string TokenPrefix = "buildworks_";
        private const string EnglishResource = "Translations.English.tsv";
        private const string RussianResource = "Translations.Russian.tsv";

        private static readonly Dictionary<string, string> English =
            LoadCatalog(EnglishResource);
        private static readonly Dictionary<string, string> Russian =
            LoadCatalog(RussianResource);
        internal static string Token(string key) => "$" + RuntimeKey(key);

        private static string RuntimeKey(string key) =>
            TokenPrefix + key.Replace('.', '_');

        internal static string CatalogLabel(string id)
        {
            string key = "catalog.group." + id;
            return English.ContainsKey(key) ? Text(key) : id;
        }

        internal static string BlueprintCategoryLabel(string category)
        {
            if (string.Equals(category, CompositeBlueprintStore.DefaultCategory,
                StringComparison.Ordinal))
                return Text("blueprint.category.default");
            return category;
        }

        internal static string Text(string key, params object[] arguments)
        {
            string fallback = English.TryGetValue(key, out string value) ? value : key;
            string token = Token(key);
            string localized = TryLocalize(token, fallback);
            return arguments == null || arguments.Length == 0
                ? localized
                : string.Format(CultureInfo.CurrentCulture, localized, arguments);
        }

        private static string TryLocalize(string token, string fallback)
        {
            try
            {
                Localization localization = Localization.instance;
                if (localization == null) return fallback;
                string localized = localization.Localize(token);
                string missing = "[" + token.Substring(1) + "]";
                return string.Equals(localized, token, StringComparison.Ordinal) ||
                    string.Equals(localized, missing, StringComparison.Ordinal)
                    ? fallback
                    : localized;
            }
            catch (Exception)
            {
                // The English fallback must also work in isolated tests and
                // during host startup before Unity's Localization is ready.
                return fallback;
            }
        }

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

        internal static void RegisterCurrent()
        {
            Localization localization = Localization.instance;
            if (localization != null)
                Register(localization, localization.GetSelectedLanguage());
        }

        internal static void Register(Localization localization, string language)
        {
            if (localization == null) return;
            MethodInfo addWordMethod = typeof(Localization).GetMethod(
                "AddWord",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(string) },
                null);
            if (addWordMethod == null)
                throw new MissingMethodException("Valheim Localization.AddWord(string, string)");
            Dictionary<string, string> selected =
                string.Equals(language, "Russian", StringComparison.OrdinalIgnoreCase)
                    ? Russian
                    : English;
            foreach (KeyValuePair<string, string> entry in English)
            {
                string text = selected.TryGetValue(entry.Key, out string translated)
                    ? translated
                    : entry.Value;
                addWordMethod.Invoke(localization, new object[] { RuntimeKey(entry.Key), text });
            }
        }

        internal static IReadOnlyCollection<string> MissingRussianKeys() =>
            English.Keys.Where(key => !Russian.ContainsKey(key)).ToArray();

        private static Dictionary<string, string> LoadCatalog(string suffix)
        {
            Assembly assembly = typeof(BuildWorksLocalization).Assembly;
            string name = assembly.GetManifestResourceNames().FirstOrDefault(candidate =>
                candidate.EndsWith(suffix, StringComparison.Ordinal));
            if (name == null)
                throw new InvalidOperationException("Missing embedded localization catalog: " + suffix);

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            using (Stream stream = assembly.GetManifestResourceStream(name))
            using (var reader = new StreamReader(stream ?? throw new InvalidOperationException(
                "Cannot open embedded localization catalog: " + suffix)))
            {
                string line;
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    if (line.Length == 0 || line[0] == '#') continue;
                    int separator = line.IndexOf('\t');
                    if (separator <= 0)
                        throw new InvalidOperationException(
                            suffix + ": invalid entry at line " + lineNumber);
                    string key = line.Substring(0, separator);
                    string text = Unescape(line.Substring(separator + 1));
                    if (result.ContainsKey(key))
                        throw new InvalidOperationException(
                            suffix + ": duplicate key '" + key + "'");
                    result.Add(key, text);
                }
            }
            return result;
        }

        private static string Unescape(string text) => text
            .Replace("\\n", "\n")
            .Replace("\\t", "\t")
            .Replace("\\\\", "\\");

        [HarmonyPatch(typeof(Localization), nameof(Localization.SetupLanguage))]
        private static class LanguageSetupPatch
        {
            private static void Postfix(Localization __instance, string language) =>
                Register(__instance, language);
        }
    }
}
