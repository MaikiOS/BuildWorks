using System;

namespace OstrixMods.BuildWorks
{
    internal static class Program
    {
        private static int Main()
        {
            try
            {
                Equal(
                    "$buildworks_editor_view_title",
                    BuildWorksLocalization.Token("editor.view.title"),
                    "runtime token");

                Localization.instance = new Localization("English");
                BuildWorksLocalization.RegisterCurrent();
                Equal(
                    "BUILDWORKS · BLUEPRINT",
                    BuildWorksLocalization.Text("editor.view.title"),
                    "English registration");
                Equal(
                    "The name must contain 1 to 48 characters.",
                    BuildWorksLocalization.ResolveUserText(
                        "$buildworks_store.invalid_name"),
                    "store error resolution");

                BuildWorksLocalization.Register(Localization.instance, "Russian");
                Equal(
                    "BUILDWORKS · ЧЕРТЁЖ",
                    BuildWorksLocalization.Text("editor.view.title"),
                    "Russian overlay");

                Equal(
                    "missing.key",
                    BuildWorksLocalization.Text("missing.key"),
                    "missing-key English fallback");

                Console.WriteLine(
                    "PASS: BuildWorks localization registers Valheim-safe English and Russian runtime tokens.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
        }

        private static void Equal(string expected, string actual, string label)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    label + ": expected '" + expected + "', got '" + actual + "'.");
        }
    }
}
