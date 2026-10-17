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
