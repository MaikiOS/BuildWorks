# Этап278 — ordinary-budget проверка остановлена подкачкой

21.10.2026. Это диагностический результат **FAILED**, не исправление обычного тумана. Табличка .255 по-прежнему лицом от здания, у земли перед постройкой.

## Изменение

QA-only MSBuild `BuildWorksFogOrdinaryBudget=true` включает точный normal-лимит98 304 000байт без residency/page-reuse кандидатов и без Editor. Опасные сочетания отклоняются в MSBuild и preprocessor. Сам allocator не заменён:32логических владельца/32768blocks автоматически дают4resident,97 423 684байта резерва и VelocityUpload=true. Наблюдатель сверяет эти фактические значения; сериализация по-прежнему проверяется210полными кадрами. Normal-поведение/качество/стоимость/очередь64 и диагностический guard48 не менялись.

## Настоящий Valheim

Run `Default/ValheimQA/artifacts/20261021-131945`, PID63764, TerrainRamp-1.0-Test, batch/Direct3D11, qaSaveRoot=true/cloud=false. Существующие BW_QA_43d3719a/world1137745799; god/ghost, персонаж неподвижен. Прежний неоплаченный28-частный дом190ab9eea6ad46e28993f1b66008f207. Нет строительства, выдачи/расхода, отмены/переходов плана, сбросов или рельефных изменений.

Пакет выполнил4шага и остановился на ожидании complete: report.state=failed. Это не4/4 успешная проверка механики. Полный batch: `fog-receipt-ordinary-short-278-20261021-162403.json`; полный внутренний отчёт: `fog-receipt-arm-5105dc166cee42ec8bc54c9b473fbed5.json`.

| Проверка | Результат |
| --- | --- |
| Требуемое окно |5с, не завершено; длинные52с не запускались|
| До остановки |1,7470967с;62фактических=62контролируемых render|
| Production clock |+1,81332097с; не доказательство обслуживания всех владельцев|
| Очередь |48при guard48, без source fault; худший confirmedTick8/capturedTick57|
| Velocity |За окно56запросов/275 251 200байт; накопленно60/294 912 000,56callbacks|
| Подкачка |52restore/upload/release цикла; NoVictim+2976за окно,3024накопленно|
| Инвентарь |Не изменился|

Причина подтверждена кодом и счётчиками: каждый неметадатный batch запрашивает полную velocity texture4 915 200байт; pending блокирует следующий batch. Всего4resident вынуждают постоянно передавать слот холодному владельцу, выгружать и восстанавливать поле. Несколько ticks уже пакетируются — среднее7ticks/batch в последнем периодическом sample. NoVictim означает отсутствие пригодного слота в конкретной попытке, а не потерю детали. Grouped receipts сами по себе это ограничение не устраняют. Отдельное прежнее PASS277 с32resident не отменено, но на обычный бюджет не распространяется.

После guard наблюдатель заморозил симуляцию; Cadence продолжил рисовать замороженное состояние до Stop. Его общие1975render не считать производительностью исправного тумана. Снимок/анимация/проход не выполнялись. Длинный и повторный неизменённый прогон намеренно не запускались. Независимый архитектурный разбор не нашёл утверждённого численного лимита93,75MiB: это прежний внутренний cap. Следующий минимальный срез — явный общий field-cap256MiB/32resident вместо нового scheduler: +132 710 400байт резерва полей; не считать этот cap общей VRAM, отдельно остаются render/source/driver. Сохранить owner/render/allocation guards, проверить default/проход/total memory и освобождение; ordinary-бюджет в278не повышен. Следующий шаг — решение resident-памяти/подкачки с явной ценой памяти; новый scheduler без доказанной справедливости не выдавать за простой готовый фикс.

## Проверки и установка

Normal .258, QAordinary .258 и восстановленная стандартная QA .258:0warnings/0errors. Три отрицательные сборки правильно отказали:ordinary безQA;ordinary+GPUresident;ordinary+pageReuse. Host contract/localization1122keys/1008refs PASS. Native210-frame serialization preflight PASS. `verify` skill отсутствует; Geometry не повторялась, поскольку изменены только диагностические compile flags/observer/версия, не геометрия. Независимый read-only разбор подтвердил parity и проверил причину; простое увеличение batching уже существующей очереди отвергнуто.

Экспериментальный ordinary-budget DLL SHA `E9F2A42A877A7231A58390D9C467134413ECE516A5B1F6881B0B96BBC4E8FE0F` после теста снят. Установлена **.258 стандартная QA**,256MiB/resident32,ordinary flag=false,default individual. SHA `3433B2D1B0EAB2CFB6CD7704E001ACAFC8F5649EE360C5CF2EC057BB7A82B302`; это не готовая сборка для владельческого прохождения дома. Обычный normal-бюджет не повышен, grouped-default не включён.

Штатные Close/logout/main_menu/Quit,0game processes,restore3/3. Все97файлов проектов совпали поSHA; ordinary-save состав/размер/mtime прежние. Значения .cfg прежние; изменился комментарий версии BuildWorks. Девять QA-save/control файлов ожидаемо записаны при обычном выходе, не сброшены. Первичная широкая проверка «config» захватила эти save/control файлы; её исходный audit сохранён отдельно, затем область проверки разделена.

Неудачи оболочки сохранены в `SCENARIO_RU.md`: позиционный Select-String, устаревший LASTEXITCODE после ожидаемого отрицательного build, git status вне Git checkout и Windows rg с буквальной glob-позицией. Первая Git-сборка также не стартовала: Out-File отказал из-за ошибочного пути BuildWorks/checkpoint-278/git-build-normal.log; путь исправлен на artifacts/checkpoint-278, обе Git-сборки и host/localization прошли. Final audit сначала строго отказал Unexpected save/config metadata change: broad scan включил QA-save/control и комментарий версии; значения .cfg сверены отдельно и прежние. Повторная проверка установила конкретные причины; сам build/deploy не объявлялись сломанными по ошибке обёртки. Индекс SocratiCode Default/codebase_fbb51d0bb614 проверен green56244/watcher active; focused semantic query не дал результатов, переиндексация не запускалась. Код и решения опираются на текущие исходники/нативный JSON.

## Осталось

Исправить обычное обслуживание без скрытого снятия memory/queue/quality ограничений; затем контакт/восстановление/v11, physicalE, платная достройка/отмена/crash/reload, тяжёлый корпус и сеть. Параллельный read-only paid-terminal план277 сохранён, переходы ещё не реализованы. EarthWorks/ресурсное хранилище/рельеф не менялись. Цель active; все14разделов и115ID живого списка сохранены, широкие требования не закрыты.

Доказательства: `artifacts/checkpoint-278/SCENARIO_RU.md`,short-summary.json,полныеbatch/receipt/cadenceJSON,build/guard/deploy logs,projects-before/after,final-audit.json и указанный ValheimQA run. Source-only Git без shader267/271, DLL, изображений/сохранений.
