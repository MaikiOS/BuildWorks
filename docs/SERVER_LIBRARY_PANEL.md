# Server blueprint library

[Русский](SERVER_LIBRARY_PANEL_RU.md) · [Current acceptance limits](CURRENT_STATUS.md)

The common editor/Hammer panel implements bounded browsing, exact-byte hash/format review and acceptance as a personal copy. Only a server-verified administrator may publish, revise or delete server entries. Visibility levels are **players** and **administrators**. Clients do not grant themselves admin rights; a local world uses its local host authority.

Publishing uses an explicitly saved local document. The server copy has its own revision; updates are checked against that revision. Preparation cancellation writes nothing. Cancelling a sent request stops waiting and cannot undo a mutation the server already committed. World/client changes invalidate reviews.

Personal copies preserve source identity/provenance without replacing a dirty editor draft. Insertion carries immutable provenance through Undo/Redo and Save/Save As; conflicting or excessive origin sets are refused before mutation. Exact source snapshots are not server publication permissions.

Implemented UI/protocol/in-process checks are not host+remote evidence. Live role denial, sockets, reconnect and server restart remain unverified; direct player-to-player delivery is still a required future feature. File Share is available and is distinct from server publication/direct delivery.

Historical implementation receipts: [foundation](SERVER_LIBRARY_FOUNDATION.md), [manual scope .184](MANUAL_QA_184.md). The current active queue is the [roadmap](ROADMAP.md), not those earlier next-step paragraphs.
