# Этап249 — запись прохода через GPU-дом без прежней остановки PNG

20.10.2026. **Ограниченный успех рекордера и cadence; модуль и внешний вид v11 не приняты.** Изменён общий диагностический ValheimQA/render-probe, не shader, расчёт тумана, стоимость или сетевой протокол BuildWorks. Версия игрового кода остаётся0.19.241; кандидат этапа247 с256МиБ временно установлен только в TerrainRamp-1.0-Test.

## Что изменено

Вместо Camera.Render → ReadPixels → PNG → disk в основном потоке: рендер на Unity-потоке, до двух AsyncGPUReadback в порядке кадров, два фиксированных raw-слота, один последовательный worker для RGB/PNG/файлов. Первый контрольный кадр остаётся синхронным; RGB async и декодированный PNG точно совпали с его GetPixels32, включая alpha255. Внутренний TextureFormat декодера не подменяет проверку содержимого.

Только frame-limit с полными requested/submitted/readback/written и точными контрольными сравнениями даёт успех. Ранний Stop, пропущенный deadline, backpressure, ошибка или timeout дают failed. Finish/Stop не освобождают target до завершения GPU/worker; Unity synchronization context продолжает drain после уничтожения компонента. Одноразовые QA-задержка и удаление при реальном overlap проверяют эти ветви, не меняя игровые права или ввод.

## Фактические проверки

| Проверка | Доказательство | Граница |
| --- | --- | --- |
| Сборка render-probe | 0 предупреждений/0 ошибок | net48, ссылки текущего Unity-хоста, netstandard facade; не запуск игры |
| Writer/QA CPU | 21 checks +6 unittest | порядок, bounded bytes, ошибки, concurrency; без GPU |
| Первый кадр | 5/5, raw и decoded PNG exact | 960×540, все RGB/alpha; только первый контрольный кадр |
| Ранний Stop | 7/7 | incomplete520 даёт failed и release |
| Destroy с delayed worker | 8/8 | worker6с, drain5с timeout, затем context завершает release без owner LateUpdate |
| Destroy с двумя незавершёнными GPU-заявками | 8/8 | до Finish зафиксированы2, после drain pending0/resourcesReleased=true |
| Запись прохода | 72/72;520/520 PNG | 451137465B, фактический span51,89869с; ни одного потерянного/переставленного кадра |
| Отдельный cadence | 1881frames/52,28433с/35,95724FPS | неизменный48–65с/30–40FPS gate;26окон35,5–36,5; max76,98мс/p9530,59мс |
| GPU-состояние дома |4 snapshots valid; queue1,1,2,1 | velocity requested/CPU bytes0; это не GPU execution timing |
| Контакт/восстановление |24/24;9active parts/105344 inner texels | отдельно от cadence; все28 частей затем smoke/surface/pages/nonzero/max/flags0 |
| Закрытие |5/5, logout/Quit, свой PID исчез | все3 прогона завершены штатно; мост каждый раз3/3 |

RawBufferBytes4147200 — только два RGBA-массива, не общий RAM: есть отдельный RGB worker buffer, первый эталон, PNG/decoder и Unity/driver allocations. PNG-файлы ограничены512МиБ; до старта нужно1ГиБ свободного диска. Размер/частота/520кадров не сокращались ради PASS.

В новой записи mean render5,27мс, main-thread copy0,13мс, worker PNG24,11мс, image write1,00мс. Readback latency75,34мс — задержка до получения данных, не время исполнения GPU. Новое captureMilliseconds=render+copy, поэтому его нельзя напрямую сравнивать с прежними70,28мс (render/readback/encode/disk вместе). Сопоставимое внешнее наблюдение: раньше запись дала25,38FPS, теперь35,96FPS при той же целевой частоте36. Max76,98мс не позволяет объявить анимацию идеально плавной.

## Неудачные прогоны не скрыты

- 20261020-102545: exact raw RGB прошёл, но ошибочное требование decoded.format==RGB24 отвергло настоящий RGB PNG960×540; тот же guard мешал delayed-destroy. Переписано на точный GetPixels32 с каноническим эталоном. Оба FAILED сохранены.
- 20261020-103246: короткие проверки прошли, полная запись FAILED через4,1088с:40/520 PNG,41readbacks, backpressure. Mean encode22,31мс/readback latency75,64мс. Причина — запрет второго запроса при свободном втором слоте. Исправлено владение заявкой в каждом из прежних двух слотов; очередь/частота/критерии не ослаблены. Исходный cadence observer достиг65с limit и восстановил настройки; это не PASS failed-записи.
- Внешний audit сначала отказал ValueError на PowerShell ISO timestamps с7, затем5 дробными знаками. Дробная часть приведена к6 для Python3.10;4 представительных self-check прошли, прежний допуск сравненияmtime1мс не менялся. Финальный auditPASS только после полного повторения.

## Восстановление и границы

Inventory projection и собственные inventoryUnchanged-assertions неизменны. Три собственных неоплаченных проекта имеют проверенные world/revision2/cancel-operation/checksum tombstones. В config изменились только QA saves и эти собственные cancelled-записи/locks. Обычные saves: прежние4137путей/размеров/mtime, полные content hashes не собирались. Никаких новых ресурсов/native-построек/рельефа/каталогов/рабочего ввода.

В отличие от этапа248, восстановлены именно фактически стоявшие перед опытом bytes обеих DLL: BuildWorks81CE055050890A8DACC8A0B6F3458E38A7271055DE77AA811B2188C2FA814226; Geometry4166DB0659711459BFEE0CFD17635D4287EEB84283DC9CD945774FB7EAF6D2E4. Guarded deploy и source artifacts также восстановлены; GPU-кандидат не оставлен установленным. Общий новый рекордер сохранён как исходник стенда и будет использоваться при следующем QA.

Кадры hold/complete просмотрены, VFR MP4 сохраняет настоящие интервалы. Это один дом, одна ось в двух направлениях между отдельными сценариями; не все повороты/масштабы/тяжёлые библиотеки. Сопоставление анимации с утверждённой лабораторией и приёмка Ostrix открыты. Одновременный writer exception+pending GPU, окончательная остановка player loop, device loss, весь HUD, платная отмена и сеть здесь не проверены.

Артефакты: ../ValheimQA/artifacts/20261020-104111/verification.json,recording-summary.json,cadence-raw.json,frames.jsonl,recording.json,contact-summary/metrics,полные reports,house-pass.mp4,selectedPNG,логи,restore.json,probe-source иprobe-build-sha.json. Два failed run-каталога сохранены рядом. Независимые source/ponytail/native обзоры: artifacts/checkpoint-249/. verify skill отсутствует; выполнены сборка, прямые проверки и обзоры.

Все14блоков цели сохранены; Goal APIactive, objective не редактировался. Следом принять bounded GPU-бюджет в рабочую QA-сборку и сопоставить внешний вид; затем реализация оплаченной отмены/возврата. Параллельный read-only обзор выбрал реальное удаление Prepared→RemovalIntent→RemovedSaved без выдачи; это только план, полный refund ещё не реализован.
