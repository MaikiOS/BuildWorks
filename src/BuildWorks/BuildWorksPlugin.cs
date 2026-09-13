using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    internal sealed class BuildWorksPlugin : BaseUnityPlugin
    {
        internal const string PluginGuid = "ostrmod.buildworks";
        internal const string PluginName = "BuildWorks";
        internal const string PluginVersion = "0.19.33";

        internal static ConfigEntry<KeyCode> TogglePrecisionKey { get; private set; }
        internal static ConfigEntry<KeyCode> LockPrecisionKey { get; private set; }
        internal static ConfigEntry<KeyCode> PassiveCursorKey { get; private set; }
        internal static ConfigEntry<KeyCode> BlueprintCompositeFrameKey { get; private set; }
        internal static ConfigEntry<int> EditorHudDock { get; private set; }
        internal static ConfigEntry<int> InterfaceScalePercent { get; private set; }

        private static BuildWorksPlugin instance;
        private static bool placementInjectionInstalled;
        private Harmony harmony;
        private PrecisionPlacementSession session;
        private BlueprintEditorController blueprintEditor;
        private CompositeBlueprintStore.Blueprint pendingBlueprintEditor;
        private bool pendingNewBlueprintEditor;
        private UnifiedHammerCatalog hammerCatalog;
        private bool hammerCatalogFailed;
        private static bool hammerCatalogLayoutFailed;
        private static bool hammerCatalogViewFailed;
        private static int suppressHammerSelectionPlacementFrame = -1;
        private bool sessionCreationFailed;
        private bool skyCleanupWrongWorldLogged;
        private const string SkyCleanupWorld = "TerrainRamp_Lab";
        private const string SkyCleanupMarker =
            "BuildWorks/cleanup-sky-TerrainRamp_Lab.once";

        private void Awake()
        {
            instance = this;
            TogglePrecisionKey = Config.Bind(
                "Controls",
                "TogglePrecisionPlacement",
                KeyCode.F9,
                "Toggle the continuous BuildWorks precision placement layer.");
            LockPrecisionKey = Config.Bind(
                "Controls",
                "LockPrecisionPlacement",
                KeyCode.F10,
                "Lock the current ghost for precise editing while BuildWorks is enabled.");
            PassiveCursorKey = Config.Bind(
                "Controls",
                "PassiveCursor",
                KeyCode.LeftAlt,
                "Hold to release the cursor and drag a BuildWorks axis from normal building.");
            BlueprintCompositeFrameKey = Config.Bind(
                "Controls",
                "BlueprintCompositeFrame",
                KeyCode.N,
                "Hold in F9 blueprint placement to show the whole-blueprint frame instead of the world anchor.");
            EditorHudDock = Config.Bind(
                "Interface",
                "EditorHudDock",
                0,
                "BuildWorks HUD dock: 0 lower-right, 1 lower-center, 2 middle-left.");
            InterfaceScalePercent = Config.Bind(
                "Interface",
                "ScalePercent",
                80,
                "BuildWorks interface scale in percent (60-140).");

            if (!PrecisionPlacementSession.HostApiAvailable)
            {
                sessionCreationFailed = true;
                Logger.LogError("BuildWorks disabled: required Player placement API was not found.");
                return;
            }

            try
            {
                hammerCatalog = new UnifiedHammerCatalog(Logger);
                placementInjectionInstalled = false;
                harmony = Harmony.CreateAndPatchAll(typeof(BuildWorksPlugin).Assembly, PluginGuid);
                if (!placementInjectionInstalled)
                {
                    throw new InvalidOperationException(
                        "UpdatePlacementGhost validation injection was not installed.");
                }
            }
            catch (Exception exception)
            {
                sessionCreationFailed = true;
                harmony?.UnpatchSelf();
                harmony = null;
                Logger.LogError("BuildWorks disabled: placement integration failed: " + exception);
                return;
            }

            Logger.LogInfo("BuildWorks " + PluginVersion + " loaded.");
        }

        private void Update()
        {
            if (!Player.m_localPlayer)
            {
                DisposeBlueprintEditor();
                DisposeSession(restoreCursor: false);
                hammerCatalog?.Dispose();
                HammerCatalogView.Clear();
                HammerCatalogOrganizer.Reset();
                hammerCatalogLayoutFailed = false;
                hammerCatalogViewFailed = false;
                return;
            }

            if (blueprintEditor?.BlocksWorldInput == true)
            {
                if (blueprintEditor.IsOpen) blueprintEditor.Update();
                return;
            }

            TryRunArmedSkyCleanup();

            if (!hammerCatalogFailed)
            {
                try
                {
                    hammerCatalog?.Update(Player.m_localPlayer);
                }
                catch (Exception exception)
                {
                    hammerCatalogFailed = true;
                    DisposeSession(restoreCursor: false);
                    hammerCatalog?.Dispose();
                    HammerCatalogView.Clear();
                    HammerCatalogOrganizer.Reset();
                    Logger.LogError(
                        "BuildWorks unified Hammer catalog disabled: " + exception);
                }
            }
            PrecisionPlacementSession current = EnsureSession();
            if (TryOpenPendingBlueprintEditor(current))
            {
                blueprintEditor.Update();
                return;
            }
            current?.ContinueAutomaticPlacement(Player.m_localPlayer);
            current?.Update();
            HammerCatalogOrganizer.HandlePaging(Player.m_localPlayer);
            if (!hammerCatalogViewFailed)
            {
                try
                {
                    HammerCatalogView.Tick(Hud.instance, Player.m_localPlayer);
                }
                catch (Exception exception)
                {
                    hammerCatalogViewFailed = true;
                    HammerCatalogView.Clear();
                    instance?.Logger.LogError(
                        "BuildWorks Hammer catalog view disabled; use PageUp/PageDown: " +
                        exception);
                }
            }
        }

        private void LateUpdate()
        {
            if (blueprintEditor?.BlocksWorldInput == true)
            {
                if (blueprintEditor.IsOpen) blueprintEditor.LateUpdate();
                return;
            }
            session?.LateUpdate();
        }

        private void OnDestroy()
        {
            DisposeBlueprintEditor();
            DisposeSession();
            hammerCatalog?.Dispose();
            hammerCatalog = null;
            HammerCatalogView.Clear();
            HammerCatalogOrganizer.Reset();
            VanillaMidpointSnapPoints.RemoveAll();
            PrecisionPlacementSession.RestoreNativePieceScale();
            harmony?.UnpatchSelf();
            harmony = null;
            if (instance == this)
            {
                instance = null;
            }
        }

        private PrecisionPlacementSession EnsureSession()
        {
            if (session != null || sessionCreationFailed || !Player.m_localPlayer)
            {
                return session;
            }

            try
            {
                session = new PrecisionPlacementSession(
                    QueueBlueprintEditor,
                    QueueNewBlueprintEditor);
            }
            catch (Exception exception)
            {
                sessionCreationFailed = true;
                Logger.LogError("BuildWorks precision placement initialization failed: " + exception);
            }

            return session;
        }

        private void DisposeSession(bool restoreCursor = true)
        {
            if (restoreCursor)
            {
                session?.Dispose();
            }
            else
            {
                session?.DisposeForHostTransition();
            }
            session = null;
        }

        private void HandleHostLogout()
        {
            DisposeBlueprintEditor();
            DisposeSession(restoreCursor: false);
            hammerCatalog?.Dispose();
            HammerCatalogView.Clear();
            HammerCatalogOrganizer.Reset();
            hammerCatalogLayoutFailed = false;
            hammerCatalogViewFailed = false;
            skyCleanupWrongWorldLogged = false;
        }

        private void QueueBlueprintEditor(CompositeBlueprintStore.Blueprint blueprint)
        {
            if (blueprint == null) return;
            pendingBlueprintEditor = blueprint;
            pendingNewBlueprintEditor = false;
            CloseHammerCatalog();
        }

        private void QueueNewBlueprintEditor()
        {
            pendingBlueprintEditor = null;
            pendingNewBlueprintEditor = true;
            CloseHammerCatalog();
        }

        private static void CloseHammerCatalog()
        {
            if (Hud.IsPieceSelectionVisible()) Hud.CloseBuildUi();
        }

        private bool TryOpenPendingBlueprintEditor(PrecisionPlacementSession current)
        {
            if ((!pendingNewBlueprintEditor && pendingBlueprintEditor == null) ||
                current == null || !Camera.main || !Hud.instance ||
                Hud.IsPieceSelectionVisible())
                return false;
            try
            {
                current.SuspendForBlueprintEditor();
                if (blueprintEditor == null)
                {
                    TMP_Text textTemplate = Hud.instance.m_pieceDescription
                        ? Hud.instance.m_pieceDescription
                        : Hud.instance.m_buildSelection;
                    blueprintEditor = new BlueprintEditorController(
                        current.BlueprintStore,
                        Camera.main,
                        textTemplate,
                        current.ResolveBlueprintEditorVisual,
                        current.ResolveBlueprintEditorName,
                        current.BlueprintEditorCatalogItems,
                        message => Logger.LogError(message),
                        _ => current.RefreshBlueprintEditorLibrary());
                    blueprintEditor.InterfaceScaleChanged += scale =>
                        InterfaceScalePercent.Value = Mathf.Clamp(
                            Mathf.RoundToInt(scale * 100f), 60, 140);
                }
                bool create = pendingNewBlueprintEditor;
                CompositeBlueprintStore.Blueprint source = pendingBlueprintEditor;
                pendingNewBlueprintEditor = false;
                pendingBlueprintEditor = null;
                bool opened = create
                    ? blueprintEditor.OpenNew(out string error)
                    : blueprintEditor.OpenExisting(source, out error);
                if (!opened)
                {
                    Logger.LogError("BuildWorks blueprint editor did not open: " + error);
                    MessageHud.instance?.ShowMessage(
                        MessageHud.MessageType.Center,
                        "BuildWorks: не удалось открыть редактор: " + error);
                }
                else blueprintEditor.SetUiScale(
                    Mathf.Clamp(InterfaceScalePercent.Value, 60, 140) / 100f);
                return opened;
            }
            catch (Exception exception)
            {
                pendingNewBlueprintEditor = false;
                pendingBlueprintEditor = null;
                Logger.LogError("BuildWorks blueprint editor startup failed: " + exception);
                return false;
            }
        }

        private void DisposeBlueprintEditor()
        {
            blueprintEditor?.Dispose();
            blueprintEditor = null;
            pendingBlueprintEditor = null;
            pendingNewBlueprintEditor = false;
        }

        private void TryRunArmedSkyCleanup()
        {
            string marker = Path.Combine(Paths.ConfigPath, SkyCleanupMarker);
            if (!File.Exists(marker) || !ZNet.instance || !Player.m_localPlayer ||
                Time.timeSinceLevelLoad < 15f) return;
            string world = ZNet.instance.GetWorldName();
            if (!string.Equals(world, SkyCleanupWorld, StringComparison.Ordinal))
            {
                if (!skyCleanupWrongWorldLogged)
                {
                    skyCleanupWrongWorldLogged = true;
                    Logger.LogWarning("BuildWorks sky cleanup is armed for " +
                        SkyCleanupWorld + ", not " + world + ".");
                }
                return;
            }

            Vector3 playerPosition = Player.m_localPlayer.transform.position;
            Vector2 cleanupCenter = new Vector2(68.74f, 335.81f);
            if ((new Vector2(playerPosition.x, playerPosition.z) - cleanupCenter)
                .sqrMagnitude > 100f * 100f) return;

            int removed = 0;
            try
            {
                string[] prefabNames =
                {
                    "ashwood_decowall_divider",
                    "woodwall",
                    "ashwood_decowall_divider"
                };
                Vector3[] positions =
                {
                    new Vector3(69.24f, 214.63f, 335.88f),
                    new Vector3(68.74f, 214.63f, 335.68f),
                    new Vector3(68.24f, 214.63f, 335.88f)
                };
                foreach (Piece piece in UnityEngine.Object.FindObjectsByType<Piece>(
                    FindObjectsSortMode.None))
                {
                    if (!piece || piece.GetCreator() == 0L) continue;
                    string prefabName = piece.gameObject.name
                        .Replace("(Clone)", string.Empty)
                        .Trim();
                    int target = -1;
                    for (int index = 0; index < positions.Length; ++index)
                    {
                        if (string.Equals(
                            prefabName,
                            prefabNames[index],
                            StringComparison.Ordinal) &&
                            (piece.transform.position - positions[index]).sqrMagnitude <=
                            0.25f * 0.25f)
                        {
                            target = index;
                            break;
                        }
                    }
                    if (target < 0) continue;
                    ZNetView view = piece.GetComponent<ZNetView>();
                    if (!view || !view.IsValid()) continue;
                    Logger.LogWarning("BuildWorks removes exact legacy sky piece " +
                        piece.gameObject.name + " at " + piece.transform.position + ".");
                    view.ClaimOwnership();
                    view.Destroy();
                    ++removed;
                }
                File.Delete(marker);
                Logger.LogWarning("BuildWorks exact one-shot sky cleanup finished in " +
                    world + ": removed " + removed + "/3 legacy pieces.");
            }
            catch (Exception exception)
            {
                Logger.LogError("BuildWorks sky cleanup failed; marker kept for retry: " +
                    exception);
            }
        }

        private static void ApplyPrecisionTransformBeforeValidation(Player player)
        {
            instance?.session?.ApplyTransformBeforeValidation(player);
        }

        private static bool ApplyPassiveAutoJoinFromNativeSnap(
            Player player,
            ref Transform sourceSnap,
            ref Transform targetSnap,
            Piece aimedPiece)
        {
            return instance?.session?.ApplyPassiveAutoJoin(
                player,
                ref sourceSnap,
                ref targetSnap,
                aimedPiece) ?? true;
        }

        private static void ApplyPrecisionValidationPoint(Player player, ref Vector3 point)
        {
            instance?.session?.ApplyValidationPoint(player, ref point);
        }

        private static readonly System.Reflection.FieldInfo PlacementRayMaskField =
            AccessTools.Field(typeof(Player), "m_placeRayMask");
        private static readonly System.Reflection.FieldInfo PlacementWaterRayMaskField =
            AccessTools.Field(typeof(Player), "m_placeWaterRayMask");
        private static readonly System.Reflection.FieldInfo MaximumPlaceDistanceField =
            AccessTools.Field(typeof(Player), "m_maxPlaceDistance");

        private static bool ApplyBlueprintValidationContact(bool nativeHit, Player player,
            ref Vector3 point, ref Vector3 normal, ref Piece hitPiece,
            ref Heightmap heightmap, ref Collider waterSurface, bool water)
        {
            if (instance?.session == null || !instance.session.TryPrepareBlueprintValidation(
                player, out GameObject ghost, out Piece source)) return nativeHit;
            point = normal = Vector3.zero;
            hitPiece = null;
            heightmap = null;
            waterSurface = null;
            try
            {
                if (!BuildWorksPlacementValidation.TryGetBounds(ghost, out Bounds bounds)) return false;
                PlacementContactSides sides = source.m_inCeilingOnly ? PlacementContactSides.Above :
                    source.m_notOnFloor ? PlacementContactSides.NonFloor :
                    source.m_groundPiece || source.m_groundOnly || source.m_cultivatedGroundOnly ||
                    source.m_vegetationGroundOnly
                        ? PlacementContactSides.Below : PlacementContactSides.All;
                int mask = (int)(water ? PlacementWaterRayMaskField : PlacementRayMaskField).GetValue(player);
                float maximumDistance = (float)MaximumPlaceDistanceField.GetValue(player) + source.m_extraPlacementDistance;
                if (!BuildWorksPlacementValidation.TryFindContact(ghost, bounds, mask,
                    player.GetEyePoint(), maximumDistance, sides, out RaycastHit contact)) return false;
                point = contact.point;
                normal = contact.normal;
                hitPiece = contact.collider.GetComponentInParent<Piece>();
                heightmap = contact.collider.GetComponent<Heightmap>();
                if (contact.collider.gameObject.layer == LayerMask.NameToLayer("Water"))
                    waterSurface = contact.collider;
                // A dry support collider below the water must not turn an underwater
                // noInWater piece into a valid placement. Query the native liquid level.
                Vector3 bottom = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                if (source.m_noInWater && Floating.GetLiquidLevel(bottom, 1f, LiquidType.Water) >
                    bottom.y + BuildWorksPlacementValidation.ContactTolerance(bounds))
                {
                    WaterVolume volume = null;
                    Floating.GetWaterLevel(bottom, ref volume);
                    if (!volume || !volume.GetComponent<Collider>()) return false;
                    waterSurface = volume.GetComponent<Collider>();
                }
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("BuildWorks blueprint contact validation failed: " + exception);
                return false;
            }
        }

        private static bool UsePrecisionFreePlacement(Player player, bool vanillaFreePlacement)
        {
            return vanillaFreePlacement ||
                instance?.session?.UsesFrozenPrecisionTransform(player) == true;
        }

        private static bool ShouldRunNativeGhostUpdate(Player player)
        {
            if (instance?.blueprintEditor?.BlocksWorldInput == true) return false;
            return instance?.session?.AllowsNativeGhostUpdate(player) != false;
        }

        private static bool ShouldBlockNativePlacement(Player player, Piece piece)
        {
            if (instance?.blueprintEditor?.BlocksWorldInput == true) return true;
            return instance?.session?.ShouldBlockNativePlacement(player, piece) == true;
        }

        private static bool ShouldBlockPlayerInput(Player player)
        {
            if (instance?.blueprintEditor?.BlocksWorldInput == true) return true;
            return instance?.session?.ShouldBlockPlayerInput(player) == true;
        }

        private static bool ShouldBlockGameCamera()
        {
            if (instance?.blueprintEditor?.BlocksWorldInput == true) return true;
            return instance?.session?.ShouldBlockGameCamera() == true;
        }

        private static bool ShouldBlockCharacterControls()
        {
            if (instance?.blueprintEditor?.BlocksWorldInput == true) return true;
            return instance?.session?.ShouldBlockCharacterControls() == true;
        }

        private static void BeginNativePlacement(Player player, Piece piece)
        {
            instance?.session?.OnNativePlacementStarting(player, piece);
        }

        private static void EndNativePlacement(Player player, Piece piece, bool placed)
        {
            instance?.session?.OnNativePlacementFinished(player, piece, placed);
        }

        private static void AbortNativePlacement(Player player, Piece piece)
        {
            instance?.session?.OnNativePlacementException(player, piece);
        }

        private static void PrepareNativePlacementUpdate(Player player, ref bool takeInput)
        {
            if (player == Player.m_localPlayer &&
                suppressHammerSelectionPlacementFrame == Time.frameCount)
            {
                takeInput = false;
                return;
            }
            if (instance?.blueprintEditor?.BlocksWorldInput == true)
            {
                takeInput = false;
                return;
            }
            instance?.session?.PrepareNativePlacementUpdate(player, ref takeInput);
        }

        private static void ApplyBlueprintCatalogLayout(PieceTable table)
        {
            instance?.session?.ApplyBlueprintCatalogLayout(table);
        }

        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static class TransformGhostBeforeNativeValidationPatch
        {
            private static bool Prefix(Player __instance)
            {
                return ShouldRunNativeGhostUpdate(__instance);
            }

            private static IEnumerable<CodeInstruction> Transpiler(
                IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> code = new List<CodeInstruction>(instructions);
                var finalValidationMethod = AccessTools.Method(
                    typeof(Location),
                    nameof(Location.IsInsideNoBuildLocation),
                    new[] { typeof(Vector3) });
                var rayTestMethod = AccessTools.Method(typeof(Player), "PieceRayTest");
                var closestSnapMethod = AccessTools.Method(
                    typeof(Player),
                    "FindClosestSnapPoints");
                var placementGhostField = AccessTools.Field(typeof(Player), "m_placementGhost");
                var applyMethod = AccessTools.Method(
                    typeof(BuildWorksPlugin),
                    nameof(ApplyPrecisionTransformBeforeValidation));
                var applyPointMethod = AccessTools.Method(
                    typeof(BuildWorksPlugin),
                    nameof(ApplyPrecisionValidationPoint));
                var contactMethod = AccessTools.Method(
                    typeof(BuildWorksPlugin),
                    nameof(ApplyBlueprintValidationContact));
                var freePlacementMethod = AccessTools.Method(
                    typeof(BuildWorksPlugin),
                    nameof(UsePrecisionFreePlacement));
                var passiveAutoJoinMethod = AccessTools.Method(
                    typeof(BuildWorksPlugin),
                    nameof(ApplyPassiveAutoJoinFromNativeSnap));
                var setRotationMethod = AccessTools.PropertySetter(
                    typeof(Transform),
                    nameof(Transform.rotation));

                int finalValidationCall = code.FindIndex(
                    instruction => instruction.Calls(finalValidationMethod));
                int rayTestCall = code.FindIndex(instruction => instruction.Calls(rayTestMethod));
                int closestSnapCall = code.FindIndex(
                    instruction => instruction.Calls(closestSnapMethod));
                int finalGhostRotation = code.FindLastIndex(
                    finalValidationCall - 1,
                    finalValidationCall,
                    instruction => instruction.Calls(setRotationMethod));
                int transformInjection = finalGhostRotation + 1;
                int freePlacementStore = code.FindIndex(
                    0,
                    rayTestCall,
                    instruction => instruction.opcode == OpCodes.Stloc_0);

                if (finalValidationCall < 0 || rayTestCall < 6 || closestSnapCall < 4 ||
                    finalGhostRotation < 0 || transformInjection >= code.Count ||
                    freePlacementStore < 0 ||
                    code[rayTestCall + 1].opcode.FlowControl != FlowControl.Cond_Branch ||
                    code[closestSnapCall + 1].opcode.FlowControl != FlowControl.Cond_Branch ||
                    code[closestSnapCall - 4].opcode != OpCodes.Ldloca_S ||
                    code[closestSnapCall - 3].opcode != OpCodes.Ldloca_S ||
                    code[rayTestCall - 6].opcode != OpCodes.Ldloca_S ||
                    code[rayTestCall - 5].opcode != OpCodes.Ldloca_S ||
                    code[rayTestCall - 4].opcode != OpCodes.Ldloca_S ||
                    code[rayTestCall - 3].opcode != OpCodes.Ldloca_S ||
                    code[rayTestCall - 2].opcode != OpCodes.Ldloca_S ||
                    code.FindIndex(0, finalGhostRotation, instruction =>
                        instruction.opcode == OpCodes.Ldfld &&
                        Equals(instruction.operand, placementGhostField)) < 0)
                {
                    throw new InvalidOperationException(
                        "Current Valheim UpdatePlacementGhost validation layout is unsupported.");
                }

                // Restrict automatic snapping to the piece under the crosshair, then
                // rotate into its axes before Valheim positions and validates the pair.
                int passiveAutoJoinInjection = closestSnapCall + 2;
                CodeInstruction passivePlayer = new CodeInstruction(OpCodes.Ldarg_0);
                passivePlayer.labels.AddRange(code[passiveAutoJoinInjection].labels);
                passivePlayer.blocks.AddRange(code[passiveAutoJoinInjection].blocks);
                code[passiveAutoJoinInjection].labels.Clear();
                code[passiveAutoJoinInjection].blocks.Clear();
                code.Insert(passiveAutoJoinInjection, passivePlayer);
                code.Insert(
                    passiveAutoJoinInjection + 1,
                    new CodeInstruction(OpCodes.Ldloca_S, code[closestSnapCall - 4].operand));
                code.Insert(
                    passiveAutoJoinInjection + 2,
                    new CodeInstruction(OpCodes.Ldloca_S, code[closestSnapCall - 3].operand));
                code.Insert(
                    passiveAutoJoinInjection + 3,
                    new CodeInstruction(OpCodes.Ldloc_S, code[rayTestCall - 4].operand));
                code.Insert(
                    passiveAutoJoinInjection + 4,
                    new CodeInstruction(OpCodes.Call, passiveAutoJoinMethod));
                code.Insert(
                    passiveAutoJoinInjection + 5,
                    new CodeInstruction(
                        OpCodes.Brfalse,
                        code[closestSnapCall + 1].operand));

                // All control-flow paths that finish Valheim snapping target this
                // instruction. Move its labels so every path applies our frozen
                // transform before later overlap and placement validation.
                CodeInstruction loadPlayer = new CodeInstruction(OpCodes.Ldarg_0);
                loadPlayer.labels.AddRange(code[transformInjection].labels);
                loadPlayer.blocks.AddRange(code[transformInjection].blocks);
                code[transformInjection].labels.Clear();
                code[transformInjection].blocks.Clear();
                code.Insert(transformInjection, loadPlayer);
                code.Insert(transformInjection + 1, new CodeInstruction(OpCodes.Call, applyMethod));

                // Feed the transformed position to checks that run before the
                // placement ghost receives its final transform.
                int pointInjection = rayTestCall + 2;
                CodeInstruction pointLocal = code[rayTestCall - 6];
                code.Insert(pointInjection, new CodeInstruction(OpCodes.Ldarg_0));
                code.Insert(
                    pointInjection + 1,
                    new CodeInstruction(pointLocal.opcode, pointLocal.operand));
                code.Insert(pointInjection + 2, new CodeInstruction(OpCodes.Call, applyPointMethod));

                // Precision mode already starts from one captured Valheim snap;
                // do not let a later automatic re-snap overwrite the edited base.
                int freePlacementInjection = freePlacementStore + 1;
                code.Insert(freePlacementInjection, new CodeInstruction(OpCodes.Ldarg_0));
                code.Insert(freePlacementInjection + 1, new CodeInstruction(OpCodes.Ldloc_0));
                code.Insert(
                    freePlacementInjection + 2,
                    new CodeInstruction(OpCodes.Call, freePlacementMethod));
                code.Insert(freePlacementInjection + 3, new CodeInstruction(OpCodes.Stloc_0));

                // Keep the native ray result on the stack, then replace the complete
                // contact before its success branch and all early surface checks.
                int contactInjection = code.FindIndex(instruction => instruction.Calls(rayTestMethod)) + 1;
                var contactCode = new List<CodeInstruction> { new CodeInstruction(OpCodes.Ldarg_0) };
                contactCode[0].labels.AddRange(code[contactInjection].labels);
                contactCode[0].blocks.AddRange(code[contactInjection].blocks);
                code[contactInjection].labels.Clear();
                code[contactInjection].blocks.Clear();
                for (int argument = 6; argument >= 1; --argument)
                {
                    CodeInstruction original = code[contactInjection - 1 - argument];
                    contactCode.Add(new CodeInstruction(original.opcode, original.operand));
                }
                contactCode.Add(new CodeInstruction(OpCodes.Call, contactMethod));
                code.InsertRange(contactInjection, contactCode);

                placementInjectionInstalled = true;
                return code;
            }
        }

        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.UpdateAvailable))]
        [HarmonyAfter("com.jotunn.jotunn")]
        private static class HammerCatalogLayoutPatch
        {
            private static void Prefix(
                PieceTable __instance,
                out HammerCatalogOrganizer.SelectionSnapshot __state)
            {
                __state = HammerCatalogOrganizer.CaptureSelection(__instance);
            }

            private static void Postfix(
                PieceTable __instance,
                HammerCatalogOrganizer.SelectionSnapshot __state)
            {
                ApplyBlueprintCatalogLayout(__instance);
                if (hammerCatalogLayoutFailed) return;
                try
                {
                    HammerCatalogOrganizer.Reorder(__instance, __state);
                }
                catch (Exception exception)
                {
                    hammerCatalogLayoutFailed = true;
                    HammerCatalogOrganizer.DisablePagination(__instance);
                    instance?.Logger.LogError(
                        "BuildWorks Hammer catalog sorting disabled: " + exception);
                }
            }
        }

        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.LeftPiece))]
        private static class HammerCatalogLeftPiecePatch
        {
            private static bool Prefix(PieceTable __instance) =>
                !HammerCatalogOrganizer.MoveIndexSelection(__instance, -1, 0);
        }

        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.RightPiece))]
        private static class HammerCatalogRightPiecePatch
        {
            private static bool Prefix(PieceTable __instance) =>
                !HammerCatalogOrganizer.MoveIndexSelection(__instance, 1, 0);
        }

        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.UpPiece))]
        private static class HammerCatalogUpPiecePatch
        {
            private static bool Prefix(PieceTable __instance) =>
                !HammerCatalogOrganizer.MoveIndexSelection(__instance, 0, -1);
        }

        [HarmonyPatch(typeof(PieceTable), nameof(PieceTable.DownPiece))]
        private static class HammerCatalogDownPiecePatch
        {
            private static bool Prefix(PieceTable __instance) =>
                !HammerCatalogOrganizer.MoveIndexSelection(__instance, 0, 1);
        }

        [HarmonyPatch(typeof(Hud), "UpdateBuild")]
        private static class HammerCatalogViewPatch
        {
            private static void Prefix(
                Player player,
                out Piece.PieceCategory __state)
            {
                PieceTable table = PrecisionPlacementSession.GetBuildPieceTable(player);
                __state = table ? table.GetSelectedCategory() : Piece.PieceCategory.Misc;
            }

            private static void Postfix(
                Hud __instance,
                Player player,
                Piece.PieceCategory __state)
            {
                if (hammerCatalogViewFailed) return;
                try
                {
                    HammerCatalogOrganizer.EnsureCategoryNavigation(player, __state);
                    HammerCatalogView.Apply(__instance, player);
                }
                catch (Exception exception)
                {
                    hammerCatalogViewFailed = true;
                    HammerCatalogView.Clear();
                    instance?.Logger.LogError(
                        "BuildWorks Hammer catalog view disabled; use PageUp/PageDown: " +
                        exception);
                }
            }
        }

        [HarmonyPatch(typeof(BuildUi), nameof(BuildUi.OpenBuildMenu))]
        private static class HammerCatalogDefaultOpenPatch
        {
            private static void Postfix()
            {
                HammerCatalogView.OpenDefaultIndex(Hud.instance, Player.m_localPlayer);
            }
        }

        [HarmonyPatch(typeof(BuildUi), nameof(BuildUi.Close))]
        private static class HammerCatalogDefaultOpenResetPatch
        {
            private static void Postfix()
            {
                HammerCatalogView.MarkNativeBuildUiClosed();
            }
        }

        [HarmonyPatch(typeof(BuildUi), "OnFavoritePieceAdded")]
        private static class HammerCatalogFavoriteAddedPatch
        {
            private static bool Prefix(BuildUi __instance, string __0)
            {
                HammerCatalogOrganizer.RefreshFavoriteButtons(__instance, __0);
                return false;
            }
        }

        [HarmonyPatch(typeof(BuildUi), "OnFavoritePieceRemoved")]
        private static class HammerCatalogFavoriteRemovedPatch
        {
            private static bool Prefix(BuildUi __instance, string __0)
            {
                HammerCatalogOrganizer.RefreshFavoriteButtons(__instance, __0);
                return false;
            }
        }

        [HarmonyPatch(typeof(BuildUi), "NavigationUpdate")]
        private static class HammerCatalogBlueprintContextInputPatch
        {
            private static bool Prefix() =>
                !HammerCatalogView.ShouldCaptureBlueprintContext();
        }

        [HarmonyPatch(typeof(Hud), "OnLeftClickPiece")]
        private static class HammerCatalogIndexSelectionInputPatch
        {
            private static void Prefix()
            {
                if (HammerCatalogView.NativeIndexActive)
                {
                    suppressHammerSelectionPlacementFrame = Time.frameCount;
                    PlayerController.SetTakeInputDelay(0.2f);
                }
            }
        }

        [HarmonyPatch(typeof(Hud), "OnDestroy")]
        private static class HammerCatalogViewCleanupPatch
        {
            private static void Prefix()
            {
                HammerCatalogView.Clear();
            }
        }

        [HarmonyPatch(typeof(ZInput), nameof(ZInput.GetMouseScrollWheel))]
        private static class HammerCatalogWheelCapturePatch
        {
            private static bool Prefix(ref float __result)
            {
                if (!HammerCatalogView.ShouldCaptureWheel) return true;
                __result = 0f;
                return false;
            }
        }

        [HarmonyPatch(typeof(Game), nameof(Game.Logout))]
        private static class GameLogoutCleanupPatch
        {
            private static void Prefix()
            {
                instance?.HandleHostLogout();
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static class NativePlacementLifecyclePatch
        {
            private static bool Prefix(Player __instance, Piece piece, ref bool __result)
            {
                if (!ShouldBlockNativePlacement(__instance, piece))
                {
                    BeginNativePlacement(__instance, piece);
                    return true;
                }

                __result = false;
                return false;
            }

            private static void Postfix(Player __instance, Piece piece, bool __result)
            {
                EndNativePlacement(__instance, piece, __result);
            }

            private static Exception Finalizer(
                Player __instance,
                Piece piece,
                Exception __exception)
            {
                if (__exception != null)
                {
                    AbortNativePlacement(__instance, piece);
                }
                return __exception;
            }
        }

        [HarmonyPatch(typeof(ZNetScene), "Awake")]
        private static class NativePieceScaleRegistrationPatch
        {
            [HarmonyPriority(Priority.Last)]
            private static void Postfix(ZNetScene __instance) =>
                PrecisionPlacementSession.EnableNativePieceScale(__instance);
        }

        [HarmonyPatch(typeof(Player), "PlacePiece")]
        private static class NativePieceScalePlacementPatch
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var replacement = AccessTools.Method(typeof(NativePieceScalePlacementPatch), nameof(InstantiatePiece));
                int count = 0;
                foreach (CodeInstruction instruction in instructions)
                {
                    if (instruction.operand is System.Reflection.MethodInfo method &&
                        method.DeclaringType == typeof(UnityEngine.Object) && method.Name == nameof(UnityEngine.Object.Instantiate) &&
                        method.IsGenericMethod && method.GetGenericArguments()[0] == typeof(GameObject) &&
                        method.GetParameters().Length == 3 && method.GetParameters()[1].ParameterType == typeof(Vector3) &&
                        method.GetParameters()[2].ParameterType == typeof(Quaternion))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = replacement;
                        ++count;
                    }
                    yield return instruction;
                }
                if (count != 1) throw new InvalidOperationException("PlacePiece native Instantiate hook count: " + count);
            }

            private static GameObject InstantiatePiece(GameObject source, Vector3 position, Quaternion rotation)
            {
                GameObject placed = UnityEngine.Object.Instantiate(source, position, rotation);
                try
                {
                    instance?.session?.ApplyNativePlacementScale(placed, source.GetComponent<Piece>());
                    return placed;
                }
                catch
                {
                    TerrainModifier.SetTriggerOnPlaced(false);
                    if (placed && ZNetScene.instance) ZNetScene.instance.Destroy(placed);
                    else if (placed) UnityEngine.Object.Destroy(placed);
                    throw;
                }
            }
        }

        [HarmonyPatch(typeof(Player), "UpdatePlacement")]
        private static class ContinuousPlacementInputPatch
        {
            private static void Prefix(Player __instance, ref bool __0)
            {
                PrepareNativePlacementUpdate(__instance, ref __0);
            }

        }

        [HarmonyPatch(typeof(Player), "TakeInput")]
        private static class PrecisionEditorPlayerInputPatch
        {
            private static void Postfix(Player __instance, ref bool __result)
            {
                if (ShouldBlockPlayerInput(__instance))
                {
                    __result = false;
                }
            }
        }

        [HarmonyPatch(typeof(Piece), nameof(Piece.GetSnapPoints),
            new Type[] { typeof(List<Transform>) })]
        private static class VanillaMidpointSnapPointPatch
        {
            private static void Postfix(Piece __instance, List<Transform> points)
            {
                VanillaMidpointSnapPoints.Append(__instance, points);
            }
        }

        [HarmonyPatch(typeof(PlayerController), "TakeInput")]
        private static class PrecisionEditorControllerInputPatch
        {
            private static bool Prefix(ref bool __result)
            {
                if (!ShouldBlockCharacterControls())
                {
                    return true;
                }

                __result = false;
                return false;
            }
        }

        [HarmonyPatch(typeof(GameCamera), "UpdateCamera")]
        private static class PrecisionEditorCameraPatch
        {
            private static bool Prefix()
            {
                return !ShouldBlockGameCamera();
            }
        }

        [HarmonyPatch(typeof(GameCamera), "UpdateMouseCapture")]
        private static class PrecisionEditorMouseCapturePatch
        {
            private static bool Prefix()
            {
                return !ShouldBlockGameCamera();
            }
        }
    }
}
