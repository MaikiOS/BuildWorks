using System;
using System.Text;
using OstrixMods.BuildWorks;

internal static class Program
{
    private static int checks;
    private static readonly string nonce = "43d3719a7ee649d184e26ef1daa441a3";
    private static string Header => "{\"version\":1,\"nonce\":\"" + nonce + "\",\"id\":2,\"expiresUtc\":2000,";
    private static void Reject(string json, string expectedNonce = null, long previous = 1, long now = 1000)
    {
        try { Read(json, expectedNonce, previous, now); }
        catch (Exception error) when (error is System.IO.InvalidDataException || error is System.Runtime.Serialization.SerializationException)
        { ++checks; return; }
        throw new Exception("Invalid command was accepted: " + json.Substring(0, Math.Min(json.Length, 180)));
    }
    private static BuildWorksQaControlProtocol.Request Read(string json, string expectedNonce = null, long previous = 1, long now = 1000)
        => BuildWorksQaControlProtocol.Read(Encoding.UTF8.GetBytes(json), expectedNonce ?? nonce, previous, now);
    private static void Main()
    {
        string state = Header + "\"action\":\"state\"}";
        if (Read(state).id != 2) throw new Exception("State command changed identity."); ++checks;
        string batch = Header + "\"action\":\"batch\",\"capture\":true,\"steps\":[{\"milliseconds\":2000,\"keys\":[\"W\",\"LeftShift\"],\"buttons\":1,\"pointer\":true,\"x\":0.4,\"y\":0.6}]}";
        if (!Read(batch).capture || Read(batch).steps[0].keys[1] != "LeftShift") throw new Exception("Input batch corrupted."); ++checks;
        string guarded = batch.Replace("\"action\":", "\"stopRevision\":7,\"action\":");
        if (BuildWorksQaControlProtocol.Read(Encoding.UTF8.GetBytes(guarded), nonce, 1, 1000, 7).stopRevision != 7)
            throw new Exception("Stop revision was not retained."); ++checks;
        Reject(guarded); // Model decision predates an emergency stop, including an idle F12.
        Reject(batch.Replace("\"action\":", "\"stopRevision\":-1,\"action\":"));
        string ui = Header + "\"action\":\"batch\",\"stopRevision\":0,\"steps\":[{\"milliseconds\":100,\"uiTarget\":\"0123456789abcdef0123456789abcdef\",\"pointer\":true,\"buttons\":1},{\"milliseconds\":300}]}";
        if (Read(ui).steps[0].uiTarget.Length != 32) throw new Exception("UI target lost."); ++checks;
        string secondId = "1123456789abcdef0123456789abcdef";
        string pair = ui.Substring(0, ui.Length - 2) + ",{\"milliseconds\":100,\"uiTarget\":\"" + secondId + "\",\"pointer\":true,\"buttons\":1},{\"milliseconds\":400}]}";
        if (Read(pair).steps.Length != 4 || Read(pair).steps[2].uiTarget != secondId || !Read(pair).InventoryPair || Read(pair).FocusThenText)
            throw new Exception("Guarded inventory pair lost its second target."); ++checks;
        Reject(pair.Replace("\"stopRevision\":0,", ""));
        Reject(pair.Replace(secondId, "bad"));
        Reject(pair.Replace("\"uiTarget\":\"" + secondId + "\",", ""));
        Reject(pair.Replace("{\"milliseconds\":300}", "{\"milliseconds\":300,\"buttons\":1}"));
        Reject(pair.Replace("{\"milliseconds\":400}", "{\"milliseconds\":400,\"pointer\":true}"));
        Reject(pair.Replace("\"pointer\":true,\"buttons\":1", "\"text\":\"not a click\""));
        Reject(pair.Replace("{\"milliseconds\":400}", "{\"milliseconds\":400,\"keys\":[\"Return\"]}"));
        string text = ui.Replace("\"pointer\":true,\"buttons\":1", "\"text\":\"QA copy\",\"keys\":[\"Return\"]");
        if (Read(text).steps[0].text != "QA copy") throw new Exception("Guarded text lost."); ++checks;
        string focusText = pair.Replace(secondId, "0123456789abcdef0123456789abcdef");
        int secondAction = focusText.LastIndexOf("\"pointer\":true,\"buttons\":1", StringComparison.Ordinal);
        focusText = focusText.Remove(secondAction, "\"pointer\":true,\"buttons\":1".Length).Insert(secondAction, "\"text\":\"QA copy\",\"keys\":[\"Return\"]");
        if (Read(focusText).steps[2].text != "QA copy" || !Read(focusText).FocusThenText || Read(focusText).InventoryPair)
            throw new Exception("Same-field focus/text sequence lost."); ++checks;
        if (Read(focusText.Replace("QA copy", "")).steps[2].text != "") throw new Exception("Clearing a focused field was rejected."); ++checks;
        Reject(focusText.Replace("\"stopRevision\":0,", ""));
        Reject(focusText.Replace("QA copy", new string('a', 257)));
        Reject(focusText.Replace("\"text\":\"QA copy\"", "\"pointer\":true,\"text\":\"QA copy\""));
        Reject(focusText.Replace("\"text\":\"QA copy\"", "\"buttons\":1,\"text\":\"QA copy\""));
        Reject(focusText.Replace("\"text\":\"QA copy\"", "\"lookX\":1,\"text\":\"QA copy\""));
        Reject(focusText.Replace("\"text\":\"QA copy\"", "\"scroll\":1,\"text\":\"QA copy\""));
        Reject(focusText.Replace("Return", "E"));
        Reject(focusText.Replace("{\"milliseconds\":300}", "{\"milliseconds\":300,\"text\":\"extra\"}"));
        Reject(focusText.Replace("{\"milliseconds\":400}", "{\"milliseconds\":400,\"keys\":[\"Return\"]}"));
        Reject(focusText.Replace("\"uiTarget\":\"0123456789abcdef0123456789abcdef\",\"text\"", "\"uiTarget\":\"" + secondId + "\",\"text\""));
        Reject(focusText.Replace("\"pointer\":true,\"buttons\":1", "\"text\":\"not a click\""));
        if (Read(focusText.Replace(",\"keys\":[\"Return\"]", "")).steps[2].keys != null)
            throw new Exception("Focus/text without submit changed keys."); ++checks;
        Reject(ui.Replace("\"stopRevision\":0,", ""));
        Reject(ui.Replace("0123456789abcdef0123456789abcdef", "bad"));
        Reject(ui.Replace("0123456789abcdef0123456789abcdef", "g123456789abcdef0123456789abcdef"));
        Reject(ui.Replace("\"buttons\":1", "\"buttons\":2"));
        Reject(ui.Replace("\"buttons\":1", "\"buttons\":1,\"keys\":[\"E\"]"));
        Reject(ui.Replace("\"buttons\":1", "\"buttons\":1,\"lookX\":1"));
        Reject(ui.Replace("{\"milliseconds\":300}", "{\"milliseconds\":300,\"buttons\":1}"));
        Reject(ui.Replace("{\"milliseconds\":300}", "{\"milliseconds\":300,\"text\":\"extra\"}"));
        Reject(ui.Replace("{\"milliseconds\":300}", "{\"milliseconds\":300,\"uiTarget\":\"0123456789abcdef0123456789abcdef\"}"));
        Reject(text.Replace("Return", "E"));
        var timed = Read(batch);
        if (!BuildWorksQaControlProtocol.CanContinue(timed, 1500, 500)) throw new Exception("Valid held input stopped early."); ++checks;
        foreach (var clock in new[] { Tuple.Create(2001L, 0d), Tuple.Create(2001L, 500d), Tuple.Create(2001L, 1000d), Tuple.Create(1500L, 10000d), Tuple.Create(1500L, -1d) })
        {
            if (BuildWorksQaControlProtocol.CanContinue(timed, clock.Item1, clock.Item2)) throw new Exception("Expired/overlong input continued.");
            ++checks;
        }
        Reject(state, "another-session"); Reject(state, previous: 2); Reject(state, now: 2001); Reject(state, now: -30000);
        Reject(state.Replace("\"version\":1", "\"version\":2")); Reject(state.Replace("\"id\":2", "\"id\":0"));
        Reject(state.Replace("state", "execute")); Reject("{}"); Reject("{"); Reject(new string('x', 32769));
        Reject(batch.Replace("2000,\"keys", "2001,\"keys")); Reject(batch.Replace("2000,\"keys", "19,\"keys"));
        Reject(batch.Replace("\"buttons\":1", "\"buttons\":8")); Reject(batch.Replace("\"x\":0.4", "\"x\":1.1"));
        Reject(batch.Replace("\"W\"", "\"../../world\"")); Reject(batch.Replace("\"keys\"", "\"lookX\":2001,\"keys\""));
        Reject(Header + "\"action\":\"batch\",\"steps\":[]}");
        Reject(Header + "\"action\":\"state\",\"steps\":[{\"milliseconds\":20}]}");
        string[] steps = new string[33]; for (int i = 0; i < steps.Length; ++i) steps[i] = "{\"milliseconds\":20}";
        Reject(Header + "\"action\":\"batch\",\"steps\":[" + string.Join(",", steps) + "]}");
        for (int i = 0; i < steps.Length; ++i) steps[i] = "{\"milliseconds\":2000}";
        Reject(Header + "\"action\":\"batch\",\"steps\":[" + string.Join(",", steps, 0, 6) + "]}");
        Console.WriteLine("PASS QA control protocol: " + checks + " assertions (state/batch, identity/replay/expiry, malformed/size/action/step/key/path/duration bounds). Not a game-input execution test.");
        FilePublisherChecks.Run();
        UiTargetsChecks.Run();
    }
}
