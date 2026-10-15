# Ordinary world snapping audit (.192)

2026-10-15. Diagnostic only; no product fix or native acceptance claimed. The running Default-Compat-Test game, input and saves were untouched.

The loader reports .192. Installed DLL SHA256 `A7EA8B0EE62080F8B07E762D6AF1A49290E57306593E6A16D0A97D8CD7F10C70` differs from the local normal build; instruction hashes for `ApplyPassiveAutoJoin`, `AutoJoinRotation`, and midpoint `Append` match separately. Current native assembly SHA256 is `94998885B103D6E5F5480123EAEE0A093E82AF2DDEEEE37536C15BF555F24F3A`.

Native automatic snapping selects the nearest source/target pair within0.5m, keeps the first equal-distance candidate and translates the pair without changing its established rotation. In auto (`m_manualSnapPoint < 0`), BuildWorks then prefers an eligible pair on the aimed piece. Manual retains its native pair. Both paths multiply an ordinary ghost rotation by the target's complete root frame. Composite blueprint ghosts keep their existing rotation. The editor instead uses cursor projection/visibility/hysteresis and compensates the source selection frame during keyboard alignment. Midpoint injection is another difference from unmodified native point sets.

Real Unity6000.0.61f1 Transform/quaternion tests of the production helper reproduced:

- A symmetric horizontal beam can retain the same box-corner set after root yaw180 or roll180 while the ordinary ghost changes by180 degrees.
- Rotating after pair selection can change which source is nearest. A synthetic example has two candidate root placements1.99999988m apart. This is hypothetical re-ranking, **not stale coordinates or detached sockets**: native reads live Transform positions after the hook and still aligns the chosen pair.
- Strict first-equal-distance selection can choose opposite endpoints for a reversed enumeration of the same symmetric set. The diagnostic models verified native IL; it does not call native Player.
- Common world translations100m and2560m retain the calculated alignment within0.001m.

Camera affects ray hit, aimed owner and initial ghost position; the editor also uses screen distance. The production frame helper has no camera input. Native IL273–292 LookRotation rotates the placement marker, not the ghost; ordinary wheel yaw and special surface-placement branches must be distinguished.

Evidence is local `artifacts/snap-audit-192`: `installed-methods.json`, `native-snap-il.txt`, `native-orientation-and-translation.txt`, `unity-evidence.txt`, `SnapAuditEvidence.cs`. The diagnostic ran exit0 and was removed from the Workbench afterward. SocratiCode returned no callers for the Harmony handler, so direct transpiler/IL reads supplied actual usage. MemPalace history was not treated as runtime proof. Independent readings were reconciled against actual IL before publishing this audit.

Recommendation: preserve player rotation for ordinary automatic placement, and give intentional target-XYZ inheritance an explicit separate purpose. Do not replace root rotation with socket-child rotation without a defined socket convention. Before changing the accepted behaviour, settle compatibility with the previous intentional XYZ inheritance and capture native owner/socket/root/ghost/wheel/manual/alternative/ray data in the QA world. Exact prefab IDs and rotations in the three screenshots remain unknown. No new DLL, deployment, resource changes or live control occurred. [Detailed Russian comparison and code locations](WORLD_SNAP_AUDIT_192_RU.md).
