# Диагностический этап238 — throughput тумана дома

19.10.2026 · [English](CHECKPOINT_238.md)

Игровая версия остаётся0.19.237. Это завершённый диагностический опыт с отрицательным результатом, не исправление тумана дома. Переноса в production и новой установки нет.

| Изменённый опыт | Результат |
| --- | --- |
| Явный polling до прежних Pump | FAILED queue64 на34,15с/30,72FPS;140 кадров |
| Дополнительно polling+один Pump после каждого Capture | FAILED queue64 на35,05с/29,24FPS;135 кадров и135 дополнительных Pump |
| Точное сравнение с dense reference | Не достигнуто:0scalar/0field, не PASS |
| Проверка отчётов | PASS5режимов/1manifest/48negative; синтетическая CPU-проверка validator |
| Независимый correctness/ponytail обзор | PASSED/Ship только для опыта |

Новый режим `PostCaptureCandidate` отделён от прежнего `PollingCandidate`; флаг и число дополнительных вызовов проверяются. История входов, solver, shader/bundle,32slots/32768blocks/Spatial3D/4resident и границы140scalar/196field/48с/drain5с не менялись. Во втором опыте все28 владельцев продолжают продвигаться: последняя группа ticks971–979,4resident. Это общее отставание обработки, не остановка одной детали. Разница времени отказа не является доказанным ускорением runtime.

Повторы после двух эквивалентных queue64-отказов остановлены. Прежний native-дом не запускался заново. Unity PID53564 завершён с exit1 по отказу сценария; настройки восстановлены runner после выхода. Игровые сохранения, каталог, ресурсы, правила оплаты и установленная пара DLL этим опытом не менялись. Все14 блоков цели сохранены, goal active.

## Следующее измерение до выбора исправления

Нужны фактические batches/samples/ticks и отказы допуска; отдельно submitted→callback→accepted для receipt и velocity, bytes/copy time, ожидание eviction, Create/Release/upload CPU-время и Capture Render/ReadPixels/PNG/диск. Stopwatch вокруг Dispatch показывает CPU-постановку, не GPU-время. Вызовы Update1833+1833 не означают завершённые transfers. Записывать ограниченные накопительные counters на32slots, не строку на каждый dispatch.

В исходниках есть проверяемая гипотеза: texture velocity уничтожается при page-out и создаётся при восстановлении. Один payload —4915200байт. Reuse четырёх textures может уменьшить эти расходы, но причина ещё не измерена. До изменения владения нужны exact-field oracle, уникальный lease/return после подтверждённой выгрузки, late reply/retirement/fault/dispose и учёт свободных textures в FieldBytes. Нельзя получить4занятых+ещё4свободных сверх прежнего бюджета.

Успех post-capture фазы всё равно не был бы доказательством production camera hook: синхронные ReadPixels/PNG/диск проходят позже OnRenderImage. Дополнительный capture в мод ради обхода не добавлять. Production-обслуживание обязано учитывать все snapshotSlots, включая retiring, а не только видимые части.

Локальные доказательства: `artifacts/checkpoint-238/`; полные отчёты/логи/frames — `artifacts/fog-production-frames/PollingCandidate-20261019-172119-b06faa2a/` и `PostCaptureCandidate-20261019-173856-b5c809e5/`. Исходники игрового тумана и его bundle неизменны относительно .237. Лабораторная геометрия процедурная, не native-дом; внешний вид/FPS/сеть этим опытом не приняты.
