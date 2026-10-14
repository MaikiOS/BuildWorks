# .185 WIP — bounded saved-storage readback

QA0.19.184.0 remains installed for manual tests. The185 core is neither deployed nor connected to placement.

NativeWorldStorageReadback checks explicit world41 streams: world UID, framing/budgets, chunk mapping, a single operation marker across all mapped chunks, prefab/creator/Persistent and exact native109 inventory bytes. Unknown fields are skipped, not interpreted. It does not copy old network IDs or authorize settlement.

Limits: metadata/map4MiB, chunk64MiB, total1GiB,4M objects,65536 chunks,32767 typed keys and target inventory1MiB. One sorted scratch buffer avoids per-object sets; large irrelevant strings/arrays are skipped without eager allocation.

345 CPU assertions pass.10001 records allocate773728B under .NET10; two2MiB skip regressions pass. This is not a Mono/Valheim performance claim. Isolated Unity uses the actual installed ZDO.Save to produce82bytes and verifies them; no ZNet, player/world save or live inventory transfer. Final receipt2026-10-14T15:56:33.2759975Z.

Normal/proper QA builds0/0, HostContract185, localization1100, new-reader formatting0, independent correctness PASSED/Ponytail Ship. Initial CPU parsing missed InvalidDataException; fixed. Reviewer found per-map allocation; fixed with regression. Standalone native serializer failed on Unity ECall; isolated Unity supplied the proper runtime. Formatter MSBuild pipe access was denied; approved retry passed. HostContract expected184; updated to185.

Still open: canonical current-generation host-file binding, worker read/revalidation of world/generation/item custody/journal, server receipt for remote, terminal settlement and placement integration. Filled storage placement stays disabled. An `.ok` file or callback is not a two-participant durable acknowledgement. Return/cancel/reload/network remain incomplete.

Evidence: artifacts/storage-world-readback/{geometry-verified-final,build-normal,build-qa,host-final,localization-final,unity-native-final}.log, format-final.json, actual-native-serializer.txt; artifacts/placement-contact/verification-status.json.
