"""Execute actual session input, pose and history routes across explicit host seams."""
import hashlib
import json
import pathlib

root = pathlib.Path(__file__).resolve().parents[2]
source = root / 'src/BuildWorks/PrecisionPlacementSession.cs'
partial = root / 'src/BuildWorks/PrecisionPlacementSession.Transform.cs'
keyboard = root / 'src/BuildWorks/TransformKeyboardInput.cs'
input_source = root / 'src/BuildWorks/BlueprintEditorInput.cs'
store = root / 'src/BuildWorks/CompositeBlueprintStore.cs'
text = source.read_text(encoding='utf-8-sig') + '\n' + partial.read_text(encoding='utf-8-sig')
signatures = [
    'private bool TryBeginWorldPrecision(',
    'private bool TryBegin(',
    'private bool BeginBlueprintWorkspacePrecision(',
    'private void HandleGizmoInput(Camera camera)',
    'private void BeginDrag(Camera camera, Vector2 mouse)',
    'private Vector3[] UpdateAnchorWorldPoints()',
    'private static Vector3 CanonicalAxis(GizmoAxis axis)',
    'private void MovePieceWithoutMovingCameraFocus(Vector3 position)',
    'private static Point3 ToGeometry(Vector3 value)',
    'private static Vector3 ToUnity(Point3 value)',
    'private static Rotation3 ToGeometry(Quaternion value)',
    'private Vector3 ResolveTransformPivot()',
    'private void ApplyGizmoRotation(Quaternion rotation)',
    'private void UpdateBlueprintFrameOverride()',
    'private void ClearSnapDrag()',
    'private void SelectAnchorConstraint(GizmoAxis axis)',
    'internal readonly struct TransformSnapshot',
    'private Vector3 ScaledAnchor(int anchor)',
    'private TransformSnapshot CaptureHistorySnapshot()',
    'private TransformSnapshot BlueprintPartTransform(int index)',
    'private void ApplyHistorySnapshot(TransformSnapshot snapshot)',
    'private void CommitTransformIfChanged(',
    'private static bool TransformChanged(',
    'private static bool HistorySnapshotChanged(',
    'private bool CancelNewestStage()',
    'private void ResetTransform()',
    'private void UndoTransform()',
    'private void RedoTransform()',
    'private bool TryAddPlacementInstance(',
    'private void RebuildBlueprintPlan()',
    'private void CommitBlueprintEditTarget()',
    'private IEnumerable<Vector3> BlueprintCustomPoints()',
    'private bool IsBlueprintTransform =>',
    'private bool HandleKeyboardTransform(Camera camera)',
    'private void BeginKeyboardTransform(Camera camera, GizmoHandleKind handle)',
    'private void SetKeyboardTransformAxis(Camera camera, GizmoAxis axis)',
    'private void RebaseKeyboardTransform(Camera camera)',
    'private void PreviewKeyboardTransform(Camera camera, bool precise)',
    'private bool TryPreviewKeyboardTransform()',
    'private void ConfirmKeyboardTransform()',
    'private void CancelKeyboardTransform()',
    'private void ClearKeyboardTransform()',
    'private static Quaternion ToUnityRotation(Rotation3 rotation)',
]


def method(signature, contents=None):
    contents = text if contents is None else contents
    assert contents.count(signature) == 1, signature
    start = contents.index(signature)
    brace = contents.find('{', start)
    arrow = contents.find('=>', start)
    if arrow >= 0 and (brace < 0 or arrow < brace):
        return contents[start:contents.index(';', arrow) + 1]
    depth = 0
    for index in range(brace, len(contents)):
        if contents[index] == '{': depth += 1
        elif contents[index] == '}':
            depth -= 1
            if depth == 0: return contents[start:index + 1]
    raise ValueError('Unclosed method: ' + signature)


output = root / 'artifacts/f9-transform-20261105/generated'
output.mkdir(parents=True, exist_ok=True)
update = method('public void Update()')
call = 'UpdateBlueprintFrameOverride();'
call_end = update.index(call) + len(call)
guard_start = update.rfind('if (sessionMode == SessionMode.BlueprintWorldPlacement &&', 0, call_end)
assert guard_start >= 0, 'Actual frame caller guard not found'
guard = update[guard_start:call_end]
partial_text = partial.read_text(encoding='utf-8-sig')
fields_start = partial_text.index('{', partial_text.index('internal sealed partial class PrecisionPlacementSession')) + 1
fields = partial_text[fields_start:partial_text.index('private bool IsBlueprintTransform =>')]
escape = method('if (Input.GetKeyDown(KeyCode.Escape))', update)
prelude = update[update.index('if (hud.IsEditingAngle)'):update.index('if (Input.GetKeyDown(KeyCode.Escape))')]
availability = method('if (!(keyboardInputAllowed ?? GameplayInputAvailable(player)))', update)
editing = method('if (state == PlacementState.Editing)', update)
inactive = method('if (state == PlacementState.Inactive)\n            {\n                UpdatePassiveCursor(player);', update)
camera_input = method('private void HandleFreeViewInput(Camera camera)')
movement = camera_input[camera_input.index('Vector3 movement = Vector3.zero;'):camera_input.index('if (movement.sqrMagnitude')]
wrappers = ('\nprivate void UpdateFrameFromActualCaller() {\n' + guard + '\n}\n'
    + 'private void EscapeFromActualUpdate() { var player = Player.m_localPlayer;\n' + prelude + escape + '\n}\n'
    + 'private void KeyboardFromActualUpdate(Camera freeViewCamera) { var player = Player.m_localPlayer;\n'
    + inactive + prelude + escape + availability + editing + '\n}\n'
    + 'private Vector3 MovementFromActualCamera(Quaternion rotation) {\n' + movement + 'return movement;\n}\n')
store_text = store.read_text(encoding='utf-8-sig')
store_methods = [
    'internal static VectorData RelativeScale(VectorData absolute, VectorData prefab)',
    'internal static bool PositiveScale(VectorData value)',
    'private static bool IsFinite(float value)',
]
part_methods = ['internal Vector3 RootScale(Vector3 prefabScale)', 'internal Vector3 PlacementScale(Vector3 prefabScale)']
code = ('using System; using System.Collections.Generic; using System.Linq; using System.Globalization; '
        'using UnityEngine; using OstrixMods.BuildWorks.Geometry;\n'
        'using Camera=HostCamera; using Input=HostInput; using Time=HostTime; '
        'using Quaternion=HostQuaternion; using KeyCode=HostKeyCode; using Piece=HostPiece; '
        'using GameObject=HostGameObject; using RaycastHit=HostRaycastHit;\n'
        'namespace OstrixMods.BuildWorks { internal sealed partial class PrecisionPlacementSession {\n'
        + fields + '\n'.join(method(signature) for signature in signatures) + wrappers + '\n} }\n'
        + 'namespace OstrixMods.BuildWorks {\n'
        + method('internal interface IBlueprintEditorInput', input_source.read_text(encoding='utf-8-sig'))
        + '\ninternal static partial class CompositeBlueprintStore {\n'
        + '\n'.join(method(signature, store_text) for signature in store_methods)
        + '\ninternal sealed partial class Part {\n'
        + '\n'.join(method(signature, store_text) for signature in part_methods) + '\n} } }\n'
        + 'namespace OstrixMods.BuildWorks' + keyboard.read_text(encoding='utf-8-sig').split('namespace OstrixMods.BuildWorks', 1)[1])
(output / 'SessionRoute.cs').write_bytes(code.encode('utf-8'))
(output / 'INPUTS.json').write_text(json.dumps({
    'source': str(source), 'sha256': hashlib.sha256(source.read_bytes()).hexdigest().upper(),
    'methods': signatures, 'actualUpdateCallerGuard': guard,
    'sources': [{'path': str(path), 'sha256': hashlib.sha256(path.read_bytes()).hexdigest().upper()}
                for path in (source, partial, keyboard, input_source, store)],
    'actualEscapeRoute': escape,
    'actualEditingRoute': editing,
    'engineSeams': ['input/camera projection/quaternion', 'bounds capture', 'renderer/layout callbacks', 'native view/extra-point host data'],
    'generatedSHA256': hashlib.sha256(code.encode('utf-8')).hexdigest().upper(),
}, indent=2), encoding='utf-8')
print(f'Extracted {len(signatures)} actual session methods; input/projection/layout boundary substituted.')
