using System;
using System.IO;
using OstrixMods.BuildWorks;

internal static class FilePublisherChecks
{
    internal static void Run()
    {
        string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "publisher-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.json"), target = Path.Combine(directory, "reply.json");
        File.WriteAllText(source, "new"); File.WriteAllText(target, "old");
        int validations = 0;
        int retries = BuildWorksQaFilePublisher.ReplaceObservation(source, target, () => validations++);
        if (retries != 0 || validations != 1 || File.Exists(source) || File.ReadAllText(target) != "new")
            throw new Exception("Ordinary observation publication changed bytes or retry count.");
        File.WriteAllText(source, "next");
        validations = 0;
        var nativeReplace = BuildWorksQaFilePublisher.ReplaceForTest;
        BuildWorksQaFilePublisher.ReplaceForTest = (s, t) => { throw new IOException("Windows1175", unchecked((int)0x80070497)); };
        try
        {
            try { BuildWorksQaFilePublisher.ReplaceObservation(source, target, () => validations++); throw new Exception("Locked replacement accepted."); }
            catch (IOException error) when ((error.HResult & 65535) == 1175)
            { if (validations != 5) throw new Exception("Windows1175 retry bound or path revalidation changed."); }
        }
        finally { BuildWorksQaFilePublisher.ReplaceForTest = nativeReplace; }
        if (File.ReadAllText(target) != "new" || File.ReadAllText(source) != "next")
            throw new Exception("Windows1175 did not preserve both source names/bytes.");
        if (BuildWorksQaFilePublisher.ReplaceObservation(source, target, () => { }) != 0 || File.ReadAllText(target) != "next")
            throw new Exception("Publication did not recover after releasing the lock.");
        File.WriteAllText(source, "untouched"); File.Delete(target); validations = 0;
        try { BuildWorksQaFilePublisher.ReplaceObservation(source, target, () => validations++); throw new Exception("Missing target accepted."); }
        catch (FileNotFoundException) { if (validations != 1) throw new Exception("Non1175 failure was retried."); }
        validations = 0;
        try { BuildWorksQaFilePublisher.ReplaceObservation(source, target, () => { validations++; throw new UnauthorizedAccessException(); }); }
        catch (UnauthorizedAccessException) { }
        if (validations != 1 || File.ReadAllText(source) != "untouched") throw new Exception("Validation failure retried or mutated source.");
        foreach (int code in new[] { 32, 1176, 1177 })
        {
            validations = 0;
            BuildWorksQaFilePublisher.ReplaceForTest = (s, t) => { throw new IOException("Other Windows failure", unchecked((int)0x80070000) | code); };
            try { BuildWorksQaFilePublisher.ReplaceObservation(source, target, () => validations++); throw new Exception("Other Windows failure accepted."); }
            catch (IOException error) when ((error.HResult & 65535) == code)
            { if (validations != 1) throw new Exception("Unsafe Windows failure was retried."); }
            finally { BuildWorksQaFilePublisher.ReplaceForTest = nativeReplace; }
        }
        Console.WriteLine("PASS QA observation publisher: exact bytes, Windows1175 bounded retry/path revalidation/preserved files, released lock recovery, non1175 and validation failure not retried. No game input.");
    }
}
