# BuildWorks 0.19.33 alpha

[English](release-notes-0.19.33.md) | **Русский**

Предварительная тестовая версия для Valheim 1.0.

## Изменения

- точный контакт одиночных скосов, крыш и мебели с выбранной поверхностью редактора;
- Q/E совмещает выбранную source snap point с курсором и показывает направление;
- семь editor-only точек для мебели/декора без vanilla snap points;
- новый Array сбрасывается в `УПАКОВАТЬ / БЕЗ ЗАЗОРА`, 2×1;
- исправлено сохранение профиля Array при Apply и один atomic Undo.

## Проверки

Release 0/0, Geometry 104, Store, EditorBridge 3, WorldLayout 10, HostContract и Unity Workbench 81/81 — PASS. Actual prefab gate: `woodwall`, `wood_beam_26`, `wood_roof`, `piece_chair`.

## Известные ограничения

- ручной игровой smoke 0.19.33 ещё не завершён;
- интерфейс BuildWorks пока только русский; полноценный English-first слой локализации запланирован и не заявляется как функция этой версии;
- это pre-release alpha, не стабильная версия;
- multiplayer, полный save/reload, import/export и procedural curves не прошли финальные ворота.

Установка и полное описание находятся в репозитории.
