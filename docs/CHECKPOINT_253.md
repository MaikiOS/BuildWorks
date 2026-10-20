# QA .245 — saved profile custom-data evidence

2026-10-20. Checklist8.7 remains open. This step does not implement collection, receipt creation or terminal paid cancellation. v11/house appearance is unchanged and unaccepted.

`TryVerifyPlayerData` now parses the verified PlayerData33/34 tail only after exact full fresh Player.Save byte identity, returning immutable `Evidence.CustomData` from the file. Legacy `TryVerify` still leaves the tail opaque and returns `CustomData=null`; an empty verified map is distinct. Skills.Save2 and v34 suffix dictionaries are covered. Native sanitized knownTexts key collisions and mod skill wire-ID collisions are allowed. The bounded BuildUi payload remains opaque; this is not complete Player.Load/UI validation. File32MiB/count65536 limits are explicit fail-closed safety policies.

Normal/QA builds have zero warnings/errors;1023 saved-map checks within1078 profile assertions, full content/persistence harness, Store, Geometry, current host contract, localization1122 and QA separation pass. An existing native profile47/PlayerData34 was inspected read-only: its exact inventory matches the retained payout evidence, player bytes and empty custom map match independent extraction. This is not a newly executed Player.Save; nonempty receipt maps are synthetic. Two independent source reviews, including correctness/ponytail, pass for this scope.

Guarded deployment installed .245 in TerrainRamp-1.0-Test with matching DLL hashes and177 protected QA files unchanged. No game/bridge was launched. Initial sandbox File.Move/NuGet access errors and stale .244 host-version rejection are retained; required checks passed after the appropriate fixes/access. A PowerShell fixture initially passed an empty string instead of null and was corrected using NullString; parser validation was not weakened.

Evidence: local `BuildWorks/artifacts/checkpoint-253/` build/test logs, retained-profile-verification.json, retained-profile-extraction.json, deployment.json and the two source reviews. Saved player data/native extracted sources are excluded from Git.

Next: exact empty-cell credit plus permanent receipt under a narrow reentrant-save guard; paired profile proof; old sealed-drop cleanup and later-world absence; crash/reload/replay after spending. Existing8Wood obligations/four sealed stacks remain owed and unchanged. No new owner test is assigned. Collection, terminal cancellation, multiplayer, faults and house visual acceptance remain open.
