<!-- docs-role: historical snapshot; reviewed 2026-10-17 -->
> Historical snapshot: versions, checkboxes, installed/next text and manual commands below apply to the original checkpoint. [Current state and queue](../CURRENT_STATUS.md).

# Exchange path acceptance — 0.19.82

Owner-facing instructions: [Russian, with literal file names and Russian explanations](player-exchange-0.19.82_RU.md).

This checkpoint only corrects the shared Hammer/editor path display, not the remaining
foreign-state, heavy-scene, world contents or oversized-lamp work.

- Compact current-folder label; explicit Edit path opens a 64px multiline field.
- Cancel restores the browsed destination; Open validates before changing it.
- Import/export cannot start with an unconfirmed path edit. Copy path retains full text.
- TMP escape parsing is disabled for literal Windows paths, including `\nfspr` and
  `\test-fixtures`. NoWrap alone did not prevent those sequences becoming control characters.
- The isolated regression uses the actual owner's path, not `C:\Users\Ostrix`;
  focused input checks reject rendered LF/CR/TAB characters. Russian collapsed/expanded
  layouts cover four resolutions and three UI scales, including deselected input.
- Test fixture names are unchanged; the Russian instructions explain all six individually.

Real Valheim acceptance remains an owner gate. Do not infer it from Workbench success.
