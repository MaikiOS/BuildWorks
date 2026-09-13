# Локализация

[English](LOCALIZATION.md) | **Русский**

English — обязательный fallback. Russian — полный overlay. BuildWorks не зависит от Jotunn, поэтому `BuildWorksLocalization` напрямую регистрирует встроенные TSV в Valheim `Localization` и повторяет регистрацию после `SetupLanguage`.

## Добавление или изменение текста

1. Добавить одинаковый стабильный ключ в `src/BuildWorks/Translations/English.tsv` и `Russian.tsv`.
2. Сохранить одинаковые нумерованные плейсхолдеры в обоих значениях.
3. Для готового UI-текста использовать `BuildWorksLocalization.Text("area.action", arguments)`.
4. `BuildWorksLocalization.Token("area.action")` использовать только там, где локализацию позже выполняет компонент Valheim.
5. Собрать плагин, затем запустить `scripts/Test-Localization.ps1 -RequireBuiltAssembly`.

Ключи исходных каталогов остаются dotted для удобства чтения. Только на границе Valheim `BuildWorksLocalization` заменяет точки подчёркиваниями: `editor.view.title` превращается в `$buildworks_editor_view_title`. В runtime-токенах Valheim нельзя оставлять точки, потому что его parser заканчивает ключ на этом разделителе. Localization gate проверяет допустимость и отсутствие коллизий всех преобразованных ключей; `BuildWorks.LocalizationTests` без запуска Unity проверяет регистрацию, выбор English/Russian, ошибки хранилища и fallback отсутствующего ключа.

Переведённые строки нельзя сравнивать в логике. Категории, материалы, источники и действия используют стабильные ID вроде `wood`, `other`, `vanilla`, `blueprints`; переводится только видимая подпись.

TSV поддерживает `\n`, `\t` и `\\`. Ключи чувствительны к регистру. Проверка отклоняет дубли, пустые значения, отсутствующие пары English/Russian, разные плейсхолдеры, отсутствующие runtime-ключи и встроенный кириллический UI-текст.

`CompositeBlueprintStore` не зависит от Unity-локализации. Он возвращает error keys `$buildworks_...`, а UI-границы разрешают их через `BuildWorksLocalization.ResolveUserText`. Старая системная категория `ПРОЧЕЕ` при загрузке мигрирует в стабильный ID `OTHER`.

Проверка сверяет пары каталогов, синтаксис стабильных ключей, плейсхолдеры, все статически используемые ключи, отсутствие встроенных русских UI-строк и наличие обоих TSV-ресурсов в собранной DLL. Изолированный Unity Workbench копирует `English.tsv` и использует тестовый English-only adapter; production-регистрация языка остаётся только в `BuildWorksLocalization.cs`.
