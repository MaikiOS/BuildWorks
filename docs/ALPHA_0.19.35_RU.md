# Статус исходников BuildWorks 0.19.35 alpha

[English](ALPHA_0.19.35.md) | **Русский**

Первый игровой smoke English отклонил 0.19.34: Valheim заканчивал dotted localization token на первой точке. 0.19.35 исправляет общую границу и установлена только в отдельный профиль `TerrainRamp-1.0-Test`. Повторная приёмка English/Russian ещё нужна.

## Исправление

- читаемый source-key `editor.view.title` превращается в Valheim-token `$buildworks_editor_view_title`;
- создание токена и `Localization.AddWord` используют одно преобразование;
- все 596 runtime-ключей состоят из допустимых символов и не имеют коллизий;
- missing-форма Valheim `[key]` возвращает English fallback;
- отдельный тест без Unity проверяет English, Russian, ошибки store и fallback.

## Автоматические доказательства

- Release build: 0 предупреждений, 0 ошибок;
- каталоги 596/596, runtime-токены 596, статические обращения 576 и точные embedded-ресурсы: PASS;
- runtime-тест локализации: PASS;
- Geometry 104, Store, EditorBridge 3, WorldLayout 10, HostContract: PASS;
- Unity Workbench: 81/81 PASS;
- установленная пара совпадает с проверенными локальными SHA-256.

Commit, push, Pull Request, бинарный пакет и публичный релиз не создавались. Persistent operations, кривые и новый F9 не начинались.
