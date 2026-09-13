# Статус исходников BuildWorks 0.19.34 alpha

[English](ALPHA_0.19.34.md) | **Русский**

0.19.34 — автоматически проверенная версия исходников. Она ещё не установлена и не принята в Valheim; в тестовом профиле остаётся 0.19.33.

## Изменения

- добавлена прямая регистрация English fallback и Russian overlay через Valheim `Localization`, без Jotunn;
- текущий текст Hammer, Blueprint Editor, Outliner, Array, Contour, F9, validation и store переведён на 596 стабильных ключей;
- переведённые подписи отделены от ID категорий, материалов, источников, действий и сохранений;
- имена копий, Array и Contour локализуются без зависимости `BuildWorks.Geometry` от Valheim;
- добавлена выполняемая проверка локализации обеих сборок и embedded-каталогов в DLL;
- добавлены English-first документы архитектуры, локализации и участия с русскими дублями;
- закреплены правила форматирования и границы ответственности основных runtime-модулей.

## Автоматические доказательства

- Release build: 0 предупреждений, 0 ошибок;
- localization: 596 одинаковых English/Russian ключей, 576 статически используемых runtime-ключей, оба TSV встроены;
- Geometry: 104 проверки;
- Store, EditorBridge 3, WorldLayout 10, HostContract: PASS;
- Unity Workbench: 81/81 состояния PASS;
- Assembly и FileVersion: 0.19.34.0.

## Ручные ворота

English и Russian нужно проверить в Valheim: индексный Hammer, библиотека, редактор, меню Outliner, Array, Contour, F9 и validation errors. Видимых `$buildworks_...` быть не должно. Save/reload и multiplayer остаются отдельными этапами.

Сохраняемые recipes операций, кривые и будущий redesign F9 не начинались.
