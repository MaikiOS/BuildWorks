# .236 — архив позы и слотов стойки брони

2026-10-19 · Исходники/сборки .236; установлена QA .233. Это законченный этап **сохранения данных**, а не завершённое восстановление ArmorStand.

## Изменение

World-selection capture теперь использует отдельный `BlueprintArmorStandCapture`: фактический private `m_nview`, fallback позы `m_pose`, упорядоченные native ключи `i_item`, `i_variant`, `i_itemData`. `m_startPose` не используется. Перепроверяются identity view/ZDO, revision, очередь прикрепления, поза, список и дескрипторы слотов. Чтение не меняет мир, инвентарь или права.

`valheim-armorstand-v1` хранит pose и count в Info; собственный binary envelope1 — упорядоченные VisSlot/ItemId/nullable prefab/variant/nullable exact package. Это не сериализованный ZDO и не инструкция его загрузки. Максимум64 слота,128 символов имени,24576 байтов item packet,1MiB envelope; document32MiB остаётся прежним. UTF-8/Base64/длины/полный EOF проверяются. Пакеты неизменяемы; null и empty различаются.

ID0 означает неактивную запись даже при оставшихся bytes. Неизвестные hash/версии/атрибуты сохраняются как архив, не становятся вещами. Известный активный prefab включается в зависимости по согласованному ID/имени независимо от понимания package. Для v109/110 повторно используется read-only item parser; несогласованные/непонятные записи считаются отдельно.

Локальная библиотека25, portable14; старые1–24/1–13 по-прежнему читаются, но новый discriminator под старой объявленной версией отвергается без перезаписи. Старые версии плагина не читают новые документы. Отключение экипировки сохраняет pose и bytes. Прежнее явное исключение внешнего PlanBuild ArmorStand не применяется к native archive.

## Доказательства

- normal/QA Release:0 предупреждений,0 ошибок; текущий host1.0.20 / Steam25909357 / SHA94998885…F3A.
- Полные Geometry и Store suites PASS, включая round-trip local25/portable14, запрет старых объявленных версий, opaque known dependency, clear/archive и editor transform/Undo/Redo.
-28 CPU-проверок **фактических capture sources** на минимальном read-side fake host: empty/full, cloned bytes, stale/unknown slots, busy queue, ZDO/revision/view/layout/pose races и отсутствие записи. Это не Unity/native capture.
- HostContract, localization1122 и compiled QA/normal separation PASS.
- Workbench RuntimeHost и Editor с новым adapter скомпилированы офлайн Unity Roslyn, без запуска Unity/GPU. Это не визуальная проверка; runtime mirror переводов синхронизирован.
- Два независимых source review PASSED; найденный dependency gap исправлен и покрыт regression.

Локальные receipts: `artifacts/checkpoint-236/{verification.json,capture-checks.json,geometry.log,store.log,build-normal.log,build-qa.log,host-contract.log,localization.log,qa-separation.log,review.md,source-manifest.json}`; offline compile — одноимённая подпапка. Неполный первый compile на старом Workbench host показал недостающие stub-поля; read-side shape обновлена, финальная компиляция прошла. Проверки версий обновлены вместе со схемой; ошибки ожиданий не считались PASS.

## Пределы и следующая работа

Поза/экипировка не отображаются и не восстанавливаются. Exchange/editor явно сообщают об этом; размещение закрыто также при отключённой экипировке, для обычного игрока и администратора. Неизвестные mod-specific поля вне сохранённых native slots не объявлены поддержанными. Конечный native capture/reopen/preview/строительство/стоимость/reload/сеть требуют отдельного прогона и адаптера.

Игра Ostrix не прерывалась; Valheim/GPU не запускались, установленная QA .233 не менялась. Deploy script запрещает замену при работающем Valheim. `artifacts/BuildWorks-0.19.236-manual.zip` — обычные две DLL для добровольной ручной установки после закрытия игры, не публичный релиз.

При освобождении игры: установить проверенную актуальную QA через штатный Deploy, подготовить ValheimQA по runbook. **Первый приоритет остаётся fog PollingCandidate**, затем исправление подтверждённой причины очереди и native дом; оплаченная отмена/возврат и остальные14 блоков цели остаются открыты. Никакого повторения прежнего failed-house без изменения причины.
