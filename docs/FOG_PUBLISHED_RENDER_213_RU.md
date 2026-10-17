# v11: опубликованное состояние в mesh и объёмном шейдере

2026-10-17 · [English](FOG_PUBLISHED_RENDER_213.md) · [Живой список](LIVE_CHECKLIST_RU.md)

**Проверен отдельный consumer-путь Unity, не игровой дом.** `BUILDWORKS_FOG_PUBLISHED` читает один64-byte GPU record для clock/body/actor/active; mesh vertex/fragment берут опубликованный surface atlas. Static shape и dynamic fields имеют отдельные размеры/offset. Owner/generation сравниваются как точные64-bit значения; sequence с CPU не сравнивается. Перед чтением проверяются реальные размеры/stride буферов, region и slot. Неверная привязка сохраняет свою статическую форму и исключает чужие поля.

| Проверка | Фактический результат |
| --- | --- |
| Unity6000.0.61f1 / D3D11 / Linear | Candidate bundle3 assets/8 kernels,88727B; shader errors0 |
| Mesh-only / volume-only / combined | 87 captures,62 comparisons,2 владельца полного96×80×80 domain |
| Dense oracle против GPU publication | Максимум среднего RGB9.8727858e-8; максимум локального канала0.00048828125 |
| Устаревшие CPU clock/body/actor/active | Разница0: подтверждение CPU удерживается до camera.Render |
| Metadata-only / отказ packing | Изменение capsule/fractional clock без шага; отказ сохраняет прежний кадр |
| Owner low/high, generation low/high, slot; region/короткие buffers | 26 отрицательных случаев совпадают со static fallback; сосед сохраняется |
| Отрицательный keyword / generation | Разница0.0034708318 /0.0021120982; контроль чувствителен |
| Установленный bundle против candidate без нового keyword | 16 captures:mesh/single/pair/atlas, idle/contact; попиксельная разница0 |
| CPU | Normal/QA/Editor C#0 warnings/0 errors;129 групп geometry;363 повреждённых квитанции/manifest отклонены |
| Независимое чтение | Correctness PASSED, Ponytail Ship; повторного GPU-запуска ревьюером не было |

Камера фиксирована: perspective49°,384×216,position(4,2.3,-5),target(0,1,1.1),roll0,Deferred/HDR/depth;фон(.15,.2,.3),без отдельного tone mapping и без луча. Две принадлежащие проекту процедурные стены(.2,2,2) имеют origins(0,0,0)/(0,0,2.5),цвет(.64,.31,.12). Исходные снимки, `comparison-sheet.png`, `verification.json` и `sources.json` локальны в `artifacts/fog-published-render/`; камеры/поля не подгонялись под результат. Это техническая проверка эквивалентности; внешний вид дома Ostrix ещё не принимал.

При расширении проверки обнаружены surface-analysis ошибка `Unexpected identifier StructuredBuffer` и legacy delta0.018276. Unity успешно завершала сборщик, несмотря на shader error. Ошибка устранена: StructuredBuffer скрыт только от surface-analysis parser; реальные GPU passes читают его. Теперь проверяются ShaderUtil messages и журнал, а manifest связывает7 исходных shaders/compute с bundle. Неверные source/bundle hash и manifest отклоняются до GPU-прогона. Неудачный промежуточный вариант не устанавливался.

Команды: `scripts/Build-FogGhostAssets.ps1 -CandidateOnly`, затем `scripts/Test-FogSimulation.ps1 -PublishedOnly`. Candidate не копируется в рабочий resource. SHA candidate `A9CD597CACB133047E9C6C7EE29B55FA9308E3D6677F7DAB753AB4F0514848CC`. Установленная QA .213 и её bundle неизменны; Valheim/миры/инвентарь не затронуты.

Следующий законченный шаг: runtime arena и split atlas в существующих Channels/Simulation/Scratch/Atlas, затем admission реальной геометрии и пространственный ledger вместо YZ-only. Полный domain, бюджет всех живых состояний и безопасный отказ обязательны. Производственный scheduler/render lifecycle, дом28, FPS, многопользовательский проход и постоянная табличка пока не проверены. Новый consumer ограничен текущим accumulated-v11 с одним actor/trail[0]; прежние варианты остаются для legacy путей. Другие графические API этим D3D11-прогоном не доказаны.

Независимое чтение также обнаружило пропуск input/state при копировании C# сборщиком assets. Оба файла добавлены и сверяются по SHA; CPU-тест выполняет реальный цикл копирования в пустую папку. Оставшиеся от прежних запусков RuntimeSources не требуются.

### Runtime: явное заимствование полей

`BlueprintFogSimulation.StagedState` заменяет reflection-подмену временных полей и ledger в fixed-pool probe. Расчёт проверяет размеры/формат/UAV/Trilinear/noMip/ClampU/V/W и отсутствие alias до GPU-записей. Заимствованные поля не выделяются повторно и не освобождаются расчётом; стадия не может разрешить recovery по общему ledger. Committed velocity/ledger остаются у владельца пула. Обычный constructor/Dispose сохранены. `BlueprintFogScratch(includeReadFields:true)` единолично владеет всеми9 полями рабочего набора44236800B; прежние callers получают6 полей как раньше. Probe не освобождает read-поля отдельно.

Проверено:11 неверных наборов ресурсов отклонены без изменения sentinel; valid Dispose сохраняет borrowed ресурсы. GPU physics664/498/28,239commit/2отказа/242metadata;Frame236 кадра54/232/174/20/54;render87/62/26+legacy16/delta0; прежние Scratch360/36 и Recovery24 прошли на одном source SHA. Normal/QA/Editor0/0,129 групп,371 отрицательная CPU-проверка, независимые PASSED/Ship. Квитанции и fingerprints локальны в `artifacts/fog-borrow-213/`.

Это рабочий API расчёта, используемый проверкой, **не готовый игровой allocator/scheduler**. Сам pool пока остаётся Editor-стендом. Полная память дома, source admission, пространственный ledger и FPS ещё открыты; installed .213/bundle неизменны.

### Runtime GPU-core

`BlueprintFogArena` теперь владеет maps/free-list/payload/control/receipts/metadata, общим9-field workspace и двумя dynamic atlases; binding/dispatch и metadata publication извлечены из Probe без изменения kernels. Сам Probe вызывает runtime-код и продолжает владеть committed velocity/ledger, очередями, callback и dense oracle. Нормальные сборки не содержат UnityEditor-зависимости этого core. Core требует, чтобы владелец сначала завершил readbacks, затем освободил арену; существующий Probe соблюдает этот порядок.

CPU-резерв теперь включает опубликованные metadata64B/owner:8/24859blocks=98303696B проходит,8/24860 превышает98304000B; округлённые24576=98157668B. Для проверенных2 владельцев/8192blocks резерв58779980B включает отдельные committed fields/ledger; это не замер всей памяти дома. Dynamic atlas проверяется отдельно против128MiB и максимума3D-текстуры; static source/driver memory не включены. Неверный размер/превышение бюджета отклоняются до выделения, metadata slot проверяется до dispatch.

Финальный GPU:physics239commit/2отказа/664field/498ledger/28atlas/242metadata;4 неверных размера/бюджета и2 slot-случая плюс11 borrowing-отказов прошли. Frame227/54commit/232/174/20/54;render87/62/26+legacy16delta0;Scratch360/36,Recovery24. Normal/QA/Editor0/0,129 групп/374 CPU-отказа, независимые PASSED/Ship. Все5 режимов привязаны к одинаковым Arena/Scratch/Simulation SHA; локальный архив `artifacts/fog-arena-runtime-213/`.

Первый запуск финальной серии не состоялся: auto-review превысил deadline. Единственный повтор с ограничением запуска до17:56UTC прошёл целиком. Это не ошибка физического расчёта. Установленная .213/bundle и миры не менялись.

**На этапе GPU-core ещё не были подключены:** создание core из `BlueprintFogChannels`, channel lifetime/callback scheduler, упаковка и загрузка двух дополнительных compute-программ в production bundle. Тогда программы предоставлял Editor-стенд; подготовка bundle описана ниже. Split static source layout, admission реальных28 деталей, пространственный ledger, дом/FPS и табличка остаются открытыми. Runtime GPU-core не означает готовое игровое строительство.

### Runtime compute assets: подготовка без GPU

Две owned compute-программы перенесены из Editor в `src/BuildWorks/Assets/FogGhostRuntime/FogArena.compute` и `FogSnapshotMetadata.compute`; compute-файлы побайтно сохранены, GUID meta сохранены; один meta приведён к LF для Git-проверки. `BlueprintFogAssetLayout` — единый точный список3 legacy/5 current assets и9 исходников. Loader принимает только полный набор, проверяет наличие и размеры рабочих групп8+17+2 ядер, сохраняет загрузку старого3-asset bundle. Arena больше не дублирует перечень17 ядер. QA возвращает фактическое число загруженных assets/ядер.

Builder готовит5 assets/27 ядер с отдельным candidate-выходом, проверкой публикации metadata/отказа/reload. Physics/frame/render стенды берут обе программы из bundle; синтетический8³ gate использует canonical source. Staging и fingerprints охватывают новые исходники; старый3-asset candidate не может подтвердить этот этап. Рабочий bundle не заменялся.

Найдена ошибка очистки: один dispatch64 потоков оставлял metadata владельцев5–8 неинициализированными. Runtime теперь рассчитывает число групп по всем16 словам каждого владельца. CPU-регрессия исполняет фактическое C# выражение и сверяет покрытие с шириной настоящего HLSL-ядра для1–32 владельцев. Старый двухвладельческий GPU gate не покрывал эту ошибку.

Проверены130 CPU-групп,377 повреждённых отчётов/manifest и32 случая покрытия; clean staging проверен на пустой папке. Normal/QA/offline Editor C#0/0; все19 compute-ядер скомпилированы fxc без GPU. Локальные доказательства: `artifacts/fog-assets-runtime-213/`. **GPU build/load/reload нового bundle, новые physics/frame/render и Valheim не запускались**: разрешённое часовое окно закончилось. Это подготовка production loader, а не готовый игровой backend. Далее новый candidate GPU gate, Channels/callback/split source atlas; admission28/spatial ledger/дом/FPS и табличка остаются открыты. Установленная .213 и встроенный3-asset bundle неизменны.

### Runtime Channels: очередь, подтверждения и освобождение

Собран отдельный Linear candidate5 assets/27 ядер,93496байт, SHA `D14FE97D44AA6DB7296A52C4FC2F2C362719CB090D76632A8A45B821CC62FB13`: загрузка/публикация/отказ/reload прошли наD3D11. Это owned shader assets; рабочий3-asset bundle не заменён.

`BlueprintFogChannels.CreateArena` — явный strict-путь рядом с сохранённым legacy API. Channels владеет full-field snapshot, committed velocity/ledger, staged solver, очередью, generation/bank и текущим native readback. Callback копирует receipt, Poll подтверждает банк/очередь без ожидания и чтения просроченных native данных. После отказа speculative CPU history не используется повторно; unknown и allocator flag4 блокируют pool до полного teardown. Повторная выдача чужого solver не передаёт владение; реальные borrowed ссылки проверяются.

Retirement отдельно подтверждается GPU-копией Control: до неё слот и ресурсы удерживаются, повторный free/reuse запрещён. Чужой последующий packing failure не подменяет её результат. Dispose закрывает ввод, дожидается только живых requests и отдельного GPU fence, затем освобождает владельцев и core. Неуспешный drain удерживает ресурсы и допускает повторный Dispose. Нормальный Poll не блокирует; teardown и частные сравнения могут ждать GPU.

`FogArenaPhysicsProbe` больше не имеет своих candidate/bank/generation lifecycle. Frames/render подают ввод через CaptureSnapshot; metadata observer и dense oracle остаются проверками.38 GPU-случаев включают ненулевой начальный clock, duplicate/wrong factory и reentry, metadata-only без зависимости от observer, pending retirement/reuse/поздний callback, повреждённые ID/map/status/null, allocator4, pending Dispose, initialization/release fault и failed-drain/retry. Прежние обязательные сравнения не удалены.

Финал: physics664field/498ledger/28atlas,240commit/2отказа/242metadata;239 настоящих кадров Unity,54commit/232field/174ledger/20atlas/54metadata;render87кадров/62сравнения/26отрицательных случаев,16legacy delta0, mean9.8727858e-8/maxlocal0.00048828125, CPU-poison0. Дополнительный commit к прежним239 — zero-step metadata теперь проходит ту же подтверждаемую очередь, вместо частного обхода без receipt. Legacy Channels6 сравнений, Scratch360/36 и Recovery24 прошли на тех же5 runtime SHA. Normal/QA/offline Editor0/0;130 CPU-групп/391 повреждённая квитанция/manifest и32 coverage. Локальные proofs/fingerprints: `artifacts/fog-channels-213/`; первая проверка bundle до переноса — `artifacts/fog-bundled-213/`.

**Граница:** это проверенный runtime Channels, вызываемый из Unity-стендов. Игровые Visual/Atlas пока не переключены. Recovery из committed ledger, split static source/dynamic atlas, admission реальных28 деталей, пространственный ledger, дом/FPS и постоянная табличка ещё открыты. Частные проверки могут выполнять блокирующие сравнения и не измеряют пропускную способность игрового scheduler. Valheim не запускался; .213 и встроенный bundle неизменны.

### Committed recovery: возврат подтверждённого владельца

`TryGetRecoveredSnapshot` читает только подтверждённый `Ledger[2]` своего владельца, независимо от transaction/retirement receipts. Скопированный ответ действителен только для всех пяти полей `LastSubmission` и пустой готовой очереди без ошибок. Новый ввод, pending, новая sequence, retirement или неопределённость pool не разрешают восстановление. Ошибочный/неверный readback возвращает false и удерживает владельца; после нового подтверждённого состояния можно запросить наблюдение снова. Сам запрос не удаляет и не освобождает эффект.

Retirement удерживает ledger, пока его recovery readback ещё выполняется. Dispose дожидается этого запроса перед освобождением. Обычный запрос не блокирует и не читает просроченные native handles. Формулы, физические размеры и пороги утверждённого v11 не менялись.

Настоящие D3D11-проверки: Recovery48 кадров проверяет сохранение ответа после истечения native handle, независимость от общего workspace, новый capture/submit во время чтения, старую sequence, новую generation, pending retirement/Dispose, дым без поверхности=2/поверхность без дыма=1 и ошибочный readback. Отдельный маршрут двух настоящих стен сначала обнаруживает активное состояние, затем нулевые committed ledger обоих на480 ticks (16секунд симуляции); поля, ledger и опубликованные atlas точно совпадают с независимыми dense solvers. Начальное пустое состояние не может завершить этот маршрут. Прежние счётчики physics сохраняются:240commit/2безопасных отказа,664field/498ledger/28atlas/242metadata; маршрут полного возврата добавлен отдельно.

Frames и сравнение изображения прошли повторно:87кадров/62сравнения/26отрицательных случаев,16legacy попиксельно точны, mean9.8727858e-8/maxlocal0.00048828125. Normal/QA/offline Editor0предупреждений/0ошибок,130CPU-групп/449 повреждённых отчётов/32metadata-покрытия; независимые correctness/Ponytail: PASSED/Ship. Локальные исходные протоколы и runtime fingerprints: `artifacts/fog-snapshot-recovery-213/`.

**Граница:** runtime Channels вызывают изолированные Unity-проверки. Игровые Visual/Atlas пока используют прежний путь; spatial ownership дома, source layout/admission, игровой FPS и постоянный проект/табличка не доказаны. Установленная .213, рабочий встроенный bundle и все игровые сохранения неизменны. Valheim не запускался.

### Runtime Visual/Atlas: раздельные источники и поля

`BlueprintFogAtlas(assets, snapshots)` владеет только неизменяемым RHalf source atlas и bindings. Два dynamic atlas и опубликованные metadata остаются у sparse pool; покадровых копий полей и дополнительных dynamic textures нет. Static packing выполняет одно новое owned ядро `PackStaticSource`; формулы solver и замороженные эталоны не менялись. Candidate5 assets/28ядер,93713байт, SHA `9E9A864C2D32F0E70088FA756DA9A27266FBE6ED99614D63113D658F91D993C8`, прошёл load/публикацию/отказ/reload.

`PreparePublished` выполняется до geometry камеры; Render отклоняет неподготовленное или изменённое соответствие. Точные handles pool превращаются в bindings slot/64-bit identity/generation. Дубли, чужие, устаревшие handles и неверное число отклоняются. Удалённым из mapping Visual и при Dispose возвращается обычный material-путь до освобождения bindings. Заимствованные текстуры pool не освобождаются. Для RHalf layout из128MiB вычитается резерв dynamic atlas; превышение отклоняется до allocation, старый atlas удерживается.

Именно runtime Visual/Atlas, а не дублирующая настройка shaders в стенде, прошёл30 кадров/14 сравнений/7отрицательных случаев против независимого dense Visual/Atlas. Маршруты и капсулы владельцев различаются. Dormant/initial/active, geometry до CPU-confirmation, metadata-only, packing rejection и reorder прошли; намеренно переставленные handles дают видимую разницу0.00239947. Правильный максимум mean7.03359e-8/local0.00048828125. Static source пакуется только дважды на обновлениях; для двух владельцев static8122272B + borrowed dynamic21086464B =29208736B. Source/pigment, поля solver, driver и oracle сюда не входят; это не память/FPS дома.

Прежние87/62/26 и16 попиксельно точных legacy-кадров сохранены. Physics240/2/664/498/28/242, настоящие кадры, Recovery48 (реальное восстановление480ticks) и прежний Atlas suite прошли на соответствующих runtime sources. Normal/QA/offline Editor0/0;130CPU-групп/501 повреждённый отчёт/32coverage. Независимое чтение сначала отклонило симметричную mapping-фикстуру; теперь воздействия различаются и есть чувствительный отрицательный контроль, PASSED/Ship. Исходные протоколы: `artifacts/fog-render-wiring-213/`.

**Следующая граница:** очередь логической детали должна удерживать принятые наблюдения, пока физический slot ожидает/освобождается, и воспроизводить их в новом поколении. Постоянный ввод не должен бесконечно делать recovery устаревшим; повторный вход не отменяет retirement и не стирает принятый путь. Этот scheduler и включение Building данным renderer-изменением не реализованы. Admission28/пространственный ledger/дом/FPS и постоянный проект/табличка открыты. Установленная .213/рабочий bundle и сохранения неизменны; Valheim не запускался.

### Logical source: очередь детали и поколения

`SnapshotSource` хранит одну detachable `BlueprintFogInputQueue` независимо от физического `SnapshotChannel`. Регистрация начинается в dormant-состоянии; принятый контакт ждёт свободный slot. Factory запускается на подтверждённом baseline очереди, а не времени/позиции позднего allocation. Attach выполняется только после успешной GPU-initialization. Физический State остаётся неизменным при перепривязке очереди к следующей generation; старые callbacks/handles не подтверждают новый ввод.

Capture продолжается во время recovery и retirement. Pump сначала подтверждает все физические release, затем выдаёт slots начиная после последнего успешного allocation: прежний источник не перехватывает свой slot у ожидающего. Recovery временно приостанавливает submissions, сохраняя capture. Контакт в хвосте отменяет barrier; если хвост пуст, последний подтверждённый контакт удерживает владельца. Восстановившаяся generation может завершиться с принятым noncontact-хвостом; он переживает release и проигрывается следующей generation. Новое поколение сначала проигрывает хвост, затем проверяет recovery, поэтому не зацикливается на начальном пустом состоянии. Overflow/unknown сохраняют ошибку и принятые данные.

Release callback источника выполняется только после подтверждённого физического release либо полного pool drain. Независимое чтение нашло orphan-путь: ошибка GPU-initialization оставляла Source.Snapshot null при сохранённом физическом владельце. RemoveSource теперь откладывает callback при необходимости pool teardown. Injected fault проверяет отсутствие раннего закрытия и ровно один callback после повторного Dispose.

Настоящий frame-gate: 105общих/57source-кадров; два источника/один slot, повторный вход во время recovery и retirement, выдача ожидающему, baseline/path replay, удержание контакта, idle-хвост, pending removal, закрытый source и failed acquisition. Отдельный физический gate ставит8 наблюдений реальной стены за занятым slot, запускает factory на исходном clock0, воспроизводит их без изменения и точно сравнивает4 поля/3 ledger с dense solver; состояние обязательно ненулевое. Прежние physics240/2/664/498/28/242 и полный возврат480ticks, frames/render30/14/7+87/62/26/legacy16delta0 сохранены. Normal/QA/offline Editor0/0,131CPU-группа/533 повреждённых отчёта/32coverage; независимые correctness/Ponytail PASSED/Ship. Протоколы: `artifacts/fog-logical-sources-213/`; семь runtime/input fingerprints совпадают в четырёх GPU-режимах.

**Граница:** принятый non-emitting хвост проигрывается, не стирается, и иногда требует нового slot; cold-state fast-forward и скорость ещё не доказаны. Logical Source пока не подключён к Visual/Building. Следом владение source textures до GPU drain, geometry binding до draw, непрерывные dormant-часы, admission настоящего дома28/full domain/пространственный ledger и вид/FPS; затем постоянная табличка/материалы/reload/сервер. Игра, установленная DLL, рабочий bundle и сохранения не менялись.

### Logical Visual и блокер непрерывного ожидания

Visual распознаёт строгий pool, регистрирует один logical source и записывает неизменяемые samples без прямого продвижения общего solver. Factory удерживает source textures, bounds и noise frame независимо от жизни Visual. Удаление Visual скрывает геометрию, но откладывает освобождение текстур/mesh до подтверждённого release либо pool drain. С pending-работой проверены оба порядка закрытия. Строгий Visual отклоняет compact physical domain и прежние volume/atlas-пути; published mapping обязан принадлежать той же детали и pool. Завершивший recovery источник отображается как dormant, пока заканчивается физический release.

Точный unassigned tuple разрешает только собственные CPU clock/body, когда GPU-slot отсутствует. Ошибочные owner/generation/region/stride/короткие буферы остаются неактивными и не выбирают этот fallback. Назначенная деталь продолжает читать подтверждённые GPU metadata даже при новом принятом CPU-вводе. Исправлен возврат часов шума к нулю после unassignment. Проверка dormant охватывает accumulated geometry и GhostClear.

Фактический Unity GPU:8 сравнений mesh/volume, максимум mean1.2938868361736031e-8 и local0.0001220703125; задержанное подтверждение/новое CPU-тело, отложенное освобождение source, оба порядка Dispose, отказы mapping/compact/legacy. Сохранены30/14/7,87/62/26 и16 legacy delta0. Candidate5assets/28kernels,100866B, SHA AE54E1539481B664AAB3710A634CB5E5B60A69A352F84E0DF5DC7D16FC89C8F5. Normal/QA/Editor0предупреждений/0ошибок;131CPU-группа,574 повреждённых отчёта/32coverage. Correctness PASSED/Ponytail Ship относятся только к этому этапу исходников. Протоколы и hashes: `artifacts/fog-logical-visual-213/`.

**Обязательная проверка сходства ещё не проходит:** после3.2с ожидания первый контакт на3.4с отличается от непрерывного v11 на0.11407470703125 в локальном RGB-канале. AdvanceVelocity переносит/затухает прежнюю velocity и добавляет curl/подъём даже при Emit=false. TrackDormant хранит последнее наблюдение, но не историю этого течения; новая generation начинает с нулевой velocity. После retirement/reentry риск тот же. Прошедший эталон интеграции явно запускается на том же baseline3.2с; он не заменяет проваленный непрерывный контроль. В отчёте остаются continuousV11Parity=false и continuousIdleControlSensitive=true; допуски не расширены. Building не переключён, backend не установлен.

Далее: сохранить ограниченную динамику idle/reentry без потери истории; затем доказать full-domain admission настоящих28 деталей, пространственное владение источниками и вид/FPS дома. Простое увеличение числа плотных полей не должно обходить лимит:28 velocity требуют137625600B ещё до workspace. Общее глобальное idle-поле — лишь предложение до доказательства эквивалентности разных границ/lattice phase. Постоянный проект/табличка/материалы/reload/сервер следуют после принятого визуала. Valheim и сохранения не запускались.
