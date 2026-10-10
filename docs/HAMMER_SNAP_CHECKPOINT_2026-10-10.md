# 0.19.123 — exact Hammer snap search

Source checkpoint only. Installed QA0.19.117 in TerrainRamp-1.0-Test remains
hash-identical. All12 import refusals, including4 large snap sets, remain open.

For large local BuildWorks ghosts, a narrow Harmony transpiler redirects the
two inner nearest-target calls. The native outer method still gathers world
targets, walks ordered source children and preserves manual indices. The
existing post-native rotation hook is unchanged. No points are removed.

The existing Geometry KD index gains a rounded-distance query with native
first-target ties and inclusive radius. Its old squared-distance query retains
exact regression parity. A target snapshot/index lives for one outer invocation;
nested calls and exceptions restore the prior thread context. Small/ordinary/
nonlocal ghosts, foreign patches and unsupported input use the native delegate.
Actual Unity distance is checked before returning the indexed target, retaining
the native fallback on mismatch. Target snapshots are bounded at50000.

Evidence: local `artifacts/checkpoint-0.19.123/` and
`artifacts/ui-runtime/hammer-snap-search-check.txt`.

- Normal/QA0 warnings/errors, Geometry, Store, HostContract Steam25185596,
  localization930 and compiled QA isolation PASS.
- Full Unity6000.0.61f1:492 UI cases and new Harmony assertions PASS,
  UTC `2026-10-10T08:14:57.7495702Z`.
- Production patch actually applied to an inert native-signature Player fixture
  using real Unity transforms:17125 rotated/XYZ sources ×128 targets retain both
  winning Transform identities without fallback calls. Manual/fresh/empty/tied/
  invalid-radius/small/ordinary/nonlocal/nested/fault/later-foreign-prefix and12
  random-set cases pass. This is not actual Valheim Player/world execution.
- One fixture sample:101.0623ms reference versus15.0395ms indexed. Not median,
  FPS, whole catalogue/placement or loaded-world performance evidence.
- All source IDs from the4 archival nearest-part assignments match again;
  the source archive remains unchanged.
- Preserved initial failure logs: missing lab Harmony dependencies fixed through
  hash-checked local copies; then734 exact-distance fallback calls fixed by
  removing the extra float store before sqrt. Native float-sum → double-sqrt →
  float expression retained; the equality fallback was not removed. The short
  diagnostic entrypoint now shares snappoint-tag setup with full acceptance.
- `verify` is unavailable; direct checks ran. Independent correctness and
  Ponytail reviews PASS; receipts are recorded in local `verification.json`.

Remaining: real Valheim world collection/placement/reload/network and FPS;
creation cost of all ordered child transforms and full loaded-world cost.
Aggregate parser/Store/Geometry/portable/capture512, per-piece world codec512,
native21/portable10, costs, permissions and object contents remain unchanged.
Next coordinate a separate custom-point capacity and complete4-file roundtrips.
No installation, game launch, save mutation or network test occurred.
