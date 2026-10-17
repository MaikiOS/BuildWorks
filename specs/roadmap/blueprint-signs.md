<!-- docs-role: design record; reviewed 2026-10-17 -->
> Design/specification record: requirements below are not blanket implementation or acceptance claims. [Current state and queue](../../docs/CURRENT_STATUS.md).

# Blueprint signs — source checkpoint 0.19.58

**English** | [Русский](blueprint-signs_RU.md)

2026-10-06. Source-only checkpoint; installed 0.19.56, worlds and saves unchanged.
This completes the bounded sign integration step, not the heavy-blueprint release.

## Implemented

- World capture reads the stored native sign text only when that sign is viewable.
  It does not read the possibly censored display widget. Unsupported capture rejects
  the save before writing a partial blueprint.
- Every placement-plan step carries its own text, including repeated constructions.
  After successful native placement, the actual created Sign receives native `SetText`.
  Native access checks remain; platform user-content permission is checked first.
  The stored result is checked because native denial can return without an exception.
- Rejection/cancellation consumes or clears pending state. Text cannot leak into a
  later unrelated piece. Failed text application retains the already placed native
  piece and pauses the remaining queue, or reports failure after the final piece.
- Explicit blank text, Unicode, line breaks and markup survive cloning, insertion,
  history and the legacy workspace save path. The shared 4096-character data bound
  remains; native placement additionally enforces the actual prefab character limit.
- Foreign PlanBuild `info` is decoded only for the exact known prefab ID `sign`.
  Quoted JSON strings and legacy raw text are preserved, not cleaned or rewritten.
  Non-sign object data, opaque ZDO extensions and malformed input reject the whole
  import. No arbitrary ZDO payload or foreign code is executed.

Store v10 and portable v1 are unchanged. Current source allows supported signs in
native placement; `customSnapPoints` placement remains blocked until world integration.
Actual inert archive: **109 format-compatible / 244 blocked of 353**. Compatibility
with a file format does not prove installed prefabs, resources or playable placement.
The original archive remains unchanged.

## Evidence

Release: zero warnings/errors. Store, WorldLayout (11), EditorBridge (4), Localization
(754 paired keys / 663 static references), DeploymentChecks and current-game
HostContract pass. The native Sign API and setter/queue ordering were inspected;
production code does not write sign ZDO directly. Independent read-only correctness
and ponytail reviews found no blocking defect or useful complexity cut.

Fresh Unity Workbench: **144 states PASS**, `2026-10-05T21:14:51.2823329Z`,
`gameHostTested=false`. The actual sign helper runs on Unity objects with isolated
host contracts: default/raw capture, hidden content, invalid view, non-sign prefab,
character/control limits, platform denial, native setter denial/result mismatch,
and explicit blank. These simulated host contracts are not real ward/network proof.
`verify` is absent; direct tests, build, host inspection and independent review were used.

## Next game acceptance — one numbered reply set

Run only after a separately verified test-profile deployment; do not test in an
important world. No new build was installed by this checkpoint.

1. Capture a readable native sign containing Cyrillic, multiple lines and markup;
   save/reopen the blueprint. Expected: exact text, not a substituted display string.
2. Place two repeated copies and distinct signs. Expected: each receives its own text;
   deliberately empty text remains empty. Cancel and place an ordinary sign: no stale text.
3. Test denied native access and user-content permission in the isolated test setup.
   Expected: no permission bypass; already placed work remains and failure is explained.
4. Save/reload the test world and inspect from another client. Expected: native sign
   text persists and is visible only under the game's normal permission rules.

For each item reply with its number, PASS/failure, and what differs from the expected
result. Source tests cannot replace these four game checks.
