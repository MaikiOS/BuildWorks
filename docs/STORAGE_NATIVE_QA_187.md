<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](CURRENT_STATUS.md).

# .187 — one-shot native storage save/reload QA

QA-only BuildWorksTestStorage is created by an explicit BuildWorksQaControl call. It never starts automatically. The normal assembly contains neither the component nor its receipt parser. No new runtime service or production construction bypass is introduced.

The fixture requires the existing owned isolated Local QA world/character, closed solo survival, focus, idle save, closed inventory/editor and an inactive placement session. It selects a nearby clear loaded flat site using the existing station footprint checks. A flushed one-shot receipt and position/identity details precede any native action. It creates exactly one native wood chest, verifies identity/ownership/creator/transform/empty inventory and charges the native recipe once. This directly exercises PlacePiece and ConsumeResources, not the full TryPlacePiece eligibility path.

Two own Wood are reserved after payment. The detached archived recipe has an extra custom property so the transfer can prove it retained the provided properties. The existing transfer primitive and .186 paired persistence run unchanged. Each meaningful stage is flushed to the isolated action log; refusal, F12, focus loss, session change or timeout stop continuation without automatic replay, restoration, refund, deletion or settlement. Completed native actions remain available for inspection.

Reload is a separate read-only check. It requires the same world/player/operation and a new Player instance or process. Only loaded containers are inspected, with one exact persistent marker match. Recovery validates native inventories and current provided properties; canonical player and current world generation readbacks must match the saved inventory hashes. A new network UID is not treated as identity. The WAL remains unresolved for the next reconciliation/settlement stage.

The local GABS script `artifacts/storage-native-qa-187/scenario.json` contains23 steps including logout/re-entry, bounded status waits, inventory observations and screenshots. It calls `BuildWorksPlugin.instance.qaControl` by its full object path. It stops on failure; polling is read-only and never repeats the mutating start call. Protection is restored by test/run; it is re-enabled after re-entry while checking the new Player.

Automated coverage:18 compiled BCL receipt cases, QA exclusion and once-before-placement/payment/transfer order, read-only reload IL checks, existing identity/path/one-shot isolation fixtures, QA input protocol43, observation publisher and UI target45 checks. Builds/HostContract/localization and independent reviews are separate evidence. None of these checks is a live native run.

Owner launch/handoff instructions: docs/MANUAL_QA_187_RU.md. The game scenario is still pending; full imported construction, recovery/settlement, remote peers and fog v11 remain open. Local evidence is under artifacts/storage-native-qa-187; game files, receipts, identifiers and private bridge implementation are excluded from the source checkpoint.

Final automated checks and source reviews passed: normal/QA0/0, Host187 on Steam25730771, localization1100, receipt18 and existing isolation fixtures, protocol43/publisher/UI45, formatter0, correctness PASSED/Ponytail Ship. Sandbox junction/File.Replace/MSBuild pipe refusals were reproduced and approved fixture-only retries passed. Moving the pure parser away from MonoBehaviour resolved the standalone UnityEngine.CoreModule dependency; the missing moved constant was fixed before final builds.

Guarded deployment followed a closed-game CIM check and WhatIf. QA0.19.187.0 SHA `13C84F9F03683CD5E45C4BB6A3D4406EDCF3C6898F0A7F94AD45E8D1CC221825`; Geometry SHA `DDFB02FE409816DE74BADFF042BC2141FAA282C07E41DF2297CBB86AA15639B1`.111 previous config/save files are unchanged; catalogue0/imports0. .186 DLLs are backed up locally. No game was launched by this checkpoint.
