# Диагностический этап234: очередь v11

19.10.2026 · [English](CHECKPOINT_234.md)

**Игровая версия остаётся0.19.233.** Отказ воспроизведён в Unity Editor;
нового runtime-исправления, установки или бинарного релиза нет.

| Условие | Фактический результат |
| --- | --- |
| Четыре сочетания контакта/записи, без cap | 4PASS,126–157FPS, queue≤8;784field/560scalar |
| Контакт+запись, cap36,22с | PASS196/140;31,76FPS, queue57, drain2,27с; не устойчивость |
| Контакт+запись, cap36, до48с | FAILED на22,13с/31,67FPS, queue64; сравнения не выполнялись |
| Контакт без записи, cap36,48с | PASS196/140;35,98FPS, queue15 |
| Запись без контакта, cap36, до48с | FAILED на26,08с/31,70FPS, queue64; сравнения не выполнялись |

Production-конфигурация32slots/32768blocks/Spatial3D/4resident,28 процедурных частей.
Все владельцы продвигаются, но накапливают отставание при записи. Контакт не нужен
для отказа; starvation не подтверждён. Чистое GPU время не измерено:
захват960×540 включает Render/readback/PNG/диск. Это не native геометрия дома.

Подготовлен **только test-only** `PollingCandidate`: явный неблокирующий
`request.Update()` перед прежними Pump. История, качество, лимиты и batch сохранены.
Код production/оплаты/сохранений не затронут. **GPU-кандидат не запускался.**

`scripts/Test-FogProductionFrames.ps1` проверяет runID/свежесть/scope/часы/FPS,
точные140scalar/196field, пары сценариев, capture/polling и source/bundle hashes.
Exit0 с failed-отчётом не считается успехом. Собственный живой процесс не убивается;
настройки восстанавливаются только после выхода. Отдельные каталоги сохраняют ошибки.
CPU suite:4 допустимых режима,1manifest и41 отказ; это синтетические входы validator.
Вся Editor assembly собрана offline штатным Unity Roslyn, exit0, без Unity/Valheim.
Независимые read-only обзоры PASSED для диагностики, не для готовности тумана дома.

Локальные доказательства: `artifacts/checkpoint-234/{production-frames,
native-cadence-22s,native-cadence,native-controls,offline-compile}` и
`artifacts/fog-evidence-fixtures/production-ab53c19e27464a3fa158ceced2a99666`.
Начальный failed-run с exit0 сохранён; последний controls-run PID51540 вернул exit1.

Ostrix играет в своём профиле: новые GPU/Valheim проверки отложены, CPU-работа
продолжается. Следом один изменённый capped-кандидат; только после доказательства —
минимальный runtime-перенос, поздние replies/retirement и настоящий Forest Hut.
Дом/v11, оплаченная отмена, специальные объекты и сеть открыты. Все14 блоков цели
сохранены; goal API active. Полный модуль этим этапом не завершён.
