#if BUILDWORKS_TEST_STAND
using System;
using System.Collections;
using System.IO;
using System.Runtime.Serialization.Json;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    [DefaultExecutionOrder(-20000)]
    internal sealed class BuildWorksQaControl : MonoBehaviour
    {
        [Serializable] private sealed class Session
        { public int version = 1; public string nonce; public int processId; public string versionBuild; public string emergency = "F12 / stop.flag"; }
        [Serializable] private sealed class Reply
        {
            public string nonce, status, detail, screenshot, world, selectedUi, uiModule;
            public string snap, placementContact, placementStatus, inventory;
            public long id, utc, stopRevision;
            public int frame, width, height;
            public bool focused, ready, inventoryVisible;
            public Vector3 position, cameraPosition, cameraEuler;
            public BuildWorksQaUiTargets.Entry[] uiTargets;
            public bool uiTargetsTruncated;
        }

        private readonly BuildWorksQaInput input = new BuildWorksQaInput();
        private readonly BuildWorksQaUiTargets uiTargets = new BuildWorksQaUiTargets();
        private BuildWorksQaUiTargets.Target uiTarget;
        private bool uiReleased;
        private string directory, nonce;
        private long lastId, stopRevision;
        private float nextPoll, started, stepStarted;
        private int stepIndex, stepFrame;
        private bool stepFinished;
        private Vector2 pointerStart;
        private BuildWorksQaControlProtocol.Request running;
        private Player player;
        private ZNet network;
        private Coroutine capture;
        private long captureId;
        private int releaseFrame = -1;

        public string StartStoragePersistenceTest()
        {
            if (running != null || capture != null) throw new InvalidOperationException("Finish the input request first.");
            player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance;
            if (!SafeSession()) throw new InvalidOperationException("Focused isolated QA session required.");
            var storage = GetComponent<BuildWorksTestStorage>() ?? gameObject.AddComponent<BuildWorksTestStorage>();
            return storage.Begin(reload: false);
        }

        public string CheckStoragePersistenceReload()
        {
            if (running != null || capture != null) throw new InvalidOperationException("Finish the input request first.");
            player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance;
            if (!SafeSession()) throw new InvalidOperationException("Focused isolated QA session required.");
            var storage = GetComponent<BuildWorksTestStorage>() ?? gameObject.AddComponent<BuildWorksTestStorage>();
            return storage.Begin(reload: true);
        }

        public string StoragePersistenceStatus() => GetComponent<BuildWorksTestStorage>()?.Status ?? "idle";
        public bool StoragePersistenceBusy() => GetComponent<BuildWorksTestStorage>()?.Busy ?? false;

        public string CheckFogGhostAssets()
        {
            player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance;
            if (running != null || capture != null || !SafeSession()) throw new InvalidOperationException("Focused idle isolated QA session required.");
            for (int load = 0; load < 2; ++load)
                using (var assets = BlueprintFogAssets.LoadEmbedded())
                using (var away = new ComputeBuffer(1024, sizeof(uint)))
                using (var peak = new ComputeBuffer(1024, sizeof(uint)))
                using (var surface = new ComputeBuffer(1, sizeof(uint)))
                {
                    uint[] values = new uint[1024];
                    for (int i = 0; i < values.Length; ++i) values[i] = 123;
                    away.SetData(values); peak.SetData(values); surface.SetData(new[] { 123u });
                    int kernel = assets.Solver.FindKernel("ClearLedger");
                    assets.Solver.SetBuffer(kernel, "_AwayMass", away); assets.Solver.SetBuffer(kernel, "_PeakAway", peak);
                    assets.Solver.SetBuffer(kernel, "_SurfaceState", surface); assets.Solver.SetInt("_ResetLedger", 1);
                    assets.Solver.Dispatch(kernel, 16, 1, 1);
                    away.GetData(values); foreach (uint value in values) if (value != 0) throw new InvalidDataException("Fog away ledger differs.");
                    peak.GetData(values); foreach (uint value in values) if (value != 0) throw new InvalidDataException("Fog peak ledger differs.");
                    surface.GetData(values, 0, 0, 1); if (values[0] != 0) throw new InvalidDataException("Fog surface ledger differs.");
                }
            return "fog-v11-3-assets-7-kernels-gpu-ledger-and-reload-verified";
        }

        public string CheckLoadedStorageInventory(string info, string bytes)
        {
            player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance;
            if (running != null || capture != null || !SafeSession()) throw new InvalidOperationException("Focused idle isolated QA session required.");
            var expected = new Geometry.BlueprintForeignPartData(Geometry.BlueprintForeignPartData.NativeContainerFormat, info, bytes, null);
            var profile = Game.instance.GetPlayerProfile();
            if (profile.m_fileSource != FileHelpers.FileSource.Local || !BlueprintStorageLoadedInventory.Matches(player.GetInventory(), expected))
                throw new InvalidOperationException("Native loaded inventory projection differs.");
            using (var file = new FileStream(profile.GetPath(), FileMode.Open, FileAccess.Read, FileShare.Read))
                if (!Geometry.NativePlayerProfileReadback.TryVerify(file, player.GetPlayerID(), profile.GetName(), expected,
                    name => name.GetStableHashCode(), out _, out string reason) ||
                    !SafeSession() || !BlueprintStorageLoadedInventory.Matches(player.GetInventory(), expected))
                    throw new InvalidOperationException(reason ?? "Native canonical profile or context differs.");
            return "native-loaded-inventory-projection-verified-read-only";
        }

        internal void Initialize()
        {
            directory = BuildWorksTestIsolation.ControlDirectory;
            BuildWorksTestIsolation.ValidateSavePath(Path.Combine(directory, "session.json"));
            Directory.CreateDirectory(directory);
            ResetSession();
            InputSystem.onAfterUpdate += AfterInput;
        }

        internal void ResetSession()
        {
            Stop("QA session reset.");
            BuildWorksTestPlacementDiagnostics.Reset();
            nonce = Guid.NewGuid().ToString("N"); lastId = 0; player = null; network = null;
            Publish("session.json", System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Session {
                nonce = nonce, processId = System.Diagnostics.Process.GetCurrentProcess().Id, versionBuild = BuildWorksPlugin.PluginVersion })));
            Respond(0, "idle", "Load the isolated QA world before batch input.");
        }

        private string PathFor(string name)
        {
            string path = Path.Combine(directory, name);
            BuildWorksTestIsolation.ValidateSavePath(path);
            return path;
        }

        private void Publish(string name, byte[] bytes)
        {
            string path = PathFor(name), temporary = PathFor(Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                PathFor(name);
                if (File.Exists(path))
                {
                    int retries = BuildWorksQaFilePublisher.ReplaceObservation(temporary, path,
                        () => { PathFor(name); PathFor(Path.GetFileName(temporary)); });
                    if (retries > 0) Debug.LogWarning("BuildWorks QA observation publication recovered Windows1175 after " + retries + " retries: " + name);
                }
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) { BuildWorksTestIsolation.ValidateSavePath(temporary); File.Delete(temporary); } }
        }

        private void Update()
        {
            if (directory == null) return;
            try
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.F12) || File.Exists(PathFor("stop.flag")))
                {
                    Stop("Emergency stop.");
                    if (File.Exists(PathFor("stop.flag"))) File.Delete(PathFor("stop.flag"));
                }
                if (running != null && (!SafeSession() || !input.UiModuleMatches || !WithinDeadline())) Stop("Focus/session/UI module changed or batch timed out.");
                if (Time.realtimeSinceStartup < nextPoll) return;
                nextPoll = Time.realtimeSinceStartup + 0.05f;
                string path = PathFor("request.json");
                if (!File.Exists(path)) return;
                if (running != null || capture != null) return;
                if (new FileInfo(path).Length > BuildWorksQaControlProtocol.MaximumBytes)
                { File.Delete(path); Respond(0, "rejected", "Request exceeds 32768 bytes."); return; }
                byte[] bytes;
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var buffer = new MemoryStream())
                {
                    byte[] block = new byte[4096]; int count;
                    while ((count = stream.Read(block, 0, Math.Min(block.Length, BuildWorksQaControlProtocol.MaximumBytes + 1 - (int)buffer.Length))) > 0)
                        buffer.Write(block, 0, count);
                    bytes = buffer.ToArray();
                }
                File.Delete(path); // Consumed before any input; never replay after an uncertain result.
                var request = BuildWorksQaControlProtocol.Read(bytes, nonce, lastId, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), stopRevision);
                lastId = request.id;
                foreach (var step in request.steps ?? Array.Empty<BuildWorksQaControlProtocol.Step>())
                    foreach (string key in step.keys ?? Array.Empty<string>()) BuildWorksQaInput.ParseKey(key);
                if (request.action == "stop") { Stop("Requested stop."); Respond(request.id, "completed", "Input released."); return; }
                if (request.action == "state")
                {
                    try { player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance; }
                    catch (Exception) { player = null; network = null; }
                    Finish(request, "completed", "Observed state only."); return;
                }
                player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance;
                if (!SafeSession()) throw new InvalidOperationException("Game must be focused in the isolated living QA session.");
                uiTarget = request.steps[0].uiTarget == null ? null : uiTargets.Resolve(request.steps[0].uiTarget);
                uiReleased = false;
                if (uiTarget != null && request.steps[0].pointer)
                { request.steps[0].x = uiTarget.entry.x; request.steps[0].y = uiTarget.entry.y; }
                running = request; started = stepStarted = Time.realtimeSinceStartup; stepIndex = 0; stepFrame = releaseFrame = -1; stepFinished = false;
                pointerStart = UnityEngine.Input.mousePosition;
                input.Begin(gameObject);
                Respond(request.id, "running", "Native input sequence started; this is not construction confirmation.");
            }
            catch (Exception error)
            {
                bool active = running != null;
                Stop(error.GetBaseException().Message);
                if (!active) Respond(lastId, "rejected", error.GetBaseException().Message);
            }
        }

        private bool SafeSession() => Application.isFocused && player && player == Player.m_localPlayer && network && network == ZNet.instance &&
            network.IsServer() && !network.IsDedicated() && !ZNet.IsOpenServer() && network.GetPeers().Count == 0 && !ZNet.m_loadError &&
            Game.instance && !Game.instance.IsShuttingDown() && !Game.instance.WaitingForRespawn() && !player.IsDead() &&
            !player.IsTeleporting() && !player.InIntro() && !Player.m_debugMode && !player.NoCostCheat();

        private bool WithinDeadline() => BuildWorksQaControlProtocol.CanContinue(running,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), (Time.realtimeSinceStartup - started) * 1000d);

        private void AfterInput()
        {
            if (running == null || (InputState.currentUpdateType != InputUpdateType.Dynamic && InputState.currentUpdateType != InputUpdateType.Manual)) return;
            try
            {
                if (!SafeSession() || !input.UiModuleMatches || input.PhysicalIntervention || UnityEngine.Input.GetKey(KeyCode.F12) || !WithinDeadline())
                { Stop("Safety guard or time limit stopped input."); return; }
                if (releaseFrame >= 0)
                {
                    if (Time.frameCount <= releaseFrame) return;
                    var complete = running; running = null; input.Release();
                    Finish(complete, "completed", "Input sequence finished; inspect the resulting state and screenshot."); return;
                }
                var step = running.steps[stepIndex];
                if (uiTarget != null && !uiReleased) uiTargets.Require(uiTarget);
                float elapsed = Time.realtimeSinceStartup - stepStarted;
                if (elapsed * 1000 >= step.milliseconds && stepFrame >= 0 && Time.frameCount > stepFrame)
                {
                    if (!stepFinished)
                    {
                        input.Feed(step, false, pointerStart, 1, 0);
                        stepFinished = true; return;
                    }
                    ++stepIndex;
                    if (stepIndex == running.steps.Length)
                    {
                        // Deliver the release edge to UI/native actions before removing test devices.
                        input.Feed(new BuildWorksQaControlProtocol.Step(), true, BuildWorksQaInput.mousePosition, 0, 0);
                        releaseFrame = Time.frameCount; return;
                    }
                    pointerStart = BuildWorksQaInput.mousePosition; stepStarted = Time.realtimeSinceStartup; stepFrame = -1; stepFinished = false;
                    step = running.steps[stepIndex]; elapsed = 0;
                }
                bool first = stepFrame < 0;
                if (first)
                {
                    BuildWorksTestIsolation.RequireLoadedSession();
                    stepFrame = Time.frameCount;
                    if (step.pointer && (step.scroll != 0 || step.buttons != 0 &&
                        !BuildWorksQaInput.GetMouseButton(0) && !BuildWorksQaInput.GetMouseButton(1) && !BuildWorksQaInput.GetMouseButton(2)))
                        pointerStart = new Vector2(step.x * (Screen.width - 1), step.y * (Screen.height - 1));
                    if (step.text != null)
                    {
                        if (uiTarget != null) uiTargets.RequireFocused(uiTarget);
                        SetFocusedText(step.text);
                    }
                }
                input.Feed(step, first, pointerStart, Mathf.Clamp01(elapsed * 1000 / step.milliseconds), Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                SubmitFocusedText(step, first);
                if (uiTarget != null && first && (stepIndex == 1 || step.text != null)) uiReleased = true;
            }
            catch (Exception error) { Stop(error.GetBaseException().Message); }
        }

        private static void SubmitFocusedText(BuildWorksQaControlProtocol.Step step, bool first)
        {
            var events = EventSystem.current;
            if (!first || Array.IndexOf(step.keys ?? Array.Empty<string>(), "Return") < 0 || !events || events.sendNavigationEvents) return;
            GameObject selected = events.currentSelectedGameObject;
            var field = selected ? selected.GetComponent<TMP_InputField>() : null;
            if (!field || !field.isActiveAndEnabled || !field.isFocused || !field.interactable || field.readOnly ||
                field.lineType == TMP_InputField.LineType.MultiLineNewline) return;
            // GuiInputField disables navigation; virtual keys do not populate TMP's legacy Event.PopEvent queue.
            ExecuteEvents.Execute(selected, new BaseEventData(events), ExecuteEvents.submitHandler);
        }

        private static void SetFocusedText(string text)
        {
            GameObject selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            var tmp = selected ? selected.GetComponent<TMP_InputField>() : null;
            var field = selected ? selected.GetComponent<InputField>() : null;
            if (tmp && tmp.interactable && !tmp.readOnly) { tmp.text = text; tmp.caretPosition = text.Length; }
            else if (field && field.interactable && !field.readOnly) { field.text = text; field.caretPosition = text.Length; }
            else throw new InvalidOperationException("Text requires a focused writable TMP/UGUI input field.");
        }

        private void Finish(BuildWorksQaControlProtocol.Request request, string status, string detail)
        {
            if (request.capture) { captureId = request.id; capture = StartCoroutine(Capture(request.id, status, detail)); }
            else Respond(request.id, status, detail);
        }

        private IEnumerator Capture(long id, string status, string detail)
        {
            yield return new WaitForEndOfFrame();
            Texture2D full = null, small = null;
            RenderTexture target = null, previous = RenderTexture.active;
            try
            {
                if ((long)Screen.width * Screen.height > 16000000 || Screen.width < 1 || Screen.height < 1)
                    throw new InvalidOperationException("Screenshot dimensions exceed the QA memory bound.");
                full = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                full.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); full.Apply();
                float scale = Mathf.Min(1, 1280f / Mathf.Max(Screen.width, Screen.height));
                int width = Mathf.Max(1, Mathf.RoundToInt(Screen.width * scale)), height = Mathf.Max(1, Mathf.RoundToInt(Screen.height * scale));
                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(full, target); RenderTexture.active = target;
                small = new Texture2D(width, height, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, width, height), 0, 0); small.Apply();
                Publish("screenshot.png", UnityEngine.ImageConversion.EncodeToPNG(small));
                Respond(id, status, detail, "screenshot.png");
            }
            catch (Exception error) { Respond(id, "uncertain", "Input was already released; screenshot failed: " + error.GetBaseException().Message); }
            finally
            {
                RenderTexture.active = previous;
                if (target) RenderTexture.ReleaseTemporary(target);
                if (full) Destroy(full); if (small) Destroy(small);
                capture = null; captureId = 0;
            }
        }

        private void Respond(long id, string status, string detail, string screenshot = null)
        {
            var eventSystem = EventSystem.current;
            var selected = eventSystem ? eventSystem.currentSelectedGameObject : null;
            var module = eventSystem ? eventSystem.currentInputModule : null;
            var currentNetwork = ZNet.instance;
            var reply = new Reply { nonce = nonce, id = id, status = status, detail = detail, screenshot = screenshot,
                utc = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), stopRevision = stopRevision,
                inventoryVisible = InventoryGui.IsVisible(), frame = Time.frameCount, width = Screen.width,
                height = Screen.height, focused = Application.isFocused, ready = SafeSession(),
                world = currentNetwork ? currentNetwork.GetWorld()?.m_worldName : null,
                selectedUi = selected ? selected.name : null,
                uiModule = module ? module.GetType().FullName : null };
            if (Player.m_localPlayer) reply.position = Player.m_localPlayer.transform.position;
            Camera camera = Camera.main;
            if (camera) { reply.cameraPosition = camera.transform.position; reply.cameraEuler = camera.transform.eulerAngles; }
            if (reply.ready)
            {
                try
                {
                    reply.snap = BuildWorksTestPlacementDiagnostics.DescribeLastSnap();
                    reply.placementContact = BuildWorksTestPlacementDiagnostics.DescribeLastContact();
                    reply.placementStatus = Player.m_localPlayer.GetPlacementStatus().ToString();
                    reply.inventory = BuildWorksTestPlacementDiagnostics.DescribeInventory(Player.m_localPlayer);
                    reply.uiTargets = uiTargets.Observe();
                    reply.uiTargetsTruncated = uiTargets.Truncated;
                }
                catch (Exception error) { reply.inventory = "QA observation unavailable: " + error.GetType().Name; }
            }
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(Reply)).WriteObject(stream, reply);
                Publish("response.json", stream.ToArray());
            }
        }

        internal void Stop(string reason)
        {
            try { GetComponent<BuildWorksTestStorage>()?.Cancel(reason); }
            catch (Exception error) { Debug.LogError("Storage QA cancellation failed; continuing input release: " + error.Message); }
            // A local model may still be thinking when F12 or focus loss stops the host.
            ++stopRevision;
            var interrupted = running; long interruptedId = interrupted?.id ?? captureId; running = null;
            uiTarget = null; uiReleased = false;
            uiTargets.Clear();
            bool hadInput = BuildWorksQaInput.Active;
            try { if (hadInput) BuildWorksPlugin.AbortQaControlInput(); }
            catch (Exception error) { Debug.LogError("BuildWorks QA gesture cancellation failed: " + error.Message); }
            try { input.Release(abort: hadInput); }
            catch (Exception error) { Debug.LogError("BuildWorks QA release failed: " + error.Message); }
            if (capture != null) { StopCoroutine(capture); capture = null; }
            captureId = 0;
            if (directory == null) return;
            try
            {
                if (File.Exists(PathFor("request.json"))) File.Delete(PathFor("request.json"));
                if (interruptedId != 0) Respond(interruptedId, "aborted", reason + " Completed native actions were not undone.");
            }
            catch (Exception error) { Debug.LogError("BuildWorks QA input released; stop receipt failed: " + error.Message); }
        }
        private void OnApplicationFocus(bool focused) { if (!focused) Stop("Focus lost."); }
        private void OnApplicationPause(bool paused) { if (paused) Stop("Application paused."); }
        private void OnDisable() { InputSystem.onAfterUpdate -= AfterInput; Stop("QA control disabled."); }
        private void OnDestroy() { InputSystem.onAfterUpdate -= AfterInput; Stop("QA control destroyed."); }
    }
}
#endif
