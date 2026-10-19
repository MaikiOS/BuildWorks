# Диагностика242–243: кандидаты не ускорили дом

19.10.2026 · [English](CHECKPOINT_243.md)

Игровая версия остаётся0.19.241. Ни один из этих опытов не исправил туман дома; новую игровую DLL ради изменения номера не устанавливаем. В Valheim на этом этапе не входили, сохранения и ресурсы не меняли. Все14 блоков цели остаются в живом списке; Goal API active.

## Что проверено

| Опыт | Проверка корректности | Нагрузка | Решение |
| --- | --- | --- | --- |
|242: два подтверждённых пакета до вытеснения velocity |9 случаев/210 точных полей, прежние252 paging-сравнения и38 lifecycle; найден и исправлен cold-owner deadlock |FAILED queue64 через21,283с;16466 подтверждённых ticks |Исходники опыта сохранены; кандидат убран из рабочего runtime |
|243: чередование трёх полей вместо копирования после каждого шага |6 случаев/84 точных поля;32 копии против104 на той же истории; исходные размеры, velocity predictor, canonical bindings и бюджет сохранены |FAILED queue64 через22,776с,35,871hostFPS |Архивирован; убран из рабочих исходников и испытательных fixtures |
|243: свежий контроль прежнего копирования |Те же28 procedural walls,32slots/32768blocks/Spatial3D/4resident; control=true в отчёте |FAILED queue64 через38,697с,35,920hostFPS |Ускорение нового пути не подтверждено; повторения остановлены |

В последних trace подтверждено17606ticks у кандидата и31047 у контроля; totals submitted выше и не подменяют подтверждённую работу. Время переполнения — результат отдельных запусков, не статистически надёжный benchmark и не игровой FPS. Средняя request→callback задержка receipt35,716мс против33,783мс; это **не время GPU-расчёта**.

## Попытка измерить настоящее время GPU

Старший архитектор проверил API в реальных Unity DLL и первичных источниках. Editor-only `CustomSampler.Create(name,true)` охватывает batch от BeginStep до FinishStep, до readback requests. Recorder сохраняет raw hostFrame/gpuFrame, число блоков, GPU nanoseconds и фактически отправленные batch/tick counts. Привязка по документированным трём кадрам принимается только при совпавшем числе batch; CPU Stopwatch не используется вместо GPU time.

Два20с опыта отказали с `GPU batch timing unavailable or frame association unconfirmed; raw frames retained.`:

- Recorder-only: compiled/support/samplerValid/recorderValid=true, но все GPU blocks/ns0.
- Отдельный GPU-area control: область действительно включена, все GPU blocks/ns снова0. Прежнее значение области восстановлено, `gpuAreaRestored=true`; глобальный Profiler не включали.

**GPU-время не измерено.** Наличие API и supportsGpuRecorder не доказывает получение timestamps в данном batchmode. Точная причина отсутствия данных в backend ещё не установлена; нули не выданы за низкую стоимость. Повторы этого пути остановлены. API: [CustomSampler](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Profiling.CustomSampler.Create.html), [GPU timestamps](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Profiling.Recorder-gpuElapsedNanoseconds.html).

## Изоляция и доказательства

Оба неудачных runtime-кандидата и timestamp probe архивированы локально. Сначала проверена полная изоляция кандидата через Unity Editor, затем все экспериментальные runtime/Workbench/fixture-изменения возвращены к проверенному710bcb6. В рабочем коде их больше нет; для исследования сохранён полный source snapshot. Исходный игровой цикл AdvanceTo и обработка подтверждений сохранены. Shader bundle неизменен: `E734631FB7DE49C00237C1F0F4DBB28DFA491DF6DF177C7D79353BE354C4AA78`.

Сборки normal/QA0 предупреждений/0 ошибок; повторный exact GPU gate6/84 и прежние252 paging/38 lifecycle PASS перед архивированием. CPU1775 отрицательных случаев; receipt gate8 режимов/96 отказов и положительные control/timing fixtures PASS. Host, localization1122, compiled QA save-isolation и сравнение IL8 методов/handlers + solver AdvanceTo PASS; эксперименты отсутствуют в normal/QA. Эти проверки не превращают failed48s/timestamp в PASS. Ошибочно вызванный `scripts/Test-TestStandSeparation.ps1` отсутствует; использован существующий `Test-QASaveIsolation.ps1`. Его первый запуск отказал на временной junction с «Отказано в доступе»; штатный повтор с доступом к тестовому каталогу прошёл.

- `artifacts/checkpoint-242/`: source manifest, exact correctness, воспроизведение deadlock и failed48s.
- `artifacts/checkpoint-243/`: кандидат и контроль, два timestamp-отказа, сборки, validators, IL и итоговая verification.json.
- Raw48s: `artifacts/fog-production-frames/AsyncCaptureCandidate-20261019-200425-317d3464/` и `AsyncCaptureCandidate-20261019-201037-228f7a01/`.
- Raw timestamps: `artifacts/fog-production-frames/TelemetryMeasurement-20261019-201539-dfbf7899/` и `TelemetryMeasurement-20261019-201838-53e62116/`.

Независимый read-only обзор корректности и сложности probe: blocking findings отсутствуют. Это не подтверждение работающего измерения или производительности. `verify` отсутствует; фактические проверки перечисляются в verification.json.

## Следующий проверяемый срез

Не подменять игру стоимостью видеозаписи: проверить настоящий дом через ValheimQA отдельно от непрерывной записи, с состоянием очереди/восстановления и редкими контрольными кадрами. Это диагностическое разделение, **не отмена проверки плавности/вида** и не разрешение ослабить48с gate. Для записи и низкой частоты кадров ограничитель остаётся открытым. После стабильного дома — оплаченная отмена и физический возврат; подготовку земли не начинать.
