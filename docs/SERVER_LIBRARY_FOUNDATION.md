# Server library — verified foundation, .184 WIP

**English** | [Русский](SERVER_LIBRARY_FOUNDATION_RU.md)

**The installed game build remains .183.** Source .184 compiles, but the network/user journey is not connected. Do not deploy this WIP over .183 until that path is ready. Manual QA .183 remains current.

| Requirement | Status | Evidence |
| --- | --- | --- |
| Player/admin access | Implemented in ServerBlueprintLibrary | Store denies both restricted metadata and bytes; unavailable/forbidden IDs share one response |
| Admin-only publication/update/delete | Implemented in core, no client role flag | Current trusted resolver, commit-time authority recheck, revocation after staging |
| Atomic revisioned writes | Implemented | Stable ID, revision CAS, Move/Replace, locked replace/delete preserves old file, staging cleanup |
| Persistence/bounds | Implemented in core | Reload ACL/bytes/revision, private array copies, SHA, changed/future/duplicate archives, shared stored/unpacked budgets |
| Remote identity adapter | BlueprintServerAuthority implemented | Actual source with inert host checks ready/connected direct peer/RPC/socket identity, current native admin policy, forged UID/principal denial |
| Current Valheim API | Verified separately from networking | Native compilation and Cecil HostContract pin direct connection API and IsAdmin admin-list delegation |
| Live RPC/catalog/localhost | Not connected | No handlers/buttons registered; no live network execution |
| Personal player transfers | Not implemented | Accepted separate-copy flow remains required |

Core requires an already authenticated principal supplied by the server caller. Identity and administrator resolver must never come from a request payload. The adapter checks server mode, current ready peer, exact RPC/socket references and connected state, then native ZNet.IsAdmin for the socket principal.

Current Valheim ZRoutedRpc.RPC_RoutedRPC deserializes sender from its payload without binding it to the incoming ZRpc. Routed sender UID therefore cannot establish admin authority. Native Kick/Ban callbacks use socket.GetHostName and the admin-list policy; the IL was inspected. No game RPC handlers are registered here. This adapter is not a complete handshake or live-network test.

Storage uses separate `<id>.bwserver.zip` files with exactly `access.json` and `blueprint.buildworks.json`: metadata version1, ID/revision/access/publisher/editor/time/SHA. Temporary files are excluded. Portable documents retain the existing format; construction cost, restore rights and world/network identity are unchanged.

Bounds:512 entries,128 MiB aggregate stored and unpacked bytes,100000 aggregate parts,32 MiB portable document,4096 metadata bytes. Remaining unpacked budget is checked before reading a member; physical size is checked on the opened FileStream. Corruption fails loading as a whole, without publishing a partial public cache. Changed files are refused before read/update/delete. Automatic watching of manual filesystem edits is not implemented.

Next: bind storage to server world UID and verify local-host authority; split heavy preparation from host-thread authenticated commit; register direct peer RPC with bounded chunks/rate/memory/timeout/cancellation/replay and disconnect cleanup; connect editor/Hammer server source, accepted personal copies and admin publication controls. Then verify real host/remote revocation, reconnect and restart. Player-to-player transfer remains a separate accepted-copy path preserving origin restrictions.

normal/QA builds0 warnings/errors; full Store, actual-source inert authority harness, Host Steam25730771, localization1055 and runtime localization pass. Independent correctness PASSED/Ponytail Ship. Formatter retains135 inherited WHITESPACE,0 new. No Unity rerun for this unconnected backend; prior Unity588 belongs to installed .183. Evidence:`artifacts/server-library-wip`.

No game/network/model/input/world/save execution, no deployment. Installed .183 hashes and clean catalog remain intact. Full goal active and incomplete.
