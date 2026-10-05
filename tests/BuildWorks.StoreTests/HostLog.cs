global using Debug = OstrixMods.BuildWorks.StoreTests.HostLog;

namespace OstrixMods.BuildWorks.StoreTests
{
    // Only logging is substituted: a standalone parser check is not a running Unity host.
    internal static class HostLog
    {
        public static void LogWarning(object message) => System.Console.WriteLine("HOST WARNING: " + message);
    }
}
