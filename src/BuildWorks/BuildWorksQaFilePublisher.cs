#if BUILDWORKS_TEST_STAND || BUILDWORKS_QA_CONTROL_TESTS
using System;
using System.IO;
using System.Threading;

namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksQaFilePublisher
    {
#if BUILDWORKS_QA_CONTROL_TESTS
        internal static Action<string, string> ReplaceForTest = (source, target) => File.Replace(source, target, null);
#endif
        internal static int ReplaceObservation(string temporary, string path, Action validatePaths)
        {
            for (int attempt = 0; ; attempt++)
            {
                validatePaths();
                try
                {
#if BUILDWORKS_QA_CONTROL_TESTS
                    ReplaceForTest(temporary, path);
#else
                    File.Replace(temporary, path, null);
#endif
                    return attempt;
                }
                // ReplaceFile error1175 preserves both names;1176/1177 do not have that guarantee.
                catch (IOException error) when ((error.HResult & 65535) == 1175 && attempt < 4)
                { Thread.Sleep(10); }
            }
        }
    }
}
#endif
