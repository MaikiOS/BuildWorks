# .240 — ограниченное повторное использование памяти: эксперимент не закрыл дом

19.10.2026 · [English](CHECKPOINT_240.md)

## Результат

Дом/v11 не исправлен. Два запланированных48с эксперимента остановились по queue64, без снижения частоты/лимитов и без выбрасывания принятой истории. После второго одинакового отказа повторы остановлены. Normal/default QA .240 не включают кандидата: старый путь выделения/подтверждения сохранён,8 методов и handlers совпали с baseline после нормализации только генерируемых имён closure. Никакой новой визуальной приёмки нет.

## Что реализовано только как кандидат

`FogVelocityPages` ограничивает active+free массивы до capacity+resident (32+4), free<=resident. Отдельная lease запрещает поздней записи попадать в повторно использованный массив. Новый baseline обнуляется, staging полностью переписывается. Старый cold сохраняется до matching committed receipt и velocity; metadata/cached alias не возвращается дважды. Сбой удерживает обе страницы до прежнего GPU drain. Кандидат включается только `BuildWorksFogPageReuseCandidate=true` или Unity Editor. В обычной и установленной QA-сборках нет lease fields/Rent calls. Отдельная candidate DLL хранится локально и не установлена.

15 групп CPU-проверок actual-source охватывают предел, уникальность, нули, stale/double/cross-pool/thread writes, snapshots, удержание до drain и40000 смен страниц после прогрева без новых крупных массивов. В Unity GPU paging252 exact сравнений прошли; дополнительно проверены receipt-first/velocity-first, отказ receipt, metadata/cached, retirement и старые fault/lifecycle cases. Это доказывает ограниченный алгоритм владения, не48с производительность.

## Отказы настоящих GPU-прогонов

| Режим | Результат | Длительность / host FPS | Кадры | Страницы |
| --- | --- | --- | --- | --- |
| ReuseCandidate + прежний synchronous Capture | FAILED queue64 |25,676с /31,391 |91 |32 allocation,2489 reuse,157286400B |
| AsyncCaptureCandidate | FAILED queue64 |34,374с /35,929 |220 completed/221 requested |32 allocation,3429 reuse,157286400B |

Оба режима:32slots/32768blocks/Spatial3D/4resident,28 процедурных стен, прежний v11/30Hz input и clamp8, отсутствие explicit Update/postCapturePump. Из-за отказа140scalar/196field в этих48с прогонах **не достигнуты**.252 выше — отдельный успешный paging-тест.

В первом опыте CallbackCopy1,072с, Upload1,901с, ReadPixels4,386с; GC deltas[133,133,133]. Async-контроль почти исключил ReadPixels (40,02мс для первого reference), CPU copy35,91мс; GC deltas[209,209,209]. Это разные длительности/нагрузки, а не прямое сравнение ускорения. Дельты GC не суммировать и не выдавать за паузы; host FPS не есть GPU time или Valheim FPS.

Async recorder: один фиксированный960x540 target, максимум1request, `done` проверяется обычным кадром без Update/Wait; занятый target при следующем кадре означает FAILED, не пропуск. На cleanup ожидается собственный request до освобождения target. Первый кадр совпал побайтно с синхронным **RGBA32** чтением. Старый RGB24 и точная metadata времени каждого PNG не подтверждены; новая запись даёт другую фактическую частоту. Recorder находится только в Workbench, игровой ValheimQA recorder не менялся.221-й request был незавершён при остановке отказавшего опыта; он не объявлен сохранённым кадром.

## Сборки, установка и доказательства

Geometry, normal/QA/explicit candidate builds0/0, host Valheim1.0.20/Steam25909357, localization1122, QA separation, candidate IL contract и независимый correctness/Ponytail review прошли. Receipt gate8 режимов/1manifest/81отрицательный случай — CPU-only. Финальные Editor-исходники с guarded branch скомпилированы offline; два GPU-прогона выше относятся к экспериментальному телу до его изоляции, не к игровому default пути.

Первичная offline-команда ошиблась cwd (`CS2021`, пустой response file); затем другое имя output-сборки нарушило friend access (`CS0122 BuildWorksPlugin`). Исправлены пути и имя `Assembly-CSharp-Editor`; финальная компиляция0. Эти ошибки команд не выдаются за дефекты игры.

Установлена **default QA0.19.240**, кандидат выключен. QA SHA256 `19D8521FECBF78A79F3B305F59933132D15311D33EE41FDD052731A5F65B354D`; normal `F4B40C75B518349092261F9D74F7054DE94EFB06FFC404FE7D60B642838DD82F`; Geometry `4166DB0659711459BFEE0CFD17635D4287EEB84283DC9CD945774FB7EAF6D2E4`. Guarded deploy/hashes прошли. Valheim .240 не запускался, мост/миры/инвентарь/ресурсы не менялись; Unity завершилась с exit1 в обоих отрицательных опытах, настройки восстановлены. Новых ручных повторов пока не требуется.

Локальные доказательства:

- `artifacts/checkpoint-240/`: geometry/build/host/separation/localization logs, candidate-contract.json, normal-il.json, paging-gpu.json, оба failed GPU receipts, source-manifest, deploy и verification.
- `artifacts/fog-production-frames/ReuseCandidate-20261019-182439-52fc5a8d/`: inputs/frames/PNG/raw report/log/own PID exit.
- `artifacts/fog-production-frames/AsyncCaptureCandidate-20261019-183008-9c6d0c2a/`: то же, отдельно220 сохранённых кадров.
- `artifacts/fog-arena-physics/verification.json`, копия `checkpoint-240/paging-gpu.json`: успешные252 paging comparisons.

## Дальше

По уже собранным трассам разобрать throughput GPU paging/receipt и график обслуживания владельцев. Уменьшение выделений и неблокирующая запись не устранили отставание даже при35,93hostFPS. Новый длинный GPU-прогон делать только после конкретного изменения/проверяемой гипотезы. Не увеличивать очередь, не сбрасывать clock/историю и не объявлять отсутствие GPU-fault доказательством дома. Затем whole-house native-проверка через ValheimQA; оплаченная отмена/возврат, специальные объекты и сеть остаются открыты. Все14 блоков цели сохранены, Goal API active.
