# BuildWorks controls

**English** | [Русский](CONTROLS_RU.md)

The editor footer and world HUD display only the actions available in the current state. The tables below are a reference, not a replacement for those contextual hints.

The editor footer reserves a separate area beneath the viewport for mode keys,
current tool actions, history/group/visibility and camera controls. Text fields,
menus and dragging show their own hints instead. The Inspector keeps its height.

## Indexed hammer

| Action | Control |
| --- | --- |
| Select a card | Left mouse button |
| Add/remove favorite | Middle mouse button |
| Change page | Q/E or PageUp/PageDown |
| Open blueprint actions | Hold right mouse button, hover, release |

## Editor: general

| Action | Control |
| --- | --- |
| Catalog | Tab or Shift+A |
| Select / Gizmo / Array / Contour | 1 / 2 / 3 / 4 (F9 also opens Gizmo) |
| Temporary Move / Rotate / uniform Scale | G / R / S |
| Single tools menu / search | Space / F3 |
| Undo / Redo | Ctrl+Z / Ctrl+Y |
| Save | Ctrl+S |
| Duplicate | Ctrl+D |
| Group | Ctrl+G |
| Select all | A or Ctrl+A |
| Clear selection / cancel tool | Esc |
| Frame selection | F |
| Frame all | Home |
| Temporarily ignore the assigned anchor | Hold N |

## Editor: piece placement

| Action | Control |
| --- | --- |
| Place preview | Left mouse button |
| Rotate | Mouse wheel |
| Change source snap point | Q/E |
| Delete hovered placed piece | Delete |
| Cancel current placement | Esc |

## Editor: selection and visibility

| Action | Control |
| --- | --- |
| Add/remove from selection | Ctrl/Shift + left mouse button |
| Hide selected | H |
| Isolate selection or group | Shift+H |
| Show everything except selected | Ctrl+H |
| Show all | Alt+H |
| Toggle see-through occluders | F7 or Display → See-through |
| Delete | Delete |

See-through starts off and is editor-only. Unselected mesh parts blocking sampled
rays to the visible selection disappear; selected parts stay solid. Camera/selection
changes and turning it off restore drawing. Only the active hidden snap target
gets a restrained outline. Document hidden flags, materials, source prefabs and
saved/world data are unchanged; temporary blockers do not intercept selection.

In Contour (C), hovering a connected support edge shows only the blue guide;
LMB creates preview copies. Cancel, Esc, tool changes and deleting the source clear both.

## Editor camera

| Action | Control |
| --- | --- |
| Orbit | Middle mouse button + move |
| Pan | Shift + middle mouse button + move |
| Zoom | Mouse wheel; Ctrl+wheel during placement/Surface Move |
| Free look | Hold right mouse button |
| Fly | Right mouse button + WASD, Q/E up/down |
| Faster flight | Shift |

## Transform

G follows another real surface, excluding the selected parts; no ray hit means no
confirmation, not distant-ground fallback. P explicitly chooses screen-plane Move.
R rotates in the camera-facing plane until constrained; S scales uniformly.
During these previews XYZ constrains an axis, repeating it switches World/Local,
Shift+axis excludes that axis. Surface Q/E cycles source points and wheel changes yaw.
Enter/LMB confirms once; Esc cancels exactly. Camera navigation pauses/rebases the
preview. Persistent Gizmo families are selected in Manipulator; hidden handles
cannot be picked. The collapsible footer keeps a highlighted next-action strip.

| Action | Control |
| --- | --- |
| Translate/rotate | Drag an arrow or ring with the left mouse button |
| Duplicate while moving | Alt + drag arrow |
| Temporary magnetic move | Ctrl + drag |
| Constrain anchor by axis | X/Y/Z |
| Precise numeric scrub | Drag value; Shift for finer, Ctrl for faster |

## F9 in the world

| Action | Default control |
| --- | --- |
| Toggle precision layer | F9 |
| Lock current ghost | F10 |
| Release cursor for handles | Hold Left Alt |
| Use full blueprint frame instead of world anchor | Hold N |
| Undo / Redo | Ctrl+Z / Ctrl+Y |
| Cancel | Esc |

F9, F10, Alt, N, HUD position, and UI scale can be changed in the BepInEx configuration.
