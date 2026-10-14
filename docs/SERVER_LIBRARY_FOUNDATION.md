# Server library — verified foundation, .184 WIP

**English** | [Русский](SERVER_LIBRARY_FOUNDATION_RU.md)

**The installed game build remains .183.** Source .184 connects a direct RPC runtime and client. Server UI and accepted personal copies are not implemented; no live socket/game test has run. Do not deploy this WIP over .183 until the user journey is ready. Manual QA .183 remains current.

| Requirement | Status | Evidence |
| --- | --- | --- |
| Player/admin access | Implemented in ServerBlueprintLibrary | Store denies both restricted metadata and bytes; unavailable/forbidden IDs share one response |
| Admin-only publication/update/delete | Implemented in core, no client role flag | Current trusted resolver, commit-time authority recheck, revocation after staging |
| Atomic revisioned writes | Implemented | Stable ID, revision CAS, Move/Replace, locked replace/delete preserves old file, staging cleanup |
| Persistence/bounds | Implemented in core | Reload ACL/bytes/revision, private array copies, SHA, changed/future/duplicate archives, shared stored/unpacked budgets |
| Remote identity adapter | BlueprintServerAuthority implemented | Actual source with inert host checks ready/connected direct peer/RPC/socket identity, current native admin policy, forged UID/principal denial |
| Current Valheim API | Verified separately from networking | Native compilation and Cecil HostContract pin direct connection API and IsAdmin admin-list delegation |
| Direct RPC runtime, localhost/dedicated | Connected in source | Actual-source inert host checks typed callbacks, exact connection, world UID, current native rights, raw ZPackage, disconnect/reconnect/teardown; not a live socket |
| Client/protocol/worker | Implemented and tested automatically | End-to-end publication/read/delete, ordered chunks/SHA, cancellation/timeout/session nonce, immutable snapshot; native callbacks stay on host thread |
| Shared UI/personal copy | Not implemented | Both catalog entries, admin controls and durable accepted-copy provenance remain required |
| Personal player transfers | Not implemented | Accepted separate-copy flow remains required |

Core requires an already authenticated principal supplied by the server caller. Identity and administrator resolver must never come from a request payload. The adapter checks server mode, current ready peer, exact RPC/socket references and connected state, then native ZNet.IsAdmin for the socket principal.

Current Valheim ZRoutedRpc.RPC_RoutedRPC deserializes sender from its payload without binding it to the incoming ZRpc. Routed sender UID therefore cannot establish admin authority. Native Kick/Ban callbacks use socket.GetHostName and the admin-list policy; the IL was inspected. Runtime registers direct typed ZRpc callbacks without replacing the existing Valheim handshake. Inert tests do not prove a live connection.

Storage uses separate `<id>.bwserver.zip` files with exactly `access.json` and `blueprint.buildworks.json`: metadata version1, ID/revision/access/publisher/editor/time/SHA. Temporary files are excluded. Native23/portable12 add optional libraryOrigins; construction cost, restore rights and world/network identity are unchanged. Previous native22/portable11 remain readable; old builds cannot read the new format.

LibraryOrigin v1 records the observed server, world UID, server entry ID/revision, visibility at receipt, publishing account, SHA of original bytes and their portable format version. It is informational, not authority or signed attestation. The publishing account does not replace the external model's author/terms retained in SourceDocument.

TrySaveAcceptedCopy creates a fresh personal ID without changing the source. Its game UI caller is not connected yet. The caller must derive origin from a verified client read-result SHA and exact bytes before host prefab preparation, using DecodeWithVersion; transient placementImportId is not provenance. At most16 records are retained, with explicit over-limit refusal and no silent truncation. A matching receipt refreshes the informational record.

Deep copies/update/actual-controller Save As/native/share/JSON/ZIP/reload/external DetachedCopy and locked-write rollback are tested. PlanBuild explicitly warns about omitted provenance and retains full data in the adjacent native file. Malformed/unknown/duplicate/future fields, ID/version/bounds are refused. Accept UI and origin merging when inserting another document's parts remain open.

Full Store, normal/QA0/0, Host Steam25730771, loc1057/runtime localization and correctness PASSED/Ponytail Ship pass. Exact-source Unity588 at2026-10-14T13:37:16Z verifies Save As and PlanBuild warning;39 source hashes match. Previous .183 UI evidence is preserved in artifacts/server-library-wip/previous-ui-183 and its checkpoint status/hash/log. Installed .183 is unchanged; this does not prove a live server journey.

Bounds:512 entries,128 MiB aggregate stored and unpacked bytes,100000 aggregate parts,32 MiB portable document,4096 metadata bytes. Remaining unpacked budget is checked before reading a member; physical size is checked on the opened FileStream. Corruption fails loading as a whole, without publishing a partial public cache. Changed files are refused before read/update/delete. Automatic watching of manual filesystem edits is not implemented.

Next: connect editor/Hammer server source, accepted personal copies and admin publication controls. Then verify real host/remote revocation, reconnect and restart. Player-to-player transfer remains a separate accepted-copy path preserving origin restrictions.

normal/QA builds0 warnings/errors; full Store, actual-source inert authority/runtime harness, end-to-end client/endpoint, Host Steam25730771, localization1055 and runtime localization pass. Independent correctness PASSED/Ponytail Ship. Formatter retains135 inherited WHITESPACE,0 new. No Unity rerun for transport without new UI; prior Unity588 belongs to installed .183. Evidence:`artifacts/server-library-wip` (`*-transport-*` and runtime-checks logs).

Transport v1 limits frames64 KiB, chunks60 KiB, pages12 and documents32 MiB. Endpoint caps64 connections,128 MiB transfer reservations, control/chunk rates and30 s inactivity; cancelled/disconnected workers retain their reservation until cleanup actually finishes. A per-connection nonce refuses old requests. The last32 mutation receipts on that connection replay an answer without another write. There is no durable cross-restart receipt journal: a new session refuses old packets, and the client never automatically republishes after disconnect.

Storage binds to `config/BuildWorks/server/world_<UID>/`. Runtime ticks before the local-player early return, registers only direct ready peer callbacks, and unregisters on disconnect/world change/teardown. Localhost uses the same endpoint with a private token and actual host check. Retired worker cleanup drains before a new world endpoint starts.

PrepareRead/PreparePublish perform heavy IO/decode/compression without Unity callbacks; host-thread CompleteRead/CommitPublish reauthorize and enforce one-use ownership/revision. Commit still hashes previous/staged files on the host thread; it is not wholly free of synchronous IO. A send failure after commit does not undo the write. Client owns its upload snapshot, verifies response metadata/SHA/revision, and hashes/sends at most four chunks per frame.

Native ZPackage.Write(byte[]) prefixes length; the raw protocol uses verified ZPackage(byte[]) instead. HostContract pins framing and dedicated tick order; incoming Size is checked before GetArray. Native allocations before our callback are outside this adapter.

Next: shared editor/Hammer server UI, admin publication/access/update/delete and durable origin for accepted personal copies. Do not misuse transient placementImportId or fabricate a foreign SourceDocument. Then deploy and verify actual host/remote/reconnect/restart. Player transfers remain a separate acceptance flow.

No game/network/model/input/world/save execution, no deployment. Installed .183 hashes and clean catalog remain intact. Full goal active and incomplete.
