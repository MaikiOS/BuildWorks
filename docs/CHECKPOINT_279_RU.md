# Этап279 — обычное обслуживание дома без подкачки

21.10.2026. Установлена QA **0.19.259**, SHA256 `EF42973670B768F673FD24B2B3BB347BA2A9C58FA536F0F4AAB0B1449A0AEBB6`. Это исправление обслуживания, не приёмка анимации v11. Табличка .255 сохранена лицом от дома.

## Изменение и цена

Общий field cap normal/QA — **256MiB**. Это прежний внутренний численный предел93,75MiB, а не отменённое пожелание владельца: цель требует ограниченной/измеренной памяти, фиксированного93,75MiB в ней нет. Новый предел позволяет обычному allocator держать32resident вместо4; резерв230134084байта, на132710400байт больше прежнего. Это не вся VRAM: отдельно остаются source/render/driver/объекты игры. Сохранены owner cap32, render cap128MiB, точные проверки allocation/максимальных текстур и отказ с сохранением состояния. Старый бюджет сохранён только QA-only флагом для воспроизведения FAILED278.

Проверенный grouped-path перенесён в normal PumpSources. Вне Pump прямой submit остаётся immediate; callback по-прежнему не коммитит — только PollSnapshots после точного owner/generation/stamp. Partial throw/failure/finally/retirement сохраняют guards и историю. QA explicit individual остаётся для сравнений. Default observer передаёт nullable mode=null и сверяет отсутствие QA override; счётчики доступны QA, не normal. Код solver/шейдер/качество/фиксированные шаги/очередь64/guard48/стоимость/права не менялись.

## Проверки

Normal/QA0warnings/0errors; Geometry всеPASS; host contract на Valheim build25909357 PASS; localization1122ключа/1008ссылок PASS. Unity actual receipt guards **35/35** без candidate define:28owners/slot31 делят один физический ответ, callback не коммитит, stale/duplicate/короткий ответ/ошибка создания/partial throw/paging/retirement/pendingDispose защищены. Добавлены проверки default cap/residency/mode и запрета смены режима после admission. Compiled Cecil contract подтверждает normal cap256MiB/groupedPump и отсутствие QA setter. Старые helper expectations редактора/candidate contract обновлены под новый normal cap; полный архивный GPU residency oracle не перезапускался, он не является доказательством этого этапа. `verify` skill отсутствует.

Независимый read-only обзор до запуска подтвердил source mirrors/обычный preprocessor и отсутствие лишнего scheduler. После native отдельно сверяются raw JSON и границы выводов.

## Настоящий Valheim

Run `Default/ValheimQA/artifacts/20261021-134519`, PID63820. TerrainRamp-1.0-Test, batch/Direct3D11, qaSaveRoot=true/cloud=false, существующие BW_QA_43d3719a/world1137745799, god/ghost, неподвижный персонаж. Прежний неоплаченный28частный дом190ab9eea6ad46e28993f1b66008f207. Новых проектов/построек/ресурсов/отмен/рельефных изменений нет.

Оба batch по10/10,0optional failures/warnings/errors; serialization210полных кадров с новыми memory scalars PASS. Сначала5с, затем52с только после чтения короткого raw отчёта. В обоих mode=default,forcedReceiptMode=false,32resident/32768blocks,256MiBcap/230134084reserve,VelocityUpload=false.

| Проверка | Короткое окно | Длинное окно |
| --- | --- | --- |
| Длительность |5,0017616с|52,0156528с|
| Production clock |+5,0234095с|+50,1357177с —96,386% wall time|
| Actual=controlled renders |113|1203|
| Наибольшая очередь по CPU-снимкам |9|10, конец6|
| Подкачки velocity / NoVictim за окно |0 /0|0 /0|
| Физические receipt requests за окно |26|267|
| Инвентарь |Не изменился|Не изменился|

Длинный отчёт содержит179полных CPU-records с28валидными источниками без fault; конечный max tick lag6. Средние23,13render calls/s — число вызовов в960×540RT, не GPU execution timing, не доказанная визуальная плавность и не обычное player-window разрешение. Сравнивать FPS разных погод как чистый A/B shader benchmark нельзя.

Учёт на конце: fieldBytes209948980 (~200,22MiB), sparse brick-render58335552 (~55,63MiB), velocity RAM0. Резерв выше фактических полей, поскольку четыреowner ещё не заняты. `Profiler.GetAllocatedMemoryForGraphicsDriver` вернул0 в меню и всех измерениях — показатель недоступен, не «нулевая VRAM». Полный пик graphics/source/driver и тяжёлый корпус открыты.

После Stop снят отдельный1280×720 `house-after-default-52s.png`, просмотрен: дом виден и табличка читается наружу; дождь. Снимок сделан после диагностической заморозки, не подтверждает проход/возврат формы/паритет с лабораторией. [Ручной осмотр .259](MANUAL_QA_279_RU.md) подготовлен.

## Параллельные ветки и завершение

Контактный агент нашёл существующий AttachProject→StartWallPass(0..3): настоящийPlayer.SetMoveDir,2с внутри/8с ожидания возврата, waypoint15с. Для сохранённого дома нужен тонкий AttachExisting QA-вход; нового контроллера/проекта не требуется. Сначала закрыть stationary receipt observer, иначе он законно остановит движение.8с сами по себе recovery не доказывают. Реализация/проверка этого среза следующая.

Отдельно уточнён paid-terminal seam CancelPaidSettled/store lock/существующий atomicCommit и CleanupChecks/PayoutInteractionHost. Это read-only план, не сделанный возврат/retirement. Локальный `PARALLEL_NEXT_SLICES_RU.md` сохраняет точки для обеих веток. EarthWorks/совместная табличка-хранилище/рельеф не изменялись.

Close/logout/main_menu/Quit штатно,0game processes,restore3/3. Все97project hashes совпали, ordinary-save состав/размер/mtime прежние, .cfgзначения и другие mod configs прежние. Ожидаемая QA-save запись при выходе сохранена, не откатана. .259 остаётся установленной; Default не является runtime target.

SocratiCode Default/codebase_fbb51d0bb614:green56495,watcheractive; без переиндексации. Прежняя нерелевантная OstrixMemory/ограниченная MemPalace история не заменяла текущий код и native raw. Факты этого этапа взяты из источников, compiled IL и тестов. Ошибки подготовки/ручного копирования пути кадра сохранены локально: первый copy не нашёл файл из-за лишней цифры в переписанном имени; точный путь взят из raw MCP, кадр затем прочитан. Сам CaptureAt был успешен, raw не переписан. При обновлении живого списка ошибка CRLF-разделения PowerShell временно заменила файл одной строкой текущей работы; guard документации остановил публикацию. Файл восстановлен из точного HEAD278, затем сравнен с committed исходником: все14разделов/115ID/check states прежние. Отклонённый текст сохранён в live-incomplete-rejected.txt. Этот дефект оболочки не затронул исходники мода или сохранения.

Доказательства: `artifacts/checkpoint-279` — SCENARIO,compiled-contract,receipt35/35verification,short/long-summary,полныеbatch/receipt/cadenceJSON,снимок,build/host/localization,projects-before/after,final-audit; указанный ValheimQA run — MCP/player/BepInEx/restore. Source-only Git исключает локальные shader267/271, DLL, изображения и сохранения. Цель active;14разделов/115ID и все широкие незавершённые требования сохранены.
