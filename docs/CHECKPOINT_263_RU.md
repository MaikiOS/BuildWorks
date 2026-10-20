# Этап263 — общий скан отмены и честная граница фонового замера

20.10.2026. Исходники/установленная QA **0.19.250**. Туман/shader, правила оплаты и действующие single-set исполнители не менялись. Общая отмена через табличку ещё не подключена.

## Что изменено

Существующий reader теперь за один полный проход mapped chunks одного поколения проверяет две независимые группы: construction operations и payout IDs. Immutable `AbsenceMarkerSet`,50000/65536 IDs, разные ключи, отсутствие пустых/повторных ID; исходные массивы не удерживаются. Старый constructor/лимит50000/callers сохранены. У composite proof старые `MarkerKey/Operations` явно отказывают вместо молчаливого чтения первой группы. Host overload использует прежние worker/generation/custody/thread fences. Форматы планов/журналов не менялись; terminal-record и публичная оплаченная отмена не реализованы этим шагом.

## Автоматические проверки и установка

- Normal/QA .250:0warning/0error.
- Geometry suite PASS;597 saved-world assertions, включая новый composite corpus, обе группы на одном объекте/позднем chunk, неверные типы/данные, ограничения и копирование.
- ContentReservations PASS;195 generation/worker/custody/file assertions с host substitutes. Это не native save/reload .250.
- Host contract PASS;localization1122 ключа/1008 используемых, built resources PASS.
- Независимый source-review PASS. `verify` skill отсутствует; прямой review, builds и целевые тесты выполнены.
- Guarded deployment только TerrainRamp-1.0-Test, версия/пара DLL независимо сверены. Игра была закрыта.

QA BuildWorks SHA256 `A5608BDC767CB49F538276AC9499FA1F4A4E5F4F6D0894AE63D3EFC12DB6113E`; Geometry `0C27A7FBA028F97C065602B632A05BFD7B84AC9065F675600140CB8DE8CB8916`.

## Настоящий Valheim — прежняя .249

`ValheimQA/artifacts/20261020-195725/`:12setup+5close, два прохода2/3 через свой неоплаченный Forest Hut28. Наблюдатель52,046/52,073с:1875/1876 последовательных host frames;36,006/36,007FPS, по26двухсекундных окон36..36,5FPS;max40,83/35,13мс. Маршруты complete/no failure,28финальных полей восстановлены в0, инвентарь прежний.

**Это только simulation/host cadence.** Независимая проверка выявила `RenderFrames=0` до/после обоих интервалов: фоновая игра без Camera.Render сама не рисовала дом. Из этих чисел нельзя делать вывод о renderer cadence, плавности картинки или соответствии v11. Счётчики submit/callback не являются GPU execution timings. Ранее FAILED343/520 в[262](CHECKPOINT_262_RU.md) сохраняется; нового успешного полного видеозахвата нет.

Старые87project-файлов не изменились; только свой новый неоплаченный план отменён/revision2/checksum сверены.4141обычный save-файл: неизменны состав/размер/mtime, содержимое не хешировалось. Weather/time pins восстановлены; normal logout/quit,3DLL моста восстановлены. Grants/native construction/terrain/reset/другие профили/сеть не выполнялись. Эта игровая серия не объявляется проверкой новой .250.

## Следующий шаг

9.2–9.7: ограниченный render-only режим прежнего QA-наблюдателя через существующий Read(...,pixels:null), с реальным счётчиком отрисованных кадров и без PNG/CPU-передачи изображения. Затем визуальное сравнение v11; appearance остаётся открытой.8.7→8.8: использовать новый composite proof в долговечном terminal, проверить rollback/replay до действия таблички. Не заменять terminal двумя разновременными сканами и не удалять старые долги/receipts.

Локальные доказательства: `artifacts/checkpoint-263/`:native-verification.json,geometry-tests.log,content-tests.log,build-normal.log,build-qa.log,host.log,localization.log,deployment-verification.json,reviews.md. Живой список сохраняет14разделов/115ID/16частных галочек; более широкие требования не закрыты.
