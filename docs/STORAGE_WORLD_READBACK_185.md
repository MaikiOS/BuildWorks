# .185 WIP — bounded saved-storage readback

QA0.19.185.0 is installed in TerrainRamp-1.0-Test after automated checks. Both deployed hashes match and111 existing config/save files are unchanged; the catalogue/imports remain empty. The185 core is not connected to placement; no in-game acceptance of185 is claimed. Existing manual184/Batch-183 checks remain applicable.

NativeWorldStorageReadback checks explicit world41 streams: world UID, framing/budgets, chunk mapping, a single operation marker across all mapped chunks, prefab/creator/Persistent and exact native109 inventory bytes. Unknown fields are skipped, not interpreted. It does not copy old network IDs or authorize settlement.

Limits: metadata/map4MiB, chunk64MiB, total1GiB,4M objects,65536 chunks,32767 typed keys and target inventory1MiB. One sorted scratch buffer avoids per-object sets; large irrelevant strings/arrays are skipped without eager allocation.

345 CPU assertions pass.10001 records allocate773728B under .NET10; two2MiB skip regressions pass. This is not a Mono/Valheim performance claim. Isolated Unity uses the actual installed ZDO.Save to produce82bytes and verifies them; no ZNet, player/world save or live inventory transfer. Final receipt2026-10-14T15:56:33.2759975Z.

Normal/proper QA builds0/0, HostContract185, localization1100, new-reader formatting0, independent correctness PASSED/Ponytail Ship. Initial CPU parsing missed InvalidDataException; fixed. Reviewer found per-map allocation; fixed with regression. Standalone native serializer failed on Unity ECall; isolated Unity supplied the proper runtime. Formatter MSBuild pipe access was denied; approved retry passed. HostContract expected184; updated to185.

Current-generation binding is implemented in source. NativeWorldStorageFiles opens only the exact committed native Local generation: absolute trusted directory, numeric `_main.<generation>.fwl2/.chunks/.ok/.db2`, no `.new/.old` fallback or reparse roots/files. DB2 validation covers its version41/finite-time header and budget, not full DB2/world load. Handles deny concurrent writes while allowing native retirement/deletion of old generations without invalidating pinned bytes.

BlueprintStorageWorldGeneration captures host/world identity, UID, generation, native filenames and exact completed journal/item custody on the owning game thread. A detached worker reads immutable expectations/files only; acceptance rechecks cancellation and the entire current context on that thread. Return-child operation IDs remain distinct from the parent storage marker. Transfer exposes preparation only; no gameplay callers, settlement, refund or marker release are enabled.

92 additional assertions pass for generation changes, thread ownership, cancellation, custody, source/server, canonical components, DB2, writer denial/delete sharing and return identity. The filesystem is real temporary fixtures; the native host is substituted. Final normal/QA0/0, Geometry, Host185, localization1100, new-file formatting and independent correctness PASSED/Ship pass. Sandbox denied File.Move for the pinned-handle test with UnauthorizedAccessException; the same fixture-only test passed outside sandbox. One whitespace issue was fixed and verified.

Still open: paired world/player durable verification, remote server receipt, terminal settlement and placement integration. Filled storage placement stays disabled. An `.ok` file or callback is not a two-participant durable acknowledgement. Return/cancel/reload/network remain incomplete. Binding evidence: artifacts/storage-world-readback/binding-*.log and artifacts/content-reservations/verification-status.json.

Evidence: artifacts/storage-world-readback/{geometry-verified-final,build-normal,build-qa,host-final,localization-final,unity-native-final}.log, format-final.json, actual-native-serializer.txt; artifacts/placement-contact/verification-status.json.
