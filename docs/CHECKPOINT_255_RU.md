# Этап255 — сохранённая пара при получении возврата

20.10.2026. QA/normal **0.19.247**. [English](CHECKPOINT_255.md).

## Сделано и проверено

Пункт8.7: per-row CreditIntent записывается до native AddItem в одну пустую ячейку, без слияния прежних стеков. Сохраняются свойства предоставленного предмета; custody-tag снимается только с независимой копии. Промежуточный pending receipt не является подтверждением. Три точных instance-prefix защищают Player.Save/Profile.SavePlayerData/Profile.SavePlayerToDisk на время staging; после выхода из scope обычные сохранения доступны.

Первое ProfileCredited требует каноническую пару: точные inventory bytes/manifest и строгую customData из одного файла, с тем же payer/контекстом/поколением мира и protected source. Один receipt не закрывает CreditIntent. Receipt-only допустим только для уже полностью подтверждённого replay, включая последующий расход материалов. Ошибка native после эффекта не повторяет вставку; можно повторить только сохранение той же coherent pair. Неопределённые исходы остаются для сверки.

- Normal/QA builds:0 ошибок/0 предупреждений; Store и Geometry прошли.
-76 payout assertions: legacy canonical без Collections, ordered intent, exact delta/атрибуты, occupied/out-of-range cell, неверные proofs, корректно перехешированная порча, запрет receipt-only settlement и idempotent replay.
-12 save-guard и16 actual extracted custody predicate/wiring cases — CPU с inert host; не native callback/fault proof.
-2037 profile assertions, включая959 recovery и прежние1023 fresh-map; отдельный случай «receipt есть, inventory отсутствует/другой» отклонён.
- Host contract, localization1122 и QA/normal isolation прошли. `verify` skill отсутствует; использованы прямые проверки и независимые обзоры.
- QA .247 установлена при закрытой игре;177 protected files unchanged. BuildWorks.dll SHA256 `2212AF6493F8B7E76EDE9CA9110081CB02F7AA8E8A40A343EB0C571681D3782F`; Geometry `EE7C108E682F0CB6FDA85AB56EA8CEA63F7D41637C5BF2DD0CFA7E46DA3BF457`.

## Игровой исход — получение НЕ проверено

Вход через ValheimQA в прежние BW_QA_43d3719a/world1137745799, profileTerrainRamp-1.0-Test, cloud=false/qaSaveRoot=true. Скопированный preflight содержал устаревшее phase0 вместо уже сохранённого IssuedSaved3. Ошибка ожидания сохранена. Защита сценария затем вернула god/ghost=false; между вызовами персонаж погиб. Следующий preflight попал в respawn и не нашёл local player. Исходный инвентарь остался в могиле; ресурсы заново не выдавались. Нельзя считать это успешной проверкой credit.

Оба первоначальных payout остаются phase3/revision7, Collections отсутствует. Ни CreditIntent, ни вставка не начались. Для продолжения нужно штатно забрать прежние вещи и заново проверить баланс59Wood/молоток — без grant и без новых стендов. Полные prepared сценарии12+12 и5reload шагов сохранены, но не выполнены.

По прямому запросу Ostrix включён штатный world key PassiveMobs (`setkey passivemobs`, cheat=false). GetGlobalKey=true подтверждён до logout и после повторной загрузки. Мобы не удалялись, другие профили не менялись. God/ghost включены отдельно на время сессии; мир штатно сохранён, Application.Quit, собственный PID9684 вышел,3DLL моста восстановлены. В дальнейшем пассивность мира не заменяет проверку временной god/ghost защиты до сценариев.

## Ограничения и следующий шаг

8.7/8.8 открыты: native credit/faults/reload получения ещё не проверены, старые world drops остаются sealed, cleanup/terminal cancellation/кнопка получения, каскадное разрушение и сеть не завершены. v11/дом9.2–9.7 остаётся главным открытым визуальным условием. Нового исправления снапинга в .247 нет; ограниченное исправление равных пар229 сохранено, полный Hammer/камера/удерживаемый наклон не приняты.

Обычный пакет для ручной установки: `artifacts/manual-update-0.19.247/` и `.zip`; обе DLL, manifest и инструкция. В нём нет QA-команд/моста. Основной профиль автоматически не менялся.

## Доказательства

`artifacts/checkpoint-255/`: `normal-journal-guard.log`, `qa-journal-guard.log`, `payout-journal-guard.log`, `store-pair.log`, `geometry-pair.log`, `content-pair.log`, `host-pair.log`, `localization-pair.log`, `isolation-final.log`, `deployment.json`, `verification.json`, `final-source-review.md`, `ponytail-review.md`.

Первичные ошибки компиляции/доступа и BLOCKED reviews сохранены: они не переписаны в PASS. Ранняя незапущенная .247 заменена после journal-guard review; её deployment-before-journal-guard сохранён отдельно. Игровые JSON/логи: `ValheimQA/artifacts/20261020-151506/` (относительно Default), `credit-not-started.json`, `passive-world-setting.json`, два неуспешных preflight, `mcp.jsonl`, `restore.json`. Пользовательские saves и игровые DLL/IL в исходный Git не включаются.
