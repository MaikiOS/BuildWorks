# 305 — точный возврат в любом порядке

22.10.2026. Установлена **QA0.19.273**, только TerrainRamp-1.0-Test.
SHA BuildWorks.dll: `2A5F8CBAB7D540592BDB0F8F2712913D6FDFE772B9AAAFEEEF5AFA029155CBD4`.
[Полная цель](GOAL_FULL_RU.md), [живой список](LIVE_CHECKLIST_RU.md), [English](CHECKPOINT_305.md).

## Что исправлено

На304 верхний предмет оказался второй строкой возврата, а журнал разрешал получать
только первую. Обычный E отказывал с ложным reconciliation. Теперь игрок получает
именно наведённый предмет в любом порядке. Физическое перекрытие не менялось.

Collections остаётся массивом по неизменным индексам Items. Пустая позиция означает,
что получение этой строки не начиналось. Новый reader читает старые prefix-записи
без миграции или записи. .272 отвергает sparse-файл; автоматический downgrade
не поддерживается и потребует отдельной проверки данных.

Сохранены точные item marker/blob/properties, уникальный источник, payer/world,
дистанция/native hover, custody, permanent profile receipt, exact inventory delta
и paired save/generation. Допускается только один незавершённый credit и одна
cleanup-операция, без пересечения. Пустую позицию нельзя подтвердить/очистить;
повтор заполненной не вставляет предмет. Cleanup и finalizer пропускают пустые
позиции и не требуют квитанцию неполученного предмета. Failure очищается после
принятого нового collection. QA exactCreditedInventory сравнивает только с
ProfileCredited и больше не считает последний индекс хронологически последним.

Независимый обзор нашёл пропущенный ранний guard: другой pending credit мог
дойти до baseline profile save перед отказом BeginCredit. Исправлено до запуска
игры: constructor отказывает до snapshot/scans; journal повторно проверяет перед
записью. Нельзя называть финальный отказ достаточной защитой от раннего save.

## Автоматические проверки

- Старый source RED: `artifacts/checkpoint-305/payout-before.log`.
- Payout journal200: обратный порядок/reload/cleanup, exact свойства/квитанции,
  отрицательные подтверждения, повтор, checksum-valid malformed sparse records:
  all-null с правильной revision7, два pending с revision9, overlap с revision11,
  неверные identity/revision. Отказ не переписывает файл.
- Cancellation64, actual admission7 и guard-removal negative control,
  interaction63, finalizer57, board input16, board router30 PASS.
- Normal/QA builds:0errors/0warnings; HostContract, geometry suite,
  localization1127 PASS. Admission включён в HostContract.
- Независимые correctness и Ponytail reviews PASS после исправлений.
  verify skill отсутствует; его проверка не заявляется.

## Настоящий Valheim

Существующие QA-мир/персонаж BW_QA_43d3719a, world UID1137745799;
cloud=false/qaSaveRoot=true проверены. Запуски142218/143123/143544,
PID43308/57920/50652. Новый проект `9d2568de03664632ac211e673fa1db2d`
из существующего двухстенного чертежа; старые проекты не восстанавливались.

1. Native ordinary plan confirmation → E к табличке → реальная кнопка Continue:
   две committed стены,4Wood67→63.
2. E и два подтверждения отмены: removed-saved,63Wood, прежние native drops.
3. Отдельные E/два подтверждения выдачи: issued-saved/terminal, табличка убрана,
   два exact sealed источника,63Wood.
4. Обычное наведение на верхнюю вторую строку
   `ee14fc4f991f43f5b45d4fd724b8855f`, E:65Wood/7receipts/CleanupSaved;
   строка0 пустая, её exact source сохранён. Штатный logout/menu/Quit.
5. Новый процесс57920:9/9,65Wood/7receipts/один exact source/пустая строка0,
  115project SHA без изменений. E к оставшемуся
   `6ca6341b953144c580a2d46ce28c70c0`:67Wood/8receipts/обаCleanupSaved/0sources.
   Штатный logout/menu/Quit.
6. Новый процесс50652:9/9, exact27inventory records/67Wood/8receipts,
   terminal/без таблички/0sources,115project SHA прежние. Существующий Hammer
   снова экипирован; штатный logout/menu/Quit.

11PASS-пакетов/59шагов; полные отчёты и виртуальный ввод сохранены в
`artifacts/checkpoint-305`. Базовое подтверждение плана использует QA helper
реального Player.UpdatePlacement; действия таблички/получения — виртуальный E
и pointer по наблюдённым UI targets. Это не физическая мышь владельца.
Aim helper временно меняет вид/положение QA-игрока; до E проверялся точный hover ID.

Независимый .fch parser проверил SHA512 и точные AfterBytes для sparse index1
и итогового index0. Оригиналы профилей сохранены локально в sparse-profile/final-profile.
Production custom-map reader отдельно подтвердил7/8квитанций; он сам по себе
не доказывает байты инвентаря. Последняя экипировка Hammer сделана после этих
inventory proof и финального сравнения27записей; прежнюю побайтную экипировку
после неё не заявляем.

Аудит:109старых project SHA прежние,6новых файлов только своего проекта,
115settled SHA прежние после запуска/выхода.4141обычных файлов сохранений:
неизменны membership/size/mtime, полные хеши не снимались. Effective cfg прежний.
Три нормальных выхода/три восстановления по3DLL с проверкой хешей; процессов
QA не осталось. Нет grants/reset/terrain edits/сетевой сессии. Мост при следующем
нужном прогоне снова готовить автоматически.

## Открыто

Полная цель не завершена: разные свойства/типы возврата, прерывание в значимых
save/credit/cleanup окнах, старые миры/rollback, secondary collapse, owner UX,
host+remote и остальные14разделов. Следующий срез8.7–8.8 — свойства предметов
и точечный recovery. Отдельный дефект: HaveRequirements выдаёт отсутствие верстака
за нехватку ресурсов. Surface303 и Confirmed/Draft EarthWorks не менялись.
Новый sparse-журнал не передавать .272 при слепом откате.
