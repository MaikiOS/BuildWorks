# Дополнительный корпус и границы форматов — 2026-10-07

[English](external-corpus-2026-10-07.md) | **Русский**

Скачан один дополнительный публичный архив [DarkTrooper/2v2t5.0.0](https://thunderstore.io/c/valheim/p/DarkTrooper/2v2t/v/5.0.0/)
с официального download endpoint. Локальный файл artifacts/external-blueprints/2v2t-5.0.0/package.zip:
12847060 байт, SHA256 `4A6E6A53F246B5CF4C7EA9472F726D9F35687BCE978F28AB47A020AEE759DD88`.
В ZIP777 записей, среди них140.blueprint и69.vbuild. Ничего не извлекалось и не устанавливалось.
README отдаёт авторство создателям; часть построек собрана на серверах/Nexus/Thunderstore.
Лицензия на перераспространение не найдена: архив только для локальных тестов, вне Git/релизов.
Оригинальные #Creator/README сохраняются; зависимости всего modpack не объявляются зависимостями каждого файла.

inventory-blueprint.json содержит140 файлов; его старый порог2–128 не означает поддержку.
Настоящий текущий LoadFile, вызванный из StoreTests с linked production source, дал209 карточек:
156 разобраны,53 отклонены; parser-report.json хранит entry/count/error. Prefab/масштаб хоста,
все состояния, призрак, строительство/reload/сеть этим не проверены.156 не являются готовыми к стройке.
Первый reflection-вызов отказал из-за PSObject вместо System.String; явная строка исправила вызов,
сам reader не менялся. Ссылки/хеши/счётчики сохранены рядом в provenance.json.

[Dvergr Library](https://www.valheimians.com/build/dvergr-library-modded-comp-build/) автора Con Seannery
предлагает чертёж и указывает Refined Stone Pieces, Finewood Furnitures, Clutter, BuildIt, Gizmo,
Plant Everything. Попытка скачивания завершилась Cloudflare `Sorry, you have been blocked`;
этот тестовый файл НЕ получен. Разрешение на перераспространение не установлено.
[Условия Valheimians](https://www.valheimians.com/terms_of_use/) не дают права свободно перепаковывать коллекцию.

| Формат | Что есть | Граница |
|---|---|---|
| Store14 / portable5 `.buildworks.json` | Структура/группы/опоры/точки/надписи/исходники/данные; экспорт нашего JSON/ZIP | Строгая схема; неизвестные поля native JSON отклоняются;32MiB portable; архивирование не восстановление поведения |
| PlanBuild `.blueprint` | Известная геометрия/масштаб/надписи/точки и отдельные typed survival conversions | Нужен хост; неизвестные состояния/заголовки блокируют мир;2–50000 деталей,±512 локальные координаты |
| BuildShare `.vbuild` |8 базовых полей, пробелы/semicolon, сохранение расширений | ZDO scale/scaleScalar неоднозначны и заблокированы; writer отсутствует |
| ZIP | Ограниченное чтение без extraction, атомарный наш обмен/сохранение оригинала |2000 документов,128MiB общий read budget; не предел доказанной игровой нагрузки |
| [KGvalheim Blueprint](https://thunderstore.io/c/valheim/p/KGvalheim/Blueprint/) `.yml`/`.oprint` | Не поддерживается | Ссылка исходников недоступна; спецификация не проверена |
| OBJ/FBX/glTF и рельеф | Вне области | Адаптеры не начинались |

[Buildheim](https://github.com/AugusDogus/Buildheim) — ещё одна реализация известных .blueprint/.vbuild,
а не новый формат и не основание копировать код.

В исходном BiomeBlueprints353-файловом ZIP89 чертежей имеют #Center/#Coordinates/#Rotation:
81 пустой Center,8 непустых;80 нулевых Rotation,9 ненулевых. У четырёх marker отсутствует,
а в church86 одинаковых ashwood_beam_1m: удалять все детали по имени нельзя.
InfinityHammer Center меняет frame деталей/точек; Coordinates нужен restore исходного места,
а не обычной установке. Writer использует XZY для координат и YXZ для Euler; исследованные
версии reader расходятся с ним. EWD обещает исключать marker; исследованный IH создаёт все rows.
Первичные ссылки и подробности — в English-дубле выше; кэшированные версии могут различаться.

Следующий адаптер должен проверить конкретный диалект, сохранять точный источник и использовать
существующие supports/placement frame. Parser и BlueprintSourceDocument row index менять согласованно.
Проверки: первый из повторных prefab, отсутствующий/повёрнутый marker, snaps, BOM/CRLF, inline header,
malformed/duplicate/unknown sections, native/portable roundtrip. Простого whitelist нет; .86 заголовки не реализует.
