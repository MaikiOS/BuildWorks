# .223 — пространственный возврат в игровом пуле

2026-10-18 · [English](CHECKPOINT_223.md) · [Все14 блоков](LIVE_CHECKLIST_RU.md)

QA **0.19.223.0** установлена только в TerrainRamp-1.0-Test; SHA `87DF0753C327F0A1904C9F56076AA1B4C48EEC78417D116F123BDCA01064031B`. Spatial3D подключён к QA InitializeBlueprint: длинная стена и тестовый дом. **Обычная стройка пока сохраняет прежний backend**; постоянный проект/колышек не реализованы этим этапом. Стоимость, права, source-domain/precision, транспорт дыма, архив v11 и embedded bundle не менялись. QA использует новый локальный candidate bundle, SHA `B8C5D64CA0836A3CE8B20B9426B55F2A2DE51D09B4DFCE4080C367A27515866C`.

## Что исправлено

[Ошибка222](FOG_RETURN_OWNERSHIP_222_RU.md): YZ-bin смешивал разные исходные X внутри одной детали и удерживал чужое прежнее повреждение. Добавлен неизменный для pool/generation layout: LegacyYZ1024 для точного эталона и Spatial3D16×32×32 для игрового кандидата. Layout проходит arena→staged state→simulation; dense oracle явно выбирает тот же Spatial3D. Живой owner между схемами не переключается; старые peak/away не переинтерпретируются. Карты/payload/velocity формат прежний, Ledger[2] остаётся одним флаговым uint.

Учтены owner и shared-stage buffers, allocation/validation, clear/stage/commit dispatch и FieldBytes. Старый bundle отвергается для Spatial3D **до выделения pool/GPU ресурсов**. Дополнительные ClearSpatialLedger/StageSpatialLedger/CommitSpatialLedger ищутся только у Spatial3D; прежние eager-списки и defaults сохранены. Порог768 оставлен прежним. Прямая GPU-проверка настоящего нового solver: источникA сохраняет0,5; другое место поX и контроль поZ восстанавливаются до0,458251953. Генерируемый прототип222 для этого больше не используется; текущий путь — FogReturnOwnershipCheck.RunRuntimeSpatial. Старый генератор222 относится к зафиксированному диагностическому опыту и не служит проверкой нового bundle.

## Автоматические и GPU проверки

- Normal/QA0warnings/0errors; Geometry132 группы с новым точным расчётом spatial reserve и invalid layout; host Valheim1.0.20/Steam25909357, localization1104/991, compiled isolation и QA-control прошли. `verify` skill отсутствует; выполнены прямые проверки и независимый correctness/Ponytail review PASSED/Ship to native QA.
- Реально выделенные28 owner buffers/7 resident/8192 blocks плюс общий stage/upload: **98 041 636 B**; сумма count×stride/точных texture extents совпала с reservation и FieldBytes. Это учтённые поля, не полный driver VRAM/process memory. Восьмой resident отвергнут, лимит98 304 000 сохранён, запас262 364 B.
- Все16384 слова away/peak stage/commit проверены, включая позиции после1023 и последний индекс. Rejected commit сохраняет owner state. Старый bundle capability guard проверен.
- Три полных16с цикла: обычная стена, yaw90, составная неплоская деталь. **168 точных сравнений** четырёх полей и трёх ledgers против standalone dense Spatial3D; контакт и конечный recovery flags0 действительно достигнуты. Это не вращательная инвариантность одинаковых карт шума и не проверка масштаба/наклона.
- Новый bundle в LegacyYZ: **270 ticks/18 exact field comparisons** с frozen v11, standingDamage1→recovered0; fractional input/catch-up/disabled emission/disposal проверены. Это сохраняет точный legacy oracle, **не доказывает одинаковый внешний вид Spatial3D**.

Build manifest учитывает28 базовых contract kernels; три spatial entry дополнительно проверены реальным полным буферным опытом. Неблокирующее ограничение: optional spatial entry пока проверяются на наличие, их64×1×1 размеры не проходят отдельный generic CheckKernel при загрузке. Конкретный bundle доказан полным16384-word тестом; перед принятием будущего варианта добавить условную проверку размеров.

## Настоящий Valheim

ValheimQA/GABS, прежние QA-мир/персонаж/cloud=false, batchMode/background. Подготовка **11/11 за5,4с**; четыре реальных прохода через одну8м стену из4 соседних native woodwall **66/66 за56,0с**, optional failures0, ошибок серии нет. Это длительности данных сценариев, не универсальный benchmark. Предупреждения подготовки относятся к двум необязательным backpack API. Новый strict bridge0ac107 применён к корректным сценариям; malformed-predicate отказы остаются compiled71 proof, отдельно в игре не проверялись.

| Направление | Tick удержания | Exact inner RGBA texel | Tick после выхода | Smoke/surface alpha и recovery flags |
|---|---:|---:|---:|---|
| Спереди | 2636 | 46656 | 2959 | Везде0 |
| Сзади | 2987 | 34112 | 3307 | Везде0 |
| Вдоль слева | 3405 | 135488 | 3754 | Везде0 |
| Вдоль справа | 3811 | 148928 | 4155 | Везде0 |

Всего **365 184 inner RGBA texel** совпали с reachable published payload. Во время каждого hold smoke/surface ненулевые. Все11 срезов сохраняют owner/generation и строго возрастающие sequence/tick/clock. Последующие свежие ticks4194/4231 подтвердили нулевой результат после последнего выхода. Порог0,001, фактически alpha/nonzero0. Читаются только флаги Ledger[2]; native Away/Peak целиком этой серией не сравнивались. Gutters/interpolation/final pixels этим не проверены.

Четыре записи Camera.main:83/74/98/85 кадров, всего340. Вход/удержание/выход/recovery присутствуют во всех; камера восстанавливается после каждого кадра. У обратных маршрутов1/3 отдельного approach нет — это не полное покрытие всех подготовительных фаз. Первый idle/previous-complete кадр до StartWallPass не засчитывается как маршрут. Оригинальные PNG/JSONL сохранены; VFR MP4 следует времени кадров, без интерполяции. Запись около6–7кадров/с **не FPS игры**.

Four-wall pool: field **82 383 460 B**, render после рендера **74 580 096 B** (до первого58 335 552). CPU-разбор срезов1,38–8,07мс; request→status148–252мс включает polling, не GPU timing. Инвентарь/рюкзаки прежние; после закрытия parts/owners/field/render/velocityRAM0. Рабочая мышь/клавиатура, выдача, native строительство, сброс мира, рельеф и удаление существ не использовались.

Logout→main_menu→Quit; PID56216 завершён штатно,3DLL моста hash-restored.4137 обычных файлов неизменны по размеру/exact DateTime ticks, не content hashes.134/143 protected paths сохранили hashes;9 штатных QA save/control изменений/ротаций. Cfg изменил только заголовок версии .221→.223. Коллектор первоначально не учёл idle side=-1, затем обратился к отсутствующему call у wait-step (KeyError); исправлены только чтение отчёта/учёт подготовительных кадров, игра не повторялась. Неверные вспомогательные пути/неподдерживаемый параметр QA-control не засчитаны как проверки; корректный вызов прошёл.

Локально: Default/ValheimQA/artifacts/20261018-154239 — сценарии, reports,11 полных metric JSON,4 MP4/PNG/JSONL, native receipt, audit/logs/restore. Unity — BuildWorks/artifacts/fog-spatial-ledger-223 и fog-legacy-spatial-bundle-223; source/installation — artifacts/checkpoint-223. SocratiCode: правильный root Default/green38174, collection codebase_fbb51d0bb614; graph не дал зависимостей SnapshotState, поэтому callers проверены прямым rg, без переиндексации. Игровые кадры/assets/бинарники не публикуются.

**Открыто:** изменение длительности/вида возврата при делении массы между3D bins (порог768 пока не нормализован); усреднённые исходные позиции всё ещё приближение; matched visual parity с утверждённым v11, масштабы/наклоны, FPS/GPU timing, дом28 при достаточном storage reserve, owner acceptance, постоянный проект/табличка/reload/сервер. Дом по-прежнему8192 blocks;32768 только four-wall. Следом — измерить canonical legacy/spatial recovery curves и согласованный вид длинной стены, затем дом; не выдавать успешный allocator/dense compare за художественную приёмку. Все14 блоков сохранены, Goal API active.
