using System;
using System.IO;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ImportErrorChecks
    {
        internal static void Run()
        {
            foreach (string malformed in new[] { "{broken", "[]", "null", "{\"format\":false}", "{} trailing" })
            {
                try { BlueprintExchangeFormat.Decode(malformed); }
                catch (InvalidDataException error)
                {
                    if (error.Message != "$buildworks_import.document_invalid" && error.Message != "$buildworks_store.unknown_version")
                        throw new Exception("Malformed document has no meaningful file-level error: " + error.Message);
                    continue;
                }
                throw new Exception("Malformed document was accepted.");
            }
            Console.WriteLine("PASS malformed document errors: file-level reason without a fictional row or unresolved placeholder.");
        }
    }
}
