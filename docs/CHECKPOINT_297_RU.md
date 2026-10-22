# 297 — согласованная выборка глубины прошла локальные GPU-проверки

22.10.2026. Новый изолированный кандидат, **не установлен и не принят как исправление v11**. Обычные shader/include/bundle и установленная QA .267 прежние.

## Изменение

Оба прохода вычисляют один integer full-depth texel из integer low pixel и читают его центр. Low pass передаёт готовую глубину интегратору; ray UV/jitter, S/T weights, plane tolerances, exact raw equality и прежний full fallback сохранены. Рабочий include не менялся: отдельная локальная копия отличается overload и wrapper. S/T адресуются через проверенные размеры xy, без зависимости от дублируемого reciprocal zw. Новых RT, настроек или контроллера нет.

Первый GPU-прогон выполнил assertions, но был отклонён compiler gate: `integer divides may be much slower, try using uints if possible`. Исправление — unsigned integer division той же формулы; guards исключают overflow. Неудачный лог/источники сохранены, нулевой допуск к compiler messages не ослаблялся.

## Что доказано

- **75 GPU assertions**, compiler0: analytic plane на8×8/9×9/3440×1440, Point/Bilinear, прежние S/T weights/source alpha, sky/thin foreground/nonplanar/clamped border/invalid dimensions. Native-size здесь — размер синтетической текстуры, не запуск Valheim.
- Настоящий low integration с published GPU buffers и smoke texture на8×8: fog непустой; выбранные foreground texels отсекают его; тонкое препятствие в полном пикселе включает прежний full fallback без протекания. Синтетические depth/state не равны всей игровой сцене.
- Linear/DX11 bundle собран и дважды перезагружен, ровно2shader, messages0. SHA256 `B652698B12B40899745AF8550636D7691222BCF225557F9BC6A3830FCEBB6D7F`.
- Длинная стена:4направления/20кадров/48source comparisons, Gamma960×540. Empty/recovered RGB точны; на остальных кадрах max mean delta0,01831/255, max channel delta9/255. Просмотрена пара с наибольшим средним расхождением, заметного смещения нет. Это выбранные кадры диагностической4-owner сцены, не полная анимация/приёмка v11.
- Helper build0errors/0warnings, Python7/7. Astra read-only correctness/Ponytail: PASS для одного transient native ABBA; лишнего refactor не требуется. Отрицательный source-Y отдельно не проверен. SocratiCode green60552/watcheractive; узкий semantic search экспериментальных shader не дал результата, файлы проверены прямо, переиндексации не было.

## Игровая проверка не выполнена

Подготовлен85-step ABBA297/289/289/297 на существующем неоплаченном доме,3440×1440, без screenshots/новых ресурсов/стройки/terrain. Candidate включается до ожидания Ready/погоды. Два `games_start` отказали на записи состояния GABS: `failed to publish runtime state: rename .runtime-*.tmp runtime.json: Access is denied`; второй клиент имел расширенный доступ. Повторения остановлены.

После ошибки checked `games_status` и actual CIM: Valheim отсутствует; GABS оставил starting/preflight claim без game PID, оба наших клиента закрыты. Claim/QA-подготовка/токены не сбрасывались. Три DLL восстановлены,97project hashes и все250config baseline hashes прежние. Сценарий/инвентарь/FPS в Valheim **не проверены**. Файловый overlay-тест настоящих Claim/Save GABS воспроизвёл Windows errno5 при удерживаемом os.Open reader: baseline и запись после Close прошли, при открытом reader — тот же Access is denied. Go открывает reader без FILE_SHARE_DELETE. Это подтверждает механизм конфликта, но не конкретного держателя в двух стартах. Defender1123 по этим файлам не найден. Это блок конкретного игрового измерения, не завершение тумана и не причина переписывать контроллер.

Следующий шаг: устранить подтверждённый конфликт чтения/atomic replacement в существующем GABS без ослабления ownership и выполнить один подготовленный native ABBA. Без практического ускорения или при нарушении краёв кандидат отклоняется. Ручную проверку ещё не назначать.

Доказательства: `artifacts/checkpoint-297/{frozen-source.json,volume.diff,Sampling-verification.json,Bundle-verification.json,Wall-verification.json,visual-deltas.json,native-scenario.json,prepare-native.py,qa-startup-audit.json}`; `artifacts/fog-longwall-explicit-297-probe/`; `ValheimQA/artifacts/20261022-090740/{mcp.jsonl,mcp-elevated.jsonl,restore.json}`. Сохранены compiler failure/71initial assertions и финальные75. Рабочие ошибки чтения Windows wildcard/os error123 и PowerShell ParserError «пустой элемент канала» исправлены; доказательства не засчитывались по этим отказавшим командам. `verify` skill отсутствует; выполненные проверки перечислены выше. FS-only test через Go overlay не менял GABS source/live claim/ACL: dependency cache сначала отказал GOPROXY=off, затем закреплённый golang.org/x/text v0.22.0 получен через официальный Go proxy; actual file test PASS, лог/errno в fs-only/result.json.
